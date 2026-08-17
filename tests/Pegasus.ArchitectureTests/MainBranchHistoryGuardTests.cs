using System.Diagnostics;

namespace Pegasus.ArchitectureTests;

public sealed class MainBranchHistoryGuardTests : IDisposable
{
    private readonly string _repositoryRoot = FindRepositoryRoot();
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"pegasus-main-history-{Guid.NewGuid():N}");

    [Fact]
    public void AllowsMergeOnlyAppend()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "checkout", "-b", "feature");
        Commit(repository, "feature.txt", "feature", "feature commit");
        Git(repository, "checkout", "main");
        Git(repository, "merge", "--no-ff", "feature", "-m", "merge feature");
        var head = Git(repository, "rev-parse", "HEAD").Output.Trim();

        var result = RunGuard(repository, before, head);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("all two-parent merges", result.Output);
    }

    [Fact]
    public void RejectsDirectCommit()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Commit(repository, "direct.txt", "direct", "direct commit");

        AssertRejected(repository, before, "has 1 parent(s)");
    }

    [Fact]
    public void RejectsMixedBatchWithDirectMainlineCommit()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "checkout", "-b", "feature");
        Commit(repository, "feature.txt", "feature", "feature commit");
        Git(repository, "checkout", "main");
        Git(repository, "merge", "--no-ff", "feature", "-m", "merge feature");
        Commit(repository, "direct.txt", "direct", "direct commit");

        AssertRejected(repository, before, "has 1 parent(s)");
    }

    [Fact]
    public void RejectsUnavailableBeforeRevision()
    {
        var repository = CreateRepository();

        AssertRejected(repository, new string('a', 40), "rev-parse --verify");
    }

    [Fact]
    public void RejectsAllZeroBeforeRevision()
    {
        var repository = CreateRepository();

        AssertRejected(repository, new string('0', 40), "all-zero sentinel");
    }

    [Fact]
    public void RejectsNonAncestorHistory()
    {
        var repository = CreateRepository();
        Git(repository, "checkout", "--orphan", "replacement");
        Git(repository, "rm", "-rf", ".");
        Commit(repository, "replacement.txt", "replacement", "replacement root");
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "checkout", "main");
        Commit(repository, "main.txt", "main", "main commit");

        AssertRejected(repository, before, "is not an ancestor");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            foreach (var file in Directory.EnumerateFiles(_testRoot, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private void AssertRejected(string repository, string before, string expected)
    {
        var head = Git(repository, "rev-parse", "HEAD").Output.Trim();
        var result = RunGuard(repository, before, head);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private CommandResult RunGuard(string repository, string before, string head) =>
        Run("pwsh", _repositoryRoot,
            "-NoLogo", "-NoProfile", "-File", Path.Combine(_repositoryRoot, "scripts", "Test-MainBranchHistory.ps1"),
            "-Before", before, "-Head", head, "-RepositoryPath", repository);

    private string CreateRepository()
    {
        Directory.CreateDirectory(_testRoot);
        var repository = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);
        Git(repository, "init", "--initial-branch=main");
        Git(repository, "config", "user.name", "Pegasus Test");
        Git(repository, "config", "user.email", "pegasus-test@example.invalid");
        Commit(repository, "initial.txt", "initial", "initial commit");
        return repository;
    }

    private static void Commit(string repository, string relativePath, string content, string message)
    {
        File.WriteAllText(Path.Combine(repository, relativePath), content);
        Git(repository, "add", relativePath);
        Git(repository, "commit", "-m", message);
    }

    private static CommandResult Git(string repository, params string[] arguments) =>
        Run("git", repository, arguments);

    private static CommandResult Run(string executable, string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
        var result = new CommandResult(process.ExitCode, output);
        if (executable == "git" && result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {output}");
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the Pegasus repository root.");
    }

    private sealed record CommandResult(int ExitCode, string Output);
}
