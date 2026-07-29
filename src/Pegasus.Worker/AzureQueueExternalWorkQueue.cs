using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Pegasus.Core.Custody;

namespace Pegasus.Worker;

internal sealed class AzureQueueExternalWorkQueue : IExternalWorkEnqueuer
{
    private readonly QueueClient queueClient;

    public AzureQueueExternalWorkQueue(IConfiguration configuration)
    {
        var serviceUri = configuration["ExternalWorkQueue:ServiceUri"]
            ?? configuration["IntakeQueue:ServiceUri"];
        queueClient = !string.IsNullOrWhiteSpace(serviceUri)
            ? new QueueServiceClient(new Uri(serviceUri, UriKind.Absolute), new DefaultAzureCredential())
                .GetQueueClient("external-work")
            : new QueueClient(
                configuration.GetConnectionString("AzureWebJobsStorage")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException(
                        "ExternalWorkQueue:ServiceUri or a Development storage connection is required."),
                "external-work");
    }

    public async Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await queueClient.SendMessageAsync(workItemId.ToString("D"), cancellationToken: cancellationToken);
    }
}
