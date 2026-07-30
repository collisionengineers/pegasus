using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.ReferenceData;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Core.Triage;
using Pegasus.Core.Operations;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Pegasus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPegasusInfrastructure(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase,
        Func<IServiceProvider, string>? localArtifactRootFactory = null,
        Func<IServiceProvider, RequestUploadLimits>? requestUploadLimitsFactory = null,
        Func<IServiceProvider, EvaMappingAcceptance>? evaMappingAcceptanceFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.AddDbContextFactory<PegasusDbContext>((serviceProvider, options) =>
        {
            options.UseOpenIddict();
            configureDatabase(serviceProvider, options);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider =>
            evaMappingAcceptanceFactory?.Invoke(provider) ?? EvaMappingAcceptance.Unaccepted);
        services.TryAddSingleton(VehicleLookupAvailability.Unavailable);
        services.AddScoped<EfIntakeReceiptStore>();
        services.AddScoped<IIntakeReceiptStore>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<IIntakeReceiptQueries>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<EfTriageStore>();
        services.AddScoped<ITriageStore>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ITriageQueries>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ITriageResponseEvidenceCandidateQueries>(
            provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<IListTriage, ListTriage>();
        services.AddScoped<IGetTriage, GetTriage>();
        services.AddScoped<ICreateTriageFromIntake, CreateTriageFromIntake>();
        services.AddScoped<IAssignTriage, AssignTriage>();
        services.AddScoped<IUnassignTriage, UnassignTriage>();
        services.AddScoped<IAwaitTriageInformation, AwaitTriageInformation>();
        services.AddScoped<IRecordTriageFinding, RecordTriageFinding>();
        services.AddScoped<ISupersedeTriageFinding, SupersedeTriageFinding>();
        services.AddScoped<ILinkTriageResponseEvidence, LinkTriageResponseEvidence>();
        services.AddScoped<IUnlinkTriageResponseEvidence, UnlinkTriageResponseEvidence>();
        services.AddScoped<ICompleteTriage, CompleteTriage>();
        services.AddScoped<ICancelTriage, CancelTriage>();
        services.AddScoped<IReopenTriage, ReopenTriage>();
        services.AddScoped<ILinkTriageCase, LinkTriageCase>();
        services.AddScoped<IUnlinkTriageCase, UnlinkTriageCase>();
        services.AddScoped<EfEmailEvidenceStore>();
        services.AddScoped<IRecordSentEmailEvidence>(
            provider => provider.GetRequiredService<EfEmailEvidenceStore>());
        services.AddScoped<IRecordEmailResponseEvidence>(
            provider => provider.GetRequiredService<EfEmailEvidenceStore>());
        services.AddScoped<IExactEmailResponseEvidenceQueries>(
            provider => provider.GetRequiredService<EfEmailEvidenceStore>());
        services.AddScoped<ISentEvidencePollOutcomeQueries, EfSentEvidencePollOutcomeQueries>();
        services.AddScoped<ReplaySentEmailEvidence>();
        services.AddScoped<IProviderReferenceCatalog, EfProviderReferenceCatalog>();
        services.TryAddSingleton<IIntakeTriageMatcher, NoAcceptedIntakeTriageMatcher>();
        services.AddSingleton<IInstructionExtractionPolicy>(provider =>
            new QdosInstructionExtractionPolicy(
                provider.GetRequiredService<IIntakeTriageMatcher>()));
        services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();
        services.AddScoped<EfStaffAccountAdministration>();
        services.AddScoped<IStaffAccountQueries>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<ICreateStaffAccountStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IDisableStaffAccountStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IAssignStaffRolesStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IReviewStaffAccessStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IListStaffAccounts, ListStaffAccounts>();
        services.AddScoped<IGetStaffAccount, GetStaffAccount>();
        services.AddScoped<IGetAccessReview, GetAccessReview>();
        services.AddScoped<IGetRoleAssignments, GetRoleAssignments>();
        services.AddScoped<ICreateStaffAccount, CreateStaffAccount>();
        services.AddScoped<IDisableStaffAccount, DisableStaffAccount>();
        services.AddScoped<IAssignStaffRoles, AssignStaffRoles>();
        services.AddScoped<IReviewStaffAccess, ReviewStaffAccess>();
        services.AddScoped<OpenIddictMcpClientAdministration>();
        services.AddScoped<IPublicMcpClientStore>(provider =>
            provider.GetRequiredService<OpenIddictMcpClientAdministration>());
        services.AddScoped<IStaffMcpAuthorizationStore>(provider =>
            provider.GetRequiredService<OpenIddictMcpClientAdministration>());
        services.AddScoped<IRegisterPublicMcpClient, RegisterPublicMcpClient>();
        services.AddScoped<IRevokePublicMcpClient, RevokePublicMcpClient>();
        services.AddScoped<IRevokeStaffMcpAuthorizations, RevokeStaffMcpAuthorizations>();
        services.AddScoped<IStaffPasswordChangeStore, EfStaffPasswordChange>();
        services.AddScoped<IChangeStaffPassword, ChangeStaffPassword>();
        services.AddScoped<EfOrganizationAdministration>();
        services.AddScoped<IOrganizationAdministrationStore>(
            provider => provider.GetRequiredService<EfOrganizationAdministration>());
        services.AddScoped<IOrganizationAdministrationQueries>(
            provider => provider.GetRequiredService<EfOrganizationAdministration>());
        services.AddScoped<ICreateOrganization, CreateOrganization>();
        services.AddScoped<IUpdateOrganizationRoles, UpdateOrganizationRoles>();
        services.AddScoped<ICreatePrincipal, CreatePrincipal>();
        services.AddScoped<IReplacePrincipal, ReplacePrincipal>();
        services.AddScoped<IListOrganizations, ListOrganizations>();
        services.AddScoped<IGetOrganization, GetOrganization>();
        services.AddScoped<EfStandaloneAuditEvidenceStore>();
        services.AddScoped<IConfirmStandaloneAuditEvidence>(
            provider => provider.GetRequiredService<EfStandaloneAuditEvidenceStore>());
        services.AddScoped<IStandaloneAuditEvidenceQueries>(
            provider => provider.GetRequiredService<EfStandaloneAuditEvidenceStore>());
        services.AddScoped<EfExternalWorkStore>();
        services.AddScoped<IExternalWorkStore>(
            provider => provider.GetRequiredService<EfExternalWorkStore>());
        services.AddScoped<IQueuedExternalWorkReader>(
            provider => provider.GetRequiredService<EfExternalWorkStore>());
        services.AddScoped<EfVehicleWorkflowStore>();
        services.AddScoped<IRequestVehicleLookupStore>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<IAcceptVehicleSuggestionStore>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<IVehicleEvidenceQueries>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<IRequestVehicleLookup, RequestVehicleLookup>();
        services.AddScoped<IAcceptVehicleSuggestion, AcceptVehicleSuggestion>();
        services.AddScoped<IVehicleLookupWorkStore, EfVehicleLookupWorkStore>();
        services.AddScoped<EfOperationsStore>();
        services.AddScoped<IEmailOperationsProjectionStore>(
            provider => provider.GetRequiredService<EfOperationsStore>());
        services.AddScoped<IRequestOperationsProjectionStore>(
            provider => provider.GetRequiredService<EfOperationsStore>());
        services.AddScoped<IMailboxProcessingRetryStore>(
            provider => provider.GetRequiredService<EfOperationsStore>());
        services.AddScoped<IExternalWorkRetryStore>(
            provider => provider.GetRequiredService<EfOperationsStore>());
        services.AddScoped<GetEmailOperations>();
        services.AddScoped<GetRequestOperations>();
        services.AddScoped<RetryMailboxProcessing>();
        services.AddScoped<RetryExternalWork>();
        services.AddScoped<IGetOperationsSnapshot, GetOperationsSnapshot>();
        services.AddScoped<EfWorkflowConfigurationStore>();
        services.AddScoped<IWorkflowConfigurationStore>(
            provider => provider.GetRequiredService<EfWorkflowConfigurationStore>());
        services.AddScoped<ICaseWorkflowConfiguration>(
            provider => provider.GetRequiredService<EfWorkflowConfigurationStore>());
        services.AddScoped<GetWorkflowConfiguration>();
        services.AddScoped<UpdateWorkflowConfiguration>();
        services.AddScoped<EfApprovedMailboxStore>();
        services.AddScoped<IApprovedMailboxStore>(
            provider => provider.GetRequiredService<EfApprovedMailboxStore>());
        services.AddScoped<IApprovedMailboxPolicy>(
            provider => provider.GetRequiredService<EfApprovedMailboxStore>());
        services.AddScoped<ListApprovedMailboxes>();
        services.AddScoped<UpdateApprovedMailbox>();
        services.AddScoped<EfCaseWorkflowStore>();
        services.AddScoped<ICaseWorkflowStore>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<IAutoLinkReportEvidenceStore>(
            provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseWorkflowQueries>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ILeaseCaseForEdit>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseArchiveStore>(
            provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseArchiveReadinessQueries>(
            provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<IAcquireCaseEditLease, AcquireCaseEditLease>();
        services.AddScoped<IRenewCaseEditLease, RenewCaseEditLease>();
        services.AddScoped<IReleaseCaseEditLease, ReleaseCaseEditLease>();
        services.AddScoped<ICaseDueWorkStore>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseDueWorkQueries>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<EfCaseQueryStore>();
        services.AddScoped<ICaseQueryStore>(
            provider => provider.GetRequiredService<EfCaseQueryStore>());
        services.AddScoped<ISearchCases, SearchCases>();
        services.AddScoped<IGetCase, GetCase>();
        services.AddScoped<EfCaseDataStore>();
        services.AddScoped<ICaseDataStore>(
            provider => provider.GetRequiredService<EfCaseDataStore>());
        services.AddScoped<ICaseDataQueries>(
            provider => provider.GetRequiredService<EfCaseDataStore>());
        services.AddScoped<IConfirmCompleteness, ConfirmCompleteness>();
        services.AddScoped<ISaveCase, SaveCase>();
        services.AddScoped<EfCaseTaskStore>();
        services.AddScoped<ICaseTaskStore>(
            provider => provider.GetRequiredService<EfCaseTaskStore>());
        services.AddScoped<ICaseTaskQueries>(
            provider => provider.GetRequiredService<EfCaseTaskStore>());
        services.AddScoped<ICaseTaskAssigneeDirectory>(
            provider => provider.GetRequiredService<EfCaseTaskStore>());
        services.AddScoped<ICreateCaseTask, CreateCaseTask>();
        services.AddScoped<IAssignCaseTask, AssignCaseTask>();
        services.AddScoped<ICompleteCaseTask, CompleteCaseTask>();
        services.AddScoped<ICancelCaseTask, CancelCaseTask>();
        services.AddScoped<EfCaseDueChaserStore>();
        services.AddScoped<ICaseDueChaserQueries>(
            provider => provider.GetRequiredService<EfCaseDueChaserStore>());
        services.AddScoped<ICaseDueChaserStore>(
            provider => provider.GetRequiredService<EfCaseDueChaserStore>());
        services.AddScoped<RunDueChasers>();
        services.AddScoped<EfCaseReportSentEvidenceStore>();
        services.AddScoped<IApprovedMailboxReportSentEvidenceStore>(
            provider => provider.GetRequiredService<EfCaseReportSentEvidenceStore>());
        services.AddScoped<IApprovedMailboxReportSentEvidenceQueries>(
            provider => provider.GetRequiredService<EfCaseReportSentEvidenceStore>());
        services.AddScoped<IRetainApprovedMailboxReportSentEvidence, RetainApprovedMailboxReportSentEvidence>();
        services.AddScoped<EfLinkedCaseReplacementStore>();
        services.AddScoped<ILinkedCaseReplacementStore>(
            provider => provider.GetRequiredService<EfLinkedCaseReplacementStore>());
        services.AddScoped<ICreateLinkedReplacement, CreateLinkedReplacement>();
        services.AddScoped<IRecordEngineerFinding, EfRecordEngineerFinding>();
        services.AddScoped<IPutCaseOnHold, PutCaseOnHold>();
        services.AddScoped<IReleaseCaseHold, ReleaseCaseHold>();
        services.AddScoped<IReturnCaseToReview, ReturnCaseToReview>();
        services.AddScoped<ICaseEngineerEligibility, EfCaseEngineerEligibility>();
        services.AddScoped<IAssignCaseEngineer, AssignCaseEngineer>();
        services.AddScoped<IStartCaseWork, StartCaseWork>();
        services.AddScoped<IHoldCase, HoldCase>();
        services.AddScoped<IReleaseCase, ReleaseCase>();
        services.AddScoped<ITransitionCase, TransitionCase>();
        services.AddScoped<IRecordCaseReportApproval, RecordCaseReportApproval>();
        services.AddScoped<ILinkReportEvidence, LinkReportEvidence>();
        services.AddScoped<IAutoLinkReportEvidence, AutoLinkReportEvidence>();
        services.AddScoped<IUnlinkReportEvidence, UnlinkReportEvidence>();
        services.AddScoped<ICloseCase, CloseCase>();
        services.AddScoped<IReopenCase, ReopenCase>();
        services.AddScoped<IArchiveCase, ArchiveCase>();
        services.AddScoped<IRecordManualCaseChase, RecordManualCaseChase>();

        if (localArtifactRootFactory is not null)
        {
            services.AddSingleton(provider =>
                new FileSystemIntakeArtifactStore(localArtifactRootFactory(provider)));
            services.AddSingleton<IIntakeArtifactStore>(provider =>
                provider.GetRequiredService<FileSystemIntakeArtifactStore>());
            services.AddSingleton<IIntakeQuarantineArtifactStore>(provider =>
                provider.GetRequiredService<FileSystemIntakeArtifactStore>());
            services.AddScoped<IIntakeSourceReader, MimeKitPdfPigOpenXmlIntakeSourceReader>();
            services.AddScoped<ProcessIntake>();

            services.AddSingleton(provider =>
                new LocalDocumentContentStore(Path.Combine(localArtifactRootFactory(provider), "custody")));
            services.AddSingleton<IEvaHandoffProxy, LocalEvaHandoffProxy>();
            services.AddScoped<EvaHandoffStore>();
            services.AddScoped<IEvaHandoffQueries>(provider =>
                provider.GetRequiredService<EvaHandoffStore>());
            services.AddScoped<IGenerateEvaHandoff>(provider =>
                provider.GetRequiredService<EvaHandoffStore>());
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
            services.AddScoped<ICaseDocumentStateQueries>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());

            services.AddScoped<EfBoxFileRequestStore>();
            services.AddScoped<ICreateBoxFileRequest>(provider =>
                provider.GetRequiredService<EfBoxFileRequestStore>());
            services.AddScoped<IRevokeBoxFileRequest>(provider =>
                provider.GetRequiredService<EfBoxFileRequestStore>());

        }
        if (localArtifactRootFactory is null)
        {
            services.AddSingleton<ICaseCustody, UnavailableCaseCustody>();
            services.AddScoped<IProcessQueuedCustody, EfQueuedCustodyProcessor>();
        }
        if (localArtifactRootFactory is not null
            && requestUploadLimitsFactory is not null)
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
        else
        {
            services.AddScoped<UnavailableDocumentRequestStore>();
            services.AddScoped<ICreateRequestUploadLink>(provider =>
                provider.GetRequiredService<UnavailableDocumentRequestStore>());
            services.AddScoped<IRevokeRequestUploadLink>(provider =>
                provider.GetRequiredService<UnavailableDocumentRequestStore>());
            services.AddScoped<IUploadToRequest>(provider =>
                provider.GetRequiredService<UnavailableDocumentRequestStore>());
            services.AddScoped<IGetRequestUpload>(provider =>
                provider.GetRequiredService<UnavailableDocumentRequestStore>());
        }
        return services;
    }
    public static IServiceCollection AddPegasusApplicationInitialization(
        this IServiceCollection services)
    {
        services.AddScoped<IApplicationInitializationStore, EfApplicationInitialization>();
        services.AddScoped<IInitializeApplication, InitializeApplication>();
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

    public static IServiceCollection AddLocalApprovedSent(
        this IServiceCollection services,
        Func<IServiceProvider, LocalApprovedSentOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);
        services.AddSingleton<LocalApprovedSentOptions>(optionsFactory);
        services.AddSingleton<IApprovedSentSource, LocalDurableApprovedSentSource>();
        services.AddScoped<ISentEvidencePollStore, EfSentEvidencePollStore>();
        services.AddScoped<PollSentEvidence>();
        return services;
    }
}
