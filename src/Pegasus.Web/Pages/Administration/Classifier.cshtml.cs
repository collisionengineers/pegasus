using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// Administration › Mail classifier (FRD-17; operator, 27 November 2026;
/// R1318 to R1320): the classifier's health, its runs newest first and, in a
/// row dialog as the Intake log opens a received file, one run's facts, its
/// summary and what it did with each e-mail. Production shows its own runs;
/// the hosted test instance shows its own and, where it has been given the
/// read, the live classifier's, each with its environment. Administrators
/// only.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ClassifierModel(
    ListClassifierRuns listRuns,
    TimeProvider timeProvider,
    IConfiguration configuration) : AdministrationPageModel
{
    private const int DefaultDays = 7;

    /// <summary>The period, as London dates; To is the last day shown.</summary>
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public ClassifierRunOutcome? Outcome { get; set; }
    [BindProperty(SupportsGet = true)] public string? Acted { get; set; }
    [BindProperty(SupportsGet = true)] public ClassifierRunEnvironment? Environment { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    // "page" is Razor Pages' own route value, so the list pages by pageNumber, as Problem reports does.
    [BindProperty(SupportsGet = true, Name = "pageNumber")] public int CurrentPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public Guid? Run { get; set; }
    [BindProperty(SupportsGet = true)] public ClassifierRunEnvironment? RunEnvironment { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Day { get; set; }

    public ClassifierRunsView? Listing { get; private set; }
    public ClassifierRunDetail? Detail { get; private set; }
    /// <summary>Why a run asked for is not shown: it is not recorded, or the live runs could not be read.</summary>
    public string? DetailMessage { get; private set; }
    public DateTimeOffset NowUtc { get; private set; }

    /// <summary>Whose runs this deployment records: production's are the live ones.</summary>
    public ClassifierRunEnvironment LocalEnvironment => string.Equals(
        configuration["Runtime:Profile"], "Production", StringComparison.Ordinal)
        ? ClassifierRunEnvironment.Live
        : ClassifierRunEnvironment.Test;

    /// <summary>The test instance shows two environments, so each row names its own.</summary>
    public bool ShowsEnvironments => LocalEnvironment == ClassifierRunEnvironment.Test;

    public bool OnlyActed => string.Equals(Acted, "acted", StringComparison.Ordinal);

    public bool OldestFirst => string.Equals(Sort, "oldest", StringComparison.Ordinal);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        NowUtc = timeProvider.GetUtcNow();
        var today = LondonCalendar.DateAt(NowUtc);
        To ??= today;
        From ??= To.Value.AddDays(-(DefaultDays - 1));
        if (CurrentPage < 1 || CurrentPage > ClassifierRunPolicy.MaximumPage)
        {
            CurrentPage = 1;
        }

        var fromUtc = LondonCalendar.StartOfDay(From.Value);
        var toUtc = LondonCalendar.StartOfNextDay(To.Value) ?? NowUtc;
        if (From > To || toUtc - fromUtc > ClassifierRunPolicy.MaximumPeriod)
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.ClassifierRuns.PeriodInvalid);
            return Page();
        }

        Listing = await listRuns.ExecuteAsync(
            new(
                actor,
                LocalEnvironment,
                fromUtc,
                toUtc,
                Outcome is { } outcome && Enum.IsDefined(outcome) ? outcome : null,
                OnlyActed,
                ShowsEnvironments && Environment is { } environment && Enum.IsDefined(environment) ? environment : null,
                OldestFirst,
                CurrentPage),
            cancellationToken);
        if (Run is { } runId && runId != Guid.Empty)
        {
            try
            {
                Detail = await listRuns.GetAsync(
                    actor,
                    LocalEnvironment,
                    RunEnvironment ?? LocalEnvironment,
                    runId,
                    Day,
                    cancellationToken);
                DetailMessage = Detail is null ? OperatorLabels.ClassifierRuns.RunNotFound : null;
            }
            catch (ProductionClassifierRunsUnavailableException)
            {
                DetailMessage = OperatorLabels.ClassifierRuns.LiveUnavailable;
            }
        }

        return Page();
    }

    public ClassifierRunOutcome OutcomeOf(ClassifierRunSummary run) => ClassifierRunPolicy.OutcomeAt(run, NowUtc);

    /// <summary>The list's address with the current filters, at a page, sort or run.</summary>
    public string ListUrl(
        int? page = null,
        bool? oldestFirst = null,
        ClassifierRunSummary? run = null)
    {
        var query = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) query.Add(key + "=" + Uri.EscapeDataString(value));
        }

        Add("From", From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add("To", To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add("Outcome", Outcome?.ToString());
        Add("Acted", OnlyActed ? "acted" : null);
        Add("Environment", ShowsEnvironments ? Environment?.ToString() : null);
        Add("Sort", (oldestFirst ?? OldestFirst) ? "oldest" : null);
        var target = page ?? CurrentPage;
        if (target > 1) Add("pageNumber", target.ToString(CultureInfo.InvariantCulture));
        if (run is not null)
        {
            Add("Run", run.Id.ToString("D"));
            Add("RunEnvironment", run.Environment.ToString());
            if (run.Environment != LocalEnvironment)
            {
                Add("Day", DateOnly.FromDateTime(run.StartedAtUtc.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        return "/Administration/Classifier" + (query.Count == 0 ? string.Empty : "?" + string.Join('&', query));
    }

    /// <summary>
    /// The Action logs of one of this deployment's runs: the Automation
    /// Actor's connector calls from its start to its close, the window the
    /// classifier's calls fall in (a run's calls carry no run id of their own).
    /// </summary>
    public string? ActionLogsUrl(ClassifierRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        if (detail.Summary.Environment != LocalEnvironment || string.IsNullOrWhiteSpace(detail.RecordedBy))
        {
            return null;
        }

        var started = detail.Summary.StartedAtUtc;
        var finished = detail.Summary.FinishedAtUtc ?? started + ClassifierRunPolicy.RunningWindow;
        var from = LondonCalendar.TimeAt(started);
        var to = LondonCalendar.TimeAt(finished).AddMinutes(1);
        static string Minute(DateTime value) =>
            new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified)
                .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        return "/Administration/Logs?Area=" + Uri.EscapeDataString(AutomationMcpAuditor.AutomationAggregateType)
            + "&Actor=" + Uri.EscapeDataString(detail.RecordedBy)
            + "&From=" + Uri.EscapeDataString(Minute(from))
            + "&To=" + Uri.EscapeDataString(Minute(to))
            + "&Sort=oldest";
    }

    /// <summary>What a run did, as the list's one cell reads it: each act with its count.</summary>
    public string Actions(ClassifierRunSummary run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.ClosedOutcome is null)
        {
            return "—";
        }

        var parts = run.Actions
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key)
            .Select(pair => OperatorLabels.ClassifierRuns.ActionCount(pair.Key, pair.Value, run.DryRun))
            .ToArray();
        return parts.Length == 0 ? OperatorLabels.ClassifierRuns.Nothing : string.Join(" · ", parts);
    }

    public static string Cost(decimal? cost) => cost is { } value ? OperatorLabels.ClassifierRuns.Usd(value) : "—";

    public static string Took(int? durationMs) => durationMs is { } value ? OperatorLabels.ClassifierRuns.Duration(value) : "—";

    public static string Count(int? count) => count?.ToString(CultureInfo.InvariantCulture) ?? "—";

    public static string Mode(bool dryRun) => dryRun ? OperatorLabels.ClassifierRuns.DryRun : OperatorLabels.ClassifierRuns.Write;

    /// <summary>The model's summary as its paragraphs.</summary>
    public static IReadOnlyList<string> Paragraphs(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
