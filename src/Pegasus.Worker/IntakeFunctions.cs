using Pegasus.Core.Vehicle;
using Pegasus.Core.Intake;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Core.PrincipalApi;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Transport;
using Pegasus.Infrastructure.Custody;
using System.Runtime.ExceptionServices;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

/// <summary>
/// Slow reconciliation for publication attempts missed after their durable
/// commit. Ordinary intake is published directly by its committing caller.
/// Every fifth minute the same run also does the two jobs that once had their
/// own five-minute timers, so they use this timer's warm instance instead of
/// starting a cold one: the due-work sweep, then the approved-inbox fallback
/// poll (Graph subscription maintenance first).
/// </summary>
/// <remarks>
/// The dispatch runs first and nothing below delays it. Each of the two jobs
/// has its own time budget and its own failure handling, so one failing or
/// slow job never stops the other or fails the dispatch. A dispatch failure is
/// held until both jobs have run, so a failing dispatch does not stop the
/// fallback poll either. The minute comes from the clock, not from the timer's
/// schedule status: the timer keeps no monitor state.
/// </remarks>
public sealed partial class PendingWorkRecoveryFunction(
    DispatchPendingWork dispatchPendingWork,
    RunDueChasers runDueChasers,
    MaintainMailboxChangeSubscriptions maintainMailboxChangeSubscriptions,
    PollApprovedInbox pollApprovedInbox,
    TimeProvider timeProvider,
    ILogger<PendingWorkRecoveryFunction> logger)
{
    /// <summary>The folded jobs run on every minute that divides by this.</summary>
    private const int FoldedJobMinuteInterval = 5;

    private static readonly TimeSpan FoldedJobBudget = TimeSpan.FromSeconds(60);

    private static readonly ActionActor InboxPollActor =
        ActionActor.SystemWorker("approved-inbox-poller");

    [Function(nameof(PendingWorkRecoveryFunction))]
    public async Task RunAsync(
        [TimerTrigger("%PendingWorkRecoverySchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        // Read before the dispatch: a slow dispatch must not push this run into
        // the next minute and skip the folded jobs.
        var runFoldedJobs = timeProvider.GetUtcNow().Minute % FoldedJobMinuteInterval == 0;

        ExceptionDispatchInfo? dispatchFailure = null;
        try
        {
            var dispatched = await dispatchPendingWork.ExecuteAsync(50, cancellationToken);
            LogDispatchedWork(logger, dispatched.IntakeWorkCount, dispatched.ExternalWorkCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dispatchFailure = ExceptionDispatchInfo.Capture(exception);
        }

        if (runFoldedJobs)
        {
            await RunFoldedJobAsync("due-work sweep", RunDueWorkSweepAsync, cancellationToken);
            await RunFoldedJobAsync("approved-inbox recovery", RunInboxRecoveryAsync, cancellationToken);
        }

        dispatchFailure?.Throw();
    }

    private async Task RunDueWorkSweepAsync(CancellationToken cancellationToken)
    {
        var result = await runDueChasers.ExecuteAsync(
            maximumItems: 50,
            cancellationToken);
        LogSweepOutcome(
            logger,
            result.ExaminedCount,
            result.GeneratedCount,
            result.ReplayCount,
            result.SupersededCount);
    }

    private async Task RunInboxRecoveryAsync(CancellationToken cancellationToken)
    {
        await maintainMailboxChangeSubscriptions.ExecuteAsync(cancellationToken);
        var handled = await pollApprovedInbox.ExecuteAsync(
            50,
            InboxPollActor,
            cancellationToken);
        LogApprovedInboxPoll(logger, handled);
    }

    private async Task RunFoldedJobAsync(
        string job,
        Func<CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        using var budget = new CancellationTokenSource(FoldedJobBudget, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            budget.Token);
        try
        {
            await run(linked.Token);
        }
        catch (OperationCanceledException)
            when (budget.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            LogFoldedJobOverBudget(logger, job, (int)FoldedJobBudget.TotalSeconds);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogFoldedJobFailed(logger, exception, job);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Recovered publication for {IntakeWorkCount} intake and {ExternalWorkCount} external durable work items.")]
    private static partial void LogDispatchedWork(
        ILogger logger,
        int intakeWorkCount,
        int externalWorkCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Examined {ExaminedCount} due-work occurrences and persisted {GeneratedCount} copyable chaser drafts; {ReplayCount} were replays and {SupersededCount} were superseded. No outbound communication was attempted and no sending, receipt, or delivery was claimed.")]
    private static partial void LogSweepOutcome(
        ILogger logger,
        int examinedCount,
        int generatedCount,
        int replayCount,
        int supersededCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Handled {ApprovedInboxMessageCount} immutable approved-inbox messages through durable intake or poison recovery.")]
    private static partial void LogApprovedInboxPoll(
        ILogger logger,
        int approvedInboxMessageCount);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The {Job} job failed; the next fifth minute tries again.")]
    private static partial void LogFoldedJobFailed(
        ILogger logger,
        Exception exception,
        string job);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The {Job} job passed its {BudgetSeconds} second budget and was cancelled; the next fifth minute tries again.")]
    private static partial void LogFoldedJobOverBudget(
        ILogger logger,
        string job,
        int budgetSeconds);
}

/// <summary>Replays only automatic EVA Review intentions that committed with a Case transition.</summary>
public sealed partial class AutomaticEvaReviewSubmissionFunction(
    ProcessAutomaticEvaReviewSubmissions processAutomaticEvaReviewSubmissions,
    ILogger<AutomaticEvaReviewSubmissionFunction> logger)
{
    [Function(nameof(AutomaticEvaReviewSubmissionFunction))]
    public async Task RunAsync(
        [TimerTrigger("%AutomaticEvaReviewSubmissionSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var processed = await processAutomaticEvaReviewSubmissions.ExecuteAsync(50, cancellationToken);
        LogProcessed(logger, processed);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processed {Count} automatic EVA Review submissions.")]
    private static partial void LogProcessed(ILogger logger, int count);
}

public sealed partial class UnifiedWorkFunction(
    IProcessQueuedIntake processQueuedIntake,
    IProcessQueuedExternalWork processQueuedExternalWork,
    PollApprovedInbox pollApprovedInbox,
    IApprovedMailboxSubscriptionStore mailboxSubscriptions,
    TimeProvider timeProvider,
    ILogger<UnifiedWorkFunction> logger)
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
                try
                {
                    await processQueuedExternalWork.ExecuteAsync(identifier, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The queue's own retry and poison handling is unchanged; the failure
                    // is named here because a store-level throw otherwise leaves only a
                    // poisoned work item and no line saying why.
                    LogExternalWorkFailed(logger, exception, identifier);
                    throw;
                }
                return;
            default:
                throw new InvalidDataException("The unified work message has an unsupported kind.");
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "External work item {WorkItemId} failed; the queue retry policy decides the next attempt.")]
    private static partial void LogExternalWorkFailed(ILogger logger, Exception exception, Guid workItemId);

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
    ISettleFiledCaseReportArtifacts settleFiledCaseReportArtifacts,
    ReconcileGroupedImageIntake reconcileGroupedImageIntake,
    IImageIntakeCasePairing imageIntakeCasePairing,
    ITriageCasePairing triageCasePairing,
    ReconcileUnidentifiedDestinations reconcileUnidentifiedDestinations,
    ReconcileAutomaticVehicleLookups reconcileAutomaticVehicleLookups,
    ReconcilePrincipalSubmissions reconcilePrincipalSubmissions,
    PurgeStaffNotifications purgeStaffNotifications,
    PrepareDocumentThumbnails prepareDocumentThumbnails,
    ILogger<StagedArtifactReconciliationFunction> logger)
{
    /// <summary>
    /// How many plain thumbnails one run makes. The sweep runs every few
    /// seconds, so a small number keeps up with filing without a burst on Box.
    /// </summary>
    private const int ThumbnailsPerRun = 2;

    /// <summary>
    /// The longest the thumbnail sweep may take in one run. The intake work
    /// above has already run; this keeps the timer from being held by Box.
    /// </summary>
    private static readonly TimeSpan ThumbnailBudget = TimeSpan.FromSeconds(20);

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

        // A generated report whose file was filed after its request ended
        // is recorded as stored, so nobody has to press Generate report
        // again. Same existing timer trigger deliberately; this is not a new
        // schedule. A report that cannot be settled is named here with its
        // cause and the intake steps below still run.
        try
        {
            var settledReportFiles = await settleFiledCaseReportArtifacts.ExecuteAsync(50, cancellationToken);
            LogFiledReportSettlement(logger, settledReportFiles);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFiledReportSettlementFailed(logger, exception);
        }

        // Recovers a grouped-image straggler that never got a
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

        var pairing = await imageIntakeCasePairing.ReconcileAsync(50, cancellationToken);
        LogImageIntakePairing(logger, pairing.Candidates, pairing.Merged, pairing.Failures, pairing.FirstFailure);
        var triagePairing = await triageCasePairing.ReconcileAsync(50, cancellationToken);
        LogTriageCasePairing(logger, triagePairing.Candidates, triagePairing.Linked,
            triagePairing.Failures, triagePairing.FirstFailure);

        // Resolves an open Unidentified item whose origin receipt
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

        // Any active case whose current registration has never been
        // looked up gets one automatic vehicle lookup enqueued; the existing
        // dispatch timer and unified work queue carry it from there. Same
        // existing timer trigger deliberately; this is not a new schedule.
        var vehicleLookups = await reconcileAutomaticVehicleLookups.ExecuteAsync(50, cancellationToken);
        LogAutomaticVehicleLookups(logger, vehicleLookups);

        // Repairs the staged-receipt back-reference and the missing
        // first Accepted history row after a process loss between the
        // Principal API's separate writes. Same existing timer trigger
        // deliberately; this is not a new schedule.
        var principalSubmissions = await reconcilePrincipalSubmissions.ExecuteAsync(
            50,
            cancellationToken);
        LogPrincipalSubmissionReconciliation(
            logger,
            principalSubmissions.Candidates,
            principalSubmissions.Repaired,
            principalSubmissions.Failures,
            principalSubmissions.FirstFailure);

        // Work Centre D10: personal notifications past their 30-day retention drop
        // off. Reads already filter on the window, so this is housekeeping on the
        // existing sweep, not a new schedule.
        var purgedNotifications = await purgeStaffNotifications.ExecuteAsync(cancellationToken);
        LogStaffNotificationPurge(logger, purgedNotifications);

        // Plain gallery thumbnails of newly filed photographs, made before
        // the first view. Same existing timer trigger deliberately; this is
        // not a new schedule. It runs last, inside its own time budget, and
        // its failure or timeout is the thumbnail's alone: it is logged, the
        // first view makes the thumbnail instead, and the run still succeeds.
        using var thumbnailBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        thumbnailBudget.CancelAfter(ThumbnailBudget);
        try
        {
            var thumbnails = await prepareDocumentThumbnails.ExecuteAsync(ThumbnailsPerRun, thumbnailBudget.Token);
            if (thumbnails.Candidates > 0)
            {
                LogDocumentThumbnailPreparation(
                    logger,
                    thumbnails.Candidates,
                    thumbnails.Prepared,
                    thumbnails.Unrenderable,
                    thumbnails.Failures,
                    thumbnails.FirstFailure);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogDocumentThumbnailPreparationFailed(logger, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Prepared document thumbnails: {Candidates} candidates, {Prepared} prepared, {Unrenderable} not renderable, {Failures} failures. First failure: {FirstFailure}")]
    private static partial void LogDocumentThumbnailPreparation(
        ILogger logger, int candidates, int prepared, int unrenderable, int failures, string? firstFailure);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Preparing document thumbnails failed; the next sweep tries again.")]
    private static partial void LogDocumentThumbnailPreparationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Enqueued {Enqueued} automatic vehicle lookups.")]
    private static partial void LogAutomaticVehicleLookups(ILogger logger, int enqueued);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reconciled registered image pairing: {Candidates} candidates, {Merged} merged, {Failures} failures. First failure: {FirstFailure}")]
    private static partial void LogImageIntakePairing(
        ILogger logger, int candidates, int merged, int failures, string? firstFailure);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reconciled Triage pairing: {Candidates} candidates, {Linked} linked, {Failures} failures. First failure: {FirstFailure}")]
    private static partial void LogTriageCasePairing(
        ILogger logger, int candidates, int linked, int failures, string? firstFailure);

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
        Message = "Settled {Settled} generated report files filed after their request ended.")]
    private static partial void LogFiledReportSettlement(ILogger logger, int settled);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Settling generated report files failed; the next sweep tries again.")]
    private static partial void LogFiledReportSettlementFailed(ILogger logger, Exception exception);

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
        Message = "Reconciled principal submission accepts: {Candidates} candidates, {Repaired} repaired, {Failures} failures. First failure: {FirstFailure}")]
    private static partial void LogPrincipalSubmissionReconciliation(
        ILogger logger,
        int candidates,
        int repaired,
        int failures,
        string? firstFailure);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Purged {Purged} staff notifications past retention.")]
    private static partial void LogStaffNotificationPurge(ILogger logger, int purged);
}
