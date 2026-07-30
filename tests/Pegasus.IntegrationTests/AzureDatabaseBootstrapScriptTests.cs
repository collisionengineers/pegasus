using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class AzureDatabaseBootstrapScriptTests
{
    private const string WebClientId = "00112233-4455-6677-8899-aabbccddeeff";
    private const string WorkerClientId = "10213243-5465-7687-98a9-bacbdcedfe0f";
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public async Task ApprovedBootstrapUsesFixedUsersRolesAndExactClientIdSids()
    {
        using var sandbox = new BootstrapSandbox();

        var result = await sandbox.RunAsync(CreateValidArguments());

        Assert.Equal(0, result.ExitCode);
        var sql = File.ReadAllText(sandbox.SqlCapturePath);
        Assert.Contains("CREATE USER [pegasus_web_runtime] WITH SID = 0x33221100554477668899AABBCCDDEEFF, TYPE = E;", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE USER [pegasus_worker_runtime] WITH SID = 0x433221106554877698A9BACBDCEDFE0F, TYPE = E;", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER ROLE [pegasus_web_runtime_role] ADD MEMBER [pegasus_web_runtime];", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER ROLE [pegasus_worker_runtime_role] ADD MEMBER [pegasus_worker_runtime];", sql, StringComparison.Ordinal);
        Assert.Contains("authentication_type = 4", sql, StringComparison.Ordinal);
        Assert.Contains("Runtime database users must not have direct permissions.", sql, StringComparison.Ordinal);
        Assert.Contains("Runtime roles must not be nested in broader database roles.", sql, StringComparison.Ordinal);
        Assert.Contains("[state] NOT IN ('G', 'D')", sql, StringComparison.Ordinal);
        Assert.Contains(
            "[state] = 'D' AND permission_name <> N'DELETE'",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("db_datareader", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_datawriter", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_owner", sql, StringComparison.OrdinalIgnoreCase);

        var arguments = File.ReadAllText(sandbox.ArgumentCapturePath);
        Assert.Contains("tcp:pegasus-test.database.windows.net,1433", arguments, StringComparison.Ordinal);
        Assert.Contains("--authentication-method ActiveDirectoryDefault", arguments, StringComparison.Ordinal);
        Assert.Contains(" -N ", $" {arguments} ", StringComparison.Ordinal);
        Assert.Contains(" -b ", $" {arguments} ", StringComparison.Ordinal);
        Assert.Contains(" -i ", $" {arguments} ", StringComparison.Ordinal);
        Assert.Contains("Approval reference: APPROVAL-review-24", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Evidence reference: EVIDENCE-review-24", result.StandardOutput, StringComparison.Ordinal);
    }
    [Fact]
    public async Task RuntimeRolePermissionGuardAllowsMigrationManagedDeleteDenials()
    {
        using var sandbox = new BootstrapSandbox();
        var result = await sandbox.RunAsync(CreateValidArguments());
        Assert.Equal(0, result.ExitCode);
        var guard = ExtractRuntimeRolePermissionGuard(
            File.ReadAllText(sandbox.SqlCapturePath));
        await using var database = await LocalDbTestDatabase.CreateAsync();

        await database.ExecuteAsync(guard);
    }

    [Theory]
    [InlineData(
        "DENY SELECT ON OBJECT::[dbo].[ApplicationInitializations] TO [pegasus_web_runtime_role];")]
    [InlineData(
        "DENY UPDATE ON OBJECT::[dbo].[ApplicationInitializations] TO [pegasus_web_runtime_role];")]
    [InlineData(
        "GRANT CONTROL ON OBJECT::[dbo].[ApplicationInitializations] TO [pegasus_web_runtime_role];")]
    public async Task RuntimeRolePermissionGuardRejectsOtherDenialsAndExtraPermissions(
        string invalidPermission)
    {
        using var sandbox = new BootstrapSandbox();
        var result = await sandbox.RunAsync(CreateValidArguments());
        Assert.Equal(0, result.ExitCode);
        var guard = ExtractRuntimeRolePermissionGuard(
            File.ReadAllText(sandbox.SqlCapturePath));
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.ExecuteAsync(invalidPermission);

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => database.ExecuteAsync(guard));

        Assert.Contains(
            "object-level DML grants",
            exception.Message,
            StringComparison.Ordinal);
    }


    [Fact]
    public async Task BootstrapRejectsCallerSelectedRoleBeforeSqlExecution()
    {
        using var sandbox = new BootstrapSandbox();
        var arguments = CreateValidArguments().Concat(["-WebRoleName", "db_owner"]).ToArray();

        var result = await sandbox.RunAsync(arguments);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(File.Exists(sandbox.SqlCapturePath));
    }

    [Fact]
    public async Task BootstrapRejectsSharedIdentityBeforeSqlExecution()
    {
        using var sandbox = new BootstrapSandbox();
        var arguments = CreateValidArguments();
        arguments[Array.IndexOf(arguments, "-WorkerClientId") + 1] = WebClientId;

        var result = await sandbox.RunAsync(arguments);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("must be distinct", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(sandbox.SqlCapturePath));
    }

    [Fact]
    public async Task BootstrapRejectsNonLiveDeploymentModeBeforeSqlExecution()
    {
        using var sandbox = new BootstrapSandbox();
        var arguments = CreateValidArguments();
        arguments[Array.IndexOf(arguments, "-DeploymentMode") + 1] = "offline-replay";

        var result = await sandbox.RunAsync(arguments);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("blocked for deployment mode", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(sandbox.SqlCapturePath));
    }

    private static string[] CreateValidArguments() =>
    [
        "-Server", "pegasus-test.database.windows.net",
        "-Database", "PegasusTest",
        "-WebClientId", WebClientId,
        "-WorkerClientId", WorkerClientId,
        "-ApprovalReference", "APPROVAL-review-24",
        "-EvidenceReference", "EVIDENCE-review-24",
        "-DeploymentMode", "approved-live-deployment",
        "-ApprovedOperation"
    ];

    private static string ExtractRuntimeRolePermissionGuard(string sql)
    {
        var normalized = sql.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string StartMarker = """
            IF EXISTS (
                SELECT 1
                FROM sys.database_permissions
                WHERE grantee_principal_id IN (@webRoleId, @workerRoleId)
            """;
        const string EndMarker =
            "THROW 51000, N'Runtime roles may contain only migration-managed object-level DML grants.', 1;";
        var start = normalized.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidDataException(
                "The generated bootstrap SQL is missing its runtime-role permission guard.");
        }
        var end = normalized.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidDataException(
                "The generated bootstrap SQL is missing its runtime-role permission guard.");
        }

        return
            $"""
            DECLARE @webRoleId int = DATABASE_PRINCIPAL_ID(N'{WebRole}');
            DECLARE @workerRoleId int = DATABASE_PRINCIPAL_ID(N'{WorkerRole}');
            {normalized[start..(end + EndMarker.Length)]}
            """;
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

    private sealed class BootstrapSandbox : IDisposable
    {
        public BootstrapSandbox()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pegasus-sql-bootstrap-{Guid.NewGuid():N}");
            ToolsDirectory = Path.Combine(Root, "tools");
            Directory.CreateDirectory(ToolsDirectory);
            Directory.CreateDirectory(Path.Combine(Root, "scripts"));
            File.Copy(
                Path.Combine(RepositoryRoot, "scripts", "Invoke-AzureDatabaseBootstrap.ps1"),
                Path.Combine(Root, "scripts", "Invoke-AzureDatabaseBootstrap.ps1"));
            File.WriteAllText(
                Path.Combine(ToolsDirectory, "sqlcmd.cmd"),
                """
                @echo off
                if /I "%~1"=="--version" (
                  echo sqlcmd test double 1.10.0
                  exit /b 0
                )
                > "%SQLCMD_CAPTURE%.args" echo %*
                :parse
                if "%~1"=="" goto done
                if /I "%~1"=="-i" (
                  if "%~2"=="" exit /b 8
                  copy /y "%~2" "%SQLCMD_CAPTURE%.sql" > nul
                  if errorlevel 1 exit /b 9
                  shift /1
                )
                shift /1
                goto parse
                :done
                exit /b 0
                """.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\n", "\r\n", StringComparison.Ordinal),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public string Root { get; }
        public string ToolsDirectory { get; }
        public string CaptureBasePath => Path.Combine(Root, "sqlcmd-capture");
        public string SqlCapturePath => CaptureBasePath + ".sql";
        public string ArgumentCapturePath => CaptureBasePath + ".args";

        public async Task<PowerShellResult> RunAsync(params string[] scriptArguments)
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
            process.StartInfo.Environment["SQLCMD_CAPTURE"] = CaptureBasePath;
            process.StartInfo.Environment["COLUMNS"] = "1000";
            process.StartInfo.Environment["NO_COLOR"] = "1";
            process.StartInfo.Environment["TERM"] = "dumb";
            process.StartInfo.ArgumentList.Add("-NoLogo");
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-File");
            process.StartInfo.ArgumentList.Add(Path.Combine(Root, "scripts", "Invoke-AzureDatabaseBootstrap.ps1"));
            foreach (var argument in scriptArguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            return new(process.ExitCode, await standardOutput, await standardError);
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
    }
}
