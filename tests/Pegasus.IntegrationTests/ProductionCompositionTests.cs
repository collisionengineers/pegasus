using Azure.Storage.Blobs;
using Azure.Core;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Email;
using Pegasus.Web;

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
        Assert.NotNull(services.GetRequiredService<IExportCaseBundle>());
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
        Assert.IsType<EvaHandoffStore>(services.GetRequiredService<IExportCaseBundle>());
        Assert.NotNull(services.GetRequiredService<IEvaHandoffProxy>());
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
    public void ProductionProfileSharesOperationsSnapshotWithAttentionRows()
    {
        using var provider = BuildProduction();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var snapshot = services.GetRequiredService<GetOperationsSnapshot>();

        Assert.Same(snapshot, services.GetRequiredService<IGetOperationsSnapshot>());
        Assert.Same(snapshot, services.GetRequiredService<IGetAttentionRows>());
        Assert.Single(services.GetServices<IGetOperationsSnapshot>());
        Assert.Single(services.GetServices<IGetAttentionRows>());
    }

    [Fact]
    public void ProductionProfileDrivesTriageFromTheAcceptedRouteClassification()
    {
        // Automatic Triage matching was pinned inactive while its predicates
        // were unaccepted. They are now accepted, and they live where FRD-03
        // and ADR-0008 put them: the route's own classification policy. This
        // test keeps the same protection pointed at the real mechanism — one
        // named, versioned owner, activated deliberately and not as a side
        // effect of composition (INTK-033).
        using var provider = BuildProduction();

        var classification = Assert.Single(provider.GetServices<IMailClassificationPolicy>());
        Assert.IsType<QdosMailClassificationPolicy>(classification);
        Assert.Equal(QdosMailClassificationPolicy.Key, classification.PolicyKey);
        Assert.Equal(QdosMailClassificationPolicy.Version, classification.PolicyVersion);
    }

    [Fact]
    public void ProductionProfileComposesAllInstructionProfilesAndTheIntakeProcessor()
    {
        using var provider = BuildProduction();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var policies = services.GetServices<IInstructionExtractionPolicy>().ToArray();
        Assert.Equal(15, policies.Length);
        Assert.Equal(
            policies.Length,
            policies.Select(policy => policy.PrincipalCode).Distinct(StringComparer.Ordinal).Count());
        Assert.All(policies, policy => Assert.IsAssignableFrom<IInstructionDocumentProfile>(policy));

        var qdos = services.GetRequiredService<QdosInstructionExtractionPolicy>();
        Assert.Same(qdos, Assert.Single(policies, policy => policy is QdosInstructionExtractionPolicy));

        Assert.NotNull(services.GetRequiredService<ProcessIntake>());
        Assert.NotNull(services.GetRequiredService<InstructionExtractionPolicySelector>());
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
    public void ProductionProfileComposesRequestUploadsWhenLimitsAreAccepted()
    {
        var services = NewServices();
        services.AddPegasusInfrastructure(
            ConfigureDatabase,
            requestUploadLimitsFactory: _ => new RequestUploadLimits(
                "accepted-v1",
                TimeSpan.FromHours(1),
                5,
                1024,
                5120,
                ["text/plain"],
                10,
                TimeSpan.FromMinutes(1)),
            documentStorage: registrations => registrations.AddProductionDocumentStorage(
                static _ => new BlobContainerClient(
                    new Uri("https://pegasuscomposition.blob.core.windows.net/transient-intake")),
                static _ => false,
                static _ => BoxOptions()));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<EfDocumentRequestStore>(
            scope.ServiceProvider.GetRequiredService<IUploadToRequest>());
        Assert.IsType<EfPublicUploadRetentionStore>(
            scope.ServiceProvider.GetRequiredService<IIncomingArtifactRetentionStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RetainIncomingArtifact>());
        Assert.IsType<BoxDocumentContentStore>(
            scope.ServiceProvider.GetRequiredService<IDocumentContentStore>());
    }

    [Fact]
    public void ProductionWebTelemetryIncludesPublicUploadUrlSanitization()
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000"
            });

        Assert.Contains(
            factory.Services.GetServices<ITelemetryInitializer>(),
            initializer => initializer is PublicUploadTelemetryInitializer);
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
        Assert.IsType<UnavailableDeletedMailSearchSource>(
            scope.ServiceProvider.GetRequiredService<IDeletedMailSearchSource>());
    }

    [Fact]
    public void ProductionGraphRegistrationSurvivesTheInfrastructureFallback()
    {
        var services = NewServices();
        services.AddSingleton<TokenCredential>(new CompositionCredential());
        services.AddSingleton<IIntakeSourceReader>(
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));
        services.AddLogging();
        services.AddProductionApprovedMailboxResolver("https://graph.microsoft.com/v1.0/");
        services.AddPegasusInfrastructure(ConfigureDatabase);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<GraphDeletedMailSearchSource>(
            scope.ServiceProvider.GetRequiredService<IDeletedMailSearchSource>());
        Assert.Single(scope.ServiceProvider.GetServices<IDeletedMailSearchSource>());
    }

    [Fact]
    public void ALocalArtifactRootAndAnExternalStorageProfileAreMutuallyExclusive()
    {
        var services = NewServices();

        Assert.Throws<InvalidOperationException>(() => services.AddPegasusInfrastructure(
            ConfigureDatabase,
            _ => Path.Combine(Path.GetTempPath(), "pegasus-composition-conflict"),
            documentStorage: registrations => registrations.AddProductionBoxCustody(_ => BoxOptions())));
    }

    [Fact]
    public void AnUnresolvedBoxSecretFailsTheFirstBoxUseNotHostBuild()
    {
        // PLAT-013: parsing the Box secret during host build aborted the whole
        // worker process (exit 134) whenever the platform handed over an
        // unresolved Key Vault reference. Composition must succeed and non-Box
        // services must resolve; only the first Box resolution fails closed.
        var services = NewServices();
        services.AddPegasusInfrastructure(
            ConfigureDatabase,
            documentStorage: registrations => registrations.AddProductionDocumentStorage(
                static _ => new BlobContainerClient(
                    new Uri("https://pegasuscomposition.blob.core.windows.net/transient-intake")),
                static _ => false,
                static _ => BoxCustodyOptions.Create(
                    "https://api.box.com/2.0/",
                    "https://upload.box.com/api/2.0/",
                    "405543781910",
                    "@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/box-config-json)",
                    "client-secret")));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<AzureBlobIntakeArtifactStore>(
            provider.GetRequiredService<IIntakeArtifactStore>());
        var error = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<ICaseCustody>());
        Assert.Contains("unresolved Key Vault reference", error.Message, StringComparison.Ordinal);
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
                static _ => BoxOptions()));
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

    private sealed class CompositionCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("unused", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
