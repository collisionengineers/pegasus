using Azure.Storage.Queues;
using Pegasus.Core.Custody;

namespace Pegasus.Worker;

internal sealed class AzureQueueExternalWorkQueue : IExternalWorkEnqueuer
{
    private readonly QueueClient queueClient;
    private readonly WorkerStorageProvisioning storageProvisioning;

    public AzureQueueExternalWorkQueue(
        WorkerQueueClients queueClients,
        WorkerStorageProvisioning storageProvisioning)
    {
        queueClient = queueClients.ExternalWorkQueue;
        this.storageProvisioning = storageProvisioning;
    }

    public async Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        await storageProvisioning.EnsureQueueExistsAsync(queueClient, cancellationToken);
        await queueClient.SendMessageAsync(workItemId.ToString("D"), cancellationToken: cancellationToken);
    }
}
