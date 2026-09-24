using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>How a workbook column is typed, so the writer formats it and totals it where that makes sense.</summary>
public enum WorkbookColumnKind
{
    Text,
    Count,
    Money,
    Duration,
    DateTime
}

public sealed record WorkbookColumn(string Title, WorkbookColumnKind Kind);

/// <summary>
/// One sheet: a titled, typed table with a header row, a filter over it, a
/// frozen header and, when <see cref="Totals"/> is set, a totals row under
/// every Integer and Money column. Cells are strings, integers, decimals,
/// <see cref="TimeSpan"/> or <see cref="DateTimeOffset"/> values, or null.
/// </summary>
public sealed record WorkbookSheet(
    string Name,
    IReadOnlyList<WorkbookColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    bool Totals);

/// <summary>The port an office-document writer fills: sheets in, one workbook's bytes out.</summary>
public interface IWorkbookWriter
{
    byte[] Write(IReadOnlyList<WorkbookSheet> sheets);
}

/// <summary>
/// The Administration reports as one workbook (MI-01 to MI-03 and the month
/// breakdown): the same columns the page shows, typed for a spreadsheet, so
/// the operator filters and sums without retyping.
/// </summary>
public static class AdministrationReportTables
{
    public static IReadOnlyList<WorkbookSheet> Build(
        EngineerActivityReport engineerReport,
        PrincipalReportActivityReport principalReport,
        IReadOnlyList<MonthlyReportActivity> monthly)
    {
        ArgumentNullException.ThrowIfNull(engineerReport);
        ArgumentNullException.ThrowIfNull(principalReport);
        ArgumentNullException.ThrowIfNull(monthly);
        var sheets = new List<WorkbookSheet>
        {
            new(
                "Engineer activity",
                [
                    new("Person", WorkbookColumnKind.Text),
                    new("Queries received", WorkbookColumnKind.Count),
                    new("Disputes", WorkbookColumnKind.Count),
                    new("Amendment requests", WorkbookColumnKind.Count),
                    new("Reports sent", WorkbookColumnKind.Count),
                    new("Audit reports sent", WorkbookColumnKind.Count),
                    new("Received to sent", WorkbookColumnKind.Duration)
                ],
                engineerReport.Rows.Select(row => (IReadOnlyList<object?>)
                [
                    row.DisplayName, row.QueriesReceived, row.Disputes, row.AmendmentRequests,
                    row.ReportsSent, row.AuditReportsSent, row.AverageReceivedToSent
                ]).ToArray(),
                Totals: true)
        };

        sheets.Add(new(
            "Reports by Principal",
            [
                new("Principal", WorkbookColumnKind.Text),
                new("Reports produced", WorkbookColumnKind.Count),
                new("Reports sent", WorkbookColumnKind.Count),
                new("Agreed fees", WorkbookColumnKind.Money),
                new("Report types", WorkbookColumnKind.Text)
            ],
            principalReport.Rows
                .Where(row => row.ReportsProduced > 0 || row.Sent > 0)
                .Select(row => (IReadOnlyList<object?>)
                [
                    row.PrincipalCode, row.ReportsProduced, row.Sent, row.AgreedFeeTotal,
                    string.Join("; ", row.ArtifactTypes.Where(type => type.Generated > 0).Select(type => $"{type.Kind} {type.Generated}"))
                ]).ToArray(),
            Totals: true));
        sheets.Add(new(
            "Turnaround",
            [
                new("Principal", WorkbookColumnKind.Text),
                new("Currently held", WorkbookColumnKind.Count),
                new("Oldest held since", WorkbookColumnKind.DateTime),
                new("Time to produce", WorkbookColumnKind.Duration),
                new("Time to ready", WorkbookColumnKind.Duration),
                new("Time to send", WorkbookColumnKind.Duration)
            ],
            principalReport.Rows
                .Where(row => row.CurrentHeldCases > 0
                    || row.AverageReceivedToGeneration.HasValue
                    || row.AverageReceivedToReady.HasValue
                    || row.AverageReceivedToSent.HasValue)
                .Select(row => (IReadOnlyList<object?>)
                [
                    row.PrincipalCode, row.CurrentHeldCases, row.OldestHeldAtUtc,
                    row.AverageReceivedToGeneration, row.AverageReceivedToReady, row.AverageReceivedToSent
                ]).ToArray(),
            Totals: false));

        sheets.Add(new(
            "By month",
            [
                new("Month", WorkbookColumnKind.Text),
                new("Principal", WorkbookColumnKind.Text),
                new("Reports produced", WorkbookColumnKind.Count),
                new("Fee notes produced", WorkbookColumnKind.Count),
                new("Reports sent", WorkbookColumnKind.Count),
                new("Agreed fees", WorkbookColumnKind.Money)
            ],
            monthly.Select(row => (IReadOnlyList<object?>)
            [
                row.MonthLabel, row.PrincipalCode, row.ReportsGenerated, row.FeeNotesGenerated, row.Sent, row.AgreedFeeTotal
            ]).ToArray(),
            Totals: true));
        return sheets;
    }
}

/// <summary>
/// One Principal's report activity in one London month (MI-02's periods,
/// feeding invoice generation): what was produced, what was sent, and the
/// agreed fees on the Cases whose reports were produced that month.
/// </summary>
public sealed record MonthlyReportActivity(
    Guid PrincipalId,
    string PrincipalCode,
    int Year,
    int Month,
    int ReportsGenerated,
    int FeeNotesGenerated,
    int Sent,
    decimal AgreedFeeTotal,
    int AuditReportsGenerated = 0,
    int AuditSent = 0,
    decimal AuditAgreedFeeTotal = 0)
{
    // MI-02: each total splits into Inspection and Audit reports.
    public int InspectionReportsGenerated => ReportsGenerated - AuditReportsGenerated;

    public int InspectionSent => Sent - AuditSent;

    public decimal InspectionAgreedFeeTotal => AgreedFeeTotal - AuditAgreedFeeTotal;

    public string MonthLabel => new DateOnly(Year, Month, 1).ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
}

public interface IMonthlyReportActivityQueries
{
    Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public sealed class GetMonthlyReportActivity(IMonthlyReportActivityQueries queries)
{
    public async Task<IReadOnlyList<MonthlyReportActivity>> ExecuteAsync(
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
            || rows.GroupBy(row => (row.PrincipalId, row.Year, row.Month)).Any(group => group.Count() != 1))
        {
            throw new InvalidDataException("The monthly report query returned an invalid row.");
        }

        return rows
            .OrderByDescending(row => row.Year).ThenByDescending(row => row.Month)
            .ThenBy(row => row.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsInvalid(MonthlyReportActivity row) =>
        row.PrincipalId == Guid.Empty
        || string.IsNullOrWhiteSpace(row.PrincipalCode)
        || row.Year is < 1 or > 9999
        || row.Month is < 1 or > 12
        || row.ReportsGenerated < 0
        || row.FeeNotesGenerated < 0
        || row.Sent < 0
        || row.AgreedFeeTotal < 0;
}

/// <summary>The one export: every Administration report for the period as one workbook.</summary>
public sealed class ExportAdministrationReports(IWorkbookWriter writer)
{
    public byte[] Execute(
        ActionActor actor,
        EngineerActivityReport engineerReport,
        PrincipalReportActivityReport principalReport,
        IReadOnlyList<MonthlyReportActivity> monthly)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        return writer.Write(AdministrationReportTables.Build(engineerReport, principalReport, monthly));
    }
}
