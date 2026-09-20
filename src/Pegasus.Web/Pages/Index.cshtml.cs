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
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.WorkCentre;

namespace Pegasus.Web.Pages;

/// <summary>
/// The Work Centre (v26, Work Centre D1–D10): four metrics, the paged Needs
/// attention list grouped by due day with Office/Mine and kind filters, the
/// Today pane whose action acts in place, New cases and AI jobs.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public partial class IndexModel(
    IGetOperationsSnapshot getOperationsSnapshot,
    IListRecentCases listRecentCases,
    IAiDraftQueries aiDrafts,
    IAiJobQueries aiJobQueries,
    IStaffAccountQueries staffAccounts,
    IGetCase getCase,
    IGetTriage getTriage,
    IAcquireCaseEditLease acquireLease,
    IReleaseCaseEditLease releaseLease,
    IAssignCaseEngineer assignEngineer,
    IAssignCaseToMe assignCaseToMe,
    IAssignTriageToMe assignTriageToMe,
    IConfirmAiJob confirmAiJob,
    TimeProvider timeProvider,
    ILogger<IndexModel> logger) : StaffPageModel
{
    /// <summary>How far back the AI jobs section reads for Failed jobs; the window is the New cases window.</summary>
    private const int RecentJobWindow = 200;

    public DateTimeOffset NowUtc { get; private set; }

    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public NeedsAttentionScope Scope { get; private set; }

    /// <summary>True when the address named the scope; otherwise the page opened on the person's default.</summary>
    public bool ScopeNamed { get; private set; }

    public IReadOnlyList<NeedsAttentionKind> Kinds { get; private set; } = [];

    public int CurrentPage { get; private set; } = 1;

    public int NewCasesPage { get; private set; } = 1;

    public NeedsAttentionPage? Attention { get; private set; }

    /// <summary>The chip counts: the scope's whole list, before the kind filter.</summary>
    public IReadOnlyDictionary<NeedsAttentionKind, int> KindCounts { get; private set; } =
        new Dictionary<NeedsAttentionKind, int>();

    public WorkCentreMetrics? Metrics { get; private set; }

    public NeedsAttentionItem? Selected { get; private set; }

    public bool CanTakeSelected { get; private set; }

    public WorkCentreAssignment? Assignment { get; private set; }

    public bool OpenAssignment { get; private set; }

    /// <summary>True only when the live Work Centre query could not be read.</summary>
    public bool IsUnavailable { get; private set; }

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

    /// <summary>True when one or more independently rendered live sections could not be read.</summary>
    public bool HasReadFailure => IsUnavailable || NewCasesUnavailable || AiJobsUnavailable;

    /// <summary>The fragment outcome is failed only when no independently rendered section was read.</summary>
    public string RefreshOutcome => !HasReadFailure
        ? "current"
        : IsUnavailable && NewCasesUnavailable && AiJobsUnavailable ? "failed" : "partial";

    public string RefreshOutcomeLabel => RefreshOutcome switch
    {
        "failed" => "Refresh unavailable",
        "partial" => "Partially refreshed",
        _ => "Current"
    };

    [TempData(Key = "WorkCentreStatus")]
    public string? StatusMessage { get; set; }

    [TempData(Key = "WorkCentreError")]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string? scope,
        [FromQuery(Name = "kind")] string[]? kind,
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
        var refusal = await LoadAsync(scope, kind, selected, assign, refresh, since, page, newPage, cancellationToken);
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

        LogRefreshOutcome(logger, !IsUnavailable, !NewCasesUnavailable, !AiJobsUnavailable);
        return Partial("_WorkCentreBody", this);
    }

    /// <summary>
    /// Assign Engineer from the Today pane (P4). The Work Centre holds no edit
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
            async (actor, details, lease) =>
            {
                if (engineerId == Guid.Empty)
                {
                    throw new ArgumentException("An Engineer is required.", nameof(engineerId));
                }

                var completeness = details.Data?.Completeness.Values;
                await assignEngineer.ExecuteAsync(
                    new AssignCaseEngineerRequest(
                        caseId,
                        lease.Version,
                        actor,
                        operationKey,
                        CaseWorkspaceLabels.HandToEngineer,
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

    /// <summary>Assign to me on an Unassigned Engineer row (P8).</summary>
    public Task<IActionResult> OnPostAssignToMeAsync(
        Guid caseId,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken) =>
        WithCaseLeaseAsync(
            caseId,
            returnUrl,
            "assign_to_me",
            (actor, _, lease) => assignCaseToMe.ExecuteAsync(
                new AssignCaseToMeRequest(caseId, lease.Version, actor, operationKey, lease.Token),
                cancellationToken),
            Labels.AssignedToYou,
            cancellationToken);

    /// <summary>Assign to me on a Triage without an assignee (P8).</summary>
    public async Task<IActionResult> OnPostAssignTriageToMeAsync(
        Guid triageId,
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
            var detail = await getTriage.ExecuteAsync(new GetTriageQuery(triageId, actor), cancellationToken)
                ?? throw new KeyNotFoundException("The Triage was not found.");
            await assignTriageToMe.ExecuteAsync(
                new AssignTriageToMeRequest(triageId, detail.Record.Version, actor, operationKey),
                cancellationToken);
            StatusMessage = Labels.TriageAssignedToYou;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, "assign_triage_to_me", triageId, exception);
            ErrorMessage = Labels.TriageAssignRefused;
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
            var job = (await aiJobQueries.ListOpenAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.JobId == jobId);
            if (job is null || !WorkCentreAiJobRow.CompletesByHand(job))
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

    /// <summary>This page's address with the given state changed and the rest kept.</summary>
    public string PageUrl(
        NeedsAttentionScope? scope = null,
        IEnumerable<NeedsAttentionKind>? kinds = null,
        int? page = null,
        Guid? selected = null,
        int? newPage = null,
        bool assign = false,
        string? fragment = null)
    {
        var query = new List<string> { "scope=" + ScopeSlug(scope ?? Scope) };
        query.AddRange((kinds ?? Kinds).Select(kind => "kind=" + NeedsAttentionPresentation.KindSlug(kind)));
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

        if (assign)
        {
            query.Add("assign=true");
        }

        return "/?" + string.Join("&", query) + (fragment is null ? string.Empty : "#" + fragment);
    }

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

    /// <summary>Loads the full-page and fragment models through one set of reads.</summary>
    private async Task<IActionResult?> LoadAsync(
        string? scope,
        string[]? kind,
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
        CurrentPage = Math.Max(1, page);
        NewCasesPage = Math.Max(1, newPage);

        try
        {
            await ReadAttentionAsync(actor, selected, assign, cancellationToken);
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
            NewCases = await listRecentCases.ExecuteAsync(
                actor,
                NewCasesPage,
                markSeen: NewCasesPage == 1 && !refresh,
                cancellationToken, NowUtc);
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
            AiJobs = await ReadAiJobsAsync(cancellationToken);
            AiJobsLoadedAtUtc = NowUtc;
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "ai_jobs", exception);
            AiJobsUnavailable = true;
        }

        return null;
    }

    private async Task ReadAttentionAsync(
        ActionActor actor,
        Guid? selected,
        bool assign,
        CancellationToken cancellationToken)
    {
        var filter = Kinds.Count > 0 ? Kinds : null;
        var snapshot = await getOperationsSnapshot.ExecuteAsync(
            new NeedsAttentionQuery(actor, Scope, CurrentPage, filter, NowUtc),
            cancellationToken);
        if (snapshot.Attention.Items.Count == 0 && CurrentPage > snapshot.Attention.TotalPages)
        {
            // A page past the end (the list shrank) lands on the last page, not an empty one.
            CurrentPage = snapshot.Attention.TotalPages;
            snapshot = await getOperationsSnapshot.ExecuteAsync(
                new NeedsAttentionQuery(actor, Scope, CurrentPage, filter, NowUtc),
                cancellationToken);
        }

        LoadedAtUtc = snapshot.AsOfUtc;
        Attention = snapshot.Attention;
        Metrics = snapshot.Metrics;
        // Core counts the chips over the scope before the kind filter: one read.
        KindCounts = snapshot.Attention.KindCounts;

        var items = snapshot.Attention.Items;
        Selected = (selected is { } id ? items.FirstOrDefault(item => item.Id == id) : null)
            ?? (items.Count > 0 ? items[0] : null);
        if (Selected is null)
        {
            return;
        }

        CanTakeSelected = Selected.OwnerStaffId is null
            && NeedsAttentionPolicy.CanTake(Selected.Kind, actor);
        if (Selected.Kind == NeedsAttentionKind.UnassignedEngineer)
        {
            Assignment = await ReadAssignmentAsync(actor, Selected.Id, cancellationToken);
            OpenAssignment = assign && Assignment is not null;
        }
    }

    /// <summary>The assignment dialog's facts and Engineers, as the Case record's dialog reads them.</summary>
    private async Task<WorkCentreAssignment?> ReadAssignmentAsync(
        ActionActor actor,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = await getCase.ExecuteAsync(new GetCaseQuery(caseId, actor), cancellationToken);
            if (details is null)
            {
                return null;
            }

            var accounts = await staffAccounts.ListAsync(0, 100, cancellationToken);
            var engineers = accounts.Accounts
                .Where(account => account.IsEnabled)
                .Select(account => new WorkCentreEngineer(account.Id, account.UserName))
                .ToArray();
            var current = details.Workflow.AssignedEngineerId is { } engineerId
                ? accounts.Accounts.FirstOrDefault(account => account.Id == engineerId)?.UserName
                : null;
            return new WorkCentreAssignment(
                caseId,
                details.Summary.Reference,
                details.Summary.Registration,
                details.Summary.Claimant,
                details.Summary.Principal,
                current,
                engineers,
                CaseLifecycleRules.CanAssignToSelf(details.Workflow));
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            LogSectionFailed(logger, "assignment", exception);
            return null;
        }
    }

    /// <summary>
    /// The office's unfinished AI work (D9): Queued, Taken and Draft ready from
    /// the open ledger, and the jobs that failed within the New cases window.
    /// Market research never waits for a person and is not listed.
    /// </summary>
    private async Task<IReadOnlyList<WorkCentreAiJobRow>> ReadAiJobsAsync(CancellationToken cancellationToken)
    {
        var open = await aiJobQueries.ListOpenAsync(cancellationToken);
        var drafts = (await aiDrafts.ListOpenAsync(cancellationToken))
            .ToDictionary(draft => draft.Job.JobId);
        var windowStart = RecentCasesPolicy.WindowStart(NowUtc);
        var failed = (await aiJobQueries.ListRecentAsync(RecentJobWindow, cancellationToken))
            .Where(job => job.State == AiJobState.Failed && (job.ClosedAtUtc ?? job.CreatedAtUtc) >= windowStart);
        var jobs = open
            .Where(job => job.State is AiJobState.Queued or AiJobState.Taken or AiJobState.DraftReady)
            .Concat(failed)
            .Where(job => job.Kind != AiJobKind.MarketResearch)
            .DistinctBy(job => job.JobId)
            .OrderBy(job => job.State switch
            {
                AiJobState.DraftReady => 0,
                AiJobState.Taken => 1,
                AiJobState.Queued => 2,
                _ => 3
            })
            .ThenByDescending(job => job.CreatedAtUtc)
            .ToArray();
        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            AiJobActions.StaffCreatorIds(jobs),
            cancellationToken);
        var clientId = HttpContext.RequestServices.GetService<AutomationMcpOptions>()?.ClientId;
        return jobs
            .Select(job => new WorkCentreAiJobRow(
                job,
                drafts.GetValueOrDefault(job.JobId),
                AiJobActions.StartedBy(job, names, clientId)))
            .ToArray();
    }

    private async Task<IActionResult> WithCaseLeaseAsync(
        Guid caseId,
        string? returnUrl,
        string commandName,
        Func<ActionActor, CaseDetails, CaseEditLease, Task> execute,
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
            var details = await getCase.ExecuteAsync(new GetCaseQuery(caseId, actor), cancellationToken)
                ?? throw new KeyNotFoundException("The case was not found.");
            lease = await acquireLease.ExecuteAsync(
                new ClaimCaseEditLeaseRequest(caseId, details.Workflow.Version, actor, NewOperationKey()),
                cancellationToken);
            await execute(actor, details, lease);
            // The committed mutation consumed the lease.
            lease = null;
            StatusMessage = successMessage;
        }
        catch (StaffAuthorizationException)
        {
            await ReleaseQuietlyAsync(caseId, actor, lease);
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, commandName, caseId, exception);
            await ReleaseQuietlyAsync(caseId, actor, lease);
            ErrorMessage = Labels.AssignRefused;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private async Task ReleaseQuietlyAsync(Guid caseId, ActionActor actor, CaseEditLease? lease)
    {
        if (lease is null)
        {
            return;
        }

        try
        {
            await releaseLease.ExecuteAsync(
                new ReleaseCaseEditLeaseRequest(caseId, actor, NewOperationKey(), lease.Token),
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, "release_lease", caseId, exception);
        }
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Work Centre section {Section} could not be read.")]
    private static partial void LogSectionFailed(ILogger logger, string section, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Work Centre refresh sections: attention={Attention}, newCases={NewCases}, aiJobs={AiJobs}.")]
    private static partial void LogRefreshOutcome(ILogger logger, bool attention, bool newCases, bool aiJobs);

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
    IReadOnlyList<WorkCentreEngineer> Engineers,
    bool CanAssignToMe);

/// <summary>One AI jobs row: the job, its draft (route and action) when Draft ready, and who started it.</summary>
public sealed record WorkCentreAiJobRow(AiJobRecord Job, AiDraft? Draft, string StartedBy)
{
    public bool CanComplete => Draft is not null && CompletesByHand(Job);

    /// <summary>
    /// FRD-27: a Draft ready Query response or queue pass is closed by hand; an
    /// Estimate and an Unidentified resolution close through their record's own act.
    /// </summary>
    public static bool CompletesByHand(AiJobRecord job) =>
        job.State == AiJobState.DraftReady
        && job.Kind is AiJobKind.QueryResponse or AiJobKind.UnidentifiedQueuePass;
}
