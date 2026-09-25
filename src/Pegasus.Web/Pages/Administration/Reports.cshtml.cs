using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;
using System.Text;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReportsModel(
    GetEngineerActivityReport engineerReport,
    GetV1ActivityReport principalActivityReport,
    GetMonthlyReportActivity monthlyActivity,
    ExportAdministrationReports export,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : AdministrationPageModel
{
    public const string WorkbookMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "engineerId")] public Guid? EngineerId { get; set; }

    /// <summary>MI-01's sort: <c>person</c>, <c>queries</c> or <c>reports</c>; anything else is the default order.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")] public string? Sort { get; set; }

    /// <summary>MI-01's direction: <c>desc</c> or anything else for ascending.</summary>
    [BindProperty(SupportsGet = true, Name = "dir")] public string? Direction { get; set; }

    public EngineerActivityReport EngineerResult { get; private set; } = new(default, default, []);
    public bool EngineerActivityUnavailable { get; private set; }
    public IReadOnlyList<StaffAccountSummary> People { get; private set; } = [];

    /// <summary>
    /// The MI-02/MI-03 per-Principal read for the same period. <see langword="null"/>
    /// means the query failed or returned invalid data; the page renders that
    /// as an unavailable state rather than a false zero.
    /// </summary>
    public PrincipalReportActivityReport? PrincipalActivity { get; private set; }

    /// <summary>MI-02's month breakdown; <see langword="null"/> when its query failed.</summary>
    public IReadOnlyList<MonthlyReportActivity>? Monthly { get; private set; }

    public bool Descending => string.Equals(Direction, "desc", StringComparison.OrdinalIgnoreCase);

    public int MostReportsSent => EngineerResult.Rows.Count == 0 ? 0 : EngineerResult.Rows.Max(row => row.ReportsSent);

    public int MostQueriesReceived => EngineerResult.Rows.Count == 0 ? 0 : EngineerResult.Rows.Max(row => row.QueriesReceived);

    public string SortDirectionFor(string column) =>
        string.Equals(Sort, column, StringComparison.OrdinalIgnoreCase) && !Descending ? "desc" : "asc";

    public string SortArrow(string column) =>
        string.Equals(Sort, column, StringComparison.OrdinalIgnoreCase) ? (Descending ? "↓" : "↑") : string.Empty;

    public string? AriaSort(string column) =>
        string.Equals(Sort, column, StringComparison.OrdinalIgnoreCase) ? (Descending ? "descending" : "ascending") : null;

    /// <summary>MI-02's split column: the measure's own label with the work it counts, e.g. "Reports produced · Inspection".</summary>
    public static string InspectionColumn(string measure) =>
        $"{measure} · {OperatorLabels.CaseTypeName(CaseType.Inspection)}";

    /// <summary>MI-02's split column: the measure's own label with the work it counts, e.g. "Agreed fees · Audit".</summary>
    public static string AuditColumn(string measure) =>
        $"{measure} · {OperatorLabels.CaseTypeName(CaseType.Audit)}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ? Page() : Forbid();

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        if (ReportsUnavailable) return StatusCode(StatusCodes.Status422UnprocessableEntity);
        return File(
            Encoding.UTF8.GetBytes(EngineerActivityReportCsv.ToCsv(EngineerResult.Rows)),
            "text/csv; charset=utf-8",
            "engineer-activity.csv");
    }

    public async Task<IActionResult> OnGetPrincipalCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        if (ReportsUnavailable) return StatusCode(StatusCodes.Status422UnprocessableEntity);
        return File(
            Encoding.UTF8.GetBytes(ReportsByPrincipalCsv(PrincipalActivity)),
            "text/csv; charset=utf-8",
            "reports-by-principal.csv");
    }

    public async Task<IActionResult> OnGetTurnaroundCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        if (ReportsUnavailable) return StatusCode(StatusCodes.Status422UnprocessableEntity);
        return File(
            Encoding.UTF8.GetBytes(TurnaroundCsv(PrincipalActivity)),
            "text/csv; charset=utf-8",
            "turnaround.csv");
    }

    /// <summary>Every report for the period as one workbook, a sheet each plus the month breakdown.</summary>
    public async Task<IActionResult> OnGetWorkbookAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        if (!TryGetActor(out var actor)) return Forbid();
        if (EngineerActivityUnavailable || PrincipalActivity is null || Monthly is null)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity);
        }

        var bytes = export.Execute(actor, EngineerResult, PrincipalActivity, Monthly);
        var from = LondonCalendar.DateAt(EngineerResult.FromUtc).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var to = LondonCalendar.DateAt(EngineerResult.ToUtc).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return File(bytes, WorkbookMediaType, $"administration-reports-{from}-{to}.xlsx");
    }

    private bool ReportsUnavailable => EngineerActivityUnavailable || PrincipalActivity is null || Monthly is null;

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return false;
        var to = To is { } localTo ? LondonCalendar.ToUtc(localTo) : timeProvider.GetUtcNow();
        var from = From is { } localFrom ? LondonCalendar.ToUtc(localFrom) : to.AddDays(-31);
        // The per-Principal and month reads are factory-backed and independent.
        // The account list and Engineer report share the scoped staff context,
        // so they remain serial while those separate reads are in flight.
        var principalTask = principalActivityReport.ExecuteAsync(actor, from, to, cancellationToken);
        var monthlyTask = monthlyActivity.ExecuteAsync(actor, from, to, cancellationToken);
        try
        {
            var people = await staffAccounts.ListAsync(0, 100, cancellationToken);
            People = people.Accounts
                .Where(account => account.IsEnabled)
                .OrderBy(account => account.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(account => account.Id)
                .ToArray();
            EngineerResult = await engineerReport.ExecuteAsync(actor, from, to, EngineerId, cancellationToken);
            EngineerResult = EngineerResult with { Rows = Sorted(EngineerResult.Rows) };
            From ??= LondonCalendar.TimeAt(EngineerResult.FromUtc);
            To ??= LondonCalendar.TimeAt(EngineerResult.ToUtc);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, "Choose a valid date range.");
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            && exception is not StaffAuthorizationException)
        {
            EngineerActivityUnavailable = true;
        }

        try
        {
            PrincipalActivity = await principalTask;
        }
        catch (ArgumentOutOfRangeException)
        {
            // The invalid-period case is already reported above; the two
            // reports share one period filter.
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            && exception is not StaffAuthorizationException)
        {
            PrincipalActivity = null;
        }

        try
        {
            Monthly = await monthlyTask;
        }
        catch (ArgumentOutOfRangeException)
        {
            // As above: one period filter, reported once.
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            && exception is not StaffAuthorizationException)
        {
            Monthly = null;
        }

        return true;
    }

    private EngineerActivityRow[] Sorted(IReadOnlyList<EngineerActivityRow> rows)
    {
        IOrderedEnumerable<EngineerActivityRow> ordered = Sort?.ToLowerInvariant() switch
        {
            "queries" => Descending ? rows.OrderByDescending(row => row.QueriesReceived) : rows.OrderBy(row => row.QueriesReceived),
            "reports" => Descending ? rows.OrderByDescending(row => row.ReportsSent) : rows.OrderBy(row => row.ReportsSent),
            "person" => Descending
                ? rows.OrderByDescending(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => 0)
        };
        return ordered.ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.EngineerId).ToArray();
    }

    /// <summary>
    /// MI-02: per-Principal report counts by type for the period, each total
    /// beside its Inspection and Audit split. Mirrors exactly the columns
    /// <c>Reports.cshtml</c> renders for this section.
    /// </summary>
    private static string ReportsByPrincipalCsv(PrincipalReportActivityReport? report)
    {
        var header = string.Join(
            ",",
            "Principal",
            "Reports produced", InspectionColumn("Reports produced"), AuditColumn("Reports produced"),
            "Reports sent", InspectionColumn("Reports sent"), AuditColumn("Reports sent"),
            "Agreed fees", InspectionColumn("Agreed fees"), AuditColumn("Agreed fees"),
            "Report types");
        var builder = new StringBuilder(header).Append("\r\n");
        if (report is null) return builder.ToString();
        foreach (var row in report.Rows.Where(row => row.ReportsProduced > 0 || row.Sent > 0))
        {
            builder.Append(EngineerActivityReportCsv.EscapeField(row.PrincipalCode)).Append(',')
                .Append(row.ReportsProduced).Append(',')
                .Append(row.InspectionReportsProduced).Append(',')
                .Append(row.AuditReportsProduced).Append(',')
                .Append(row.Sent).Append(',')
                .Append(row.InspectionSent).Append(',')
                .Append(row.AuditSent).Append(',')
                .Append(row.AgreedFeeTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.InspectionAgreedFeeTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.AuditAgreedFeeTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(EngineerActivityReportCsv.EscapeField(string.Join(
                    "; ",
                    row.ArtifactTypes
                        .Where(type => type.Generated > 0)
                        .Select(type => $"{OperatorLabels.ReportKind(type.Kind)} {type.Generated}"))))
                .Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// MI-03: holding age and instruction-to-produced/ready/sent turnaround
    /// per Principal for the period. Mirrors exactly the columns
    /// <c>Reports.cshtml</c> renders for this section.
    /// </summary>
    private static string TurnaroundCsv(PrincipalReportActivityReport? report)
    {
        var builder = new StringBuilder(
            "Principal,Currently held,Oldest held since,Time to produce,Time to ready,Time to send").Append("\r\n");
        if (report is null) return builder.ToString();
        foreach (var row in report.Rows.Where(row =>
            row.CurrentHeldCases > 0
            || row.AverageReceivedToGeneration.HasValue
            || row.AverageReceivedToReady.HasValue
            || row.AverageReceivedToSent.HasValue))
        {
            builder.Append(EngineerActivityReportCsv.EscapeField(row.PrincipalCode)).Append(',')
                .Append(row.CurrentHeldCases).Append(',')
                .Append(OperatorLabels.OfficeTime(row.OldestHeldAtUtc, string.Empty)).Append(',')
                .Append(OperatorLabels.ReportTurnaround(row.AverageReceivedToGeneration, string.Empty)).Append(',')
                .Append(OperatorLabels.ReportTurnaround(row.AverageReceivedToReady, string.Empty)).Append(',')
                .Append(OperatorLabels.ReportTurnaround(row.AverageReceivedToSent, string.Empty))
                .Append("\r\n");
        }

        return builder.ToString();
    }
}
