using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Transport;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class WorkerAzureClientCompositionTests
{
    private const string WorkerClientId = "10213243-5465-7687-98a9-bacbdcedfe0f";
    private const string StorageServiceUri = "https://storage.example.test/";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{10213243-5465-7687-98a9-bacbdcedfe0f}")]
    public void ProductionRejectsMissingOrMalformedWorkerClientIdBeforeRegistration(
        string? configuredClientId)
    {
        var values = CreateProductionValues();
        values[WorkerAzureClientFactory.WorkerClientIdKey] = configuredClientId;
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPegasusWorker(
                CreateConfiguration(values),
                new TestHostEnvironment()));

        Assert.Contains(
            WorkerAzureClientFactory.WorkerClientIdKey,
            exception.Message,
            StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void ProductionDoesNotAcceptAWorkerIdentityFromAnotherConfigurationKey()
    {
        var values = CreateProductionValues();
        values.Remove(WorkerAzureClientFactory.WorkerClientIdKey);
        values["AZURE_CLIENT_ID"] = WorkerClientId;
        values["AzureIdentity:WebClientId"] = WorkerClientId;
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPegasusWorker(
                CreateConfiguration(values),
                new TestHostEnvironment()));

        Assert.Contains(
            WorkerAzureClientFactory.WorkerClientIdKey,
            exception.Message,
            StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Theory]
    [InlineData(WorkerAzureClientFactory.IntakeStorageServiceUriKey, null)]
    [InlineData(WorkerAzureClientFactory.IntakeStorageServiceUriKey, "intake-staging")]
    [InlineData(WorkerAzureClientFactory.IntakeStorageServiceUriKey, "http://storage.example.test/")]
    [InlineData(WorkerAzureClientFactory.IntakeStorageServiceUriKey, "https://storage.example.test/intake-staging")]
    [InlineData(WorkerAzureClientFactory.IntakeQueueServiceUriKey, null)]
    [InlineData(WorkerAzureClientFactory.IntakeQueueServiceUriKey, "intake-work")]
    public void ProductionRejectsMissingOrInvalidStorageServiceUrisBeforeRegistration(
        string key,
        string? configuredUri)
    {
        var values = CreateProductionValues();
        values[key] = configuredUri;
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPegasusWorker(
                CreateConfiguration(values),
                new TestHostEnvironment()));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void ProductionRejectsStorageConnectionStringsInsteadOfFallingBack()
    {
        var values = CreateProductionValues();
        values["AzureWebJobsStorage"] = "UseDevelopmentStorage=true";
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPegasusWorker(
                CreateConfiguration(values),
                new TestHostEnvironment()));

        Assert.Contains("Storage connection strings", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void DevelopmentOfflineRejectsProductionIdentityConfiguration()
    {
        var values = CreateDevelopmentOfflineValues();
        values[WorkerAzureClientFactory.WorkerClientIdKey] = WorkerClientId;
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPegasusWorker(
                CreateConfiguration(values),
                new TestHostEnvironment()));

        Assert.Contains("Production-only", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void DevelopmentOfflineRejectsNonAzuriteQueueTransport()
    {
        var values = CreateDevelopmentOfflineValues();
        values["AzureWebJobsStorage"] =
            "DefaultEndpointsProtocol=https;AccountName=production;AccountKey=not-a-secret";
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPegasusWorker(
                CreateConfiguration(values),
                new TestHostEnvironment()));

        Assert.Contains("Azurite", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void ProductionTargetsOneWorkerManagedIdentityForEveryStorageClient()
    {
        using var provider = CreateProvider(CreateProductionValues());

        var credentialOptions = provider.GetRequiredService<DefaultAzureCredentialOptions>();
        Assert.Equal(WorkerClientId, credentialOptions.ManagedIdentityClientId);
        Assert.True(credentialOptions.ExcludeEnvironmentCredential);
        Assert.True(credentialOptions.ExcludeWorkloadIdentityCredential);
        Assert.False(credentialOptions.ExcludeManagedIdentityCredential);
        Assert.True(credentialOptions.ExcludeVisualStudioCredential);
        Assert.True(credentialOptions.ExcludeVisualStudioCodeCredential);
        Assert.True(credentialOptions.ExcludeAzureCliCredential);
        Assert.True(credentialOptions.ExcludeAzurePowerShellCredential);
        Assert.True(credentialOptions.ExcludeAzureDeveloperCliCredential);
        Assert.True(credentialOptions.ExcludeInteractiveBrowserCredential);
        Assert.True(credentialOptions.ExcludeBrokerCredential);

        var credential = Assert.Single(provider.GetServices<TokenCredential>());
        Assert.IsType<DefaultAzureCredential>(credential);

        var productionOptions = provider.GetRequiredService<WorkerAzureProductionOptions>();
        Assert.Equal(Guid.Parse(WorkerClientId), productionOptions.WorkerClientId);
        Assert.Equal(new Uri(StorageServiceUri), productionOptions.IntakeStorageServiceUri);
        Assert.Equal(new Uri(StorageServiceUri), productionOptions.IntakeQueueServiceUri);

        var queueClients = provider.GetRequiredService<WorkerQueueClients>();
        Assert.Equal(new Uri($"{StorageServiceUri}intake-work"), queueClients.WorkQueue.Uri);
        Assert.Equal(
            new Uri($"{StorageServiceUri}transient-intake"),
            provider.GetRequiredService<BlobContainerClient>().Uri);
        Assert.False(
            provider.GetRequiredService<WorkerStorageProvisioning>()
                .AllowLocalCreateIfNotExists);
    }

    [Fact]
    public void DevelopmentOfflineUsesAzuriteQueuesWithoutConstructingACloudCredential()
    {
        using var provider = CreateProvider(CreateDevelopmentOfflineValues());

        Assert.Empty(provider.GetServices<TokenCredential>());
        Assert.Null(provider.GetService<DefaultAzureCredentialOptions>());
        Assert.Null(provider.GetService<WorkerAzureProductionOptions>());
        Assert.Null(provider.GetService<BlobContainerClient>());

        var queueClients = provider.GetRequiredService<WorkerQueueClients>();
        Assert.True(queueClients.WorkQueue.Uri.IsLoopback);
        Assert.EndsWith(
            "/devstoreaccount1/intake-work",
            queueClients.WorkQueue.Uri.AbsolutePath,
            StringComparison.Ordinal);
        Assert.True(
            provider.GetRequiredService<WorkerStorageProvisioning>()
                .AllowLocalCreateIfNotExists);
        Assert.Equal(
            "Pegasus.Infrastructure.Intake.FileSystemIntakeArtifactStore",
            provider.GetRequiredService<IIntakeArtifactStore>().GetType().FullName);
    }

    [Fact]
    public async Task ProductionProvisioningNeverIssuesResourceCreationRequests()
    {
        using var handler = new RecordingStorageHandler();
        var clients = CreateRecordingClients(handler);
        var provisioning = new WorkerStorageProvisioning(allowLocalCreateIfNotExists: false);

        await provisioning.EnsureQueueExistsAsync(clients.IntakeQueue, CancellationToken.None);
        await provisioning.EnsureContainerExistsAsync(clients.Container, CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DevelopmentOfflineProvisioningCreatesTheUnifiedQueueAndBlobContainer()
    {
        using var handler = new RecordingStorageHandler();
        var clients = CreateRecordingClients(handler);
        var provisioning = new WorkerStorageProvisioning(allowLocalCreateIfNotExists: true);

        await provisioning.EnsureQueueExistsAsync(clients.IntakeQueue, CancellationToken.None);
        await provisioning.EnsureContainerExistsAsync(clients.Container, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Put, request.Method));
        Assert.Equal(
            ["/intake-work", "/intake-staging"],
            handler.Requests.Select(request => request.Uri.AbsolutePath));
    }

    [Fact]
    public void AzureStorageAdaptersRequirePrecomposedClientsAndExplicitProvisioningPolicy()
    {
        Assert.Equal(
            [typeof(QueueClient), typeof(bool)],
            TypeInspection.OnlyConstructorParameterTypes(typeof(AzureQueueIntakeWorkEnqueuer)));
        Assert.Equal(
            [typeof(QueueClient), typeof(bool)],
            TypeInspection.OnlyConstructorParameterTypes(typeof(AzureQueueExternalWorkEnqueuer)));
        Assert.Equal(
            [typeof(BlobContainerClient), typeof(bool)],
            TypeInspection.OnlyConstructorParameterTypes(typeof(AzureBlobIntakeArtifactStore)));
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> values)
    {
        var configuration = CreateConfiguration(values);
        var environment = new TestHostEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddPegasusWorker(configuration, environment);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static Dictionary<string, string?> CreateProductionValues() =>
        new(StringComparer.Ordinal)
        {
            ["Runtime:Profile"] = "Production",
            ["ConnectionStrings:Pegasus"] =
                "Server=tcp:sql.example.test,1433;Database=pegasus;Encrypt=true",
            [WorkerAzureClientFactory.WorkerClientIdKey] = WorkerClientId,
            [WorkerAzureClientFactory.IntakeStorageServiceUriKey] = StorageServiceUri,
            [WorkerAzureClientFactory.IntakeQueueServiceUriKey] = StorageServiceUri,
            ["Graph:BaseUri"] = "https://graph.microsoft.com/v1.0/",
            ["Graph:MailboxId"] = "mailbox-id",
            ["Graph:MailboxAddress"] = "instructions@example.test",
            ["Graph:InboxFolderId"] = "inbox-id",
            ["Graph:SentFolderId"] = "sent-id",
            ["Box:BaseUri"] = "https://api.box.com/2.0/",
            ["Box:UploadUri"] = "https://upload.box.com/api/2.0/",
            ["Box:RootFolderId"] = "405543781910",
            ["Box:HoldingFolderId"] = "test-holding-folder",
            ["Box:ConfigJson"] = "{\"boxAppSettings\":{\"clientID\":\"client-id\",\"appAuth\":{\"publicKeyID\":\"key-id\",\"privateKey\":\"private-key\",\"passphrase\":\"passphrase\"}},\"enterpriseID\":\"enterprise-id\"}",
            ["Box:ClientSecret"] = "test-client-secret",
            ["Dvla:BaseUri"] = "https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/vehicles/",
            ["Dvla:ApiKey"] = "test-dvla-key",
            ["Dvsa:BaseUri"] = "https://history.mot.api.gov.uk/v1/trade/vehicles/registration/",
            ["Dvsa:TokenUri"] = "https://login.microsoftonline.com/test/oauth2/v2.0/token",
            ["Dvsa:ClientId"] = "test-dvsa-client",
            ["Dvsa:ClientSecret"] = "test-dvsa-secret",
            ["Dvsa:ApiKey"] = "test-dvsa-key",
            ["Dvsa:Scope"] = "https://tapi.dvsa.gov.uk/.default"
        };

    private static Dictionary<string, string?> CreateDevelopmentOfflineValues() =>
        new(StringComparer.Ordinal)
        {
            ["Runtime:Profile"] = "DevelopmentOffline",
            ["ConnectionStrings:Pegasus"] =
                "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_WorkerAzureComposition;" +
                "Integrated Security=true;Encrypt=false",
            ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
            ["Intake:LocalArtifactPath"] = "intake",
            ["ApprovedInbox:MailboxId"] = "instructions",
            ["ApprovedInbox:MailboxAddress"] = "instructions@example.test",
            ["ApprovedInbox:LocalRootPath"] = "approved-inbox",
            ["ApprovedSent:MailboxId"] = "instructions",
            ["ApprovedSent:MailboxAddress"] = "instructions@example.test",
            ["ApprovedSent:SentFolderIdentity"] = "sent-items",
            ["ApprovedSent:LocalRootPath"] = "approved-sent"
        };

    private static RecordingClients CreateRecordingClients(HttpMessageHandler handler)
    {
        var transport = new HttpClientTransport(handler);
        var credential = new StaticTokenCredential();
        var queueOptions = new QueueClientOptions
        {
            Transport = transport,
            Retry = { MaxRetries = 0 }
        };
        var blobOptions = new BlobClientOptions
        {
            Transport = transport,
            Retry = { MaxRetries = 0 }
        };

        return new(
            new QueueClient(
                new Uri("https://storage.example.test/intake-work"),
                credential,
                queueOptions),
            new BlobContainerClient(
                new Uri("https://storage.example.test/intake-staging"),
                credential,
                blobOptions));
    }

    private sealed record RecordingClients(
        QueueClient IntakeQueue,
        BlobContainerClient Container);

    private sealed class StaticTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("test-token", DateTimeOffset.MaxValue);

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => Token;

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => ValueTask.FromResult(Token);
    }

    private sealed class RecordingStorageHandler : HttpMessageHandler
    {
        internal List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method, request.RequestUri!));
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([])
            };
            response.Headers.Date = DateTimeOffset.UtcNow;
            response.Headers.ETag = new EntityTagHeaderValue("\"test-etag\"");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", Guid.NewGuid().ToString("D"));
            response.Headers.TryAddWithoutValidation("x-ms-version", "2025-11-05");
            response.Content.Headers.LastModified = DateTimeOffset.UtcNow;
            return Task.FromResult(response);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pegasus.ArchitectureTests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
