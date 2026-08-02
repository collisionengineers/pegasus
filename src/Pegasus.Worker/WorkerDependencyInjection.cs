using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Vehicle;

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
            localArtifactRootFactory);
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
            services.AddSingleton(serviceProvider =>
                new AzureBlobIntakeArtifactStore(
                    serviceProvider.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
                    serviceProvider.GetRequiredService<WorkerStorageProvisioning>()
                        .AllowLocalCreateIfNotExists));
            services.AddSingleton<IIntakeArtifactStore>(serviceProvider =>
                serviceProvider.GetRequiredService<AzureBlobIntakeArtifactStore>());
            services.AddSingleton<IIntakeQuarantineArtifactStore>(serviceProvider =>
                serviceProvider.GetRequiredService<AzureBlobIntakeArtifactStore>());
            services.AddProductionExternalAdapters(
                productionOptions!.Value.Graph,
                productionOptions.Value.Box,
                productionOptions.Value.Vehicle);
            services.AddScoped<IProcessQueuedVehicleLookup, ProcessQueuedVehicleLookup>();
            services.AddScoped<IProcessQueuedExternalWork, ProcessQueuedExternalWork>();
        }

        services.AddScoped<EfIntakeWorkStore>();
        services.AddScoped<IIntakeWorkStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfIntakeWorkStore>());
        services.AddScoped<IStagedArtifactAuthority>(serviceProvider =>
            serviceProvider.GetRequiredService<EfIntakeWorkStore>());
        services.AddSingleton<IIntakeWorkEnqueuer, AzureQueueIntakeWorkQueue>();
        services.AddScoped<ReceiveIntake>();
        services.AddScoped<DispatchPendingIntakeWork>();
        services.AddScoped<ProcessQueuedIntake>();
        services.AddScoped<ReconcilePoisonedIntakeWork>();
        services.AddScoped<ReconcileStagedArtifacts>();
        services.AddScoped<ResolveIntake>();
        services.AddScoped<ReevaluateIntake>();
        services.AddSingleton<IExternalWorkEnqueuer, AzureQueueExternalWorkQueue>();
        services.AddScoped<DispatchPendingExternalWork>();
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
        var box = BoxCustodyOptions.Create(
            configuration["Box:BaseUri"],
            configuration["Box:UploadUri"],
            configuration["Box:RootFolderId"],
            configuration["Box:ConfigJson"],
            configuration["Box:ClientSecret"]);
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
        return new(graph, box, DvlaDvsaProductionOptions.Create(vehicleValues));
    }

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
        return new(
            DevelopmentOfflineProfile,
            mailboxId,
            mailboxAddress,
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath)));
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
        BoxCustodyOptions Box,
        DvlaDvsaProductionOptions Vehicle);

}
