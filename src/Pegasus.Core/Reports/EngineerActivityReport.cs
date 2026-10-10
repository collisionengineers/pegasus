using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// The Engineer Report (MI-01; FRD-17 § Management Reports): per recorded
/// send actor and period, reports sent and queries received. A report is
/// credited to the recorded actor of its case-linked Sent evidence: a staff
/// member by account, and every other kind of sender (Automation) as one row
/// of its own, so the reports sent agree with Reports by Principal. A query
/// is credited to the assigned Engineer of its associated case (operator
/// decision D12). Those are separate dimensions. Both are counted by the time
/// the mail was sent or received, in the half-open period <c>[from, to)</c>.
/// </summary>
public sealed record EngineerActivityCounts(
    Guid EngineerId,
    int ReportsSent,
    int QueriesReceived,
    int AmendmentRequests = 0,
    int AuditReportsSent = 0,
    TimeSpan? AverageReceivedToSent = null)
{
    /// <summary>
    /// The sender's kind. A <see cref="ActorKind.Staff"/> row names its
    /// account in <see cref="EngineerId"/>; any other kind is one row with an
    /// empty <see cref="EngineerId"/> and no queries (item J, 9 October 2026).
    /// </summary>
    public ActorKind SenderKind { get; init; } = ActorKind.Staff;
}

public interface IEngineerActivityQueries
{
    /// <summary>
    /// One entry per staff member, and per other kind of sender, with any
    /// activity in the period; a sender with none is absent.
    /// </summary>
    Task<IReadOnlyList<EngineerActivityCounts>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

/// <param name="QueriesReceived">Post-report mail; a dispute is a query.</param>
/// <param name="AmendmentRequests">Post-report mail classified as an amendment request; part of <paramref name="QueriesReceived"/>.</param>
/// <param name="AuditReportsSent">Audit reports sent (<c>CaseWorkPolicy.IsAuditReport</c>: a standalone Audit Case's or an Inspection + Audit Case's Audit); part of <paramref name="ReportsSent"/> (MI-01's Audit uplift).</param>
/// <param name="AverageReceivedToSent">Instruction received to report sent, averaged over the sends with a known origin; an Audit work's report counts from the Audit's creation.</param>
public sealed record EngineerActivityRow(
    Guid EngineerId,
    string DisplayName,
    int ReportsSent,
    int QueriesReceived,
    int AmendmentRequests = 0,
    int AuditReportsSent = 0,
    TimeSpan? AverageReceivedToSent = null)
{
    public ActorKind SenderKind { get; init; } = ActorKind.Staff;
}

public sealed record EngineerActivityReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<EngineerActivityRow> Rows)
{
    /// <summary>The staff members with activity in the period: the Person choices (item N).</summary>
    public IEnumerable<EngineerActivityRow> People => Rows.Where(row => row.SenderKind == ActorKind.Staff);

    /// <summary>Person narrows the report to one staff member's row; every other sender leaves.</summary>
    public EngineerActivityReport For(Guid? engineerId) => engineerId is { } id
        ? this with { Rows = [.. People.Where(row => row.EngineerId == id)] }
        : this;
}

public sealed class GetEngineerActivityReport(
    IEngineerActivityQueries queries,
    IStaffAccountQueries staffAccounts)
{
    /// <summary>
    /// The longest period one report may cover. A year is the longest span
    /// the office reasons about; anything longer is several reports.
    /// </summary>
    public static readonly TimeSpan MaximumPeriod = TimeSpan.FromDays(366);

    private readonly IEngineerActivityQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly IStaffAccountQueries staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));

    public async Task<EngineerActivityReport> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        if (fromUtc >= toUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toUtc),
                "The report period must end after it starts.");
        }
        if (toUtc - fromUtc > MaximumPeriod)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toUtc),
                "The report period cannot exceed 366 days.");
        }

        var counts = await queries.GetAsync(fromUtc, toUtc, cancellationToken);
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Any(item => (item.SenderKind == ActorKind.Staff
                ? item.EngineerId == Guid.Empty
                : item.EngineerId != Guid.Empty || item.QueriesReceived != 0)
            || item.ReportsSent < 0
            || item.QueriesReceived < 0
            || item.AmendmentRequests < 0
            || item.AmendmentRequests > item.QueriesReceived
            || item.AuditReportsSent < 0
            || item.AuditReportsSent > item.ReportsSent
            || item.AverageReceivedToSent < TimeSpan.Zero))
        {
            throw new InvalidDataException("The Engineer activity query returned an invalid row.");
        }
        if (counts.Select(item => (item.SenderKind, item.EngineerId)).Distinct().Count() != counts.Count)
        {
            throw new InvalidDataException("The Engineer activity query returned a duplicate sender.");
        }

        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            counts.Where(item => item.SenderKind == ActorKind.Staff).Select(item => item.EngineerId),
            cancellationToken);
        var rows = counts
            .Select(item => new EngineerActivityRow(
                item.EngineerId,
                ActorDisplayNames.Resolve(item.SenderKind, item.EngineerId.ToString("D"), names),
                item.ReportsSent,
                item.QueriesReceived,
                item.AmendmentRequests,
                item.AuditReportsSent,
                item.AverageReceivedToSent)
            {
                SenderKind = item.SenderKind
            })
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.EngineerId)
            .ToList();
        return new(fromUtc, toUtc, rows);
    }
}
