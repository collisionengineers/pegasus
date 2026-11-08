using System.Diagnostics;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The migration class guard CI runs on a pull request's changed migrations
/// (engineering#migrations, R605, R760, R761; #1845).
/// </summary>
public sealed class MigrationClassGuardTests : IDisposable
{
    private const string AdditiveUp = """migrationBuilder.AddColumn<string>(name: "New", table: "Cases", nullable: true);""";
    private const string DropUp = """migrationBuilder.DropColumn(name: "Old", table: "Cases");""";
    private const string SqlUp = """migrationBuilder.Sql("UPDATE [Cases] SET [New] = [Old];");""";

    private const string DestructiveDeclaration = """
        ## Summary
        Removes a column.

        ## Migration
        Non-additive: DropColumn of Cases.Old, on the destructive route.

        ## How verified
        CI.
        """;

    private const string AdditiveDeclaration = """
        ## Migration
        Additive: the UPDATE only fills the new column.
        """;

    // The pull request template's prompt names every class; a comment is not
    // a declaration.
    private const string TemplatePrompt = """
        ## Migration

        <!-- None, or: additive / non-additive (list the operations) and the destructive route (R760, R761; docs/engineering.md#migrations) -->
        """;

    private readonly string _repositoryRoot = RepositoryRoot.Find();
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"pegasus-migration-class-{Guid.NewGuid():N}");

    [Fact]
    public void AllowsAdditiveMigrationWithoutDeclaration()
    {
        var migration = WriteMigration("20261108120000_AddsColumn.cs", AdditiveUp);

        var result = RunGuard(migration);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("20261108120000_AddsColumn additive", result.Output);
    }

    [Fact]
    public void RejectsUndeclaredDrop()
    {
        var migration = WriteMigration("20261108120000_DropsColumn.cs", DropUp);

        var result = RunGuard(migration);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DropColumn", result.Output);
        Assert.Contains("destructive route", result.Output);
    }

    [Fact]
    public void AllowsDropDeclaredForDestructiveRoute()
    {
        var migration = WriteMigration("20261108120000_DropsColumn.cs", DropUp);

        var result = RunGuard(migration, DestructiveDeclaration);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("declared for the destructive route", result.Output);
    }

    [Fact]
    public void RejectsDropDeclaredAdditive()
    {
        var migration = WriteMigration("20261108120000_DropsColumn.cs", DropUp);

        var result = RunGuard(migration, AdditiveDeclaration);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DropColumn", result.Output);
    }

    [Fact]
    public void RejectsRawSqlWithoutDeclaration()
    {
        var migration = WriteMigration("20261108120000_RewritesRows.cs", SqlUp);

        var result = RunGuard(migration);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("cannot be classified by keyword", result.Output);
    }

    [Fact]
    public void AllowsRawSqlDeclaredAdditive()
    {
        var migration = WriteMigration("20261108120000_RewritesRows.cs", SqlUp);

        var result = RunGuard(migration, AdditiveDeclaration);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("declared additive", result.Output);
    }

    [Fact]
    public void RejectsTemplatePromptAsDeclaration()
    {
        var migration = WriteMigration("20261108120000_DropsColumn.cs", DropUp);

        var result = RunGuard(migration, TemplatePrompt);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DropColumn", result.Output);
    }

    [Fact]
    public void SkipsDesignerFile()
    {
        var designer = WriteMigration("20261108120000_DropsColumn.Designer.cs", DropUp);

        var result = RunGuard(designer);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("not a migration source", result.Output);
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

    /// <summary>
    /// Writes a migration source whose Down drops what its Up adds, as a
    /// generated one does, and returns its repository-relative path.
    /// </summary>
    private string WriteMigration(string fileName, string up)
    {
        var directory = Path.Combine(_testRoot, "src", "Pegasus.Infrastructure", "Persistence", "Migrations");
        Directory.CreateDirectory(directory);
        AssertTemporaryDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), $$"""
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Pegasus.Infrastructure.Persistence.Migrations;

            public partial class Probe : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    {{up}}
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(name: "New", table: "Cases");
                }
            }
            """);
        return $"src/Pegasus.Infrastructure/Persistence/Migrations/{fileName}";
    }

    private CommandResult RunGuard(string migrationPath, string? pullRequestBody = null)
    {
        var arguments = new List<string>
        {
            "-NoLogo", "-NoProfile", "-File", Path.Combine(_repositoryRoot, "scripts", "Test-MigrationClass.ps1"),
            "-MigrationPaths", migrationPath, "-RepositoryPath", _testRoot,
        };
        if (pullRequestBody is not null)
        {
            arguments.Add("-PullRequestBody");
            arguments.Add(pullRequestBody);
        }

        return Run("pwsh", _repositoryRoot, arguments.ToArray());
    }

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
        return new CommandResult(process.ExitCode, output);
    }

    private static void AssertTemporaryDirectory(string directory)
    {
        var expected = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        var temporaryParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryParent, expected + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CommandResult(int ExitCode, string Output);
}
