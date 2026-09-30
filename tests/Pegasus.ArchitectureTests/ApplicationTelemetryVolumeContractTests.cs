using System.Text.Json;

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
