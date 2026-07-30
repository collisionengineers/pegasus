using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Worker;
using Pegasus.Worker.Functions;

namespace Pegasus.ArchitectureTests;

public sealed class WorkerCompositionTests
{
    [Fact]
    public async Task ProductionCompositionUsesExplicitUnavailableExternalAdapters()
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
            var custody = provider.GetRequiredService<ICaseCustody>();
            var vehicleLookup = provider.GetRequiredService<IVehicleLookupAdapter>();

            Assert.Equal("Pegasus.Worker.AzureBlobIntakeArtifactStore", artifactStore.GetType().FullName);
            Assert.Equal(
                "Pegasus.Infrastructure.Custody.UnavailableCaseCustody",
                custody.GetType().FullName);
            await Assert.ThrowsAsync<CaseCustodyUnavailableException>(() =>
                custody.CreateCaseRootAsync(
                    Guid.NewGuid(),
                    "QDOS31001",
                    "production-custody-denial",
                    CancellationToken.None));
            Assert.Contains(
                "UnavailableVehicleLookupAdapter",
                vehicleLookup.GetType().FullName,
                StringComparison.Ordinal);
            await Assert.ThrowsAsync<VehicleLookupUnavailableException>(() =>
                vehicleLookup.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None));
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedCustody>());
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedVehicleLookup>());
            Assert.NotNull(scopedServices.GetRequiredService<IProcessQueuedExternalWork>());
            Assert.NotNull(scopedServices.GetRequiredService<DispatchPendingWork>());
            Assert.Null(provider.GetService<IApprovedInboxSource>());
            Assert.Null(provider.GetService<LocalApprovedInboxOptions>());
            Assert.Null(provider.GetService<IApprovedSentSource>());
            Assert.Null(provider.GetService<LocalApprovedSentOptions>());
            Assert.Null(provider.GetService<PollSentEvidence>());
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
            Assert.NotNull(scopedServices.GetRequiredService<PollSentEvidence>());
            Assert.NotNull(scopedServices.GetRequiredService<RunDueChasers>());
            Assert.Same(
                scopedServices.GetRequiredService<IIntakeWorkStore>(),
                scopedServices.GetRequiredService<IStagedArtifactAuthority>());
            Assert.NotNull(scopedServices.GetRequiredService<ReconcileStagedArtifacts>());

            Assert.NotNull(ActivatorUtilities.CreateInstance<PendingWorkDispatchFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<IntakeWorkFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<IntakePoisonFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<StagedArtifactReconciliationFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<InboxPollFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<SentEvidencePollFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<DueWorkSweepFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<ExternalWorkFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<ExternalPoisonFunction>(scopedServices));
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
        var values = new Dictionary<string, string?>
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
        if (profile.Equals("Production", StringComparison.Ordinal))
        {
            values["AzureIdentity:WorkerClientId"] = "10213243-5465-7687-98a9-bacbdcedfe0f";
            values["IntakeStorage:ServiceUri"] = "https://storage.example.test/";
            values["IntakeQueue:ServiceUri"] = "https://storage.example.test/";
            values["ExternalWorkQueue:ServiceUri"] = "https://storage.example.test/";
        }
        else if (profile.Equals("DevelopmentOffline", StringComparison.Ordinal))
        {
            values["AzureWebJobsStorage"] = "UseDevelopmentStorage=true";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

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
}
