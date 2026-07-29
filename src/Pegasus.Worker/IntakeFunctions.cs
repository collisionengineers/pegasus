using Pegasus.Core.Intake;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

public sealed partial class PendingWorkDispatchFunction(
    DispatchPendingIntakeWork dispatchPendingIntakeWork,
    ILogger<PendingWorkDispatchFunction> logger)
{
    [Function(nameof(PendingWorkDispatchFunction))]
    public async Task RunAsync(
        [TimerTrigger("%IntakeWorkDispatchSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var dispatched = await dispatchPendingIntakeWork.ExecuteAsync(50, cancellationToken);
        LogDispatchedIntakeWork(logger, dispatched);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Dispatched {IntakeWorkCount} durable intake work items.")]
    private static partial void LogDispatchedIntakeWork(ILogger logger, int intakeWorkCount);
}

public sealed class IntakeWorkFunction(ProcessQueuedIntake processQueuedIntake)
{
    [Function(nameof(IntakeWorkFunction))]
    public Task RunAsync(
        [QueueTrigger("intake-work", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message, out var stagedReceiptId))
        {
            throw new InvalidDataException("The intake work message does not contain a staged receipt identifier.");
        }

        return processQueuedIntake.ExecuteAsync(stagedReceiptId, cancellationToken);
    }
}

public sealed class IntakePoisonFunction(ReconcilePoisonedIntakeWork reconcilePoisonedIntakeWork)
{
    [Function(nameof(IntakePoisonFunction))]
    public Task RunAsync(
        [QueueTrigger("intake-work-poison", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message, out var stagedReceiptId))
        {
            throw new InvalidDataException("The intake poison message does not contain a staged receipt identifier.");
        }

        return reconcilePoisonedIntakeWork.ExecuteAsync(stagedReceiptId, cancellationToken);
    }
}

public sealed partial class StagedArtifactReconciliationFunction(
    ReconcileStagedIntakeArtifacts reconcileStagedIntakeArtifacts,
    ILogger<StagedArtifactReconciliationFunction> logger)
{
    [Function(nameof(StagedArtifactReconciliationFunction))]
    public async Task RunAsync(
        [TimerTrigger("%IntakeStagedArtifactReconciliationSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var reconciled = await reconcileStagedIntakeArtifacts.ExecuteAsync(50, cancellationToken);
        LogReconciledIntakeWorkLeases(logger, reconciled);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciled {IntakeWorkCount} expired intake work leases.")]
    private static partial void LogReconciledIntakeWorkLeases(ILogger logger, int intakeWorkCount);
}
