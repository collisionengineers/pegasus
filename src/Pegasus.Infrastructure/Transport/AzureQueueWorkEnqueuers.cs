using Azure.Storage.Queues;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;

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

public sealed class AzureQueueMailboxWakeEnqueuer(
    QueueClient queueClient,
    bool allowLocalCreateIfNotExists) : IMailboxWakeEnqueuer
{
    public async Task EnqueueAsync(
        Guid approvedMailboxId,
        Guid subscriptionId,
        long generation,
        MailboxWakeKind wakeKind,
        string? immutableMessageId,
        CancellationToken cancellationToken)
    {
        if (approvedMailboxId == Guid.Empty || subscriptionId == Guid.Empty)
        {
            throw new ArgumentException("Mailbox and subscription identifiers are required.");
        }

        if (allowLocalCreateIfNotExists)
        {
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        await queueClient.SendMessageAsync(
            UnifiedWorkQueueMessage.FormatMailbox(
                approvedMailboxId,
                subscriptionId,
                generation,
                wakeKind,
                immutableMessageId),
            cancellationToken: cancellationToken);
    }
}

/// <summary>
/// The single critical-work queue contract. Both identifiers are GUIDs, so the
/// kind is explicit rather than inferred from a database lookup.
/// </summary>
public static class UnifiedWorkQueueMessage
{
    private const string IntakePrefix = "intake:";
    private const string ExternalPrefix = "external:";
    private const string MailboxPrefix = "mailbox:";

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

    public static string FormatMailbox(
        Guid approvedMailboxId,
        Guid subscriptionId,
        long generation,
        MailboxWakeKind wakeKind,
        string? immutableMessageId)
    {
        if (approvedMailboxId == Guid.Empty || subscriptionId == Guid.Empty)
        {
            throw new ArgumentException("Mailbox and subscription identifiers are required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);
        if (immutableMessageId is { } id
            && (id.Length is 0 or > 500
                || id.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))))
        {
            throw new ArgumentException("The immutable message identity is invalid.", nameof(immutableMessageId));
        }
        var messageIdentity = immutableMessageId is null
            ? "-"
            : Uri.EscapeDataString(immutableMessageId);
        return $"{MailboxPrefix}{approvedMailboxId:D}:{subscriptionId:D}:{generation}:{wakeKind}:{messageIdentity}";
    }

    public static bool TryParseMailbox(
        string? message,
        out Guid approvedMailboxId,
        out Guid subscriptionId,
        out long generation,
        out MailboxWakeKind wakeKind,
        out string? immutableMessageId)
    {
        approvedMailboxId = Guid.Empty;
        subscriptionId = Guid.Empty;
        generation = 0;
        wakeKind = default;
        immutableMessageId = null;
        if (message is null || !message.StartsWith(MailboxPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = message[MailboxPrefix.Length..].Split(':');
        if (parts.Length != 5
            || !Guid.TryParseExact(parts[0], "D", out approvedMailboxId)
            || approvedMailboxId == Guid.Empty
            || !Guid.TryParseExact(parts[1], "D", out subscriptionId)
            || subscriptionId == Guid.Empty
            || !long.TryParse(parts[2], out generation)
            || generation <= 0
            || !Enum.TryParse(parts[3], ignoreCase: false, out wakeKind)
            || !Enum.IsDefined(wakeKind))
        {
            return false;
        }
        immutableMessageId = parts[4] == "-" ? null : Uri.UnescapeDataString(parts[4]);
        return string.Equals(
            message,
            FormatMailbox(approvedMailboxId, subscriptionId, generation, wakeKind, immutableMessageId),
            StringComparison.Ordinal);
    }
}
