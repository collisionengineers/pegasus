using Pegasus.Core.Actors;
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
    DateOnly? InstructionDate = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Origin = null,
    string? Query = null);

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
/// search read alone (CASE-026); they are display facts, not filters.
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
    DateOnly? InstructionDate,
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

public sealed record CaseRequestUploadSummary(
    Guid Id,
    RequestUploadStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    int AcceptedFileCount,
    long AcceptedByteCount,
    long Version);

public sealed record CaseHistoryEntry(
    string EventType,
    string Actor,
    string ActorKind,
    DateTimeOffset OccurredAtUtc,
    string Reason,
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
}

public sealed record CaseQueryEmail(
    Guid RetainedMessageId,
    DateTimeOffset ReceivedAtUtc,
    string? EffectiveSenderAddress,
    string? SenderDisplayName,
    string? SenderAddress,
    string? Subject,
    MailCategory Classification);

public sealed record CaseDetails(
    CaseSearchItem Summary,
    CaseWorkflowRecord Workflow,
    CaseEditLeaseSnapshot? ActiveEditLease,
    IReadOnlyList<CaseDocument> Documents,
    string? CustodyFolderRemoteId,
    CaseCustodyState CustodyState,
    IReadOnlyList<CaseRequestUploadSummary> RequestUploadLinks,
    IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence,
    IReadOnlyList<CaseHistoryEntry> History)
{
    public CaseDataProjection? Data { get; init; }
    public IReadOnlyList<CaseTaskRecord> Tasks { get; init; } = [];
    public GeneratedCaseChaser? LatestChaser { get; init; }
    public CaseVehicleEvidence? VehicleEvidence { get; init; }
    public IReadOnlyList<CaseCustodyPreparation> Custody { get; init; } = [];
    public IReadOnlyList<CaseQueryEmail> QueryEmails { get; init; } = [];

    /// <summary>
    /// The operator-facing name for <c>Workflow.ReportApproval.ApprovedBy</c>,
    /// resolved by <c>GetCase</c>. Null when there is no report approval to name.
    /// </summary>
    public string? ReportApprovedByDisplayName { get; init; }
}

public sealed record GetCaseQuery(Guid CaseId, ActionActor Actor);

public interface ICaseQueryStore
{
    Task<SearchCasesResult> SearchAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken);

    Task<CaseDetails?> GetAsync(
        GetCaseQuery query,
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

public sealed class SearchCases(ICaseQueryStore store) : ISearchCases
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<SearchCasesResult> ExecuteAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        ArgumentNullException.ThrowIfNull(query.Filters);
        if (query.Page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The requested page is outside the supported range.");
        }
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The requested page size is outside the supported range.");
        }
        if (query.Filters.EngineerId == Guid.Empty)
        {
            throw new ArgumentException("The Engineer filter is invalid.", nameof(query));
        }
        if (query.Filters.State is { } state && !Enum.IsDefined(state))
        {
            throw new ArgumentException("The lifecycle-state filter is invalid.", nameof(query));
        }
        if (!Enum.IsDefined(query.Order))
        {
            throw new ArgumentException("The sort order is invalid.", nameof(query));
        }
        if (query.Filters.FromDate is { } fromDate
            && query.Filters.ToDate is { } toDate
            && fromDate > toDate)
        {
            throw new ArgumentException("The start date cannot be after the end date.", nameof(query));
        }

        var normalized = query with
        {
            Filters = query.Filters with
            {
                CaseReference = Normalize(query.Filters.CaseReference, 100, nameof(query.Filters.CaseReference)),
                Registration = NormalizeRegistration(query.Filters.Registration),
                Claimant = Normalize(query.Filters.Claimant, 300, nameof(query.Filters.Claimant)),
                ClaimNumber = Normalize(query.Filters.ClaimNumber, 100, nameof(query.Filters.ClaimNumber)),
                Principal = Normalize(query.Filters.Principal, 20, nameof(query.Filters.Principal))?.ToUpperInvariant(),
                Origin = Normalize(query.Filters.Origin, 100, nameof(query.Filters.Origin)),
                Query = Normalize(query.Filters.Query, 300, nameof(query.Filters.Query))
            }
        };

        return _store.SearchAsync(normalized, cancellationToken);
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

        var data = await _caseDataQueries.GetAsync(query.CaseId, cancellationToken)
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

        var approvedBy = details.Workflow.ReportApproval?.ApprovedBy;
        var staffIds = details.History
            .Where(entry => entry.ActorKind == nameof(ActorKind.Staff) && Guid.TryParse(entry.Actor, out _))
            .Select(entry => Guid.Parse(entry.Actor));
        if (approvedBy is { Kind: ActorKind.Staff } && Guid.TryParse(approvedBy.SubjectId, out var approverId))
        {
            staffIds = staffIds.Append(approverId);
        }
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
                .ToArray(),
            ReportApprovedByDisplayName = approvedBy is null
                ? null
                : ActorDisplayNames.Resolve(approvedBy.Kind, approvedBy.SubjectId, staffNames)
        };
    }
}
