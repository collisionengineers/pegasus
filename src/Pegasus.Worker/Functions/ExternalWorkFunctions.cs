using Microsoft.Azure.Functions.Worker;
using Pegasus.Core.Custody;

namespace Pegasus.Worker.Functions;

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

public sealed class ExternalPoisonFunction(IProcessQueuedCustody processQueuedCustody)
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

        return processQueuedCustody.ExecuteAsync(workItemId, cancellationToken);
    }
}

