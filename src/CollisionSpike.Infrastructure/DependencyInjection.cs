using CollisionSpike.Core.Intake;
using CollisionSpike.Infrastructure.Intake;
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
        services.AddScoped<IIntakeSourceReader, MimeKitPdfPigOpenXmlIntakeSourceReader>();
        services.AddScoped<EfIntakeReceiptStore>();
        services.AddScoped<IIntakeReceiptStore>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<IIntakeReceiptQueries>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddSingleton<IInstructionExtractionPolicy, QdosInstructionExtractionPolicy>();
        services.AddScoped<ProcessIntake>();
        return services;
    }
}
