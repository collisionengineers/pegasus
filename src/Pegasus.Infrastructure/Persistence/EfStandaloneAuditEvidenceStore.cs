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
    : IRecordAutomaticStandaloneAuditEvidence, IStandaloneAuditEvidenceQueries
{
    private const string AutomaticActor = "system-worker:automatic-standalone-audit";

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
        RecordAutomaticStandaloneAuditEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IntakeReceiptId == Guid.Empty || request.OriginalReportAssetId == Guid.Empty)
        {
            throw new ArgumentException("Automatic Audit evidence requires receipt and report identities.", nameof(request));
        }
        if (request.ExpectedIntakeVersion < 0 || !Enum.IsDefined(request.Assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        var operationKey = $"automatic-standalone-audit:{request.IntakeReceiptId:N}";
        var reason = $"The retained original report states {request.Assessment}.";
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{request.IntakeReceiptId:N}|{request.OriginalReportAssetId:N}|{request.Assessment}"))).ToLowerInvariant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await context.Set<StandaloneAuditEvidenceEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IntakeReceiptId == request.IntakeReceiptId, cancellationToken);
        if (existing is not null)
        {
            if (existing.OriginalReportAssetId != request.OriginalReportAssetId
                || !string.Equals(existing.Assessment, ToCode(request.Assessment), StringComparison.Ordinal))
            {
                throw new StandaloneAuditEvidenceConflictException(request.IntakeReceiptId);
            }
            return Map(existing, isDuplicate: true);
        }

        var receipt = await context.IntakeReceipts.SingleOrDefaultAsync(
            item => item.Id == request.IntakeReceiptId, cancellationToken)
            ?? throw new InvalidOperationException("The intake receipt does not exist.");
        if (receipt.Version != request.ExpectedIntakeVersion)
        {
            throw new DbUpdateConcurrencyException(
                "The intake receipt changed before automatic original-report evidence could be recorded.");
        }
        var originalReport = await context.IntakeAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.OriginalReportAssetId && item.IntakeReceiptId == request.IntakeReceiptId,
            cancellationToken)
            ?? throw new InvalidOperationException("The classified original Engineer report is not a retained asset.");
        await RequireRetainedOriginalReportAsync(originalReport, cancellationToken);

        var recordedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        receipt.Version++;
        var evidence = new StandaloneAuditEvidenceEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            OriginalReportAssetId = originalReport.Id,
            Assessment = ToCode(request.Assessment),
            ConfirmedByKind = nameof(ActorKind.SystemWorker),
            ConfirmedBySubjectId = AutomaticActor,
            ConfirmedByRolesJson = "[]",
            ConfirmedAtUtc = recordedAtUtc,
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
            EventType = "standalone_audit_evidence_recorded",
            Actor = AutomaticActor,
            OccurredAtUtc = recordedAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                evidenceId = evidence.Id,
                originalReportAssetId = evidence.OriginalReportAssetId,
                assessment = evidence.Assessment,
                source = "literal_original_report"
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

    private static StandaloneAuditEvidence Map(
        StandaloneAuditEvidenceEntity entity,
        bool isDuplicate)
    {
        var staffId = Guid.Empty;
        if (string.Equals(entity.ConfirmedByKind, nameof(ActorKind.Staff), StringComparison.Ordinal))
        {
            if (!Guid.TryParse(entity.ConfirmedBySubjectId, out staffId) || staffId == Guid.Empty)
            {
                throw new InvalidDataException(
                    "The retained standalone Audit evidence has an invalid confirming staff identity.");
            }
        }
        else if (!string.Equals(entity.ConfirmedByKind, nameof(ActorKind.SystemWorker), StringComparison.Ordinal)
            || !string.Equals(entity.ConfirmedBySubjectId, AutomaticActor, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The retained standalone Audit evidence has an invalid recording identity.");
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
