using CollisionSpike.Core.Intake.Qdos;
using CollisionSpike.Infrastructure.Intake.Qdos;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCollisionSpikeInfrastructure(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase,
        string? localArtifactRoot = null)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.AddDbContextFactory<CollisionSpikeDbContext>(configureDatabase);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIntakeArtifactStore>(new FileSystemIntakeArtifactStore(
            localArtifactRoot ?? Path.Combine(AppContext.BaseDirectory, "artifacts", "intake")));
        services.AddScoped<IQdosIntakeSourceReader, MimeKitPdfPigQdosSourceReader>();
        services.AddScoped<EfQdosIntakeStore>();
        services.AddScoped<IQdosIntakeStore>(provider => provider.GetRequiredService<EfQdosIntakeStore>());
        services.AddScoped<IQdosIntakeQueries>(provider => provider.GetRequiredService<EfQdosIntakeStore>());
        services.AddScoped<ProcessQdosIntake>();
        return services;
    }
}
