namespace Pegasus.ArchitectureTests;

/// <summary>
/// The three rules of R447 (22 October 2026): matching failures, chasing failures
/// and Web readiness. Each test reads the rule from the Bicep and the line it
/// counts from the source that writes it, so renaming either breaks the test.
/// </summary>
public sealed class MatchingChasingReadinessAlertContractTests
{
    [Fact]
    public void MatchingFailuresPageOnThreePairingStepFailuresInFifteenMinutes()
    {
        var rule = Rule("matchingFailureAlert");

        Assert.Contains("name: '${prefix}-matching-failures'", rule, StringComparison.Ordinal);
        Assert.Contains("displayName: 'Pegasus production matching failures'", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("description:", rule, StringComparison.Ordinal);
        Assert.Contains("severity: 2", rule, StringComparison.Ordinal);
        Assert.Contains("evaluationFrequency: 'PT15M'", rule, StringComparison.Ordinal);
        Assert.Contains("windowSize: 'PT15M'", rule, StringComparison.Ordinal);
        Assert.Contains("union AppExceptions, AppTraces", rule, StringComparison.Ordinal);
        Assert.Contains(
            "tostring(Properties.CategoryName) == \"Pegasus.Worker.StagedArtifactReconciliationFunction\"",
            rule,
            StringComparison.Ordinal);
        Assert.Contains(
            "tostring(Properties.Step) in (\"image pairing\", \"Triage pairing\")",
            rule,
            StringComparison.Ordinal);
        Assert.Contains("operator: 'GreaterThanOrEqual'", rule, StringComparison.Ordinal);
        Assert.Contains("threshold: 3", rule, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", rule, StringComparison.Ordinal);

        var worker = WorkerFunctions();
        Assert.Contains("ILogger<StagedArtifactReconciliationFunction> logger", worker, StringComparison.Ordinal);
        Assert.Contains("RunStepAsync(\"image pairing\"", worker, StringComparison.Ordinal);
        Assert.Contains("RunStepAsync(\"Triage pairing\"", worker, StringComparison.Ordinal);
        Assert.Contains(
            "Message = \"The {Step} step failed; the invocation fails after the other steps have run.\"",
            worker,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChasingFailuresPageOnOneDueWorkSweepFailureInAnHour()
    {
        var rule = Rule("chasingFailureAlert");

        Assert.Contains("name: '${prefix}-chasing-failures'", rule, StringComparison.Ordinal);
        Assert.Contains("displayName: 'Pegasus production chasing failures'", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("description:", rule, StringComparison.Ordinal);
        Assert.Contains("severity: 2", rule, StringComparison.Ordinal);
        Assert.Contains("evaluationFrequency: 'PT15M'", rule, StringComparison.Ordinal);
        Assert.Contains("windowSize: 'PT1H'", rule, StringComparison.Ordinal);
        Assert.Contains("union AppExceptions, AppTraces", rule, StringComparison.Ordinal);
        Assert.Contains(
            "tostring(Properties.CategoryName) == \"Pegasus.Worker.PendingWorkRecoveryFunction\"",
            rule,
            StringComparison.Ordinal);
        Assert.Contains("tostring(Properties.Job) == \"due-work sweep\"", rule, StringComparison.Ordinal);
        Assert.Contains("operator: 'GreaterThanOrEqual'", rule, StringComparison.Ordinal);
        Assert.Contains("threshold: 1", rule, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", rule, StringComparison.Ordinal);

        var worker = WorkerFunctions();
        Assert.Contains("ILogger<PendingWorkRecoveryFunction> logger", worker, StringComparison.Ordinal);
        Assert.Contains("RunFoldedJobAsync(\"due-work sweep\"", worker, StringComparison.Ordinal);
        Assert.Contains("FoldedJobBudget = TimeSpan.FromSeconds(60)", worker, StringComparison.Ordinal);
        Assert.Contains(
            "Message = \"The {Job} job failed; the invocation fails after the other steps have run.\"",
            worker,
            StringComparison.Ordinal);
        Assert.Contains(
            "Message = \"The {Job} job passed its {BudgetSeconds} second budget and was cancelled; the invocation fails after the other steps have run.\"",
            worker,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WebReadinessPagesOnThreeFailedProbesInFifteenMinutes()
    {
        var rule = Rule("webReadinessAlert");

        Assert.Contains("name: '${prefix}-web-readiness'", rule, StringComparison.Ordinal);
        Assert.Contains("displayName: 'Pegasus production Web readiness'", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("description:", rule, StringComparison.Ordinal);
        Assert.Contains("severity: 1", rule, StringComparison.Ordinal);
        Assert.Contains("evaluationFrequency: 'PT15M'", rule, StringComparison.Ordinal);
        Assert.Contains("windowSize: 'PT15M'", rule, StringComparison.Ordinal);
        Assert.Contains(
            "AppRequests\n              | where Name == \"GET /health/ready\" or tostring(parse_url(Url).Path) == \"/health/ready\"",
            rule.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains("where Success == false or toint(ResultCode) >= 500", rule, StringComparison.Ordinal);
        Assert.Contains("operator: 'GreaterThanOrEqual'", rule, StringComparison.Ordinal);
        Assert.Contains("threshold: 3", rule, StringComparison.Ordinal);
        Assert.Contains("actionGroups: [actionGroup.id]", rule, StringComparison.Ordinal);

        var root = RepositoryRoot.Find();
        Assert.Contains(
            "app.MapHealthChecks(\"/health/ready\"",
            File.ReadAllText(Path.Combine(root, "src", "Pegasus.Web", "Program.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "healthCheckPath: '/health/ready'",
            File.ReadAllText(Path.Combine(root, "infra", "modules", "platform.bicep")),
            StringComparison.Ordinal);
    }

    private static string Rule(string resourceName)
    {
        var template = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "infra", "modules", "platform.bicep"));
        var start = template.IndexOf(
            $"resource {resourceName} 'Microsoft.Insights/scheduledQueryRules@",
            StringComparison.Ordinal);
        Assert.True(start >= 0, $"{resourceName} was not found.");
        var end = template.IndexOf("\n}", start, StringComparison.Ordinal);
        return template[start..end];
    }

    private static string WorkerFunctions() => File.ReadAllText(Path.Combine(
        RepositoryRoot.Find(), "src", "Pegasus.Worker", "IntakeFunctions.cs"));
}
