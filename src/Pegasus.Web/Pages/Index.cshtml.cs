using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.WorkCentre;

namespace Pegasus.Web.Pages;

/// <summary>
/// The Work Centre (v30 B "Office ledger" over Work Centre D1–D10): the five
/// queue totals, then one tabbed panel holding the paged Needs attention
/// ledger (grouped by due day, Office/Mine, kind chips and Find, the selected
/// row expanding to its facts and next action in place), New cases and AI jobs.
/// A tab is omitted when its section is empty and kept when it is unavailable.
/// Under the counts sits the Activity panel (v32 A, 5 October 2026): the
/// day and week figures, read beside the three sections with its own outcome.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public partial class IndexModel(
    IGetOperationsSnapshot getOperationsSnapshot,
    IListRecentCases listRecentCases,
    IAiJobQueries aiJobQueries,
    ICaseWorkflowConfiguration workflowConfiguration,
    IStaffAccountQueries staffAccounts,
    IGetCaseHeader getCaseHeader,
    IGetCaseEditBasis getCaseEditBasis,
    IAcquireCaseEditLease acquireLease,
    IReleaseCaseEditLease releaseLease,
    IAssignCaseEngineer assignEngineer,
    IConfirmAiJob confirmAiJob,
    IListWorkCentreAiJobs listAiJobs,
    IDismissWorkCentreItem dismissItem,
    IGetWorkCentreActivity getActivity,
    TimeProvider timeProvider,
    ILogger<IndexModel> logger) : StaffPageModel
{
    public const string AttentionTab = "attention";
    public const string NewCasesTab = "new-cases";
    public const string AiJobsTab = "ai-jobs";

    public DateTimeOffset NowUtc { get; private set; }

    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public NeedsAttentionScope Scope { get; private set; }

    /// <summary>True when the address named the scope; otherwise the page opened on the person's default.</summary>
    public bool ScopeNamed { get; private set; }

    public IReadOnlyList<NeedsAttentionKind> Kinds { get; private set; } = [];

    /// <summary>Find within Needs attention (v30 WB): trimmed, null when empty.</summary>
    public string? Search { get; private set; }

    /// <summary>The section the panel shows: attention, new-cases or ai-jobs (v30 WD).</summary>
    public string Tab { get; private set; } = AttentionTab;

    public bool AttentionFiltered => Kinds.Count > 0 || Search is not null;

    public int CurrentPage { get; private set; } = 1;

    public int NewCasesPage { get; private set; } = 1;

    public NeedsAttentionPage? Attention { get; private set; }

    /// <summary>The chip counts: the scope's whole list, before the kind filter.</summary>
    public IReadOnlyDictionary<NeedsAttentionKind, int> KindCounts { get; private set; } =
        new Dictionary<NeedsAttentionKind, int>();

    public WorkCentreMetrics? Metrics { get; private set; }

    public NeedsAttentionItem? Selected { get; private set; }

    public WorkCentreAssignment? Assignment { get; private set; }

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

    public bool ShowNewCases => NewCasesFailed || NewCases!.Page.TotalCount > 0;

    public bool ShowAiJobs => AiJobsUnavailable || AiJobs.Count > 0;

    /// <summary>The whole page has nothing to show and no read failed to say so.</summary>
    public bool NothingToShow => !ShowAttention && !ShowNewCases && !ShowAiJobs;

    public IReadOnlyList<string> VisibleTabs =>
        new[] { (AttentionTab, ShowAttention), (NewCasesTab, ShowNewCases), (AiJobsTab, ShowAiJobs) }
            .Where(tab => tab.Item2)
            .Select(tab => tab.Item1)
            .ToArray();

    public RecentCasesFeed? NewCases { get; private set; }

    /// <summary>When the current New cases content was successfully read.</summary>
    public DateTimeOffset? NewCasesLoadedAtUtc { get; private set; }

    /// <summary>Where the "since you last looked" line falls; a refresh keeps the line the page opened with.</summary>
    public DateTimeOffset? DividerUtc { get; private set; }

    public bool NewCasesUnavailable { get; private set; }

    public IReadOnlyList<WorkCentreAiJobRow> AiJobs { get; private set; } = [];

    /// <summary>When the current AI jobs content was successfully read.</summary>
    public DateTimeOffset? AiJobsLoadedAtUtc { get; private set; }

    public bool AiJobsUnavailable { get; private set; }

    /// <summary>The Activity figures (FRD-15): null until read, and never drawn as zero when the read failed.</summary>
    public WorkCentreActivityCounts? Activity { get; private set; }

    public bool ActivityUnavailable { get; private set; }

    public bool ActivityFailed => ActivityUnavailable || Activity is null;

    /// <summary>True when one or more independently rendered live sections could not be read.</summary>
    public bool HasReadFailure => IsUnavailable || NewCasesUnavailable || AiJobsUnavailable || ActivityUnavailable;

    /// <summary>The fragment outcome is failed only when no independently rendered section was read.</summary>
    public string RefreshOutcome => !HasReadFailure
        ? "current"
        : IsUnavailable && NewCasesUnavailable && AiJobsUnavailable && ActivityUnavailable ? "failed" : "partial";

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
        Guid? selected,
        bool assign,
        bool refresh,
        string? since,
        CancellationToken cancellationToken,
        // "page" is also the Razor Pages route value naming this page, which
        // outranks the query string; bind the list's page from the query.
        [FromQuery(Name = "page")] int page = 1,
        int newPage = 1)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.main");
        var refusal = await LoadAsync(scope, kind, q, tab, selected, assign, refresh, since, page, newPage, cancellationToken);
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
        Guid? selected,
        string? since,
        CancellationToken cancellationToken,
        [FromQuery(Name = "page")] int page = 1,
        int newPage = 1)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.refresh.main");
        // A refresh is never the one-shot instruction to reopen an assignment
        // dialog. The current display may retain a dismissed dialog's selection.
        var refusal = await LoadAsync(
            scope,
            kind,
            q,
            tab,
            selected,
            assign: false,
            refresh: true,
            since: since,
            page: page,
            newPage: newPage,
            cancellationToken: cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        LogRefreshOutcome(logger, !IsUnavailable, !NewCasesUnavailable, !AiJobsUnavailable, !ActivityUnavailable);
        return Partial("_WorkCentreBody", this);
    }

    /// <summary>
    /// Assign Engineer from the expanded row (P4). The Work Centre holds no edit
    /// session, so the handler claims the Case's lease, runs the same Core
    /// assignment the Case record's dialog runs, and returns here; a refused
    /// assignment releases the lease it claimed.
    /// </summary>
    public Task<IActionResult> OnPostAssignEngineerAsync(
        Guid caseId,
        Guid engineerId,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken) =>
        WithCaseLeaseAsync(
            caseId,
            returnUrl,
            "assign_engineer",
            async (actor, basis, lease) =>
            {
                if (engineerId == Guid.Empty)
                {
                    throw new ArgumentException("An Engineer is required.", nameof(engineerId));
                }

                var completeness = basis.Data.Completeness.Values;
                await assignEngineer.ExecuteAsync(
                    new AssignCaseEngineerRequest(
                        caseId,
                        lease.Version,
                        actor,
                        operationKey,
                        CaseWorkspaceLabels.AssignEngineer,
                        lease.Token,
                        engineerId,
                        new CaseReadinessEvidence(
                            completeness?.InstructionComplete ?? false,
                            completeness?.ImagesComplete ?? false,
                            "case-completeness-projection")),
                    cancellationToken);
            },
            Labels.Assigned,
            cancellationToken);

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
            var job = (await aiJobQueries.ListOpenAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.JobId == jobId);
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
        bool assign = false,
        string? fragment = null,
        string? tab = null,
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

        var section = tab ?? Tab;
        if (section != AttentionTab)
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
        AiJobsTab => Labels.AiJobsTitle,
        _ => Labels.NeedsAttention
    };

    /// <summary>The tab's count: the section's whole total, or a dash when it could not be read.</summary>
    public string TabCount(string tab) => tab switch
    {
        NewCasesTab => NewCasesFailed ? "—" : NewCases!.Page.TotalCount.ToString(CultureInfo.InvariantCulture),
        AiJobsTab => AiJobsUnavailable ? "—" : AiJobs.Count.ToString(CultureInfo.InvariantCulture),
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
        NewCasesTab => NewCasesTab,
        AiJobsTab => AiJobsTab,
        _ => AttentionTab
    };

    /// <summary>Loads the full-page and fragment models through one set of reads.</summary>
    private async Task<IActionResult?> LoadAsync(
        string? scope,
        string[]? kind,
        string? q,
        string? tab,
        Guid? selected,
        bool assign,
        bool refresh,
        string? since,
        int page,
        int newPage,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        NowUtc = timeProvider.GetUtcNow();
        var namedScope = ParseScope(scope);
        ScopeNamed = namedScope is not null;
        Scope = namedScope ?? DefaultScope();
        Kinds = NeedsAttentionPresentation.ParseKinds(kind);
        Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        Tab = ParseTab(tab);
        CurrentPage = Math.Max(1, page);
        NewCasesPage = Math.Max(1, newPage);

        // The three sections read side by side, each on its own database
        // contexts, and each keeps its own failure: one unreadable section
        // never hides the others. The page's state is set once all three end.
        // Each read starts inside its own async method, so a reader that
        // throws before its first await still fails only its own section.
        var clientId = HttpContext.RequestServices.GetService<AutomationMcpOptions>()?.ClientId;
        // The open AI jobs and the workflow configuration feed both the
        // attention list and the AI jobs section, so they are read once.
        var sharedRead = ReadSharedAiInputsAsync(cancellationToken);
        var attentionRead = ReadAttentionAsync(actor, Scope, CurrentPage, Kinds, Search, selected, assign, sharedRead, cancellationToken);
        var newCasesRead = ReadNewCasesAsync(actor, markSeen: NewCasesPage == 1 && !refresh, cancellationToken);
        var aiJobsRead = ReadAiJobsAsync(actor, clientId, sharedRead, cancellationToken);
        var activityRead = ReadActivityAsync(actor, cancellationToken);
        try
        {
            await Task.WhenAll(attentionRead, newCasesRead, aiJobsRead, activityRead);
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
            NewCasesLoadedAtUtc = NowUtc;
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
            AiJobs = await aiJobsRead;
            AiJobsLoadedAtUtc = NowUtc;
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
            Tab = visible.Count > 0 ? visible[0] : AttentionTab;
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
        CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.attention");
        var filter = kinds.Count > 0 ? kinds : null;
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

        var snapshot = await getOperationsSnapshot.ExecuteAsync(
            new NeedsAttentionQuery(actor, scope, page, filter, NowUtc, search, shared?.OpenJobs, shared?.Configuration),
            cancellationToken);
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
        // The rail's Case count sums the stage counts and the open Unidentified
        // queue the snapshot has just read. The Triages metric counts every
        // active Triage state, as the rail does, so the shell reads none again.
        RailCountsPageFilter.SetCaseCounts(
            HttpContext, read.Snapshot.CaseStages, read.Snapshot.Metrics.Triages, read.Snapshot.UnidentifiedCount);
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
            var engineers = accounts.Accounts
                .Where(account => account.IsEnabled)
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
        return await listRecentCases.ExecuteAsync(actor, NewCasesPage, markSeen, cancellationToken, NowUtc);
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
        CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.workcentre.aijobs");
        var shared = await sharedRead;
        var jobs = await listAiJobs.ExecuteAsync(
            actor, shared.OpenJobs, shared.Configuration.AiDraftTargetDays, NowUtc, cancellationToken);
        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            AiJobActions.StaffCreatorIds(jobs.Select(row => row.Job)),
            cancellationToken);
        return jobs
            .Select(row => new WorkCentreAiJobRow(
                row.Job,
                row.Draft,
                AiJobActions.StartedBy(row.Job, names, clientId)))
            .ToArray();
    }

    private async Task<IActionResult> WithCaseLeaseAsync(
        Guid caseId,
        string? returnUrl,
        string commandName,
        Func<ActionActor, CaseEditBasis, CaseEditLease, Task> execute,
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
            await execute(actor, basis, lease);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Work Centre refresh sections: attention={Attention}, newCases={NewCases}, aiJobs={AiJobs}, activity={Activity}.")]
    private static partial void LogRefreshOutcome(ILogger logger, bool attention, bool newCases, bool aiJobs, bool activity);

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
public sealed record WorkCentreAiJobRow(AiJobRecord Job, AiDraft? Draft, string StartedBy)
{
    public bool CanComplete => Draft is not null && AiJobPolicy.CompletesByHand(Job);
}
