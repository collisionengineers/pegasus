
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegasus.Worker;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging => logging.ApplyWorkerLogFilters())
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddApplicationInsightsTelemetryProcessor<SqlDependencyTelemetryFilter>();

        // Ingestion is configured for Entra, so the telemetry client needs the
        // Worker's managed-identity credential. WorkerAzureClientFactory defines
        // it once; Production registers it and DevelopmentOffline does not.
        services.AddWorkerTelemetryCredential();

        services.AddPegasusWorker(context.Configuration, context.HostingEnvironment);
    })
    .Build();

host.Run();
