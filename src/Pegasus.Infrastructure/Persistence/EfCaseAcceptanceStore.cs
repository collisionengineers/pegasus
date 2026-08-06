using System.Diagnostics;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseAcceptanceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null,
    IEnumerable<Pegasus.Core.Intake.IProviderCaseMatchPolicy>? caseMatchPolicies = null)
    : ICaseAcceptanceStore
{
    private static readonly TimeZoneInfo LondonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public async Task<CaseAcceptanceOutcome> AcceptAsync(
        CaseAcceptanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PrincipalCode);
        if (request.Actor.Kind is not (ActorKind.Staff or ActorKind.SystemWorker))
        {
            throw new ArgumentException(
                "Case acceptance requires a staff or system-worker actor.",
                nameof(request));
        }
        ArgumentNullException.ThrowIfNull(request.Completeness);
        ArgumentNullException.ThrowIfNull(request.CompletenessEvaluation);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CompletenessEvaluation.PolicyKey);
        if (request.CompletenessEvaluation.PolicyVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The completeness policy version must be positive.");
        }
        if (request.IntakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt is required for case acceptance.", nameof(request));
        }

        if (request.ExpectedIntakeVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The expected intake version cannot be negative.");
        }

        if (!Enum.IsDefined(request.CaseType))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The case type is invalid.");
        }
        if (request.CaseType == CaseType.Audit
            && (request.StandaloneAuditEvidenceId is null
                || request.StandaloneAuditEvidenceId == Guid.Empty))
        {
            throw new ArgumentException(
                "A standalone Audit requires retained original-report evidence.",
                nameof(request));
        }
        if (request.CaseType != CaseType.Audit && request.StandaloneAuditEvidenceId is not null)
        {
            throw new ArgumentException(
                "Only a standalone Audit can link standalone Audit evidence.",
                nameof(request));
        }
        if (request.Actor.SubjectId.Length > 200)
        {
            throw new ArgumentException("The case acceptance actor subject cannot exceed 200 characters.", nameof(request));
        }
        var reason = request.Reason.Trim();
        if (reason.Length > 500)
        {
            throw new ArgumentException("The case acceptance reason cannot exceed 500 characters.", nameof(request));
        }

        if (request.OperationKey.Length > 100)
        {
            throw new ArgumentException("The case acceptance operation key cannot exceed 100 characters.", nameof(request));
        }

        if (request.PrincipalCode.Trim().Length > 20)
        {
            throw new ArgumentException("The principal code cannot exceed 20 characters.", nameof(request));
        }

        request = request with
        {
            OperationKey = request.OperationKey.Trim(),
            Reason = reason
        };
        var principalCode = QdosAlphaCaseActivationPolicy.RequireActivatedPrincipal(
            request.PrincipalCode);
        var command = CreateAcceptanceCommand(request, principalCode);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await AcceptOnceAsync(request, principalCode, command, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                var duplicate = await FindAcceptedAsync(request, principalCode, command, cancellationToken);
                if (duplicate is not null)
                {
                    return duplicate with { IsDuplicate = true };
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<CaseAcceptanceOutcome> AcceptOnceAsync(
        CaseAcceptanceRequest request,
        string principalCode,
        AcceptanceCommand command,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingLink = await context.CaseIntakeLinks
            .AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.IntakeReceiptId == request.IntakeReceiptId, cancellationToken);
        if (existingLink is not null)
        {
            EnsureExactReplay(existingLink, request, principalCode, command);
            return Map(existingLink.Case, existingLink.CustodyWorkId, true);
        }
        CaseDataPolicy.ValidateCompleteness(request.Completeness);

        var receipt = await context.IntakeReceipts
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
            .SingleOrDefaultAsync(item => item.Id == request.IntakeReceiptId, cancellationToken)
            ?? throw new InvalidOperationException("The intake receipt does not exist.");
        if (receipt.Version != request.ExpectedIntakeVersion)
        {
            throw new DbUpdateConcurrencyException("The intake receipt changed before it could be accepted.");
        }

        // Two decisions can produce a case, and they are the two the business
        // recognises: a definitive instruction, which processing allocates
        // automatically, and material a person has sorted (INT-26). Anything
        // else - blocked, unreadable, unsupported, an image registration - is
        // refused here, so the fail-closed boundary does not depend on which
        // caller asked.
        //
        // `draft_ready` is the legacy code for the definitive outcome, kept
        // readable so receipts written before the acceptance gate was removed
        // still resolve.
        if (receipt.Decision is not ("case_created" or "draft_ready" or "needs_sorting"))
        {
            throw new InvalidOperationException(
                "Only a definitive instruction or an item that needs sorting can become a case.");
        }
        var standaloneAuditEvidence = await ResolveStandaloneAuditEvidenceAsync(
            context,
            request,
            cancellationToken);
        var standaloneAuditAssessment = standaloneAuditEvidence is null
            ? (AuditAssessment?)null
            : ParseAuditAssessment(standaloneAuditEvidence.Assessment);

        var principal = await context.Principals
            .SingleOrDefaultAsync(
                item => item.Code == principalCode && item.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException($"The active principal '{principalCode}' does not exist.");
        if (!string.Equals(
                principal.InspectionMode,
                ProviderInspectionModePolicy.ToCode(request.ProviderInspectionMode),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The provider inspection-mode setting changed while the intake was being accepted. Reload and retry.");
        }

        var acceptedAtUtc = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        var year = TimeZoneInfo.ConvertTime(acceptedAtUtc, LondonTimeZone).Year;
        var sequence = await context.CaseSequences.SingleOrDefaultAsync(
            item => item.SequenceLineageId == principal.SequenceLineageId && item.Year == year,
            cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity
            {
                SequenceLineageId = principal.SequenceLineageId,
                Year = year,
                LastAllocatedSequence = 0
            };
            context.CaseSequences.Add(sequence);
        }

        if (sequence.LastAllocatedSequence >= 999)
        {
            throw new CaseIdentitySequenceExhaustedException(principal.Code, year);
        }

        var allocatedSequence = ++sequence.LastAllocatedSequence;
        var reference = $"{principal.Code}{year % 100:00}{allocatedSequence:000}";
        var auditReference = standaloneAuditAssessment is { } assessment
            ? AuditIdentity.Create(reference, assessment)
            : null;
        var caseId = Guid.NewGuid();
        var custodyWorkId = Guid.NewGuid();
        var initialState = request.CompletenessEvaluation.SatisfiesPolicy
            ? CaseInitialState.Review
            : CaseInitialState.NotReady;

        var caseEntity = new CaseEntity
        {
            Id = caseId,
            PrincipalId = principal.Id,
            Principal = principal,
            SequenceLineageId = principal.SequenceLineageId,
            Year = year,
            Sequence = allocatedSequence,
            Reference = reference,
            AuditReference = auditReference,
            Type = ToCode(request.CaseType),
            InitialState = ToCode(initialState),
            CustodyState = ToCode(CaseCustodyState.Pending),
            OriginIntakeReceiptId = receipt.Id,
            StandaloneAuditAssessment = standaloneAuditAssessment is null
                ? null
                : ToCode(standaloneAuditAssessment.Value),
            StandaloneAuditEvidenceId = standaloneAuditEvidence?.Id,
            AcceptedInspectionDeadline = request.AcceptedInspectionDeadline,
            InstructionComplete = request.Completeness.InstructionComplete,
            ImagesComplete = request.Completeness.ImagesComplete,
            InstructionConfirmedByStaff = request.Completeness.InstructionConfirmedByStaff,
            ImagesConfirmedByStaff = request.Completeness.ImagesConfirmedByStaff,
            CreatedAtUtc = acceptedAtUtc,
            Version = 0
        };
        context.Cases.Add(caseEntity);
        var dataSnapshot = CaseDataSnapshotFactory.Create(caseEntity, receipt, request, acceptedAtUtc);
        context.CaseDataSnapshots.Add(dataSnapshot);
        CaseMatchIndexProjector.Apply(
            context,
            existing: null,
            CaseMatchIndexProjector.Project(
                caseEntity,
                dataSnapshot.Fields,
                caseMatchPolicies ?? [],
                acceptedAtUtc));
        var workflowEntity = new CaseWorkflowEntity
        {
            Case = caseEntity,
            CaseId = caseId,
            State = CaseInitialWorkflowState.From(initialState).ToString(),
            Version = 0
        };
        context.CaseWorkflows.Add(workflowEntity);
        if (initialState == CaseInitialState.NotReady)
        {
            context.CaseDueWork.Add(new()
            {
                CaseId = caseId,
                Workflow = workflowEntity,
                MissingMaterialReason = "Accepted intake is incomplete",
                DueBy = request.AcceptedInspectionDeadline,
                State = CaseDueWorkState.Scheduled.ToString(),
                NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(acceptedAtUtc),
                Version = 0
            });
        }
        context.CaseIntakeLinks.Add(new()
        {
            IntakeReceiptId = receipt.Id,
            Case = caseEntity,
            CaseId = caseId,
            CustodyWorkId = custodyWorkId,
            LinkedAtUtc = acceptedAtUtc,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = request.Reason,
            OperationKey = request.OperationKey,
            ExpectedIntakeVersion = request.ExpectedIntakeVersion,
            AcceptanceCommandMaterialJson = command.MaterialJson,
            AcceptanceCommandFingerprint = command.Fingerprint
        });
        receipt.ManualAssociation = new()
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = caseId,
            Case = caseEntity,
            IsActive = true,
            Version = 0,
            LinkedAtUtc = acceptedAtUtc,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = request.Reason,
            LastOperationKey = request.OperationKey
        };
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            Case = caseEntity,
            CaseId = caseId,
            EventType = "case_accepted",
            Actor = request.Actor.SubjectId,
            Reason = request.Reason,
            OccurredAtUtc = acceptedAtUtc,
            OperationKey = request.OperationKey,
            BeforeVersion = null,
            AfterVersion = 0
        });
        if (request.ProviderInspectionMode == CaseInspectionMode.ImageBasedAssessment)
        {
            context.CaseHistory.Add(new()
            {
                Id = Guid.NewGuid(),
                Case = caseEntity,
                CaseId = caseId,
                EventType = "provider_inspection_mode_applied",
                Actor = request.Actor.SubjectId,
                Reason = "Provider setting: inspection address autofilled as Image Based Assessment",
                OccurredAtUtc = acceptedAtUtc,
                OperationKey = $"provider-mode:{caseId:N}",
                BeforeVersion = null,
                AfterVersion = 0
            });
        }
        context.ExternalWorkItems.Add(new()
        {
            Id = custodyWorkId,
            Case = caseEntity,
            CaseId = caseId,
            Kind = "create_case_custody",
            OperationKey = $"case-custody:{caseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = acceptedAtUtc
        });
        receipt.Version++;
        context.IntakeMutationHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = caseId,
            Case = caseEntity,
            EventType = "intake_case_association_seeded",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = request.Reason,
            OperationKey = request.OperationKey,
            RequestFingerprint = command.Fingerprint,
            OccurredAtUtc = acceptedAtUtc,
            ExpectedIntakeVersion = request.ExpectedIntakeVersion,
            BeforeIntakeVersion = request.ExpectedIntakeVersion,
            AfterIntakeVersion = receipt.Version,
            ExpectedCaseVersion = null,
            BeforeCaseVersion = null,
            AfterCaseVersion = 0
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(caseEntity, custodyWorkId, false);
    }

    private static async Task<StandaloneAuditEvidenceEntity?> ResolveStandaloneAuditEvidenceAsync(
        PegasusDbContext context,
        CaseAcceptanceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CaseType != CaseType.Audit)
        {
            return null;
        }

        var evidenceId = request.StandaloneAuditEvidenceId!.Value;
        var evidence = await context.Set<StandaloneAuditEvidenceEntity>()
            .AsNoTracking()
            .Include(item => item.OriginalReportAsset)
            .SingleOrDefaultAsync(
                item => item.Id == evidenceId
                    && item.IntakeReceiptId == request.IntakeReceiptId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The standalone Audit evidence is not retained for this intake receipt.");
        if (!string.Equals(evidence.ConfirmedByKind, "Staff", StringComparison.Ordinal)
            || !Guid.TryParse(evidence.ConfirmedBySubjectId, out var staffId)
            || staffId == Guid.Empty
            || evidence.ResultingReceiptVersion > request.ExpectedIntakeVersion
            || evidence.RequestHash.Length != 64
            || evidence.RequestHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidDataException(
                "The retained standalone Audit confirmation is incomplete or invalid.");
        }

        var report = evidence.OriginalReportAsset;
        if (report.IntakeReceiptId != request.IntakeReceiptId
            || report.Id != evidence.OriginalReportAssetId
            || report.Kind is not ("source" or "attachment")
            || report.ContentLength <= 0
            || string.IsNullOrWhiteSpace(report.StorageKey)
            || report.ContentHash.Length != 64
            || report.ContentHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidDataException(
                "The retained standalone Audit confirmation does not identify a valid original Engineer report.");
        }

        _ = ParseAuditAssessment(evidence.Assessment);
        return evidence;
    }

    private async Task<CaseAcceptanceOutcome?> FindAcceptedAsync(
        CaseAcceptanceRequest request,
        string principalCode,
        AcceptanceCommand command,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var link = await context.CaseIntakeLinks
            .AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == request.IntakeReceiptId,
                cancellationToken);
        if (link is null)
        {
            return null;
        }

        EnsureExactReplay(link, request, principalCode, command);
        return Map(link.Case, link.CustodyWorkId, true);
    }

    private static void EnsureExactReplay(
        CaseIntakeLinkEntity link,
        CaseAcceptanceRequest request,
        string principalCode,
        AcceptanceCommand command)
    {
        if (!string.Equals(link.OperationKey, request.OperationKey, StringComparison.Ordinal)
            || link.ExpectedIntakeVersion != request.ExpectedIntakeVersion
            || !string.Equals(
                link.AcceptanceCommandMaterialJson,
                command.MaterialJson,
                StringComparison.Ordinal)
            || !string.Equals(
                link.AcceptanceCommandFingerprint,
                command.Fingerprint,
                StringComparison.Ordinal)
            || link.ActorKind != request.Actor.Kind.ToString()
            || link.ActorSubjectId != request.Actor.SubjectId
            || link.ActorRolesJson != RolesJson(request.Actor)
            || link.Reason != request.Reason
            || !string.Equals(link.Case.Type, ToCode(request.CaseType), StringComparison.Ordinal)
            || !string.Equals(link.Case.Principal.Code, principalCode, StringComparison.Ordinal)
            || link.Case.StandaloneAuditEvidenceId != request.StandaloneAuditEvidenceId)
        {
            throw new CaseAcceptanceOperationConflictException(
                request.IntakeReceiptId,
                request.OperationKey);
        }
    }

    private static CaseAcceptanceOutcome Map(CaseEntity entity, Guid custodyWorkId, bool isDuplicate) => new(
        new(
            entity.Id,
            entity.Principal.Code,
            entity.Year,
            entity.Sequence,
            entity.Reference,
            entity.AuditReference),
        ParseInitialState(entity.InitialState),
        CaseCustodyState.Pending,
        custodyWorkId,
        isDuplicate);


    private static string ToCode(CaseType value) => value switch
    {
        CaseType.Inspection => "inspection",
        CaseType.Audit => "audit",
        CaseType.InspectionAndAudit => "inspection_and_audit",
        _ => throw new InvalidOperationException($"Unknown CaseType value '{(int)value}'.")
    };

    private static string ToCode(AuditAssessment value) => value switch
    {
        AuditAssessment.Repairable => "repairable",
        AuditAssessment.TotalLoss => "total_loss",
        _ => throw new InvalidOperationException($"Unknown AuditAssessment value '{(int)value}'.")
    };

    private static AuditAssessment ParseAuditAssessment(string value) => value switch
    {
        "repairable" => AuditAssessment.Repairable,
        "total_loss" => AuditAssessment.TotalLoss,
        _ => throw new InvalidDataException($"Unknown persisted Audit assessment '{value}'.")
    };

    private static string ToCode(CaseInitialState value) => value switch
    {
        CaseInitialState.NotReady => "not_ready",
        CaseInitialState.Review => "review",
        _ => throw new InvalidOperationException($"Unknown CaseInitialState value '{(int)value}'.")
    };

    private static CaseInitialState ParseInitialState(string value) => value switch
    {
        "not_ready" => CaseInitialState.NotReady,
        "review" => CaseInitialState.Review,
        _ => throw new InvalidDataException($"Unknown persisted case initial state '{value}'.")
    };

    private static string ToCode(CaseCustodyState value) => value switch
    {
        CaseCustodyState.Pending => "pending",
        CaseCustodyState.Confirmed => "confirmed",
        CaseCustodyState.Failed => "failed",
        _ => throw new InvalidOperationException($"Unknown CaseCustodyState value '{(int)value}'.")
    };


    private static AcceptanceCommand CreateAcceptanceCommand(
        CaseAcceptanceRequest request,
        string principalCode)
    {
        var materialJson = JsonSerializer.Serialize(new AcceptanceCommandMaterial(
            4,
            request.IntakeReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            request.Actor.Roles
                .OrderBy(role => role)
                .Select(role => role.ToString())
                .ToArray(),
            request.Reason,
            ToCode(request.CaseType),
            principalCode,
            request.Completeness.InstructionComplete,
            request.Completeness.ImagesComplete,
            request.Completeness.InstructionConfirmedByStaff,
            request.Completeness.ImagesConfirmedByStaff,
            request.StandaloneAuditEvidenceId,
            request.AcceptedInspectionDeadline,
            ProviderInspectionModePolicy.ToCode(request.ProviderInspectionMode)));
        var fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(materialJson)))
            .ToLowerInvariant();
        return new(materialJson, fingerprint);
    }

    private sealed record AcceptanceCommand(
        string MaterialJson,
        string Fingerprint);

    private sealed record AcceptanceCommandMaterial(
        int SchemaVersion,
        Guid IntakeReceiptId,
        long ExpectedIntakeVersion,
        string ActorKind,
        string ActorSubjectId,
        IReadOnlyList<string> ActorRoles,
        string Reason,
        string CaseType,
        string PrincipalCode,
        bool InstructionComplete,
        bool ImagesComplete,
        bool InstructionConfirmedByStaff,
        bool ImagesConfirmedByStaff,
        Guid? StandaloneAuditEvidenceId,
        DateOnly? AcceptedInspectionDeadline,
        string ProviderInspectionMode);

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(
            actor.Roles
                .OrderBy(role => role)
                .Select(role => role.ToString())
                .ToArray());

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsRetryableConcurrencyFailure(innerException),
        _ => false
    };
}
