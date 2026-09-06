using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

public sealed record ActionLogFilter(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? Actor,
    string? EventKind,
    string? AggregateType,
    string? Outcome,
    string? CorrelationId,
    int Page = 1,
    int PageSize = 50);

public sealed record ActionLogRow(
    Guid Id, string AggregateType, string AggregateId, string EventKind,
    string ActorKind, string ActorSubjectId, DateTimeOffset OccurredAtUtc,
    string Outcome, string CorrelationId);

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
            || filter.Page < 1 || filter.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(filter));
        return queries.ListAsync(filter, cancellationToken);
    }
}
