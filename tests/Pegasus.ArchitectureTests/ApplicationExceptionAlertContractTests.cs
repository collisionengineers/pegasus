namespace Pegasus.ArchitectureTests;

public sealed class ApplicationExceptionAlertContractTests
{
    [Fact]
    public void ProductionAlertPagesForFailedOrRepeatedOperations()
    {
        var template = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "infra", "modules", "platform.bicep"));

        Assert.Contains("name: '${prefix}-application-exceptions'", template, StringComparison.Ordinal);
        Assert.Contains("severity: 2", template, StringComparison.Ordinal);
        Assert.Contains("windowSize: 'PT15M'", template, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", template, StringComparison.Ordinal);
        Assert.Contains("AppRequests", template, StringComparison.Ordinal);
        Assert.Contains("TimeGenerated > ago(5m) and Success == false", template, StringComparison.Ordinal);
        Assert.Contains("DistinctOperations >= 3", template, StringComparison.Ordinal);
        Assert.Contains("DistinctMinuteBuckets >= 3", template, StringComparison.Ordinal);
        Assert.Contains("threshold: 0", template, StringComparison.Ordinal);
        Assert.Contains("minFailingPeriodsToAlert: 1", template, StringComparison.Ordinal);
    }
}
