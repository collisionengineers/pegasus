using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Pegasus.Core.Intake;

namespace Pegasus.Worker;

internal sealed class AzureQueueIntakeWorkQueue : IIntakeWorkEnqueuer
{
    private readonly QueueClient queueClient;

    public AzureQueueIntakeWorkQueue(IConfiguration configuration)
    {
        var serviceUri = configuration["IntakeQueue:ServiceUri"];
        queueClient = !string.IsNullOrWhiteSpace(serviceUri)
            ? new QueueServiceClient(new Uri(serviceUri, UriKind.Absolute), new DefaultAzureCredential())
                .GetQueueClient("intake-work")
            : new QueueClient(
                configuration.GetConnectionString("AzureWebJobsStorage")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException(
                        "IntakeQueue:ServiceUri or AzureWebJobsStorage is required for intake work dispatch."),
                "intake-work");
    }

    public async Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await queueClient.SendMessageAsync(stagedReceiptId.ToString("D"), cancellationToken: cancellationToken);
    }
}
