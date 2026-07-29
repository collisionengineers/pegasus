using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.ReferenceData;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPegasusInfrastructure(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase,
        Func<IServiceProvider, string>? localArtifactRootFactory = null,
        Func<IServiceProvider, RequestUploadLimits>? requestUploadLimitsFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.AddDbContextFactory<PegasusDbContext>((serviceProvider, options) =>
        {
            options.UseOpenIddict();
            configureDatabase(serviceProvider, options);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<EfIntakeReceiptStore>();
        services.AddScoped<IIntakeReceiptStore>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<IIntakeReceiptQueries>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<IProviderReferenceCatalog, EfProviderReferenceCatalog>();
        services.AddSingleton<IInstructionExtractionPolicy, QdosInstructionExtractionPolicy>();
        services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();

        if (localArtifactRootFactory is not null)
        {
            services.AddSingleton<IIntakeArtifactStore>(provider =>
                new FileSystemIntakeArtifactStore(localArtifactRootFactory(provider)));
            services.AddScoped<IIntakeSourceReader, MimeKitPdfPigOpenXmlIntakeSourceReader>();
            services.AddScoped<ProcessIntake>();

            services.AddSingleton(provider =>
                new LocalDocumentContentStore(Path.Combine(localArtifactRootFactory(provider), "custody")));
            services.AddSingleton<ICaseCustody>(provider =>
                new LocalCaseCustody(
                    Path.Combine(localArtifactRootFactory(provider), "custody"),
                    provider.GetRequiredService<IIntakeArtifactStore>()));
            services.AddScoped<IProcessQueuedCustody, EfQueuedCustodyProcessor>();

            services.AddScoped<EfDocumentCustodyStore>();
            services.AddScoped<IAddCaseDocument>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IDownloadCaseDocument>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IExportCaseDocuments>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<ILogicallyRemoveDocument>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());

            services.AddScoped<EfBoxFileRequestStore>();
            services.AddScoped<ICreateBoxFileRequest>(provider =>
                provider.GetRequiredService<EfBoxFileRequestStore>());
            services.AddScoped<IRevokeBoxFileRequest>(provider =>
                provider.GetRequiredService<EfBoxFileRequestStore>());

            if (requestUploadLimitsFactory is not null)
            {
                services.AddSingleton(requestUploadLimitsFactory);
                services.AddSingleton<RequestUploadPolicy>();
                services.AddScoped<EfDocumentRequestStore>();
                services.AddScoped<ICreateRequestUploadLink>(provider =>
                    provider.GetRequiredService<EfDocumentRequestStore>());
                services.AddScoped<IRevokeRequestUploadLink>(provider =>
                    provider.GetRequiredService<EfDocumentRequestStore>());
                services.AddScoped<IUploadToRequest>(provider =>
                    provider.GetRequiredService<EfDocumentRequestStore>());
                services.AddScoped<IGetRequestUpload>(provider =>
                    provider.GetRequiredService<EfDocumentRequestStore>());
            }
        }
        return services;
    }
}
