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
