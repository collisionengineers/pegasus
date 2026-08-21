namespace Pegasus.ArchitectureTests;

public sealed class ApplicationExceptionAlertContractTests
{
    [Fact]
    public void AlertQualifiesFailedAndPersistentOperationsWithoutChangingPagingRoute()
    {
        var template = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "infra",
            "modules",
            "platform.bicep"));

        Assert.Contains("name: '${prefix}-application-exceptions'", template, StringComparison.Ordinal);
        Assert.Contains("severity: 1", template, StringComparison.Ordinal);
        Assert.Contains("windowSize: 'PT15M'", template, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", template, StringComparison.Ordinal);
        Assert.Contains("AppRequests", template, StringComparison.Ordinal);
        Assert.Contains("TimeGenerated > ago(5m) and Success == false", template, StringComparison.Ordinal);
        Assert.Contains("DistinctOperations >= 3", template, StringComparison.Ordinal);
        Assert.Contains("isempty(OperationId)", template, StringComparison.Ordinal);
        Assert.Contains("MinuteBucket=bin(TimeGenerated, 1m)", template, StringComparison.Ordinal);
        Assert.Contains("DistinctMinuteBuckets >= 3", template, StringComparison.Ordinal);
        Assert.Contains("summarize ExceptionCount=count()", template, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AppExceptions | where TimeGenerated > ago(5m) | summarize ExceptionCount=count()",
            template,
            StringComparison.Ordinal);
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
