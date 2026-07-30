using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

public sealed class ReleaseEvidenceScriptTests
{
    private const string SourceRevision = "1111111111111111111111111111111111111111";
    private const string OtherRevision = "2222222222222222222222222222222222222222";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] OfflineReplayMode = ["offline-replay"];
    private const string ReleaseVersion = "0.1.0-alpha.1";
    private static readonly string[] ExpectedWorkerFunctions =
    [
        "InboxPollFunction",
        "SentEvidencePollFunction",
        "PendingWorkDispatchFunction",
        "IntakeWorkFunction",
        "ExternalWorkFunction",
        "IntakePoisonFunction",
        "ExternalPoisonFunction",
        "StagedArtifactReconciliationFunction",
        "DueWorkSweepFunction"
    ];
    private static readonly string[] DeploymentInputPaths =
    [
        "azure.yaml",
        "infra/main.bicep",
        "infra/main.parameters.json",
        "infra/modules/platform.bicep"
    ];
    private static readonly string[] ProvenanceInputPaths =
    [
        ".config/dotnet-tools.json",
        "Directory.Build.props",
        "global.json",
        "package.json",
        "package-lock.json",
        "Pegasus.slnx",
        "src/Pegasus.Core/Pegasus.Core.csproj",
        "src/Pegasus.Core/packages.lock.json",
        "src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj",
        "src/Pegasus.Infrastructure/packages.lock.json",
        "src/Pegasus.Web/Pegasus.Web.csproj",
        "src/Pegasus.Web/packages.lock.json",
        "src/Pegasus.Worker/Pegasus.Worker.csproj",
        "src/Pegasus.Worker/packages.lock.json",
        "src/Pegasus.Bootstrap/Pegasus.Bootstrap.csproj",
        "src/Pegasus.Bootstrap/packages.lock.json"
    ];
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly Regex AnsiEscapePattern = new(
        "\u001b\\[[0-?]*[ -/]*[@-~]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PowerShellErrorGutterPattern = new(
        "^[ \t]*\\|[ \t]?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    [Fact]
    public async Task DeploymentValidationRejectsMixedRevisionBeforeBicepCompilation()
    {
        using var sandbox = new ScriptSandbox();
        sandbox.CopyRepositoryScript("Test-AzureDeploymentPlan.ps1");
        sandbox.WriteFile("infra/main.bicep", "var activationAllowed = false\n");
        sandbox.WriteFile(".azure/deployment-plan.md", "# Test plan\n");
        sandbox.WriteGitCommand(SourceRevision);
        sandbox.WriteTool(
            "bicep.cmd",
            "@echo off\r\ntype nul > \"%~dp0bicep-invoked.txt\"\r\nexit /b 0\r\n");

        var artifactDirectory = sandbox.CreateDirectory("artifacts/release");
        sandbox.WriteJson(
            "artifacts/release/release-manifest.json",
            new
            {
                schemaVersion = 1,
                releaseMode = "offline-replay",
                sourceRevision = OtherRevision,
                webDiagnostic = new
                {
                    schemaVersion = 1,
                    version = "0.1.0-test",
                    sourceSha = OtherRevision
                },
                artifacts = Array.Empty<object>()
            });

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        foreach (var expectedToken in new[]
                 {
                     "Release manifest sourceRevision",
                     OtherRevision,
                     "does not match",
                     "clean checkout",
                     "HEAD",
                     SourceRevision
                 })
        {
            Assert.True(
                result.NormalizedOutput.Contains(
                    NormalizePowerShellOutput(expectedToken),
                    StringComparison.Ordinal),
                result.CombinedOutput);
        }
        Assert.False(File.Exists(Path.Combine(sandbox.ToolsDirectory, "bicep-invoked.txt")));
    }

    [Fact]
    public async Task DeploymentValidationAcceptsExactRevisionArtifactsAndPackagedInputs()
    {
        var disabledSettings = ExpectedWorkerFunctions.ToDictionary(
            name => name,
            _ => "true",
            StringComparer.Ordinal);
        using var sandbox = CreateDeploymentValidationSandbox(
            disabledSettings,
            out var artifactDirectory);

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory,
            "-Mode",
            "Local");

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains(
            NormalizePowerShellOutput("are valid"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        var bicepArguments = await File.ReadAllTextAsync(
            Path.Combine(sandbox.ToolsDirectory, "bicep-arguments.txt"));
        Assert.Contains(
            "pegasus-deployment-plan-",
            bicepArguments,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Path.Combine(sandbox.Root, "infra", "main.bicep"),
            bicepArguments,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(sandbox.ToolsDirectory, "dotnet-invoked.txt")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task DeploymentValidationRejectsMissingOrEnabledWorkerFunctionGate(
        string? dueWorkSweepGate)
    {
        var disabledSettings = ExpectedWorkerFunctions.ToDictionary(
            name => name,
            _ => "true",
            StringComparer.Ordinal);
        if (dueWorkSweepGate is null)
        {
            disabledSettings.Remove("DueWorkSweepFunction");
        }
        else
        {
            disabledSettings["DueWorkSweepFunction"] = dueWorkSweepGate;
        }

        using var sandbox = CreateDeploymentValidationSandbox(
            disabledSettings,
            out var artifactDirectory);

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("DueWorkSweepFunction"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AzureIdentity__WorkerClientId", null, "AzureIdentity__WorkerClientId")]
    [InlineData(
        "AzureIdentity__WorkerClientId",
        "[reference('differentIdentity').clientId]",
        "same exact Worker user-assigned managed identity")]
    [InlineData("ExternalWorkQueue__ServiceUri", null, "ExternalWorkQueue__ServiceUri")]
    [InlineData(
        "IntakeStorage__ServiceUri",
        "DefaultEndpointsProtocol=https;AccountKey=forbidden",
        "direct non-secret identity or endpoint metadata")]
    [InlineData(
        "AzureWebJobsStorage__credential",
        "connectionstring",
        "must require managed identity authentication")]
    public async Task DeploymentValidationRejectsUnsafeWorkerAzureBindings(
        string settingName,
        string? replacementValue,
        string expectedMessage)
    {
        var workerAzureSettings = CreateWorkerAzureSettings();
        if (replacementValue is null)
        {
            workerAzureSettings.Remove(settingName);
        }
        else
        {
            workerAzureSettings[settingName] = replacementValue;
        }

        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory,
            workerAzureSettings: workerAzureSettings);

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput(expectedMessage),
            result.NormalizedOutput,
            StringComparison.Ordinal);
    }


    [Fact]
    public async Task DeploymentValidationRejectsTamperedArtifactBeforeCompilation()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        await File.AppendAllTextAsync(
            Path.Combine(artifactDirectory, "web-linux-x64.zip"),
            "tampered");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("length differs from the manifest"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ToolsDirectory, "bicep-arguments.txt")));
    }

    [Fact]
    public async Task DeploymentValidationRejectsBootstrapManifestDigestMismatch()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        var manifestPath = Path.Combine(
            artifactDirectory,
            "release-manifest.json");
        var manifest = JsonNode.Parse(
            await File.ReadAllTextAsync(manifestPath))!.AsObject();
        manifest["bootstrapManifest"]!.AsObject()["sha256"] = new string('0', 64);
        await File.WriteAllTextAsync(
            manifestPath,
            manifest.ToJsonString(IndentedJsonOptions));
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "release-manifest.sha256"),
            $"{Sha256(manifestPath)}  release-manifest.json\n");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("Bootstrap manifest hash differs"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ToolsDirectory, "bicep-arguments.txt")));
    }

    [Fact]
    public async Task DeploymentValidationRejectsReleaseInputTreeMismatch()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        var manifestPath = Path.Combine(
            artifactDirectory,
            "release-manifest.json");
        var manifest = JsonNode.Parse(
            await File.ReadAllTextAsync(manifestPath))!.AsObject();
        manifest["releaseInputTree"]!.AsObject()["sha256"] = new string('0', 64);
        await File.WriteAllTextAsync(
            manifestPath,
            manifest.ToJsonString(IndentedJsonOptions));
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "release-manifest.sha256"),
            $"{Sha256(manifestPath)}  release-manifest.json\n");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("input-tree digest does not match"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ToolsDirectory, "bicep-arguments.txt")));
    }

    [Fact]
    public async Task DeploymentValidationRejectsRawPublishDirectory()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        sandbox.CreateDirectory("artifacts/release/publish/web");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("raw directory"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeploymentValidationRejectsMissingArtifact()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        File.Delete(Path.Combine(artifactDirectory, "migration-bundle-win-x64.zip"));

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("Release output files must be exact"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeploymentValidationRejectsExtraArtifact()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "unexpected.zip"),
            "unexpected");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("Release output files must be exact"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeploymentValidationRejectsArchivePathTraversal()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory,
            unsafeWorkerEntry: "../outside.txt");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("unsafe entry"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeploymentValidationRejectsDirtyCheckoutBeforeArtifacts()
    {
        using var sandbox = CreateDeploymentValidationSandbox(
            ExpectedWorkerFunctions.ToDictionary(
                name => name,
                _ => "true",
                StringComparer.Ordinal),
            out var artifactDirectory);
        sandbox.WriteGitCommand(SourceRevision, reportDirtyFlag: true);
        sandbox.WriteFile("tools/dirty.flag", string.Empty);

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Test-AzureDeploymentPlan.ps1"),
            "-ArtifactDirectory",
            artifactDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("tracked or untracked changes"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ToolsDirectory, "bicep-arguments.txt")));
    }

    [Fact]
    public async Task InitializationRejectsCheckoutThatBecomesDirtyDuringBuild()
    {
        using var sandbox = new ScriptSandbox();
        sandbox.CopyRepositoryScript("Initialize-LocalDevelopment.ps1");
        sandbox.WriteFile("Pegasus.slnx", "<Solution />\n");
        sandbox.WriteGitCommand(SourceRevision, reportDirtyFlag: true);
        sandbox.WriteTool(
            "dotnet.cmd",
            "@echo off\r\n" +
            "if /I \"%~1\"==\"build\" type nul > \"%~dp0dirty.flag\"\r\n" +
            "exit /b 0\r\n");
        foreach (var command in new[] { "npm.cmd", "sqllocaldb.cmd", "func.cmd" })
        {
            sandbox.WriteTool(command, "@echo off\r\nexit /b 0\r\n");
        }

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Initialize-LocalDevelopment.ps1"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("requires a clean checkout before and after the build"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            sandbox.Root,
            "artifacts",
            "local-development",
            ".initialized.json")));
    }

    [Fact]
    public async Task LocalStartRejectsRuntimeArtifactChangedAfterInitialization()
    {
        using var sandbox = new ScriptSandbox();
        sandbox.CopyRepositoryScript("Invoke-LocalDevelopment.ps1");
        sandbox.WriteGitCommand(SourceRevision);
        sandbox.WriteFile("package-lock.json", "{\"lockfileVersion\":3}\n");
        sandbox.WriteFile("node_modules/azurite/dist/src/azurite.js", "module.exports = {};\n");
        var webAssembly = sandbox.WriteFile(
            "src/Pegasus.Web/bin/Debug/net10.0/Pegasus.Web.dll",
            "original-web");
        var workerAssembly = sandbox.WriteFile(
            "src/Pegasus.Worker/bin/Debug/net10.0/Pegasus.Worker.dll",
            "original-worker");
        sandbox.WriteJson(
            "artifacts/local-development/.initialized.json",
            CreateInitializationMarker(sandbox, webAssembly, workerAssembly));
        await File.AppendAllTextAsync(webAssembly, "-altered");

        var result = await sandbox.RunPowerShellAsync(
            "-File",
            sandbox.ScriptPath("Invoke-LocalDevelopment.ps1"),
            "-Action",
            "Start");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput("runtime artifact changed after the clean build"),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateDirectories(
            Path.Combine(sandbox.Root, "artifacts", "local-development")));
    }

    [Theory]
    [InlineData("failed-state", "successful exact-source")]
    [InlineData("wrong-source", "successful exact-source")]
    [InlineData("uninitialized-identity", "successful exact-source")]
    [InlineData("failed-readiness", "no successful current-attempt")]
    [InlineData("failed-smoke", "no successful current-attempt")]
    [InlineData("altered-binary", "differs from the initialized bytes")]
    public async Task AcceptanceRejectsInvalidLocalRunBeforeTests(
        string invalidity,
        string expectedMessage)
    {
        using var setup = CreateAcceptanceSetup();
        switch (invalidity)
        {
            case "failed-state":
                setup.LocalRun["state"] = "Failed";
                break;
            case "wrong-source":
                setup.LocalRun["sourceSha"] = OtherRevision;
                break;
            case "uninitialized-identity":
                setup.LocalRun["identity"]!["initializationCompleted"] = false;
                break;
            case "failed-readiness":
                setup.LocalRun["verification"]!["readiness"]!["result"] = "Failed";
                break;
            case "failed-smoke":
                setup.Smoke["result"] = "Failed";
                break;
            case "altered-binary":
                await File.AppendAllTextAsync(setup.WebAssemblyPath, "-altered");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidity));
        }
        setup.WriteLocalRun();

        var result = await setup.RunAcceptanceAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            NormalizePowerShellOutput(expectedMessage),
            result.NormalizedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(setup.Sandbox.ToolsDirectory, "dotnet-invocations.txt")));
        Assert.False(Directory.Exists(Path.Combine(
            setup.Sandbox.Root,
            "artifacts",
            "qdos-alpha-acceptance",
            setup.RunId)));
    }

    [Fact]
    public async Task AcceptanceAllowsExactSuccessfulRunEvidence()
    {
        using var setup = CreateAcceptanceSetup();
        setup.WriteLocalRun();

        var result = await setup.RunAcceptanceAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains(
            NormalizePowerShellOutput("offline candidate verification passed"),
            result.NormalizedOutput,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(
            setup.Sandbox.Root,
            "artifacts",
            "qdos-alpha-acceptance",
            setup.RunId,
            "evidence.json")));
        Assert.Equal(
            2,
            File.ReadAllLines(Path.Combine(setup.Sandbox.ToolsDirectory, "dotnet-invocations.txt")).Length);
    }

    private static AcceptanceSetup CreateAcceptanceSetup()
    {
        var sandbox = new ScriptSandbox();
        try
        {
            sandbox.CopyRepositoryScript("Invoke-QdosAlphaAcceptance.ps1");
            sandbox.WriteGitCommand(SourceRevision);
            sandbox.WriteTool(
                "dotnet.cmd",
                "@echo off\r\n" +
                "set \"TOOL_DIR=%~dp0\"\r\n" +
                "set \"RESULTS=\"\r\n" +
                "set \"LOG=qdos-pressure.trx\"\r\n" +
                ":parse\r\n" +
                "if \"%~1\"==\"\" goto done\r\n" +
                "if /I \"%~1\"==\"--results-directory\" (\r\n" +
                "  set \"RESULTS=%~2\"\r\n" +
                "  shift /1\r\n" +
                "  shift /1\r\n" +
                "  goto parse\r\n" +
                ")\r\n" +
                "if /I \"%~1\"==\"qdos-alpha-acceptance.trx\" set \"LOG=qdos-alpha-acceptance.trx\"\r\n" +
                "shift /1\r\n" +
                "goto parse\r\n" +
                ":done\r\n" +
                "if \"%RESULTS%\"==\"\" exit /b 7\r\n" +
                "if not exist \"%RESULTS%\" mkdir \"%RESULTS%\" || exit /b 8\r\n" +
                "> \"%RESULTS%\\%LOG%\" echo passed\r\n" +
                ">> \"%TOOL_DIR%dotnet-invocations.txt\" echo %LOG%\r\n" +
                "exit /b 0\r\n");
            sandbox.WriteFile(
                "tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj",
                "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
            sandbox.WriteFile("tests/Pegasus.PerformanceTests/CapacitySoakTests.cs", "// pressure\n");
            sandbox.WriteFile("tests/Pegasus.PerformanceTests/FailureInjectionTests.cs", "// failure\n");

            var runId = Guid.NewGuid().ToString("N");
            var datasetPath = sandbox.WriteFile("acceptance-input/capacity.json", "[]\n");
            var capacityManifestPath = sandbox.WriteJson(
                "acceptance-input/capacity-manifest.json",
                new
                {
                    schemaVersion = 1,
                    caseCount = 2000,
                    approvalReference = "operator-approved-test-evidence",
                    datasetPath = "capacity.json",
                    datasetSha256 = Sha256(datasetPath)
                });
            var callerManifestPath = sandbox.WriteJson(
                "acceptance-input/caller-manifest.json",
                new
                {
                    schemaVersion = 1,
                    kind = "Pegasus.QdosAlpha.AcceptanceEvidence",
                    sourceRevision = SourceRevision,
                    runId
                });
            var webAssemblyPath = sandbox.WriteFile(
                "src/Pegasus.Web/bin/Debug/net10.0/Pegasus.Web.dll",
                "exact-web");
            var workerAssemblyPath = sandbox.WriteFile(
                "src/Pegasus.Worker/bin/Debug/net10.0/Pegasus.Worker.dll",
                "exact-worker");

            var now = DateTimeOffset.UtcNow;
            var readiness = new JsonObject
            {
                ["result"] = "Passed",
                ["startAttempt"] = 1,
                ["observedUtc"] = now.AddMinutes(-2).ToString("O"),
                ["azuriteReady"] = true,
                ["webReady"] = true,
                ["functionsRunning"] = true
            };
            var smoke = new JsonObject
            {
                ["result"] = "Passed",
                ["startAttempt"] = 1,
                ["observedUtc"] = now.AddMinutes(-1).ToString("O"),
                ["sourceSha"] = SourceRevision,
                ["webReady"] = true,
                ["functionsRunning"] = true,
                ["identityInitialized"] = true,
                ["httpsOriginValidated"] = true,
                ["oauthMetadataValidated"] = true,
                ["administratorRouteValidated"] = true
            };
            var localRun = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kind"] = "Pegasus.LocalDevelopment.Run",
                ["runId"] = runId,
                ["state"] = "Running",
                ["startAttempt"] = 1,
                ["createdUtc"] = now.AddMinutes(-3).ToString("O"),
                ["updatedUtc"] = now.ToString("O"),
                ["sourceSha"] = SourceRevision,
                ["ownership"] = new JsonObject
                {
                    ["repositoryRoot"] = sandbox.Root,
                    ["runRoot"] = Path.Combine(
                        sandbox.Root,
                        "artifacts",
                        "local-development",
                        runId),
                    ["cloudOperations"] = "disabled"
                },
                ["runtime"] = new JsonObject
                {
                    ["profile"] = "DevelopmentOffline",
                    ["environment"] = "Development",
                    ["artifacts"] = new JsonObject
                    {
                        ["web"] = CreateRuntimeArtifactRecord(
                            "src/Pegasus.Web/bin/Debug/net10.0/Pegasus.Web.dll",
                            webAssemblyPath),
                        ["worker"] = CreateRuntimeArtifactRecord(
                            "src/Pegasus.Worker/bin/Debug/net10.0/Pegasus.Worker.dll",
                            workerAssemblyPath)
                    }
                },
                ["identity"] = new JsonObject
                {
                    ["initializationCompleted"] = true,
                    ["subjectId"] = "d47fbbae-ea22-4ca6-b983-01e2ed1fbd13",
                    ["userName"] = "development-offline-administrator",
                    ["role"] = "Administrator",
                    ["oauthClientId"] = "pegasus-development-mcp",
                    ["oauthCallback"] = "http://127.0.0.1:7890/callback"
                },
                ["verification"] = new JsonObject
                {
                    ["readiness"] = readiness,
                    ["smoke"] = smoke
                }
            };

            return new(
                sandbox,
                runId,
                capacityManifestPath,
                callerManifestPath,
                webAssemblyPath,
                localRun,
                smoke);
        }
        catch
        {
            sandbox.Dispose();
            throw;
        }
    }

    private static object CreateInitializationMarker(
        ScriptSandbox sandbox,
        string webAssembly,
        string workerAssembly)
    {
        return new
        {
            schemaVersion = 1,
            kind = "Pegasus.LocalDevelopment.Initialization",
            profile = "Offline",
            sdkVersion = "10.0.302",
            azuriteVersion = "3.36.0",
            functionsCoreToolsVersion = "4.12.1",
            packageLockSha256 = Sha256(Path.Combine(sandbox.Root, "package-lock.json")),
            sourceSha = SourceRevision,
            runtimeArtifacts = new
            {
                web = new
                {
                    relativePath = "src/Pegasus.Web/bin/Debug/net10.0/Pegasus.Web.dll",
                    byteLength = new FileInfo(webAssembly).Length,
                    sha256 = Sha256(webAssembly)
                },
                worker = new
                {
                    relativePath = "src/Pegasus.Worker/bin/Debug/net10.0/Pegasus.Worker.dll",
                    byteLength = new FileInfo(workerAssembly).Length,
                    sha256 = Sha256(workerAssembly)
                }
            }
        };
    }

    private static JsonObject CreateRuntimeArtifactRecord(string relativePath, string path)
    {
        return new()
        {
            ["relativePath"] = relativePath,
            ["byteLength"] = new FileInfo(path).Length,
            ["sha256"] = Sha256(path)
        };
    }

    private static object CreateArtifactRecord(
        string path,
        string family,
        string runtimeIdentifier,
        string deploymentKind)
    {
        return new
        {
            family,
            fileName = Path.GetFileName(path),
            runtimeIdentifier,
            deploymentKind,
            byteLength = new FileInfo(path).Length,
            sha256 = Sha256(path)
        };
    }

    private static object CreateInputRecord(
        ScriptSandbox sandbox,
        string relativePath,
        string purpose)
    {
        var path = Path.Combine(
            sandbox.Root,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return new
        {
            path = relativePath,
            purpose,
            byteLength = new FileInfo(path).Length,
            sha256 = Sha256(path)
        };
    }

    private static object CreateArchiveEntryRecord(
        string archivePath,
        string entryName)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = Assert.Single(
            archive.Entries,
            candidate => string.Equals(candidate.FullName, entryName, StringComparison.Ordinal));
        using var stream = entry.Open();
        return new
        {
            entryName,
            byteLength = entry.Length,
            sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()
        };
    }

    private static object CreateEmptyReleaseInputTreeRecord()
    {
        var header = Encoding.UTF8.GetBytes("Pegasus.ReleaseInputTree/v1\n");
        return new
        {
            schema = "tracked-path-mode-file-bytes-v1",
            algorithm = "sha256",
            sha256 = Convert.ToHexString(SHA256.HashData(header)).ToLowerInvariant(),
            includedPathCount = 0,
            excludedPathCounts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["docs/changes/2026-07-27-qdos-alpha-reference-corpora.md"] = 0,
                ["docs/reference/imp-docs/"] = 0,
                ["corpus/"] = 0,
                ["artifacts/"] = 0
            }
        };
    }

    private static void WriteReleaseInputFiles(ScriptSandbox sandbox)
    {
        sandbox.WriteFile(
            "Directory.Build.props",
            $"<Project><PropertyGroup><Version>{ReleaseVersion}</Version></PropertyGroup></Project>\n");
        sandbox.WriteFile(
            "global.json",
            "{\"sdk\":{\"version\":\"10.0.302\"}}\n");
        sandbox.WriteFile(
            "package.json",
            $"{{\"name\":\"pegasus\",\"version\":\"{ReleaseVersion}\"}}\n");
        sandbox.WriteFile(
            "package-lock.json",
            $"{{\"name\":\"pegasus\",\"version\":\"{ReleaseVersion}\",\"packages\":{{\"\":{{\"name\":\"pegasus\",\"version\":\"{ReleaseVersion}\"}}}}}}\n");
        sandbox.WriteFile(
            ".config/dotnet-tools.json",
            "{\"version\":1,\"isRoot\":true,\"tools\":{\"dotnet-ef\":{\"version\":\"10.0.10\",\"commands\":[\"dotnet-ef\"]}}}\n");
        sandbox.WriteFile("Pegasus.slnx", "<Solution />\n");
        foreach (var relativePath in ProvenanceInputPaths)
        {
            if (File.Exists(Path.Combine(
                    sandbox.Root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                continue;
            }

            sandbox.WriteFile(
                relativePath,
                relativePath.EndsWith(".csproj", StringComparison.Ordinal)
                    ? "<Project />\n"
                    : "{}\n");
        }

        sandbox.WriteFile(
            "azure.yaml",
            $"name: pegasus\nmetadata:\n  template: pegasus@{ReleaseVersion}\n");
        sandbox.WriteFile(
            "infra/main.bicep",
            "param deploymentMode string = 'offline-replay'\n" +
            "var activationAllowed = deploymentMode == 'approved-live-deployment'\n" +
            "resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = if (activationAllowed) {\n" +
            "  name: 'blocked'\n" +
            "  location: 'uksouth'\n" +
            "}\n");
        sandbox.WriteJson(
            "infra/main.parameters.json",
            new
            {
                parameters = new
                {
                    deploymentMode = new
                    {
                        value = "offline-replay"
                    }
                }
            });
        sandbox.WriteFile("infra/modules/platform.bicep", "// packaged platform fixture\n");
    }

    private static Dictionary<string, string> CreateWorkerAzureSettings() =>
        new(StringComparer.Ordinal)
        {
            ["AzureIdentity__WorkerClientId"] = "[reference('workerIdentity').clientId]",
            ["AzureWebJobsStorage__clientId"] = "[reference('workerIdentity').clientId]",
            ["AzureWebJobsStorage__credential"] = "managedidentity",
            ["IntakeStorage__ServiceUri"] = "[reference('storage').primaryEndpoints.blob]",
            ["IntakeQueue__ServiceUri"] = "[reference('storage').primaryEndpoints.queue]",
            ["ExternalWorkQueue__ServiceUri"] = "[reference('storage').primaryEndpoints.queue]"
        };

    private static ScriptSandbox CreateDeploymentValidationSandbox(
        IReadOnlyDictionary<string, string> disabledSettings,
        out string artifactDirectory,
        string? unsafeWorkerEntry = null,
        IReadOnlyDictionary<string, string>? workerAzureSettings = null)
    {
        workerAzureSettings ??= CreateWorkerAzureSettings();
        var sandbox = new ScriptSandbox();
        sandbox.CopyRepositoryScript("Test-AzureDeploymentPlan.ps1");
        WriteReleaseInputFiles(sandbox);
        sandbox.WriteFile(".azure/deployment-plan.md", "# Test plan\n");
        sandbox.WriteGitCommand(SourceRevision);
        sandbox.WriteJson(
            "tools/compiled-template.json",
            new
            {
                parameters = new
                {
                    deploymentMode = new
                    {
                        allowedValues = OfflineReplayMode
                    }
                },
                variables = new
                {
                    activationAllowed = "[equals(parameters('deploymentMode'), 'approved-live-deployment')]"
                },
                resources = new object[]
                {
                    new
                    {
                        type = "Microsoft.Resources/resourceGroups",
                        condition = "[variables('activationAllowed')]"
                    },
                    new
                    {
                        type = "Microsoft.Resources/deployments",
                        properties = new
                        {
                            template = new
                            {
                                resources = new object[]
                                {
                                    new
                                    {
                                        type = "Microsoft.Web/sites",
                                        kind = "functionapp,linux",
                                        properties = new
                                        {
                                            siteConfig = new
                                            {
                                                appSettings = disabledSettings
                                                    .Select(setting => new
                                                    {
                                                        name = $"AzureWebJobs.{setting.Key}.Disabled",
                                                        value = setting.Value
                                                    })
                                                    .Concat(workerAzureSettings.Select(setting => new
                                                    {
                                                        name = setting.Key,
                                                        value = setting.Value
                                                    }))
                                                    .ToArray()
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        sandbox.WriteTool(
            "bicep.cmd",
            "@echo off\r\n" +
            "set \"TOOL_DIR=%~dp0\"\r\n" +
            "echo %* > \"%~dp0bicep-arguments.txt\"\r\n" +
            "set \"OUT=\"\r\n" +
            ":parse\r\n" +
            "if \"%~1\"==\"\" goto done\r\n" +
            "if /I \"%~1\"==\"--outfile\" (\r\n" +
            "  set \"OUT=%~2\"\r\n" +
            "  shift /1\r\n" +
            "  shift /1\r\n" +
            "  goto parse\r\n" +
            ")\r\n" +
            "shift /1\r\n" +
            "goto parse\r\n" +
            ":done\r\n" +
            "if \"%OUT%\"==\"\" exit /b 7\r\n" +
            "copy /Y \"%TOOL_DIR%compiled-template.json\" \"%OUT%\" >nul || exit /b 8\r\n" +
            "if not exist \"%OUT%\" exit /b 9\r\n" +
            "exit /b 0\r\n");
        sandbox.WriteTool(
            "dotnet.cmd",
            "@echo off\r\ntype nul > \"%~dp0dotnet-invoked.txt\"\r\nexit /b 99\r\n");

        artifactDirectory = sandbox.CreateDirectory("artifacts/release");
        var webArchive = Path.Combine(artifactDirectory, "web-linux-x64.zip");
        var workerArchive = Path.Combine(artifactDirectory, "worker-linux-x64.zip");
        var migrationArchive = Path.Combine(
            artifactDirectory,
            "migration-bundle-win-x64.zip");
        var bootstrapArchive = Path.Combine(artifactDirectory, "bootstrap-win-x64.zip");
        var deploymentArchive = Path.Combine(
            artifactDirectory,
            "azure-deployment-inputs.zip");
        CreateArchive(
            webArchive,
            "Pegasus.Web.dll",
            "Pegasus.Web.deps.json",
            "Pegasus.Web.runtimeconfig.json",
            "appsettings.json");
        CreateArchive(
            workerArchive,
            "host.json",
            "Pegasus.Worker.dll",
            "Pegasus.Worker.deps.json",
            "Pegasus.Worker.runtimeconfig.json");
        WriteArchiveEntry(
            workerArchive,
            "functions.metadata",
            JsonSerializer.Serialize(
                ExpectedWorkerFunctions.Select(name => new { name })));
        if (unsafeWorkerEntry is not null)
        {
            WriteArchiveEntry(workerArchive, unsafeWorkerEntry, "unsafe");
        }
        CreateArchive(migrationArchive, "Pegasus.Migrations.exe");
        CreateArchive(bootstrapArchive, "Pegasus.Bootstrap.exe");
        var bootstrapManifest = new
        {
            schemaVersion = 1,
            productVersion = ReleaseVersion,
            sourceRevision = SourceRevision,
            expectedMigrationId = "20260729199000_RuntimeRoleReconciliation",
            targetIdentity = "sqlserver://pegasus.database.windows.net/Pegasus",
            sqlServer = "pegasus.database.windows.net",
            sqlDatabase = "Pegasus",
            issuer = "https://pegasus.example",
            administrators = new[]
            {
                new { manifestIdentity = "andrew", userName = "andrew@example.test" },
                new { manifestIdentity = "alex", userName = "alex@example.test" }
            },
            publicMcpClient = new
            {
                clientId = "00000000-0000-0000-0000-000000000001",
                displayName = "Pegasus MCP",
                redirectUris = new[] { "http://127.0.0.1:7890/callback" },
                resource = "https://pegasus.example/mcp",
                scopes = new[] { "pegasus.read", "pegasus.write" }
            }
        };
        WriteArchiveEntry(
            bootstrapArchive,
            "bootstrap-manifest.json",
            JsonSerializer.Serialize(bootstrapManifest));
        var bootstrapManifestRecord = CreateArchiveEntryRecord(
            bootstrapArchive,
            "bootstrap-manifest.json");
        CreateArchiveFromFiles(
            deploymentArchive,
            sandbox.Root,
            DeploymentInputPaths);

        var inputRecords = ProvenanceInputPaths
            .Select(path => CreateInputRecord(sandbox, path, "provenance"))
            .Concat(DeploymentInputPaths.Select(
                path => CreateInputRecord(sandbox, path, "deployment")))
            .ToArray();
        var manifestPath = sandbox.WriteJson(
            "artifacts/release/release-manifest.json",
            new
            {
                schemaVersion = 1,
                releaseMode = "offline-replay",
                releaseVersion = ReleaseVersion,
                sourceRevision = SourceRevision,
                releaseInputTree = CreateEmptyReleaseInputTreeRecord(),
                configuration = "Release",
                webDiagnostic = new
                {
                    schemaVersion = 1,
                    version = ReleaseVersion,
                    sourceSha = SourceRevision
                },
                bootstrapManifest = bootstrapManifestRecord,
                toolchain = new
                {
                    dotnetSdk = "10.0.302",
                    dotnetEf = "10.0.10",
                    restore = "locked-offline"
                },
                runtimes = new
                {
                    web = "linux-x64-framework-dependent",
                    worker = "linux-x64-framework-dependent",
                    migration = "win-x64-self-contained",
                    bootstrap = "win-x64-self-contained",
                    azureDeploymentInputs = "bicep-parameters"
                },
                inputs = inputRecords,
                artifacts = new[]
                {
                    CreateArtifactRecord(
                        webArchive,
                        "web",
                        "linux-x64",
                        "framework-dependent"),
                    CreateArtifactRecord(
                        workerArchive,
                        "worker",
                        "linux-x64",
                        "framework-dependent"),
                    CreateArtifactRecord(
                        migrationArchive,
                        "migration",
                        "win-x64",
                        "self-contained-ef-bundle"),
                    CreateArtifactRecord(
                        bootstrapArchive,
                        "bootstrap",
                        "win-x64",
                        "self-contained-one-shot"),
                    CreateArtifactRecord(
                        deploymentArchive,
                        "azure-deployment-inputs",
                        "none",
                        "bicep-parameters")
                }
            });
        sandbox.WriteFile(
            "artifacts/release/release-manifest.sha256",
            $"{Sha256(manifestPath)}  release-manifest.json\n");
        return sandbox;
    }

    private static void CreateArchive(string path, params string[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(name);
        }
    }

    private static void CreateArchiveFromFiles(
        string archivePath,
        string sourceRoot,
        IEnumerable<string> relativePaths)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var relativePath in relativePaths)
        {
            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(
                1980,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);
            using var source = File.OpenRead(Path.Combine(
                sourceRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            using var destination = entry.Open();
            source.CopyTo(destination);
        }
    }
    private static void WriteArchiveEntry(string path, string entryName, string content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }


    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string NormalizePowerShellOutput(string output)
    {
        var plainText = AnsiEscapePattern.Replace(output, string.Empty);
        var withoutErrorGutters = PowerShellErrorGutterPattern.Replace(plainText, string.Empty);
        return new string(withoutErrorGutters
            .Where(character => !char.IsWhiteSpace(character))
            .ToArray());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("The Pegasus repository root was not found.");
    }

    private sealed class AcceptanceSetup(
        ScriptSandbox sandbox,
        string runId,
        string capacityManifestPath,
        string callerManifestPath,
        string webAssemblyPath,
        JsonObject localRun,
        JsonObject smoke) : IDisposable
    {
        public ScriptSandbox Sandbox { get; } = sandbox;
        public string RunId { get; } = runId;
        public string WebAssemblyPath { get; } = webAssemblyPath;
        public JsonObject LocalRun { get; } = localRun;
        public JsonObject Smoke { get; } = smoke;

        private string CapacityManifestPath { get; } = capacityManifestPath;
        private string CallerManifestPath { get; } = callerManifestPath;
        private string LocalRunPath => Path.Combine(
            Sandbox.Root,
            "artifacts",
            "local-development",
            RunId,
            "run-manifest.json");

        public void WriteLocalRun()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LocalRunPath)!);
            File.WriteAllText(
                LocalRunPath,
                LocalRun.ToJsonString(IndentedJsonOptions));
        }

        public Task<PowerShellResult> RunAcceptanceAsync()
        {
            return Sandbox.RunPowerShellAsync(
                "-File",
                Sandbox.ScriptPath("Invoke-QdosAlphaAcceptance.ps1"),
                "-Profile",
                "OfflineCandidate",
                "-SourceRevision",
                SourceRevision,
                "-RunId",
                RunId,
                "-CapacityDatasetManifest",
                CapacityManifestPath,
                "-CallerEvidenceManifest",
                CallerManifestPath,
                "-LocalRunManifest",
                LocalRunPath);
        }

        public void Dispose()
        {
            Sandbox.Dispose();
        }
    }

    private sealed class ScriptSandbox : IDisposable
    {
        public ScriptSandbox()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pegasus-release-evidence-{Guid.NewGuid():N}");
            ToolsDirectory = CreateDirectory("tools");
            CreateDirectory("scripts");
        }

        public string Root { get; }
        public string ToolsDirectory { get; }

        public string ScriptPath(string name) => Path.Combine(Root, "scripts", name);

        public void CopyRepositoryScript(string name)
        {
            File.Copy(Path.Combine(RepositoryRoot, "scripts", name), ScriptPath(name));
        }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            return path;
        }

        public string WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }

        public string WriteJson(string relativePath, object value)
        {
            var canonicalJson = JsonSerializer
                .Serialize(value, IndentedJsonOptions)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            return WriteFile(relativePath, canonicalJson);
        }

        public void WriteTool(string name, string content)
        {
            WriteFile($"tools/{name}", content);
        }

        public void WriteGitCommand(string revision, bool reportDirtyFlag = false)
        {
            var dirtyOutput = reportDirtyFlag
                ? "  if exist \"%TOOL_DIR%dirty.flag\" echo ?? generated.cs\r\n"
                : string.Empty;
            WriteTool(
                "git.cmd",
                "@echo off\r\n" +
                "set \"TOOL_DIR=%~dp0\"\r\n" +
                ":dispatch\r\n" +
                "if \"%~1\"==\"\" exit /b 9\r\n" +
                "if /I \"%~1\"==\"rev-parse\" goto revision\r\n" +
                "if /I \"%~1\"==\"ls-tree\" exit /b 0\r\n" +
                "if /I \"%~1\"==\"status\" goto status\r\n" +
                "shift /1\r\n" +
                "goto dispatch\r\n" +
                ":revision\r\n" +
                $"echo {revision}\r\n" +
                "exit /b 0\r\n" +
                ":status\r\n" +
                dirtyOutput +
                "exit /b 0\r\n");
        }

        public async Task<PowerShellResult> RunPowerShellAsync(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    WorkingDirectory = Root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.Environment["PATH"] =
                ToolsDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            process.StartInfo.Environment["COLUMNS"] = "1000";
            process.StartInfo.Environment.Remove("PEGASUS_QDOS_PRESSURE_PROFILE");
            process.StartInfo.Environment.Remove("PEGASUS_QDOS_ACCEPTANCE_MANIFEST");
            process.StartInfo.Environment.Remove("PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION");
            process.StartInfo.Environment["NO_COLOR"] = "1";
            process.StartInfo.Environment["TERM"] = "dumb";
            process.StartInfo.Environment["POWERSHELL_TELEMETRY_OPTOUT"] = "1";
            process.StartInfo.ArgumentList.Add("-NoLogo");
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            return new(
                process.ExitCode,
                await standardOutput,
                await standardError);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed record PowerShellResult(
        int ExitCode,
        string StandardOutput,
        string StandardError)
    {
        public string CombinedOutput => StandardOutput + Environment.NewLine + StandardError;
        public string NormalizedOutput => NormalizePowerShellOutput(CombinedOutput);
    }
}
