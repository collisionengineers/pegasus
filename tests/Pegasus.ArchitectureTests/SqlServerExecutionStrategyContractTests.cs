namespace Pegasus.ArchitectureTests;

public sealed class SqlServerExecutionStrategyContractTests
{
    [Theory]
    [InlineData("src/Pegasus.Web/Program.cs")]
    [InlineData("src/Pegasus.Worker/WorkerDependencyInjection.cs")]
    public void BothHostsUseTheSharedSqlRetryConfiguration(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot.Find(), relativePath));

        Assert.Contains("PegasusSqlServer.Configure(options, connectionString);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("options.UseSqlServer(", source, StringComparison.Ordinal);
    }
}
