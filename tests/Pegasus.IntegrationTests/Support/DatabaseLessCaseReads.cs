using Pegasus.Core.Address;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// The reads a Case page makes beside its recording store, answered on a
/// database-less test host as the migrated, initialised database answers them
/// for a Case it does not hold.
/// </summary>
/// <remarks>
/// A recording-store test serves its Case from the store, so these are the
/// reads it used to send to a restored database: no kind, preparation,
/// previous address, AI job, estimate, saved unroadworthy reason, salvage
/// matrix, report, valuation, claim source, repairer, image intake or lease to
/// resume; Send to AI switched on, because no row says otherwise; and the
/// reference rows the migrations seed (the labour-rate cards, the valuation
/// presets and the built-in image tags). <c>DatabaseLessWebHostTests</c> holds
/// every answer to the migrated database's. A read the Case page does not make
/// is refused rather than guessed.
/// </remarks>
internal sealed class DatabaseLessCaseReads :
    ICaseKindQueries,
    ICaseAssetPreparationQueries,
    IInspectionAddressChoicesQueries,
    IAiJobQueries,
    IListCaseEstimates,
    ILabourRateCardStore,
    IUnroadworthyReasonBankStore,
    IPrincipalSalvageMatrixQueries,
    ICaseReportSnapshotSource,
    ICaseReportGenerationStore,
    ISendToAiControl,
    IListCaseValuations,
    IListAppliedValuations,
    IValuationPresetStore,
    IContactDirectoryQueries,
    IResumeCaseEditLease,
    IReadImageTagVocabulary,
    IImageIntakeQueries
{
    /// <summary>The built-in cards <c>20261013090000_LabourRateBuiltInCards</c> seeds.</summary>
    private static readonly IReadOnlyList<LabourRateCard> LabourRateCards =
    [
        new(new Guid("5b0f3c1e-8f4a-4d2b-9a57-1c3e2f6a7b01"), LabourRateCardAdministration.Standard, 83.28m, 1),
        new(new Guid("5b0f3c1e-8f4a-4d2b-9a57-1c3e2f6a7b02"), LabourRateCardAdministration.PrestigeAluminium, 103.06m, 1),
    ];

    /// <summary>The presets <c>20260906054658_V1PlatformFoundation</c> seeds, in the store's label order.</summary>
    private static readonly IReadOnlyList<ValuationPreset> ValuationPresets =
    [
        SeededPreset("00000000-0000-4000-8000-00000000f004", "Camper conversion", 0.00m),
        SeededPreset("00000000-0000-4000-8000-00000000f003", "Decals", 500.00m),
        SeededPreset("00000000-0000-4000-8000-00000000f005", "Driving tuition", 500.00m),
        SeededPreset("00000000-0000-4000-8000-00000000f002", "PCO plated", 1500.00m),
        SeededPreset("00000000-0000-4000-8000-00000000f001", "Tow bar", 300.00m),
    ];

    /// <summary>
    /// The built-in tags <c>20260910120000_CaseImageTags</c> seeds, in the
    /// reader's name order (<c>20261007140000_MarketResearchDocumentRole</c>
    /// removed the fifth).
    /// </summary>
    private static readonly IReadOnlyList<ImageTag> ImageTags =
    [
        new(ImageTagVocabulary.CloseUpId, "Close-up", ImageTagColour.Green, true, 1),
        new(ImageTagVocabulary.OverviewId, "Overview", ImageTagColour.Blue, true, 1),
        new(ImageTagVocabulary.ReflectionId, "Reflection", ImageTagColour.Navy, true, 1),
        new(ImageTagVocabulary.ThirdPartyId, "Third party", ImageTagColour.Amber, true, 1),
    ];

    Task<CaseType?> ICaseKindQueries.GetAsync(Guid caseId, CancellationToken cancellationToken) =>
        Task.FromResult<CaseType?>(null);

    Task<CaseAssetPreparation?> ICaseAssetPreparationQueries.GetForOccurrenceAsync(
        Guid caseId, Guid occurrenceId, CancellationToken cancellationToken) =>
        Task.FromResult<CaseAssetPreparation?>(null);

    Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationQueries.ListForCaseAsync(
        Guid caseId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CaseAssetPreparation>>([]);

    Task<IReadOnlyList<string>> IInspectionAddressChoicesQueries.GetPreviousAddressesAsync(
        Guid caseId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    Task<IReadOnlyList<AiJobRecord>> IAiJobQueries.ListForSubjectAsync(
        Guid subjectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiJobRecord>>([]);

    Task<AiJobRecord?> IAiJobQueries.GetPendingMarketResearchAsync(
        Guid subjectId, CancellationToken cancellationToken) =>
        Task.FromResult<AiJobRecord?>(null);

    Task<IReadOnlyList<AiJobRecord>> IAiJobQueries.ListOpenAsync(CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<AiJobRecord>>(nameof(IAiJobQueries));

    Task<AiJobQueryPage> IAiJobQueries.ListOpenPageAsync(
        AiJobKind? kind,
        string grantId,
        DateTimeOffset? afterCreatedAtUtc,
        Guid? afterJobId,
        int limit,
        CancellationToken cancellationToken) =>
        Refused<AiJobQueryPage>(nameof(IAiJobQueries));

    Task<IReadOnlyList<AiJobRecord>> IAiJobQueries.ListRecentAsync(int max, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<AiJobRecord>>(nameof(IAiJobQueries));

    Task<AiJobCounts> IAiJobQueries.GetCountsAsync(CancellationToken cancellationToken) =>
        Refused<AiJobCounts>(nameof(IAiJobQueries));

    Task<IReadOnlyList<RepairSpecificationVersion>> IListCaseEstimates.ExecuteAsync(
        Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>([]);

    Task<IReadOnlyList<LabourRateCard>> ILabourRateCardStore.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(LabourRateCards);

    Task<LabourRateCard> ILabourRateCardStore.SaveAsync(
        SaveLabourRateCardRequest request, CancellationToken cancellationToken) =>
        Refused<LabourRateCard>(nameof(ILabourRateCardStore));

    Task<IReadOnlyList<UnroadworthyReason>> IUnroadworthyReasonBankStore.ListAsync(
        string principalCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UnroadworthyReason>>([]);

    Task<UnroadworthyReason?> IUnroadworthyReasonBankStore.AddAsync(
        SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken) =>
        Refused<UnroadworthyReason?>(nameof(IUnroadworthyReasonBankStore));

    Task<SalvageMatrix?> IPrincipalSalvageMatrixQueries.GetForCaseAsync(
        Guid caseId, CancellationToken cancellationToken) =>
        Task.FromResult<SalvageMatrix?>(null);

    Task<CaseReportFreezeInputs?> ICaseReportSnapshotSource.GetAsync(
        Guid caseId,
        ActionActor actor,
        CaseWorkSelector work,
        ReportProjectionReuse? reuse,
        CancellationToken cancellationToken) =>
        Task.FromResult<CaseReportFreezeInputs?>(null);

    Task<CaseReportGenerationRecord?> ICaseReportGenerationStore.GetCurrentAsync(
        ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
        Task.FromResult<CaseReportGenerationRecord?>(null);

    Task<CaseReportFreezeResult> ICaseReportGenerationStore.FreezeAsync(
        FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken) =>
        Refused<CaseReportFreezeResult>(nameof(ICaseReportGenerationStore));

    Task<CaseReportGenerationRecord> ICaseReportGenerationStore.ConfirmArtifactAsync(
        ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken) =>
        Refused<CaseReportGenerationRecord>(nameof(ICaseReportGenerationStore));

    Task<CaseReportGenerationRecord> ICaseReportGenerationStore.RecordArtifactOutcomeAsync(
        RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken) =>
        Refused<CaseReportGenerationRecord>(nameof(ICaseReportGenerationStore));

    Task<CaseReportGenerationRecord?> ICaseReportGenerationStore.GetAsync(
        ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
        Refused<CaseReportGenerationRecord?>(nameof(ICaseReportGenerationStore));

    Task<CaseReportGenerationRecord> ICaseReportGenerationStore.GetForDeliveryAsync(
        SendCaseReportRequest request, CancellationToken cancellationToken) =>
        Refused<CaseReportGenerationRecord>(nameof(ICaseReportGenerationStore));

    Task<IReadOnlyList<CaseReportGenerationRecord>> ICaseReportGenerationStore.ListAsync(
        ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<CaseReportGenerationRecord>>(nameof(ICaseReportGenerationStore));

    Task<int> ICaseReportGenerationStore.MarkStaleAsync(
        Guid caseId, string reasonCode, CancellationToken cancellationToken) =>
        Refused<int>(nameof(ICaseReportGenerationStore));

    Task ICaseReportGenerationStore.RecordDraftPreviewedAsync(
        RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken) =>
        Refused<bool>(nameof(ICaseReportGenerationStore));

    Task<bool> ISendToAiControl.IsEnabledAsync(CancellationToken cancellationToken) =>
        Task.FromResult(true);

    Task<bool> ISendToAiControl.SetEnabledAsync(
        bool enabled, ActionActor actor, string? reason, string operationKey, CancellationToken cancellationToken) =>
        Refused<bool>(nameof(ISendToAiControl));

    Task<IReadOnlyList<CaseValuation>> IListCaseValuations.ExecuteAsync(
        Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CaseValuation>>([]);

    Task<IReadOnlyList<AppliedValuation>> IListAppliedValuations.ExecuteAsync(
        Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AppliedValuation>>([]);

    Task<IReadOnlyList<ValuationPreset>> IValuationPresetStore.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ValuationPresets);

    Task<ValuationPreset> IValuationPresetStore.SaveAsync(
        SaveValuationPresetRequest request, CancellationToken cancellationToken) =>
        Refused<ValuationPreset>(nameof(IValuationPresetStore));

    Task<ValuationPreset> IValuationPresetStore.RemoveAsync(
        RemoveValuationPresetRequest request, CancellationToken cancellationToken) =>
        Refused<ValuationPreset>(nameof(IValuationPresetStore));

    /// <summary>The page offers claim sources and repairers; no migration seeds either.</summary>
    Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.ListByRoleAsync(
        ActionActor actor, ContactRole role, CancellationToken cancellationToken) =>
        role is ContactRole.ClaimSource or ContactRole.Repairer
            ? Task.FromResult<IReadOnlyList<ContactDirectoryRecord>>([])
            : Refused<IReadOnlyList<ContactDirectoryRecord>>(nameof(IContactDirectoryQueries));

    Task<ContactDirectoryRecord?> IContactDirectoryQueries.GetAsync(
        ActionActor actor, Guid organizationId, CancellationToken cancellationToken) =>
        Refused<ContactDirectoryRecord?>(nameof(IContactDirectoryQueries));

    Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.ListAsync(
        ContactDirectoryQuery query, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<ContactDirectoryRecord>>(nameof(IContactDirectoryQueries));

    Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.FindPossibleMatchesAsync(
        ActionActor actor, string name, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<ContactDirectoryRecord>>(nameof(IContactDirectoryQueries));

    Task<IReadOnlyList<PrincipalAdministrationDetails>> IContactDirectoryQueries.ListPrincipalChoicesAsync(
        ActionActor actor, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<PrincipalAdministrationDetails>>(nameof(IContactDirectoryQueries));

    Task<CaseEditLease?> IResumeCaseEditLease.ExecuteAsync(
        ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<CaseEditLease?>(null);

    Task<IReadOnlyList<ImageTag>> IReadImageTagVocabulary.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ImageTags);

    Task<IReadOnlyList<ImageIntakeSummary>> IImageIntakeQueries.ListForCaseAsync(
        Guid caseId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

    /// <summary>The page asks for the images of the intakes it listed, and it listed none.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>> IImageIntakeQueries.ListImagesAsync(
        IReadOnlyCollection<Guid> imageIntakeIds, CancellationToken cancellationToken) =>
        imageIntakeIds.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>>(
                new Dictionary<Guid, IReadOnlyList<ImageIntakeImage>>())
            : Refused<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>>(nameof(IImageIntakeQueries));

    Task<IReadOnlyList<ImageIntakeSummary>> IImageIntakeQueries.ListAsync(
        bool? associated, ImageInitiatedCaseState? state, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<ImageIntakeSummary>>(nameof(IImageIntakeQueries));

    Task<ImageIntakeDetail?> IImageIntakeQueries.GetAsync(Guid id, CancellationToken cancellationToken) =>
        Refused<ImageIntakeDetail?>(nameof(IImageIntakeQueries));

    Task<ImageIntakeDetail?> IImageIntakeQueries.GetByReferenceAsync(
        string imageIntakeReference, CancellationToken cancellationToken) =>
        Refused<ImageIntakeDetail?>(nameof(IImageIntakeQueries));

    Task<ImageIntakeDetail?> IImageIntakeQueries.GetByOriginReceiptAsync(
        Guid intakeReceiptId, CancellationToken cancellationToken) =>
        Refused<ImageIntakeDetail?>(nameof(IImageIntakeQueries));

    Task<IReadOnlyList<ImageIntakeSummary>> IImageIntakeQueries.SearchByRegistrationAsync(
        string normalizedVehicleRegistration, CancellationToken cancellationToken) =>
        Refused<IReadOnlyList<ImageIntakeSummary>>(nameof(IImageIntakeQueries));

    private static ValuationPreset SeededPreset(string id, string label, decimal suggestedAmount) =>
        new(new Guid(id), label, suggestedAmount, true, 1, "system:v1-foundation", DateTimeOffset.UnixEpoch);

    private static Task<T> Refused<T>(string port) =>
        Task.FromException<T>(new NotSupportedException(
            $"A database-less test host answers only the Case page's reads of {port}; keep this test on the database."));
}
