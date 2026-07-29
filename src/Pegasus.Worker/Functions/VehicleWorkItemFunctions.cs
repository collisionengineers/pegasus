using Microsoft.Azure.Functions.Worker;
using Pegasus.Core.Vehicle;

namespace Pegasus.Worker.Functions;

public sealed class ExternalWorkFunction(ProcessQueuedVehicleLookup processQueuedVehicleLookup)
{
    [Function(nameof(ExternalWorkFunction))]
    public Task RunAsync(
        [QueueTrigger("external-work", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message, out var workItemId) || workItemId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The external work message does not contain a vehicle lookup work item identifier.");
        }

        return processQueuedVehicleLookup.ExecuteAsync(workItemId, cancellationToken);
    }
}

public sealed class ExternalPoisonFunction(ReconcilePoisonedVehicleLookup reconcilePoisonedVehicleLookup)
{
    [Function(nameof(ExternalPoisonFunction))]
    public Task RunAsync(
        [QueueTrigger("external-work-poison", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message, out var workItemId) || workItemId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The external poison message does not contain a vehicle lookup work item identifier.");
        }

        return reconcilePoisonedVehicleLookup.ExecuteAsync(workItemId, cancellationToken);
    }
}
