using System.Diagnostics;
using System.Text.Json;

namespace Pegasus.IntegrationTests;

public sealed class LocalDevelopmentScriptTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string LifecycleScript = Path.Combine(
        RepositoryRoot,
        "scripts",
        "Invoke-LocalDevelopment.ps1");

    [Fact]
    public async Task PublishedLifecycleActionsAndDoctorProfilesAreBound()
    {
        var doctorScript = Path.Combine(RepositoryRoot, "scripts", "Invoke-Doctor.ps1");
        var lifecycleLiteral = LifecycleScript.Replace("'", "''", StringComparison.Ordinal);
        var doctorLiteral = doctorScript.Replace("'", "''", StringComparison.Ordinal);
        var command = "& { " +
            $"$lifecycle = Get-Command -CommandType ExternalScript -Name '{lifecycleLiteral}'; " +
            $"$doctor = Get-Command -CommandType ExternalScript -Name '{doctorLiteral}'; " +
            "$actions = $lifecycle.Parameters['Action'].Attributes | " +
            "Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] } | " +
            "Select-Object -ExpandProperty ValidValues; " +
            "$profiles = $doctor.Parameters['Profile'].Attributes | " +
            "Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] } | " +
            "Select-Object -ExpandProperty ValidValues; " +
            "$expectedActions = @('Start','Status','Smoke','Stop','Reset'); " +
            "$expectedProfiles = @('Offline','Cloud'); " +
            "if ((Compare-Object $actions $expectedActions) -or " +
            "(Compare-Object $profiles $expectedProfiles)) { exit 7 } " +
            "}";

        var result = await RunPowerShellAsync("-Command", command);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError.Trim());
    }

    [Fact]
    public async Task StatusWithNoOwnedRunIsReadOnlyAndSuccessful()
    {
        var result = await RunPowerShellAsync(
            "-File",
            LifecycleScript,
            "-Action",
            "Status");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("ParameterBinding", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetRefusesManifestWhoseRunRootEscapesItsOwnedDirectory()
    {
        var runId = Guid.NewGuid().ToString("N");
        var localRoot = Path.Combine(RepositoryRoot, "artifacts", "local-development");
        var runRoot = Path.Combine(localRoot, runId);
        var sentinelRoot = Path.Combine(
            RepositoryRoot,
            "artifacts",
            "local-development-reset-sentinel",
            runId);
        var sentinelPath = Path.Combine(sentinelRoot, "must-remain.txt");
        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(sentinelRoot);
        await File.WriteAllTextAsync(sentinelPath, "owned only by this test");

        try
        {
            var manifest = CreateManifest(runId, runRoot, sentinelRoot);
            await File.WriteAllTextAsync(
                Path.Combine(runRoot, "run-manifest.json"),
                JsonSerializer.Serialize(manifest));

            var result = await RunPowerShellAsync(
                "-File",
                LifecycleScript,
                "-Action",
                "Reset",
                "-RunId",
                runId);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Run root does not match", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(sentinelPath));
            Assert.True(Directory.Exists(runRoot));
        }
        finally
        {
            if (Directory.Exists(runRoot))
            {
                Directory.Delete(runRoot, recursive: true);
            }
            if (Directory.Exists(sentinelRoot))
            {
                Directory.Delete(sentinelRoot, recursive: true);
            }
        }
    }

    private static object CreateManifest(string runId, string runRoot, string escapedRunRoot)
    {
        var state = Path.Combine(runRoot, "state");
        var logs = Path.Combine(runRoot, "logs");
        var azurite = Path.Combine(runRoot, "azurite");
        var intake = Path.Combine(runRoot, "intake");
        var mailbox = Path.Combine(runRoot, "mailbox");
        var caseFiles = Path.Combine(runRoot, "case-files");
        const int webPort = 45001;
        const int functionsPort = 45002;
        const int blobPort = 45003;
        const int queuePort = 45004;
        const int tablePort = 45005;
        var webBase = $"https://localhost:{webPort}";
        var databaseName = $"PegasusDevelopment_{runId}";

        return new
        {
            schemaVersion = 1,
            kind = "Pegasus.LocalDevelopment.Run",
            runId,
            state = "Stopped",
            startAttempt = 1,
            createdUtc = DateTimeOffset.UtcNow.ToString("O"),
            updatedUtc = DateTimeOffset.UtcNow.ToString("O"),
            sourceSha = new string('a', 40),
            ownership = new
            {
                repositoryRoot = RepositoryRoot,
                runRoot = escapedRunRoot,
                cloudOperations = "disabled"
            },
            runtime = new
            {
                profile = "DevelopmentOffline",
                environment = "Development"
            },
            identity = new
            {
                initializationCompleted = true,
                subjectId = "d47fbbae-ea22-4ca6-b983-01e2ed1fbd13",
                userName = "development-offline-administrator",
                role = "Administrator",
                oauthClientId = "pegasus-development-mcp",
                oauthCallback = "http://127.0.0.1:7890/callback",
                issuer = $"{webBase}/",
                resource = $"{webBase}/mcp"
            },
            resources = new
            {
                database = new
                {
                    provider = "SqlServer",
                    instanceName = databaseName,
                    databaseName,
                    created = false
                },
                ports = new
                {
                    webHttps = webPort,
                    functions = functionsPort,
                    azuriteBlob = blobPort,
                    azuriteQueue = queuePort,
                    azuriteTable = tablePort
                },
                paths = new
                {
                    state,
                    logs,
                    azurite,
                    intake,
                    mailbox,
                    mailboxInbox = Path.Combine(mailbox, "inbox"),
                    mailboxSent = Path.Combine(mailbox, "sent"),
                    caseFiles
                }
            },
            endpoints = new
            {
                webBase,
                webLive = $"{webBase}/health/live",
                webReady = $"{webBase}/health/ready",
                webVersion = $"{webBase}/diagnostics/version",
                functionsStatus = $"http://127.0.0.1:{functionsPort}/admin/host/status",
                azuriteBlob = $"http://127.0.0.1:{blobPort}/devstoreaccount1",
                azuriteQueue = $"http://127.0.0.1:{queuePort}/devstoreaccount1",
                azuriteTable = $"http://127.0.0.1:{tablePort}/devstoreaccount1"
            },
            processes = new
            {
                azurite = (object?)null,
                web = (object?)null,
                worker = (object?)null
            },
            failure = (object?)null
        };
    }

    private static async Task<PowerShellResult> RunPowerShellAsync(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                WorkingDirectory = RepositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
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

    private sealed record PowerShellResult(
        int ExitCode,
        string StandardOutput,
        string StandardError)
    {
        public string CombinedOutput => StandardOutput + Environment.NewLine + StandardError;
    }
}
