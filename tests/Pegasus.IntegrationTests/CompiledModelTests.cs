using Microsoft.EntityFrameworkCore;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Persistence.CompiledModel;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Both hosts start from the checked-in compiled model, and it describes the same entities
/// the runtime would build. Nothing here opens a connection.
/// </summary>
public sealed class CompiledModelTests
{
    private const string ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=CompiledModelTests;Integrated Security=true";

    [Fact]
    public void ConfiguredContextUsesTheCompiledModelWithTheRuntimeModelsEntities()
    {
        var configured = new DbContextOptionsBuilder<PegasusDbContext>();
        PegasusSqlServer.Configure(configured, ConnectionString);
        using var compiledContext = new PegasusDbContext(configured.Options);

        var runtime = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(ConnectionString);
        using var runtimeContext = new PegasusDbContext(runtime.Options);

        Assert.Same(PegasusDbContextModel.Instance, compiledContext.Model);
        Assert.NotSame(PegasusDbContextModel.Instance, runtimeContext.Model);
        Assert.Equal(
            runtimeContext.Model.GetEntityTypes().Select(entity => entity.Name).Order(StringComparer.Ordinal),
            compiledContext.Model.GetEntityTypes().Select(entity => entity.Name).Order(StringComparer.Ordinal));
    }
}
