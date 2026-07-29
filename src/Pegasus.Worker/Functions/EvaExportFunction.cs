using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker.Functions;

public sealed class EvaExportFunction(ILogger<EvaExportFunction> logger)
{
    internal const string QueueName = "eva-export";
    private const string FunctionName = nameof(EvaExportFunction);
    private const string ActivationGateMessage =
        "EVA export is not activated: the reviewed 13-key source mapping and approved drag-and-drop evidence are absent.";

    [Function(FunctionName)]
    public Task RunAsync(
        [QueueTrigger(QueueName, Connection = "AzureWebJobsStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var scope = FunctionTelemetry.BeginQueueAttempt(logger, FunctionName, QueueName, message);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = ParseWorkItemId(message);
            throw new InvalidOperationException(ActivationGateMessage);
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
                "The EVA export message must contain one non-empty work item identifier in canonical form.");
        }

        return workItemId;
    }
}
