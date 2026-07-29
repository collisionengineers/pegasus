using Microsoft.Azure.Functions.Worker;
using Pegasus.Core.Custody;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker.Functions;

public sealed partial class ExternalWorkDispatchFunction(
    DispatchPendingExternalWork dispatchPendingExternalWork,
    ILogger<ExternalWorkDispatchFunction> logger)
{
    [Function(nameof(ExternalWorkDispatchFunction))]
    public async Task RunAsync(
        [TimerTrigger("%ExternalWorkDispatchSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var dispatched = await dispatchPendingExternalWork.ExecuteAsync(50, cancellationToken);
        LogDispatchedExternalWork(logger, dispatched);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Dispatched {ExternalWorkCount} durable external work items.")]
    private static partial void LogDispatchedExternalWork(ILogger logger, int externalWorkCount);
}

public sealed class ExternalWorkFunction(IProcessQueuedCustody processQueuedCustody)
{
    [Function(nameof(ExternalWorkFunction))]
    public Task RunAsync(
        [QueueTrigger("external-work", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message, out var workItemId) || workItemId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The external work message does not contain a custody work item identifier.");
        }

        return processQueuedCustody.ExecuteAsync(workItemId, cancellationToken);
    }
}

public sealed class ExternalPoisonFunction(
    ReconcilePoisonedExternalWork reconcilePoisonedExternalWork)
{
    [Function(nameof(ExternalPoisonFunction))]
    public Task RunAsync(
        [QueueTrigger("external-work-poison", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message, out var workItemId) || workItemId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The external poison message does not contain a custody work item identifier.");
        }

        return reconcilePoisonedExternalWork.ExecuteAsync(workItemId, cancellationToken);
    }
}

