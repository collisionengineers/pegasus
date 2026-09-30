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
