using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Notifications;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.WorkCentre;

namespace Pegasus.Web.Pages;

/// <summary>
/// The Work Centre (v42 Design A): Office queues beside Activity, then one
/// tabbed panel. New cases is the landing tab and stamps the last look; System
/// Actions, AI Actions and Needs attention follow. A tab is omitted when its
/// section is empty and kept when it is unavailable. One search box applies to
/// the active tab.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public partial class IndexModel(
    IGetOperationsSnapshot getOperationsSnapshot,
    IListRecentCases listRecentCases,
    IAiJobQueries aiJobQueries,
    IAiJobStore aiJobStore,
    ICaseWorkflowConfiguration workflowConfiguration,
    IStaffAccountQueries staffAccounts,
    IGetCaseHeader getCaseHeader,
    IGetCaseEditBasis getCaseEditBasis,
    IAcquireCaseEditLease acquireLease,
    IReleaseCaseEditLease releaseLease,
    IAssignCaseEngineer assignEngineer,
    IAssignCaseQuery assignQuery,
    IConfirmAiJob confirmAiJob,
    IListWorkCentreAiJobs listAiJobs,
    IListSystemActions listSystemActions,
    IWorkCentreDismissalStore dismissalStore,
    IDismissWorkCentreItem dismissItem,
    IGetWorkCentreActivity getActivity,
    TimeProvider timeProvider,
    ILogger<IndexModel> logger) : StaffPageModel
{
    public const string NewCasesTab = "new-cases";
    public const string SystemActionsTab = "system-actions";
    public const string AiActionsTab = "ai-actions";
    public const string AttentionTab = "attention";

    public DateTimeOffset NowUtc { get; private set; }

    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public NeedsAttentionScope Scope { get; private set; }

    /// <summary>True when the address named the scope; otherwise the page opened on the person's default.</summary>
    public bool ScopeNamed { get; private set; }

    public IReadOnlyList<NeedsAttentionKind> Kinds { get; private set; } = [];

    /// <summary>Find within Needs attention (v30 WB): trimmed, null when empty.</summary>
    public string? Search { get; private set; }

    /// <summary>The section the panel shows. Landing on <c>/</c> is New cases (v42 Design A).</summary>
    public string Tab { get; private set; } = NewCasesTab;

    /// <summary>The New cases channel sub-filter; null is every channel.</summary>
    public CaseArrival? Arrival { get; private set; }

    /// <summary>The System Actions sub-filter.</summary>
    public SystemActionFilter SystemFilter { get; private set; } = SystemActionFilter.All;

    /// <summary>The AI Actions sub-filter.</summary>
    public WorkCentreAiActionFilter AiFilter { get; private set; } = WorkCentreAiActionFilter.All;

    public bool AttentionFiltered => Kinds.Count > 0 || Search is not null;

    public int CurrentPage { get; private set; } = 1;

    public int NewCasesPage { get; private set; } = 1;

    public int SystemActionsPage { get; private set; } = 1;

    public NeedsAttentionPage? Attention { get; private set; }

    /// <summary>The chip counts: the scope's whole list, before the kind filter.</summary>
    public IReadOnlyDictionary<NeedsAttentionKind, int> KindCounts { get; private set; } =
        new Dictionary<NeedsAttentionKind, int>();

    public WorkCentreMetrics? Metrics { get; private set; }

    public NeedsAttentionItem? Selected { get; private set; }

    /// <summary>The signed-in person's staff id: Take posts it, and the Assign Engineer list marks it "(you)".</summary>
    public Guid? ViewerStaffId { get; private set; }

    public WorkCentreAssignment? Assignment { get; private set; }

    /// <summary>An Assign Engineer option's words: the signed-in person reads "Name (you)" (#1213).</summary>
    public string EngineerChoiceLabel(WorkCentreEngineer engineer)
    {
        ArgumentNullException.ThrowIfNull(engineer);
        return engineer.Id == ViewerStaffId ? OperatorLabels.Triage.You(engineer.Name) : engineer.Name;
    }

    public bool OpenAssignment { get; private set; }

    /// <summary>True only when the live Work Centre query could not be read.</summary>
    public bool IsUnavailable { get; private set; }

    public bool AttentionFailed => IsUnavailable || Attention is null || Metrics is null;

    /// <summary>The scope's whole list before the kind and search filters.</summary>
    public int ScopeTotal => KindCounts.Values.Sum();

    /// <summary>
    /// Needs attention shows when the scope has work, when a filter is on (so
    /// Clear filters stays reachable), on Mine (so the switch back to Office
    /// stays reachable) and when the read failed; an empty Office is omitted.
    /// </summary>
    public bool ShowAttention => AttentionFailed || ScopeTotal > 0 || AttentionFiltered || Scope == NeedsAttentionScope.Mine;

    public bool NewCasesFailed => NewCasesUnavailable || NewCases is null;

    /// <summary>The window's Cases before the channel and search filters, so an empty filter keeps the tab.</summary>
    public int NewCasesWindowCount => NewCases?.Page.ArrivalCounts.Values.Sum() ?? 0;

    public bool ShowNewCases => NewCasesFailed || NewCasesWindowCount > 0
        || Arrival is not null || (Tab == NewCasesTab && Search is not null);

    public bool SystemActionsFailed => SystemActionsUnavailable || SystemActions is null;

    public int SystemActionsWindowCount => SystemActions?.KindCounts.Values.Sum() ?? 0;

    public bool ShowSystemActions => SystemActionsFailed || SystemActionsWindowCount > 0
        || SystemFilter != SystemActionFilter.All || (Tab == SystemActionsTab && Search is not null);

    public bool ShowAiJobs => AiJobsUnavailable || AiJobs.Count > 0
        || AiFilter != WorkCentreAiActionFilter.All || (Tab == AiActionsTab && Search is not null);

    /// <summary>The whole page has nothing to show and no read failed to say so.</summary>
    public bool NothingToShow => !ShowAttention && !ShowNewCases && !ShowSystemActions && !ShowAiJobs;

    public IReadOnlyList<string> VisibleTabs =>
        new[]
        {
            (NewCasesTab, ShowNewCases),
            (SystemActionsTab, ShowSystemActions),
            (AiActionsTab, ShowAiJobs),
            (AttentionTab, ShowAttention)
        }
            .Where(tab => tab.Item2)
            .Select(tab => tab.Item1)
            .ToArray();

    public RecentCasesFeed? NewCases { get; private set; }

    /// <summary>Where the "since you last looked" line falls; a refresh keeps the line the page opened with.</summary>
    public DateTimeOffset? DividerUtc { get; private set; }

    public bool NewCasesUnavailable { get; private set; }

    public IReadOnlyList<WorkCentreAiJobRow> AiJobs { get; private set; } = [];

    /// <summary>The AI Actions table: the window narrowed by the sub-filter and by Find.</summary>
    public IReadOnlyList<WorkCentreAiJobRow> ShownAiJobs => AiJobs
        .Where(row => WorkCentreAiActionPolicy.Matches(AiFilter, row.Job)
            && (Search is null || Tab != AiActionsTab || MatchesAiSearch(row)))
        .ToArray();

    public bool AiJobsUnavailable { get; private set; }

    public SystemActionPage? SystemActions { get; private set; }

    public bool SystemActionsUnavailable { get; private set; }

    /// <summary>The Activity figures (FRD-15): null until read, and never drawn as zero when the read failed.</summary>
    public WorkCentreActivityCounts? Activity { get; private set; }

    public bool ActivityUnavailable { get; private set; }

    public bool ActivityFailed => ActivityUnavailable || Activity is null;

    /// <summary>True when one or more independently rendered live sections could not be read.</summary>
    public bool HasReadFailure => IsUnavailable || NewCasesUnavailable || SystemActionsUnavailable || AiJobsUnavailable || ActivityUnavailable;

    /// <summary>The fragment outcome is failed only when no independently rendered section was read.</summary>
    public string RefreshOutcome => !HasReadFailure
        ? "current"
        : IsUnavailable && NewCasesUnavailable && SystemActionsUnavailable && AiJobsUnavailable && ActivityUnavailable
            ? "failed"
            : "partial";

    /// <summary>The head's freshness words: "Updated HH:MM" (FRD-15) unless a section failed.</summary>
    public string RefreshOutcomeLabel => RefreshOutcome switch
    {
        "failed" => "Refresh unavailable",
        "partial" => "Partially refreshed",
        _ => LoadedAtUtc is { } loaded ? Labels.Updated(loaded) : "Current"
    };

    [TempData(Key = "WorkCentreStatus")]
    public string? StatusMessage { get; set; }

    [TempData(Key = "WorkCentreError")]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string? scope,
        [FromQuery(Name = "kind")] string[]? kind,
        string? q,
        string? tab,
        string? arrival,
        string? sys,
        string? ai,
        Guid? selected,
        bool assign,
        bool refresh,
        string? since,
        CancellationToken cancellationToken,
        // "page" is also the Razor Pages route value naming this page, which
        // outranks the query string; bind the list's page from the query.
        [FromQuery(Name = "page")] int page = 1,
        int newPage = 1,
        int sysPage = 1)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.main");
        var refusal = await LoadAsync(
            scope, kind, q, tab, arrival, sys, ai, selected, assign, refresh, since, page, newPage, sysPage, cancellationToken);
        return refusal ?? Page();
    }

    /// <summary>
    /// Refreshes only the Work Centre body. The global rail filter intentionally
    /// ignores this <see cref="PartialViewResult"/>, because the response never
    /// renders the shell.
    /// </summary>
    public async Task<IActionResult> OnGetRefreshAsync(
        string? scope,
        [FromQuery(Name = "kind")] string[]? kind,
        string? q,
        string? tab,
        string? arrival,
        string? sys,
        string? ai,
        Guid? selected,
        string? since,
        CancellationToken cancellationToken,
        [FromQuery(Name = "page")] int page = 1,
        int newPage = 1,
        int sysPage = 1)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.refresh.main");
        // A refresh is never the one-shot instruction to reopen an assignment
        // dialog. The current display may retain a dismissed dialog's selection.
        var refusal = await LoadAsync(
            scope,
            kind,
            q,
            tab,
            arrival,
            sys,
            ai,
            selected,
            assign: false,
            refresh: true,
            since: since,
            page: page,
            newPage: newPage,
            sysPage: sysPage,
            cancellationToken: cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        LogRefreshOutcome(
            logger, !IsUnavailable, !NewCasesUnavailable, !SystemActionsUnavailable, !AiJobsUnavailable, !ActivityUnavailable);
        return Partial("_WorkCentreBody", this);
    }

    /// <summary>
    /// Assign Engineer from the expanded row (P4), and Take, which posts the
    /// viewer's own staff id (#1213). The Work Centre holds no edit session, so
    /// the handler claims the Case's lease, runs the same Core assignment the
    /// Case record's dialog runs, and returns here; a refused assignment
    /// releases the lease it claimed. Core composes the history reason.
    /// </summary>
    public Task<IActionResult> OnPostAssignEngineerAsync(
        Guid caseId,
        string? engineerId,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken) =>
        WithCaseLeaseAsync(
            caseId,
            returnUrl,
            "assign_engineer",
            async (actor, lease) =>
            {
                await assignEngineer.ExecuteAsync(
                    new AssignCaseEngineerRequest(
                        caseId,
                        lease.Version,
                        actor,
                        operationKey,
                        string.Empty,
                        lease.Token,
                        AssignEngineerChoice.Parse(engineerId)),
                    cancellationToken);
            },
            Labels.Assigned,
            cancellationToken);

    /// <summary>
    /// Take on an Unassigned open query (operator, 27 October 2026). Posts the
    /// viewer's staff id through the same assignment a Case row's Take uses,
    /// and returns to the Work Centre. No Case lease: a query act takes none.
    /// </summary>
    public async Task<IActionResult> OnPostTakeQueryAsync(
        Guid queryId,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var staffId = Pegasus.Core.Notifications.StaffNotificationPolicy.StaffId(actor)
                ?? throw new InvalidOperationException("Take is a staff act.");
            await assignQuery.ExecuteAsync(
                new(queryId, actor, operationKey, staffId),
                cancellationToken);
            StatusMessage = Labels.Assigned;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, "take_query", queryId, exception);
            ErrorMessage = Labels.AssignRefused;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    /// <summary>Complete job on a Draft ready Query response or queue pass (D9).</summary>
    public async Task<IActionResult> OnPostCompleteAiJobAsync(
        Guid jobId,
        long expectedVersion,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var job = await aiJobStore.GetAsync(jobId, cancellationToken);
            if (job is null || !AiJobPolicy.CompletesByHand(job))
            {
                throw new InvalidOperationException("The job cannot be completed by hand.");
            }

            await confirmAiJob.ExecuteAsync(
                new ConfirmAiJobCommand(jobId, expectedVersion, actor, operationKey),
                cancellationToken);
            StatusMessage = Labels.JobCompleted;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, "complete_ai_job", jobId, exception);
            ErrorMessage = Labels.JobRefused;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    /// <summary>
    /// Dismiss on any row (FRD-15): the record's rows leave every tab, for
    /// everyone, until it next qualifies. The row's leaving is the only notice.
    /// </summary>
    public async Task<IActionResult> OnPostDismissAsync(
        Guid recordId,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await dismissItem.ExecuteAsync(new DismissWorkCentreItemRequest(recordId, actor), cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    /// <summary>
    /// This page's address with the given state changed and the rest kept. The
    /// search term and tab travel with every address unless cleared or named.
    /// </summary>
    public string PageUrl(
        NeedsAttentionScope? scope = null,
        IEnumerable<NeedsAttentionKind>? kinds = null,
        int? page = null,
        Guid? selected = null,
        int? newPage = null,
        int? sysPage = null,
        bool assign = false,
        string? fragment = null,
        string? tab = null,
        CaseArrival? arrival = null,
        bool clearArrival = false,
        SystemActionFilter? systemFilter = null,
        WorkCentreAiActionFilter? aiFilter = null,
        bool clearSearch = false)
    {
        var query = new List<string> { "scope=" + ScopeSlug(scope ?? Scope) };
        query.AddRange((kinds ?? Kinds).Select(kind => "kind=" + NeedsAttentionPresentation.KindSlug(kind)));
        if (!clearSearch && Search is { } search)
        {
            query.Add("q=" + Uri.EscapeDataString(search));
        }

        var attentionPage = page ?? CurrentPage;
        if (attentionPage > 1)
        {
            query.Add(string.Create(CultureInfo.InvariantCulture, $"page={attentionPage}"));
        }

        if (selected is { } id)
        {
            query.Add("selected=" + id.ToString("D"));
        }

        var feedPage = newPage ?? NewCasesPage;
        if (feedPage > 1)
        {
            query.Add(string.Create(CultureInfo.InvariantCulture, $"newPage={feedPage}"));
        }

        var actionsPage = sysPage ?? SystemActionsPage;
        if (actionsPage > 1)
        {
            query.Add(string.Create(CultureInfo.InvariantCulture, $"sysPage={actionsPage}"));
        }

        if (!clearArrival && ArrivalSlug(arrival ?? Arrival) is { } arrivalSlug)
        {
            query.Add("arrival=" + arrivalSlug);
        }

        if (SystemFilterSlug(systemFilter ?? SystemFilter) is { } systemSlug)
        {
            query.Add("sys=" + systemSlug);
        }

        if (AiFilterSlug(aiFilter ?? AiFilter) is { } aiSlug)
        {
            query.Add("ai=" + aiSlug);
        }

        var section = tab ?? Tab;
        if (section != NewCasesTab)
        {
            query.Add("tab=" + section);
        }

        if (assign)
        {
            query.Add("assign=true");
        }

        return "/?" + string.Join("&", query) + (fragment is null ? string.Empty : "#" + fragment);
    }

    public static string TabLabel(string tab) => tab switch
    {
        NewCasesTab => Labels.NewCases,
        SystemActionsTab => Labels.SystemActions,
        AiActionsTab => Labels.AiActions,
        _ => Labels.NeedsAttention
    };

    /// <summary>The tab's count: the section's whole total, or a dash when it could not be read.</summary>
    public string TabCount(string tab) => tab switch
    {
        NewCasesTab => NewCasesFailed ? "—" : NewCasesWindowCount.ToString(CultureInfo.InvariantCulture),
        SystemActionsTab => SystemActionsFailed ? "—" : SystemActionsWindowCount.ToString(CultureInfo.InvariantCulture),
        AiActionsTab => AiJobsUnavailable ? "—" : AiJobs.Count.ToString(CultureInfo.InvariantCulture),
        _ => AttentionFailed ? "—" : ScopeTotal.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>The kinds with <paramref name="kind"/> added or removed (multi-select).</summary>
    public IReadOnlyList<NeedsAttentionKind> ToggledKinds(NeedsAttentionKind kind) =>
        Kinds.Contains(kind)
            ? Kinds.Where(existing => existing != kind).ToArray()
            : NeedsAttentionPresentation.ChipOrder.Where(existing => existing == kind || Kinds.Contains(existing)).ToArray();

    public static string ScopeSlug(NeedsAttentionScope scope) =>
        scope == NeedsAttentionScope.Mine ? "mine" : "office";

    private static NeedsAttentionScope? ParseScope(string? scope) => scope?.Trim().ToLowerInvariant() switch
    {
        "mine" => NeedsAttentionScope.Mine,
        "office" => NeedsAttentionScope.Office,
        _ => null
    };

    /// <summary>Every staff role opens on Office; Mine remains an explicit scope (P2).</summary>
    private static NeedsAttentionScope DefaultScope() => NeedsAttentionScope.Office;

    private static string ParseTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        SystemActionsTab => SystemActionsTab,
        AiActionsTab => AiActionsTab,
        AttentionTab => AttentionTab,
        _ => NewCasesTab
    };

    public static string? ArrivalSlug(CaseArrival? arrival) => arrival switch
    {
        CaseArrival.PrincipalApi => "api",
        CaseArrival.Email => "email",
        CaseArrival.Manual => "manual",
        CaseArrival.Automation => "automation",
        _ => null
    };

    private static CaseArrival? ParseArrival(string? arrival) => arrival?.Trim().ToLowerInvariant() switch
    {
        "api" => CaseArrival.PrincipalApi,
        "email" => CaseArrival.Email,
        "manual" => CaseArrival.Manual,
        "automation" => CaseArrival.Automation,
        _ => null
    };

    public static string? SystemFilterSlug(SystemActionFilter filter) => filter switch
    {
        SystemActionFilter.AutoMatched => "auto-match",
        SystemActionFilter.ImagesPaired => "images",
        SystemActionFilter.RulesAndQueries => "rules",
        _ => null
    };

    private static SystemActionFilter ParseSystemFilter(string? filter) => filter?.Trim().ToLowerInvariant() switch
    {
        "auto-match" => SystemActionFilter.AutoMatched,
        "images" => SystemActionFilter.ImagesPaired,
        "rules" => SystemActionFilter.RulesAndQueries,
        _ => SystemActionFilter.All
    };

    public static string? AiFilterSlug(WorkCentreAiActionFilter filter) => filter switch
    {
        WorkCentreAiActionFilter.Estimate => "estimate",
        WorkCentreAiActionFilter.QueryDraft => "query-draft",
        WorkCentreAiActionFilter.Failed => "failed",
        _ => null
    };

    private static WorkCentreAiActionFilter ParseAiFilter(string? filter) => filter?.Trim().ToLowerInvariant() switch
    {
        "estimate" => WorkCentreAiActionFilter.Estimate,
        "query-draft" => WorkCentreAiActionFilter.QueryDraft,
        "failed" => WorkCentreAiActionFilter.Failed,
        _ => WorkCentreAiActionFilter.All
    };

    private static bool MatchesAiSearch(WorkCentreAiJobRow row, string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return true;
        }

        return row.Job.SubjectReference.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.Job.Instruction.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (row.Job.ClosureReason?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private bool MatchesAiSearch(WorkCentreAiJobRow row) => MatchesAiSearch(row, Search);

    /// <summary>Loads the full-page and fragment models through one set of reads.</summary>
    private async Task<IActionResult?> LoadAsync(
        string? scope,
        string[]? kind,
        string? q,
        string? tab,
        string? arrival,
        string? sys,
        string? ai,
        Guid? selected,
        bool assign,
        bool refresh,
        string? since,
        int page,
        int newPage,
        int sysPage,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        NowUtc = timeProvider.GetUtcNow();
        ViewerStaffId = StaffNotificationPolicy.StaffId(actor);
        var namedScope = ParseScope(scope);
        ScopeNamed = namedScope is not null;
        Scope = namedScope ?? DefaultScope();
        Kinds = NeedsAttentionPresentation.ParseKinds(kind);
        Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        Tab = ParseTab(tab);
        Arrival = ParseArrival(arrival);
        SystemFilter = ParseSystemFilter(sys);
        AiFilter = ParseAiFilter(ai);
        CurrentPage = Math.Max(1, page);
        NewCasesPage = Math.Max(1, newPage);
        SystemActionsPage = Math.Max(1, sysPage);

        // The three sections read side by side, each on its own database
        // contexts, and each keeps its own failure: one unreadable section
        // never hides the others. The page's state is set once all three end.
        // Each read starts inside its own async method, so a reader that
        // throws before its first await still fails only its own section.
        var clientId = HttpContext.RequestServices.GetService<AutomationMcpOptions>()?.ClientId;
        // The open AI jobs and the workflow configuration feed both the
        // attention list and the AI jobs section, so they are read once.
        var sharedRead = ReadSharedAiInputsAsync(cancellationToken);
        // The attention list and the AI jobs section look up dismissals once
        // between them; a section that ends without looking up withdraws.
        var dismissals = new WorkCentreDismissalBatch(dismissalStore, sections: 2, cancellationToken);
        var attentionRead = ReadAttentionAsync(actor, Scope, CurrentPage, Kinds, Search, selected, assign, sharedRead, dismissals, cancellationToken);
        var newCasesRead = ReadNewCasesAsync(actor, markSeen: NewCasesPage == 1 && !refresh, cancellationToken);
        var systemActionsRead = ReadSystemActionsAsync(actor, cancellationToken);
        var aiJobsRead = ReadAiJobsAsync(actor, clientId, sharedRead, dismissals, cancellationToken);
        var activityRead = ReadActivityAsync(actor, cancellationToken);
        try
        {
            await Task.WhenAll(attentionRead, newCasesRead, systemActionsRead, aiJobsRead, activityRead);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Every read has ended; each section's own outcome is taken below.
        }

        try
        {
            ApplyAttention(await attentionRead);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A failed live read is not an empty queue: no zero is rendered as a fact.
            LogSectionFailed(logger, "needs_attention", exception);
            IsUnavailable = true;
        }

        try
        {
            NewCases = await newCasesRead;
            DividerUtc = refresh
                && DateTimeOffset.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var carried)
                    ? carried
                    : NewCases.LastSeenUtc;
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "new_cases", exception);
            NewCasesUnavailable = true;
        }

        try
        {
            SystemActions = await systemActionsRead;
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "system_actions", exception);
            SystemActionsUnavailable = true;
        }

        try
        {
            AiJobs = await aiJobsRead;
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "ai_jobs", exception);
            AiJobsUnavailable = true;
        }

        try
        {
            Activity = await activityRead;
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "activity", exception);
            ActivityUnavailable = true;
        }

        // A named tab whose section is omitted falls back to the first shown.
        var visible = VisibleTabs;
        if (!visible.Contains(Tab))
        {
            Tab = visible.Count > 0 ? visible[0] : NewCasesTab;
        }

        return null;
    }

    /// <summary>What the Needs attention section read, set on the page together.</summary>
    private sealed record AttentionRead(
        int Page,
        OperationsSnapshot Snapshot,
        NeedsAttentionItem? Selected,
        WorkCentreAssignment? Assignment,
        bool OpenAssignment);

    /// <summary>What the attention list and the AI jobs section both need: the open jobs and the workflow configuration.</summary>
    private sealed record SharedAiInputs(
        IReadOnlyList<AiJobRecord> OpenJobs,
        CaseWorkflowConfiguration Configuration);

    private async Task<SharedAiInputs> ReadSharedAiInputsAsync(CancellationToken cancellationToken)
    {
        var openRead = aiJobQueries.ListOpenAsync(cancellationToken);
        var configurationRead = workflowConfiguration.GetCurrentAsync(cancellationToken);
        await Task.WhenAll(openRead, configurationRead);
        return new(await openRead, await configurationRead);
    }

    private async Task<AttentionRead> ReadAttentionAsync(
        ActionActor actor,
        NeedsAttentionScope scope,
        int page,
        IReadOnlyList<NeedsAttentionKind> kinds,
        string? search,
        Guid? selected,
        bool assign,
        Task<SharedAiInputs> sharedRead,
        WorkCentreDismissalBatch dismissalBatch,
        CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.attention");
        var filter = kinds.Count > 0 ? kinds : null;
        OperationsSnapshot snapshot;
        using (var dismissals = dismissalBatch.Join())
        {
            SharedAiInputs? shared = null;
            try
            {
                shared = await sharedRead;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The AI jobs section reports this failure. The list still reads
                // for itself, so a section that cannot read never hides the other.
            }

            snapshot = await getOperationsSnapshot.ExecuteAsync(
                new NeedsAttentionQuery(actor, scope, page, filter, NowUtc, search, shared?.OpenJobs, shared?.Configuration, dismissals),
                cancellationToken);
        }

        // Core lands a page past the end on the last page; the page it read is the one shown.
        page = snapshot.Attention.Page;

        // Only the row the address names expands (v30 B); nothing opens by itself.
        var items = snapshot.Attention.Items;
        var selectedItem = selected is { } id ? items.FirstOrDefault(item => item.Id == id) : null;
        if (selectedItem is null)
        {
            return new(page, snapshot, null, null, false);
        }

        WorkCentreAssignment? assignment = null;
        if (selectedItem.Kind == NeedsAttentionKind.UnassignedEngineer)
        {
            assignment = await ReadAssignmentAsync(actor, selectedItem.Id, cancellationToken);
        }

        return new(page, snapshot, selectedItem, assignment, assign && assignment is not null);
    }

    private void ApplyAttention(AttentionRead read)
    {
        CurrentPage = read.Page;
        LoadedAtUtc = read.Snapshot.AsOfUtc;
        Attention = read.Snapshot.Attention;
        Metrics = read.Snapshot.Metrics;
        // Core counts the chips over the scope before the kind filter: one read.
        KindCounts = read.Snapshot.Attention.KindCounts;
        Selected = read.Selected;
        Assignment = read.Assignment;
        OpenAssignment = read.OpenAssignment;
    }

    /// <summary>The assignment dialog's facts and Engineers, as the Case record's dialog reads them.</summary>
    private async Task<WorkCentreAssignment?> ReadAssignmentAsync(
        ActionActor actor,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var header = await getCaseHeader.ExecuteAsync(new GetCaseHeaderQuery(caseId, actor), cancellationToken);
            if (header is null)
            {
                return null;
            }

            var accounts = await staffAccounts.ListAsync(0, 100, cancellationToken);
            // The signed-in person first, as the Triage Assign list (#1213).
            var engineers = accounts.Accounts
                .Where(account => account.IsEnabled)
                .OrderBy(account => account.Id == ViewerStaffId ? 0 : 1)
                .Select(account => new WorkCentreEngineer(account.Id, account.UserName))
                .ToArray();
            var current = header.Workflow.AssignedEngineerId is { } engineerId
                ? accounts.Accounts.FirstOrDefault(account => account.Id == engineerId)?.UserName
                : null;
            return new WorkCentreAssignment(
                caseId,
                header.Summary.Reference,
                header.Summary.Registration,
                header.Summary.Claimant,
                header.Summary.Principal,
                current,
                engineers);
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "assignment", exception);
            return null;
        }
    }

    /// <summary>The New cases section: the requested page of the recent-Case feed.</summary>
    private async Task<RecentCasesFeed> ReadNewCasesAsync(
        ActionActor actor,
        bool markSeen,
        CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.newcases");
        return await listRecentCases.ExecuteAsync(
            actor, NewCasesPage, markSeen, Arrival, Tab == NewCasesTab ? Search : null, cancellationToken, NowUtc);
    }

    /// <summary>The System Actions section: the window, narrowed by the sub-filter and by Find.</summary>
    private async Task<SystemActionPage> ReadSystemActionsAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.systemactions");
        return await listSystemActions.ExecuteAsync(
            actor,
            SystemFilter,
            Tab == SystemActionsTab ? Search : null,
            SystemActionsPage,
            cancellationToken,
            NowUtc);
    }

    /// <summary>The Activity panel: the day and week figures as of this load.</summary>
    private async Task<WorkCentreActivityCounts> ReadActivityAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.activity");
        return await getActivity.ExecuteAsync(actor, NowUtc, cancellationToken);
    }

    /// <summary>The AI jobs section (D9): Core's rows, each with who started it.</summary>
    private async Task<IReadOnlyList<WorkCentreAiJobRow>> ReadAiJobsAsync(
        ActionActor actor,
        string? clientId,
        Task<SharedAiInputs> sharedRead,
        WorkCentreDismissalBatch dismissalBatch,
        CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.aijobs");
        IReadOnlyList<WorkCentreAiJob> jobs;
        using (var dismissals = dismissalBatch.Join())
        {
            var shared = await sharedRead;
            jobs = await listAiJobs.ExecuteAsync(
                actor, shared.OpenJobs, shared.Configuration.AiDraftTargetDays, NowUtc, cancellationToken, dismissals);
        }

        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            AiJobActions.StaffCreatorIds(jobs.Select(row => row.Job))
                .Concat(jobs.Select(row => row.AdoptedByStaffId).OfType<Guid>())
                .Distinct()
                .ToArray(),
            cancellationToken);
        return jobs
            .Select(row => new WorkCentreAiJobRow(
                row.Job,
                row.Draft,
                AiJobActions.StartedBy(row.Job, names, clientId),
                row.AdoptedByStaffId is { } adopted && names.TryGetValue(adopted, out var name) ? name : null))
            .ToArray();
    }

    private async Task<IActionResult> WithCaseLeaseAsync(
        Guid caseId,
        string? returnUrl,
        string commandName,
        Func<ActionActor, CaseEditLease, Task> execute,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        CaseEditLease? lease = null;
        try
        {
            var basis = await getCaseEditBasis.ExecuteAsync(new GetCaseQuery(caseId, actor), cancellationToken)
                ?? throw new KeyNotFoundException("The case was not found.");
            lease = await acquireLease.ExecuteAsync(
                new ClaimCaseEditLeaseRequest(caseId, basis.Workflow.Version, actor, NewOperationKey()),
                cancellationToken);
            await execute(actor, lease);
            // The committed mutation consumed the lease.
            lease = null;
            StatusMessage = successMessage;
        }
        catch (StaffAuthorizationException)
        {
            await Pegasus.Web.Presentation.CaseEditLeaseRelease.ReleaseQuietlyAsync(
                releaseLease, logger, caseId, actor, lease);
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, commandName, caseId, exception);
            await Pegasus.Web.Presentation.CaseEditLeaseRelease.ReleaseQuietlyAsync(
                releaseLease, logger, caseId, actor, lease);
            ErrorMessage = Labels.AssignRefused;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Work Centre section {Section} could not be read.")]
    private static partial void LogSectionFailed(ILogger logger, string section, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Work Centre refresh sections: attention={Attention}, newCases={NewCases}, systemActions={SystemActions}, aiJobs={AiJobs}, activity={Activity}.")]
    private static partial void LogRefreshOutcome(
        ILogger logger, bool attention, bool newCases, bool systemActions, bool aiJobs, bool activity);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Work Centre command {CommandName} failed for {RecordId}.")]
    private static partial void LogCommandFailed(ILogger logger, string commandName, Guid recordId, Exception exception);
}

public sealed record WorkCentreEngineer(Guid Id, string Name);

/// <summary>What the Assign Engineer dialog on the Work Centre reads.</summary>
public sealed record WorkCentreAssignment(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    string? Principal,
    string? EngineerName,
    IReadOnlyList<WorkCentreEngineer> Engineers);

/// <summary>One AI jobs row: the job, its draft (route and action) when Draft ready, and who started it.</summary>
public sealed record WorkCentreAiJobRow(AiJobRecord Job, AiDraft? Draft, string StartedBy, string? AdoptedBy = null)
{
    public bool CanComplete => Draft is not null && AiJobPolicy.CompletesByHand(Job);
}
