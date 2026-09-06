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
            Assert.NotNull(scopedServices.GetRequiredService<SubmitMailboxImageIntake>());
            Assert.NotNull(scopedServices.GetRequiredService<ProcessQueuedIntake>());

            Assert.NotNull(ActivatorUtilities.CreateInstance<PendingWorkRecoveryFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<UnifiedWorkFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<UnifiedWorkPoisonFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<StagedArtifactReconciliationFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<InboxRecoveryFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<SentEvidencePollFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<DueWorkSweepFunction>(scopedServices));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProductionCompositionFailsBeforeRegistrationWhenMailboxIdentityIsMissing()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var values = CreateProductionValues(root);
            values.Remove("Graph:MailboxId");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddPegasusWorker(configuration, new TestHostEnvironment(root)));

            Assert.Contains("Graph:MailboxId", exception.Message, StringComparison.Ordinal);
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
            Assert.NotNull(scopedServices.GetRequiredService<PollSentEvidence>());
            Assert.NotNull(scopedServices.GetRequiredService<RunDueChasers>());
            Assert.Same(
                scopedServices.GetRequiredService<IIntakeWorkStore>(),
                scopedServices.GetRequiredService<IStagedArtifactAuthority>());
            Assert.NotNull(scopedServices.GetRequiredService<ReconcileStagedArtifacts>());

            Assert.NotNull(ActivatorUtilities.CreateInstance<PendingWorkRecoveryFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<UnifiedWorkFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<UnifiedWorkPoisonFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<StagedArtifactReconciliationFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<InboxRecoveryFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<SentEvidencePollFunction>(scopedServices));
            Assert.NotNull(ActivatorUtilities.CreateInstance<DueWorkSweepFunction>(scopedServices));
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

    private static Dictionary<string, string?> CreateProductionValues(string root) => new()
    {
        ["Runtime:Profile"] = "Production",
        ["ConnectionStrings:Pegasus"] =
            "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_WorkerComposition;" +
            "Integrated Security=true;Encrypt=false",
        ["AzureIdentity:WorkerClientId"] = "10213243-5465-7687-98a9-bacbdcedfe0f",
        ["IntakeStorage:ServiceUri"] = "https://custody.example.test/",
        ["IntakeQueue:ServiceUri"] = "https://transport.example.test/",
        ["Graph:BaseUri"] = "https://graph.microsoft.com/v1.0/",
        ["Graph:MailboxId"] = "mailbox-object-id",
        ["Graph:MailboxAddress"] = "instructions@collisionengineers.co.uk",
        ["Graph:InboxFolderId"] = "inbox-folder-id",
        ["Graph:SentFolderId"] = "sent-folder-id",
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
}
