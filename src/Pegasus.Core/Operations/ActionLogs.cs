using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <param name="Actor">One person or client, by subject id.</param>
/// <param name="ActorType">
/// A whole class of actor, by <see cref="ActorKind"/> name: the operator
/// question "what did staff do" or "what did the AI do" is about the kind, not
/// about one subject. Rejected here rather than silently ignored when it is not
/// a recognised kind, so a mistyped filter never reads as an unfiltered log.
/// </param>
public sealed record ActionLogFilter(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? SearchText,
    string? Area,
    string? Actor,
    string? Result,
    string? Operation,
    string? Record,
    string? CorrelationId,
    bool OldestFirst = false,
    int Page = 1,
    int PageSize = 50,
    string? ActorType = null);

public sealed record ActionLogRow(
    Guid Id, string Area, string Operation, string Reference, string Actor,
    DateTimeOffset OccurredAtUtc, string Result, string CorrelationId,
    string? ActorKind);

public sealed record ActionLogPage(IReadOnlyList<ActionLogRow> Rows, bool HasMore);

public interface IActionLogQueries
{
    Task<ActionLogPage> ListAsync(ActionLogFilter filter, CancellationToken cancellationToken);
}

public sealed class ListActionLogs(IActionLogQueries queries)
{
    public static readonly TimeSpan MaximumPeriod = TimeSpan.FromDays(366);

    public Task<ActionLogPage> ExecuteAsync(ActionActor actor, ActionLogFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(filter);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        if (filter.FromUtc >= filter.ToUtc || filter.ToUtc - filter.FromUtc > MaximumPeriod
            || filter.Page < 1 || filter.PageSize is < 1 or > 100
            || filter.Page > (int.MaxValue - filter.PageSize - 1) / filter.PageSize + 1
            || (filter.ActorType is { } actorType
                && !Enum.TryParse<ActorKind>(actorType, ignoreCase: true, out _)))
            throw new ArgumentOutOfRangeException(nameof(filter));
        return queries.ListAsync(filter, cancellationToken);
    }
}
