using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// One Principal's reports in the period (MI-02, MI-03; R673): the reports
/// first sent, the re-sends, the fees frozen in the reports those first sends
/// carried and the average time to a first send (<see cref="ReportSendPolicy"/>),
/// with its queues now (MI-05).
/// </summary>
public sealed record PrincipalReportActivity(
    Guid PrincipalId,
    string PrincipalCode,
    int Sent,
    int Resends,
    TimeSpan? AverageReceivedToSent,
    int CurrentTriage,
    DateTimeOffset? OldestCurrentTriageCreatedAtUtc,
    int CurrentHeldCases,
    DateTimeOffset? OldestHeldAtUtc,
    int HeldWithoutRecordedHoldEvent,
    decimal AgreedFeeTotal = 0,
    int AuditSent = 0,
    int AuditResends = 0,
    decimal AuditAgreedFeeTotal = 0)
{
    // MI-02: each total splits into Inspection and Audit reports
    // (CaseWorkPolicy.IsAuditReport decides which).
    public int InspectionSent => Sent - AuditSent;

    public int InspectionResends => Resends - AuditResends;

    public decimal InspectionAgreedFeeTotal => AgreedFeeTotal - AuditAgreedFeeTotal;
}

public sealed record PrincipalReportActivityReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<PrincipalReportActivity> Rows);

public interface IV1ActivityReportQueries
{
    Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public sealed class GetV1ActivityReport(IV1ActivityReportQueries queries)
{
    public async Task<PrincipalReportActivityReport> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc));
        }

        var rows = await queries.GetAsync(fromUtc, toUtc, cancellationToken);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Any(IsInvalid)
            || rows.Select(x => x.PrincipalId).Distinct().Count() != rows.Count)
        {
            throw new InvalidDataException("The principal report query returned an invalid row.");
        }

        return new(fromUtc, toUtc, rows
            .OrderBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PrincipalId)
            .ToArray());
    }

    private static bool IsInvalid(PrincipalReportActivity row) =>
        row.PrincipalId == Guid.Empty
        || string.IsNullOrWhiteSpace(row.PrincipalCode)
        || row.Sent < 0
        || row.Resends < 0
        || row.AverageReceivedToSent < TimeSpan.Zero
        || row.CurrentTriage < 0
        || row.CurrentHeldCases < 0
        || row.HeldWithoutRecordedHoldEvent < 0
        || row.HeldWithoutRecordedHoldEvent > row.CurrentHeldCases
        || row.AgreedFeeTotal < 0
        // MI-02's Audit share is part of each total.
        || row.AuditSent < 0
        || row.AuditSent > row.Sent
        || row.AuditResends < 0
        || row.AuditResends > row.Resends
        || row.AuditAgreedFeeTotal < 0
        || row.AuditAgreedFeeTotal > row.AgreedFeeTotal;
}
