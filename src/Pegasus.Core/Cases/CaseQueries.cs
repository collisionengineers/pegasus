using System.Globalization;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Cases;

public sealed record CaseSearchFilters(
    string? CaseReference = null,
    string? Registration = null,
    string? Claimant = null,
    string? ClaimNumber = null,
    string? Principal = null,
    CaseLifecycleState? State = null,
    Guid? EngineerId = null,
    DateOnly? ReceivedDate = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Origin = null,
    string? Query = null,
    bool IncludeTriage = false);

/// <summary>
/// The sort a case list renders in. Newest received first is the default
/// everywhere; the rest are the sortable columns, each in both directions.
/// </summary>
public enum CaseSearchOrder
{
    ReceivedDesc,
    ReceivedAsc,
    ReferenceAsc,
    ReferenceDesc,
    RegistrationAsc,
    RegistrationDesc,
    ClaimantAsc,
    ClaimantDesc,
    PrincipalAsc,
    PrincipalDesc
}

public sealed record SearchCasesQuery(
    ActionActor Actor,
    CaseSearchFilters Filters,
    int Page = 1,
    int PageSize = 25,
    CaseSearchOrder Order = CaseSearchOrder.ReceivedDesc);

/// <summary>
/// One case as a list row. <see cref="VehicleMake"/>, <see cref="VehicleModel"/>
/// and <see cref="AccidentCircumstances"/> ride the same projection so the
/// Search page can draw its vehicle column and selected-case preview from the
/// search read alone; they are display facts, not filters.
/// </summary>
public sealed record CaseSearchItem(
    Guid CaseId,
    string Reference,
    string? AuditReference,
    CaseType CaseType,
    string Principal,
    CaseLifecycleState State,
    Guid? EngineerId,
    string? Registration,
    string? Claimant,
    string? ClaimNumber,
    DateTimeOffset ReceivedAtUtc,
    string Origin,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? NextChaseAtUtc = null,
    string? VehicleMake = null,
    string? VehicleModel = null,
    string? AccidentCircumstances = null)
{
    /// <summary>
    /// The case's recorded completeness facts (<see cref="CaseCompleteness"/>),
    /// so a Not ready list can say what each case is still missing without a
    /// second query per row. Null when the store did not project them.
    /// </summary>
    public bool? InstructionComplete { get; init; }

    public bool? ImagesComplete { get; init; }

    /// <summary>The hold's review date when the Case is Held with one, so a list can read "Held · review on 24 Sep".</summary>
    public DateOnly? HoldReviewOn { get; init; }

    /// <summary>
    /// The member of staff whose edit lease on the Case is live, so a list can
    /// say who is editing it before anyone opens it; null when nobody is.
    /// </summary>
    public Guid? EditingStaffId { get; init; }

    /// <summary>When the current hold was placed; null unless the Case is Held.</summary>
    public DateTimeOffset? HeldAtUtc { get; init; }

    /// <summary>When the Case entered its current state, so Review ageing counts from the transition.</summary>
    public DateTimeOffset? StateEnteredAtUtc { get; init; }

    /// <summary>
    /// The Triage state of a Triage Case row (<see cref="CaseType"/> is
    /// <see cref="CaseType.Triage"/>), which has no Case workflow: the
    /// positional <see cref="State"/> is unused for such a row. Null for
    /// every other Case. Rows of Triage Cases appear only when the search asks
    /// for them (<see cref="CaseSearchFilters.IncludeTriage"/>).
    /// </summary>
    public Pegasus.Core.Triage.TriageState? TriageState { get; init; }
}

public sealed record SearchCasesResult(
    IReadOnlyList<CaseSearchItem> Items,
    int Page,
    int PageSize,
    bool HasPreviousPage,
    bool HasNextPage);

/// <summary>
/// A live lease as other readers see it. <paramref name="HolderKind"/> is null only for a lease
/// retained before the holder's kind was recorded; such a holder is nobody's and stays read-only
/// to every actor until the lease expires.
/// </summary>
public sealed record CaseEditLeaseSnapshot(
    string Holder,
    ActorKind? HolderKind,
    DateTimeOffset ExpiresAtUtc,
    string OperationKey,
    long Generation = 0);

public sealed record CaseGuidanceEntry(string EventType, string OrganizationName, long TemplateVersion, string Text);

public sealed record CaseHistoryEntry(
    string EventType,
    string Actor,
    string ActorKind,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    long BeforeVersion,
    long AfterVersion)
{
    /// <summary>
    /// The operator-facing name for <see cref="Actor"/>, resolved by <c>GetCase</c>
    /// (see <see cref="ActorDisplayNames"/>). Defaults to the
    /// same honest "not yet resolved" fallback a missing account gets, so a caller
    /// that forgets to populate it never renders the raw subject id.
    /// </summary>
    public string ActorDisplayName { get; init; } = ActorDisplayNames.UnknownStaff;

    /// <summary>
    /// The persisted event's own stable identifier, appended so
    /// existing positional construction keeps compiling. Populated by the
    /// store from the row it read the entry from; a history entry built any
    /// other way (e.g. in a test fake) never had one and stays
    /// <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid EntryId { get; init; }
    public IReadOnlyList<CaseGuidanceEntry> Guidance { get; init; } = [];
}

public sealed record CaseCorrespondenceEmail(
    Guid RetainedMessageId,
    DateTimeOffset ReceivedAtUtc,
    string? EffectiveSenderAddress,
    string? SenderDisplayName,
    string? SenderAddress,
    string? Subject,
    MailCategory? Classification,
    string? SourceSha256 = null);

public sealed record CaseDetails(
    CaseSearchItem Summary,
    CaseWorkflowRecord Workflow,
    CaseEditLeaseSnapshot? ActiveEditLease,
    IReadOnlyList<CaseDocument> Documents,
    string? CustodyFolderRemoteId,
    CaseCustodyState CustodyState,
    IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence,
    IReadOnlyList<CaseHistoryEntry> History)
{
    public CaseDataProjection? Data { get; init; }
    public IReadOnlyList<CaseTaskRecord> Tasks { get; init; } = [];
    public GeneratedCaseChaser? LatestChaser { get; init; }
    public CaseVehicleEvidence? VehicleEvidence { get; init; }
    public IReadOnlyList<CaseCustodyPreparation> Custody { get; init; } = [];
    public IReadOnlyList<CaseCorrespondenceEmail> CorrespondenceEmails { get; init; } = [];

    /// <summary>
    /// The Principal record's and the Claim source record's "Notes on every Case", read
    /// live from the records when the Case is read and never copied onto it, so a
    /// change to a record shows on every Case at once. Absent when a record has none.
    /// </summary>
    public CaseRecordNotes RecordNotes { get; init; } = CaseRecordNotes.None;
}

/// <summary>The record-level notes shown read-only on a Case's Overview beside the Case's own.</summary>
public sealed record CaseRecordNotes(string? PrincipalNotes, string? ClaimSourceNotes)
{
    public static readonly CaseRecordNotes None = new(null, null);
}

public sealed record GetCaseQuery(Guid CaseId, ActionActor Actor);

/// <summary>
/// A bounded read of a case: everything
/// <see cref="GetCaseQuery"/> reads except the document, history and task
/// lists, which collapse to their counts so a host that only needs to know
/// how much a case carries never pays to read it all.
/// </summary>
public sealed record GetCaseHeaderQuery(Guid CaseId, ActionActor Actor);

/// <summary>
/// The common identity, workflow, live-lease and custody-state facts every
/// lazily rendered Case section needs. It deliberately has no body
/// collections: each section supplies only its own content.
/// </summary>
public sealed record CaseSectionFrame(
    CaseSearchItem Summary,
    CaseWorkflowRecord Workflow,
    CaseEditLeaseSnapshot? ActiveEditLease,
    string? CustodyFolderRemoteId = null,
    CaseCustodyState CustodyState = CaseCustodyState.Pending,
    CaseWorkSet? Works = null);

/// <summary>
/// The already-authorized page inputs a directly rendered body may reuse. A
/// fragment leaves these absent and obtains the same content through its
/// focused reader. A supplied frame is request-local and must belong to the
/// requested Case.
/// </summary>
public sealed record GetCaseSectionQuery(
    Guid CaseId,
    ActionActor Actor,
    AssessmentWorkspace? AssessmentWorkspace = null,
    bool HasAssessmentWorkspace = false,
    CaseDataProjection? Data = null,
    IReadOnlyList<CaseDocument>? Documents = null,
    CaseSectionFrame? Frame = null,
    CaseWorkSelector Work = CaseWorkSelector.Current);

/// <summary>
/// The Case page's first-response frame. It keeps the identity, workflow,
/// accepted data and report files that the permanently rendered page consumes,
/// but deliberately excludes deferred section bodies.
/// </summary>
public sealed record CasePageFrame(
    CaseSectionFrame Frame,
    IReadOnlyList<CaseDocument> Documents,
    IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence,
    CaseRecordNotes RecordNotes,
    CaseDataProjection Data)
{
    public CaseSearchItem Summary => Frame.Summary;
    public CaseWorkflowRecord Workflow => Frame.Workflow;
    public CaseEditLeaseSnapshot? ActiveEditLease => Frame.ActiveEditLease;
}

/// <summary>The persistence half of <see cref="CasePageFrame"/>.</summary>
public sealed record CasePageFrameData(
    CaseSectionFrame Frame,
    IReadOnlyList<CaseDocument> Documents,
    IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence,
    CaseRecordNotes RecordNotes);

public sealed record CaseVehicleSection(
    CaseSectionFrame Frame,
    CaseDataProjection Data,
    VehicleLookupObservation? LatestVehicleObservation,
    CaseAssessmentProjection? Assessment);

public sealed record CaseValuationSection(
    CaseSectionFrame Frame,
    CaseDataProjection Data,
    CaseAssessmentProjection? Assessment);

public sealed record CaseNotesSection(
    CaseSectionFrame Frame,
    IReadOnlyList<CaseHistoryEntry> History);

/// <summary>
/// The Files body source. Documents and correspondence belong together because
/// the section renders them together; history, tasks and unrelated Case bodies do not.
/// </summary>
public sealed record CaseFilesSection(
    CaseSectionFrame Frame,
    IReadOnlyList<CaseDocument> Documents,
    string? CustodyFolderRemoteId,
    CaseCustodyState CustodyState,
    IReadOnlyList<CaseCorrespondenceEmail> CorrespondenceEmails,
    Guid? StandaloneAuditEvidenceId = null,
    CaseCustodyState? AuditCustodyState = null,
    string? AuditCustodyFolderRemoteId = null);

/// <summary>
/// Persistence material used only to prove a fragment's render-only lease
/// header. It never exposes the retained token or token hash.
/// </summary>
public sealed record CaseRenderLeaseValidation(
    Guid CaseId,
    long CaseVersion,
    ActorKind? HolderKind,
    string? Holder,
    DateTimeOffset? ExpiresAtUtc,
    bool HasTokenHash,
    bool TokenMatches);

public sealed record CaseHeader(
    CaseSearchItem Summary,
    CaseWorkflowRecord Workflow,
    CaseEditLeaseSnapshot? ActiveEditLease,
    int DocumentCount,
    int HistoryCount,
    int OpenTaskCount,
    CaseWorkSet? Works = null);

public interface ICaseQueryStore
{
    Task<SearchCasesResult> SearchAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken);

    Task<CaseDetails?> GetAsync(
        GetCaseQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// The bounded sibling of <see cref="GetAsync"/>: the same summary/workflow/edit-lease facts, with the case's
    /// document, history and open-task lists reduced to their counts instead
    /// of materializing every row.
    /// </summary>
    Task<CaseHeader?> GetHeaderAsync(
        GetCaseHeaderQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// The frame required by a Case body. Unlike <see cref="GetHeaderAsync"/>,
    /// it does not count document, history or task rows.
    /// </summary>
    Task<CaseSectionFrame?> GetSectionFrameAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    /// <summary>Reads the non-deferred body of the initial Case page.</summary>
    Task<CasePageFrameData?> GetPageFrameAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    /// <summary>Reads only the ordered history body used by Notes.</summary>
    Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the document-oriented body used by Files. A caller that already
    /// holds a Case-bound frame supplies it so this reader does not reread the
    /// workflow and summary.
    /// </summary>
    Task<CaseFilesSectionData?> GetFilesSectionAsync(
        Guid caseId,
        bool includeDocuments,
        CaseSectionFrame? frame,
        CancellationToken cancellationToken);

    /// <summary>Gets only the material needed to prove a render-only lease header.</summary>
    Task<CaseRenderLeaseValidation?> GetRenderLeaseValidationAsync(
        Guid caseId,
        string presentedToken,
        CancellationToken cancellationToken);

    /// <summary>Returns display references for a bounded set of Cases.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetReferencesAsync(
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The keyset-paged sibling of <see cref="SearchAsync"/>. The
    /// after-values are the decoded cursor's sort position: <paramref
    /// name="afterReceivedAtUtc"/> for the two received-date orders,
    /// <paramref name="afterSortText"/> for every text-column order; both
    /// null on the first page. <paramref name="fetchCount"/> is the caller's
    /// limit plus one, so it can tell whether another page follows.
    /// </summary>
    Task<IReadOnlyList<CaseSearchItem>> SearchByCursorAsync(
        CaseSearchFilters filters,
        CaseSearchOrder order,
        DateTimeOffset? afterReceivedAtUtc,
        string? afterSortText,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// A case's document occurrences, newest recorded first then occurrence
    /// id. The row unit is the occurrence —
    /// not the document — so a document carrying more occurrences than the
    /// caller's limit still enumerates every one of them across consecutive
    /// pages; a document-unit page cannot split one document's occurrences.
    /// <paramref name="afterRecordedAtUtc"/>/<paramref name="afterId"/> are
    /// the decoded cursor's sort position, both null on the first page.
    /// </summary>
    Task<IReadOnlyList<CaseDocumentPageItem>> ListDocumentsByCursorAsync(
        Guid caseId,
        DateTimeOffset? afterRecordedAtUtc,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// A case's history, newest event first then entry id.
    /// <paramref name="afterOccurredAtUtc"/>/<paramref name="afterId"/> are
    /// the decoded cursor's sort position, both null on the first page.
    /// </summary>
    Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryByCursorAsync(
        Guid caseId,
        DateTimeOffset? afterOccurredAtUtc,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken);
}

public interface ISearchCases
{
    Task<SearchCasesResult> ExecuteAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken);
}

public interface IGetCase
{
    Task<CaseDetails?> ExecuteAsync(
        GetCaseQuery query,
        CancellationToken cancellationToken);
}

public interface IGetCaseHeader
{
    Task<CaseHeader?> ExecuteAsync(
        GetCaseHeaderQuery query,
        CancellationToken cancellationToken);
}

public interface IGetCasePageFrame
{
    Task<CasePageFrame?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// The persistence half of <see cref="CaseFilesSection"/>. It contains only
/// Files-owned rows; the page frame supplies the already-rendered documents.
/// </summary>
/// <remarks>
/// <see cref="AuditCustodyState"/> is the Audit's <c>a.</c> folder, present
/// only once the Case has an Audit work.
/// </remarks>
public sealed record CaseFilesSectionData(
    CaseSectionFrame Frame,
    IReadOnlyList<CaseDocument> Documents,
    string? CustodyFolderRemoteId,
    CaseCustodyState CustodyState,
    IReadOnlyList<CaseCorrespondenceEmail> CorrespondenceEmails,
    Guid? StandaloneAuditEvidenceId = null,
    CaseCustodyState? AuditCustodyState = null,
    string? AuditCustodyFolderRemoteId = null);

public interface IGetCaseVehicleSection
{
    Task<CaseVehicleSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken);
}

public interface IGetCaseValuationSection
{
    Task<CaseValuationSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken);
}

public interface IGetCaseNotesSection
{
    Task<CaseNotesSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken);
}

public interface IGetCaseFilesSection
{
    Task<CaseFilesSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken);
}

public sealed record ValidateCaseRenderLeaseQuery(Guid CaseId, ActionActor Actor, string Token);

public interface IValidateCaseRenderLease
{
    Task<bool> ExecuteAsync(ValidateCaseRenderLeaseQuery query, CancellationToken cancellationToken);
}

public sealed class GetCasePageFrame(
    ICaseQueryStore store,
    ICaseDataQueries caseDataQueries) : IGetCasePageFrame
{
    public async Task<CasePageFrame?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken)
    {
        CaseSectionQueries.Validate(query);
        var frame = await store.GetPageFrameAsync(query.CaseId, cancellationToken);
        if (frame is null)
        {
            return null;
        }

        var data = await caseDataQueries.GetAsync(query.CaseId, query.Work, cancellationToken)
            ?? throw new InvalidDataException("The accepted case is missing its typed data projection.");
        return new(
            frame.Frame,
            frame.Documents,
            frame.AvailableReportSentEvidence,
            frame.RecordNotes,
            data);
    }
}

public sealed class GetCaseVehicleSection(
    ICaseQueryStore store,
    IGetAssessmentWorkspace workspaces,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries) : IGetCaseVehicleSection
{
    public async Task<CaseVehicleSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken)
    {
        CaseSectionQueries.Validate(query);
        var frame = query.Frame ?? await store.GetSectionFrameAsync(query.CaseId, cancellationToken);
        if (frame is null)
        {
            return null;
        }

        var workspace = await CaseSectionQueries.WorkspaceAsync(query, workspaces, cancellationToken);
        var data = workspace?.Data ?? query.Data ?? await caseDataQueries.GetAsync(query.CaseId, query.Work, cancellationToken)
            ?? throw new InvalidDataException("The accepted case is missing its typed data projection.");
        var evidence = workspace?.LatestVehicleObservation
            ?? (await vehicleEvidenceQueries.GetAsync(query.CaseId, cancellationToken))?.LatestObservation;
        return new(frame, data, evidence, workspace?.Assessment);
    }
}

public sealed class GetCaseValuationSection(
    ICaseQueryStore store,
    IGetAssessmentWorkspace workspaces,
    ICaseDataQueries caseDataQueries) : IGetCaseValuationSection
{
    public async Task<CaseValuationSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken)
    {
        CaseSectionQueries.Validate(query);
        var frame = query.Frame ?? await store.GetSectionFrameAsync(query.CaseId, cancellationToken);
        if (frame is null)
        {
            return null;
        }

        var workspace = await CaseSectionQueries.WorkspaceAsync(query, workspaces, cancellationToken);
        var data = workspace?.Data ?? query.Data ?? await caseDataQueries.GetAsync(query.CaseId, query.Work, cancellationToken)
            ?? throw new InvalidDataException("The accepted case is missing its typed data projection.");
        return new(frame, data, workspace?.Assessment);
    }
}

public sealed class GetCaseNotesSection(
    ICaseQueryStore store,
    IStaffAccountQueries staffAccounts) : IGetCaseNotesSection
{
    public async Task<CaseNotesSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken)
    {
        CaseSectionQueries.Validate(query);
        var frame = query.Frame ?? await store.GetSectionFrameAsync(query.CaseId, cancellationToken);
        if (frame is null)
        {
            return null;
        }

        var history = await store.ListHistoryAsync(query.CaseId, cancellationToken);
        var staffIds = history
            .Where(entry => entry.ActorKind == nameof(ActorKind.Staff) && Guid.TryParse(entry.Actor, out _))
            .Select(entry => Guid.Parse(entry.Actor));
        var names = await ActorDisplayNames.ResolveStaffNamesAsync(staffAccounts, staffIds, cancellationToken);
        return new(frame, history.Select(entry => entry with
        {
            ActorDisplayName = Enum.TryParse<ActorKind>(entry.ActorKind, out var kind)
                ? ActorDisplayNames.Resolve(kind, entry.Actor, names)
                : ActorDisplayNames.UnknownStaff
        }).ToArray());
    }
}

public sealed class GetCaseFilesSection(ICaseQueryStore store) : IGetCaseFilesSection
{
    public async Task<CaseFilesSection?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken)
    {
        CaseSectionQueries.Validate(query);
        var body = await store.GetFilesSectionAsync(
            query.CaseId,
            query.Documents is null,
            query.Frame,
            cancellationToken);
        if (body is null)
        {
            return null;
        }

        return new(body.Frame, query.Documents ?? body.Documents, body.CustodyFolderRemoteId,
            body.CustodyState, body.CorrespondenceEmails,
            body.StandaloneAuditEvidenceId,
            body.AuditCustodyState, body.AuditCustodyFolderRemoteId);
    }
}

public sealed class ValidateCaseRenderLease(
    ICaseQueryStore store,
    TimeProvider timeProvider) : IValidateCaseRenderLease
{
    public async Task<bool> ExecuteAsync(ValidateCaseRenderLeaseQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty
            || string.IsNullOrWhiteSpace(query.Token)
            || query.Token.Length != CaseEditAuthority.LeaseTokenLength)
        {
            return false;
        }

        var validation = await store.GetRenderLeaseValidationAsync(
            query.CaseId, query.Token, cancellationToken);
        if (validation is null)
        {
            return false;
        }

        try
        {
            CaseEditAuthority.RequireLease(
                validation.CaseId,
                validation.CaseVersion,
                query.Actor,
                query.Token,
                validation.HolderKind,
                validation.Holder,
                validation.HasTokenHash,
                validation.ExpiresAtUtc,
                validation.TokenMatches,
                timeProvider.GetUtcNow());
            return true;
        }
        catch (CaseEditLeaseExpiredException)
        {
            return false;
        }
        catch (CaseEditLeaseConflictException)
        {
            return false;
        }
    }
}

public sealed record ListCaseReferencesQuery(ActionActor Actor, IReadOnlyCollection<Guid> CaseIds);

public interface IListCaseReferences
{
    Task<IReadOnlyDictionary<Guid, string>> ExecuteAsync(ListCaseReferencesQuery query, CancellationToken cancellationToken);
}

public sealed class ListCaseReferences(ICaseQueryStore store) : IListCaseReferences
{
    public Task<IReadOnlyDictionary<Guid, string>> ExecuteAsync(ListCaseReferencesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.CaseIds);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        var caseIds = query.CaseIds.Distinct().ToArray();
        if (caseIds.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "No more than 100 Case references can be read at once.");
        }
        if (caseIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("A Case identifier is required.", nameof(query));
        }
        return store.GetReferencesAsync(caseIds, cancellationToken);
    }
}

internal static class CaseSectionQueries
{
    public static void Validate(GetCaseSectionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }
        if (query.AssessmentWorkspace is { Header.CaseId: var workspaceCaseId }
            && workspaceCaseId != query.CaseId)
        {
            throw new ArgumentException("The Assessment workspace belongs to another Case.", nameof(query));
        }
        if (query.Data is { Identity.CaseId: var dataCaseId } && dataCaseId != query.CaseId)
        {
            throw new ArgumentException("The Case data belongs to another Case.", nameof(query));
        }
        if (query.Frame is { } frame
            && (frame.Summary.CaseId != query.CaseId || frame.Workflow.CaseId != query.CaseId))
        {
            throw new ArgumentException("The Case section frame belongs to another Case.", nameof(query));
        }
    }

    public static Task<AssessmentWorkspace?> WorkspaceAsync(
        GetCaseSectionQuery query,
        IGetAssessmentWorkspace workspaces,
        CancellationToken cancellationToken) =>
        query.HasAssessmentWorkspace
            ? Task.FromResult(query.AssessmentWorkspace)
            : workspaces.ExecuteAsync(new(query.CaseId, query.Actor, query.Work), cancellationToken);

}

/// <summary>
/// The bounded sibling of <see cref="GetCase"/>:
/// the same actor boundary and case-identifier validation, delegated
/// straight to the store's counted read.
/// </summary>
public sealed class GetCaseHeader(ICaseQueryStore store) : IGetCaseHeader
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseHeader?> ExecuteAsync(
        GetCaseHeaderQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }
        return _store.GetHeaderAsync(query, cancellationToken);
    }
}

public static class CaseRegistration
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = string.Concat(value.Trim().Where(char.IsLetterOrDigit)).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }
}

/// <summary>
/// The filter/order validation and normalization <see cref="SearchCases"/>
/// and <see cref="SearchCasesByCursor"/> share, so the two search
/// entry points can never drift into two rules for what a valid filter is.
/// </summary>
internal static class CaseSearchQueryValidation
{
    public static CaseSearchFilters ValidateAndNormalize(CaseSearchFilters filters, CaseSearchOrder order)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.EngineerId == Guid.Empty)
        {
            throw new ArgumentException("The Engineer filter is invalid.", nameof(filters));
        }
        if (filters.State is { } state && !Enum.IsDefined(state))
        {
            throw new ArgumentException("The lifecycle-state filter is invalid.", nameof(filters));
        }
        if (!Enum.IsDefined(order))
        {
            throw new ArgumentException("The sort order is invalid.", nameof(order));
        }
        if (filters.FromDate is { } fromDate
            && filters.ToDate is { } toDate
            && fromDate > toDate)
        {
            throw new ArgumentException("The start date cannot be after the end date.", nameof(filters));
        }

        return filters with
        {
            CaseReference = Normalize(filters.CaseReference, 100, nameof(CaseSearchFilters.CaseReference)),
            Registration = NormalizeRegistration(filters.Registration),
            Claimant = Normalize(filters.Claimant, 300, nameof(CaseSearchFilters.Claimant)),
            ClaimNumber = Normalize(filters.ClaimNumber, 100, nameof(CaseSearchFilters.ClaimNumber)),
            Principal = Normalize(filters.Principal, 20, nameof(CaseSearchFilters.Principal))?.ToUpperInvariant(),
            Origin = Normalize(filters.Origin, 100, nameof(CaseSearchFilters.Origin)),
            Query = Normalize(filters.Query, 300, nameof(CaseSearchFilters.Query))
        };
    }

    private static string? Normalize(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The filter cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeRegistration(string? value)
    {
        var normalized = Normalize(value, 20, nameof(CaseSearchFilters.Registration));
        if (normalized is null)
        {
            return null;
        }

        var compact = CaseRegistration.Normalize(normalized);
        if (compact is null)
        {
            throw new ArgumentException("The registration filter is invalid.", nameof(value));
        }

        return compact;
    }
}

public sealed class SearchCases(ICaseQueryStore store) : ISearchCases
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<SearchCasesResult> ExecuteAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.Page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The requested page is outside the supported range.");
        }
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The requested page size is outside the supported range.");
        }

        var normalized = query with
        {
            Filters = CaseSearchQueryValidation.ValidateAndNormalize(query.Filters, query.Order)
        };

        return _store.SearchAsync(normalized, cancellationToken);
    }
}

public sealed class GetCase(
    ICaseQueryStore store,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries,
    ICaseCustodyQueries caseCustodyQueries,
    ICaseDueChaserQueries dueChaserQueries,
    ICaseTaskQueries taskQueries,
    IStaffAccountQueries staffAccountQueries) : IGetCase
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseDataQueries _caseDataQueries =
        caseDataQueries ?? throw new ArgumentNullException(nameof(caseDataQueries));
    private readonly IVehicleEvidenceQueries _vehicleEvidenceQueries =
        vehicleEvidenceQueries ?? throw new ArgumentNullException(nameof(vehicleEvidenceQueries));
    private readonly ICaseCustodyQueries _caseCustodyQueries =
        caseCustodyQueries ?? throw new ArgumentNullException(nameof(caseCustodyQueries));
    private readonly ICaseDueChaserQueries _dueChaserQueries =
        dueChaserQueries ?? throw new ArgumentNullException(nameof(dueChaserQueries));
    private readonly ICaseTaskQueries _taskQueries =
        taskQueries ?? throw new ArgumentNullException(nameof(taskQueries));
    private readonly IStaffAccountQueries _staffAccountQueries =
        staffAccountQueries ?? throw new ArgumentNullException(nameof(staffAccountQueries));

    public async Task<CaseDetails?> ExecuteAsync(
        GetCaseQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }

        var details = await _store.GetAsync(query, cancellationToken);
        if (details is null)
        {
            return null;
        }

        var data = await _caseDataQueries.GetAsync(query.CaseId, CaseWorkSelector.Current, cancellationToken)
            ?? throw new InvalidDataException("The accepted case is missing its typed data projection.");
        var vehicleEvidence = await _vehicleEvidenceQueries.GetAsync(query.CaseId, cancellationToken);
        var custody = await _caseCustodyQueries.GetPreparationsAsync(query.CaseId, cancellationToken);
        var latestChaser = await _dueChaserQueries.GetLatestAsync(query.CaseId, cancellationToken);
        var tasks = await _taskQueries.ListAsync(query.CaseId, cancellationToken);
        if (data.Identity.CaseId != details.Workflow.CaseId
            || vehicleEvidence is not null && vehicleEvidence.CaseId != details.Workflow.CaseId
            || latestChaser is not null && latestChaser.CaseId != details.Workflow.CaseId
            || tasks.Any(item => item.CaseId != details.Workflow.CaseId))
        {
            throw new InvalidDataException("A composed case projection belongs to another case.");
        }

        var staffIds = details.History
            .Where(entry => entry.ActorKind == nameof(ActorKind.Staff) && Guid.TryParse(entry.Actor, out _))
            .Select(entry => Guid.Parse(entry.Actor));
        var staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            _staffAccountQueries,
            staffIds,
            cancellationToken);

        return details with
        {
            Data = data,
            VehicleEvidence = vehicleEvidence,
            Custody = custody,
            LatestChaser = latestChaser,
            Tasks = tasks,
            History = details.History
                .Select(entry => entry with
                {
                    ActorDisplayName = Enum.TryParse<ActorKind>(entry.ActorKind, out var actorKind)
                        ? ActorDisplayNames.Resolve(actorKind, entry.Actor, staffNames)
                        : ActorDisplayNames.UnknownStaff
                })
                .ToArray()
        };
    }
}

// --- Stable cursor continuations ----------------------------------------

// Cursor page, limit and rejection primitives are the shared Pegasus.Core
// CursorPage<T>, CursorPaging and CursorRejectedException (G9); this file adds the Case queries.


/// <summary>
/// The one page-assembly rule every Case cursor query shares: a store is
/// asked for one row more than the limit, and that extra row is what says
/// another page follows. It is dropped from the returned items, and the last
/// kept row mints the next cursor through the shared <see
/// cref="ICursorProtector"/> (G9); when it is absent the page is the last one
/// and carries no cursor.
/// </summary>
internal static class CursorPageBuilder
{
    public static CursorPage<T> Build<T>(
        IReadOnlyList<T> rows,
        int limit,
        ICursorProtector protector,
        string scope,
        Func<T, string> sortKeyOf,
        Func<T, Guid> idOf)
    {
        if (rows.Count <= limit)
        {
            return new(rows, null);
        }

        var items = rows.Take(limit).ToArray();
        var last = items[^1];
        return new(items, protector.Protect(scope, sortKeyOf(last), idOf(last)));
    }
}

/// <summary>
/// A stable-cursor sibling of <see cref="SearchCasesQuery"/> (requested
/// by the MCP adapters). <see cref="Cursor"/> null starts
/// from the first page; <see cref="Limit"/> null takes
/// <see cref="CursorPaging.DefaultLimit"/>.
/// </summary>
public sealed record SearchCasesCursorQuery(
    ActionActor Actor,
    CaseSearchFilters Filters,
    CaseSearchOrder Order = CaseSearchOrder.ReceivedDesc,
    string? Cursor = null,
    int? Limit = null);

public interface ISearchCasesByCursor
{
    Task<CursorPage<CaseSearchItem>> ExecuteAsync(
        SearchCasesCursorQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// The keyset-paged sibling of <see cref="SearchCases"/>. Shares the same
/// authorization and filter/order validation
/// (<see cref="CaseSearchQueryValidation"/>) so the two search entry points
/// can never disagree about what a valid query is.
/// </summary>
public sealed class SearchCasesByCursor(ICaseQueryStore store, ICursorProtector protector) : ISearchCasesByCursor
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICursorProtector _protector = protector ?? throw new ArgumentNullException(nameof(protector));

    public async Task<CursorPage<CaseSearchItem>> ExecuteAsync(
        SearchCasesCursorQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        var limit = CursorPaging.NormalizeLimit(query.Limit);
        var filters = CaseSearchQueryValidation.ValidateAndNormalize(query.Filters, query.Order);

        var scope = CursorPaging.CreateScope(
            "SearchCases",
            query.Actor,
            filters.CaseReference,
            filters.Registration,
            filters.Claimant,
            filters.ClaimNumber,
            filters.Principal,
            filters.State?.ToString(),
            filters.EngineerId?.ToString(),
            InvariantDate(filters.ReceivedDate),
            InvariantDate(filters.FromDate),
            InvariantDate(filters.ToDate),
            filters.Origin,
            filters.Query,
            query.Order.ToString());

        DateTimeOffset? afterReceivedAtUtc = null;
        string? afterSortText = null;
        Guid? afterId = null;
        if (query.Cursor is { Length: > 0 } cursor)
        {
            var position = _protector.Unprotect(cursor, scope);
            if (IsReceivedDateOrder(query.Order))
            {
                afterReceivedAtUtc = CursorPaging.DecodeUtcTimestamp(position.SortKey);
            }
            else
            {
                afterSortText = position.SortKey;
            }
            afterId = position.Id;
        }

        var rows = await _store.SearchByCursorAsync(
            filters, query.Order, afterReceivedAtUtc, afterSortText, afterId, limit + 1, cancellationToken);

        return CursorPageBuilder.Build(
            rows,
            limit,
            _protector,
            scope,
            item => IsReceivedDateOrder(query.Order)
                ? CursorPaging.EncodeUtcTimestamp(item.ReceivedAtUtc)
                : SortTextOf(item, query.Order),
            item => item.CaseId);
    }

    private static bool IsReceivedDateOrder(CaseSearchOrder order) =>
        order is CaseSearchOrder.ReceivedAsc or CaseSearchOrder.ReceivedDesc;

    private static string SortTextOf(CaseSearchItem item, CaseSearchOrder order) => order switch
    {
        CaseSearchOrder.ReferenceAsc or CaseSearchOrder.ReferenceDesc => item.Reference,
        CaseSearchOrder.RegistrationAsc or CaseSearchOrder.RegistrationDesc => item.Registration ?? string.Empty,
        CaseSearchOrder.ClaimantAsc or CaseSearchOrder.ClaimantDesc => item.Claimant ?? string.Empty,
        CaseSearchOrder.PrincipalAsc or CaseSearchOrder.PrincipalDesc => item.Principal,
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "Unsupported cursor sort order.")
    };

    private static string? InvariantDate(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// One case's cursor-paged sub-list request: documents, history,
/// and (Estimates.cs) estimates all share this shape, so a caller only
/// learns one query record for every per-case list.
/// </summary>
public sealed record CaseListCursorQuery(ActionActor Actor, Guid CaseId, string? Cursor = null, int? Limit = null);

/// <summary>
/// One row of the cursor-paged case document list: a single occurrence paired with exactly the version it names.
/// A host that flattens a page item-for-item can never lose occurrences the
/// way a document-unit page does when one document carries more occurrences
/// than the limit — the occurrence is the page unit, so the version a host
/// shows beside it is the one that occurrence itself names, never "the
/// current one of the document".
/// </summary>
public sealed record CaseDocumentPageItem(
    DocumentOccurrence Occurrence,
    DocumentVersion Version);

public interface IListCaseDocumentsByCursor
{
    Task<CursorPage<CaseDocumentPageItem>> ExecuteAsync(
        CaseListCursorQuery query,
        CancellationToken cancellationToken);
}

public interface IListCaseHistoryByCursor
{
    Task<CursorPage<CaseHistoryEntry>> ExecuteAsync(
        CaseListCursorQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Applies the same actor boundary <see cref="GetCase"/> applies
/// (<see cref="StaffAccessRight.PerformCasework"/>) before reading a case's
/// documents, newest occurrence first then occurrence id.
/// </summary>
public sealed class ListCaseDocumentsByCursor(ICaseQueryStore store, ICursorProtector protector)
    : IListCaseDocumentsByCursor
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICursorProtector _protector = protector ?? throw new ArgumentNullException(nameof(protector));

    public async Task<CursorPage<CaseDocumentPageItem>> ExecuteAsync(
        CaseListCursorQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }
        var limit = CursorPaging.NormalizeLimit(query.Limit);
        var scope = CaseListCursorScope.For("ListCaseDocuments", query.Actor, query.CaseId);

        DateTimeOffset? afterRecordedAtUtc = null;
        Guid? afterId = null;
        if (query.Cursor is { Length: > 0 } cursor)
        {
            var position = _protector.Unprotect(cursor, scope);
            afterRecordedAtUtc = CursorPaging.DecodeUtcTimestamp(position.SortKey);
            afterId = position.Id;
        }

        var rows = await _store.ListDocumentsByCursorAsync(
            query.CaseId, afterRecordedAtUtc, afterId, limit + 1, cancellationToken);

        return CursorPageBuilder.Build(
            rows,
            limit,
            _protector,
            scope,
            item => CursorPaging.EncodeUtcTimestamp(item.Occurrence.RecordedAtUtc),
            item => item.Occurrence.Id);
    }
}

/// <summary>
/// Applies the same actor boundary <see cref="GetCase"/> applies before
/// reading a case's history, newest event first then entry id.
/// </summary>
public sealed class ListCaseHistoryByCursor(ICaseQueryStore store, ICursorProtector protector)
    : IListCaseHistoryByCursor
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICursorProtector _protector = protector ?? throw new ArgumentNullException(nameof(protector));

    public async Task<CursorPage<CaseHistoryEntry>> ExecuteAsync(
        CaseListCursorQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }
        var limit = CursorPaging.NormalizeLimit(query.Limit);
        var scope = CaseListCursorScope.For("ListCaseHistory", query.Actor, query.CaseId);

        DateTimeOffset? afterOccurredAtUtc = null;
        Guid? afterId = null;
        if (query.Cursor is { Length: > 0 } cursor)
        {
            var position = _protector.Unprotect(cursor, scope);
            afterOccurredAtUtc = CursorPaging.DecodeUtcTimestamp(position.SortKey);
            afterId = position.Id;
        }

        var rows = await _store.ListHistoryByCursorAsync(
            query.CaseId, afterOccurredAtUtc, afterId, limit + 1, cancellationToken);

        return CursorPageBuilder.Build(
            rows,
            limit,
            _protector,
            scope,
            entry => CursorPaging.EncodeUtcTimestamp(entry.OccurredAtUtc),
            entry => entry.EntryId);
    }
}

/// <summary>
/// The one scope rule every <see cref="CaseListCursorQuery"/>-shaped cursor
/// (documents, history, and Estimates.cs' estimates) shares: the query name
/// plus an actor and the case identifier, since these lists carry no
/// separate filter or order for a cursor to be minted against. Binding the
/// query name into the scope (<see cref="CursorPaging.CreateScope"/>) also
/// keeps a documents cursor from being replayed against the history or
/// estimates list for the same case.
/// </summary>
internal static class CaseListCursorScope
{
    public static string For(string query, ActionActor actor, Guid caseId) =>
        CursorPaging.CreateScope(query, actor, caseId.ToString());
}
