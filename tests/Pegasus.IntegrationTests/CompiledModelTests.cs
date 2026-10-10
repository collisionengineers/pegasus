using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Persistence.CompiledModel;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Both hosts start from the checked-in compiled model, and it describes the same entities
/// the runtime would build. Nothing here opens a connection.
/// </summary>
public sealed class CompiledModelTests
{
    private const string ConnectionString = "Server=tcp:127.0.0.1,1;Database=Unused;Encrypt=False";

    [Fact]
    public void ConfiguredContextUsesTheCompiledModelWithTheRuntimeModelsEntities()
    {
        var configured = new DbContextOptionsBuilder<PegasusDbContext>();
        PegasusSqlServer.Configure(configured, ConnectionString);
        using var compiledContext = new PegasusDbContext(configured.Options);

        // The design-time model is always built from OnModelCreating, never loaded compiled.
        var runtimeModel = compiledContext.GetService<IDesignTimeModel>().Model;

        Assert.Same(PegasusDbContextModel.Instance, compiledContext.Model);
        Assert.NotSame(PegasusDbContextModel.Instance, runtimeModel);
        Assert.Equal(
            runtimeModel.GetEntityTypes().Select(entity => entity.Name).Order(StringComparer.Ordinal),
            compiledContext.Model.GetEntityTypes().Select(entity => entity.Name).Order(StringComparer.Ordinal));
    }
}
