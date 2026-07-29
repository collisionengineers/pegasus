using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.ReferenceData;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Core.Triage;
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
        services.AddScoped<EfTriageStore>();
        services.AddScoped<ITriageStore>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ITriageQueries>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ICreateTriageFromIntake, CreateTriageFromIntake>();
        services.AddScoped<IProviderReferenceCatalog, EfProviderReferenceCatalog>();
        services.AddSingleton<IInstructionExtractionPolicy, QdosInstructionExtractionPolicy>();
        services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();
        services.AddScoped<IExternalWorkStore, EfExternalWorkStore>();
        services.AddSingleton<ICaseWorkflowConfiguration, DefaultCaseWorkflowConfiguration>();
        services.AddScoped<EfCaseWorkflowStore>();
        services.AddScoped<ICaseWorkflowStore>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseWorkflowQueries>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ILeaseCaseForEdit>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseDueWorkStore>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseDueWorkQueries>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<IPutCaseOnHold, PutCaseOnHold>();
        services.AddScoped<IReleaseCaseHold, ReleaseCaseHold>();
        services.AddScoped<IReturnCaseToReview, ReturnCaseToReview>();
        services.AddScoped<IAssignCaseEngineer, AssignCaseEngineer>();
        services.AddScoped<IStartCaseWork, StartCaseWork>();
        services.AddScoped<IBeginCaseReportPreparation, BeginCaseReportPreparation>();
        services.AddScoped<IRecordCaseReportApproval, RecordCaseReportApproval>();
        services.AddScoped<IRecordCaseReportSent, RecordCaseReportSent>();
        services.AddScoped<ICloseCase, CloseCase>();
        services.AddScoped<IReopenCase, ReopenCase>();
        services.AddScoped<IRecordManualCaseChase, RecordManualCaseChase>();

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
    public static IServiceCollection AddLocalApprovedInbox(
        this IServiceCollection services,
        Func<IServiceProvider, LocalApprovedInboxOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);
        services.AddSingleton<LocalApprovedInboxOptions>(optionsFactory);
        services.AddSingleton<IApprovedInboxSource, LocalDurableApprovedInboxSource>();
        services.AddScoped<IApprovedInboxPollStore, EfApprovedInboxPollStore>();
        services.AddScoped<PollApprovedInbox>();
        return services;
    }
}
