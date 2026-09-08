using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.Worker;

internal sealed record WorkerAzureProductionOptions(
    Guid WorkerClientId,
    Uri IntakeStorageServiceUri,
    Uri IntakeQueueServiceUri);

internal sealed record WorkerQueueClients(
    QueueClient WorkQueue);

internal sealed class WorkerStorageProvisioning(bool allowLocalCreateIfNotExists)
{
    internal bool AllowLocalCreateIfNotExists { get; } = allowLocalCreateIfNotExists;

    internal async ValueTask EnsureQueueExistsAsync(
        QueueClient queueClient,
        CancellationToken cancellationToken)
    {
        if (AllowLocalCreateIfNotExists)
        {
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }

    internal async ValueTask EnsureContainerExistsAsync(
        BlobContainerClient containerClient,
        CancellationToken cancellationToken)
    {
        if (AllowLocalCreateIfNotExists)
        {
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }
}

internal sealed class WorkerAzureClientRegistration(
    WorkerQueueClients queueClients,
    WorkerStorageProvisioning storageProvisioning,
    BlobContainerClient? intakeArtifactContainer = null,
    WorkerAzureProductionOptions? productionOptions = null,
    DefaultAzureCredentialOptions? credentialOptions = null,
    TokenCredential? credential = null)
{
    internal void AddTo(IServiceCollection services)
    {
        services.AddSingleton(queueClients);
        services.AddSingleton(storageProvisioning);

        if (intakeArtifactContainer is not null)
        {
            services.AddSingleton(intakeArtifactContainer);
        }

        if (productionOptions is not null && credentialOptions is not null && credential is not null)
        {
            services.AddSingleton(productionOptions);
            services.AddSingleton(credentialOptions);
            services.AddSingleton(credential);
        }
    }
}

internal static class WorkerAzureClientFactory
{
    internal const string WorkerClientIdKey = "AzureIdentity:WorkerClientId";
    internal const string IntakeStorageServiceUriKey = "IntakeStorage:ServiceUri";
    internal const string IntakeQueueServiceUriKey = "IntakeQueue:ServiceUri";
    internal const string DocumentIntelligenceEndpointKey = "DocumentIntelligence:Endpoint";

    private const string DevelopmentStorageKey = "AzureWebJobsStorage";
    private const string IntakeStorageConnectionStringKey = "IntakeStorage:ConnectionString";
    private const string IntakeArtifactContainerName = "transient-intake";
    private const string IntakeWorkQueueName = "intake-work";
    private const string AzuriteAccountName = "devstoreaccount1";

    private static readonly string[] ProductionOnlyKeys =
    [
        WorkerClientIdKey,
        IntakeStorageServiceUriKey,
        IntakeQueueServiceUriKey,
        DocumentIntelligenceEndpointKey
    ];

    internal static WorkerAzureClientRegistration CreateProduction(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        EnsureProductionStorageConnectionStringsAreAbsent(configuration);

        var productionOptions = new WorkerAzureProductionOptions(
            ParseWorkerClientId(configuration),
            ParseProductionServiceUri(configuration, IntakeStorageServiceUriKey),
            ParseProductionServiceUri(configuration, IntakeQueueServiceUriKey));
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = productionOptions.WorkerClientId.ToString("D"),
            ExcludeEnvironmentCredential = true,
            ExcludeWorkloadIdentityCredential = true,
            ExcludeManagedIdentityCredential = false,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeAzureDeveloperCliCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            ExcludeBrokerCredential = true
        };
        TokenCredential credential = new DefaultAzureCredential(credentialOptions);

        var intakeArtifactContainer = new BlobServiceClient(
                productionOptions.IntakeStorageServiceUri,
                credential)
            .GetBlobContainerClient(IntakeArtifactContainerName);
        var queueClients = new WorkerQueueClients(
            new QueueServiceClient(productionOptions.IntakeQueueServiceUri, credential)
                .GetQueueClient(IntakeWorkQueueName));

        return new(
            queueClients,
            new WorkerStorageProvisioning(allowLocalCreateIfNotExists: false),
            intakeArtifactContainer,
            productionOptions,
            credentialOptions,
            credential);
    }

    internal static WorkerAzureClientRegistration CreateDevelopmentOffline(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        EnsureProductionSettingsAreAbsent(configuration);

        var queueConnectionString = configuration[DevelopmentStorageKey]
            ?? configuration.GetConnectionString(DevelopmentStorageKey)
            ?? throw new InvalidOperationException(
                $"{DevelopmentStorageKey} is required for DevelopmentOffline queue transport.");
        EnsureAzuriteConnectionString(queueConnectionString, DevelopmentStorageKey);

        var intakeStorageConnectionString = configuration[IntakeStorageConnectionStringKey]
            ?? configuration.GetConnectionString("IntakeStorage");
        if (!string.IsNullOrWhiteSpace(intakeStorageConnectionString))
        {
            EnsureAzuriteConnectionString(
                intakeStorageConnectionString,
                IntakeStorageConnectionStringKey);
        }

        return new(
            new WorkerQueueClients(
                new QueueClient(queueConnectionString, IntakeWorkQueueName)),
            new WorkerStorageProvisioning(allowLocalCreateIfNotExists: true));
    }

    private static Guid ParseWorkerClientId(IConfiguration configuration)
    {
        var configuredClientId = configuration[WorkerClientIdKey];
        if (!Guid.TryParseExact(configuredClientId, "D", out var clientId)
            || clientId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{WorkerClientIdKey} must be the exact non-empty GUID client ID of the Worker user-assigned managed identity in Production.");
        }

        return clientId;
    }

    private static Uri ParseProductionServiceUri(
        IConfiguration configuration,
        string key)
    {
        var configuredUri = configuration[key];
        if (!Uri.TryCreate(configuredUri, UriKind.Absolute, out var serviceUri)
            || !serviceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(serviceUri.Host)
            || !string.IsNullOrEmpty(serviceUri.UserInfo)
            || !string.IsNullOrEmpty(serviceUri.Query)
            || !string.IsNullOrEmpty(serviceUri.Fragment)
            || serviceUri.AbsolutePath != "/")
        {
            throw new InvalidOperationException(
                $"{key} must be an absolute HTTPS Azure Storage service URI in Production.");
        }

        return serviceUri;
    }

    private static void EnsureProductionStorageConnectionStringsAreAbsent(
        IConfiguration configuration)
    {
        foreach (var key in new[]
                 {
                     DevelopmentStorageKey,
                     $"ConnectionStrings:{DevelopmentStorageKey}",
                     IntakeStorageConnectionStringKey,
                     "ConnectionStrings:IntakeStorage"
                 })
        {
            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                throw new InvalidOperationException(
                    $"Storage connection strings are not permitted for Runtime:Profile Production ({key}).");
            }
        }
    }

    private static void EnsureProductionSettingsAreAbsent(IConfiguration configuration)
    {
        foreach (var key in ProductionOnlyKeys)
        {
            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                throw new InvalidOperationException(
                    $"{key} is a Production-only setting and is not permitted for Runtime:Profile DevelopmentOffline.");
            }
        }
    }

    private static void EnsureAzuriteConnectionString(string connectionString, string key)
    {
        var trimmedConnectionString = connectionString.Trim();
        if (trimmedConnectionString.Equals(
                "UseDevelopmentStorage=true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in trimmedConnectionString.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0
                || !settings.TryAdd(segment[..separator].Trim(), segment[(separator + 1)..].Trim()))
            {
                throw InvalidAzuriteConnectionString(key);
            }
        }

        if (!settings.TryGetValue("AccountName", out var accountName)
            || !accountName.Equals(AzuriteAccountName, StringComparison.Ordinal)
            || !settings.TryGetValue("AccountKey", out var accountKey)
            || string.IsNullOrWhiteSpace(accountKey)
            || !IsLoopbackStorageEndpoint(settings, "BlobEndpoint")
            || !IsLoopbackStorageEndpoint(settings, "QueueEndpoint"))
        {
            throw InvalidAzuriteConnectionString(key);
        }
    }

    private static bool IsLoopbackStorageEndpoint(
        Dictionary<string, string> settings,
        string endpointKey)
    {
        return settings.TryGetValue(endpointKey, out var endpoint)
            && Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            && endpointUri.IsLoopback
            && (endpointUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || endpointUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    private static InvalidOperationException InvalidAzuriteConnectionString(string key) =>
        new($"{key} must target the loopback Azurite development account in Runtime:Profile DevelopmentOffline.");
}
