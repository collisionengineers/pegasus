using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Data.SqlClient;

namespace Pegasus.Infrastructure.Maintenance;

public static class ProductionIntakeCleanBaselineCommand
{
    public static async Task<string> RunJsonAsync(
        string invocationJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationJson);
        var invocation = JsonSerializer.Deserialize(
            invocationJson,
            CleanBaselineJsonContext.Default.ProductionIntakeCleanBaselineInvocation)
            ?? throw new InvalidDataException("The clean-baseline invocation is empty.");
        IntakeCleanBaselineService.ValidateInvocation(invocation);
        var evidence = CleanBaselineAccessEvidenceValidator.Load(invocation, TimeProvider.System);

        var session = new NamedOperatorTokenSession(invocation);
        var storageCredential = new NamedOperatorTokenCredential(
            session,
            "https://storage.azure.com");
        var blobContainer = new BlobContainerClient(
            new Uri(
                $"https://{invocation.StorageAccount}.blob.core.windows.net/{invocation.BlobContainer}"),
            storageCredential);
        var queueService = new QueueServiceClient(
            new Uri($"https://{invocation.StorageAccount}.queue.core.windows.net"),
            storageCredential);
        var queueClients = CleanBaselineQueueStore.QueueNames.ToDictionary(
            name => name,
            queueService.GetQueueClient,
            StringComparer.Ordinal);

        var sqlConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = invocation.SqlServer,
            InitialCatalog = invocation.SqlDatabase,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 15,
            MultipleActiveResultSets = false
        }.ConnectionString;
        var sql = new CleanBaselineSqlStore(
            sqlConnectionString,
            async cancellation => await session.GetTokenAsync(
                NamedOperatorTokenSession.Scope("https://database.windows.net"),
                cancellation));
        using var graphHttp = new HttpClient
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"),
            Timeout = TimeSpan.FromSeconds(100)
        };
        var graph = new CleanBaselineGraphClient(graphHttp, session, invocation);
        var validator = new CleanBaselineAccessValidator(
            invocation,
            evidence,
            session,
            sql,
            graph,
            blobContainer,
            queueClients);
        var service = new IntakeCleanBaselineService(
            invocation,
            validator,
            sql,
            new CleanBaselineBlobStore(blobContainer),
            new CleanBaselineQueueStore(queueClients),
            graph,
            TimeProvider.System);
        return await service.RunAsync(cancellationToken);
    }
}
