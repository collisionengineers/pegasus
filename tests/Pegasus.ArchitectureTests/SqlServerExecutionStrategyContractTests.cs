namespace Pegasus.ArchitectureTests;

/// <summary>
/// Both hosts configure SQL Server through the one shared helper, so the
/// transaction-scoped retrying strategy is a single list rather than a
/// per-host choice that can drift.
/// </summary>
public sealed class SqlServerExecutionStrategyContractTests
{
    [Theory]
    [InlineData("src/Pegasus.Web/Program.cs")]
    [InlineData("src/Pegasus.Worker/WorkerDependencyInjection.cs")]
    public void EveryHostConfiguresSqlServerThroughTheSharedHelper(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

        Assert.Contains("PegasusSqlServer.Configure(options, connectionString);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("options.UseSqlServer(", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
