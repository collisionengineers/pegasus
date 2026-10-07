using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class WorkerCompositionTests
{
    [Fact]
    public void ProductionCompositionUsesApprovedExternalAdapters()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var configuration = CreateConfiguration("Production", root);
            var environment = new TestHostEnvironment(root);
            var services = CreateWorkerServices(configuration, environment);

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var artifactStore = provider.GetRequiredService<IIntakeArtifactStore>();
            var quarantineStore = provider.GetRequiredService<IIntakeQuarantineArtifactStore>();
            var custody = provider.GetRequiredService<ICaseCustody>();
            var vehicleLookup = provider.GetRequiredService<IVehicleLookupAdapter>();

            Assert.IsType<AzureBlobIntakeArtifactStore>(artifactStore);
            Assert.Same(artifactStore, quarantineStore);
            Assert.Equal("Pegasus.Infrastructure.Custody.BoxCaseCustody", custody.GetType().FullName);
            Assert.Equal(
                "Pegasus.Infrastructure.Vehicle.DvlaDvsaProductionAdapter",
                vehicleLookup.GetType().FullName);
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedCustody>());
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedVehicleLookup>());
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedExternalWork>());
            Assert.IsType<AzureDocumentIntelligenceOcr>(
                scopedServices.GetRequiredService<IIntakeOcrProvider>());
            Assert.NotNull(scopedServices.GetRequiredService<IProcessIntakeOcr>());
            Assert.Same(
                scopedServices.GetRequiredService<EfIntakeOcrOperationStore>(),
                scopedServices.GetRequiredService<IIntakeOcrOperationStore>());
            Assert.NotNull(scopedServices.GetRequiredService<DispatchPendingWork>());
            Assert.Equal(
                "Pegasus.Infrastructure.Email.GraphApprovedInboxSource",
                provider.GetRequiredService<IApprovedInboxSource>().GetType().FullName);
            Assert.Null(provider.GetService<LocalApprovedInboxOptions>());
            Assert.Equal(
                "Pegasus.Infrastructure.Email.GraphApprovedSentSource",
                provider.GetRequiredService<IApprovedSentSource>().GetType().FullName);
            Assert.Null(provider.GetService<LocalApprovedSentOptions>());
            Assert.NotNull(scopedServices.GetRequiredService<PollSentEvidence>());
            Assert.NotNull(scopedServices.GetRequiredService<IGroupedIntakeSubmission>());
            Assert.NotNull(scopedServices.GetRequiredService<RetainIncomingArtifact>());
            Assert.Equal(
                "Pegasus.Infrastructure.Persistence.EfIncomingArtifactRetentionStore",
                scopedServices.GetRequiredService<IIncomingArtifactRetentionStore>().GetType().FullName);
            Assert.NotNull(scopedServices.GetRequiredService<ProcessQueuedIntake>());
            Assert.NotNull(scopedServices.GetRequiredService<VehicleRegistrationCandidateLookup>());
            var analysis = scopedServices.GetRequiredService<AnalyzeRetainedInstruction>();
            Assert.Same(analysis, scopedServices.GetRequiredService<IAnalyzeRetainedInstruction>());
            // The sweep settles a report whose file custody filed after its request ended.
            Assert.IsType<EfSettleFiledCaseReportArtifacts>(
                scopedServices.GetRequiredService<ISettleFiledCaseReportArtifacts>());
            // The sweep makes plain thumbnails from the cached production store.
            Assert.NotNull(scopedServices.GetRequiredService<Pegasus.Core.Documents.PrepareDocumentThumbnails>());
            Assert.Equal(
                "Pegasus.Infrastructure.Custody.EfDocumentThumbnailCandidates",
                provider.GetRequiredService<Pegasus.Core.Documents.IListDocumentThumbnailCandidates>().GetType().FullName);

            WorkerFunctionSet.AssertEveryFunctionActivates(scopedServices);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProductionWithoutDocumentIntelligenceEndpointKeepsOcrUnavailable()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateProductionValues(root);
            values.Remove("DocumentIntelligence:Endpoint");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = CreateWorkerServices(configuration, new TestHostEnvironment(root));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProcessQueuedExternalWork>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIntakeOcrOperationStore>());
            Assert.Null(scope.ServiceProvider.GetService<IIntakeOcrProvider>());
            Assert.Null(scope.ServiceProvider.GetService<IProcessIntakeOcr>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguredProductionRouterDispatchesOcrToItsTypedHandler()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var workItemId = Guid.NewGuid();
            var configuration = CreateConfiguration("Production", root);
            var services = CreateWorkerServices(configuration, new TestHostEnvironment(root));
            var recorder = new RecordingOcrProcessor();
            services.AddSingleton<IQueuedExternalWorkReader>(
                new FixedExternalWorkReader(new(workItemId, ExternalWorkKinds.IntakeOcr)));
            services.AddSingleton<IProcessIntakeOcr>(recorder);

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IProcessQueuedExternalWork>()
                .ExecuteAsync(workItemId, CancellationToken.None);

            Assert.Equal([workItemId], recorder.ProcessedIds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UnconfiguredProductionRouterRefusesOcrWork()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var workItemId = Guid.NewGuid();
            var values = CreateProductionValues(root);
            values.Remove("DocumentIntelligence:Endpoint");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = CreateWorkerServices(configuration, new TestHostEnvironment(root));
            services.AddSingleton<IQueuedExternalWorkReader>(
                new FixedExternalWorkReader(new(workItemId, ExternalWorkKinds.IntakeOcr)));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            var exception = await Assert.ThrowsAsync<UnknownExternalWorkKindException>(() =>
                scope.ServiceProvider.GetRequiredService<IProcessQueuedExternalWork>()
                    .ExecuteAsync(workItemId, CancellationToken.None));

            Assert.Equal(workItemId, exception.WorkItemId);
            Assert.Equal(ExternalWorkKinds.IntakeOcr, exception.Kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("not-an-endpoint")]
    [InlineData("   ")]
    [InlineData("http://ocr.example.test/")]
    public void ProductionRefusesInvalidDocumentIntelligenceEndpointBeforeRegistration(
        string endpoint)
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateProductionValues(root);
            values["DocumentIntelligence:Endpoint"] = endpoint;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddPegasusWorker(configuration, new TestHostEnvironment(root)));

            Assert.Contains("DocumentIntelligence:Endpoint", exception.Message, StringComparison.Ordinal);
            Assert.Empty(services);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DevelopmentOfflineRefusesDocumentIntelligenceEndpoint()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "DevelopmentOffline",
                ["DocumentIntelligence:Endpoint"] = "https://ocr.example.test/",
                ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddPegasusWorker(configuration, new TestHostEnvironment(root)));

            Assert.Contains("DocumentIntelligence:Endpoint", exception.Message, StringComparison.Ordinal);
            Assert.Empty(services);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProductionCompositionFailsBeforeRegistrationWhenGraphEndpointIsMissing()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateProductionValues(root);
            values.Remove("Graph:BaseUri");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddPegasusWorker(configuration, new TestHostEnvironment(root)));

            Assert.Contains("Graph:BaseUri", exception.Message, StringComparison.Ordinal);
            Assert.Empty(services);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DevelopmentOfflineCompositionActivatesLocalAdapterFunctions()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var configuration = CreateConfiguration("DevelopmentOffline", root);
            var environment = new TestHostEnvironment(root);
            var services = CreateWorkerServices(configuration, environment);

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            Assert.Equal(
                "Pegasus.Infrastructure.Intake.FileSystemIntakeArtifactStore",
                provider.GetRequiredService<IIntakeArtifactStore>().GetType().FullName);
            Assert.Equal(
                "Pegasus.Infrastructure.Custody.LocalCaseCustody",
                provider.GetRequiredService<ICaseCustody>().GetType().FullName);
            Assert.Equal(
                "Pegasus.Infrastructure.Intake.LocalDurableApprovedInboxSource",
                provider.GetRequiredService<IApprovedInboxSource>().GetType().FullName);
            Assert.Equal(
                "Pegasus.Infrastructure.Email.LocalDurableApprovedSentSource",
                provider.GetRequiredService<IApprovedSentSource>().GetType().FullName);
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedCustody>());
            Assert.Equal(
                "Pegasus.Infrastructure.Vehicle.DvlaDvsaReplayAdapter",
                provider.GetRequiredService<IVehicleLookupAdapter>().GetType().FullName);
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedVehicleLookup>());
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedExternalWork>());
            Assert.NotNull(scopedServices.GetRequiredService<IIntakeOcrOperationStore>());
            Assert.Null(scopedServices.GetService<IIntakeOcrProvider>());
            Assert.Null(scopedServices.GetService<IProcessIntakeOcr>());
            Assert.NotNull(scopedServices.GetRequiredService<PollSentEvidence>());
            Assert.NotNull(scopedServices.GetRequiredService<RunDueChasers>());
            Assert.NotNull(scopedServices.GetRequiredService<VehicleRegistrationCandidateLookup>());
            var analysis = scopedServices.GetRequiredService<AnalyzeRetainedInstruction>();
            Assert.Same(analysis, scopedServices.GetRequiredService<IAnalyzeRetainedInstruction>());
            Assert.Same(
                scopedServices.GetRequiredService<IIntakeWorkStore>(),
                scopedServices.GetRequiredService<IStagedArtifactAuthority>());
            Assert.NotNull(scopedServices.GetRequiredService<ReconcileStagedArtifacts>());
            Assert.IsType<EfSettleFiledCaseReportArtifacts>(
                scopedServices.GetRequiredService<ISettleFiledCaseReportArtifacts>());
            // No thumbnail cache here, so the sweep has nothing to make.
            Assert.IsType<Pegasus.Infrastructure.Custody.NoDocumentThumbnailCandidates>(
                provider.GetRequiredService<Pegasus.Core.Documents.IListDocumentThumbnailCandidates>());
            // The offline Worker composes no EVA route; its timer still activates
            // and replays nothing rather than failing on absent credentials.
            Assert.Null(scopedServices.GetService<Pegasus.Core.Eva.ISubmitCaseToEva>());
            Assert.Null(scopedServices.GetService<ProcessAutomaticEvaReviewSubmissions>());

            WorkerFunctionSet.AssertEveryFunctionActivates(scopedServices);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The Functions host validates the whole graph at build in Development, so
    /// every shared use case has to be constructible even where this host never
    /// calls it. The paged queries take a cursor protector; the Worker serves no
    /// page, and composes the one that says so rather than leaving them
    /// unconstructible (the local Worker process exited 134 on exactly this).
    /// </summary>
    [Theory]
    [InlineData("DevelopmentOffline")]
    [InlineData("Production")]
    public void TheWorkerComposesAnUnavailableCursorProtectorSoThePagedQueriesConstruct(string profile)
    {
        var root = CreateTemporaryRoot();
        try
        {
            var services = CreateWorkerServices(CreateConfiguration(profile, root), new TestHostEnvironment(root));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();

            Assert.IsType<Pegasus.Infrastructure.Support.UnavailableCursorProtector>(
                provider.GetRequiredService<Pegasus.Core.ICursorProtector>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<Pegasus.Core.Cases.IListCaseDocumentsByCursor>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<Pegasus.Core.Cases.IListCaseHistoryByCursor>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<Pegasus.Core.Assessment.IListCaseEstimatesByCursor>());
            Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<Pegasus.Core.ICursorProtector>().Protect("scope", "key", Guid.NewGuid()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Features:LiveVehicleLookup composes the live DVLA/DVSA adapter in the
    /// offline Worker and nothing of Graph: the mailbox stays the local folder.
    /// </summary>
    [Fact]
    public void DevelopmentOfflineWithLiveVehicleLookupComposesTheLiveAdapterWithoutGraph()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateDevelopmentOfflineValues(root);
            values["Features:LiveVehicleLookup"] = "true";
            AddLiveVehicleValues(values);
            var services = CreateWorkerServices(Build(values), new TestHostEnvironment(root));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();

            Assert.Equal(
                "Pegasus.Infrastructure.Vehicle.DvlaDvsaProductionAdapter",
                provider.GetRequiredService<IVehicleLookupAdapter>().GetType().FullName);
            Assert.Same(
                VehicleLookupAvailability.ProductionLive,
                provider.GetRequiredService<VehicleLookupAvailability>());
            Assert.Equal(
                "Pegasus.Infrastructure.Intake.LocalDurableApprovedInboxSource",
                provider.GetRequiredService<IApprovedInboxSource>().GetType().FullName);
            Assert.Null(provider.GetService<Pegasus.Infrastructure.Email.GraphApprovedMailboxOptions>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProcessQueuedVehicleLookup>());
            WorkerFunctionSet.AssertEveryFunctionActivates(scope.ServiceProvider);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A missing DVLA/DVSA key fails the live opt-in at build, naming the key.</summary>
    [Fact]
    public void DevelopmentOfflineWithLiveVehicleLookupFailsAtBuildWhenAKeyIsMissing()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateDevelopmentOfflineValues(root);
            values["Features:LiveVehicleLookup"] = "true";
            AddLiveVehicleValues(values);
            values["Dvsa:ClientSecret"] = null;

            var error = Assert.Throws<InvalidOperationException>(() =>
                CreateWorkerServices(Build(values), new TestHostEnvironment(root)));

            Assert.Contains("Dvsa:ClientSecret", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Features:LiveBoxCustody composes the production storage shape over the
    /// run's Azurite container with the development Box root, not the local
    /// artifact folder: Web and Worker then share one storage truth.
    /// </summary>
    [Fact]
    public void DevelopmentOfflineWithLiveBoxCustodyComposesTheProductionStorageShape()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateDevelopmentOfflineValues(root);
            values["Features:LiveBoxCustody"] = "true";
            AddLiveBoxValues(values, "425169015650");
            var services = CreateWorkerServices(Build(values), new TestHostEnvironment(root));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();

            Assert.IsType<AzureBlobIntakeArtifactStore>(provider.GetRequiredService<IIntakeArtifactStore>());
            Assert.Equal(
                "Pegasus.Infrastructure.Custody.BoxCaseCustody",
                provider.GetRequiredService<ICaseCustody>().GetType().FullName);
            var container = provider.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>();
            Assert.Equal("transient-intake", container.Name);
            Assert.Equal("devstoreaccount1", container.AccountName);
            Assert.Equal(
                "Pegasus.Infrastructure.Custody.EfDocumentThumbnailCandidates",
                provider.GetRequiredService<Pegasus.Core.Documents.IListDocumentThumbnailCandidates>().GetType().FullName);
            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Name == "BoxTokenRenewalService");
            Assert.Null(provider.GetService<FileSystemIntakeArtifactStore>());
            WorkerFunctionSet.AssertEveryFunctionActivates(scope.ServiceProvider);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The offline opt-in is fenced to the development root; the production root is refused at first Box use.</summary>
    [Fact]
    public void DevelopmentOfflineWithLiveBoxCustodyRefusesTheProductionRoot()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateDevelopmentOfflineValues(root);
            values["Features:LiveBoxCustody"] = "true";
            AddLiveBoxValues(values, "405543781910");
            var services = CreateWorkerServices(Build(values), new TestHostEnvironment(root));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ICaseCustody>());

            Assert.Contains("425169015650", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Features:LiveVehicleLookup")]
    [InlineData("Features:LiveBoxCustody")]
    public void ProductionRefusesTheLocalLiveOptIns(string key)
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateProductionValues(root);
            values[key] = "true";

            var error = Assert.Throws<InvalidOperationException>(() =>
                CreateWorkerServices(Build(values), new TestHostEnvironment(root)));

            Assert.Equal($"{key} requires the DevelopmentOffline runtime profile.", error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The Worker holds the Box token provider in production, so it renews the
    /// token in the background there. The offline profile has no Box token.
    /// </summary>
    [Theory]
    [InlineData("Production", true)]
    [InlineData("DevelopmentOffline", false)]
    public void TheBoxTokenIsRenewedInTheBackgroundInProductionOnly(string profile, bool composed)
    {
        var root = CreateTemporaryRoot();
        try
        {
            var services = CreateWorkerServices(
                CreateConfiguration(profile, root), new TestHostEnvironment(root));

            Assert.Equal(
                composed,
                services.Any(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.FullName
                        == "Pegasus.Infrastructure.Custody.BoxTokenRenewalService"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The Worker files intake evidence to Box, so in production it publishes
    /// each filed file's read-cache copy while it holds the bytes. The offline
    /// profile has no content cache and composes the null publisher.
    /// </summary>
    [Theory]
    [InlineData("Production", false)]
    [InlineData("DevelopmentOffline", true)]
    public void TheWorkerComposesTheCachePublisherInProductionAndTheNullOneOffline(
        string profile,
        bool nullPublisher)
    {
        var root = CreateTemporaryRoot();
        try
        {
            var services = CreateWorkerServices(
                CreateConfiguration(profile, root), new TestHostEnvironment(root));

            // The last registration is the one a scope resolves.
            var composed = services.Last(descriptor =>
                descriptor.ServiceType == typeof(Pegasus.Core.Documents.IDocumentContentCachePublisher));

            if (nullPublisher)
            {
                Assert.Equal(
                    typeof(Pegasus.Infrastructure.Custody.NoDocumentContentCachePublisher),
                    composed.ImplementationType);
            }
            else
            {
                Assert.Equal(ServiceLifetime.Scoped, composed.Lifetime);
                Assert.NotNull(composed.ImplementationFactory);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnsupportedRuntimeProfileFailsBeforeAdaptersAreRegistered()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var configuration = CreateConfiguration("Development", root);
            var environment = new TestHostEnvironment(root);
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(
                () => services.AddPegasusWorker(configuration, environment));

            Assert.Contains("Unsupported Runtime:Profile", exception.Message, StringComparison.Ordinal);
            Assert.Empty(services);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TheSpanBridgeIsComposedOverTheApplicationInsightsClientAsOneSingleton()
    {
        using var telemetry = TelemetryConfiguration.CreateDefault();
        telemetry.DisableTelemetry = true;
        var services = new ServiceCollection();
        services.AddSingleton(new TelemetryClient(telemetry));
        services.AddWorkerSpanTelemetry();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Same(
            provider.GetRequiredService<WorkerSpanTelemetryBridge>(),
            provider.GetRequiredService<WorkerSpanTelemetryBridge>());
    }

    private static ServiceCollection CreateWorkerServices(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton(environment);
        services.AddPegasusWorker(configuration, environment);
        return services;
    }

    private static IConfiguration CreateConfiguration(string profile, string root)
    {
        var values = profile.Equals("Production", StringComparison.Ordinal)
            ? CreateProductionValues(root)
            : new Dictionary<string, string?>
        {
            ["Runtime:Profile"] = profile,
            ["ConnectionStrings:Pegasus"] =
                "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_WorkerComposition;" +
                "Integrated Security=true;Encrypt=false",
            ["Intake:LocalArtifactPath"] = Path.Combine(root, "intake"),
            ["ApprovedInbox:MailboxId"] = "instructions",
            ["ApprovedInbox:MailboxAddress"] = "instructions@example.test",
            ["ApprovedInbox:LocalRootPath"] = Path.Combine(root, "approved-inbox"),
            ["ApprovedSent:MailboxId"] = "instructions",
            ["ApprovedSent:MailboxAddress"] = "instructions@example.test",
            ["ApprovedSent:SentFolderIdentity"] = "sent-items",
            ["ApprovedSent:LocalRootPath"] = Path.Combine(root, "approved-sent")
        };
        if (profile.Equals("DevelopmentOffline", StringComparison.Ordinal))
        {
            values["AzureWebJobsStorage"] = "UseDevelopmentStorage=true";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static Dictionary<string, string?> CreateDevelopmentOfflineValues(string root)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in CreateConfiguration("DevelopmentOffline", root).AsEnumerable())
        {
            values[key] = value;
        }

        return values;
    }

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static void AddLiveVehicleValues(Dictionary<string, string?> values)
    {
        foreach (var (key, value) in CreateProductionValues(string.Empty)
                     .Where(pair => pair.Key.StartsWith("Dvla:", StringComparison.Ordinal)
                         || pair.Key.StartsWith("Dvsa:", StringComparison.Ordinal)))
        {
            values[key] = value;
        }
    }

    private static void AddLiveBoxValues(Dictionary<string, string?> values, string rootFolderId)
    {
        foreach (var (key, value) in CreateProductionValues(string.Empty)
                     .Where(pair => pair.Key.StartsWith("Box:", StringComparison.Ordinal)))
        {
            values[key] = value;
        }

        values["Box:RootFolderId"] = rootFolderId;
    }

    private static Dictionary<string, string?> CreateProductionValues(string root) => new()
    {
        ["Runtime:Profile"] = "Production",
        ["ConnectionStrings:Pegasus"] =
            "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_WorkerComposition;" +
            "Integrated Security=true;Encrypt=false",
        ["AzureIdentity:WorkerClientId"] = "10213243-5465-7687-98a9-bacbdcedfe0f",
        ["IntakeStorage:ServiceUri"] = "https://custody.example.test/",
        ["IntakeQueue:ServiceUri"] = "https://transport.example.test/",
        ["DocumentIntelligence:Endpoint"] = "https://ocr.example.test/",
        ["Graph:BaseUri"] = "https://graph.microsoft.com/v1.0/",
        ["Box:BaseUri"] = "https://api.box.com/2.0/",
        ["Box:UploadUri"] = "https://upload.box.com/api/2.0/",
        ["Box:RootFolderId"] = "405543781910",
        ["Box:HoldingFolderId"] = "test-holding-folder",
        ["Box:ConfigJson"] = "{\"boxAppSettings\":{\"clientID\":\"client-id\",\"appAuth\":{\"publicKeyID\":\"key-id\",\"privateKey\":\"private-key\",\"passphrase\":\"passphrase\"}},\"enterpriseID\":\"enterprise-id\"}",
        ["Box:ClientSecret"] = "resolved-key-vault-reference",
        ["Dvla:BaseUri"] = "https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/",
        ["Dvla:ApiKey"] = "resolved-key-vault-reference",
        ["Dvsa:BaseUri"] = "https://history.mot.api.gov.uk/v1/trade/vehicles/registration/",
        ["Dvsa:TokenUri"] = "https://login.microsoftonline.com/tenant/oauth2/v2.0/token",
        ["Dvsa:ClientId"] = "resolved-key-vault-reference",
        ["Dvsa:ClientSecret"] = "resolved-key-vault-reference",
        ["Dvsa:ApiKey"] = "resolved-key-vault-reference",
        ["Dvsa:Scope"] = "https://tapi.dvsa.gov.uk/.default",
        // EXT-04: production now composes the EVA API submission route,
        // so its configuration is part of what a production Worker needs.
        ["Eva:BaseUri"] = "https://sentry.evasoftware.co.uk/api/",
        ["Eva:ClientId"] = "eva-client",
        ["Eva:ClientSecret"] = "eva-secret",
        ["Eva:RequestFrom"] = "COLLENGAPI",
        ["Eva:InspectionType"] = "Vehicle Damage Inspection",
        ["Eva:InstructionEmail"] = "digital@collisionengineers.co.uk"
    };

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pegasus-worker-composition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pegasus.ArchitectureTests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedExternalWorkReader(QueuedExternalWork work)
        : IQueuedExternalWorkReader
    {
        public Task<QueuedExternalWork?> GetAsync(
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult<QueuedExternalWork?>(work);
    }

    private sealed class RecordingOcrProcessor : IProcessIntakeOcr
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            ProcessedIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }
}
