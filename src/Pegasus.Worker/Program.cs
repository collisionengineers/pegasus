
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pegasus.Worker;
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddApplicationInsightsTelemetryProcessor<SqlDependencyTelemetryFilter>();

        // PLAT-034: ingestion is configured for Entra — the deployed app sets
        // APPLICATIONINSIGHTS_AUTHENTICATION_STRING naming the Worker's
        // user-assigned identity — but the worker process's own telemetry
        // client was never given a credential, so everything it sent was
        // rejected and thirty days of production produced no telemetry at all.
        // Silent on purpose upstream: a telemetry client that cannot
        // authenticate drops rather than throws, which is why this went
        // unnoticed until a custody failure had to be diagnosed without logs.
        var workerClientId = context.Configuration["AzureIdentity:WorkerClientId"];
        if (!string.IsNullOrWhiteSpace(workerClientId))
        {
            services.Configure<TelemetryConfiguration>(telemetry =>
                telemetry.SetAzureTokenCredential(
                    new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = workerClientId,
                        ExcludeEnvironmentCredential = true,
                        ExcludeWorkloadIdentityCredential = true,
                        ExcludeManagedIdentityCredential = false,
                        ExcludeVisualStudioCredential = true,
                        ExcludeVisualStudioCodeCredential = true,
                        ExcludeAzureCliCredential = true,
                        ExcludeAzurePowerShellCredential = true,
                        ExcludeAzureDeveloperCliCredential = true,
                        ExcludeInteractiveBrowserCredential = true,
                        ExcludeBrokerCredential = true
                    })));
        }

        services.AddPegasusWorker(context.Configuration, context.HostingEnvironment);
    })
    .Build();

host.Run();
