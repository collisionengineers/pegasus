using Pegasus.Core.Vehicle;
using Pegasus.Core.Intake;
using Pegasus.Core.Custody;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

public sealed partial class PendingWorkDispatchFunction(
    DispatchPendingWork dispatchPendingWork,
    ILogger<PendingWorkDispatchFunction> logger)
{
    [Function(nameof(PendingWorkDispatchFunction))]
    public async Task RunAsync(
        [TimerTrigger("%PendingWorkDispatchSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var dispatched = await dispatchPendingWork.ExecuteAsync(50, cancellationToken);
        LogDispatchedWork(logger, dispatched.IntakeWorkCount, dispatched.ExternalWorkCount);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Dispatched {IntakeWorkCount} intake and {ExternalWorkCount} external durable work items.")]
    private static partial void LogDispatchedWork(
        ILogger logger,
        int intakeWorkCount,
        int externalWorkCount);
}

public sealed class IntakeWorkFunction(ProcessQueuedIntake processQueuedIntake)
{
    [Function(nameof(IntakeWorkFunction))]
    public Task RunAsync(
        [QueueTrigger("intake-work", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!QueueMessageIdentifier.TryParse(message, out var stagedReceiptId))
        {
            throw new InvalidDataException(
                "The intake work message does not contain one canonical staged receipt identifier.");
        }

        return processQueuedIntake.ExecuteAsync(stagedReceiptId, cancellationToken);
    }
}

public sealed class IntakePoisonFunction(ReconcilePoisonedQueueWork reconcilePoisonedQueueWork)
{
    [Function(nameof(IntakePoisonFunction))]
    public Task RunAsync(
        [QueueTrigger("intake-work-poison", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!QueueMessageIdentifier.TryParse(message, out var stagedReceiptId))
        {
            throw new InvalidDataException(
                "The intake poison message does not contain one canonical staged receipt identifier.");
        }

        return reconcilePoisonedQueueWork.ExecuteAsync(
            PoisonedQueueWorkKind.Intake,
            stagedReceiptId,
            cancellationToken);
    }
}

public sealed partial class StagedArtifactReconciliationFunction(
    ReconcileStagedArtifacts reconcileStagedArtifacts,
    ReconcileGroupedImageIntake reconcileGroupedImageIntake,
    ReconcileUnidentifiedDestinations reconcileUnidentifiedDestinations,
    ReconcileAutomaticVehicleLookups reconcileAutomaticVehicleLookups,
    ILogger<StagedArtifactReconciliationFunction> logger)
{
    [Function(nameof(StagedArtifactReconciliationFunction))]
    public async Task RunAsync(
        [TimerTrigger("%IntakeStagedArtifactReconciliationSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var result = await reconcileStagedArtifacts.ExecuteAsync(50, cancellationToken);
        LogStagedArtifactReconciliation(
            logger,
            result.RecoveredLeases,
            result.Completed,
            result.Retained,
            result.Orphans,
            result.Unmatched,
            result.Failures);

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
        // dispatch timer and external-work queue carry it from there. Same
        // existing timer trigger deliberately; this is not a new schedule.
        var vehicleLookups = await reconcileAutomaticVehicleLookups.ExecuteAsync(50, cancellationToken);
        LogAutomaticVehicleLookups(logger, vehicleLookups);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Enqueued {Enqueued} automatic vehicle lookups.")]
    private static partial void LogAutomaticVehicleLookups(ILogger logger, int enqueued);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciled staged intake artifacts: {RecoveredLeases} leases recovered, {Completed} completed and deleted, {Retained} retained, {Orphans} orphaned, {Unmatched} unmatched, and {Failures} failures.")]
    private static partial void LogStagedArtifactReconciliation(
        ILogger logger,
        int recoveredLeases,
        int completed,
        int retained,
        int orphans,
        int unmatched,
        int failures);

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
}

internal static class QueueMessageIdentifier
{
    public static bool TryParse(string message, out Guid identifier)
    {
        if (Guid.TryParseExact(message, "D", out identifier)
            && identifier != Guid.Empty
            && string.Equals(message, identifier.ToString("D"), StringComparison.Ordinal))
        {
            return true;
        }

        identifier = Guid.Empty;
        return false;
    }
}
