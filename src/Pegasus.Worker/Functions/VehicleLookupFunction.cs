using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Vehicle;

namespace Pegasus.Worker.Functions;

public sealed class VehicleLookupFunction(
    ProcessQueuedVehicleLookup processQueuedVehicleLookup,
    ILogger<VehicleLookupFunction> logger)
{
    internal const string QueueName = "vehicle-lookup";
    private const string FunctionName = nameof(VehicleLookupFunction);

    [Function(FunctionName)]
    public async Task RunAsync(
        [QueueTrigger(QueueName, Connection = "AzureWebJobsStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var scope = FunctionTelemetry.BeginQueueAttempt(logger, FunctionName, QueueName, message);
        try
        {
            var workItemId = ParseWorkItemId(message);
            await processQueuedVehicleLookup.ExecuteAsync(workItemId, cancellationToken);
            FunctionTelemetry.CompleteQueueAttempt(logger, FunctionName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            FunctionTelemetry.FailQueueAttempt(logger, FunctionName, exception);
            throw;
        }
    }

    private static Guid ParseWorkItemId(QueueMessage message)
    {
        var body = message.Body.ToString();
        if (!Guid.TryParseExact(body, "D", out var workItemId) || workItemId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The vehicle lookup message must contain one non-empty work item identifier in canonical form.");
        }

        return workItemId;
    }
}
