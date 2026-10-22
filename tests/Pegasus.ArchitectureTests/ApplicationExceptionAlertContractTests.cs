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
        Assert.Contains("windowSize: 'PT30M'", template, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", template, StringComparison.Ordinal);
        Assert.Contains("AppRequests", template, StringComparison.Ordinal);
        Assert.Contains("TimeGenerated > ago(15m) and Success == false", template, StringComparison.Ordinal);
        Assert.Contains("DistinctOperations >= 3", template, StringComparison.Ordinal);
        Assert.Contains("DistinctMinuteBuckets >= 3", template, StringComparison.Ordinal);
        Assert.Contains("threshold: 0", template, StringComparison.Ordinal);
        Assert.Contains("minFailingPeriodsToAlert: 1", template, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedOperationLookbackCoversTheEvaluationInterval()
    {
        var template = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "infra", "modules", "platform.bicep"));
        var start = template.IndexOf("resource applicationExceptionAlert ", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = template.IndexOf("\n}", start, StringComparison.Ordinal);
        var rule = template[start..end];

        var frequency = System.Text.RegularExpressions.Regex.Match(rule, @"evaluationFrequency: 'PT(\d+)M'");
        Assert.True(frequency.Success);
        var intervalMinutes = int.Parse(frequency.Groups[1].Value);

        var lines = rule.Split('\n').Where(line =>
            line.Contains("Success == false", StringComparison.Ordinal)
            || line.Contains("LastSeen > ago(", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, lines.Count);
        foreach (var line in lines)
        {
            var lookback = System.Text.RegularExpressions.Regex.Match(line, @"ago\((\d+)m\)");
            Assert.True(lookback.Success, line);
            Assert.True(int.Parse(lookback.Groups[1].Value) >= intervalMinutes, line);
        }
    }
}
