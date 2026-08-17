using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Production runtime profile must compose the real durable-storage services,
/// not the unavailable fallbacks. These are registration assertions only — no Box,
/// Graph, or Azure call is made, and a composed service is not a deployed feature.
/// </summary>
public sealed class ProductionCompositionTests
{
    private const string BoxConfigJson = """
    {
      "boxAppSettings": {
        "clientID": "client-id",
        "appAuth": {
          "publicKeyID": "key-id",
          "privateKey": "private-key",
          "passphrase": "passphrase"
        }
      },
      "enterpriseID": "enterprise-id"
    }
    """;

    [Fact]
    public void ProductionProfileComposesBoxCustodyAndDocumentContent()
    {
        using var provider = BuildProduction();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsType<BoxCaseCustody>(services.GetRequiredService<ICaseCustody>());
        Assert.IsType<BoxDocumentContentStore>(services.GetRequiredService<IDocumentContentStore>());
        Assert.IsType<AzureBlobIntakeArtifactStore>(services.GetRequiredService<IIntakeArtifactStore>());
        Assert.IsType<AzureBlobIntakeArtifactStore>(
            services.GetRequiredService<IIntakeQuarantineArtifactStore>());
    }

    [Fact]
    public void ProductionProfileComposesTheStaffDocumentAndEvaSurface()
    {
        using var provider = BuildProduction();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<IAddCaseDocument>());
        Assert.NotNull(services.GetRequiredService<IDownloadCaseDocument>());
        Assert.NotNull(services.GetRequiredService<IExportCaseDocuments>());
        Assert.NotNull(services.GetRequiredService<ILogicallyRemoveDocument>());
        Assert.NotNull(services.GetRequiredService<IConfirmThirdPartyVehicleEvidence>());
        Assert.NotNull(services.GetRequiredService<ICaseDocumentStateQueries>());
        Assert.NotNull(services.GetRequiredService<IGenerateEvaHandoff>());
        Assert.NotNull(services.GetRequiredService<IEvaHandoffQueries>());
        Assert.NotNull(services.GetRequiredService<IProcessQueuedCustody>());
    }

    [Fact]
    public void ProductionCustodyAndEvaPortsResolveOnlyApprovedAdaptersAndCoreUseCases()
    {
        using var provider = BuildProduction();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsType<BoxCaseCustody>(services.GetRequiredService<ICaseCustody>());
        Assert.IsType<BoxDocumentContentStore>(services.GetRequiredService<IDocumentContentStore>());
        Assert.IsType<RetryCaseCustody>(services.GetRequiredService<IRetryCaseCustody>());
        Assert.IsType<GenerateEvaHandoff>(services.GetRequiredService<IGenerateEvaHandoff>());
        Assert.IsType<DownloadEvaHandoff>(services.GetRequiredService<IDownloadEvaHandoff>());
        Assert.IsType<EvaHandoffStore>(services.GetRequiredService<IEvaHandoffQueries>());
        Assert.IsType<EvaHandoffStore>(services.GetRequiredService<IEvaHandoffPersistence>());
    }

    [Fact]
    public void ProductionProfileComposesExactlyOneCustodyAndContentImplementation()
    {
        using var provider = BuildProduction();

        Assert.Single(provider.GetServices<ICaseCustody>());
        Assert.Single(provider.GetServices<IDocumentContentStore>());
        Assert.Single(provider.GetServices<IIntakeArtifactStore>());
    }

    [Fact]
    public void ProductionProfileKeepsTheTriageMatcherInactive()
    {
        // Automatic Triage matching stays inactive until its predicates are
        // accepted (docs/open-decisions.md). Activation must be a deliberate
        // change to a named matcher, never a side effect of composition.
        using var provider = BuildProduction();

        Assert.IsType<NoAcceptedIntakeTriageMatcher>(
            provider.GetRequiredService<IIntakeTriageMatcher>());
    }

    [Fact]
    public void ProductionProfileKeepsUploadLinksUnavailableWithoutAcceptedLimits()
    {
        // INT-31 is not on the alpha path and its limits are an open decision, so
        // composing document custody must not activate anonymous upload links.
        using var provider = BuildProduction();
        using var scope = provider.CreateScope();

        Assert.IsType<UnavailableDocumentRequestStore>(
            scope.ServiceProvider.GetRequiredService<ICreateRequestUploadLink>());
    }

    [Fact]
    public void ProfileWithoutDurableStorageStillFailsClosed()
    {
        var services = NewServices();
        services.AddPegasusInfrastructure(ConfigureDatabase);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<UnavailableCaseCustody>(
            scope.ServiceProvider.GetRequiredService<ICaseCustody>());
        Assert.IsType<UnavailableDocumentRequestStore>(
            scope.ServiceProvider.GetRequiredService<ICreateRequestUploadLink>());
        Assert.Null(scope.ServiceProvider.GetService<IDocumentContentStore>());
        Assert.Null(scope.ServiceProvider.GetService<IAddCaseDocument>());
    }

    [Fact]
    public void ALocalArtifactRootAndAnExternalStorageProfileAreMutuallyExclusive()
    {
        var services = NewServices();

        Assert.Throws<InvalidOperationException>(() => services.AddPegasusInfrastructure(
            ConfigureDatabase,
            _ => Path.Combine(Path.GetTempPath(), "pegasus-composition-conflict"),
            documentStorage: registrations => registrations.AddProductionBoxCustody(BoxOptions())));
    }

    private static ServiceProvider BuildProduction()
    {
        var services = NewServices();
        services.AddPegasusInfrastructure(
            ConfigureDatabase,
            documentStorage: registrations => registrations.AddProductionDocumentStorage(
                static _ => new BlobContainerClient(
                    new Uri("https://pegasuscomposition.blob.core.windows.net/transient-intake")),
                static _ => false,
                BoxOptions()));
        return services.BuildServiceProvider();
    }

    private static ServiceCollection NewServices() => new();

    private static void ConfigureDatabase(IServiceProvider _, DbContextOptionsBuilder options) =>
        options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PegasusCompositionOnly;");

    private static BoxCustodyOptions BoxOptions() => BoxCustodyOptions.Create(
        "https://api.box.com/2.0/",
        "https://upload.box.com/api/2.0/",
        "405543781910",
        BoxConfigJson,
        "client-secret");
}
