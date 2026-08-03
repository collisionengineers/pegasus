namespace Pegasus.Core.Identity;

/// <summary>
/// One consolidated Automation Actor activity record: either a permanent
/// action-history entry attributed to the Automation actor kind, or a security
/// event recorded for an automation ingress denial. The record type names which
/// store the row came from; the correlation identifier addresses the exact
/// operation across both.
/// </summary>
public sealed record AutomationActivityRecord(
    Guid Id,
    AutomationActivityRecordType RecordType,
    string EventKind,
    string SubjectId,
    DateTimeOffset OccurredAtUtc,
    string Outcome,
    string CorrelationId,
    string? AggregateType,
    string? AggregateId,
    string? Reason);

public enum AutomationActivityRecordType
{
    ActionHistory,
    SecurityEvent
}

/// <summary>
/// Shared conventions binding the automation ingress writers to the
/// consolidated activity reader: ingress security events carry reason codes
/// with this prefix so denials remain queryable without a parallel store.
/// </summary>
public static class AutomationActivityConventions
{
    public const string SecurityEventReasonPrefix = "automation_";
}

public sealed record ListAutomationActivityRequest(
    ActionActor Actor,
    string? CorrelationId = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ListAutomationActivityResult(
    IReadOnlyList<AutomationActivityRecord> Records,
    string? CorrelationId,
    int Page,
    int PageSize,
    bool HasPreviousPage,
    bool HasMoreRecords);

/// <summary>
/// Read port over the permanent action-history and security-event stores,
/// restricted to Automation actor attribution and automation ingress denials.
/// </summary>
public interface IAutomationActivityQueries
{
    Task<ListAutomationActivityResult> ListAsync(
        ListAutomationActivityRequest request,
        CancellationToken cancellationToken);
}

public interface IListAutomationActivity
{
    Task<ListAutomationActivityResult> ExecuteAsync(
        ListAutomationActivityRequest request,
        CancellationToken cancellationToken);
}

public sealed class ListAutomationActivity(IAutomationActivityQueries queries)
    : IListAutomationActivity
{
    public const int MaximumPageSize = 100;
    public const int MaximumCorrelationIdLength = 100;

    private readonly IAutomationActivityQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<ListAutomationActivityResult> ExecuteAsync(
        ListAutomationActivityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageAutomationClients);
        if (request.Page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The requested page is outside the supported range.");
        }
        if (request.PageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The requested page size is outside the supported range.");
        }

        var correlationId = request.CorrelationId?.Trim();
        if (correlationId is { Length: 0 })
        {
            correlationId = null;
        }
        if (correlationId is { Length: > MaximumCorrelationIdLength })
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The correlation identifier filter is outside the supported range.");
        }

        return _queries.ListAsync(
            request with { CorrelationId = correlationId },
            cancellationToken);
    }
}
