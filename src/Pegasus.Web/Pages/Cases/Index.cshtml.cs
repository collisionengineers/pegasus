using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// Cases (v26): the workflow rail with queried counts, the open
/// scope as a table (decision L) and a fixed-width quick detail of the selected
/// row.
/// </summary>
/// <remarks>
/// The rail groups are Workflow (Not ready, Review, With Engineer, Complete, Query),
/// Pre-Case work (Triage, Awaiting instruction) and Exceptions (Held,
/// Unidentified). The Unidentified scope lists open items, with closed items
/// behind its Show filter (received file D5); nothing here lists a Blocked
/// receipt or links to a received item (received file D1, D2).
///
/// The group is <c>?tab=</c>; the earlier <c>?queue=</c> is accepted as
/// an alias and hyphenated spellings normalise to the same keys. A request
/// carrying a search-only parameter belongs to <c>/Search</c> and is
/// redirected there permanently with its values intact.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    IListTriage listTriage,
    ISearchCases searchCases,
    IGetCase getCase,
    IDashboardQueries dashboardQueries,
    IUnidentifiedStore unidentifiedStore,
    IImageIntakeQueries imageIntakeQueries,
    IUploadCaseDecision caseDecision,
    IGetIntake getIntake,
    IStaffAccountQueries staffAccounts,
    ICaseWorkflowConfiguration workflowConfiguration,
    TimeProvider timeProvider) : UploadConfirmationPageModel(caseDecision)
{
    private const int PageSize = 25;

    /// <summary>
    /// Not ready and the Unidentified lists are read whole rather than paged —
    /// the bounded exception-queue trade-off, not a second convention.
    /// </summary>
    private const int MergedPageSize = 100;

    private readonly IListTriage _listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly ISearchCases _searchCases =
        searchCases ?? throw new ArgumentNullException(nameof(searchCases));
    private readonly IGetCase _getCase =
        getCase ?? throw new ArgumentNullException(nameof(getCase));
    private readonly IDashboardQueries _dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly IUnidentifiedStore _unidentifiedStore =
        unidentifiedStore ?? throw new ArgumentNullException(nameof(unidentifiedStore));
    private readonly IImageIntakeQueries _imageIntakeQueries =
        imageIntakeQueries ?? throw new ArgumentNullException(nameof(imageIntakeQueries));
    private readonly IStaffAccountQueries _staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));
    private readonly ICaseWorkflowConfiguration _workflowConfiguration =
        workflowConfiguration ?? throw new ArgumentNullException(nameof(workflowConfiguration));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>One rail entry: its key, label, group and icon.</summary>
    public sealed record Tab(string Key, string Label, string Group, string Icon, bool IsException = false);

    public const string WorkflowGroup = "Workflow";
    public const string PreCaseGroup = "Pre-Case work";
    public const string ExceptionsGroup = "Exceptions";

    /// <summary>
    /// The rail, in rail order; the group labels are the section labels.
    /// Every label comes from <see cref="OperatorLabels.CaseStage"/> (D3) or
    /// is the record kind's own settled name.
    /// </summary>
    public static readonly IReadOnlyList<Tab> Tabs =
    [
        new("not_ready", OperatorLabels.CaseStage(CaseLifecycleState.NotReady), WorkflowGroup, "icon-clock"),
        new("review", OperatorLabels.CaseStage(CaseLifecycleState.Review), WorkflowGroup, "icon-check-circle"),
        new("with_engineer", OperatorLabels.CaseStage(CaseLifecycleState.ReportPreparation), WorkflowGroup, "icon-user"),
        new("complete", OperatorLabels.CaseStage(CaseLifecycleState.PostReportComplete), WorkflowGroup, "icon-check"),
        new("query", OperatorLabels.CaseStage(CaseLifecycleState.Query), WorkflowGroup, "icon-reply"),
        new("triage", "Triage", WorkflowGroup, "icon-file-text"),
        new("awaiting", "Awaiting instruction", PreCaseGroup, "icon-image"),
        new("held", OperatorLabels.CaseStage(CaseLifecycleState.Held), ExceptionsGroup, "icon-pause", IsException: true),
        new("unidentified", "Unidentified", ExceptionsGroup, "icon-alert-triangle", IsException: true)
    ];

    /// <summary>
    /// The query parameters that belong to <c>/Search</c> and never to this
    /// page. Their presence means an old <c>/Cases</c> search link.
    /// </summary>
    private static readonly string[] SearchOnlyParameters =
    [
        "case", "registration", "claimant", "claimNumber", "engineerId",
        "receivedDate", "instructionDate", "fromDate", "toDate", "query"
    ];

    /// <summary>
    /// When these counts and rows were last read. Set only after the queries
    /// return, so a failed load never claims to be fresh.
    /// </summary>
    public DateTimeOffset? LoadedAtUtc { get; private set; }

    /// <summary>True when the live queues could not be read: the page says so rather than rendering zeros.</summary>
    public bool IsUnavailable { get; private set; }

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabFilter { get; set; }

    /// <summary>The earlier name of <see cref="TabFilter"/>, accepted as an alias.</summary>
    [BindProperty(SupportsGet = true, Name = "queue")]
    public string? QueueFilter { get; set; }

    /// <summary>The open rail scope; hyphenated spellings normalise to it.</summary>
    public string Queue => (string.IsNullOrWhiteSpace(TabFilter)
            ? string.IsNullOrWhiteSpace(QueueFilter) ? "not_ready" : QueueFilter
            : TabFilter)
        .Trim().ToLowerInvariant().Replace('-', '_');

    public Tab CurrentTab => Tabs.First(tab => tab.Key == Queue);

    /// <summary>The Principal filter; it only filters Case rows, so only Case queues offer it.</summary>
    [BindProperty(SupportsGet = true, Name = "principal")]
    public string? PrincipalFilter { get; set; }

    /// <summary>
    /// The Not ready group's Missing filter: <c>instructions</c>, <c>images</c>
    /// or <c>both</c>, read from each case's recorded completeness facts. The
    /// options are exclusive — "Instructions" means the instruction is the
    /// only thing missing — because "Both missing" exists for the remainder.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "missing")]
    public string? MissingFilter { get; set; }

    /// <summary>The Unidentified scope's Show filter: open items (default) or <c>closed</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "show")]
    public string? ShowFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>The row the quick-detail pane is showing; the first row when unset.</summary>
    [BindProperty(SupportsGet = true, Name = "selected")]
    public Guid? SelectedId { get; set; }

    public bool ShowingNotReady => Queue == "not_ready";

    public bool ShowingClosed => Queue == "unidentified" && ShowFilter == "closed";

    /// <summary>Whether the scope lists Case rows, so the Principal filter applies.</summary>
    public static bool ListsCases(string queue) =>
        queue is "not_ready" or "review" or "with_engineer" or "complete" or "query" or "held";

    public CaseStageCounts StageCounts { get; private set; } = new(0, 0, 0, 0);

    public int TriageCount { get; private set; }

    /// <summary>Open Unidentified items only; a closed item is never counted.</summary>
    public int UnidentifiedCount { get; private set; }

    public int Count(Tab tab) => tab.Key switch
    {
        "not_ready" => StageCounts.NotReady,
        "review" => StageCounts.Review,
        "with_engineer" => StageCounts.WithEngineer,
        "complete" => StageCounts.Complete,
        "query" => StageCounts.Query,
        "triage" => TriageCount,
        "awaiting" => StageCounts.AwaitingInstruction,
        "held" => StageCounts.Held,
        "unidentified" => UnidentifiedCount,
        _ => 0
    };

    public enum RowKind
    {
        Case,
        Image,
        Triage,
        Unidentified
    }

    public enum CellKind
    {
        Text,
        Mono,
        Link,
        Chip,
        Late,
        Date
    }

    /// <summary>One table cell: its text, how it renders and, for a chip, its tone.</summary>
    public sealed record Cell(string Text, CellKind Kind = CellKind.Text, string? Tone = null)
    {
        public static Cell Empty => new("—");

        public static Cell Of(string? text) => string.IsNullOrWhiteSpace(text) ? Empty : new(text);
    }

    /// <summary>
    /// One row of the scope table: its cells (in <see cref="Columns"/> order,
    /// the first opening the record), the quick-detail heading and facts, when
    /// it was received (the order, newest first) and the record's address.
    /// </summary>
    public sealed record QueueRow(
        RowKind Kind,
        Guid Id,
        string Title,
        IReadOnlyList<Cell> Cells,
        DateTimeOffset ReceivedAtUtc,
        string DetailHref,
        IReadOnlyList<(string Label, string Value)> Facts,
        Guid? OriginReceiptId = null,
        string? Chip = null,
        string? ChipTone = null,
        string? Notice = null,
        string? NoticeTone = null);

    public IReadOnlyList<QueueRow> Rows { get; private set; } = [];

    /// <summary>The open scope's column headings.</summary>
    public IReadOnlyList<string> Columns => Queue switch
    {
        "triage" => ["Reference", "Registration", "Principal", "Received", "Assignee", "State"],
        "awaiting" => ["Image reference", "Registration", "Received", "Images", "Source"],
        "unidentified" when ShowingClosed => ["Reference", "Received", "Material", "Outcome", "Source"],
        "unidentified" => ["Reference", "Received", "Material", "Reason", "Source"],
        "not_ready" => ["Case/PO", "Registration", "Claimant", "Principal", "Received", "Due", "Missing", CaseWorkspaceLabels.Frame.Editing],
        "with_engineer" => ["Case/PO", "Registration", "Claimant", "Principal", "Received", "Due", "Engineer", CaseWorkspaceLabels.Frame.Editing],
        _ => ["Case/PO", "Registration", "Claimant", "Principal", "Received", "Due", "State", CaseWorkspaceLabels.Frame.Editing]
    };

    public bool HasPreviousPage { get; private set; }

    public bool HasNextPage { get; private set; }

    /// <summary>The principals present in the loaded Case rows, for the Principal select.</summary>
    public IReadOnlyList<string> Principals { get; private set; } = [];

    /// <summary>
    /// The quick-detail pane. A Case carries its outstanding requirements;
    /// every kind carries its facts, an optional notice and the link to its
    /// full record.
    /// </summary>
    public sealed record QuickDetail(
        RowKind Kind,
        string Eyebrow,
        string Heading,
        string DetailHref,
        string OpenLabel,
        IReadOnlyList<(string Label, string Value)> Facts,
        CaseLifecycleState? State = null,
        IReadOnlyList<OperatorLabels.CaseRequirement>? Outstanding = null,
        Guid? OriginReceiptId = null,
        IReadOnlyList<(string Label, string Value)>? Work = null,
        string? StateChip = null,
        string? StateTone = null,
        string? Notice = null,
        string? NoticeTone = null);

    public QuickDetail? Selected { get; private set; }

    /// <summary>The version rendered with the awaiting-image confirmation form.</summary>
    public long? SelectedImageReceiptVersion { get; private set; }

    /// <summary>The existing submission workflow owns a manual multi-image decision.</summary>
    public Guid? SelectedImageSubmissionGroupId { get; private set; }

    /// <summary>
    /// This page's address with the given overrides. Filters ride along per
    /// the target scope — the Principal select exists on Case queues, the
    /// Missing select on Not ready only, Show on Unidentified only — so
    /// switching scope never carries a filter the destination cannot use.
    /// </summary>
    public string Href(string? tab = null, Guid? selected = null, int? page = null, bool keepFilters = true)
    {
        var target = tab ?? Queue;
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tab"] = target
        };
        if (keepFilters)
        {
            if (ListsCases(target))
            {
                values["principal"] = PrincipalFilter;
            }
            if (target == "not_ready")
            {
                values["missing"] = MissingFilter;
            }
            if (target == "unidentified")
            {
                values["show"] = ShowFilter;
            }
        }
        var pageNumber = page ?? CurrentPage;
        values["page"] = pageNumber > 1
            ? pageNumber.ToString(CultureInfo.InvariantCulture)
            : null;
        values["selected"] = selected?.ToString("D");
        return QueryHelpers.AddQueryString(
            "/Cases",
            values.Where(item => !string.IsNullOrWhiteSpace(item.Value)));
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage(new { tab = "awaiting", selected = id });

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (SearchOnlyParameters.Any(parameter => Request.Query.ContainsKey(parameter)))
        {
            return RedirectPermanent("/Search" + Request.QueryString.Value);
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (Tabs.All(tab => tab.Key != Queue)
            || CurrentPage > 10_000
            || MissingFilter is not (null or "" or "instructions" or "images" or "both")
            || ShowFilter is not (null or "" or "open" or "closed"))
        {
            return NotFound();
        }

        CurrentPage = Math.Max(1, CurrentPage);
        PrincipalFilter = EmptyToNull(PrincipalFilter);
        MissingFilter = ShowingNotReady ? EmptyToNull(MissingFilter) : null;
        ShowFilter = Queue == "unidentified" && ShowFilter == "closed" ? "closed" : null;

        try
        {
            return await LoadAsync(actor, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A failed live read is not an empty queue.
            IsUnavailable = true;
            Rows = [];
            Selected = null;
            return Page();
        }
    }

    private async Task<IActionResult> LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        // Every group carries its count whichever one is open. The three
        // count queries use their own DbContext each, so they run together.
        var stageCountsTask = _dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var triageTask = _listTriage.CountAsync(
            actor,
            state: null,
            cancellationToken: cancellationToken);
        var openUnidentifiedCountTask = _unidentifiedStore.CountOpenAsync(cancellationToken);
        await Task.WhenAll(stageCountsTask, triageTask, openUnidentifiedCountTask);
        StageCounts = stageCountsTask.Result;
        TriageCount = triageTask.Result;
        UnidentifiedCount = openUnidentifiedCountTask.Result;
        RailCountsPageFilter.SetCaseCounts(
            HttpContext,
            StageCounts,
            TriageCount,
            UnidentifiedCount);

        var rows = Queue switch
        {
            "triage" => await LoadTriageAsync(actor, cancellationToken),
            "awaiting" => await LoadAwaitingAsync(cancellationToken),
            "unidentified" => ShowingClosed
                ? await LoadClosedUnidentifiedAsync(cancellationToken)
                : await LoadOpenUnidentifiedAsync(cancellationToken),
            "not_ready" => await LoadNotReadyAsync(actor, cancellationToken),
            _ => await LoadCasesAsync(actor, cancellationToken)
        };
        Rows = rows
            .OrderByDescending(row => row.ReceivedAtUtc)
            .ThenBy(row => row.Title, StringComparer.Ordinal)
            .ToArray();

        var selectedRow = SelectedId is { } selectedId
            ? Rows.FirstOrDefault(row => row.Id == selectedId)
            : Rows.Count > 0 ? Rows[0] : null;
        if (SelectedId is not null && selectedRow is null)
        {
            var isPostAttachRedirect = Queue == "awaiting"
                && (TempData.ContainsKey("Confirmation")
                    || TempData.ContainsKey("UploadConfirmationError"));
            if (!isPostAttachRedirect)
            {
                return NotFound();
            }

            // A row just attached to a case leaves the Awaiting instruction queue
            // (LoadAwaitingAsync excludes it), so its post-attach redirect no longer
            // resolves. Preserve the TempData notice and drop only that stale selection.
            SelectedId = null;
            selectedRow = Rows.Count > 0 ? Rows[0] : null;
        }

        if (selectedRow is not null)
        {
            SelectedId = selectedRow.Id;
            Selected = await LoadDetailAsync(actor, selectedRow, cancellationToken);
            if (Selected is { Kind: RowKind.Image, OriginReceiptId: { } receiptId })
            {
                SelectedImageReceiptVersion = (await getIntake.ExecuteAsync(
                    new(receiptId, actor), cancellationToken))?.Version;
                var image = await _imageIntakeQueries.GetAsync(SelectedId.Value, cancellationToken);
                if (RequiresGroupConfirmation(image) && image!.Record.SubmissionGroupId is { } groupId)
                {
                    SelectedImageSubmissionGroupId = groupId;
                }
            }
        }

        LoadedAtUtc = _timeProvider.GetUtcNow();
        return Page();
    }

    protected override async Task<bool> SurfaceContainsReceiptAsync(
        Guid surfaceId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var image = await _imageIntakeQueries.GetByOriginReceiptAsync(receiptId, cancellationToken);
        return image is not null
            && image.Record.Id == surfaceId
            && image.Record.Origin.ReceiptId == receiptId
            && !RequiresGroupConfirmation(image);
    }

    protected override Task<IReadOnlyList<Guid>> SearchReceiptIdsAsync(
        Guid surfaceId,
        CancellationToken cancellationToken) =>
        SelectedOriginReceiptIdsAsync(surfaceId, cancellationToken);

    private async Task<IReadOnlyList<Guid>> SelectedOriginReceiptIdsAsync(
        Guid surfaceId,
        CancellationToken cancellationToken)
    {
        var image = await _imageIntakeQueries.GetAsync(surfaceId, cancellationToken);
        return image is null
            || image.State != ImageInitiatedCaseState.AwaitingInstruction
            || RequiresGroupConfirmation(image)
            ? []
            : [image.Record.Origin.ReceiptId];
    }

    protected override async Task<IActionResult> RenderSurfaceAsync(
        Guid surfaceId,
        CancellationToken cancellationToken)
    {
        TabFilter = "awaiting";
        SelectedId = surfaceId;
        return await OnGetAsync(cancellationToken);
    }

    private static bool RequiresGroupConfirmation(ImageIntakeDetail? image) =>
        image is not null
        && image.Record.Origin.SourceIdentity.Channel == IntakeSourceChannel.ManualUpload
        && image.Record.SubmissionGroupId is not null
        && image.GroupExpectedMemberCount > 1;

    private async Task<IReadOnlyList<QueueRow>> LoadCasesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        // With Engineer is two Core states read as one group (D3): both
        // pages are read and merged, so the group can carry up to two pages.
        CaseLifecycleState[] states = Queue switch
        {
            "review" => [CaseLifecycleState.Review],
            "with_engineer" => [CaseLifecycleState.ReportPreparation, CaseLifecycleState.PostReport],
            "complete" => [CaseLifecycleState.PostReportComplete],
            "query" => [CaseLifecycleState.Query],
            _ => [CaseLifecycleState.Held]
        };
        var results = await Task.WhenAll(states.Select(state => _searchCases.ExecuteAsync(
            new(
                actor,
                new(State: state, Principal: PrincipalFilter),
                CurrentPage,
                PageSize),
            cancellationToken)));
        HasPreviousPage = CurrentPage > 1;
        HasNextPage = results.Any(result => result.HasNextPage);
        var items = results.SelectMany(result => result.Items).ToArray();
        Principals = PrincipalOptions(items);
        var engineers = await EngineerNamesAsync(items, cancellationToken);
        return items.Select(item => CaseRow(item, engineers)).ToArray();
    }

    /// <summary>
    /// Formal Not ready Cases, filtered by their recorded completeness facts.
    /// </summary>
    private async Task<IReadOnlyList<QueueRow>> LoadNotReadyAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var result = await _searchCases.ExecuteAsync(
            new(
                actor,
                new(State: CaseLifecycleState.NotReady, Principal: PrincipalFilter),
                Page: 1,
                PageSize: MergedPageSize),
            cancellationToken);

        // The Missing filter is applied to the read, so what it removes
        // never reaches the Principal select's options either — a principal
        // whose every row the filter dropped stays listed (PrincipalOptions)
        // so the active choice remains visible.
        var matchingCases = result.Items
            .Where(item => MissingFilter switch
            {
                "instructions" => item.InstructionComplete == false && item.ImagesComplete == true,
                "images" => item.InstructionComplete == true && item.ImagesComplete == false,
                "both" => item.InstructionComplete == false && item.ImagesComplete == false,
                _ => true
            })
            .ToArray();
        Principals = PrincipalOptions(matchingCases);
        var engineers = await EngineerNamesAsync(matchingCases, cancellationToken);
        return matchingCases.Select(item => CaseRow(item, engineers)).ToArray();
    }

    private async Task<IReadOnlyList<QueueRow>> LoadAwaitingAsync(CancellationToken cancellationToken)
    {
        var images = await _imageIntakeQueries.ListAsync(false, cancellationToken);
        var configuration = await _workflowConfiguration.GetCurrentAsync(cancellationToken);
        return images
            .Where(item => item.State == ImageInitiatedCaseState.AwaitingInstruction)
            .Select(item => ImageRow(item, configuration.ChaseIntervalDays))
            .ToArray();
    }

    private async Task<IReadOnlyList<QueueRow>> LoadTriageAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var page = await _listTriage.ExecuteAsync(new(actor, State: null, CurrentPage, PageSize), cancellationToken);
        HasPreviousPage = page.Page > 1;
        HasNextPage = page.Page < page.TotalPages;
        var assignees = await ActorDisplayNames.ResolveStaffNamesAsync(
            _staffAccounts,
            page.Items.Where(item => item.AssigneeId is not null).Select(item => item.AssigneeId!.Value),
            cancellationToken);
        return page.Items
            .Select(item => TriageRow(item, item.AssigneeId is { } assigneeId
                ? ActorDisplayNames.Resolve(ActorKind.Staff, assigneeId.ToString("D"), assignees)
                : null))
            .ToArray();
    }

    /// <summary>
    /// The Closed filter (received file D5): each closed item with the reason it
    /// was closed, read from the item itself.
    /// </summary>
    private async Task<IReadOnlyList<QueueRow>> LoadClosedUnidentifiedAsync(CancellationToken cancellationToken)
    {
        var closed = await _unidentifiedStore.ListClosedQueueAsync(null, cancellationToken);
        var rows = new List<QueueRow>(Math.Min(closed.Count, MergedPageSize));
        foreach (var row in closed.Take(MergedPageSize))
        {
            var item = await _unidentifiedStore.GetAsync(row.Id, cancellationToken);
            rows.Add(ClosedUnidentifiedRow(row, item?.ResolutionReason));
        }

        return rows;
    }

    private async Task<IReadOnlyList<QueueRow>> LoadOpenUnidentifiedAsync(CancellationToken cancellationToken) =>
        (await _unidentifiedStore.ListQueueAsync(null, cancellationToken))
            .Select(UnidentifiedRow)
            .ToArray();

    private async Task<IReadOnlyDictionary<Guid, string>> EngineerNamesAsync(
        IEnumerable<CaseSearchItem> items,
        CancellationToken cancellationToken) =>
        await ActorDisplayNames.ResolveStaffNamesAsync(
            _staffAccounts,
            items.SelectMany(item => new[] { item.EngineerId, item.EditingStaffId })
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .Distinct(),
            cancellationToken);

    /// <summary>
    /// The selected row's quick detail. The record kinds already carry their
    /// facts; only the Case's own requirements and work need reading here.
    /// </summary>
    private async Task<QuickDetail> LoadDetailAsync(ActionActor actor, QueueRow row, CancellationToken cancellationToken)
    {
        if (row.Kind != RowKind.Case)
        {
            return RecordDetail(row);
        }

        var details = await _getCase.ExecuteAsync(new(row.Id, actor), cancellationToken)
            ?? throw new InvalidOperationException($"Case '{row.Id}' was listed but could not be read.");
        var missingRequirements = details.Data?.Completeness.Evaluation.MissingRequirements;
        var outstanding = details.Workflow.State == CaseLifecycleState.NotReady && missingRequirements is not null
            ? OperatorLabels.CaseRequirements(missingRequirements)
            : [];

        var work = new List<(string Label, string Value)>(3);
        var dueWork = details.Workflow.DueWork;
        // Current work is the first outstanding requirement's resolve text,
        // else the due work's own state — never a sentence written here.
        if (outstanding.Count > 0)
        {
            work.Add(("Current work", outstanding[0].Resolve));
        }
        else if (dueWork is not null)
        {
            work.Add(("Current work", OperatorLabels.ChaseState(dueWork.State)));
        }

        var engineer = "Not assigned";
        if (details.Workflow.AssignedEngineerId is { } engineerId)
        {
            var names = await ActorDisplayNames.ResolveStaffNamesAsync(_staffAccounts, [engineerId], cancellationToken);
            engineer = ActorDisplayNames.Resolve(ActorKind.Staff, engineerId.ToString("D"), names);
        }
        work.Add(("Engineer", engineer));

        if (dueWork?.DueBy is { } dueBy)
        {
            work.Add(("Due", OperatorLabels.OfficeDate(dueBy.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))));
        }
        else if (details.Summary.NextChaseAtUtc is { } nextChase)
        {
            work.Add(("Due", OperatorLabels.OfficeDate(nextChase)));
        }

        var summary = details.Summary;
        var vehicle = string.Join(" ", new[] { summary.VehicleMake, summary.VehicleModel }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new(
            RowKind.Case,
            OperatorLabels.SourceChannel(summary.Origin),
            row.Title,
            row.DetailHref,
            "Open full Case",
            [
                ("Case/PO", summary.Reference),
                ("Registration", summary.Registration ?? "Not recorded"),
                ("Claimant", summary.Claimant ?? "Not recorded"),
                ("Vehicle", vehicle.Length > 0 ? vehicle : "Not recorded"),
                ("Type", OperatorLabels.CaseTypeName(summary.CaseType))
            ],
            details.Workflow.State,
            outstanding,
            Work: work,
            StateChip: row.Chip,
            StateTone: row.ChipTone);
    }

    private static QuickDetail RecordDetail(QueueRow row) =>
        new(
            row.Kind,
            row.Kind switch
            {
                RowKind.Image => "Image-initiated Case",
                RowKind.Triage => "Triage",
                _ => "Unidentified"
            },
            row.Title,
            row.DetailHref,
            row.Kind switch
            {
                RowKind.Image => "Open image record",
                RowKind.Triage => "Open Triage",
                _ => "Open Unidentified"
            },
            row.Facts,
            OriginReceiptId: row.OriginReceiptId,
            StateChip: row.Chip,
            StateTone: row.ChipTone,
            Notice: row.Notice,
            NoticeTone: row.NoticeTone);

    /// <summary>"Held · review on 24 Sep" when a held Case has a review date; otherwise the D3 stage name.</summary>
    public static string StateChipText(CaseSearchItem item) =>
        item.State == CaseLifecycleState.Held && item.HoldReviewOn is { } reviewOn
            ? $"{OperatorLabels.CaseStage(CaseLifecycleState.Held)} · review on {reviewOn.ToString("d MMM", CultureInfo.InvariantCulture)}"
            : OperatorLabels.CaseStage(item.State);

    /// <summary>What a Not ready Case is missing, from its recorded completeness facts.</summary>
    public static string MissingText(CaseSearchItem item) => (item.InstructionComplete, item.ImagesComplete) switch
    {
        (false, false) => "Instructions and images",
        (false, _) => "Instructions",
        (_, false) => "Images",
        _ => "Review pending"
    };

    private QueueRow CaseRow(CaseSearchItem item, IReadOnlyDictionary<Guid, string> engineers)
    {
        var now = _timeProvider.GetUtcNow();
        var chip = StateChipText(item);
        var last = Queue switch
        {
            "not_ready" => new Cell(MissingText(item), CellKind.Chip, MissingText(item) == "Review pending" ? "neutral" : "amber"),
            "with_engineer" => Cell.Of(item.EngineerId is { } engineerId
                ? ActorDisplayNames.Resolve(ActorKind.Staff, engineerId.ToString("D"), engineers)
                : null),
            _ => new Cell(chip, CellKind.Chip)
        };
        return new QueueRow(
            RowKind.Case,
            item.CaseId,
            Join(item.Reference, item.Registration),
            [
                new Cell(item.Reference, CellKind.Link),
                item.Registration is { } registration ? new Cell(registration, CellKind.Mono) : Cell.Empty,
                Cell.Of(item.Claimant),
                Cell.Of(item.Principal),
                new Cell(OperatorLabels.OfficeDate(item.ReceivedAtUtc), CellKind.Date),
                item.NextChaseAtUtc is { } chase
                    ? new Cell(OperatorLabels.OfficeDate(chase), chase < now ? CellKind.Late : CellKind.Date)
                    : Cell.Empty,
                last,
                // Who holds the Case's edit lease right now, so nobody opens a
                // Case only to find it taken (D2 of the 15 September walk).
                Cell.Of(item.EditingStaffId is { } editing
                    ? ActorDisplayNames.Resolve(ActorKind.Staff, editing.ToString("D"), engineers)
                    : null)
            ],
            item.ReceivedAtUtc,
            $"/Cases/{item.CaseId:D}",
            [],
            Chip: chip,
            ChipTone: null) with
        {
            // An Audit Case (a.) reads its type beside its reference.
            Notice = item.CaseType == CaseType.Audit ? OperatorLabels.CaseTypeName(CaseType.Audit) : null
        };
    }

    private QueueRow ImageRow(ImageIntakeSummary item, int chaseIntervalDays)
    {
        var imageCountLabel = $"{item.ImageCount} retained image{(item.ImageCount == 1 ? string.Empty : "s")}";
        var facts = new List<(string Label, string Value)>
        {
            ("Image reference", item.ImageIntakeReference),
            ("Registration", item.NormalizedVehicleRegistration),
            ("Images", imageCountLabel),
        };
        if (item.Custody is { } custodyDetail)
        {
            facts.Add(("Box", OperatorLabels.ImageCustodyState(custodyDetail)));
        }
        facts.Add(("Received", OperatorLabels.OfficeDate(item.RegisteredAtUtc)));
        facts.Add(("Source", OperatorLabels.SourceChannel(item.Source)));
        facts.Add(("Chase", OperatorLabels.ImageChaseState(
            ImageIntakeChaseSchedule.IsChaseDue(
                item.RegisteredAtUtc,
                _timeProvider.GetUtcNow(),
                chaseIntervalDays))));
        return new QueueRow(
            RowKind.Image,
            item.Id,
            Join(item.ImageIntakeReference, item.NormalizedVehicleRegistration),
            [
                new Cell(item.ImageIntakeReference, CellKind.Link),
                new Cell(item.NormalizedVehicleRegistration, CellKind.Mono),
                new Cell(OperatorLabels.OfficeDate(item.RegisteredAtUtc), CellKind.Date),
                new Cell(item.ImageCount.ToString(CultureInfo.InvariantCulture)),
                new Cell(OperatorLabels.SourceChannel(item.Source))
            ],
            item.RegisteredAtUtc,
            $"/VehicleImages/{item.Id:D}",
            facts,
            item.OriginReceiptId,
            Chip: "Awaiting instruction",
            ChipTone: "amber");
    }

    private static QueueRow TriageRow(TriageSummary item, string? assignee)
    {
        var facts = new List<(string Label, string Value)>
        {
            ("Reference", item.Reference)
        };
        facts.Add(("Registration", item.NormalizedVehicleRegistration));
        facts.Add(("Principal", item.Provider ?? "Not known"));
        facts.Add(("Assigned to", assignee ?? "Unassigned"));
        facts.Add(("Opened", OperatorLabels.OfficeDate(item.CreatedAtUtc)));
        return new QueueRow(
            RowKind.Triage,
            item.CaseId,
            Join(item.Reference, item.NormalizedVehicleRegistration),
            [
                new Cell(item.Reference, CellKind.Link),
                new Cell(item.NormalizedVehicleRegistration, CellKind.Mono),
                Cell.Of(item.Provider),
                new Cell(OperatorLabels.OfficeDate(item.CreatedAtUtc), CellKind.Date),
                Cell.Of(assignee),
                new Cell(OperatorLabels.TriageState(item.State), CellKind.Chip)
            ],
            item.CreatedAtUtc,
            $"/Cases/{item.CaseId:D}",
            facts,
            Chip: OperatorLabels.TriageState(item.State));
    }

    private static QueueRow UnidentifiedRow(UnidentifiedQueueRow row) => new(
        RowKind.Unidentified,
        row.Id,
        Join(row.Reference, OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
        [
            new Cell(row.Reference, CellKind.Link),
            new Cell(OperatorLabels.OfficeTime(row.ReceivedAtUtc), CellKind.Date),
            new Cell(OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
            new Cell(OperatorLabels.UnidentifiedReason(row.ReasonCode)),
            new Cell(Handle(row))
        ],
        row.ReceivedAtUtc,
        $"/Unidentified/{row.Id:D}",
        [
            ("Reference", row.Reference),
            ("Material", OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
            ("Received", OperatorLabels.OfficeTime(row.ReceivedAtUtc)),
            ("Source", Handle(row))
        ],
        Notice: OperatorLabels.UnidentifiedReason(row.ReasonCode),
        NoticeTone: "warning");

    private static QueueRow ClosedUnidentifiedRow(UnidentifiedQueueRow row, string? reason)
    {
        var outcome = string.IsNullOrWhiteSpace(reason) ? "Closed" : $"Closed · {reason}";
        return new(
            RowKind.Unidentified,
            row.Id,
            Join(row.Reference, OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
            [
                new Cell(row.Reference, CellKind.Link),
                new Cell(OperatorLabels.OfficeTime(row.ReceivedAtUtc), CellKind.Date),
                new Cell(OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
                new Cell(outcome),
                new Cell(Handle(row))
            ],
            row.ReceivedAtUtc,
            $"/Unidentified/{row.Id:D}",
            [
                ("Reference", row.Reference),
                ("Material", OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
                ("Received", OperatorLabels.OfficeTime(row.ReceivedAtUtc)),
                ("Source", Handle(row))
            ],
            Notice: outcome,
            NoticeTone: "success");
    }

    /// <summary>"first · second", dropping whichever half is absent.</summary>
    private static string Join(string? first, string? second) =>
        string.Join(" · ", new[] { first, second }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string[] PrincipalsOf(IEnumerable<CaseSearchItem> items) => items
        .Select(item => item.Principal)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// The Principal select's options: the principals of the rows the
    /// filters still show — a sample of the queue, not a census — plus the
    /// active principal when no shown row carries it, so the choice that is
    /// filtering the list is never invisible in the control that made it.
    /// </summary>
    private string[] PrincipalOptions(CaseSearchItem[] items)
    {
        var options = PrincipalsOf(items).ToList();
        if (PrincipalFilter is not null && !options.Contains(PrincipalFilter))
        {
            options.Add(PrincipalFilter);
            options.Sort(StringComparer.Ordinal);
        }
        return [.. options];
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The operator-meaningful handle for an Unidentified row: the original
    /// filename for an image or document, or the subject and sender for an
    /// e-mail (formatted by the one shared rule,
    /// <see cref="OperatorLabels.EmailHandle"/>, that the Unidentified detail
    /// page also uses). Never a GUID or internal reference.
    /// </summary>
    private static string Handle(UnidentifiedQueueRow row) => row.MediaKind switch
    {
        UnidentifiedMediaKind.Email => OperatorLabels.EmailHandle(row.EmailSubject, row.EmailSender),
        _ => row.FileName ?? "Not available"
    };
}
