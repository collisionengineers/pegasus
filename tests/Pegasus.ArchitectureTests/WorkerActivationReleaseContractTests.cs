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
        "InboxRecoveryFunction",
        "PendingWorkRecoveryFunction",
        "SentEvidencePollFunction",
        "StagedArtifactReconciliationFunction",
        "UnifiedWorkFunction",
        "UnifiedWorkPoisonFunction"
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
    public void WorkerActivationTemplateControlsExactSevenFunctionCensus()
    {
        var platformBicep = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "infra",
            "modules",
            "platform.bicep"));
        var nameMatches = Regex.Matches(
            platformBicep,
            "name:\\s*'AzureWebJobs\\.([A-Za-z0-9]+)\\.Disabled'",
            RegexOptions.CultureInvariant);
        var conditionalMatches = Regex.Matches(
            platformBicep,
            "name:\\s*'AzureWebJobs\\.([A-Za-z0-9]+)\\.Disabled'\\s*,\\s*" +
            "value:\\s*workerActivationApproved\\s*\\?\\s*'false'\\s*:\\s*'true'",
            RegexOptions.CultureInvariant);
        var actualFunctions = nameMatches
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var conditionalFunctions = conditionalMatches
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(7, nameMatches.Count);
        Assert.Equal(ExpectedFunctions, actualFunctions);
        Assert.Equal(7, conditionalMatches.Count);
        Assert.Equal(ExpectedFunctions, conditionalFunctions);
    }

    [Fact]
    public void WorkerTemplateKeepsTheUnifiedQueueConsumerWarm()
    {
        var platformBicep = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "infra",
            "modules",
            "platform.bicep"));

        Assert.Matches(
            @"scaleAndConcurrency:\s*\{[\s\S]*?instanceMemoryMB:\s*2048[\s\S]*?" +
            @"alwaysReady:\s*\[[\s\S]*?name:\s*'function:UnifiedWorkFunction'[\s\S]*?" +
            @"instanceCount:\s*1",
            platformBicep);
    }

    [Fact]
    public async Task LocalDeploymentPlanRejectsAppendedRogueHardCodedWorkerSetting()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"pegasus-worker-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        try
        {
            AssertTemporaryFixtureRoot(testRoot);
            CopyValidationFixtureFile(repositoryRoot, testRoot, "azure.yaml");
            CopyValidationFixtureFile(repositoryRoot, testRoot, "infra/main.bicep");
            CopyValidationFixtureFile(repositoryRoot, testRoot, "infra/main.parameters.json");
            CopyValidationFixtureFile(
                repositoryRoot,
                testRoot,
                "infra/modules/platform.bicep");
            CopyValidationFixtureFile(
                repositoryRoot,
                testRoot,
                "scripts/Invoke-ProductionSmoke.ps1");
            CopyValidationFixtureFile(
                repositoryRoot,
                testRoot,
                "scripts/Build-ReleaseArtifacts.ps1");
            CopyValidationFixtureFile(
                repositoryRoot,
                testRoot,
                "scripts/Test-AzureDeploymentPlan.ps1");

            var platformBicepPath = Path.Combine(
                testRoot,
                "infra",
                "modules",
                "platform.bicep");
            var platformBicep = File.ReadAllText(platformBicepPath);
            const string marker =
                "        { name: 'AzureWebJobs.PendingWorkRecoveryFunction.Disabled'";
            var mutatedPlatformBicep = platformBicep.Replace(
                marker,
                "        { name: 'AzureWebJobs.Rogue-Function.Disabled', value: 'false' }" +
                Environment.NewLine + marker,
                StringComparison.Ordinal);
            Assert.NotEqual(platformBicep, mutatedPlatformBicep);
            File.WriteAllText(platformBicepPath, mutatedPlatformBicep);

            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = testRoot
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(
                testRoot,
                "scripts",
                "Test-AzureDeploymentPlan.ps1"));
            startInfo.ArgumentList.Add("-Mode");
            startInfo.ArgumentList.Add("Local");
            startInfo.ArgumentList.Add("-WorkerActivation");
            startInfo.ArgumentList.Add("disabled");
            ScrubInheritedGitEnvironment(startInfo);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "Failed to start isolated Local deployment-plan validation.");
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw new TimeoutException(
                    "Isolated Local deployment-plan validation did not finish within 30 seconds.");
            }
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            var diagnostic = NormalizeDiagnosticWhitespace(
                $"Exit code: {process.ExitCode}{Environment.NewLine}" +
                $"Standard output:{Environment.NewLine}{standardOutput}{Environment.NewLine}" +
                $"Standard error:{Environment.NewLine}{standardError}");

            Assert.NotEqual(0, process.ExitCode);
            Assert.True(
                diagnostic.Contains(
                    "exact seven-function disabled-setting name census",
                    StringComparison.Ordinal),
                diagnostic);
            Assert.DoesNotContain("Rogue-Function", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            AssertTemporaryFixtureRoot(testRoot);
            Directory.Delete(testRoot, recursive: true);
        }
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
        Assert.Contains("$sourceWorkerNameMatches", deploymentPlan);
        Assert.Contains("$sourceWorkerConditionalMatches", deploymentPlan);
        Assert.Contains("$compiledWorkerNameMatches", deploymentPlan);
        Assert.Contains("$compiledWorkerConditionalMatches", deploymentPlan);
        Assert.Contains("GetUnixFileMode", deploymentPlan);
        Assert.Contains("UnixFileMode]::UserExecute", deploymentPlan);
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
            setting.Name == "AzureWebJobs.InboxRecoveryFunction.Disabled");
        settings.Add(new("AzureWebJobs.inboxpollfunction.Disabled", "true"));

        AssertCensusRejected(settings, "AzureWebJobs.inboxpollfunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsMissingDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.RemoveAll(setting =>
            setting.Name == "AzureWebJobs.InboxRecoveryFunction.Disabled");

        AssertCensusRejected(settings, "AzureWebJobs.InboxRecoveryFunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsDuplicateDisabledSetting()
    {
        var settings = ExactSettings("true");
        settings.Add(new("AzureWebJobs.InboxRecoveryFunction.Disabled", "true"));

        AssertCensusRejected(settings, "AzureWebJobs.InboxRecoveryFunction.Disabled");
    }

    [Fact]
    public void WorkerSmokeRejectsMixedDisabledValues()
    {
        var settings = ExactSettings("true");
        var index = settings.FindIndex(setting =>
            setting.Name == "AzureWebJobs.InboxRecoveryFunction.Disabled");
        settings[index] = settings[index] with { Value = "false" };

        var result = RunWorkerSmoke(settings, "disabled");
        var output = NormalizeDiagnosticWhitespace(
            result.StandardOutput + result.StandardError);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "do not match the intended 'disabled' activation value",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain("InboxRecoveryFunction", output, StringComparison.Ordinal);
        Assert.DoesNotContain("false", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PreProvisionRejectsEveryMissingOrEmptyRequiredConfigurationBeforeSmoke()
    {
        foreach (var key in new[]
                 {
                     "BOX_HOLDING_FOLDER_ID",
                     "AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS",
                     "AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS"
                 })
        {
            foreach (var value in new string?[] { null, string.Empty, "   " })
            {
                var environment = ValidPreProvisionEnvironment();
                if (value is null)
                {
                    environment.Remove(key);
                }
                else
                {
                    environment[key] = value;
                }

                var result = RunPreProvisionValidation(environment);

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains($"missing {key}", result.Diagnostic, StringComparison.Ordinal);
                Assert.DoesNotContain("functionapp config appsettings list", result.AzureArguments, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void PreProvisionRejectsMalformedOrCrossVaultCertificateUrisBeforeSmoke()
    {
        foreach (var value in new[]
                 {
                     "http://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version",
                     "https://operator@pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version",
                     "https://pegasusprodkv252ow37g.vault.azure.net:444/secrets/signing/version",
                     "https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version?query=value",
                     "https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version#fragment",
                     "https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing"
                 })
        {
            var environment = ValidPreProvisionEnvironment();
            environment["AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS"] = value;

            var result = RunPreProvisionValidation(environment);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS must contain",
                result.Diagnostic,
                StringComparison.Ordinal);
            Assert.DoesNotContain("functionapp config appsettings list", result.AzureArguments, StringComparison.Ordinal);
        }

        var crossVault = ValidPreProvisionEnvironment();
        crossVault["AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS"] =
            "https://another-vault.vault.azure.net/secrets/encryption/version";
        var crossVaultResult = RunPreProvisionValidation(crossVault);

        Assert.NotEqual(0, crossVaultResult.ExitCode);
        Assert.Contains("same Azure Key Vault", crossVaultResult.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("functionapp config appsettings list", crossVaultResult.AzureArguments, StringComparison.Ordinal);
    }

    [Fact]
    public void PreProvisionAcceptsVersionedSameVaultCertificatesAndReachesSmoke()
    {
        var result = RunPreProvisionValidation(ValidPreProvisionEnvironment());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Azure deployment plan validation passed (PreProvision", result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("functionapp config appsettings list", result.AzureArguments, StringComparison.Ordinal);
    }

    private static void AssertCensusRejected(
        IReadOnlyCollection<WorkerSetting> settings,
        string protectedSettingName)
    {
        var result = RunWorkerSmoke(settings, "disabled");
        var output = NormalizeDiagnosticWhitespace(
            result.StandardOutput + result.StandardError);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "census differs from the exact seven-function release contract",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(protectedSettingName, output, StringComparison.Ordinal);
    }

    private static List<WorkerSetting> ExactSettings(string value) =>
        ExpectedSettingNames
            .Select(name => new WorkerSetting(name, value))
            .ToList();

    private static Dictionary<string, string> ValidPreProvisionEnvironment() => new(StringComparer.Ordinal)
    {
        ["AZURE_SUBSCRIPTION_ID"] = "e6076573-23a5-46a8-acef-7e22d264e5db",
        ["AZURE_TENANT_ID"] = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94",
        ["AZURE_RESOURCE_GROUP"] = "rg-pegasus-prod",
        ["WORKER_APP_NAME"] = "pegasus-prod-worker-252ow37gij",
        ["PEGASUS_WORKER_ACTIVATION"] = "disabled",
        ["BOX_HOLDING_FOLDER_ID"] = "test-holding-folder",
        ["AZURE_KEY_VAULT_NAME"] = "pegasusprodkv252ow37g",
        ["AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS"] =
            "https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing-current/version-one," +
            "https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing-retained/version-two",
        ["AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS"] =
            "https://pegasusprodkv252ow37g.vault.azure.net/secrets/encryption/version-three"
    };

    private static PreProvisionResult RunPreProvisionValidation(
        IReadOnlyDictionary<string, string> environment)
    {
        var repositoryRoot = FindRepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), $"pegasus-preprovision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        try
        {
            AssertTemporaryFixtureRoot(testRoot);
            var environmentPath = Path.Combine(testRoot, "environment.txt");
            var settingsPath = Path.Combine(testRoot, "worker-settings.json");
            var compiledTemplatePath = Path.Combine(testRoot, "compiled-template.json");
            var azureArgumentsPath = Path.Combine(testRoot, "azure-arguments.txt");
            File.WriteAllLines(environmentPath, environment.Select(item => $"{item.Key}={item.Value}"));
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(ExactSettings("true")));
            File.WriteAllText(compiledTemplatePath, CompiledWorkerTemplate());
            WriteFakePreProvisionCommands(testRoot);

            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot
            };
            ScrubInheritedGitEnvironment(startInfo);
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts", "Test-AzureDeploymentPlan.ps1"));
            startInfo.ArgumentList.Add("-Mode");
            startInfo.ArgumentList.Add("PreProvision");
            startInfo.ArgumentList.Add("-Environment");
            startInfo.ArgumentList.Add("test");
            startInfo.ArgumentList.Add("-WorkerActivation");
            startInfo.ArgumentList.Add("disabled");
            startInfo.ArgumentList.Add("-ExpectedLiveWorkerActivation");
            startInfo.ArgumentList.Add("disabled");
            startInfo.Environment["PATH"] = testRoot + Path.PathSeparator + startInfo.Environment["PATH"];
            startInfo.Environment["PEGASUS_TEST_AZD_VALUES_PATH"] = environmentPath;
            startInfo.Environment["PEGASUS_TEST_AZ_SETTINGS_PATH"] = settingsPath;
            startInfo.Environment["PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH"] = compiledTemplatePath;
            startInfo.Environment["PEGASUS_TEST_AZ_ARGUMENTS_PATH"] = azureArgumentsPath;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start mocked PreProvision validation.");
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException("Mocked PreProvision validation did not finish within 30 seconds.");
            }

            return new(
                process.ExitCode,
                NormalizeDiagnosticWhitespace(
                    standardOutputTask.GetAwaiter().GetResult() + Environment.NewLine +
                    standardErrorTask.GetAwaiter().GetResult()),
                File.Exists(azureArgumentsPath) ? File.ReadAllText(azureArgumentsPath) : string.Empty);
        }
        finally
        {
            AssertTemporaryFixtureRoot(testRoot);
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CompiledWorkerTemplate() =>
        "{\"resources\":[" + string.Join(",", ExpectedSettingNames.Select(name =>
            $"{{\"name\":\"{name}\",\"value\":\"[if(variables('workerActivationApproved'), 'false', 'true')]\"}}")) +
        "],\"variables\":{\"workerActivationApproved\":\"[equals(parameters('workerActivation'), 'approved-live-worker')]\"}," +
        "\"parameters\":{\"workerActivation\":{\"type\":\"string\",\"defaultValue\":\"disabled\"}}}";

    private static void WriteFakePreProvisionCommands(string testRoot)
    {
        AssertTemporaryFixtureRoot(testRoot);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(
                Path.Combine(testRoot, "az.cmd"),
                "@echo off\r\n" +
                ">> \"%PEGASUS_TEST_AZ_ARGUMENTS_PATH%\" echo %*\r\n" +
                "echo %* | findstr /c:\"bicep build\" >nul && (type \"%PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH%\" & exit /b 0)\r\n" +
                "type \"%PEGASUS_TEST_AZ_SETTINGS_PATH%\"\r\n");
            File.WriteAllText(
                Path.Combine(testRoot, "azd.cmd"),
                "@echo off\r\n" +
                "type \"%PEGASUS_TEST_AZD_VALUES_PATH%\"\r\n");
            return;
        }

        var azPath = Path.Combine(testRoot, "az");
        File.WriteAllText(
            azPath,
            "#!/bin/sh\n" +
            "printf '%s\\n' \"$*\" >> \"$PEGASUS_TEST_AZ_ARGUMENTS_PATH\"\n" +
            "case \"$*\" in *'bicep build'*) cat \"$PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH\";; *) cat \"$PEGASUS_TEST_AZ_SETTINGS_PATH\";; esac\n");
        File.SetUnixFileMode(azPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var azdPath = Path.Combine(testRoot, "azd");
        File.WriteAllText(azdPath, "#!/bin/sh\ncat \"$PEGASUS_TEST_AZD_VALUES_PATH\"\n");
        File.SetUnixFileMode(azdPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string NormalizeDiagnosticWhitespace(string value) =>
        Regex.Replace(
            Regex.Replace(value, @"\s*\|\s*", " ", RegexOptions.CultureInvariant),
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);

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
            ScrubInheritedGitEnvironment(startInfo);
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
            AssertTemporaryFixtureRoot(testRoot);
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void WriteFakeAzureCli(string testRoot)
    {
        AssertTemporaryFixtureRoot(testRoot);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(
                Path.Combine(testRoot, "az.cmd"),
                "@echo off\r\n" +
                "> \"%PEGASUS_TEST_AZ_ARGUMENTS_PATH%\" echo %*\r\n" +
                "echo %* | findstr /c:\"PendingWorkRecoverySchedule\" >nul && (echo 0 * * * * * & exit /b 0)\r\n" +
                "echo %PEGASUS_TEST_AZ_SETTINGS_JSON%\r\n" +
                "exit /b 0\r\n");
            return;
        }

        var path = Path.Combine(testRoot, "az");
        File.WriteAllText(
            path,
            "#!/bin/sh\n" +
            "printf '%s\\n' \"$*\" > \"$PEGASUS_TEST_AZ_ARGUMENTS_PATH\"\n" +
            "case \"$*\" in *PendingWorkRecoverySchedule*) printf '%s\\n' '0 * * * * *'; exit 0;; esac\n" +
            "printf '%s\\n' \"$PEGASUS_TEST_AZ_SETTINGS_JSON\"\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);
    }

    private static void CopyValidationFixtureFile(
        string repositoryRoot,
        string testRoot,
        string relativePath)
    {
        var sourcePath = Path.Combine(repositoryRoot, relativePath);
        var destinationPath = Path.Combine(testRoot, relativePath);
        AssertTemporaryFixtureRoot(testRoot, destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath);
    }

    private static void ScrubInheritedGitEnvironment(ProcessStartInfo startInfo)
    {
        foreach (var name in startInfo.Environment.Keys
                     .Where(name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(name);
        }
    }

    private static void AssertTemporaryFixtureRoot(string testRoot, string? target = null)
    {
        var root = Path.GetFullPath(testRoot).TrimEnd(Path.DirectorySeparatorChar);
        var temporaryParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryParent, root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
        if (target is not null)
        {
            Assert.StartsWith(root + Path.DirectorySeparatorChar, Path.GetFullPath(target),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record WorkerSetting(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("value")] string Value);

    private sealed record SmokeResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string AzureArguments);

    private sealed record PreProvisionResult(int ExitCode, string Diagnostic, string AzureArguments);

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
