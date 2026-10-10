using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Options;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class ApplicationTelemetryVolumeContractTests
{
    [Fact]
    public void WebSuppressesSuccessfulEntityFrameworkCommandLogs()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Pegasus.Web",
            "appsettings.json")));

        var logLevel = configuration.RootElement
            .GetProperty("Logging")
            .GetProperty("LogLevel");

        Assert.Equal(
            "Warning",
            logLevel.GetProperty("Microsoft.EntityFrameworkCore.Database.Command").GetString());
    }

    // Lane B of the optimisation roadmap adds volume on purpose, and states it
    // here so it stays bounded. Metrics are never sampled, so the counter list
    // is the whole of it: twelve counters at the SDK's 60 s interval is 17,280
    // rows a day. The heartbeat trace is 288 rows a day. Per-request stamps are
    // properties of a row that already exists. What the quiet-row filter drops
    // (probe, ping and static-file rows) is far larger than all of that.
    [Fact]
    public void WebSendsTwelveRuntimeCountersAndNoOthers()
    {
        var counters = Pegasus.Web.Health.RuntimeCounters.Requested;

        Assert.Equal(12, counters.Count);
        Assert.Equal(12, counters.Distinct().Count());
        Assert.Equal(
            ["Microsoft.Data.SqlClient.EventSource", "System.Runtime"],
            counters.Select(counter => counter.Source).Distinct().Order().ToArray());
        Assert.Equal(7, counters.Count(counter => counter.Source == "System.Runtime"));
        Assert.Equal(5, counters.Count(counter => counter.Source == "Microsoft.Data.SqlClient.EventSource"));
    }

    [Fact]
    public void WebComposesTheCounterListTheQuietRowFilterAndTheHeartbeat()
    {
        var program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Pegasus.Web", "Program.cs"));

        Assert.Contains("ConfigureTelemetryModule<EventCounterCollectionModule>", program, StringComparison.Ordinal);
        Assert.Contains("RuntimeCounters.Apply(module)", program, StringComparison.Ordinal);
        Assert.Contains(
            "AddApplicationInsightsTelemetryProcessor<QuietRequestTelemetryFilter>", program, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<RuntimeHeartbeat>", program, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeartbeatTraceReachesApplicationInsightsAtInformation()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Pegasus.Web",
            "appsettings.json")));

        var categories = configuration.RootElement
            .GetProperty("Logging")
            .GetProperty("ApplicationInsights")
            .GetProperty("LogLevel");

        Assert.Equal("Information", categories.GetProperty("Pegasus.Web.Health.RuntimeHeartbeat").GetString());
    }

    /// <summary>
    /// The Worker host writes about 90% of the telemetry ingested. Its own chatter is
    /// dropped at source with the exact categories seen in production traces. Every
    /// Warning and Error line still passes, and so does the alert's source: AppRequests
    /// (category Host.Results, which must stay unfiltered) and AppExceptions.
    /// </summary>
    [Fact]
    public void WorkerHostDropsItsOwnChatterAtSourceAndKeepsRequests()
    {
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Pegasus.Worker",
            "host.json")));

        var logging = host.RootElement.GetProperty("logging");
        var levels = logging.GetProperty("logLevel")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.Ordinal);

        Assert.Equal(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["default"] = "Information",
                ["Function"] = "Warning",
                ["Host.Startup"] = "Warning",
                ["Host.Triggers"] = "Warning",
                ["Host.Aggregator"] = "Warning",
                ["Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService"] = "Warning",
                ["Microsoft.Azure.WebJobs.Script.Description.FunctionGroupListenerDecorator"] = "Warning",
                ["Microsoft.Azure.WebJobs.Host.DrainModeManager"] = "Warning",
                ["Azure.Core"] = "Warning",
                ["Azure.Identity"] = "Warning",
                ["Microsoft.AspNetCore"] = "Warning",
                ["Pegasus.Worker"] = "Information",
                ["Microsoft.EntityFrameworkCore.Database.Command"] = "None"
            },
            levels);
        Assert.DoesNotContain("Host.Results", levels.Keys);
        Assert.Equal(
            "Request;Dependency;Exception",
            logging.GetProperty("applicationInsights").GetProperty("samplingSettings")
                .GetProperty("excludedTypes").GetString());
    }

    [Fact]
    public void WorkerProcessDropsEntityFrameworkCommandLinesFromApplicationInsightsByName()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ApplyWorkerLogFilters());
        using var provider = services.BuildServiceProvider();

        var rules = provider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value.Rules;

        var rule = Assert.Single(rules, candidate =>
            candidate.CategoryName == "Microsoft.EntityFrameworkCore.Database.Command");
        Assert.Equal(typeof(ApplicationInsightsLoggerProvider).FullName, rule.ProviderName);
        Assert.Equal(LogLevel.None, rule.LogLevel);
    }

    /// <summary>
    /// Program composes the span bridge beside the SQL dependency filter, and
    /// resolves it before the host runs: the listener is a singleton's constructor,
    /// so it exists only once something asks for the singleton.
    /// </summary>
    [Fact]
    public void WorkerProgramComposesAndStartsTheSpanBridgeBeforeTheHostRuns()
    {
        var program = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Pegasus.Worker",
            "Program.cs"));

        var filter = program.IndexOf(
            "AddApplicationInsightsTelemetryProcessor<SqlDependencyTelemetryFilter>()",
            StringComparison.Ordinal);
        var compose = program.IndexOf("services.AddWorkerSpanTelemetry()", StringComparison.Ordinal);
        var resolve = program.IndexOf(
            "GetRequiredService<WorkerSpanTelemetryBridge>()",
            StringComparison.Ordinal);
        var run = program.IndexOf("host.Run()", StringComparison.Ordinal);

        Assert.True(filter >= 0, "The SQL dependency filter is composed.");
        Assert.True(compose > filter, "The span bridge is composed after the filter.");
        Assert.True(resolve > compose, "The span bridge is resolved after it is composed.");
        Assert.True(run > resolve, "The span bridge is resolved before the host runs.");
    }

    /// <summary>
    /// The app's own no-store middleware is overwritten by antiforgery on every
    /// protected post, which logged 1,264 warnings in 14 days and carries nothing
    /// to act on. Both the console and the Application Insights providers hide it.
    /// </summary>
    [Fact]
    public void WebHidesTheAntiforgeryCacheHeaderWarningFromBothLogProviders()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Pegasus.Web",
            "appsettings.json")));

        var logging = configuration.RootElement.GetProperty("Logging");

        Assert.Equal(
            "Error",
            logging.GetProperty("LogLevel").GetProperty("Microsoft.AspNetCore.Antiforgery").GetString());
        Assert.Equal(
            "Error",
            logging.GetProperty("ApplicationInsights").GetProperty("LogLevel")
                .GetProperty("Microsoft.AspNetCore.Antiforgery").GetString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }
}
