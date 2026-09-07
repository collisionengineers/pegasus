using System.Text;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// The Engineer Report (MI-01; FRD-12 § Administration → Reports): per
/// recorded send actor and period, reports sent and queries received. A report
/// is credited to the recorded staff actor of its case-linked Sent evidence;
/// a query is credited to the assigned Engineer of its associated case
/// (operator decision D12, EPIC-011). Those are separate dimensions. Both are
/// counted by the time the mail was sent or received, in the half-open
/// period <c>[from, to)</c>.
/// </summary>
public sealed record EngineerActivityCounts(
    Guid EngineerId,
    int ReportsSent,
    int QueriesReceived);

public interface IEngineerActivityQueries
{
    /// <summary>
    /// One entry per Engineer with any activity in the period; an Engineer
    /// with none is absent. <paramref name="engineerId"/> narrows to one.
    /// </summary>
    Task<IReadOnlyList<EngineerActivityCounts>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? engineerId,
        CancellationToken cancellationToken);
}

public sealed record EngineerActivityRow(
    Guid EngineerId,
    string DisplayName,
    int ReportsSent,
    int QueriesReceived);

public sealed record EngineerActivityReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<EngineerActivityRow> Rows);

/// <summary>
/// The export shape: the same three columns the table shows, RFC 4180
/// quoted, CRLF-terminated. Rows only — the caller owns the response.
/// </summary>
public static class EngineerActivityReportCsv
{
    public const string Header = "Recorded send actor,Queries received for assigned Engineer,Reports sent by recorded actor";

    public static string ToCsv(IReadOnlyList<EngineerActivityRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");
        foreach (var row in rows)
        {
            builder
                .Append(EscapeField(row.DisplayName)).Append(',')
                .Append(row.QueriesReceived).Append(',')
                .Append(row.ReportsSent)
                .Append("\r\n");
        }

        return builder.ToString();
    }

    internal static string EscapeField(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? "'" + value
            : value;
        return safe.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? safe
            : $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
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
        Guid? engineerId,
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
        if (engineerId == Guid.Empty)
        {
            throw new ArgumentException("An Engineer filter must name an account.", nameof(engineerId));
        }

        var counts = await queries.GetAsync(fromUtc, toUtc, engineerId, cancellationToken);
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Any(item => item.EngineerId == Guid.Empty
            || item.ReportsSent < 0
            || item.QueriesReceived < 0))
        {
            throw new InvalidDataException("The Engineer activity query returned an invalid row.");
        }
        if (counts.Select(item => item.EngineerId).Distinct().Count() != counts.Count)
        {
            throw new InvalidDataException("The Engineer activity query returned a duplicate Engineer.");
        }

        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            counts.Select(item => item.EngineerId),
            cancellationToken);
        var rows = counts
            .Select(item => new EngineerActivityRow(
                item.EngineerId,
                ActorDisplayNames.Resolve(ActorKind.Staff, item.EngineerId.ToString("D"), names),
                item.ReportsSent,
                item.QueriesReceived))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.EngineerId)
            .ToList();
        return new(fromUtc, toUtc, rows);
    }
}
