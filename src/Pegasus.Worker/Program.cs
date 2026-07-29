using Pegasus.Core.Custody;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegasus.Worker;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();

        services.AddPegasusInfrastructure((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var databaseProvider = configuration["Database:Provider"]
                ?? throw new InvalidOperationException("Database:Provider is required.");
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = configuration["Database:LocalPath"]
                    ?? throw new InvalidOperationException("Database:LocalPath is required for SQLite.");
                var fullPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                options.UsePegasusSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = fullPath,
                    ForeignKeys = true
                }.ToString());
                return;
            }

            if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                var connectionName = configuration["Database:ConnectionStringName"] ?? "Pegasus";
                options.UseSqlServer(configuration.GetConnectionString(connectionName)
                    ?? throw new InvalidOperationException($"Connection string '{connectionName}' is required."));
                return;
            }

            throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
        }, GetOfflineReplayArtifactRoot);
        services.AddLocalApprovedInbox(GetLocalApprovedInboxOptions);
        if (!string.Equals(
                context.Configuration["Runtime:Profile"],
                "DevelopmentOffline",
                StringComparison.Ordinal))
        {
            services.AddSingleton<IIntakeArtifactStore, AzureBlobIntakeArtifactStore>();
        }
        services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
        services.AddSingleton<IIntakeWorkEnqueuer, AzureQueueIntakeWorkQueue>();
        services.AddScoped<ReceiveIntake>();
        services.AddScoped<DispatchPendingIntakeWork>();
        services.AddScoped<ProcessQueuedIntake>();
        services.AddScoped<ReconcilePoisonedIntakeWork>();
        services.AddScoped<ReconcileStagedIntakeArtifacts>();
        services.AddScoped<ResolveIntake>();
        services.AddScoped<ReevaluateIntake>();
        services.AddSingleton<IExternalWorkEnqueuer, AzureQueueExternalWorkQueue>();
        services.AddScoped<DispatchPendingExternalWork>();
        services.AddScoped<ReconcilePoisonedExternalWork>();
    })
    .Build();

host.Run();

static string GetOfflineReplayArtifactRoot(IServiceProvider serviceProvider)
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    if (!string.Equals(
            configuration["Runtime:Profile"],
            "DevelopmentOffline",
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The local custody adapter is disabled. Runtime:Profile must be DevelopmentOffline.");
    }

    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var localPath = configuration["Intake:LocalArtifactPath"];
    if (string.IsNullOrWhiteSpace(localPath))
    {
        throw new InvalidOperationException(
            "Intake:LocalArtifactPath is required for deterministic offline source retention.");
    }
    return Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath));
}

static LocalApprovedInboxOptions GetLocalApprovedInboxOptions(IServiceProvider serviceProvider)
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var mailboxId = configuration["ApprovedInbox:MailboxId"]
        ?? throw new InvalidOperationException("ApprovedInbox:MailboxId is required.");
    var mailboxAddress = configuration["ApprovedInbox:MailboxAddress"]
        ?? throw new InvalidOperationException("ApprovedInbox:MailboxAddress is required.");
    var localPath = configuration["ApprovedInbox:LocalRootPath"]
        ?? throw new InvalidOperationException("ApprovedInbox:LocalRootPath is required.");
    return new(
        configuration["Runtime:Profile"] ?? string.Empty,
        mailboxId,
        mailboxAddress,
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, localPath)));
}
