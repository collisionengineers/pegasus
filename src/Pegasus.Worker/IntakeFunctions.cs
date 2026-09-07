using Pegasus.Core.Vehicle;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ProviderApi;
using Pegasus.Infrastructure.Transport;
using Pegasus.Infrastructure.Custody;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

/// <summary>
/// Slow reconciliation for publication attempts missed after their durable
/// commit. Ordinary intake is published directly by its committing caller.
/// </summary>
public sealed partial class PendingWorkRecoveryFunction(
    DispatchPendingWork dispatchPendingWork,
    ILogger<PendingWorkRecoveryFunction> logger)
{
    [Function(nameof(PendingWorkRecoveryFunction))]
    public async Task RunAsync(
        [TimerTrigger("%PendingWorkRecoverySchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var dispatched = await dispatchPendingWork.ExecuteAsync(50, cancellationToken);
        LogDispatchedWork(logger, dispatched.IntakeWorkCount, dispatched.ExternalWorkCount);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Recovered publication for {IntakeWorkCount} intake and {ExternalWorkCount} external durable work items.")]
    private static partial void LogDispatchedWork(
        ILogger logger,
        int intakeWorkCount,
        int externalWorkCount);
}

public sealed class UnifiedWorkFunction(
    IProcessQueuedIntake processQueuedIntake,
    IProcessQueuedExternalWork processQueuedExternalWork,
    PollApprovedInbox pollApprovedInbox,
    IApprovedMailboxSubscriptionStore mailboxSubscriptions,
    TimeProvider timeProvider)
{
    private static readonly ActionActor MailboxWakeActor =
        ActionActor.SystemWorker("approved-inbox-notification");

    [Function(nameof(UnifiedWorkFunction))]
    public async Task RunAsync(
        [QueueTrigger("intake-work", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (UnifiedWorkQueueMessage.TryParseMailbox(
                message,
                out var approvedMailboxId,
                out var subscriptionId,
                out var generation,
                out var wakeKind,
                out var immutableMessageId))
        {
            var subscription = await mailboxSubscriptions.GetActiveAsync(
                subscriptionId.ToString("D"),
                timeProvider.GetUtcNow(),
                cancellationToken)
                ?? throw new InvalidDataException("The mailbox wake subscription is no longer active.");
            if (subscription.ApprovedMailboxId != approvedMailboxId
                || subscription.Generation != generation)
            {
                throw new InvalidDataException("The mailbox wake does not match its subscription.");
            }
            if (wakeKind == MailboxWakeKind.Created && immutableMessageId is not null)
            {
                await pollApprovedInbox.ExecuteNotificationAsync(
                    approvedMailboxId,
                    generation,
                    immutableMessageId,
                    MailboxWakeActor,
                    cancellationToken);
            }
            else
            {
                await pollApprovedInbox.ExecuteMailboxAsync(
                    approvedMailboxId,
                    50,
                    MailboxWakeActor,
                    cancellationToken);
            }
            if (wakeKind != MailboxWakeKind.Created)
            {
                await mailboxSubscriptions.SaveAsync(
                    subscription with { LifecycleState = LifecycleState(wakeKind) },
                    subscription.SubscriptionId,
                    cancellationToken);
            }
            return;
        }

        if (!UnifiedWorkQueueMessage.TryParse(message, out var kind, out var identifier))
        {
            throw new InvalidDataException(
                "The unified work message does not contain one typed canonical durable identifier.");
        }

        switch (kind)
        {
            case UnifiedWorkQueueKind.Intake:
                await processQueuedIntake.ExecuteAsync(identifier, cancellationToken);
                return;
            case UnifiedWorkQueueKind.External:
                await processQueuedExternalWork.ExecuteAsync(identifier, cancellationToken);
                return;
            default:
                throw new InvalidDataException("The unified work message has an unsupported kind.");
        }
    }

    private static ApprovedMailboxSubscriptionLifecycleState LifecycleState(
        MailboxWakeKind wakeKind) => wakeKind switch
    {
        MailboxWakeKind.Missed => ApprovedMailboxSubscriptionLifecycleState.Missed,
        MailboxWakeKind.SubscriptionRemoved => ApprovedMailboxSubscriptionLifecycleState.Removed,
        MailboxWakeKind.ReauthorizationRequired =>
            ApprovedMailboxSubscriptionLifecycleState.ReauthorizationRequired,
        _ => ApprovedMailboxSubscriptionLifecycleState.Active
    };
}
public sealed class UnifiedWorkPoisonFunction(
    ReconcilePoisonedQueueWork reconcilePoisonedQueueWork,
    IApprovedMailboxSubscriptionStore mailboxSubscriptions,
    TimeProvider timeProvider)
{
    [Function(nameof(UnifiedWorkPoisonFunction))]
    public async Task RunAsync(
        [QueueTrigger("intake-work-poison", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (UnifiedWorkQueueMessage.TryParseMailbox(
                message,
                out var approvedMailboxId,
                out var subscriptionId,
                out var generation,
                out _,
                out _))
        {
            try
            {
                await mailboxSubscriptions.RecordMaintenanceFailureAsync(
                    approvedMailboxId,
                    generation,
                    subscriptionId.ToString("D"),
                    "notification_poison",
                    timeProvider.GetUtcNow(),
                    cancellationToken);
            }
            catch (ApprovedMailboxSubscriptionMaintenanceLostException)
            {
                // The poison wake belongs to an older mailbox generation.
            }
            return;
        }

        if (!UnifiedWorkQueueMessage.TryParse(message, out var kind, out var identifier))
        {
            throw new InvalidDataException(
                "The unified poison message does not contain one typed canonical durable identifier.");
        }

        await reconcilePoisonedQueueWork.ExecuteAsync(
            kind == UnifiedWorkQueueKind.Intake
                ? PoisonedQueueWorkKind.Intake
                : PoisonedQueueWorkKind.External,
            identifier,
            cancellationToken);
    }
}

public sealed partial class StagedArtifactReconciliationFunction(
    ReconcileStagedArtifacts reconcileStagedArtifacts,
    IDocumentContentCacheCleanup documentContentCacheCleanup,
    ReconcilePendingArtifactCustody reconcilePendingArtifactCustody,
    ReconcileGroupedImageIntake reconcileGroupedImageIntake,
    ReconcileUnidentifiedDestinations reconcileUnidentifiedDestinations,
    ReconcileAutomaticVehicleLookups reconcileAutomaticVehicleLookups,
    ReconcileProviderSubmissions reconcileProviderSubmissions,
    ILogger<StagedArtifactReconciliationFunction> logger,
    ReconcileAutomaticEvaSubmissions? reconcileAutomaticEvaSubmissions = null)
{
    [Function(nameof(StagedArtifactReconciliationFunction))]
    public async Task RunAsync(
        [TimerTrigger("%IntakeStagedArtifactReconciliationSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var result = await reconcileStagedArtifacts.ExecuteAsync(50, cancellationToken);
        LogStagedArtifactReconciliation(
            logger,
            result.RecoveredWorkItems,
            result.Completed,
            result.Retained,
            result.Orphans,
            result.Unmatched,
            result.Failures);

        var cache = await documentContentCacheCleanup.ExecuteAsync(50, cancellationToken);
        if (cache.Failures > 0)
        {
            LogDocumentContentCacheCleanupFailure(
                logger,
                cache.Failures,
                cache.Candidates);
        }
        var pendingArtifacts = await reconcilePendingArtifactCustody.ExecuteAsync(
            50,
            cancellationToken);
        if (pendingArtifacts.Failures > 0)
        {
            LogPendingArtifactCustodyFailure(
                logger,
                pendingArtifacts.Failures,
                pendingArtifacts.Candidates);
        }

        // INTK-011: recovers a grouped-image straggler that never got a
        // registered Image intake or an Unidentified reference — re-drives
        // its already-completed work item's safe replay branch, and
        // registers Unidentified directly once it has been pending long
        // enough (the poison-path escape). No manual SQL. Runs on the same
        // existing timer trigger deliberately; this is not a new schedule.
        var groupedImageResult = await reconcileGroupedImageIntake.ExecuteAsync(50, cancellationToken);
        LogGroupedImageIntakeReconciliation(
            logger,
            groupedImageResult.Candidates,
            groupedImageResult.Retried,
            groupedImageResult.Escaped,
            groupedImageResult.Failures);

        // INTK-018: resolves an open Unidentified item whose origin receipt
        // was promoted outside its own processing pass (a sibling group
        // member's registration, a staff action, or a historic stale row) —
        // the product's own reconciliation, never manual SQL. Same existing
        // timer trigger deliberately; this is not a new schedule.
        var unidentifiedResult = await reconcileUnidentifiedDestinations.ExecuteAsync(50, cancellationToken);
        LogUnidentifiedDestinationReconciliation(
            logger,
            unidentifiedResult.Candidates,
            unidentifiedResult.Resolved,
            unidentifiedResult.Failures);

        // CASE-008: any active case whose current registration has never been
        // looked up gets one automatic vehicle lookup enqueued; the existing
        // dispatch timer and unified work queue carry it from there. Same
        // existing timer trigger deliberately; this is not a new schedule.
        var vehicleLookups = await reconcileAutomaticVehicleLookups.ExecuteAsync(50, cancellationToken);
        LogAutomaticVehicleLookups(logger, vehicleLookups);

        // EXT-04: a case sitting in Review whose principal has automatic EVA
        // submission switched on gets one submission enqueued; the existing
        // dispatch timer and unified work queue carry it from there. Same
        // existing timer trigger deliberately; this is not a new schedule.
        //
        // A sweep rather than a hook because three separate places write
        // State = Review, each inside its own transaction — and because a
        // sweep self-heals, where a missed hook would leave a case unsent
        // forever. Null where EVA is not composed, which is the offline
        // profile and any host without credentials.
        if (reconcileAutomaticEvaSubmissions is not null)
        {
            var evaSubmissions = await reconcileAutomaticEvaSubmissions.ExecuteAsync(
                50,
                cancellationToken);
            LogAutomaticEvaSubmissions(logger, evaSubmissions);
        }

        // AUTO-012: repairs the staged-receipt back-reference and the missing
        // first Accepted history row after a process loss between the
        // Provider API's separate writes. Same existing timer trigger
        // deliberately; this is not a new schedule.
        var providerSubmissions = await reconcileProviderSubmissions.ExecuteAsync(
            50,
            cancellationToken);
        LogProviderSubmissionReconciliation(
            logger,
            providerSubmissions.Candidates,
            providerSubmissions.Repaired,
            providerSubmissions.Failures,
            providerSubmissions.FirstFailure);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Enqueued {Enqueued} automatic EVA submissions.")]
    private static partial void LogAutomaticEvaSubmissions(ILogger logger, int enqueued);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Enqueued {Enqueued} automatic vehicle lookups.")]
    private static partial void LogAutomaticVehicleLookups(ILogger logger, int enqueued);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciled staged intake artifacts: {RecoveredWorkItems} work items recovered, {Completed} completed and deleted, {Retained} retained, {Orphans} orphaned, {Unmatched} unmatched, and {Failures} failures.")]
    private static partial void LogStagedArtifactReconciliation(
        ILogger logger,
        int recoveredWorkItems,
        int completed,
        int retained,
        int orphans,
        int unmatched,
        int failures);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Document content cache cleanup failed for {FailureCount} of {CandidateCount} candidates.")]
    private static partial void LogDocumentContentCacheCleanupFailure(
        ILogger logger,
        int failureCount,
        int candidateCount);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Pending artifact custody recovery failed for {FailureCount} of {CandidateCount} candidates.")]
    private static partial void LogPendingArtifactCustodyFailure(
        ILogger logger,
        int failureCount,
        int candidateCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciled grouped image intake stragglers: {Candidates} candidates, {Retried} retried, {Escaped} escaped to Unidentified, {Failures} failures.")]
    private static partial void LogGroupedImageIntakeReconciliation(
        ILogger logger,
        int candidates,
        int retried,
        int escaped,
        int failures);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciled Unidentified destinations: {Candidates} candidates, {Resolved} resolved, {Failures} failures.")]
    private static partial void LogUnidentifiedDestinationReconciliation(
        ILogger logger,
        int candidates,
        int resolved,
        int failures);

    // The cause travels with the count. The sweep swallows every recoverable
    // failure, and a count alone cannot tell a denied permission from a
    // dropped connection -- a distinction no local run can make for us,
    // because tests run full-privilege and the deployed roles do not.
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciled provider submission accepts: {Candidates} candidates, {Repaired} repaired, {Failures} failures. First failure: {FirstFailure}")]
    private static partial void LogProviderSubmissionReconciliation(
        ILogger logger,
        int candidates,
        int repaired,
        int failures,
        string? firstFailure);
}
