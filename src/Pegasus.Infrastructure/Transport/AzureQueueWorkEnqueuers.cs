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
            stagedReceiptId.ToString("D"),
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
            workItemId.ToString("D"),
            cancellationToken: cancellationToken);
    }
}
