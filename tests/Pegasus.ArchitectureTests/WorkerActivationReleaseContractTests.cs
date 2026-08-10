using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

public sealed class WorkerActivationReleaseContractTests
{
    private static readonly string[] ExpectedFunctions =
    [
        "DueWorkSweepFunction",
        "ExternalPoisonFunction",
        "ExternalWorkFunction",
        "InboxPollFunction",
        "IntakePoisonFunction",
        "IntakeWorkFunction",
        "PendingWorkDispatchFunction",
        "SentEvidencePollFunction",
        "StagedArtifactReconciliationFunction"
    ];

    [Fact]
    public void WorkerActivationInputDefaultsFailClosedAndUsesExactApprovalValue()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainBicep = File.ReadAllText(Path.Combine(repositoryRoot, "infra", "main.bicep"));
        var platformBicep = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "infra",
            "modules",
            "platform.bicep"));
        var parameters = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "infra",
            "main.parameters.json"));

        Assert.Matches(@"param\s+workerActivation\s+string\s*=\s*'disabled'", mainBicep);
        Assert.Matches(@"workerActivation:\s*workerActivation", mainBicep);
        Assert.Matches(
            "\"workerActivation\"\\s*:\\s*\\{\\s*\"value\"\\s*:\\s*" +
            "\"\\$\\{PEGASUS_WORKER_ACTIVATION=disabled\\}\"\\s*\\}",
            parameters);
        Assert.Matches(
            @"workerActivationApproved\s*=\s*workerActivation\s*==\s*'approved-live-worker'",
            platformBicep);
    }

    [Fact]
    public void WorkerActivationTemplateControlsExactNineFunctionCensus()
    {
        var platformBicep = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "infra",
            "modules",
            "platform.bicep"));
        var matches = Regex.Matches(
            platformBicep,
            "name:\\s*'AzureWebJobs\\.([A-Za-z0-9]+)\\.Disabled'\\s*,\\s*" +
            "value:\\s*workerActivationApproved\\s*\\?\\s*'false'\\s*:\\s*'true'",
            RegexOptions.CultureInvariant);
        var actualFunctions = matches
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(9, matches.Count);
        Assert.Equal(ExpectedFunctions, actualFunctions);
    }

    [Fact]
    public void WorkerActivationReleaseValidationUsesTheSameExactCensusAndStopsUnsafeDisable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var deploymentPlan = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "scripts",
            "Test-AzureDeploymentPlan.ps1"));
        var productionSmoke = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "scripts",
            "Invoke-ProductionSmoke.ps1"));
        var smokeFunctions = Regex.Matches(
                productionSmoke,
                "'AzureWebJobs\\.([A-Za-z0-9]+)\\.Disabled'",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedFunctions, smokeFunctions);
        Assert.Contains("az functionapp config appsettings list", productionSmoke);
        Assert.Contains("Compare-Object", productionSmoke);
        Assert.Contains("ExpectedWorkerActivation -eq 'approved-live-worker'", productionSmoke);
        Assert.Contains(
            "An enabled production Worker may not be redeployed with an omitted or disabled desired activation.",
            deploymentPlan);
        Assert.Contains(
            "-AllowWorkerDisable is valid only for an explicit enabled-to-disabled rollback.",
            deploymentPlan);
        Assert.Contains("PEGASUS_WORKER_ACTIVATION=disabled", deploymentPlan);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Pegasus.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }
}
