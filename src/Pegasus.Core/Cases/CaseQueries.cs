using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
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

public sealed record SearchCasesQuery(
    ActionActor Actor,
    CaseSearchFilters Filters,
    int Page = 1,
    int PageSize = 25);

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
    DateTimeOffset CreatedAtUtc);

public sealed record SearchCasesResult(
    IReadOnlyList<CaseSearchItem> Items,
    int Page,
    int PageSize,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record CaseEditLeaseSnapshot(
    string Holder,
    DateTimeOffset ExpiresAtUtc,
    string OperationKey);

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
    long AfterVersion);

public sealed record CaseDetails(
    CaseSearchItem Summary,
    CaseWorkflowRecord Workflow,
    CaseEditLeaseSnapshot? ActiveEditLease,
    IReadOnlyList<CaseDocument> Documents,
    IReadOnlyList<BoxFileRequest> BoxFileRequests,
    IReadOnlyList<CaseRequestUploadSummary> RequestUploadLinks,
    IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence,
    IReadOnlyList<CaseHistoryEntry> History)
{
    public CaseDataProjection? Data { get; init; }
    public IReadOnlyList<CaseTaskRecord> Tasks { get; init; } = [];
    public GeneratedCaseChaser? LatestChaser { get; init; }
    public CaseVehicleEvidence? VehicleEvidence { get; init; }
    public EvaHandoffPreparation? EvaHandoff { get; init; }
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

        var compact = string.Concat(normalized.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        if (compact.Length == 0)
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
    IEvaHandoffQueries evaHandoffQueries,
    ICaseDueChaserQueries dueChaserQueries,
    ICaseTaskQueries taskQueries) : IGetCase
{
    private readonly ICaseQueryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseDataQueries _caseDataQueries =
        caseDataQueries ?? throw new ArgumentNullException(nameof(caseDataQueries));
    private readonly IVehicleEvidenceQueries _vehicleEvidenceQueries =
        vehicleEvidenceQueries ?? throw new ArgumentNullException(nameof(vehicleEvidenceQueries));
    private readonly IEvaHandoffQueries _evaHandoffQueries =
        evaHandoffQueries ?? throw new ArgumentNullException(nameof(evaHandoffQueries));
    private readonly ICaseDueChaserQueries _dueChaserQueries =
        dueChaserQueries ?? throw new ArgumentNullException(nameof(dueChaserQueries));
    private readonly ICaseTaskQueries _taskQueries =
        taskQueries ?? throw new ArgumentNullException(nameof(taskQueries));

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
        var evaHandoff = await _evaHandoffQueries.GetPreparationAsync(query.CaseId, cancellationToken);
        var latestChaser = await _dueChaserQueries.GetLatestAsync(query.CaseId, cancellationToken);
        var tasks = await _taskQueries.ListAsync(query.CaseId, cancellationToken);
        if (data.Identity.CaseId != details.Workflow.CaseId
            || vehicleEvidence is not null && vehicleEvidence.CaseId != details.Workflow.CaseId
            || evaHandoff is not null && evaHandoff.CaseId != details.Workflow.CaseId
            || latestChaser is not null && latestChaser.CaseId != details.Workflow.CaseId
            || tasks.Any(item => item.CaseId != details.Workflow.CaseId))
        {
            throw new InvalidDataException("A composed case projection belongs to another case.");
        }

        return details with
        {
            Data = data,
            VehicleEvidence = vehicleEvidence,
            EvaHandoff = evaHandoff,
            LatestChaser = latestChaser,
            Tasks = tasks
        };
    }
}
