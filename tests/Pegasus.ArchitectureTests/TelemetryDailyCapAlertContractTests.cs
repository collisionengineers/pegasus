namespace Pegasus.ArchitectureTests;

public sealed class TelemetryDailyCapAlertContractTests
{
    [Fact]
    public void ProductionAlertWarnsBeforeTheDailyCapTrips()
    {
        var template = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "infra", "modules", "platform.bicep"));

        Assert.Contains("name: '${prefix}-telemetry-daily-cap'", template, StringComparison.Ordinal);
        var start = template.IndexOf("resource telemetryDailyCapAlert ", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = template.IndexOf("\n}", start, StringComparison.Ordinal);
        var rule = template[start..end];

        Assert.Contains("severity: 3", rule, StringComparison.Ordinal);
        Assert.Contains("overrideQueryTimeRange: 'P1D'", rule, StringComparison.Ordinal);
        Assert.Contains("sum(Quantity)", rule, StringComparison.Ordinal);
        Assert.Contains("Usage | where TimeGenerated >= startofday(now())", template, StringComparison.Ordinal);
        Assert.Contains("(${telemetryDailyCapGb} * 1000.0)", template, StringComparison.Ordinal);
        Assert.Contains("threshold: 80", template, StringComparison.Ordinal);
        Assert.Contains("autoMitigate: false", template, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", template, StringComparison.Ordinal);
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
