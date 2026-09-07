using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Assessment;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Eva;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.ReferenceData;
using Pegasus.Core.Reports;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Core.Triage;
using Pegasus.Core.Operations;
using Pegasus.Core.ProviderApi;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Vehicle;
using Pegasus.Infrastructure.Vision;
using Pegasus.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Azure.Core;

namespace Pegasus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPegasusInfrastructure(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase,
        Func<IServiceProvider, string>? localArtifactRootFactory = null,
        Func<IServiceProvider, RequestUploadLimits>? requestUploadLimitsFactory = null,
        Action<IServiceCollection>? documentStorage = null)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);
        if (localArtifactRootFactory is not null && documentStorage is not null)
        {
            throw new InvalidOperationException(
                "A runtime profile supplies either a local artifact root or an external document storage registration, never both.");
        }

        services.AddDbContextFactory<PegasusDbContext>((serviceProvider, options) =>
        {
            configureDatabase(serviceProvider, options);
        });

        services.AddLogging();
        services.AddScoped<IActionLogQueries, EfActionLogQueries>();
        services.AddScoped<ListActionLogs>();
        services.AddScoped<IV1ActivityReportQueries, EfV1ActivityReportQueries>();
        services.AddScoped<GetV1ActivityReport>();
        services.AddScoped<IAdministrationAiJobQueries, EfAdministrationAiJobQueries>();
        services.AddScoped<GetAdministrationAiJobs>();
        services.AddScoped<IAdministrationHealthMetricsQueries, EfAdministrationHealthMetricsQueries>();
        services.AddScoped<GetAdministrationHealthMetrics>();
        services.AddSingleton<DocumentContentCacheMetrics>();
        services.AddSingleton<IDocumentContentCacheMetrics>(provider =>
            provider.GetRequiredService<DocumentContentCacheMetrics>());
        services.AddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDocumentContentCacheCleanup, NoDocumentContentCacheCleanup>();
        services.TryAddSingleton(VehicleLookupAvailability.Unavailable);
        services.AddScoped<EfIntakeReceiptStore>();
        services.AddScoped<EfIntakeSubmissionGroupStore>();
        services.AddScoped<IIntakeSubmissionGroupStore>(provider =>
            provider.GetRequiredService<EfIntakeSubmissionGroupStore>());
        services.AddScoped<IIntakeReceiptStore>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<IIntakeReceiptQueries>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<ICaseEvidenceImageQueries>(provider => provider.GetRequiredService<EfIntakeReceiptStore>());
        services.AddScoped<EfIntakeAllocationStore>();
        services.AddScoped<IIntakeAllocationStore>(
            provider => provider.GetRequiredService<EfIntakeAllocationStore>());
        services.AddScoped<IAllocateIntake, AllocateIntake>();
        services.AddScoped<IListIntake, ListIntake>();
        services.AddScoped<IListIntakeByCursor, ListIntakeByCursor>();
        services.AddScoped<IGetIntake, GetIntake>();
        services.AddScoped<IGetIntakeSourceMetadata, GetIntakeSourceMetadata>();
        // The read half of retained mail only. The write port is registered by the
        // poll compositions below, so nothing in Web can add a retained message.
        services.AddScoped<EfRetainedMailboxMessageStore>();
        services.AddScoped<IRetainedMailQueries>(
            provider => provider.GetRequiredService<EfRetainedMailboxMessageStore>());
        services.AddScoped<IRetainedMailClassificationStore>(
            provider => provider.GetRequiredService<EfRetainedMailboxMessageStore>());
        services.AddScoped<ListRetainedMail>();
        services.AddScoped<GetRetainedMail>();
        services.AddScoped<CorrectRetainedMailClassification>();
        services.TryAddSingleton<IRetainedMailFolderMover, UnavailableRetainedMailFolderMover>();
        services.AddScoped<EfRetainedMailFolderMoveStore>();
        services.AddScoped<IRetainedMailFolderMoveStore>(provider =>
            provider.GetRequiredService<EfRetainedMailFolderMoveStore>());
        services.AddScoped<MoveRetainedMailFolder>();
        services.AddScoped<GetRetainedMailFreshness>();
        services.TryAddSingleton<IDeletedMailSearchSource, UnavailableDeletedMailSearchSource>();
        services.AddScoped<SearchDeletedMail>();
        services.AddScoped<IDownloadIntakeSource, DownloadIntakeSource>();
        services.AddScoped<IDownloadIntakeAsset, DownloadIntakeAsset>();
        services.AddScoped<EfIntakeMutationStore>();
        services.AddScoped<IIntakeMutationStore>(provider =>
            provider.GetRequiredService<EfIntakeMutationStore>());
        services.AddScoped<IAutomaticCaseAssociationStore>(provider =>
            provider.GetRequiredService<EfIntakeMutationStore>());
        services.AddScoped<IAutomaticMailCaseAssociationEvidenceQueries>(provider =>
            provider.GetRequiredService<EfIntakeMutationStore>());
        services.AddScoped<AssociateRetainedMailWithCase>();
        services.AddScoped<IResolveIntake, ResolveIntake>();
        services.AddScoped<IReevaluateIntake, ReevaluateIntake>();
        services.AddScoped<ILinkIntake, LinkIntake>();
        services.AddScoped<IReverseIntakeLink, ReverseIntakeLink>();
        services.AddScoped<EfImageIntakeStore>();
        services.AddScoped<IImageIntakeStore>(provider => provider.GetRequiredService<EfImageIntakeStore>());
        services.AddScoped<IImageIntakeQueries>(provider => provider.GetRequiredService<EfImageIntakeStore>());
        services.AddScoped<IImageIntakeOriginResolver, EfImageIntakeOriginResolver>();
        services.AddScoped<IImageIntakeCaseCandidates, EfImageIntakeCaseCandidates>();
        services.AddScoped<IRegisterImageIntake, RegisterImageIntake>();
        services.AddScoped<IVrmSuggestionStore, EfImageVrmSuggestionStore>();
        services.TryAddSingleton<IVrmRecognitionEngine, OnnxVrmRecognitionEngine>();
        services.AddScoped<IImageIntakeAutomation, ImageIntakeAutomation>();
        services.AddScoped<IImageIntakeCasePairing, ImageIntakeCasePairing>();
        services.AddScoped<EfUnidentifiedStore>();
        services.AddScoped<IUnidentifiedStore>(provider => provider.GetRequiredService<EfUnidentifiedStore>());
        services.AddScoped<IListUnidentifiedQueueByCursor, ListUnidentifiedQueueByCursor>();
        services.AddScoped<IRegisterUnidentified, RegisterUnidentified>();
        services.AddScoped<IResolveUnidentified, ResolveUnidentified>();
        services.AddScoped<ReconcileUnidentifiedDestinations>();
        services.AddScoped<EfTriageStore>();
        services.AddScoped<ITriageStore>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ITriageQueries>(provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<ITriageResponseEvidenceCandidateQueries>(
            provider => provider.GetRequiredService<EfTriageStore>());
        services.AddScoped<IListTriage, ListTriage>();
        services.AddScoped<IListTriagePage, ListTriagePage>();
        services.AddScoped<IGetTriage, GetTriage>();
        services.AddScoped<ICreateTriageFromIntake, CreateTriageFromIntake>();
        services.AddScoped<IAssignTriage, AssignTriage>();
        services.AddScoped<IAddTriageNote, AddTriageNote>();
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
        services.AddSingleton<IMailRoutePolicy, QdosMailRoutePolicy>();
        services.AddSingleton<IMailClassificationPolicy, QdosMailClassificationPolicy>();
        services.AddSingleton<IProviderCaseMatchPolicy, QdosCaseMatchPolicy>();
        services.AddScoped<ICaseMatchCandidateQueries, EfCaseMatchIndex>();
        services.AddScoped<EvaluateIntakeCaseMatch>();
        services.AddSingleton<QdosInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy>(provider =>
            provider.GetRequiredService<QdosInstructionExtractionPolicy>());
        services.AddSingleton<IInstructionExtractionPolicy, AlsInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, AxInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, BcInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, BlackInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, DfdInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, FwInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, KbsInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, MpInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, OakInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, PchInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, QclInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, RjsInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, SblInstructionExtractionPolicy>();
        services.AddSingleton<IInstructionExtractionPolicy, YmlInstructionExtractionPolicy>();
        services.AddScoped<InstructionExtractionPolicySelector>();
        services.AddScoped<EfRetainedInstructionAnalysisStore>();
        services.AddScoped<IRetainedInstructionAnalysisStore>(provider =>
            provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
        services.AddScoped<ISourceCandidateQueries>(provider =>
            provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
        services.AddScoped<IThirdPartyReportCandidateQueries>(provider =>
            provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
        services.AddScoped<IGetLatestRetainedInstructionAnalysis, GetLatestRetainedInstructionAnalysis>();
        services.AddScoped<AnalyzeRetainedInstruction>();
        services.AddScoped<IAnalyzeRetainedInstruction>(provider =>
            provider.GetRequiredService<AnalyzeRetainedInstruction>());
        services.AddScoped<ICaseAcceptanceStore, EfCaseAcceptanceStore>();

        // Registered here rather than only in the Web composition root, because
        // allocation is no longer a staff action: the Worker's processing path
        // creates the case for a definitive instruction, and it composes only
        // Infrastructure.
        services.AddScoped<IAcceptIntake, AcceptIntake>();
        services.AddScoped<IProviderInspectionModeStore, EfProviderInspectionModeStore>();
        services.AddScoped<IEvaSubmissionModeStore, EfEvaSubmissionModeStore>();
        services.AddScoped<IEvaSubmissionQueries, EfEvaSubmissionQueries>();
        services.AddScoped<EfStaffAccountAdministration>();
        // UserManager-free: safe for hosts (the Worker; Infrastructure-only test
        // hosts) that never compose ASP.NET Identity, unlike EfStaffAccountAdministration.
        services.AddScoped<EfStaffAccountQueries>();
        services.AddScoped<IStaffAccountQueries>(provider => provider.GetRequiredService<EfStaffAccountQueries>());
        services.AddScoped<IStaffHeldCaseEditLeaseQueries>(provider => provider.GetRequiredService<EfStaffAccountQueries>());
        services.AddScoped<ICaseEngineerChoices>(provider => provider.GetRequiredService<EfStaffAccountQueries>());
        services.AddScoped<ICreateStaffAccountStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IDisableStaffAccountStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IAssignStaffRolesStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IEnableStaffAccountStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IForceStaffLogoutStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IResetStaffPasswordStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IDeleteStaffAccountStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IUpdateStaffAccountSignOffStore>(provider =>
            provider.GetRequiredService<EfStaffAccountAdministration>());
        services.AddScoped<IListStaffAccounts, ListStaffAccounts>();
        services.AddScoped<IGetStaffAccount, GetStaffAccount>();
        services.AddScoped<IDescribeCaseEditAuthorityHolder, DescribeCaseEditAuthorityHolder>();
        services.AddScoped<IGetStaffHeldCaseEditLeases, GetStaffHeldCaseEditLeases>();
        services.AddScoped<IGetRoleAssignments, GetRoleAssignments>();
        services.AddScoped<ICreateStaffAccount, CreateStaffAccount>();
        services.AddScoped<IDisableStaffAccount, DisableStaffAccount>();
        services.AddScoped<IAssignStaffRoles, AssignStaffRoles>();
        services.AddScoped<IEnableStaffAccount, EnableStaffAccount>();
        services.AddScoped<IForceStaffLogout, ForceStaffLogout>();
        services.AddScoped<IResetStaffPassword, ResetStaffPassword>();
        services.AddScoped<IDeleteStaffAccount, DeleteStaffAccount>();
        services.AddScoped<IUpdateStaffAccountSignOff, UpdateStaffAccountSignOff>();
        services.AddScoped<IStaffPasswordChangeStore, EfStaffPasswordChange>();
        services.AddScoped<IChangeStaffPassword, ChangeStaffPassword>();
        services.AddScoped<EfPerUserExternalCredentialStore>();
        services.AddScoped<IPerUserExternalCredentialReader>(provider =>
            provider.GetRequiredService<EfPerUserExternalCredentialStore>());
        services.AddScoped<IPerUserExternalCredentialAdministration>(provider =>
            provider.GetRequiredService<EfPerUserExternalCredentialStore>());
        services.AddScoped<EfOrganizationAdministration>();
        services.AddScoped<IOrganizationAdministrationStore>(
            provider => provider.GetRequiredService<EfOrganizationAdministration>());
        services.AddScoped<IOrganizationAdministrationQueries>(
            provider => provider.GetRequiredService<EfOrganizationAdministration>());
        services.AddScoped<EfClaimSourceAdministration>();
        services.AddScoped<IClaimSourceAdministration>(
            provider => provider.GetRequiredService<EfClaimSourceAdministration>());
        services.AddScoped<IClaimSourceQueries>(
            provider => provider.GetRequiredService<EfClaimSourceAdministration>());
        services.AddScoped<IOrganizationDirectoryQueries, EfOrganizationDirectory>();
        services.AddScoped<IUpdatePrincipalDefaultInspectionLocation, UpdatePrincipalDefaultInspectionLocation>();
        services.AddScoped<EfPrincipalCredentialStore>();
        services.AddScoped<IPrincipalCredentialStore>(
            provider => provider.GetRequiredService<EfPrincipalCredentialStore>());
        services.AddScoped<IPrincipalCredentialQueries>(
            provider => provider.GetRequiredService<EfPrincipalCredentialStore>());
        services.AddScoped<IIssuePrincipalCredential, IssuePrincipalCredential>();
        services.AddScoped<IPausePrincipalCredential, PausePrincipalCredential>();
        services.AddScoped<IResumePrincipalCredential, ResumePrincipalCredential>();
        services.AddScoped<IRevokePrincipalCredential, RevokePrincipalCredential>();
        services.AddScoped<IGetPrincipalCredential, GetPrincipalCredential>();
        services.AddScoped<IAuthenticatePrincipalCredential, AuthenticatePrincipalCredential>();
        // API-01: the submission row is both the idempotency record and the
        // Principal binding processing reads, so the Worker composes the
        // bindings port too.
        services.AddScoped<EfProviderSubmissionStore>();
        services.AddScoped<IProviderSubmissionStore>(
            provider => provider.GetRequiredService<EfProviderSubmissionStore>());
        services.AddScoped<IProviderSubmissionBindings>(
            provider => provider.GetRequiredService<EfProviderSubmissionStore>());
        services.AddScoped<ISubmitProviderInstruction, SubmitProviderInstruction>();
        services.AddScoped<IGetProviderSubmissionResult, GetProviderSubmissionResult>();
        services.AddScoped<ICreateOrganization, CreateOrganization>();
        services.AddScoped<IUpdateOrganizationRoles, UpdateOrganizationRoles>();
        services.AddScoped<ICreatePrincipal, CreatePrincipal>();
        services.AddScoped<IReplacePrincipal, ReplacePrincipal>();
        services.AddScoped<IUpdatePrincipalEvaSubmission, UpdatePrincipalEvaSubmission>();
        services.AddScoped<IListOrganizations, ListOrganizations>();
        services.AddScoped<IGetOrganization, GetOrganization>();
        services.AddScoped<EfStandaloneAuditEvidenceStore>();
        services.AddScoped<IRecordAutomaticStandaloneAuditEvidence>(
            provider => provider.GetRequiredService<EfStandaloneAuditEvidenceStore>());
        services.AddScoped<IStandaloneAuditEvidenceQueries>(
            provider => provider.GetRequiredService<EfStandaloneAuditEvidenceStore>());
        services.AddScoped<EfExternalWorkStore>();
        services.AddScoped<IExternalWorkStore>(
            provider => provider.GetRequiredService<EfExternalWorkStore>());
        services.AddScoped<IQueuedExternalWorkReader>(
            provider => provider.GetRequiredService<EfExternalWorkStore>());
        services.AddScoped<ICustodyRecoveryPersistence>(
            provider => provider.GetRequiredService<EfExternalWorkStore>());
        services.AddScoped<ICaseCustodyQueries>(
            provider => provider.GetRequiredService<EfExternalWorkStore>());
        services.AddScoped<IRetryCaseCustody, RetryCaseCustody>();
        services.AddScoped<EfVehicleWorkflowStore>();
        services.AddScoped<IRequestVehicleLookupStore>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<IAcceptVehicleSuggestionStore>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<IVehicleEvidenceQueries>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<IAutomaticVehicleLookupStore>(
            provider => provider.GetRequiredService<EfVehicleWorkflowStore>());
        services.AddScoped<ReconcileAutomaticVehicleLookups>();
        services.AddScoped<IAutomaticEvaSubmissionStore, EfAutomaticEvaSubmissionStore>();
        services.AddScoped<ReconcileAutomaticEvaSubmissions>();
        services.AddScoped<ReconcileProviderSubmissions>();
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
        services.AddScoped<IDashboardQueries, EfDashboardQueries>();
        services.AddScoped<GetOperationsSnapshot>();
        services.AddScoped<IGetOperationsSnapshot>(provider =>
            provider.GetRequiredService<GetOperationsSnapshot>());
        services.AddScoped<IGetAttentionRows>(provider =>
            provider.GetRequiredService<GetOperationsSnapshot>());
        services.AddScoped<IServiceHealthQueries, EfServiceHealthQueries>();
        services.AddScoped<IEngineerActivityQueries, EfEngineerActivityQueries>();
        services.AddScoped<GetEngineerActivityReport>();
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
        services.AddScoped<IApprovedIntakeMailboxes>(
            provider => provider.GetRequiredService<EfApprovedMailboxStore>());
        services.AddScoped<IApprovedMailboxPollStatusQueries, EfApprovedMailboxPollStatusQueries>();
        services.AddScoped<IApprovedMailboxSubscriptionStore, EfApprovedMailboxSubscriptionStore>();
        services.AddScoped<ListApprovedMailboxes>();
        services.AddScoped<UpdateApprovedMailbox>();
        services.AddScoped<EfApprovedOutlookCategoryStore>();
        services.AddScoped<IApprovedOutlookCategoryStore>(provider =>
            provider.GetRequiredService<EfApprovedOutlookCategoryStore>());
        services.AddScoped<IApprovedOutlookCategoryResolver>(provider =>
            provider.GetRequiredService<EfApprovedOutlookCategoryStore>());
        services.AddScoped<ListApprovedOutlookCategories>();
        services.AddScoped<UpdateApprovedOutlookCategory>();
        services.AddScoped<ResolveApprovedOutlookCategory>();
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
        services.AddScoped<IHeartbeatCaseEditLease, HeartbeatCaseEditLease>();
        services.AddScoped<IReleaseCaseEditLease, ReleaseCaseEditLease>();
        services.AddScoped<IAdministrativeCaseEditLeaseStore>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<IClearCaseEditLease, ClearCaseEditLease>();
        services.AddScoped<ICaseDueWorkStore>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<ICaseDueWorkQueries>(provider => provider.GetRequiredService<EfCaseWorkflowStore>());
        services.AddScoped<EfCaseQueryStore>();
        services.AddScoped<ICaseQueryStore>(
            provider => provider.GetRequiredService<EfCaseQueryStore>());
        services.AddScoped<ISearchCases, SearchCases>();
        services.AddScoped<ISearchCasesByCursor, SearchCasesByCursor>();
        services.AddScoped<IListCaseDocumentsByCursor, ListCaseDocumentsByCursor>();
        services.AddScoped<IListCaseHistoryByCursor, ListCaseHistoryByCursor>();
        services.AddScoped<IGetCaseHeader, GetCaseHeader>();
        services.AddScoped<IGetCase, GetCase>();
        services.AddScoped<EfCaseDataStore>();
        services.AddScoped<ICaseDataStore>(
            provider => provider.GetRequiredService<EfCaseDataStore>());
        services.AddScoped<ICaseDataQueries>(
            provider => provider.GetRequiredService<EfCaseDataStore>());
        services.AddScoped<InspectionAddressChoicesQueries>();
        services.AddScoped<IInspectionAddressChoicesQueries>(
            provider => provider.GetRequiredService<InspectionAddressChoicesQueries>());
        services.AddScoped<IInspectionLocationChoices>(
            provider => provider.GetRequiredService<InspectionAddressChoicesQueries>());
        services.AddScoped<IConfirmCompleteness, ConfirmCompleteness>();
        services.AddScoped<ICaseNoteStore, EfCaseNoteStore>();
        services.AddScoped<IAddCaseNote, AddCaseNote>();
        services.AddScoped<EfEngineerNoteStore>();
        services.AddScoped<IEngineerNoteStore>(provider =>
            provider.GetRequiredService<EfEngineerNoteStore>());
        services.AddScoped<IEngineerNoteQueries>(provider =>
            provider.GetRequiredService<EfEngineerNoteStore>());
        services.AddScoped<IAddEngineerNote, AddEngineerNote>();
        services.AddScoped<ISaveCase, SaveCase>();
        services.AddScoped<ICaseWorkspaceStore, EfCaseWorkspaceStore>();
        services.AddScoped<ISaveCaseWorkspace, SaveCaseWorkspace>();
        services.AddScoped<IRepairSpecificationStore, EfRepairSpecificationStore>();
        // The JSON estimate document (ENG-026) sits beside the Audatex PDF;
        // the import dialog selects the parser by the chosen source route.
        services.AddSingleton<JsonEstimateParser>();
        services.AddSingleton<IEstimateDocumentParser>(provider =>
            provider.GetRequiredService<JsonEstimateParser>());
        services.AddSingleton<IEstimateDocumentParser, GlassEstimateXmlParser>();
        // Details still requests the PDF parser singly and JSON by its concrete
        // type; canonical import consumes all parsers through the collection.
        services.AddSingleton<IEstimateDocumentParser, AudatexEstimatePdfParser>();
        services.AddScoped<IGlassRepairEstimateSessionStore, EfGlassRepairEstimateSessionStore>();
        services.AddScoped<IImportRawEstimate, ImportRawEstimate>();
        services.AddScoped<ISaveEstimate, SaveEstimate>();
        services.AddScoped<IDuplicateEstimate, DuplicateEstimate>();
        services.AddScoped<IDiscardEstimate, DiscardEstimate>();
        services.AddScoped<ISetCurrentEstimate, SetCurrentEstimate>();
        services.AddScoped<IListCaseEstimates, ListCaseEstimates>();
        services.AddScoped<IListCaseEstimatesByCursor, ListCaseEstimatesByCursor>();
        services.AddScoped<EfCaseAssetPreparationStore>();
        services.AddScoped<ICaseAssetPreparationStore>(provider =>
            provider.GetRequiredService<EfCaseAssetPreparationStore>());
        services.AddScoped<ICaseAssetPreparationQueries>(provider =>
            provider.GetRequiredService<EfCaseAssetPreparationStore>());
        services.AddScoped<EfValuationStore>();
        services.AddScoped<IValuationStore>(provider =>
            provider.GetRequiredService<EfValuationStore>());
        services.AddScoped<IAppliedValuationStore>(provider =>
            provider.GetRequiredService<EfValuationStore>());
        services.AddScoped<EfValuationPresetStore>();
        services.AddScoped<IValuationPresetStore>(provider =>
            provider.GetRequiredService<EfValuationPresetStore>());
        services.AddScoped<IListValuationPresets, ListValuationPresets>();
        services.AddScoped<ISaveValuationPreset, SaveValuationPreset>();
        services.AddScoped<IPreviewValuationCalculation, PreviewValuationCalculation>();
        services.AddScoped<IApplyValuationCalculation, ApplyValuationCalculation>();
        services.AddScoped<IListAppliedValuations, ListAppliedValuations>();
        services.AddScoped<ISaveValuation, SaveValuation>();
        services.AddScoped<IEditValuation, EditValuation>();
        services.AddScoped<IListCaseValuations, ListCaseValuations>();
        services.AddScoped<ICaseAssessmentStore, EfCaseAssessmentStore>();
        services.AddScoped<IGetCaseAssessment, GetCaseAssessment>();
        services.AddScoped<IAssessmentAccessSource, EfAssessmentAccessSource>();
        services.AddScoped<IGetAssessmentAccess, GetAssessmentAccess>();
        services.AddScoped<IAssessmentWorkspaceSource, EfAssessmentWorkspaceSource>();
        services.AddScoped<IGetAssessmentWorkspace, GetAssessmentWorkspace>();
        services.AddScoped<ISaveAssessment, SaveAssessment>();
        services.AddScoped<IAiWorkRequestStore, EfAiWorkRequestStore>();
        services.AddScoped<ISendToAiControl, EfSendToAiControlStore>();
        services.AddScoped<EfAiJobStore>();
        services.AddScoped<IAiJobStore>(provider => provider.GetRequiredService<EfAiJobStore>());
        services.AddScoped<IAiJobQueries>(provider => provider.GetRequiredService<EfAiJobStore>());
        services.AddScoped<ICreateAiJob, CreateAiJob>();
        services.AddScoped<IWorkAiJob, WorkAiJob>();
        services.AddScoped<ICancelAiJob, CancelAiJob>();
        services.AddScoped<IConfirmAiJob, ConfirmAiJob>();
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
        services.AddScoped<ISetCaseSignOffEngineer, SetCaseSignOffEngineer>();
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

        // The document, EVA and custody surface is composed for every profile that
        // has durable content storage. Only the implementations differ; a profile
        // must never silently resolve a different service set.
        var composesDocumentSurface = localArtifactRootFactory is not null || documentStorage is not null;

        if (localArtifactRootFactory is not null)
        {
            services.AddSingleton(provider =>
                new FileSystemIntakeArtifactStore(localArtifactRootFactory(provider)));
            services.AddSingleton<IIntakeArtifactStore>(provider =>
                provider.GetRequiredService<FileSystemIntakeArtifactStore>());
            services.AddSingleton<IIntakeQuarantineArtifactStore>(provider =>
                provider.GetRequiredService<FileSystemIntakeArtifactStore>());

            services.AddSingleton(provider =>
                new LocalDocumentContentStore(Path.Combine(localArtifactRootFactory(provider), "custody")));
            services.AddSingleton<IDocumentContentStore>(provider =>
                provider.GetRequiredService<LocalDocumentContentStore>());
            services.AddScoped<IReadLogicalDocumentVersion, LocalLogicalDocumentVersionReader>();
            services.AddScoped<ReconcilePendingArtifactCustody>();
            services.AddScoped(provider => new EfCaseArtifactCustody(
                provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                provider.GetRequiredService<IDocumentContentStore>(),
                provider.GetRequiredService<IIntakeArtifactStore>(),
                provider.GetRequiredService<TimeProvider>()));
            services.AddScoped<ICaseArtifactCustody>(provider =>
                provider.GetRequiredService<EfCaseArtifactCustody>());
            services.AddScoped<ICaseArtifactCustodyStatus>(provider =>
                provider.GetRequiredService<EfCaseArtifactCustody>());
            services.AddSingleton<IEvaHandoffProxy, LocalEvaHandoffProxy>();
            services.AddSingleton<ICaseCustody>(provider =>
                new LocalCaseCustody(
                    Path.Combine(localArtifactRootFactory(provider), "custody"),
                    provider.GetRequiredService<IIntakeArtifactStore>()));
        }
        else if (documentStorage is not null)
        {
            documentStorage(services);
            services.AddSingleton<IEvaHandoffProxy, LocalEvaHandoffProxy>();
        }
        else
        {
            services.AddSingleton<ICaseCustody, UnavailableCaseCustody>();
        }

        services.AddScoped<IProcessQueuedCustody, EfQueuedCustodyProcessor>();

        if (composesDocumentSurface)
        {
            // The Provider API reader decorates the ordinary one: it answers for
            // its own channel and defers for every other (API-01).
            services.AddScoped<MimeKitPdfPigOpenXmlIntakeSourceReader>();
            services.AddScoped<IIntakeSourceReader>(provider =>
                new ProviderApiIntakeSourceReader(
                    provider.GetRequiredService<MimeKitPdfPigOpenXmlIntakeSourceReader>()));
            services.AddScoped(provider =>
                ActivatorUtilities.CreateInstance<ProcessIntake>(
                    provider,
                    provider.GetRequiredService<QdosInstructionExtractionPolicy>()));

            // Shared by both EVA routes so the archive and the API submission
            // cannot state the same case differently.
            services.AddScoped<EvaCaseImageReader>();
            services.AddScoped<EvaHandoffStore>();
            services.AddScoped<IExportCaseBundle>(provider =>
                provider.GetRequiredService<EvaHandoffStore>());

            services.AddScoped<EfDocumentCustodyStore>();
            services.AddScoped<IAddCaseDocument>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IDownloadCaseDocument>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IGetCaseDocumentMetadata>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IExportCaseDocuments>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<ILogicallyRemoveDocument>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IConfirmThirdPartyVehicleEvidence>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<ICaseDocumentStateQueries>(provider =>
                provider.GetRequiredService<EfDocumentCustodyStore>());
            services.AddScoped<IMarketResearchAiJobCompletionStore, EfMarketResearchAiJobCompletionStore>();
            services.AddScoped<ICompleteMarketResearchAiJob, CompleteMarketResearchAiJob>();
        }
        if (composesDocumentSurface
            && requestUploadLimitsFactory is not null)
        {
            services.AddSingleton(requestUploadLimitsFactory);
            services.AddSingleton<RequestUploadPolicy>();
            services.AddScoped<EfPublicUploadRetentionStore>();
            services.AddScoped<IIncomingArtifactRetentionStore>(provider =>
                provider.GetRequiredService<EfPublicUploadRetentionStore>());
            services.AddScoped<RetainIncomingArtifact>();
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

    public static IServiceCollection AddPegasusReportRendering(this IServiceCollection services)
    {
        services.AddSingleton<IAssessmentReportRenderer, PlaywrightAssessmentReportRenderer>();
        services.AddScoped<GenerateAssessmentReportDraft>();
        services.AddScoped<EfAssessmentReportProjectionSource>();
        services.AddScoped<IAssessmentReportProjectionSource>(provider =>
            provider.GetRequiredService<EfAssessmentReportProjectionSource>());
        services.AddScoped<ICaseReportSnapshotSource>(provider =>
            provider.GetRequiredService<EfAssessmentReportProjectionSource>());
        services.AddScoped<EfCaseReportGenerationStore>();
        services.AddScoped<ICaseReportGenerationStore>(provider =>
            provider.GetRequiredService<EfCaseReportGenerationStore>());
        services.AddScoped<ICaseReportGenerationQueries>(provider =>
            provider.GetRequiredService<EfCaseReportGenerationStore>());
        services.AddScoped<IGeneratedCaseArtifactStore>(provider =>
            provider.GetRequiredService<EfCaseReportGenerationStore>());
        services.AddScoped<ICaseReportContentSource, EfCaseReportContentSource>();
        services.AddScoped<IGenerateCaseReport, GenerateCaseReport>();
        services.AddScoped<ICaseReportDeliveryPreparationStore, EfCaseReportDeliveryPreparationStore>();
        services.AddScoped<IPrepareCaseReportDelivery, PrepareCaseReportDelivery>();
        services.AddScoped<IReportSendReadiness, ReportSendReadiness>();
        services.AddScoped<ISendPreparedCaseReport, SendPreparedCaseReport>();
        services.AddScoped<GenerateCaseAssessmentReportDraft>();
        return services;
    }
    public static IServiceCollection AddLocalApprovedInbox(
        this IServiceCollection services,
        Func<IServiceProvider, LocalApprovedInboxOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);
        services.AddSingleton<LocalApprovedInboxOptions>(optionsFactory);
        services.AddSingleton<IApprovedInboxSourceSettings>(provider =>
            provider.GetRequiredService<LocalApprovedInboxOptions>());
        services.AddSingleton<IApprovedInboxSource, LocalDurableApprovedInboxSource>();
        services.AddScoped<IApprovedInboxPollStore, EfApprovedInboxPollStore>();
        services.AddScoped<IRetainedMailboxMessageStore>(
            provider => provider.GetRequiredService<EfRetainedMailboxMessageStore>());
        services.AddScoped<PollApprovedInbox>();
        return services;
    }

    public static IServiceCollection AddLocalApprovedSent(
        this IServiceCollection services,
        Func<IServiceProvider, LocalApprovedSentOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);
        services.AddSingleton<LocalApprovedSentOptions>(optionsFactory);
        services.AddSingleton<IApprovedSentSourceSettings>(provider =>
            provider.GetRequiredService<LocalApprovedSentOptions>());
        services.AddSingleton<IApprovedSentSource, LocalDurableApprovedSentSource>();
        services.AddScoped<ISentEvidencePollStore, EfSentEvidencePollStore>();
        services.AddScoped<PollSentEvidence>();
        services.AddScoped<IStaffMailEvidenceReconciler>(provider =>
            provider.GetRequiredService<PollSentEvidence>());
        return services;
    }

    /// <summary>
    /// The production durable-storage profile: blob-backed intake artifacts plus the
    /// approved Box custody root for case custody and managed document content. Web
    /// and Worker both compose this, so both hosts read and write one storage truth.
    /// </summary>
    public static IServiceCollection AddProductionDocumentStorage(
        this IServiceCollection services,
        Func<IServiceProvider, Azure.Storage.Blobs.BlobContainerClient> intakeContainerFactory,
        Func<IServiceProvider, bool> allowContainerCreateIfNotExists,
        Func<IServiceProvider, BoxCustodyOptions> boxOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(intakeContainerFactory);
        ArgumentNullException.ThrowIfNull(allowContainerCreateIfNotExists);

        services.AddSingleton(provider => new AzureBlobIntakeArtifactStore(
            intakeContainerFactory(provider),
            allowContainerCreateIfNotExists(provider)));
        services.AddSingleton<IIntakeArtifactStore>(provider =>
            provider.GetRequiredService<AzureBlobIntakeArtifactStore>());
        services.AddSingleton<IIntakeQuarantineArtifactStore>(provider =>
            provider.GetRequiredService<AzureBlobIntakeArtifactStore>());
        services.AddScoped(provider => new CachedDocumentContentStore(
            provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            intakeContainerFactory(provider),
            provider.GetRequiredService<BoxContentClient>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IDocumentContentCacheMetrics>()));
        services.AddScoped<IReadLogicalDocumentVersion>(provider =>
            provider.GetRequiredService<CachedDocumentContentStore>());
        services.AddScoped<IDocumentContentCacheCleanup>(provider =>
            provider.GetRequiredService<CachedDocumentContentStore>());
        return services.AddProductionBoxCustody(boxOptions);
    }

    /// <summary>
    /// Registers the approved Box custody root as both the case custody adapter and
    /// the managed-document content store. Both composition roots call this so Web
    /// and Worker resolve the same fenced Box client rather than diverging.
    /// The options factory runs at first Box resolution, not at host build: an
    /// invalid or still-unresolved Box secret fails the Box work item, never the
    /// whole process (PLAT-013 — the worker exit-134 crash loop).
    /// </summary>
    public static IServiceCollection AddProductionBoxCustody(
        this IServiceCollection services,
        Func<IServiceProvider, BoxCustodyOptions> boxOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(boxOptions);

        services.AddSingleton(provider => boxOptions(provider));
        services.AddHttpClient(nameof(BoxContentClient), client =>
            client.Timeout = BoxJwtAuthorizationHeaderProvider.RequestTimeout);
        // The header provider needs a clock. Every caller reaches this through
        // AddPegasusInfrastructure, which registers one, but the storage
        // profile should stand up on its own rather than depend on the order
        // two extension methods happen to be called in.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IBoxAuthorizationHeaderProvider>(provider =>
            new BoxJwtAuthorizationHeaderProvider(
                provider.GetRequiredService<BoxCustodyOptions>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new BoxContentClient(
            provider.GetRequiredService<BoxCustodyOptions>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(BoxContentClient)),
            provider.GetRequiredService<IBoxAuthorizationHeaderProvider>()));
        services.AddSingleton<ICaseCustody>(provider => new BoxCaseCustody(
            provider.GetRequiredService<IIntakeArtifactStore>(),
            provider.GetRequiredService<BoxContentClient>()));
        services.AddSingleton<IDocumentContentStore>(provider => new BoxDocumentContentStore(
            provider.GetRequiredService<BoxContentClient>()));
        services.AddScoped(provider => new EfCaseArtifactCustody(
            provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            provider.GetRequiredService<IDocumentContentStore>(),
            provider.GetRequiredService<IIntakeArtifactStore>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<BoxContentClient>(),
            provider.GetRequiredService<BoxCustodyOptions>().HoldingFolderId));
        services.AddScoped<ICaseArtifactCustody>(provider =>
            provider.GetRequiredService<EfCaseArtifactCustody>());
        services.AddScoped<ICaseArtifactCustodyStatus>(provider =>
            provider.GetRequiredService<EfCaseArtifactCustody>());
        services.AddScoped<ReconcilePendingArtifactCustody>();
        return services;
    }

    /// <summary>
    /// EXT-04: the EVA API submission route.
    ///
    /// Composed separately from the document surface and from the other
    /// external adapters, because it is the one route that is switched on per
    /// principal rather than per deployment. A host that does not call this
    /// has no <see cref="ISubmitCaseToEva"/> at all, which is the honest
    /// shape: the case page then offers only the export, and a principal's
    /// toggles are unreachable rather than half-working.
    ///
    /// The options come through a factory rather than a value so they are
    /// parsed at first use. Parsing at host build is what crash-looped the
    /// worker when the platform handed over an unresolved Key Vault reference
    /// (PLAT-013), and EVA's credentials arrive by exactly that route.
    /// </summary>
    public static IServiceCollection AddEvaApiSubmission(
        this IServiceCollection services,
        Func<IServiceProvider, EvaApiOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.AddSingleton(optionsFactory);
        services.AddSingleton(provider =>
            provider.GetRequiredService<EvaApiOptions>().Instruction);
        services.AddHttpClient(nameof(EvaApiTransport), client =>
            client.Timeout = TimeSpan.FromSeconds(100));
        services.AddSingleton<IEvaApiTransport>(provider => new EvaApiTransport(
            provider.GetRequiredService<EvaApiOptions>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(EvaApiTransport)),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<EvaSubmissionStore>();
        services.AddScoped<ISubmitCaseToEva>(provider =>
            provider.GetRequiredService<EvaSubmissionStore>());
        services.AddScoped<IEvaSubmissionWorkStore, EfEvaSubmissionWorkStore>();
        return services;
    }

    /// <summary>
    /// The mailbox and vehicle-lookup adapters. Box custody is not registered here —
    /// it belongs to the storage profile (<see cref="AddProductionBoxCustody"/>) so a
    /// host can compose custody without also composing mailbox polling.
    /// </summary>
    public static IServiceCollection AddProductionExternalAdapters(
        this IServiceCollection services,
        GraphApprovedMailboxOptions graphOptions,
        DvlaDvsaProductionOptions vehicleOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(graphOptions);
        ArgumentNullException.ThrowIfNull(vehicleOptions);

        services.AddSingleton(graphOptions);
        services.AddSingleton<IApprovedInboxSourceSettings>(graphOptions);
        services.AddSingleton(vehicleOptions);
        services.AddHttpClient(nameof(GraphMailClient), client =>
            client.Timeout = TimeSpan.FromSeconds(100));
        services.AddHttpClient(nameof(DvlaDvsaProductionAdapter), client =>
            client.Timeout = TimeSpan.FromSeconds(100));
        services.AddSingleton(provider => new GraphMailClient(
            provider.GetRequiredService<TokenCredential>(),
            provider.GetRequiredService<GraphApprovedMailboxOptions>().BaseUri,
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GraphMailClient))));
        AddStaffMailSending(services);
        services.AddSingleton<GraphMailboxChangeSubscriptions>();
        services.AddSingleton<IApprovedInboxSource, GraphApprovedInboxSource>();
        services.AddSingleton<IApprovedSentSource, GraphApprovedSentSource>();
        services.AddScoped<IApprovedInboxPollStore, EfApprovedInboxPollStore>();
        services.AddScoped<ISentEvidencePollStore, EfSentEvidencePollStore>();
        services.AddScoped<IRetainedMailboxMessageStore>(
            provider => provider.GetRequiredService<EfRetainedMailboxMessageStore>());
        services.AddScoped<PollApprovedInbox>();
        services.AddScoped<PollSentEvidence>();
        services.AddScoped<IStaffMailEvidenceReconciler>(provider =>
            provider.GetRequiredService<PollSentEvidence>());
        services.AddSingleton(VehicleLookupAvailability.ProductionLive);
        services.AddSingleton<IVehicleLookupAdapter>(provider => new DvlaDvsaProductionAdapter(
            provider.GetRequiredService<DvlaDvsaProductionOptions>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DvlaDvsaProductionAdapter)),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }

    /// <summary>
    /// The mailbox-administration "add an address" resolve port alone — independent of
    /// <see cref="AddProductionExternalAdapters"/>, which also composes the single
    /// configured polling mailbox and its Worker-only pollers. Web composes only this:
    /// it never polls, it only resolves an address the operator just typed.
    /// </summary>
    public static IServiceCollection AddProductionApprovedMailboxResolver(
        this IServiceCollection services,
        string? graphBaseUri)
    {
        ArgumentNullException.ThrowIfNull(services);
        var baseUri = GraphApprovedMailboxOptions.ParseBaseUri(graphBaseUri);
        services.AddHttpClient(nameof(GraphMailClient), client =>
            client.Timeout = TimeSpan.FromSeconds(100));
        services.AddSingleton(provider => new GraphApprovedMailboxResolver(
            provider.GetRequiredService<TokenCredential>(),
            baseUri,
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GraphMailClient)),
            provider.GetRequiredService<ILogger<GraphApprovedMailboxResolver>>()));
        services.AddSingleton<IResolveApprovedMailboxIdentity>(provider =>
            provider.GetRequiredService<GraphApprovedMailboxResolver>());
        services.AddSingleton<ICheckApprovedMailboxAccess>(provider =>
            provider.GetRequiredService<GraphApprovedMailboxResolver>());
        services.AddSingleton(provider => new GraphMailClient(
            provider.GetRequiredService<TokenCredential>(),
            baseUri,
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GraphMailClient))));
        services.AddSingleton<IApprovedSentSource, GraphApprovedSentSource>();
        services.AddScoped<ISentEvidencePollStore, EfSentEvidencePollStore>();
        services.AddScoped<PollSentEvidence>();
        services.AddScoped<IStaffMailEvidenceReconciler>(provider =>
            provider.GetRequiredService<PollSentEvidence>());
        AddStaffMailSending(services);
        services.AddScoped<IDeletedMailSearchSource, GraphDeletedMailSearchSource>();
        return services;
    }

    private static void AddStaffMailSending(IServiceCollection services)
    {
        services.AddScoped<EfStaffMailSendStore>();
        services.AddScoped<IStaffMailSendStore>(provider => provider.GetRequiredService<EfStaffMailSendStore>());
        services.AddScoped<IApprovedStaffSendMailboxQueries>(provider => provider.GetRequiredService<EfStaffMailSendStore>());
        services.AddScoped<IStaffMailUploadProgress, EfStaffMailUploadProgress>();
        services.AddScoped<IStaffMailExecutionLock, SqlStaffMailExecutionLock>();
        services.AddScoped<StaffMailSend>();
        services.AddScoped<IStaffMailSend>(provider => provider.GetRequiredService<StaffMailSend>());
        services.AddScoped<IStaffReportSend>(provider => new StaffReportSend(
            provider.GetRequiredService<IReportSendReadiness>(),
            provider.GetRequiredService<StaffMailSend>()));
        services.AddHttpClient(nameof(GraphStaffMailSender), client => client.Timeout = TimeSpan.FromSeconds(100));
        services.AddScoped<IStaffMailTransport>(provider => new GraphStaffMailSender(
            provider.GetRequiredService<GraphMailClient>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GraphStaffMailSender)),
            provider.GetRequiredService<IStaffMailUploadProgress>()));
    }
}
