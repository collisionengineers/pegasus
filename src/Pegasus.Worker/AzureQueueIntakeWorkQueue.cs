using Azure.Storage.Queues;
using Pegasus.Core.Intake;

namespace Pegasus.Worker;

internal sealed class AzureQueueIntakeWorkQueue : IIntakeWorkEnqueuer
{
    private readonly QueueClient queueClient;
    private readonly WorkerStorageProvisioning storageProvisioning;

    public AzureQueueIntakeWorkQueue(
        WorkerQueueClients queueClients,
        WorkerStorageProvisioning storageProvisioning)
    {
        queueClient = queueClients.IntakeWorkQueue;
        this.storageProvisioning = storageProvisioning;
    }

    public async Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
    {
        await storageProvisioning.EnsureQueueExistsAsync(queueClient, cancellationToken);
        await queueClient.SendMessageAsync(stagedReceiptId.ToString("D"), cancellationToken: cancellationToken);
    }
}
