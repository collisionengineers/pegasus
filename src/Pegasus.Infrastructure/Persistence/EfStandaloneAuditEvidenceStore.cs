using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfStandaloneAuditEvidenceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IIntakeArtifactStore artifactStore,
    TimeProvider timeProvider)
    : IConfirmStandaloneAuditEvidence, IStandaloneAuditEvidenceQueries
{
    public async Task<StandaloneAuditEvidence?> GetForReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        if (intakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "An intake receipt identifier is required.",
                nameof(intakeReceiptId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var evidence = await context.Set<StandaloneAuditEvidenceEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == intakeReceiptId,
                cancellationToken);
        return evidence is null ? null : Map(evidence, isDuplicate: false);
    }

    public async Task<StandaloneAuditEvidence> ExecuteAsync(
        ConfirmStandaloneAuditEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var confirmingStaffId = StandaloneAuditEvidencePolicy.ValidateConfirmation(request);
        var operationKey = request.OperationKey.Trim();
        var reason = request.Reason.Trim();
        var requestHash = RequestHash(request, operationKey, reason);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.Set<StandaloneAuditEvidenceEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == request.IntakeReceiptId,
                cancellationToken);
        if (existing is not null)
        {
            EnsureExactReplay(existing, request, operationKey, requestHash);
            return Map(existing, isDuplicate: true);
        }

        var receipt = await context.IntakeReceipts
            .SingleOrDefaultAsync(
                item => item.Id == request.IntakeReceiptId,
                cancellationToken)
            ?? throw new InvalidOperationException("The intake receipt does not exist.");
        if (receipt.Version != request.ExpectedIntakeVersion)
        {
            throw new DbUpdateConcurrencyException(
                "The intake receipt changed before its original-report evidence could be confirmed.");
        }
        // This read `draft_ready` — the decision code removed with the manual
        // acceptance gate. No receipt written since carries it, so standalone
        // Audit evidence could not be confirmed for anything at all, and the
        // Audit branch of case creation was unreachable. The rule it meant to
        // state is the one acceptance itself applies: only pre-case material
        // can carry the evidence that turns it into a case.
        if (!IntakeDecisionPolicy.CanBecomeCase(
                EfIntakeReceiptStore.ParseDecision(receipt.Decision)))
        {
            throw new InvalidOperationException(
                "Standalone Audit evidence can be confirmed only for an item that can still become a case.");
        }

        var originalReport = await context.IntakeAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == request.OriginalReportAssetId
                    && item.IntakeReceiptId == request.IntakeReceiptId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected original Engineer report is not a retained asset of this intake receipt.");
        await RequireRetainedOriginalReportAsync(originalReport, cancellationToken);

        var confirmedAtUtc = timeProvider.GetUtcNow();
        if (confirmedAtUtc.Offset != TimeSpan.Zero)
        {
            confirmedAtUtc = confirmedAtUtc.ToUniversalTime();
        }

        receipt.Version++;
        var evidence = new StandaloneAuditEvidenceEntity
        {
            Id = request.EvidenceId,
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            OriginalReportAssetId = originalReport.Id,
            Assessment = ToCode(request.Assessment),
            ConfirmedByKind = request.Actor.Kind.ToString(),
            ConfirmedBySubjectId = request.Actor.SubjectId,
            ConfirmedByRolesJson = RolesJson(request.Actor),
            ConfirmedAtUtc = confirmedAtUtc,
            OperationKey = operationKey,
            Reason = reason,
            RequestHash = requestHash,
            ResultingReceiptVersion = receipt.Version
        };
        context.Set<StandaloneAuditEvidenceEntity>().Add(evidence);
        context.IntakeReceiptEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            EventType = "standalone_audit_evidence_confirmed",
            Actor = confirmingStaffId.ToString("D"),
            OccurredAtUtc = confirmedAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                evidenceId = evidence.Id,
                originalReportAssetId = evidence.OriginalReportAssetId,
                assessment = evidence.Assessment,
                reason = evidence.Reason,
                resultingReceiptVersion = evidence.ResultingReceiptVersion
            })
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(evidence, isDuplicate: false);
    }

    private async Task RequireRetainedOriginalReportAsync(
        IntakeAssetEntity asset,
        CancellationToken cancellationToken)
    {
        if (asset.Kind is not ("source" or "attachment")
            || asset.ContentLength <= 0
            || string.IsNullOrWhiteSpace(asset.StorageKey)
            || asset.ContentHash.Length != 64
            || asset.ContentHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidOperationException(
                "Standalone Audit evidence must identify a retained source or attachment selected as the original Engineer report.");
        }

        var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected original Engineer report is no longer available in retained intake custody.");
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        if (content.Length != asset.ContentLength
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(asset.ContentHash)))
        {
            throw new IntakeArtifactIntegrityException();
        }
    }

    private static void EnsureExactReplay(
        StandaloneAuditEvidenceEntity evidence,
        ConfirmStandaloneAuditEvidenceRequest request,
        string operationKey,
        string requestHash)
    {
        if (evidence.Id != request.EvidenceId
            || !string.Equals(evidence.OperationKey, operationKey, StringComparison.Ordinal)
            || !FixedTimeHashEquals(evidence.RequestHash, requestHash))
        {
            throw new StandaloneAuditEvidenceConflictException(request.IntakeReceiptId);
        }
    }

    private static StandaloneAuditEvidence Map(
        StandaloneAuditEvidenceEntity entity,
        bool isDuplicate)
    {
        if (!string.Equals(entity.ConfirmedByKind, nameof(ActorKind.Staff), StringComparison.Ordinal)
            || !Guid.TryParse(entity.ConfirmedBySubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The retained standalone Audit evidence has an invalid confirming staff identity.");
        }

        return new(
            entity.Id,
            entity.IntakeReceiptId,
            entity.OriginalReportAssetId,
            ParseAssessment(entity.Assessment),
            staffId,
            entity.ConfirmedAtUtc,
            entity.Reason,
            entity.ResultingReceiptVersion,
            isDuplicate);
    }

    private static string RequestHash(
        ConfirmStandaloneAuditEvidenceRequest request,
        string operationKey,
        string reason)
    {
        var material = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            request.EvidenceId,
            request.IntakeReceiptId,
            request.ExpectedIntakeVersion,
            request.OriginalReportAssetId,
            assessment = ToCode(request.Assessment),
            actorKind = request.Actor.Kind.ToString(),
            actorSubjectId = request.Actor.SubjectId,
            actorRoles = request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            operationKey,
            reason
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static string RolesJson(ActionActor actor) => JsonSerializer.Serialize(
        actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray());

    private static bool FixedTimeHashEquals(string left, string right) =>
        left.Length == 64
        && right.Length == 64
        && left.All(char.IsAsciiHexDigit)
        && right.All(char.IsAsciiHexDigit)
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));

    private static string ToCode(AuditAssessment assessment) => assessment switch
    {
        AuditAssessment.Repairable => "repairable",
        AuditAssessment.TotalLoss => "total_loss",
        _ => throw new ArgumentOutOfRangeException(nameof(assessment))
    };

    private static AuditAssessment ParseAssessment(string assessment) => assessment switch
    {
        "repairable" => AuditAssessment.Repairable,
        "total_loss" => AuditAssessment.TotalLoss,
        _ => throw new InvalidDataException(
            $"Unknown retained Audit assessment '{assessment}'.")
    };
}
