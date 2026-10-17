using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

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
/// matrix, report, valuation, image intake or lease to resume; Send to AI
/// switched on, because no row says otherwise; and the reference rows the
/// migrations seed (the labour-rate cards, the valuation presets, the image-tag
/// vocabulary, the claim sources and the repairers). Those rows are read once
/// per test run from a restored, initialised database (<see cref="Instance"/>),
/// so no copy of a seed drifts from it; no database-less host opens that
/// connection. <c>DatabaseLessWebHostTests</c> holds every answer to the
/// migrated database's. A read the Case page does not make is refused rather
/// than guessed.
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
    private static readonly Lazy<Task<DatabaseLessCaseReads>> Shared = new(() => Task.Run(ReadSeededRowsAsync));

    private readonly IReadOnlyList<LabourRateCard> labourRateCards;
    private readonly IReadOnlyList<ValuationPreset> valuationPresets;
    private readonly IReadOnlyList<ImageTag> imageTags;
    private readonly IReadOnlyList<ContactDirectoryRecord> claimSources;
    private readonly IReadOnlyList<ContactDirectoryRecord> repairers;

    private DatabaseLessCaseReads(
        IReadOnlyList<LabourRateCard> labourRateCards,
        IReadOnlyList<ValuationPreset> valuationPresets,
        IReadOnlyList<ImageTag> imageTags,
        IReadOnlyList<ContactDirectoryRecord> claimSources,
        IReadOnlyList<ContactDirectoryRecord> repairers)
    {
        this.labourRateCards = labourRateCards;
        this.valuationPresets = valuationPresets;
        this.imageTags = imageTags;
        this.claimSources = claimSources;
        this.repairers = repairers;
    }

    /// <summary>The run's one instance; the first use reads the seeded rows.</summary>
    public static DatabaseLessCaseReads Instance => Shared.Value.GetAwaiter().GetResult();

    private static async Task<DatabaseLessCaseReads> ReadSeededRowsAsync()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var administrator = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var contacts = services.GetRequiredService<IContactDirectoryQueries>();
        return new(
            await services.GetRequiredService<ILabourRateCardStore>().ListAsync(CancellationToken.None),
            await services.GetRequiredService<IValuationPresetStore>().ListAsync(CancellationToken.None),
            await services.GetRequiredService<IReadImageTagVocabulary>().ListAsync(CancellationToken.None),
            await contacts.ListByRoleAsync(administrator, ContactRole.ClaimSource, CancellationToken.None),
            await contacts.ListByRoleAsync(administrator, ContactRole.Repairer, CancellationToken.None));
    }

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
        Task.FromResult(labourRateCards);

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
        Task.FromResult(valuationPresets);

    Task<ValuationPreset> IValuationPresetStore.SaveAsync(
        SaveValuationPresetRequest request, CancellationToken cancellationToken) =>
        Refused<ValuationPreset>(nameof(IValuationPresetStore));

    Task<ValuationPreset> IValuationPresetStore.RemoveAsync(
        RemoveValuationPresetRequest request, CancellationToken cancellationToken) =>
        Refused<ValuationPreset>(nameof(IValuationPresetStore));

    /// <summary>The page offers the claim sources and the repairers.</summary>
    Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.ListByRoleAsync(
        ActionActor actor, ContactRole role, CancellationToken cancellationToken) => role switch
        {
            ContactRole.ClaimSource => Task.FromResult(claimSources),
            ContactRole.Repairer => Task.FromResult(repairers),
            _ => Refused<IReadOnlyList<ContactDirectoryRecord>>(nameof(IContactDirectoryQueries)),
        };

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
        Task.FromResult(imageTags);

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

    private static Task<T> Refused<T>(string port) =>
        Task.FromException<T>(new NotSupportedException(
            $"A database-less test host answers only the Case page's reads of {port}; keep this test on the database."));
}
