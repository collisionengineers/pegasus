namespace Pegasus.ArchitectureTests;

public sealed class SqlServerExecutionStrategyContractTests
{
    [Theory]
    [InlineData("src/Pegasus.Web/Program.cs")]
    [InlineData("src/Pegasus.Worker/WorkerDependencyInjection.cs")]
    public void BothHostsUseTheSharedSqlRetryConfiguration(string relativePath)
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

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }
}
