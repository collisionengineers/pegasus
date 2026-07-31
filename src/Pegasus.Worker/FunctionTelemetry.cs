using System.Diagnostics;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

internal static partial class FunctionTelemetry
{
    private static readonly Func<ILogger, string, string, string, long, IDisposable?> QueueAttemptScope =
        LoggerMessage.DefineScope<string, string, string, long>(
            "Function {FunctionName}; queue {QueueName}; message {MessageId}; delivery {DeliveryCount}");

    public static IDisposable? BeginQueueAttempt(
        ILogger logger,
        string functionName,
        string queueName,
        QueueMessage message)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);

        var activity = Activity.Current;
        activity?.SetTag("messaging.system", "azure_queue_storage");
        activity?.SetTag("messaging.operation.name", functionName);
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.destination.name", queueName);
        activity?.SetTag("messaging.message.id", message.MessageId);
        activity?.SetTag("messaging.message.delivery_count", message.DequeueCount);

        LogQueueAttemptStarted(logger, functionName, queueName, message.MessageId, message.DequeueCount);
        return QueueAttemptScope(
            logger,
            functionName,
            queueName,
            message.MessageId,
            message.DequeueCount);
    }

    public static void CompleteQueueAttempt(ILogger logger, string functionName)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        Activity.Current?.SetStatus(ActivityStatusCode.Ok);
        LogQueueAttemptCompleted(logger, functionName);
    }

    public static void FailQueueAttempt(ILogger logger, string functionName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(exception);

        var activity = Activity.Current;
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        activity?.SetTag("error.type", exception.GetType().FullName);
        LogQueueAttemptFailed(logger, functionName, exception.GetType().Name);
    }

    [LoggerMessage(
        EventId = 9100,
        Level = LogLevel.Information,
        Message = "Starting {FunctionName} for queue {QueueName}, message {MessageId}, delivery {DeliveryCount}.")]
    private static partial void LogQueueAttemptStarted(
        ILogger logger,
        string functionName,
        string queueName,
        string messageId,
        long deliveryCount);

    [LoggerMessage(
        EventId = 9101,
        Level = LogLevel.Information,
        Message = "Completed {FunctionName}.")]
    private static partial void LogQueueAttemptCompleted(ILogger logger, string functionName);

    [LoggerMessage(
        EventId = 9102,
        Level = LogLevel.Error,
        Message = "Failed {FunctionName} with {ExceptionType}.")]
    private static partial void LogQueueAttemptFailed(
        ILogger logger,
        string functionName,
        string exceptionType);
}
