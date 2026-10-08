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
    GetCaseList getCaseList,
    ExportCaseList exportCaseList,
    ListCaseListPresets listCaseListPresets,
    SaveCaseListPreset saveCaseListPreset,
    RemoveCaseListPreset removeCaseListPreset,
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
    public static string InspectionColumn(string measure) => ReportColumnTitles.Inspection(measure);

    /// <summary>MI-02's split column: the measure's own label with the work it counts, e.g. "Agreed fees · Audit".</summary>
    public static string AuditColumn(string measure) => ReportColumnTitles.Audit(measure);

    /// <summary>The Case list preset whose columns the page ticks.</summary>
    [BindProperty(SupportsGet = true, Name = "preset")] public Guid? PresetId { get; set; }

    /// <summary>The shared Case list presets; <see langword="null"/> when they could not be read.</summary>
    public IReadOnlyList<CaseListPreset>? CaseListPresets { get; private set; }

    public CaseListPreset? SelectedPreset => CaseListPresets?.FirstOrDefault(preset => preset.Id == PresetId);

    /// <summary>The Case list form as the page shows it: its defaults, the chosen preset's columns, or what was posted.</summary>
    public CaseListInput CaseList { get; private set; } = new();

    public string? CaseListError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        await LoadCaseListAsync(null, cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostCaseListCsvAsync(CaseListInput input, CancellationToken cancellationToken) =>
        DownloadCaseListAsync(
            input,
            (_, result) => File(
                Encoding.UTF8.GetBytes(WorkbookSheetCsv.Write(CaseListTables.Build(result, OperatorCaseListLabels.Instance))),
                "text/csv; charset=utf-8",
                CaseListFileName(result, "csv")),
            cancellationToken);

    public Task<IActionResult> OnPostCaseListWorkbookAsync(CaseListInput input, CancellationToken cancellationToken) =>
        DownloadCaseListAsync(
            input,
            (actor, result) => File(
                exportCaseList.Execute(actor, result, OperatorCaseListLabels.Instance),
                WorkbookMediaType,
                CaseListFileName(result, "xlsx")),
            cancellationToken);

    public Task<IActionResult> OnPostCaseListPresetCreateAsync(CaseListInput input, CancellationToken cancellationToken) =>
        RunPresetAsync(
            input,
            async actor =>
            {
                var preset = await saveCaseListPreset.ExecuteAsync(
                    new(input.NewPresetId, input.PresetName ?? string.Empty, input.Columns, ExpectedVersion: 0, actor, input.OperationKey ?? string.Empty),
                    cancellationToken);
                return (OperatorLabels.CaseList.PresetCreated, (Guid?)preset.Id);
            },
            cancellationToken);

    public Task<IActionResult> OnPostCaseListPresetSaveAsync(CaseListInput input, CancellationToken cancellationToken) =>
        RunPresetAsync(
            input,
            async actor =>
            {
                var preset = await saveCaseListPreset.ExecuteAsync(
                    new(input.PresetId ?? Guid.Empty, input.PresetName ?? string.Empty, input.Columns, input.ExpectedVersion, actor, input.OperationKey ?? string.Empty),
                    cancellationToken);
                return (OperatorLabels.CaseList.PresetSaved, (Guid?)preset.Id);
            },
            cancellationToken);

    public Task<IActionResult> OnPostCaseListPresetRemoveAsync(CaseListInput input, CancellationToken cancellationToken) =>
        RunPresetAsync(
            input,
            async actor =>
            {
                await removeCaseListPreset.ExecuteAsync(
                    new(input.PresetId ?? Guid.Empty, input.ExpectedVersion, actor, input.OperationKey ?? string.Empty),
                    cancellationToken);
                return (OperatorLabels.CaseList.PresetRemoved, null);
            },
            cancellationToken);

    /// <summary>
    /// MI-04's downloads run only the Case list. A refusal answers a script's
    /// fetch with its reason as text; without script the page shows it.
    /// </summary>
    private async Task<IActionResult> DownloadCaseListAsync(
        CaseListInput input,
        Func<ActionActor, CaseListResult, IActionResult> file,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        string message;
        try
        {
            var result = await getCaseList.ExecuteAsync(
                new(
                    actor,
                    input.AllTime ? null : input.ReceivedFrom,
                    input.AllTime ? null : input.ReceivedTo,
                    input.IncludeTriage,
                    input.Columns),
                cancellationToken);
            return file(actor, result);
        }
        catch (ArgumentOutOfRangeException)
        {
            message = OperatorLabels.CaseList.ChoosePeriod;
        }
        catch (ArgumentException)
        {
            message = OperatorLabels.CaseList.ChooseColumns;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message = OperatorLabels.CaseList.Unavailable;
        }

        if (IsScriptRequest)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
                Content = message,
                ContentType = "text/plain; charset=utf-8"
            };
        }

        return await RedisplayAsync(input, message, cancellationToken);
    }

    private async Task<IActionResult> RunPresetAsync(
        CaseListInput input,
        Func<ActionActor, Task<(string Confirmation, Guid? PresetId)>> operation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!IsOperationKeyValid(input.OperationKey))
        {
            return await RedisplayAsync(input, OperatorLabels.CaseList.PresetExpired, cancellationToken);
        }

        string message;
        try
        {
            var (confirmation, presetId) = await operation(actor);
            TempData["Confirmation"] = confirmation;
            return RedirectToPage(new { preset = presetId });
        }
        catch (CaseListPresetException exception)
        {
            message = exception.Error switch
            {
                CaseListPresetError.DuplicateName => OperatorLabels.CaseList.PresetDuplicateName,
                CaseListPresetError.NotFound or CaseListPresetError.Removed => OperatorLabels.CaseList.PresetNotFound,
                CaseListPresetError.VersionConflict => OperatorLabels.CaseList.PresetStale,
                CaseListPresetError.OperationConflict => OperatorLabels.CaseList.PresetExpired,
                _ => OperatorLabels.CaseList.PresetNotAccepted
            };
        }
        catch (ArgumentException exception) when (exception.ParamName == "name")
        {
            message = OperatorLabels.CaseList.PresetNameRequired;
        }
        catch (ArgumentException exception) when (exception.ParamName == "keys")
        {
            message = OperatorLabels.CaseList.ChooseColumns;
        }
        catch (ArgumentException)
        {
            message = OperatorLabels.CaseList.PresetNotAccepted;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return await RedisplayAsync(input, message, cancellationToken);
    }

    /// <summary>The page again, with the Case list form as posted and the reason it was refused.</summary>
    private async Task<IActionResult> RedisplayAsync(CaseListInput input, string message, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        await LoadCaseListAsync(input, cancellationToken);
        CaseListError = message;
        return Page();
    }

    private async Task LoadCaseListAsync(CaseListInput? posted, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return;
        try
        {
            CaseListPresets = await listCaseListPresets.ExecuteAsync(actor, cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            && exception is not StaffAuthorizationException)
        {
            CaseListPresets = null;
        }

        if (posted is not null)
        {
            PresetId = posted.PresetId;
            CaseList = posted with { OperationKey = NewOperationKey(), NewPresetId = Guid.NewGuid() };
            return;
        }

        var today = LondonCalendar.DateAt(timeProvider.GetUtcNow());
        var preset = SelectedPreset;
        CaseList = new()
        {
            ReceivedFrom = today.AddDays(-31),
            ReceivedTo = today,
            Columns = [.. preset?.ColumnKeys ?? CaseListColumns.DefaultKeys],
            PresetId = preset?.Id,
            ExpectedVersion = preset?.Version ?? 0,
            PresetName = preset?.Name,
        };
    }

    private static string CaseListFileName(CaseListResult result, string extension) =>
        result.ReceivedFrom is { } from && result.ReceivedTo is { } to
            ? $"case-list-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.{extension}"
            : $"case-list-all-time.{extension}";

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
            "Agreed fees", InspectionColumn("Agreed fees"), AuditColumn("Agreed fees"));
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
                .Append(row.AuditAgreedFeeTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
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

/// <summary>
/// The Case list form (MI-04): its period or All time, whether Triage Cases
/// are in, the chosen columns, and the preset the Save and Remove actions
/// address with its version and replay key.
/// </summary>
public sealed record CaseListInput
{
    public DateOnly? ReceivedFrom { get; init; }
    public DateOnly? ReceivedTo { get; init; }
    public bool AllTime { get; init; }
    public bool IncludeTriage { get; init; }
    public string[] Columns { get; init; } = [];
    public Guid? PresetId { get; init; }
    public long ExpectedVersion { get; init; }
    public string? PresetName { get; init; }
    public Guid NewPresetId { get; init; } = Guid.NewGuid();
    public string? OperationKey { get; init; } = StaffPageModel.NewOperationKey();
}
