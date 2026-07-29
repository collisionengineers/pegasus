using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
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
    .ConfigureServices(services =>
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
                options.UseSqlite(new SqliteConnectionStringBuilder
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
        }, serviceProvider => Path.Combine(
            serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath,
            ".unused-local-intake-artifacts"));
        services.AddSingleton<IIntakeArtifactStore, AzureBlobIntakeArtifactStore>();
        services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
        services.AddSingleton<IIntakeWorkEnqueuer, AzureQueueIntakeWorkQueue>();
        services.AddScoped<ReceiveIntake>();
        services.AddScoped<DispatchPendingIntakeWork>();
        services.AddScoped<ProcessQueuedIntake>();
        services.AddScoped<ReconcilePoisonedIntakeWork>();
        services.AddScoped<ReconcileStagedIntakeArtifacts>();
        services.AddScoped<ResolveIntake>();
        services.AddScoped<ReevaluateIntake>();
    })
    .Build();

host.Run();
