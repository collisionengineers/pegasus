using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Vehicle;
using Pegasus.Infrastructure.Transport;

namespace Pegasus.Worker;

public static class WorkerDependencyInjection
{
    private const string DevelopmentOfflineProfile = "DevelopmentOffline";
    private const string ProductionProfile = "Production";

    public static IServiceCollection AddPegasusWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var runtimeProfile = configuration["Runtime:Profile"]
            ?? throw new InvalidOperationException("Runtime:Profile is required.");
        var developmentOffline = runtimeProfile.Equals(
            DevelopmentOfflineProfile,
            StringComparison.Ordinal);
        if (!developmentOffline && !runtimeProfile.Equals(ProductionProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported Runtime:Profile '{runtimeProfile}'.");
        }
        ProductionExternalOptions? productionOptions = developmentOffline
            ? null
            : GetProductionExternalOptions(configuration);
        // The local live-integration opt-ins. Each is a DevelopmentOffline
        // feature in the same sense as Features:LocalIntake: Production never
        // reads them, and a Production host that carries one fails at start
        // naming the key rather than composing a second storage or vendor truth.
        var liveVehicleLookup = RequireOfflineFeature(configuration, developmentOffline, "Features:LiveVehicleLookup");
        var liveBoxCustody = RequireOfflineFeature(configuration, developmentOffline, "Features:LiveBoxCustody");
        AzureDocumentIntelligenceOptions? ocrOptions = null;
        var ocrEndpointValue = configuration[WorkerAzureClientFactory.DocumentIntelligenceEndpointKey];
        if (!developmentOffline && !string.IsNullOrEmpty(ocrEndpointValue))
        {
            if (string.IsNullOrWhiteSpace(ocrEndpointValue)
                || !Uri.TryCreate(ocrEndpointValue, UriKind.Absolute, out var ocrEndpoint)
                || !ocrEndpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{WorkerAzureClientFactory.DocumentIntelligenceEndpointKey} must be an absolute HTTPS URI.");
            }

            ocrOptions = AzureDocumentIntelligenceOptions.Create(ocrEndpoint);
        }
        var azureClientRegistration = developmentOffline
            ? WorkerAzureClientFactory.CreateDevelopmentOffline(configuration)
            : WorkerAzureClientFactory.CreateProduction(configuration);

        // Live Box custody offline keeps the production storage shape (blob-backed
        // intake artifacts, cached document reads, Box case custody) over the run's
        // Azurite container, so Web and Worker still share one storage truth. The
        // Azurite connection string was validated by CreateDevelopmentOffline above.
        if (liveBoxCustody)
        {
            var intakeConnectionString = configuration["IntakeStorage:ConnectionString"]
                ?? configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException(
                    "AzureWebJobsStorage is required for Features:LiveBoxCustody.");
            // Pinned to the newest service version the repository's Azurite pin
            // (3.36.0) speaks; the SDK's default is ahead of it, and Azurite
            // refuses the request rather than downgrading.
            services.AddSingleton(new Azure.Storage.Blobs.BlobContainerClient(
                intakeConnectionString,
                WorkerAzureClientFactory.IntakeArtifactContainerName,
                new Azure.Storage.Blobs.BlobClientOptions(
                    Azure.Storage.Blobs.BlobClientOptions.ServiceVersion.V2025_11_05)));
        }
        var composesLocalArtifactRoot = developmentOffline && !liveBoxCustody;
        Func<IServiceProvider, string>? localArtifactRootFactory = composesLocalArtifactRoot
            ? _ => GetOfflineArtifactRoot(configuration, environment)
            : null;
        var approvedBoxRoot = developmentOffline
            ? BoxCustodyOptions.DevelopmentRootFolderId
            : BoxCustodyOptions.ProductionRootFolderId;
        services.AddPegasusInfrastructure(
            (_, options) => ConfigureDatabase(configuration, options),
            localArtifactRootFactory,
            documentStorage: composesLocalArtifactRoot
                ? null
                : registrations => registrations.AddProductionDocumentStorage(
                    provider => provider.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
                    provider => provider.GetRequiredService<WorkerStorageProvisioning>()
                        .AllowLocalCreateIfNotExists,
                    // Deferred to first Box use: parsing this at host build aborted
                    // the whole worker process whenever the platform handed over an
                    // unresolved Key Vault reference.
                    _ => CreateBoxCustodyOptions(configuration, approvedBoxRoot)));
        services.AddScoped<EfIdentityAuditStore>();
        services.AddScoped<IActionHistoryWriter>(serviceProvider =>
            serviceProvider.GetRequiredService<EfIdentityAuditStore>());
        // The shared use cases include the paged queries the Web serves; this
        // host serves none, and says so if one is ever asked for.
        services.AddSingleton<Pegasus.Core.ICursorProtector, Pegasus.Infrastructure.Support.UnavailableCursorProtector>();
        azureClientRegistration.AddTo(services);
        services.AddSingleton<StaffNotificationPurgeSchedule>();

        if (developmentOffline)
        {
            services.AddLocalApprovedInbox(
                _ => GetLocalApprovedInboxOptions(configuration, environment));
            services.AddLocalApprovedSent(
                _ => GetLocalApprovedSentOptions(configuration, environment));
            if (liveVehicleLookup)
            {
                // Parsed eagerly, as Production does: a missing DVLA/DVSA key
                // fails the Worker at start naming the key, not the first lookup.
                services.AddLiveVehicleLookup(
                    DvlaDvsaProductionOptions.Create(ReadVehicleValues(configuration)));
            }
            else
            {
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay);
                services.AddSingleton<IVehicleLookupAdapter>(provider =>
                    new DvlaDvsaReplayAdapter(
                        Path.Combine(GetOfflineArtifactRoot(configuration, environment), "vehicle-replay"),
                        provider.GetRequiredService<TimeProvider>()));
            }
            services.AddScoped<IProcessQueuedVehicleLookup, ProcessQueuedVehicleLookup>();
            services.AddScoped<IProcessQueuedExternalWork, ProcessQueuedExternalWork>();
        }
        else
        {
            services.AddProductionExternalAdapters(
                productionOptions!.Value.Graph,
                productionOptions.Value.Vehicle);
            services.AddScoped<IProcessQueuedVehicleLookup, ProcessQueuedVehicleLookup>();

            if (ocrOptions is not null)
            {
                services.AddSingleton(ocrOptions);
                services.AddHttpClient<IIntakeOcrProvider, AzureDocumentIntelligenceOcr>();
                services.AddScoped<IProcessIntakeOcr, ProcessIntakeOcr>();
            }
            services.AddScoped<IProcessQueuedExternalWork, ProcessQueuedExternalWork>();
        }

        services.AddScoped<VehicleRegistrationCandidateLookup>();

        services.AddScoped<EfIntakeWorkStore>();
        services.AddScoped<IIntakeWorkStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfIntakeWorkStore>());
        services.AddScoped<IStagedArtifactAuthority>(serviceProvider =>
            serviceProvider.GetRequiredService<EfIntakeWorkStore>());
        services.AddSingleton<IIntakeWorkEnqueuer>(serviceProvider =>
        {
            var queues = serviceProvider.GetRequiredService<WorkerQueueClients>();
            var provisioning = serviceProvider.GetRequiredService<WorkerStorageProvisioning>();
            return new AzureQueueIntakeWorkEnqueuer(
                queues.WorkQueue,
                provisioning.AllowLocalCreateIfNotExists);
        });
        services.AddScoped<ReceiveIntake>();
        services.AddScoped<IIntakeSubmission>(serviceProvider =>
            serviceProvider.GetRequiredService<ReceiveIntake>());
        services.AddScoped<SubmitGroupedIntake>();
        services.AddScoped<IGroupedIntakeSubmission>(serviceProvider =>
            serviceProvider.GetRequiredService<SubmitGroupedIntake>());
        services.AddScoped<DispatchPendingIntakeWork>();
        services.AddScoped<ICommittedIntakeWorkPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<DispatchPendingIntakeWork>());
        services.AddScoped<ProcessQueuedIntake>();
        services.AddScoped<IProcessQueuedIntake>(serviceProvider =>
            serviceProvider.GetRequiredService<ProcessQueuedIntake>());
        services.AddScoped<ReconcilePoisonedIntakeWork>();
        services.AddScoped<ReconcileStagedArtifacts>();
        services.AddScoped<ReconcileGroupedImageIntake>();
        services.AddScoped<ReconcileAutomaticVehicleLookups>();
        services.AddScoped<ResolveIntake>();
        services.AddScoped<ReevaluateIntake>();
        services.AddSingleton<IExternalWorkEnqueuer>(serviceProvider =>
        {
            var queues = serviceProvider.GetRequiredService<WorkerQueueClients>();
            var provisioning = serviceProvider.GetRequiredService<WorkerStorageProvisioning>();
            return new AzureQueueExternalWorkEnqueuer(
                queues.WorkQueue,
                provisioning.AllowLocalCreateIfNotExists);
        });
        services.AddScoped<DispatchPendingExternalWork>();
        services.AddScoped<ICommittedExternalWorkPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<DispatchPendingExternalWork>());
        services.AddScoped<ReconcilePoisonedExternalWork>();
        services.AddScoped<ReconcilePoisonedQueueWork>();
        services.AddScoped<DispatchPendingWork>();
        // Composed in both profiles: it does nothing when no Graph adapter is present.
        services.AddScoped<MaintainMailboxChangeSubscriptions>();
        return services;
    }

    private static bool RequireOfflineFeature(
        IConfiguration configuration,
        bool developmentOffline,
        string key)
    {
        var enabled = configuration.GetValue<bool>(key);
        if (enabled && !developmentOffline)
        {
            throw new InvalidOperationException(
                $"{key} requires the DevelopmentOffline runtime profile.");
        }

        return enabled;
    }

    private static ProductionExternalOptions GetProductionExternalOptions(
        IConfiguration configuration)
    {
        var graph = GraphApprovedMailboxOptions.Create(configuration["Graph:BaseUri"]);
        return new(graph, DvlaDvsaProductionOptions.Create(ReadVehicleValues(configuration)));
    }

    private static Dictionary<string, string?> ReadVehicleValues(IConfiguration configuration) =>
        new(StringComparer.Ordinal)
        {
            ["Dvla:BaseUri"] = configuration["Dvla:BaseUri"],
            ["Dvla:ApiKey"] = configuration["Dvla:ApiKey"],
            ["Dvsa:BaseUri"] = configuration["Dvsa:BaseUri"],
            ["Dvsa:TokenUri"] = configuration["Dvsa:TokenUri"],
            ["Dvsa:ClientId"] = configuration["Dvsa:ClientId"],
            ["Dvsa:ClientSecret"] = configuration["Dvsa:ClientSecret"],
            ["Dvsa:ApiKey"] = configuration["Dvsa:ApiKey"],
            ["Dvsa:Scope"] = configuration["Dvsa:Scope"]
        };

    private static BoxCustodyOptions CreateBoxCustodyOptions(
        IConfiguration configuration,
        string approvedRootFolderId) =>
        BoxCustodyOptions.Create(
            configuration["Box:BaseUri"],
            configuration["Box:UploadUri"],
            configuration["Box:RootFolderId"],
            configuration["Box:ConfigJson"],
            configuration["Box:ClientSecret"],
            configuration["Box:HoldingFolderId"],
            approvedRootFolderId);

    private static void ConfigureDatabase(
        IConfiguration configuration,
        DbContextOptionsBuilder options)
    {
        var connectionString = configuration.GetConnectionString("Pegasus")
            ?? throw new InvalidOperationException("Connection string 'Pegasus' is required.");
        PegasusSqlServer.Configure(options, connectionString);
    }

    private static string GetOfflineArtifactRoot(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var localPath = configuration["Intake:LocalArtifactPath"];
        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new InvalidOperationException(
                "Intake:LocalArtifactPath is required for deterministic offline source retention.");
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath));
    }

    private static LocalApprovedInboxOptions GetLocalApprovedInboxOptions(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var mailboxId = configuration["ApprovedInbox:MailboxId"]
            ?? throw new InvalidOperationException("ApprovedInbox:MailboxId is required.");
        var mailboxAddress = configuration["ApprovedInbox:MailboxAddress"]
            ?? throw new InvalidOperationException("ApprovedInbox:MailboxAddress is required.");
        var localPath = configuration["ApprovedInbox:LocalRootPath"]
            ?? throw new InvalidOperationException("ApprovedInbox:LocalRootPath is required.");
        // Optional: the offline root is now the root of a local mailbox estate, and each
        // mailbox reads one folder beneath it. Existing settings keep the default.
        var inboxFolderIdentity = configuration["ApprovedInbox:InboxFolderIdentity"];
        return string.IsNullOrWhiteSpace(inboxFolderIdentity)
            ? new(
                DevelopmentOfflineProfile,
                mailboxId,
                mailboxAddress,
                Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath)))
            : new(
                DevelopmentOfflineProfile,
                mailboxId,
                mailboxAddress,
                Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath)),
                inboxFolderIdentity);
    }

    private static LocalApprovedSentOptions GetLocalApprovedSentOptions(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var mailboxId = configuration["ApprovedSent:MailboxId"]
            ?? throw new InvalidOperationException("ApprovedSent:MailboxId is required.");
        var mailboxAddress = configuration["ApprovedSent:MailboxAddress"]
            ?? throw new InvalidOperationException("ApprovedSent:MailboxAddress is required.");
        var sentFolderIdentity = configuration["ApprovedSent:SentFolderIdentity"]
            ?? throw new InvalidOperationException("ApprovedSent:SentFolderIdentity is required.");
        var localPath = configuration["ApprovedSent:LocalRootPath"]
            ?? throw new InvalidOperationException("ApprovedSent:LocalRootPath is required.");
        return new(
            DevelopmentOfflineProfile,
            mailboxId,
            mailboxAddress,
            sentFolderIdentity,
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath)));
    }
    private readonly record struct ProductionExternalOptions(
        GraphApprovedMailboxOptions Graph,
        DvlaDvsaProductionOptions Vehicle);

}
