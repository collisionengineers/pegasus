using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly string[] ExpectedSettingNames = ExpectedFunctions
        .Select(name => $"AzureWebJobs.{name}.Disabled")
        .ToArray();

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
        Assert.Contains("StringComparer]::Ordinal", productionSmoke);
        Assert.Contains("--subscription $SubscriptionId", productionSmoke);
        Assert.Contains("$workerAppName = 'pegasus-prod-worker-252ow37gij'", productionSmoke);
        Assert.Contains("ExpectedWorkerActivation -eq 'approved-live-worker'", productionSmoke);
        Assert.Contains(
            "An enabled production Worker may not be redeployed with an omitted or disabled desired activation.",
            deploymentPlan);
        Assert.Contains(
            "-AllowWorkerDisable is valid only for an explicit enabled-to-disabled rollback.",
            deploymentPlan);
        Assert.Contains("PEGASUS_WORKER_ACTIVATION=disabled", deploymentPlan);
    }

    [Fact]
    public void WorkerSmokeAcceptsExactDisabledCensusAndBindsApprovedTarget()
    {
        var result = RunWorkerSmoke(ExactSettings("true"), "disabled");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Production Worker activation smoke passed (disabled).",
            result.StandardOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "--subscription e6076573-23a5-46a8-acef-7e22d264e5db",
            result.AzureArguments,
            StringComparison.Ordinal);
        Assert.Contains(
            "--name pegasus-prod-worker-252ow37gij",
            result.AzureArguments,
            StringComparison.Ordinal);
        Assert.Contains(
            "--resource-group rg-pegasus-prod",
            result.AzureArguments,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerSmokeAcceptsExactApprovedCensus()
    {
        var result = RunWorkerSmoke(ExactSettings("false"), "approved-live-worker");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Production Worker activation smoke passed (approved-live-worker).",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerSmokeRejectsUnapprovedSubscriptionBeforeAzureRead()
    {
        var result = RunWorkerSmoke(
            ExactSettings("true"),
            "disabled",
            subscriptionId: "00000000-0000-0000-0000-000000000000");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("does not belong to the set", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(result.AzureArguments);
    }

    [Fact]
    public void WorkerSmokeRejectsExtraDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.Add(new("AzureWebJobs.UnexpectedFunction.Disabled", "true"));

        AssertCensusRejected(settings, "AzureWebJobs.UnexpectedFunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsMalformedDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.Add(new("AzureWebJobs.Extra-Function.Disabled", "true"));

        AssertCensusRejected(settings, "AzureWebJobs.Extra-Function.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsCaseVariantDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.RemoveAll(setting =>
            setting.Name == "AzureWebJobs.InboxPollFunction.Disabled");
        settings.Add(new("AzureWebJobs.inboxpollfunction.Disabled", "true"));

        AssertCensusRejected(settings, "AzureWebJobs.inboxpollfunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsMissingDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.RemoveAll(setting =>
            setting.Name == "AzureWebJobs.InboxPollFunction.Disabled");

        AssertCensusRejected(settings, "AzureWebJobs.InboxPollFunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsDuplicateDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.Add(new("AzureWebJobs.InboxPollFunction.Disabled", "true"));

        AssertCensusRejected(settings, "AzureWebJobs.InboxPollFunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsMixedDisabledValues()
    {
        var settings = ExactSettings("true");
        var index = settings.FindIndex(setting =>
            setting.Name == "AzureWebJobs.InboxPollFunction.Disabled");
        settings[index] = settings[index] with { Value = "false" };

        var result = RunWorkerSmoke(settings, "disabled");
        var output = result.StandardOutput + result.StandardError;

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "do not match the intended 'disabled' activation value",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain("InboxPollFunction", output, StringComparison.Ordinal);
        Assert.DoesNotContain("false", output, StringComparison.Ordinal);
    }

    private static void AssertCensusRejected(
        IReadOnlyCollection<WorkerSetting> settings,
        string protectedSettingName)
    {
        var result = RunWorkerSmoke(settings, "disabled");
        var output = result.StandardOutput + result.StandardError;

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "census differs from the exact nine-function release contract",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(protectedSettingName, output, StringComparison.Ordinal);
    }

    private static List<WorkerSetting> ExactSettings(string value) =>
        ExpectedSettingNames
            .Select(name => new WorkerSetting(name, value))
            .ToList();

    private static SmokeResult RunWorkerSmoke(
        IReadOnlyCollection<WorkerSetting> settings,
        string expectedActivation,
        string subscriptionId = "e6076573-23a5-46a8-acef-7e22d264e5db")
    {
        var repositoryRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"pegasus-worker-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        try
        {
            var azureArgumentsPath = Path.Combine(testRoot, "azure-arguments.txt");
            WriteFakeAzureCli(testRoot);

            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(
                repositoryRoot,
                "scripts",
                "Invoke-ProductionSmoke.ps1"));
            startInfo.ArgumentList.Add("-WorkerOnly");
            startInfo.ArgumentList.Add("-SubscriptionId");
            startInfo.ArgumentList.Add(subscriptionId);
            startInfo.ArgumentList.Add("-ResourceGroupName");
            startInfo.ArgumentList.Add("rg-pegasus-prod");
            startInfo.ArgumentList.Add("-ExpectedWorkerActivation");
            startInfo.ArgumentList.Add(expectedActivation);
            startInfo.Environment["PATH"] = testRoot + Path.PathSeparator +
                startInfo.Environment["PATH"];
            startInfo.Environment["PEGASUS_TEST_AZ_SETTINGS_JSON"] =
                JsonSerializer.Serialize(settings);
            startInfo.Environment["PEGASUS_TEST_AZ_ARGUMENTS_PATH"] = azureArgumentsPath;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start mocked Worker smoke.");
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException("Mocked Worker smoke did not finish within 30 seconds.");
            }
            var standardOutput = standardOutputTask.GetAwaiter().GetResult();
            var standardError = standardErrorTask.GetAwaiter().GetResult();

            var azureArguments = File.Exists(azureArgumentsPath)
                ? File.ReadAllText(azureArgumentsPath)
                : string.Empty;
            return new(
                process.ExitCode,
                standardOutput,
                standardError,
                azureArguments);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void WriteFakeAzureCli(string testRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(
                Path.Combine(testRoot, "az.cmd"),
                "@echo off\r\n" +
                "> \"%PEGASUS_TEST_AZ_ARGUMENTS_PATH%\" echo %*\r\n" +
                "echo %PEGASUS_TEST_AZ_SETTINGS_JSON%\r\n" +
                "exit /b 0\r\n");
            return;
        }

        var path = Path.Combine(testRoot, "az");
        File.WriteAllText(
            path,
            "#!/bin/sh\n" +
            "printf '%s\\n' \"$*\" > \"$PEGASUS_TEST_AZ_ARGUMENTS_PATH\"\n" +
            "printf '%s\\n' \"$PEGASUS_TEST_AZ_SETTINGS_JSON\"\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);
    }

    private sealed record WorkerSetting(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("value")] string Value);

    private sealed record SmokeResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string AzureArguments);

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
