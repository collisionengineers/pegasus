using System.Diagnostics;

namespace Pegasus.ArchitectureTests;

public sealed class MainBranchHistoryGuardTests : IDisposable
{
    private readonly string _repositoryRoot = FindRepositoryRoot();
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"pegasus-main-history-{Guid.NewGuid():N}");

    [Fact]
    public void AllowsExactFastForward()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "branch", "dev");
        Git(repository, "checkout", "dev");
        Commit(repository, "feature.txt", "feature", "development commit");
        Git(repository, "checkout", "main");
        Git(repository, "merge", "--ff-only", "dev");
        var head = Git(repository, "rev-parse", "HEAD").Output.Trim();

        var result = RunGuard(repository, before, head, "dev");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("contained in the release branch", result.Output);
    }

    [Fact]
    public void AllowsReleasedHeadWhenDevHasAdvanced()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "branch", "dev");
        Git(repository, "checkout", "dev");
        Commit(repository, "released.txt", "released", "released development commit");
        var releasedHead = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "checkout", "main");
        Git(repository, "merge", "--ff-only", "dev");
        Git(repository, "checkout", "dev");
        Commit(repository, "later.txt", "later", "later development commit");

        var result = RunGuard(repository, before, releasedHead, "dev");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void RejectsDirectMainCommitOutsideDev()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "branch", "dev");
        Commit(repository, "direct.txt", "direct", "direct commit");

        AssertRejected(repository, before, "dev", "not an ancestor of release branch");
    }

    [Fact]
    public void RejectsGitHubStyleMergeCommitOutsideDev()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();
        Git(repository, "branch", "dev");
        Git(repository, "checkout", "-b", "feature");
        Commit(repository, "feature.txt", "feature", "feature commit");
        Git(repository, "checkout", "main");
        Git(repository, "merge", "--no-ff", "feature", "-m", "GitHub-style merge");

        AssertRejected(repository, before, "dev", "not an ancestor of release branch");
    }

    [Fact]
    public void RejectsUnavailableBeforeRevision()
    {
        var repository = CreateRepository();

        AssertRejected(repository, new string('a', 40), "main", "rev-parse --verify");
    }

    [Fact]
    public void RejectsUnavailableReleaseBranch()
    {
        var repository = CreateRepository();
        var before = Git(repository, "rev-parse", "HEAD").Output.Trim();

        AssertRejected(repository, before, new string('a', 40), "rev-parse --verify");
    }

    [Fact]
    public void RejectsAllZeroBeforeRevision()
    {
        var repository = CreateRepository();

        AssertRejected(repository, new string('0', 40), "main", "all-zero sentinel");
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

        AssertRejected(repository, before, "main", "is not an ancestor");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            AssertTemporaryDirectory(_testRoot);
            foreach (var file in Directory.EnumerateFiles(_testRoot, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private void AssertRejected(string repository, string before, string releaseBranch, string expected)
    {
        var head = Git(repository, "rev-parse", "HEAD").Output.Trim();
        var result = RunGuard(repository, before, head, releaseBranch);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private CommandResult RunGuard(string repository, string before, string head, string releaseBranch) =>
        Run("pwsh", _repositoryRoot,
            "-NoLogo", "-NoProfile", "-File", Path.Combine(_repositoryRoot, "scripts", "Test-MainBranchHistory.ps1"),
            "-Before", before, "-Head", head, "-ReleaseBranch", releaseBranch, "-RepositoryPath", repository);

    private string CreateRepository()
    {
        Directory.CreateDirectory(_testRoot);
        var repository = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);
        Git(repository, "init", "--initial-branch=main");
        AssertTemporaryRepositoryRoot(repository);
        Git(repository, "config", "user.name", "Pegasus Test");
        Git(repository, "config", "user.email", "pegasus-test@example.invalid");
        Commit(repository, "initial.txt", "initial", "initial commit");
        return repository;
    }

    private static void Commit(string repository, string relativePath, string content, string message)
    {
        AssertTemporaryRepositoryRoot(repository);
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
        foreach (var name in startInfo.Environment.Keys
                     .Where(name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(name);
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

    private static void AssertTemporaryRepositoryRoot(string repository)
    {
        var expected = Path.GetFullPath(repository).TrimEnd(Path.DirectorySeparatorChar);
        var temporaryParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryParent, expected + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
        var actual = Git(repository, "rev-parse", "--show-toplevel").Output.Trim();
        Assert.True(string.Equals(
            expected,
            Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal));
    }

    private static void AssertTemporaryDirectory(string directory)
    {
        var expected = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        var temporaryParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryParent, expected + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
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
