using Pegasus.Core.Intake;
using Pegasus.Core.ReferenceData;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Core.Cases;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPegasusInfrastructure(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase,
        Func<IServiceProvider, string>? localArtifactRootFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.AddDbContextFactory<PegasusDbContext>(configureDatabase);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<EfIntakeReceiptStore>();
        services.AddScoped<IIntakeReceiptStore>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<EfTriageStore>();
        services.AddScoped<ITriageQueries>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ITriageStore>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<EfCaseStore>();
        services.AddScoped<ICaseQueries>(provider => provider.GetRequiredService<EfCaseStore>());
        services.AddScoped<ICaseWorkflow>(provider => provider.GetRequiredService<EfCaseStore>());
        services.AddScoped<ICaseEditing>(provider => provider.GetRequiredService<EfCaseStore>());
        services.AddScoped<ICaseAcceptance, EfCaseAcceptanceStore>();
        services.AddScoped<IIntakeReceiptQueries>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<IProviderReferenceCatalog, EfProviderReferenceCatalog>();
        services.AddSingleton<IInstructionExtractionPolicy, QdosInstructionExtractionPolicy>();

        if (localArtifactRootFactory is not null)
        {
            services.AddSingleton<IIntakeArtifactStore>(provider =>
                new FileSystemIntakeArtifactStore(localArtifactRootFactory(provider)));
            services.AddScoped<IIntakeSourceReader, MimeKitPdfPigOpenXmlIntakeSourceReader>();
            services.AddScoped<ProcessIntake>();
        }
        return services;
    }
}
