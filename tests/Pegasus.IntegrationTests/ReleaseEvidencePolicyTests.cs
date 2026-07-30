namespace Pegasus.IntegrationTests;

public sealed class ReleaseEvidencePolicyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ReleaseBuilderUsesOnlyPinnedRuntimeSpecificDeploymentCommands()
    {
        var script = ReadScript("Build-ReleaseArtifacts.ps1");

        var webPublish = CommandWindow(
            script,
            "Web linux-x64 framework-dependent publish",
            "Assert-CleanRepositoryRevision");
        AssertContainsAll(
            webPublish,
            "src/Pegasus.Web/Pegasus.Web.csproj",
            "'-r', $ApplicationRuntime",
            "'--self-contained', 'false'",
            "'--no-restore'");

        var workerPublish = CommandWindow(
            script,
            "Worker linux-x64 framework-dependent publish",
            "Assert-CleanRepositoryRevision");
        AssertContainsAll(
            workerPublish,
            "src/Pegasus.Worker/Pegasus.Worker.csproj",
            "'-r', $ApplicationRuntime",
            "'--self-contained', 'false'",
            "'--no-restore'");

        var migrationBundle = CommandWindow(
            script,
            "Self-contained win-x64 EF migrations bundle generation",
            "Assert-CleanRepositoryRevision");
        AssertContainsAll(
            migrationBundle,
            "'migrations'",
            "'bundle'",
            "'--self-contained'",
            "'-r', $MigrationRuntime",
            "'--output', $migrationExecutable");
        Assert.Contains("Pegasus.Migrations.exe", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'script'", migrationBundle, StringComparison.Ordinal);
        Assert.DoesNotContain("'--idempotent'", migrationBundle, StringComparison.Ordinal);

        var bootstrapPublish = CommandWindow(
            script,
            "Bootstrap win-x64 self-contained publish",
            "Assert-CleanRepositoryRevision");
        AssertContainsAll(
            bootstrapPublish,
            "src/Pegasus.Bootstrap/Pegasus.Bootstrap.csproj",
            "'-r', $BootstrapRuntime",
            "'--self-contained', 'true'");
        AssertContainsAll(
            script,
            "BootstrapManifestPath",
            "bootstrap-manifest.json",
            "must not publish a generic bootstrap-manifest.json",
            "Assert-ApprovedBootstrapManifestContract");
    }

    [Fact]
    public void ReleaseBuilderEmitsOnlyImmutableNamedFamiliesAndCanonicalDigests()
    {
        var script = ReadScript("Build-ReleaseArtifacts.ps1");

        AssertContainsAll(
            script,
            "web-linux-x64.zip",
            "worker-linux-x64.zip",
            "migration-bundle-win-x64.zip",
            "bootstrap-win-x64.zip",
            "azure-deployment-inputs.zip",
            "release-manifest.json",
            "release-manifest.sha256",
            "Assert-ExactArtifactDirectory",
            "Assert-ReproducibleCandidates",
            "[System.IO.Directory]::Move");
        Assert.DoesNotContain("'web.zip'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'worker.zip'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'migration.zip'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("portable-net10.0", script, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotent-sql", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseBuilderRetainsCleanExactSourceAndNoReuseGates()
    {
        var script = ReadScript("Build-ReleaseArtifacts.ps1");

        AssertContainsAll(
            script,
            "rev-parse --verify 'HEAD^{commit}'",
            "status --porcelain=v1 --untracked-files=all",
            "Assert-CleanRepositoryRevision",
            "checked-out source revision changed during release packaging",
            "Release output path already exists",
            "Release output path appeared during packaging",
            "-VerifyReproducible",
            "SourceRevisionId",
            "Get-ReleaseInputTreeRecord",
            "tracked-path-mode-file-bytes-v1",
            "--diagnostics-version");
    }

    [Fact]
    public void DeploymentValidatorConsumesPackagedInputsWithoutApplicationRebuild()
    {
        var script = ReadScript("Test-AzureDeploymentPlan.ps1");

        AssertContainsAll(
            script,
            "azure-deployment-inputs.zip",
            "Expand-ValidatedDeploymentInputs",
            "$packagedBicepPath",
            "build $packagedBicepPath --no-restore",
            "release-manifest.sha256",
            "Assert-ReleaseInputTree",
            "tracked-path-mode-file-bytes-v1",
            "Assert-SafeArchiveEntryName",
            "Assert-BootstrapManifestContract",
            "Bootstrap manifest hash differs from the release manifest",
            "Release output contains a raw directory");
        Assert.DoesNotContain("dotnet publish", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet build", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet ef", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("azd package", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("infra/main.bicep --no-restore", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", name));

    private static string CommandWindow(
        string script,
        string operation,
        string endMarker)
    {
        var start = script.IndexOf(operation, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Operation marker was not found: {operation}");
        var end = script.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Operation terminator was not found after: {operation}");
        return script[start..end];
    }

    private static void AssertContainsAll(string source, params string[] expectedTokens)
    {
        foreach (var token in expectedTokens)
        {
            Assert.Contains(token, source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("The Pegasus repository root was not found.");
    }
}
