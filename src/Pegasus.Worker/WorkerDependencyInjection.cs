using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegasus.Core.Custody;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Eva;
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
        var azureClientRegistration = developmentOffline
            ? WorkerAzureClientFactory.CreateDevelopmentOffline(configuration)
            : WorkerAzureClientFactory.CreateProduction(configuration);

        Func<IServiceProvider, string>? localArtifactRootFactory = developmentOffline
            ? _ => GetOfflineArtifactRoot(configuration, environment)
            : null;
        services.AddPegasusInfrastructure(
            (_, options) => ConfigureDatabase(configuration, options),
            localArtifactRootFactory,
            documentStorage: developmentOffline
                ? null
                : registrations => registrations.AddProductionDocumentStorage(
                    provider => provider.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
                    provider => provider.GetRequiredService<WorkerStorageProvisioning>()
                        .AllowLocalCreateIfNotExists,
                    // Deferred to first Box use: parsing this at host build aborted
                    // the whole worker process whenever the platform handed over an
                    // unresolved Key Vault reference (PLAT-013).
                    _ => CreateBoxCustodyOptions(configuration)));
        services.AddScoped<EfIdentityAuditStore>();
        services.AddScoped<IActionHistoryWriter>(serviceProvider =>
            serviceProvider.GetRequiredService<EfIdentityAuditStore>());
        azureClientRegistration.AddTo(services);

        if (developmentOffline)
        {
            services.AddLocalApprovedInbox(
                _ => GetLocalApprovedInboxOptions(configuration, environment));
            services.AddLocalApprovedSent(
                _ => GetLocalApprovedSentOptions(configuration, environment));
            services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay);
            services.AddSingleton<IVehicleLookupAdapter>(provider =>
                new DvlaDvsaReplayAdapter(
                    Path.Combine(GetOfflineArtifactRoot(configuration, environment), "vehicle-replay"),
                    provider.GetRequiredService<TimeProvider>()));
            services.AddScoped<IProcessQueuedVehicleLookup, ProcessQueuedVehicleLookup>();
            services.AddScoped<IProcessQueuedExternalWork, ProcessQueuedExternalWork>();
        }
        else
        {
            services.AddProductionExternalAdapters(
                productionOptions!.Value.Graph,
                productionOptions.Value.Vehicle);
            services.AddScoped<IProcessQueuedVehicleLookup, ProcessQueuedVehicleLookup>();

            // EXT-04. Composed only in production, and only when EVA is
            // configured: the offline profile reaches no vendor, so it has no
            // EVA handler and a row of that kind fails closed rather than
            // being quietly completed.
            services.AddEvaApiSubmission(_ => GetEvaApiOptions(configuration));
            services.AddScoped<IProcessQueuedEvaSubmission, ProcessQueuedEvaSubmission>();
            services.AddScoped<IProcessQueuedExternalWork, ProcessQueuedExternalWork>();
        }

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
        services.AddScoped<SubmitMailboxImageIntake>();
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
        return services;
    }

    private static ProductionExternalOptions GetProductionExternalOptions(
        IConfiguration configuration)
    {
        var graph = GraphApprovedMailboxOptions.Create(
            configuration["Graph:BaseUri"],
            configuration["Graph:MailboxId"],
            configuration["Graph:MailboxAddress"],
            configuration["Graph:InboxFolderId"],
            configuration["Graph:SentFolderId"]);
        var vehicleValues = new Dictionary<string, string?>(StringComparer.Ordinal)
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
        return new(graph, DvlaDvsaProductionOptions.Create(vehicleValues));
    }

    /// <summary>
    /// EXT-04: EVA's credentials and the three instruction values that are
    /// deployment configuration rather than case data. Read lazily by
    /// <c>AddEvaApiSubmission</c>, because these arrive as Key Vault
    /// references and parsing one at host build is what crash-looped the
    /// worker in PLAT-013.
    /// </summary>
    private static EvaApiOptions GetEvaApiOptions(IConfiguration configuration) =>
        EvaApiOptions.Create(key => configuration[key]);

    private static BoxCustodyOptions CreateBoxCustodyOptions(IConfiguration configuration) =>
        BoxCustodyOptions.Create(
            configuration["Box:BaseUri"],
            configuration["Box:UploadUri"],
            configuration["Box:RootFolderId"],
            configuration["Box:ConfigJson"],
            configuration["Box:ClientSecret"],
            configuration["Box:HoldingFolderId"]);

    private static void ConfigureDatabase(
        IConfiguration configuration,
        DbContextOptionsBuilder options)
    {
        var connectionString = configuration.GetConnectionString("Pegasus")
            ?? throw new InvalidOperationException("Connection string 'Pegasus' is required.");
        options.UseSqlServer(connectionString);
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
