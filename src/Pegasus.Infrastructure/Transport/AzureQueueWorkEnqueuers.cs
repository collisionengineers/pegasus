using Azure.Storage.Queues;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Transport;

/// <summary>
/// Transport-only queue senders shared by the Web and Worker composition roots.
/// They publish stable identifiers; durable claim and recovery policy remains in Core.
/// </summary>
public sealed class AzureQueueIntakeWorkEnqueuer(
    QueueClient queueClient,
    bool allowLocalCreateIfNotExists) : IIntakeWorkEnqueuer
{
    public async Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
    {
        if (stagedReceiptId == Guid.Empty)
        {
            throw new ArgumentException("A staged receipt identifier is required.", nameof(stagedReceiptId));
        }

        if (allowLocalCreateIfNotExists)
        {
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
        await queueClient.SendMessageAsync(
            UnifiedWorkQueueMessage.Format(UnifiedWorkQueueKind.Intake, stagedReceiptId),
            cancellationToken: cancellationToken);
    }
}

public sealed class AzureQueueExternalWorkEnqueuer(
    QueueClient queueClient,
    bool allowLocalCreateIfNotExists) : IExternalWorkEnqueuer
{
    public async Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("An external work item identifier is required.", nameof(workItemId));
        }

        if (allowLocalCreateIfNotExists)
        {
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
        await queueClient.SendMessageAsync(
            UnifiedWorkQueueMessage.Format(UnifiedWorkQueueKind.External, workItemId),
            cancellationToken: cancellationToken);
    }
}

public enum UnifiedWorkQueueKind
{
    Intake,
    External
}

/// <summary>
/// The single critical-work queue contract. Both identifiers are GUIDs, so the
/// kind is explicit rather than inferred from a database lookup.
/// </summary>
public static class UnifiedWorkQueueMessage
{
    private const string IntakePrefix = "intake:";
    private const string ExternalPrefix = "external:";

    public static string Format(UnifiedWorkQueueKind kind, Guid identifier)
    {
        if (identifier == Guid.Empty)
        {
            throw new ArgumentException("A durable work identifier is required.", nameof(identifier));
        }

        return kind switch
        {
            UnifiedWorkQueueKind.Intake => $"{IntakePrefix}{identifier:D}",
            UnifiedWorkQueueKind.External => $"{ExternalPrefix}{identifier:D}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public static bool TryParse(
        string? message,
        out UnifiedWorkQueueKind kind,
        out Guid identifier)
    {
        kind = default;
        identifier = Guid.Empty;
        if (message is null)
        {
            return false;
        }

        var prefix = message.StartsWith(IntakePrefix, StringComparison.Ordinal)
            ? IntakePrefix
            : message.StartsWith(ExternalPrefix, StringComparison.Ordinal)
                ? ExternalPrefix
                : null;
        if (prefix is null
            || !Guid.TryParseExact(message[prefix.Length..], "D", out identifier)
            || identifier == Guid.Empty
            || !string.Equals(message, $"{prefix}{identifier:D}", StringComparison.Ordinal))
        {
            identifier = Guid.Empty;
            return false;
        }

        kind = prefix == IntakePrefix
            ? UnifiedWorkQueueKind.Intake
            : UnifiedWorkQueueKind.External;
        return true;
    }
}
