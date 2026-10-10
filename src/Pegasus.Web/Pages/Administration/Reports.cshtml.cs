using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;
using System.Text;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReportsModel(
    GetEngineerActivityReport engineerReport,
    GetV1ActivityReport principalActivityReport,
    GetMonthlyReportActivity monthlyActivity,
    GetReportOutcomes reportOutcomes,
    ExportAdministrationReports export,
    IStaffAccountQueries staffAccounts,
    IUnidentifiedStore unidentifiedStore,
    GetCaseList getCaseList,
    ExportCaseList exportCaseList,
    ListCaseListPresets listCaseListPresets,
    SaveCaseListPreset saveCaseListPreset,
    RemoveCaseListPreset removeCaseListPreset,
    TimeProvider timeProvider) : AdministrationPageModel
{
    public const string WorkbookMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The Period choices (item H) by their query value.</summary>
    public static IReadOnlyList<(string Value, ReportPeriod Period, string Label)> Periods { get; } =
    [
        ("this-month", ReportPeriod.ThisMonth, "This month"),
        ("last-month", ReportPeriod.LastMonth, "Last month"),
        ("this-quarter", ReportPeriod.ThisQuarter, "This quarter"),
        ("last-12-months", ReportPeriod.Last12Months, "Last 12 months"),
        ("custom", ReportPeriod.Custom, "Custom")
    ];

    /// <summary>The Work choices (item D): every report, or one work's.</summary>
    public static IReadOnlyList<(string Value, string Label)> Works { get; } =
        [("all", "All"), ("inspection", "Inspection"), ("audit", "Audit")];

    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "period")] public string? Period { get; set; }
    [BindProperty(SupportsGet = true, Name = "engineerId")] public Guid? EngineerId { get; set; }

    /// <summary>Engineer activity's sort: <c>person</c>, <c>queries</c> or <c>reports</c>; anything else is the default order.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")] public string? Sort { get; set; }

    /// <summary>Engineer activity's direction: <c>desc</c> or anything else for ascending.</summary>
    [BindProperty(SupportsGet = true, Name = "dir")] public string? Direction { get; set; }

    /// <summary>Reports by Principal's sort: <c>code</c>, <c>produced</c>, <c>sent</c> or <c>fees</c> (item P).</summary>
    [BindProperty(SupportsGet = true, Name = "msort")] public string? PrincipalSort { get; set; }

    [BindProperty(SupportsGet = true, Name = "mdir")] public string? PrincipalDirection { get; set; }

    /// <summary>Reports by Principal's Work choice: <c>all</c>, <c>inspection</c> or <c>audit</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "work")] public string? Work { get; set; }

    /// <summary>By month's Work choice.</summary>
    [BindProperty(SupportsGet = true, Name = "workm")] public string? MonthWork { get; set; }

    /// <summary>From after To, or longer than a year: the period bar says so and no report is read (item B2).</summary>
    public bool PeriodInvalid { get; private set; }

    public EngineerActivityReport EngineerResult { get; private set; } = new(default, default, []);
    public bool EngineerActivityUnavailable { get; private set; }

    /// <summary>The Person choices: the staff with activity in the period, and the chosen person (item N).</summary>
    public IReadOnlyList<(Guid Id, string Name)> People { get; private set; } = [];

    /// <summary>
    /// The per-Principal read for the period. <see langword="null"/> means the
    /// query failed or returned invalid data; the page renders that as an
    /// unavailable state rather than a false zero.
    /// </summary>
    public PrincipalReportActivityReport? PrincipalActivity { get; private set; }

    /// <summary>By month; <see langword="null"/> when its query failed.</summary>
    public IReadOnlyList<MonthlyReportActivity>? Monthly { get; private set; }

    /// <summary>Outcomes (item O); <see langword="null"/> when its query failed.</summary>
    public ReportOutcomesReport? Outcomes { get; private set; }

    /// <summary>The open Unidentified queue now (item E); <see langword="null"/> when it could not be read.</summary>
    public UnidentifiedFigures? Unidentified { get; private set; }

    /// <summary>How many Unidentified items are open and when the oldest was received.</summary>
    public sealed record UnidentifiedFigures(int Count, DateTimeOffset? OldestReceivedAtUtc);

    /// <summary>The same reports for the period just before (item G); absent when they could not be read.</summary>
    public EngineerActivityReport? PreviousEngineer { get; private set; }

    public PrincipalReportActivityReport? PreviousPrincipal { get; private set; }

    public string PeriodValue => Periods.Any(item => item.Value == Period) ? Period! : "custom";

    public string WorkValue => Works.Any(item => item.Value == Work) ? Work! : "all";

    public string MonthWorkValue => Works.Any(item => item.Value == MonthWork) ? MonthWork! : "all";

    public bool Descending => string.Equals(Direction, "desc", StringComparison.OrdinalIgnoreCase);

    public bool PrincipalDescending => string.Equals(PrincipalDirection, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>A count's first click sorts largest first; a name's smallest first; a second click reverses (item P).</summary>
    public string SortDirectionFor(string column) => NextDirection(Sort, Descending, column, column == "person");

    public string PrincipalSortDirectionFor(string column) => NextDirection(PrincipalSort, PrincipalDescending, column, column == "code");

    public string? AriaSort(string column) => AriaSortOf(Sort, Descending, column);

    public string? PrincipalAriaSort(string column) => AriaSortOf(PrincipalSort, PrincipalDescending, column);

    /// <summary>A Reports by Principal measure for the chosen work.</summary>
    public int Produced(PrincipalReportActivity row) => ByWork(WorkValue, row.ReportsProduced, row.InspectionReportsProduced, row.AuditReportsProduced);

    public int Sent(PrincipalReportActivity row) => ByWork(WorkValue, row.Sent, row.InspectionSent, row.AuditSent);

    public decimal Fees(PrincipalReportActivity row) => ByWork(WorkValue, row.AgreedFeeTotal, row.InspectionAgreedFeeTotal, row.AuditAgreedFeeTotal);

    /// <summary>A By month measure for its own Work choice.</summary>
    public (int Produced, int FeeNotes, int Sent, decimal Fees) MonthFigures(MonthlyReportActivity row) => MonthWorkValue switch
    {
        "inspection" => (row.InspectionReportsGenerated, row.InspectionFeeNotesGenerated, row.InspectionSent, row.InspectionAgreedFeeTotal),
        "audit" => (row.AuditReportsGenerated, row.AuditFeeNotesGenerated, row.AuditSent, row.AuditAgreedFeeTotal),
        _ => (row.ReportsGenerated, row.FeeNotesGenerated, row.Sent, row.AgreedFeeTotal)
    };

    /// <summary>Reports by Principal's rows: a Principal with anything produced or sent, in the chosen order.</summary>
    public IReadOnlyList<PrincipalReportActivity> PrincipalRows => PrincipalActivity is { } report
        ? SortedPrincipals(report.Rows.Where(row => row.ReportsProduced > 0 || row.Sent > 0))
        : [];

    /// <summary>
    /// The page's state as query values, so a sort, a Work choice, Person and
    /// the period survive every link and form on the page (finding 7).
    /// Each override replaces a value; a null drops it.
    /// </summary>
    public Dictionary<string, string> State(params (string Key, string? Value)[] overrides)
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal);
        void Set(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) state[key] = value; else state.Remove(key);
        }

        Set("period", PeriodValue == "custom" ? null : PeriodValue);
        Set("from", From?.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture));
        Set("to", To?.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture));
        Set("engineerId", EngineerId?.ToString("D"));
        Set("sort", Sort);
        Set("dir", Sort is null ? null : Direction);
        Set("msort", PrincipalSort);
        Set("mdir", PrincipalSort is null ? null : PrincipalDirection);
        Set("work", WorkValue == "all" ? null : WorkValue);
        Set("workm", MonthWorkValue == "all" ? null : MonthWorkValue);
        foreach (var (key, value) in overrides)
        {
            Set(key, value);
        }

        return state;
    }

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
        if (!await LoadAsync(Reads.Page, cancellationToken)) return Forbid();
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
        if (!await LoadAsync(Reads.Page, cancellationToken)) return Forbid();
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

    // Each report's CSV is its sheet, reads only its own report and refuses
    // only when that read failed (item L); the workbook needs every report.
    public Task<IActionResult> OnGetCsvAsync(CancellationToken cancellationToken) =>
        CsvAsync(Reads.Engineer, () => EngineerActivityUnavailable ? null : AdministrationReportTables.EngineerActivity(EngineerResult, OperatorAdministrationReportLabels.Instance), "engineer-activity.csv", cancellationToken);

    public Task<IActionResult> OnGetPrincipalCsvAsync(CancellationToken cancellationToken) =>
        CsvAsync(Reads.Principal, () => PrincipalActivity is { } report ? AdministrationReportTables.ReportsByPrincipal(report with { Rows = PrincipalRows }) : null, "reports-by-principal.csv", cancellationToken);

    public Task<IActionResult> OnGetMonthsCsvAsync(CancellationToken cancellationToken) =>
        CsvAsync(Reads.Monthly, () => Monthly is { } months ? AdministrationReportTables.ByMonth(months) : null, "by-month.csv", cancellationToken);

    public Task<IActionResult> OnGetOutcomesCsvAsync(CancellationToken cancellationToken) =>
        CsvAsync(Reads.Outcomes, () => Outcomes is { } outcomes ? AdministrationReportTables.Outcomes(outcomes) : null, "outcomes.csv", cancellationToken);

    public Task<IActionResult> OnGetTurnaroundCsvAsync(CancellationToken cancellationToken) =>
        CsvAsync(Reads.Principal, () => PrincipalActivity is { } report ? AdministrationReportTables.Turnaround(report, OperatorAdministrationReportLabels.Instance) : null, "turnaround.csv", cancellationToken);

    public Task<IActionResult> OnGetQueuesCsvAsync(CancellationToken cancellationToken) =>
        CsvAsync(Reads.Principal, () => PrincipalActivity is { } report ? AdministrationReportTables.Queues(report) : null, "queues.csv", cancellationToken);

    /// <summary>Every report for the period as one workbook, a sheet each.</summary>
    public async Task<IActionResult> OnGetWorkbookAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(Reads.Workbook, cancellationToken)) return Forbid();
        if (!TryGetActor(out var actor)) return Forbid();
        if (PeriodInvalid || EngineerActivityUnavailable || PrincipalActivity is null || Monthly is null || Outcomes is null)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity);
        }

        var bytes = export.Execute(
            actor,
            EngineerResult,
            PrincipalActivity,
            Monthly,
            Outcomes,
            OperatorAdministrationReportLabels.Instance);
        var from = LondonCalendar.DateAt(EngineerResult.FromUtc).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var to = LondonCalendar.DateAt(EngineerResult.ToUtc).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return File(bytes, WorkbookMediaType, $"administration-reports-{from}-{to}.xlsx");
    }

    private async Task<IActionResult> CsvAsync(Reads report, Func<WorkbookSheet?> sheet, string fileName, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(report, cancellationToken)) return Forbid();
        return !PeriodInvalid && sheet() is { } table
            ? File(Encoding.UTF8.GetBytes(WorkbookSheetCsv.Write(table)), "text/csv; charset=utf-8", fileName)
            : StatusCode(StatusCodes.Status422UnprocessableEntity);
    }

    /// <summary>The reads a handler asks <see cref="LoadAsync"/> for: the page draws every report, a CSV only its own.</summary>
    [Flags]
    private enum Reads
    {
        Engineer = 1,
        Principal = 2,
        Monthly = 4,
        Outcomes = 8,
        Unidentified = 16,

        /// <summary>The Person choices and the period just before, which only the page draws.</summary>
        PageOnly = 32,
        Workbook = Engineer | Principal | Monthly | Outcomes,
        Page = Workbook | Unidentified | PageOnly
    }

    private async Task<bool> LoadAsync(Reads reads, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return false;
        var now = timeProvider.GetUtcNow();
        var choice = Periods.First(item => item.Value == PeriodValue).Period;
        var (from, to) = choice == ReportPeriod.Custom
            ? (From is { } localFrom ? LondonCalendar.ToUtc(localFrom) : (To is { } toFrom ? LondonCalendar.ToUtc(toFrom) : now).AddDays(-31),
               To is { } localTo ? LondonCalendar.ToUtc(localTo) : now)
            : ReportPeriods.Resolve(choice, now);
        From = LondonCalendar.TimeAt(from);
        To = LondonCalendar.TimeAt(to);
        if (!ReportPeriods.IsValid(from, to))
        {
            PeriodInvalid = true;
            return true;
        }

        // The per-Principal, month, outcome and queue reads are factory-backed
        // and independent. The account list and Engineer report share the
        // scoped staff context, so they remain serial while those are in flight.
        var (previousFrom, previousTo) = ReportPeriods.Previous(from, to);
        var page = reads.HasFlag(Reads.PageOnly);
        var principalTask = Read(reads.HasFlag(Reads.Principal), () => principalActivityReport.ExecuteAsync(actor, from, to, cancellationToken));
        var monthlyTask = Read(reads.HasFlag(Reads.Monthly), () => monthlyActivity.ExecuteAsync(actor, from, to, cancellationToken));
        var outcomesTask = Read(reads.HasFlag(Reads.Outcomes), () => reportOutcomes.ExecuteAsync(actor, from, to, cancellationToken));
        var unidentifiedTask = Read(reads.HasFlag(Reads.Unidentified), async () =>
        {
            var count = unidentifiedStore.CountOpenAsync(cancellationToken);
            var oldest = unidentifiedStore.OldestOpenReceivedAtUtcAsync(cancellationToken);
            await Task.WhenAll(count, oldest);
            return new UnidentifiedFigures(await count, await oldest);
        });
        var previousPrincipalTask = Read(page, () => principalActivityReport.ExecuteAsync(actor, previousFrom, previousTo, cancellationToken));

        var all = await Read(reads.HasFlag(Reads.Engineer), () => engineerReport.ExecuteAsync(actor, from, to, cancellationToken));
        EngineerActivityUnavailable = reads.HasFlag(Reads.Engineer) && all is null;
        if (all is not null)
        {
            EngineerResult = Sorted(all.For(EngineerId));
        }

        if (page && all is not null)
        {
            var people = all.People.Select(row => (row.EngineerId, row.DisplayName)).ToList();
            if (EngineerId is { } chosen && people.All(person => person.EngineerId != chosen))
            {
                var accounts = await staffAccounts.ListAsync(0, 100, cancellationToken);
                if (accounts.Accounts.FirstOrDefault(account => account.Id == chosen) is { } account)
                {
                    people.Add((account.Id, account.UserName));
                }
            }

            People = [.. people.OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(person => person.EngineerId)];
        }

        PreviousEngineer = (await Read(page && all is not null, () => engineerReport.ExecuteAsync(actor, previousFrom, previousTo, cancellationToken)))?.For(EngineerId);
        PrincipalActivity = await principalTask;
        Monthly = await monthlyTask;
        Outcomes = await outcomesTask;
        Unidentified = await unidentifiedTask;
        PreviousPrincipal = await previousPrincipalTask;
        return true;
    }

    /// <summary>
    /// A read the handler asked for. One that fails or returns invalid data
    /// is unavailable, never a zero; one it did not ask for is not run.
    /// </summary>
    private static async Task<T?> Read<T>(bool wanted, Func<Task<T>> read) where T : class
    {
        if (!wanted) return null;
        try
        {
            return await read();
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            && exception is not StaffAuthorizationException)
        {
            return null;
        }
    }

    private EngineerActivityReport Sorted(EngineerActivityReport report)
    {
        var rows = report.Rows;
        IOrderedEnumerable<EngineerActivityRow> ordered = Sort?.ToLowerInvariant() switch
        {
            "queries" => Descending ? rows.OrderByDescending(row => row.QueriesReceived) : rows.OrderBy(row => row.QueriesReceived),
            "reports" => Descending ? rows.OrderByDescending(row => row.ReportsSent) : rows.OrderBy(row => row.ReportsSent),
            "person" => Descending
                ? rows.OrderByDescending(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => 0)
        };
        return report with
        {
            Rows = [.. ordered.ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.EngineerId)]
        };
    }

    private PrincipalReportActivity[] SortedPrincipals(IEnumerable<PrincipalReportActivity> rows)
    {
        IOrderedEnumerable<PrincipalReportActivity> ordered = PrincipalSort?.ToLowerInvariant() switch
        {
            "produced" => PrincipalDescending ? rows.OrderByDescending(Produced) : rows.OrderBy(Produced),
            "sent" => PrincipalDescending ? rows.OrderByDescending(Sent) : rows.OrderBy(Sent),
            "fees" => PrincipalDescending ? rows.OrderByDescending(Fees) : rows.OrderBy(Fees),
            "code" => PrincipalDescending
                ? rows.OrderByDescending(row => row.PrincipalCode, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.PrincipalCode, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => 0)
        };
        return [.. ordered.ThenBy(row => row.PrincipalCode, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.PrincipalId)];
    }

    private static string NextDirection(string? current, bool descending, string column, bool textual) =>
        string.Equals(current, column, StringComparison.OrdinalIgnoreCase)
            ? descending ? "asc" : "desc"
            : textual ? "asc" : "desc";

    private static string? AriaSortOf(string? current, bool descending, string column) =>
        string.Equals(current, column, StringComparison.OrdinalIgnoreCase) ? (descending ? "descending" : "ascending") : null;

    private static T ByWork<T>(string work, T all, T inspection, T audit) => work switch
    {
        "inspection" => inspection,
        "audit" => audit,
        _ => all
    };
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
