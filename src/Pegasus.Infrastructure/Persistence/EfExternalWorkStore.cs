using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfExternalWorkStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IExternalWorkStore, IQueuedExternalWorkReader, ICaseCustodyQueries, ICustodyRecoveryPersistence
{
    private const int CandidateBatchSize = 256;

    public async Task<bool> HoldsProcessingLeaseAsync(
        Guid workItemId,
        string leaseToken,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty || string.IsNullOrWhiteSpace(leaseToken))
        {
            return false;
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return await context.ExternalWorkItems.AsNoTracking().AnyAsync(
            item => item.Id == workItemId
                && item.State == ExternalWorkStatePersistence.Processing
                && item.LeaseToken == leaseToken
                && item.LeaseExpiresAtUtc > now,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CaseCustodyPreparation>> GetPreparationsAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return [];
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var version = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => (long?)item.Version)
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return [];
        }
        var work = await context.ExternalWorkItems
            .AsNoTracking()
            .Where(item => item.CaseId == caseId
                && (item.Kind == ExternalWorkKinds.CreateCaseCustody
                    || item.Kind == ExternalWorkKinds.CreateAuditReferenceCustody))
            .OrderBy(item => item.Kind)
            .Select(item => new
            {
                item.Kind,
                item.State,
                item.FailureReason,
                item.AttemptCount
            })
            .ToArrayAsync(cancellationToken);
        return work.Select(item => new CaseCustodyPreparation(
            caseId,
            version.Value,
            item.Kind == ExternalWorkKinds.CreateCaseCustody
                ? CustodyTargetKind.CaseSource
                : CustodyTargetKind.AuditReference,
            item.State,
            item.FailureReason,
            item.AttemptCount,
            string.Equals(item.State, ExternalWorkStatePersistence.Failed, StringComparison.Ordinal)))
            .ToArray();
    }

    public async Task<RetryCaseCustodyResult> RetryAsync(
        RetryCaseCustodyRequest request,
        string normalizedReason,
        string requestHash,
        CustodyRetryPolicyAuthority policy,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await context.CaseWorkflowEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId
                && item.OperationKey == request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            return policy.Decide(new(
                OperationExists: true,
                OperationMatches: string.Equals(
                    replay.EventType, "custody_retry_requested", StringComparison.Ordinal)
                    && string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal),
                replay.AfterVersion,
                CaseExists: false, null, WorkExists: false, null,
                AnotherRetryWon: false, null,
                CustodyAlreadyConfirmed: false,
                AuditReferenceExists: true));
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
        if (workflow is null)
        {
            return policy.Decide(new(
                false, false, null, false, null, false, null, false, null, false, true));
        }
        ArchivedCaseGuard.RequireMutable(workflow);
        var kind = request.TargetKind == CustodyTargetKind.CaseSource
            ? ExternalWorkKinds.CreateCaseCustody
            : ExternalWorkKinds.CreateAuditReferenceCustody;
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId && item.Kind == kind, cancellationToken);
        if (work is null)
        {
            return policy.Decide(new(
                false, false, null, true, workflow.Version, false, null,
                false, null, false, true));
        }
        if (!string.Equals(work.State, ExternalWorkStatePersistence.Failed, StringComparison.Ordinal))
        {
            var winner = await context.CaseWorkflowEvents
                .AsNoTracking()
                .Where(item => item.CaseId == request.CaseId
                    && item.EventType == "custody_retry_requested")
                .OrderByDescending(item => item.OccurredAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            return policy.Decide(new(
                false, false, null, true, workflow.Version, true, work.State,
                winner is not null, winner?.AfterVersion, false, true));
        }
        CaseMutationGuard.Require(
            workflow,
            request.Actor,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            timeProvider.GetUtcNow());
        if (request.TargetKind == CustodyTargetKind.CaseSource
            && string.Equals(workflow.Case.CustodyState, "confirmed", StringComparison.Ordinal)
            || request.TargetKind == CustodyTargetKind.AuditReference
                && !string.IsNullOrWhiteSpace(workflow.Case.AuditCustodyRemoteId))
        {
            return policy.Decide(new(
                false, false, null, true, workflow.Version, true, work.State,
                false, null, true, true));
        }
        if (request.TargetKind == CustodyTargetKind.AuditReference
            && string.IsNullOrWhiteSpace(workflow.Case.AuditReference))
        {
            return policy.Decide(new(
                false, false, null, true, workflow.Version, true, work.State,
                false, null, false, false));
        }

        var decision = policy.Decide(new(
            false, false, null, true, workflow.Version, true, work.State,
            false, null, false, true));
        if (decision.Outcome != RetryCaseCustodyOutcome.Pending)
        {
            return decision;
        }

        work.CaseRootCreationToken ??= CustodyCreationOwner.Create();
        if (!string.IsNullOrWhiteSpace(workflow.Case.AuditReference))
        {
            work.AuditFolderCreationToken ??= CustodyCreationOwner.Create();
        }
        work.State = ExternalWorkStatePersistence.Pending;
        work.DueAtUtc = timeProvider.GetUtcNow();
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.CompletedAtUtc = null;
        work.FailureCode = null;
        work.FailureReason = null;
        var beforeVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        var now = timeProvider.GetUtcNow();
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Workflow = workflow,
            EventType = "custody_retry_requested",
            OperationKey = request.OperationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = normalizedReason,
            OccurredAtUtc = now,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version,
            ResultJson = JsonSerializer.Serialize(new
            {
                target = request.TargetKind.ToString(),
                state = ExternalWorkStatePersistence.Pending
            })
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            EventType = "custody_retry_requested",
            Actor = request.Actor.SubjectId,
            Reason = normalizedReason,
            OccurredAtUtc = now,
            OperationKey = request.OperationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = request.CaseId.ToString("D"),
            EventKind = "custody_retry_requested",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = normalizedReason,
            BeforeJson = JsonSerializer.Serialize(new { workflowVersion = beforeVersion }),
            AfterJson = JsonSerializer.Serialize(new
            {
                workflowVersion = workflow.Version,
                state = ExternalWorkStatePersistence.Pending
            }),
            PolicyVersion = "custody-recovery-v1"
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsRetryConcurrencyConflict(exception))
        {
            return await ResolveConcurrentRetryAsync(request, requestHash, cancellationToken);
        }
        return new(RetryCaseCustodyOutcome.Pending, workflow.Version,
            "Custody retry is pending.");
    }

    private async Task<RetryCaseCustodyResult> ResolveConcurrentRetryAsync(
        RetryCaseCustodyRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var verification = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exact = await verification.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId
                && item.OperationKey == request.OperationKey, cancellationToken);
        if (exact is not null
            && string.Equals(exact.EventType, "custody_retry_requested", StringComparison.Ordinal)
            && string.Equals(exact.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new(RetryCaseCustodyOutcome.Replay, exact.AfterVersion,
                "The original custody retry request is already pending.");
        }
        return new(RetryCaseCustodyOutcome.Conflict, null,
            "Another authorized custody retry changed the case. Reload before retrying.");
    }

    private static bool IsRetryConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException
                || current is SqlException { Number: 1205 or 2601 or 2627 })
            {
                return true;
            }
        }
        return false;
    }

    public async Task<QueuedExternalWork?> GetAsync(
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExternalWorkItems
            .AsNoTracking()
            .Where(item => item.Id == workItemId)
            .Select(item => new QueuedExternalWork(item.Id, item.Kind))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        while (true)
        {
            var candidate = await FindNextCandidateAsync(context, nowUtc, cancellationToken);
            if (candidate is null)
            {
                return null;
            }

            var selected = candidate.Value;
            var leaseToken = Guid.NewGuid().ToString("N");
            var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
            var claimed = await context.ExternalWorkItems
                .Where(item => item.Id == selected.Id
                    && item.State == selected.State
                    && item.DueAtUtc == selected.DueAtUtc
                    && item.LeaseToken == selected.LeaseToken
                    && item.LeaseExpiresAtUtc == selected.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, ExternalWorkStatePersistence.Dispatching)
                    .SetProperty(item => item.LeaseToken, leaseToken)
                    .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(item => item.FailureCode, (string?)null)
                    .SetProperty(item => item.FailureReason, (string?)null),
                    cancellationToken);
            if (claimed == 1)
            {
                return new(selected.Id, leaseToken);
            }
        }
    }

    public async Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
        Guid workItemId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("An external work item identifier is required.", nameof(workItemId));
        }
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var leaseToken = Guid.NewGuid().ToString("N");
        var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        var claimed = await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.State == ExternalWorkStatePersistence.Pending
                && item.DueAtUtc <= nowUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, ExternalWorkStatePersistence.Dispatching)
                .SetProperty(item => item.LeaseToken, leaseToken)
                .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                .SetProperty(item => item.FailureCode, (string?)null)
                .SetProperty(item => item.FailureReason, (string?)null),
                cancellationToken);
        return claimed == 1 ? new(workItemId, leaseToken) : null;
    }

    public async Task MarkDispatchedAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dispatchedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateLease(workItemId, leaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.State == ExternalWorkStatePersistence.Dispatching
                && item.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, ExternalWorkStatePersistence.Queued)
                .SetProperty(item => item.DueAtUtc, dispatchedAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, (string?)null)
                .SetProperty(item => item.FailureReason, (string?)null),
                cancellationToken);
        if (updated == 0)
        {
            await EnsureWorkExistsAsync(context, workItemId, cancellationToken);
        }
    }

    public async Task ReleaseDispatchAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateLease(workItemId, leaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.State == ExternalWorkStatePersistence.Dispatching
                && item.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, ExternalWorkStatePersistence.Pending)
                .SetProperty(item => item.DueAtUtc, dueAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, "queue_dispatch_failure")
                .SetProperty(
                    item => item.FailureReason,
                    "The external work identifier could not be confirmed in the queue."),
                cancellationToken);
        if (updated == 0)
        {
            await EnsureWorkExistsAsync(context, workItemId, cancellationToken);
        }
    }

    public async Task MarkPoisonedAsync(
        Guid workItemId,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var work = await context.ExternalWorkItems
            .Include(item => item.Case)
            .Include(item => item.ImageIntake)
            .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The external work item is unavailable.");
        if (work.State is ExternalWorkStatePersistence.Completed or ExternalWorkStatePersistence.Failed)
        {
            return;
        }
        var workflow = work.CaseId is { } workflowCaseId
            ? await context.CaseWorkflows
                .SingleOrDefaultAsync(item => item.CaseId == workflowCaseId, cancellationToken)
            : null;

        switch (work.Kind)
        {
            case ExternalWorkKinds.CreateCaseCustody:
                if (string.Equals(work.Case!.CustodyState, "confirmed", StringComparison.Ordinal))
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                FailWork(
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "Case evidence storage could not complete after queue delivery failed.");
                if (workflow is not null)
                {
                    var beforeVersion = workflow.Version;
                    work.Case!.CustodyState = "failed";
                    workflow.State = CaseLifecycleState.NotReady.ToString();
                    CaseMutationGuard.Complete(workflow);
                    context.CaseHistory.Add(new()
                    {
                        Id = Guid.NewGuid(),
                        CaseId = workflow.CaseId,
                        EventType = "custody_failed",
                        Actor = "system",
                        Reason = "Case evidence storage could not complete after queue delivery failed.",
                        OccurredAtUtc = failedAtUtc,
OperationKey = $"{work.OperationKey}:poisoned:{beforeVersion}",
                        BeforeVersion = beforeVersion,
                        AfterVersion = workflow.Version
                    });
                }
                break;

            case ExternalWorkKinds.CreateAuditReferenceCustody:
                if (!string.IsNullOrWhiteSpace(work.Case!.AuditCustodyRemoteId))
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                FailWork(
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "Case evidence storage could not complete after queue delivery failed.");
                if (workflow is not null)
                {
                    var beforeAuditVersion = workflow.Version;
                    CaseMutationGuard.Complete(workflow);
                    context.CaseHistory.Add(new()
                    {
                        Id = Guid.NewGuid(),
                        CaseId = workflow.CaseId,
                        EventType = "audit_custody_failed",
                        Actor = "system",
                        Reason = "Case evidence storage could not complete after queue delivery failed.",
                        OccurredAtUtc = failedAtUtc,
OperationKey = $"{work.OperationKey}:poisoned:{beforeAuditVersion}",
                        BeforeVersion = beforeAuditVersion,
                        AfterVersion = workflow.Version
                    });
                }
                break;

            case ExternalWorkKinds.CreateImageCaseCustody:
                if (work.ImageIntake is { CustodyState: ImageCustodyStates.Confirmed or ImageCustodyStates.Merged })
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                FailWork(
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "Image evidence storage could not complete after queue delivery failed.");
                if (work.ImageIntake is not null)
                {
                    work.ImageIntake.CustodyState = ImageCustodyStates.Failed;
                }
                break;

            case ExternalWorkKinds.MergeImageCaseCustody:
                if (work.ImageIntake is { CustodyState: ImageCustodyStates.Merged })
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                FailWork(
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "Image evidence could not be folded after queue delivery failed.");
                break;

            case ExternalWorkKinds.VehicleLookup:
                FailWork(
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "Vehicle lookup exhausted the queue retry policy.");
                break;

            case ExternalWorkKinds.SubmitCaseToEva:
                FailWork(
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "EVA submission exhausted the queue retry policy.");
                break;

            case ExternalWorkKinds.IntakeOcr:
                await FailOcrAsync(
                    context,
                    work,
                    failedAtUtc,
                    "queue_poisoned",
                    "OCR processing exhausted the queue retry policy.",
                    cancellationToken);
                break;

            default:
                FailWork(
                    work,
                    failedAtUtc,
                    "unknown_external_work_kind",
                    "The persisted external-work kind is not recognized and was denied.");
                break;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FailProcessingAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset failedAtUtc,
        string failureCode,
        string failureReason,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var work = await context.ExternalWorkItems
            .Include(item => item.Case)
            .Include(item => item.ImageIntake)
            .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The external work item is unavailable.");
        if (work.State is ExternalWorkStatePersistence.Completed or ExternalWorkStatePersistence.Failed)
        {
            return;
        }
        var now = timeProvider.GetUtcNow();
        if (!string.Equals(work.State, ExternalWorkStatePersistence.Processing, StringComparison.Ordinal)
            || !string.Equals(work.LeaseToken, leaseToken, StringComparison.Ordinal)
            || work.LeaseExpiresAtUtc <= now)
        {
            return;
        }
        var workflow = work.CaseId is { } workflowCaseId
            ? await context.CaseWorkflows
                .SingleOrDefaultAsync(item => item.CaseId == workflowCaseId, cancellationToken)
            : null;

        switch (work.Kind)
        {
            case ExternalWorkKinds.CreateCaseCustody:
                if (string.Equals(work.Case!.CustodyState, "confirmed", StringComparison.Ordinal))
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                FailWork(work, failedAtUtc, failureCode, failureReason);
                if (workflow is not null)
                {
                    var beforeVersion = workflow.Version;
                    work.Case!.CustodyState = "failed";
                    workflow.State = CaseLifecycleState.NotReady.ToString();
                    CaseMutationGuard.Complete(workflow);
                    context.CaseHistory.Add(new()
                    {
                        Id = Guid.NewGuid(),
                        CaseId = workflow.CaseId,
                        EventType = "custody_failed",
                        Actor = "system",
                        Reason = failureReason,
                        OccurredAtUtc = failedAtUtc,
                        OperationKey = $"{work.OperationKey}:failed:{work.AttemptCount}",
                        BeforeVersion = beforeVersion,
                        AfterVersion = workflow.Version
                    });
                }
                break;

            case ExternalWorkKinds.CreateAuditReferenceCustody:
                if (!string.IsNullOrWhiteSpace(work.Case!.AuditCustodyRemoteId))
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                FailWork(work, failedAtUtc, failureCode, failureReason);
                if (workflow is not null)
                {
                    var beforeAuditVersion = workflow.Version;
                    CaseMutationGuard.Complete(workflow);
                    context.CaseHistory.Add(new()
                    {
                        Id = Guid.NewGuid(),
                        CaseId = workflow.CaseId,
                        EventType = "audit_custody_failed",
                        Actor = "system",
                        Reason = failureReason,
                        OccurredAtUtc = failedAtUtc,
                        OperationKey = $"{work.OperationKey}:failed:{work.AttemptCount}",
                        BeforeVersion = beforeAuditVersion,
                        AfterVersion = workflow.Version
                    });
                }
                break;

            case ExternalWorkKinds.CreateImageCaseCustody:
                if (work.ImageIntake is { CustodyState: ImageCustodyStates.Confirmed or ImageCustodyStates.Merged })
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                if (!TryRearmImageCustody(work, failedAtUtc, failureCode, failureReason))
                {
                    FailWork(work, failedAtUtc, failureCode, failureReason);
                    if (work.ImageIntake is not null)
                    {
                        work.ImageIntake.CustodyState = ImageCustodyStates.Failed;
                    }
                }
                break;

            case ExternalWorkKinds.MergeImageCaseCustody:
                if (work.ImageIntake is { CustodyState: ImageCustodyStates.Merged })
                {
                    CompletePoisonReplay(work, failedAtUtc);
                    break;
                }

                if (!TryRearmImageCustody(work, failedAtUtc, failureCode, failureReason))
                {
                    // The image-case folder still holds the evidence, so the
                    // intake's custody state stays an honest "confirmed"; only
                    // the fold itself is recorded as failed.
                    FailWork(work, failedAtUtc, failureCode, failureReason);
                }
                break;

            case ExternalWorkKinds.IntakeOcr:
                await FailOcrAsync(
                    context,
                    work,
                    failedAtUtc,
                    failureCode,
                    failureReason,
                    cancellationToken);
                break;

            default:
                FailWork(work, failedAtUtc, failureCode, failureReason);
                break;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task FailOcrAsync(
        PegasusDbContext context,
        ExternalWorkItemEntity work,
        DateTimeOffset failedAtUtc,
        string failureCode,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var operation = await context.Set<IntakeOcrOperationEntity>()
            .SingleOrDefaultAsync(item => item.Id == work.Id, cancellationToken);
        if (operation is null)
        {
            FailWork(
                work,
                failedAtUtc,
                "ocr_operation_unavailable",
                "The OCR operation paired with this external work item is unavailable.");
            return;
        }

        if (operation.State == nameof(IntakeOcrState.Completed))
        {
            CompletePoisonReplay(work, failedAtUtc);
            return;
        }

        var uncertain = operation.State is nameof(IntakeOcrState.Processing)
            or nameof(IntakeOcrState.Unknown)
            || !string.IsNullOrWhiteSpace(operation.ProviderOperationId);
        if (operation.State != nameof(IntakeOcrState.Failed))
        {
            operation.State = uncertain
                ? nameof(IntakeOcrState.Unknown)
                : nameof(IntakeOcrState.Failed);
            operation.LastError = $"{failureCode}: {failureReason}";
            operation.RetryAtUtc = null;
            operation.Version++;
        }

        FailWork(work, failedAtUtc, failureCode, failureReason);
    }

    private static bool TryRearmImageCustody(
        ExternalWorkItemEntity work,
        DateTimeOffset failedAtUtc,
        string failureCode,
        string failureReason)
    {
        if (ImageCustodyRetryPolicy.NextAttemptDelay(work.AttemptCount, failureCode)
            is not { } delay)
        {
            return false;
        }

        work.State = ExternalWorkStatePersistence.Pending;
        work.DueAtUtc = failedAtUtc.Add(delay);
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.FailureCode = failureCode;
        work.FailureReason = failureReason;
        return true;
    }

    private static void CompletePoisonReplay(
        ExternalWorkItemEntity work,
        DateTimeOffset completedAtUtc)
    {
        work.State = ExternalWorkStatePersistence.Completed;
        work.CompletedAtUtc ??= completedAtUtc;
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.FailureCode = null;
        work.FailureReason = null;
    }

    private static void FailWork(
        ExternalWorkItemEntity work,
        DateTimeOffset failedAtUtc,
        string failureCode,
        string failureReason)
    {
        work.State = ExternalWorkStatePersistence.Failed;
        work.DueAtUtc = failedAtUtc;
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.FailureCode = failureCode;
        work.FailureReason = failureReason;
    }

    private static async Task<DispatchCandidate?> FindNextCandidateAsync(
        PegasusDbContext context,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        DispatchCandidate? next = null;
        for (var offset = 0; ; offset += CandidateBatchSize)
        {
            var batch = await context.ExternalWorkItems
                .AsNoTracking()
                .Where(item => item.State == ExternalWorkStatePersistence.Pending
                    || item.State == ExternalWorkStatePersistence.Dispatching)
                .OrderBy(item => item.Id)
                .Skip(offset)
                .Take(CandidateBatchSize)
                .Select(item => new DispatchCandidate(
                    item.Id,
                    item.State,
                    item.DueAtUtc,
                    item.LeaseToken,
                    item.LeaseExpiresAtUtc))
                .ToListAsync(cancellationToken);
            foreach (var candidate in batch)
            {
                var availableAtUtc = candidate.State == ExternalWorkStatePersistence.Pending
                    ? candidate.DueAtUtc
                    : candidate.LeaseExpiresAtUtc ?? DateTimeOffset.MinValue;
                if (availableAtUtc <= nowUtc
                    && (next is null || Compare(candidate, next.Value) < 0))
                {
                    next = candidate;
                }
            }

            if (batch.Count < CandidateBatchSize)
            {
                return next;
            }
        }
    }

    private static int Compare(DispatchCandidate left, DispatchCandidate right)
    {
        var leftAvailableAtUtc = left.State == ExternalWorkStatePersistence.Pending
            ? left.DueAtUtc
            : left.LeaseExpiresAtUtc ?? DateTimeOffset.MinValue;
        var rightAvailableAtUtc = right.State == ExternalWorkStatePersistence.Pending
            ? right.DueAtUtc
            : right.LeaseExpiresAtUtc ?? DateTimeOffset.MinValue;
        var comparison = leftAvailableAtUtc.CompareTo(rightAvailableAtUtc);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }

    private static async Task EnsureWorkExistsAsync(
        PegasusDbContext context,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        if (!await context.ExternalWorkItems
                .AsNoTracking()
                .AnyAsync(item => item.Id == workItemId, cancellationToken))
        {
            throw new InvalidOperationException("The external work item is unavailable.");
        }
    }

    private static void ValidateLease(Guid workItemId, string leaseToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
    }

    private static string RolesJson(ActionActor actor) => JsonSerializer.Serialize(
        actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray());

    private readonly record struct DispatchCandidate(
        Guid Id,
        string State,
        DateTimeOffset DueAtUtc,
        string? LeaseToken,
        DateTimeOffset? LeaseExpiresAtUtc);
}
