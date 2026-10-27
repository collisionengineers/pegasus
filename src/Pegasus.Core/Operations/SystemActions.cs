using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>
/// What Pegasus itself did on a Case (v42 Design A). Each row is read from the
/// record the action wrote. Dismissal belongs to that record
/// (<see cref="WorkCentreDismissalPolicy"/>), so dismissing an action never
/// hides the Case's New cases row.
/// </summary>
public enum SystemActionKind
{
    AutoMatchedMail,
    VehicleImagesPaired,
    QueryOpenedAutomatically,
    CancellationMatched
}

/// <summary>
/// The System Actions sub-filters. Rules and queries is the two rule kinds
/// together; the other two name one kind each.
/// </summary>
public enum SystemActionFilter
{
    All,
    AutoMatched,
    ImagesPaired,
    RulesAndQueries
}

/// <summary>
/// One System Actions row. <see cref="RecordId"/> is the underlying record —
/// the receipt, the image intake or the query — and <see cref="Signal"/> is
/// the text that record left (the association reason, which names the agreeing
/// signals, or the recorded reason of the other kinds).
/// </summary>
public sealed record SystemActionRow(
    Guid RecordId,
    SystemActionKind Kind,
    Guid CaseId,
    string Reference,
    string? Registration,
    string? VehicleMake,
    string? VehicleModel,
    string Summary,
    string Signal,
    DateTimeOffset OccurredAtUtc,
    Guid? QueryId,
    bool QueryUnassigned);

/// <summary>
/// The System Actions page. <see cref="KindCounts"/> is the window before the
/// kind filter and the search, so every chip states what it would show.
/// <see cref="TotalCount"/> is the filtered list the page walks.
/// </summary>
public sealed record SystemActionPage(
    IReadOnlyList<SystemActionRow> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<SystemActionKind, int> KindCounts)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public interface ISystemActionQueries
{
    /// <summary>
    /// System actions at or after <paramref name="sinceUtc"/>, newest first,
    /// less those whose record was dismissed at or after the action, narrowed
    /// to <paramref name="filter"/> and to <paramref name="search"/> when given
    /// (<see cref="SystemActionsPolicy"/>).
    /// </summary>
    Task<SystemActionPage> ListAsync(
        DateTimeOffset sinceUtc,
        SystemActionFilter filter,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public static class SystemActionsPolicy
{
    /// <summary>The window is the New cases window: seven calendar days, midnight Europe/London.</summary>
    public static DateTimeOffset WindowStart(DateTimeOffset nowUtc) => RecentCasesPolicy.WindowStart(nowUtc);

    public const int PageSize = 50;

    public static bool Matches(SystemActionFilter filter, SystemActionKind kind) => filter switch
    {
        SystemActionFilter.AutoMatched => kind == SystemActionKind.AutoMatchedMail,
        SystemActionFilter.ImagesPaired => kind == SystemActionKind.VehicleImagesPaired,
        SystemActionFilter.RulesAndQueries => kind is SystemActionKind.QueryOpenedAutomatically
            or SystemActionKind.CancellationMatched,
        _ => true
    };

    /// <summary>
    /// Find within System Actions: the reference, the registration with its
    /// spaces and dashes ignored, the vehicle, the summary or the signal.
    /// The same compact registration rule as <see cref="RecentCasesPolicy.Matches"/>.
    /// </summary>
    public static bool MatchesSearch(SystemActionRow row, string? search)
    {
        ArgumentNullException.ThrowIfNull(row);
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return true;
        }

        var compact = Compact(term);
        return Contains(row.Reference, term)
            || Contains(row.Summary, term)
            || Contains(row.Signal, term)
            || Contains(row.VehicleMake, term)
            || Contains(row.VehicleModel, term)
            || (row.Registration is { } registration && Contains(Compact(registration), compact));

        static bool Contains(string? value, string term) =>
            value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        static string Compact(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
    }
}

public interface IListSystemActions
{
    Task<SystemActionPage> ExecuteAsync(
        ActionActor actor,
        SystemActionFilter filter,
        string? search,
        int page,
        CancellationToken cancellationToken,
        DateTimeOffset? asOfUtc = null);
}

public sealed class ListSystemActions(
    ISystemActionQueries queries,
    TimeProvider timeProvider) : IListSystemActions
{
    private readonly ISystemActionQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<SystemActionPage> ExecuteAsync(
        ActionActor actor,
        SystemActionFilter filter,
        string? search,
        int page,
        CancellationToken cancellationToken,
        DateTimeOffset? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);
        var since = SystemActionsPolicy.WindowStart(asOfUtc ?? _timeProvider.GetUtcNow());
        return _queries.ListAsync(
            since,
            filter,
            search,
            page,
            SystemActionsPolicy.PageSize,
            cancellationToken);
    }
}
