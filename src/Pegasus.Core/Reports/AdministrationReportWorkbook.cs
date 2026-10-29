using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>How a workbook column is typed, so the writer formats it and totals it where that makes sense.</summary>
public enum WorkbookColumnKind
{
    Text,
    Count,
    Money,
    Duration,
    DateTime,

    /// <summary>A calendar date with no time: a <see cref="DateOnly"/> cell.</summary>
    Date
}

public sealed record WorkbookColumn(string Title, WorkbookColumnKind Kind);

/// <summary>
/// The one naming of a Management Reports column split by work: the measure's
/// own label with the work it counts, e.g. "Reports produced · Inspection".
/// </summary>
public static class ReportColumnTitles
{
    public static string Inspection(string measure) => measure + " · Inspection";

    public static string Audit(string measure) => measure + " · Audit";
}

/// <summary>
/// The Management Reports' headings and column titles, one copy: the page
/// draws its section headings, column headers and the totals they head from
/// these, and each CSV and workbook sheet is named and headed with them
/// (FRD-17: the CSVs and the workbook use the page's headings).
/// </summary>
public static class AdministrationReportHeadings
{
    public const string EngineerActivity = "Engineer activity";
    public const string ReportsByPrincipal = "Reports by Principal";
    public const string ByMonth = "By month";
    public const string Outcomes = "Outcomes";
    public const string Turnaround = "Turnaround";
    public const string Queues = "Queues";

    public const string Person = "Person";
    public const string QueriesReceived = "Queries received";
    public const string ReportsSent = "Reports sent";
    public const string AuditReportsSent = "Audit reports sent";
    public const string ReceivedToSent = "Received to sent";
    public const string Principal = "Principal";
    public const string ReportsProduced = "Reports produced";
    public const string AgreedFees = "Agreed fees";
    public const string Month = "Month";
    public const string FeeNotesProduced = "Fee notes produced";
    public const string TimeToProduce = "Time to produce";
    public const string TimeToReady = "Time to ready";
    public const string TimeToSend = "Time to send";
    public const string CurrentlyHeld = "Currently held";
    public const string OldestHeldSince = "Oldest held since";
    public const string Triages = "Triages";
    public const string OldestTriageSince = "Oldest Triage since";
}

/// <summary>
/// One sheet: a titled, typed table with a header row, a filter over it, a
/// frozen header and, when <see cref="Totals"/> is set, a totals row under
/// every Integer and Money column. Cells are strings, integers, decimals,
/// <see cref="TimeSpan"/>, <see cref="DateTimeOffset"/> or <see cref="DateOnly"/>
/// values, or null. A text cell in a typed column (the Case list's N/A) is
/// written as text and left out of the totals.
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

/// <summary>The words the Management Reports exports borrow from the operator vocabulary, which Core cannot see.</summary>
public interface IAdministrationReportLabels
{
    /// <summary>A turnaround as the page writes it, e.g. "6 days" (item K, 9 October 2026).</summary>
    string Turnaround(TimeSpan value);
}

/// <summary>
/// The Management Reports as sheets: each report's CSV is its sheet, and the
/// workbook is every sheet. The columns are the page's, under the page's
/// headings, with every Inspection and Audit split the page's Work choice
/// draws from.
/// </summary>
public static class AdministrationReportTables
{
    public static WorkbookSheet EngineerActivity(EngineerActivityReport report, IAdministrationReportLabels labels)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        return new(
            AdministrationReportHeadings.EngineerActivity,
            [
                new(AdministrationReportHeadings.Person, WorkbookColumnKind.Text),
                new(AdministrationReportHeadings.QueriesReceived, WorkbookColumnKind.Count),
                new(AdministrationReportHeadings.ReportsSent, WorkbookColumnKind.Count),
                new(AdministrationReportHeadings.AuditReportsSent, WorkbookColumnKind.Count),
                new(AdministrationReportHeadings.ReceivedToSent, WorkbookColumnKind.Text)
            ],
            report.Rows.Select(row => (IReadOnlyList<object?>)
            [
                row.DisplayName, row.QueriesReceived,
                row.ReportsSent, row.AuditReportsSent, Turnaround(row.AverageReceivedToSent, labels)
            ]).ToArray(),
            Totals: true);
    }

    public static WorkbookSheet ReportsByPrincipal(PrincipalReportActivityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new(
            AdministrationReportHeadings.ReportsByPrincipal,
            [
                new(AdministrationReportHeadings.Principal, WorkbookColumnKind.Text),
                .. Split(AdministrationReportHeadings.ReportsProduced, WorkbookColumnKind.Count),
                .. Split(AdministrationReportHeadings.ReportsSent, WorkbookColumnKind.Count),
                .. Split(AdministrationReportHeadings.AgreedFees, WorkbookColumnKind.Money)
            ],
            report.Rows
                .Where(row => row.ReportsProduced > 0 || row.Sent > 0)
                .Select(row => (IReadOnlyList<object?>)
                [
                    row.PrincipalCode,
                    row.ReportsProduced, row.InspectionReportsProduced, row.AuditReportsProduced,
                    row.Sent, row.InspectionSent, row.AuditSent,
                    row.AgreedFeeTotal, row.InspectionAgreedFeeTotal, row.AuditAgreedFeeTotal
                ]).ToArray(),
            Totals: true);
    }

    /// <summary>The period's times; the held figures are now, so they are Queues'.</summary>
    public static WorkbookSheet Turnaround(PrincipalReportActivityReport report, IAdministrationReportLabels labels)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        return new(
            AdministrationReportHeadings.Turnaround,
            [
                new(AdministrationReportHeadings.Principal, WorkbookColumnKind.Text),
                new(AdministrationReportHeadings.TimeToProduce, WorkbookColumnKind.Text),
                new(AdministrationReportHeadings.TimeToReady, WorkbookColumnKind.Text),
                new(AdministrationReportHeadings.TimeToSend, WorkbookColumnKind.Text)
            ],
            TurnaroundRows(report)
                .Select(row => (IReadOnlyList<object?>)
                [
                    row.PrincipalCode,
                    Turnaround(row.AverageReceivedToGeneration, labels),
                    Turnaround(row.AverageReceivedToReady, labels),
                    Turnaround(row.AverageReceivedToSent, labels)
                ]).ToArray(),
            Totals: false);
    }

    public static WorkbookSheet ByMonth(IReadOnlyList<MonthlyReportActivity> monthly)
    {
        ArgumentNullException.ThrowIfNull(monthly);
        return new(
            AdministrationReportHeadings.ByMonth,
            [
                new(AdministrationReportHeadings.Month, WorkbookColumnKind.Text),
                new(AdministrationReportHeadings.Principal, WorkbookColumnKind.Text),
                .. Split(AdministrationReportHeadings.ReportsProduced, WorkbookColumnKind.Count),
                .. Split(AdministrationReportHeadings.FeeNotesProduced, WorkbookColumnKind.Count),
                .. Split(AdministrationReportHeadings.ReportsSent, WorkbookColumnKind.Count),
                .. Split(AdministrationReportHeadings.AgreedFees, WorkbookColumnKind.Money)
            ],
            monthly.Select(row => (IReadOnlyList<object?>)
            [
                row.MonthLabel, row.PrincipalCode,
                row.ReportsGenerated, row.InspectionReportsGenerated, row.AuditReportsGenerated,
                row.FeeNotesGenerated, row.InspectionFeeNotesGenerated, row.AuditFeeNotesGenerated,
                row.Sent, row.InspectionSent, row.AuditSent,
                row.AgreedFeeTotal, row.InspectionAgreedFeeTotal, row.AuditAgreedFeeTotal
            ]).ToArray(),
            Totals: true);
    }

    public static WorkbookSheet Outcomes(ReportOutcomesReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new(
            AdministrationReportHeadings.Outcomes,
            [
                new(AdministrationReportHeadings.Principal, WorkbookColumnKind.Text),
                .. GetReportOutcomes.Order.Select(outcome =>
                    new WorkbookColumn(CaseReportDeliveryNaming.OutcomeWords(outcome), WorkbookColumnKind.Count)),
                new(CaseListPolicy.Agrees, WorkbookColumnKind.Count),
                new(CaseListPolicy.Differs, WorkbookColumnKind.Count)
            ],
            report.Rows.Select(row => (IReadOnlyList<object?>)
            [
                row.PrincipalCode, row.Repairable, row.TotalLoss, row.CashInLieu, row.ContractRepair,
                row.Agrees, row.Differs
            ]).ToArray(),
            Totals: true);
    }

    /// <summary>The queues now (item E): each Principal's held Cases and Triages, with the oldest of each.</summary>
    public static WorkbookSheet Queues(PrincipalReportActivityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new(
            AdministrationReportHeadings.Queues,
            [
                new(AdministrationReportHeadings.Principal, WorkbookColumnKind.Text),
                new(AdministrationReportHeadings.CurrentlyHeld, WorkbookColumnKind.Count),
                new(AdministrationReportHeadings.OldestHeldSince, WorkbookColumnKind.DateTime),
                new(AdministrationReportHeadings.Triages, WorkbookColumnKind.Count),
                new(AdministrationReportHeadings.OldestTriageSince, WorkbookColumnKind.DateTime)
            ],
            QueueRows(report)
                .Select(row => (IReadOnlyList<object?>)
                [
                    row.PrincipalCode, row.CurrentHeldCases, row.OldestHeldAtUtc,
                    row.CurrentTriage, row.OldestCurrentTriageCreatedAtUtc
                ]).ToArray(),
            Totals: true);
    }

    /// <summary>Turnaround's rows: a Principal with any time in the period.</summary>
    public static IEnumerable<PrincipalReportActivity> TurnaroundRows(PrincipalReportActivityReport report) =>
        report.Rows.Where(row => row.AverageReceivedToGeneration.HasValue
            || row.AverageReceivedToReady.HasValue
            || row.AverageReceivedToSent.HasValue);

    /// <summary>Queues' rows: a Principal with a held Case or an open Triage now.</summary>
    public static IEnumerable<PrincipalReportActivity> QueueRows(PrincipalReportActivityReport report) =>
        report.Rows.Where(row => row.CurrentHeldCases > 0 || row.CurrentTriage > 0);

    public static IReadOnlyList<WorkbookSheet> Build(
        EngineerActivityReport engineerReport,
        PrincipalReportActivityReport principalReport,
        IReadOnlyList<MonthlyReportActivity> monthly,
        ReportOutcomesReport outcomes,
        IAdministrationReportLabels labels) =>
    [
        EngineerActivity(engineerReport, labels),
        ReportsByPrincipal(principalReport),
        ByMonth(monthly),
        Outcomes(outcomes),
        Turnaround(principalReport, labels),
        Queues(principalReport)
    ];

    private static WorkbookColumn[] Split(string measure, WorkbookColumnKind kind) =>
    [
        new(measure, kind),
        new(ReportColumnTitles.Inspection(measure), kind),
        new(ReportColumnTitles.Audit(measure), kind)
    ];

    private static string? Turnaround(TimeSpan? value, IAdministrationReportLabels labels) =>
        value is { } duration ? labels.Turnaround(duration) : null;
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
    decimal AuditAgreedFeeTotal = 0,
    int AuditFeeNotesGenerated = 0)
{
    // MI-02: each total splits into Inspection and Audit reports.
    public int InspectionReportsGenerated => ReportsGenerated - AuditReportsGenerated;

    public int InspectionFeeNotesGenerated => FeeNotesGenerated - AuditFeeNotesGenerated;

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
        || row.AgreedFeeTotal < 0
        // MI-02's Audit share is part of each total.
        || row.AuditReportsGenerated < 0
        || row.AuditReportsGenerated > row.ReportsGenerated
        || row.AuditFeeNotesGenerated < 0
        || row.AuditFeeNotesGenerated > row.FeeNotesGenerated
        || row.AuditSent < 0
        || row.AuditSent > row.Sent
        || row.AuditAgreedFeeTotal < 0
        || row.AuditAgreedFeeTotal > row.AgreedFeeTotal;
}

/// <summary>The one export: every Administration report for the period as one workbook.</summary>
public sealed class ExportAdministrationReports(IWorkbookWriter writer)
{
    public byte[] Execute(
        ActionActor actor,
        EngineerActivityReport engineerReport,
        PrincipalReportActivityReport principalReport,
        IReadOnlyList<MonthlyReportActivity> monthly,
        ReportOutcomesReport outcomes,
        IAdministrationReportLabels labels)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        return writer.Write(AdministrationReportTables.Build(engineerReport, principalReport, monthly, outcomes, labels));
    }
}
