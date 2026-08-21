using System.Data;
using System.Diagnostics;
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

public sealed class EfLinkedCaseReplacementStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IEnumerable<Pegasus.Core.Intake.IProviderCaseMatchPolicy>? caseMatchPolicies = null)
    : ILinkedCaseReplacementStore
{
    private static readonly TimeZoneInfo LondonTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public async Task<CaseAcceptanceOutcome> CreateAsync(
        CreateLinkedReplacementRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = RequestHash(request);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await CreateOnceAsync(request, requestHash, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                var replay = await FindReplayAsync(request, requestHash, cancellationToken);
                if (replay is not null)
                {
                    return replay;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<CaseAcceptanceOutcome> CreateOnceAsync(
        CreateLinkedReplacementRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replayEvent = await context.CaseWorkflowEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId
                    && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replayEvent is not null)
        {
            EnsureExactReplay(request, requestHash, replayEvent);
            return await LoadReplayOutcomeAsync(context, request.CaseId, cancellationToken);
        }

        var original = await context.CaseWorkflows
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        ArchivedCaseGuard.RequireMutable(original);
        RequireVersion(original, request.ExpectedVersion);
        RequireLease(original, request.Actor, request.EditLeaseToken, timeProvider.GetUtcNow());
        if (IsTerminal(original.State))
        {
            throw new InvalidOperationException(
                "A closed case cannot allocate another corrected-principal replacement.");
        }
        if (original.ReplacementCaseId is not null)
        {
            throw new InvalidOperationException(
                "The wrong-principal case already has an immutable replacement link.");
        }
        await CaseTerminalReadinessGuard.RequireNoOpenTasksAsync(
            context,
            original.CaseId,
            cancellationToken);

        var replacementPrincipal = await context.Principals
            .SingleOrDefaultAsync(
                item => item.Code == request.ReplacementPrincipalCode && item.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"The active corrected principal '{request.ReplacementPrincipalCode}' does not exist.");
        if (replacementPrincipal.Id == original.Case.PrincipalId)
        {
            throw new InvalidOperationException(
                "A wrong-principal replacement must use a different corrected principal.");
        }
        var originalCaseData = await context.CaseDataSnapshots
            .Include(item => item.Fields)
            .SingleOrDefaultAsync(item => item.CaseId == original.CaseId, cancellationToken)
            ?? throw new InvalidDataException(
                "The original case has no immutable typed case-data snapshot.");


        var now = timeProvider.GetUtcNow();
        var year = TimeZoneInfo.ConvertTime(now, LondonTimeZone).Year;
        var sequence = await context.CaseSequences.SingleOrDefaultAsync(
            item => item.SequenceLineageId == replacementPrincipal.SequenceLineageId
                && item.Year == year,
            cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity
            {
                SequenceLineageId = replacementPrincipal.SequenceLineageId,
                Year = year,
                LastAllocatedSequence = 0
            };
            context.CaseSequences.Add(sequence);
        }
        if (sequence.LastAllocatedSequence >= 999)
        {
            throw new CaseIdentitySequenceExhaustedException(replacementPrincipal.Code, year);
        }

        var allocatedSequence = ++sequence.LastAllocatedSequence;
        var reference = $"{replacementPrincipal.Code}{year % 100:00}{allocatedSequence:000}";
        var initialState = ParseInitialState(original.Case.InitialState);
        var replacementCaseId = Guid.NewGuid();
        var custodyWorkId = Guid.NewGuid();
        var replacementCase = new CaseEntity
        {
            Id = replacementCaseId,
            PrincipalId = replacementPrincipal.Id,
            Principal = replacementPrincipal,
            SequenceLineageId = replacementPrincipal.SequenceLineageId,
            Year = year,
            Sequence = allocatedSequence,
            Reference = reference,
            AuditReference = CreateStandaloneAuditReference(original.Case, reference),
            Type = original.Case.Type,
            InitialState = original.Case.InitialState,
            CustodyState = "pending",
            OriginIntakeReceiptId = original.Case.OriginIntakeReceiptId,
            StandaloneAuditAssessment = original.Case.StandaloneAuditAssessment,
            StandaloneAuditEvidenceId = original.Case.StandaloneAuditEvidenceId,
            AcceptedInspectionDeadline = original.Case.AcceptedInspectionDeadline,
            InstructionComplete = original.Case.InstructionComplete,
            ImagesComplete = original.Case.ImagesComplete,
            InstructionConfirmedByStaff = original.Case.InstructionConfirmedByStaff,
            ImagesConfirmedByStaff = original.Case.ImagesConfirmedByStaff,
            CreatedAtUtc = now,
            Version = 0
        };
        context.Cases.Add(replacementCase);
        var replacementCaseData = CloneCaseDataSnapshot(originalCaseData, replacementCase);
        context.CaseDataSnapshots.Add(replacementCaseData);
        // The replacement case must be matchable in its own right: the Created in error
        // original's index row stays (redirects resolve through it), and the replacement
        // gets its own row in this same transaction.
        CaseMatchIndexProjector.Apply(
            context,
            existing: null,
            CaseMatchIndexProjector.Project(
                replacementCase,
                replacementCaseData.Fields,
                caseMatchPolicies ?? [],
                now));


        var replacementWorkflow = new CaseWorkflowEntity
        {
            CaseId = replacementCaseId,
            Case = replacementCase,
            State = CaseInitialWorkflowState.From(initialState).ToString(),
            OriginalCaseId = original.CaseId,
            OriginalCase = original.Case,
            Version = 0
        };
        context.CaseWorkflows.Add(replacementWorkflow);
        if (initialState == CaseInitialState.NotReady)
        {
            context.CaseDueWork.Add(new CaseDueWorkEntity
            {
                CaseId = replacementCaseId,
                Workflow = replacementWorkflow,
                MissingMaterialReason = original.DueWork?.MissingMaterialReason
                    ?? "Corrected replacement awaits required material",
                DueBy = original.DueWork?.DueBy ?? original.Case.AcceptedInspectionDeadline,
                State = nameof(CaseDueWorkState.Scheduled),
                NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now),
                Version = 0
            });
        }

        context.ExternalWorkItems.Add(new ExternalWorkItemEntity
        {
            Id = custodyWorkId,
            CaseId = replacementCaseId,
            Case = replacementCase,
            Kind = "create_case_custody",
            OperationKey = $"case-custody:{replacementCaseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = now
        });

        var beforeVersion = original.Version;
        original.State = nameof(CaseLifecycleState.CreatedInError);
        original.ClosureOutcome = nameof(CaseClosureOutcome.CreatedInError);
        original.ReplacementCaseId = replacementCaseId;
        original.ReplacementCase = replacementCase;
        CaseChaseState.Stop(original.DueWork);
        original.Version++;
        ClearLease(original);

        AddWorkflowEvent(
            context,
            original,
            request.Actor,
            request.OperationKey,
            request.Reason,
            requestHash,
            "case_created_in_error_replaced",
            now,
            beforeVersion,
            original.Version);
        AddWorkflowEvent(
            context,
            replacementWorkflow,
            request.Actor,
            ReplacementOperationKey(request.OperationKey),
            request.Reason,
            requestHash,
            "case_created_as_replacement",
            now,
            0,
            0);
        context.CaseHistory.Add(new CaseHistoryEntity
        {
            Id = Guid.NewGuid(),
            CaseId = replacementCaseId,
            Case = replacementCase,
            EventType = "case_created_as_replacement",
            Actor = request.Actor.SubjectId,
            Reason = request.Reason,
            OccurredAtUtc = now,
            OperationKey = ReplacementOperationKey(request.OperationKey),
            BeforeVersion = null,
            AfterVersion = 0
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapOutcome(replacementCase, custodyWorkId, isDuplicate: false);
    }

    private static CaseDataSnapshotEntity CloneCaseDataSnapshot(
        CaseDataSnapshotEntity original,
        CaseEntity replacementCase)
    {
        var replacement = new CaseDataSnapshotEntity
        {
            CaseId = replacementCase.Id,
            Case = replacementCase,
            OriginIntakeReceiptId = original.OriginIntakeReceiptId,
            OriginSourceChannel = original.OriginSourceChannel,
            OriginExternalReceiptToken = original.OriginExternalReceiptToken,
            OriginSourceHash = original.OriginSourceHash,
            OriginReceivedAtUtc = original.OriginReceivedAtUtc,
            SourceReaderKey = original.SourceReaderKey,
            SourceReaderVersion = original.SourceReaderVersion,
            ExtractionPolicyKey = original.ExtractionPolicyKey,
            ExtractionPolicyVersion = original.ExtractionPolicyVersion,
            CompletenessPolicyKey = original.CompletenessPolicyKey,
            CompletenessPolicyVersion = original.CompletenessPolicyVersion,
            CompletenessPolicySatisfied = original.CompletenessPolicySatisfied,
            AcceptedAtUtc = original.AcceptedAtUtc
        };
        replacement.Fields.AddRange(original.Fields.Select(field => new CaseDataFieldEntity
        {
            CaseId = replacementCase.Id,
            Snapshot = replacement,
            FieldName = field.FieldName,
            ValueKind = field.ValueKind,
            ValueType = field.ValueType,
            Value = field.Value,
            SourceKind = field.SourceKind,
            SourceIdentity = field.SourceIdentity,
            SourceLabel = field.SourceLabel,
            PolicyKey = field.PolicyKey,
            PolicyVersion = field.PolicyVersion,
            ConfirmedByActor = field.ConfirmedByActor,
            ConfirmedAtUtc = field.ConfirmedAtUtc
        }));
        return replacement;
    }

    private async Task<CaseAcceptanceOutcome?> FindReplayAsync(
        CreateLinkedReplacementRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var replayEvent = await context.CaseWorkflowEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId
                    && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replayEvent is null)
        {
            return null;
        }

        EnsureExactReplay(request, requestHash, replayEvent);
        return await LoadReplayOutcomeAsync(context, request.CaseId, cancellationToken);
    }

    private static async Task<CaseAcceptanceOutcome> LoadReplayOutcomeAsync(
        PegasusDbContext context,
        Guid originalCaseId,
        CancellationToken cancellationToken)
    {
        var originalLink = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == originalCaseId)
            .Select(item => new
            {
                item.ReplacementCaseId,
                item.State,
                item.ClosureOutcome
            })
            .SingleAsync(cancellationToken);
        if (originalLink.ReplacementCaseId is not { } replacementId
            || originalLink.State != nameof(CaseLifecycleState.CreatedInError)
            || originalLink.ClosureOutcome != nameof(CaseClosureOutcome.CreatedInError))
        {
            throw new InvalidDataException(
                "The replacement operation history has no immutable replacement link.");
        }

        var reciprocalOriginalCaseId = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == replacementId)
            .Select(item => item.OriginalCaseId)
            .SingleAsync(cancellationToken);
        if (reciprocalOriginalCaseId != originalCaseId)
        {
            throw new InvalidDataException(
                "The replacement operation history has no reciprocal original-case link.");
        }
        var replacement = await context.Cases
            .AsNoTracking()
            .Include(item => item.Principal)
            .SingleAsync(item => item.Id == replacementId, cancellationToken);
        var custodyWorkId = await context.ExternalWorkItems
            .AsNoTracking()
            .Where(item => item.CaseId == replacementId && item.Kind == "create_case_custody")
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        return MapOutcome(replacement, custodyWorkId, isDuplicate: true);
    }

    private static void EnsureExactReplay(
        CreateLinkedReplacementRequest request,
        string requestHash,
        CaseWorkflowEventEntity replayEvent)
    {
        if (!string.Equals(replayEvent.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
        }
    }

    private static void AddWorkflowEvent(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string requestHash,
        string eventType,
        DateTimeOffset occurredAtUtc,
        long beforeVersion,
        long afterVersion) => context.CaseWorkflowEvents.Add(new CaseWorkflowEventEntity
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = RolesJson(actor),
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now) =>
        CaseMutationGuard.RequireLease(workflow, actor, token, now);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private static bool IsTerminal(string state) => state is
        nameof(CaseLifecycleState.PostReportComplete) or
        nameof(CaseLifecycleState.ProviderCancelled) or
        nameof(CaseLifecycleState.CollisionEngineersRejected) or
        nameof(CaseLifecycleState.CreatedInError);

    private static string? CreateStandaloneAuditReference(
        CaseEntity original,
        string replacementReference)
    {
        if (!string.Equals(original.Type, "audit", StringComparison.Ordinal))
        {
            return null;
        }
        if (original.StandaloneAuditAssessment is null)
        {
            throw new InvalidDataException(
                "The standalone Audit case has no retained original-report assessment.");
        }

        return AuditIdentity.Create(
            replacementReference,
            ParseAssessment(original.StandaloneAuditAssessment));
    }

    private static AuditAssessment ParseAssessment(string value) => value switch
    {
        "repairable" => AuditAssessment.Repairable,
        "total_loss" => AuditAssessment.TotalLoss,
        _ => throw new InvalidDataException(
            $"Unknown persisted Audit assessment '{value}'.")
    };

    private static CaseInitialState ParseInitialState(string value) => value switch
    {
        "not_ready" => CaseInitialState.NotReady,
        "review" => CaseInitialState.Review,
        _ => throw new InvalidDataException(
            $"Unknown persisted case initial state '{value}'.")
    };

    private static CaseCustodyState ParseCustodyState(string value) => value switch
    {
        "pending" => CaseCustodyState.Pending,
        "confirmed" => CaseCustodyState.Confirmed,
        "failed" => CaseCustodyState.Failed,
        _ => throw new InvalidDataException(
            $"Unknown persisted case custody state '{value}'.")
    };

    private static CaseAcceptanceOutcome MapOutcome(
        CaseEntity entity,
        Guid custodyWorkId,
        bool isDuplicate) => new(
        new(
            entity.Id,
            entity.Principal.Code,
            entity.Year,
            entity.Sequence,
            entity.Reference,
            entity.AuditReference),
        ParseInitialState(entity.InitialState),
        ParseCustodyState(entity.CustodyState),
        custodyWorkId,
        isDuplicate);

    private static string RequestHash(CreateLinkedReplacementRequest request)
    {
        var material = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            request.CaseId,
            request.ExpectedVersion,
            actorKind = request.Actor.Kind.ToString(),
            actorSubjectId = request.Actor.SubjectId,
            actorRoles = request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            request.OperationKey,
            request.Reason,
            editLeaseTokenHash = Hash(request.EditLeaseToken),
            request.ReplacementPrincipalCode
        });
        return Hash(material);
    }

    private static string ReplacementOperationKey(string operationKey) =>
        $"replacement:{Hash(operationKey)[..64]}";

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(actor.Roles.OrderBy(role => role));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsRetryableConcurrencyFailure(innerException),
        _ => false
    };
}
