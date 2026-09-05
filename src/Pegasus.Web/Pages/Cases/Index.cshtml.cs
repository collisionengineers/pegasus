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
/// Cases (EPIC-011 §1.4): the three-pane queue — a rail of workflow groups
/// with queried counts, the rows of the open group, and a quick detail of the
/// selected row.
/// </summary>
/// <remarks>
/// The rail groups are Workflow (Not ready, Review, With Engineer, Complete),
/// Pre-Case work (Triage, Awaiting instruction) and Exceptions (Held,
/// Unidentified). With Engineer and Complete are display groupings of Core
/// states (D3); the other terminal outcomes are not listed here. Blocked
/// intake rows sit in the Unidentified group with their own chip and are not
/// counted (D14).
///
/// The group is <c>?tab=</c>; the pre-EPIC-011 <c>?queue=</c> is accepted as
/// an alias and the README's hyphenated spellings normalise to the same keys.
/// A request carrying a search-only parameter belongs to <c>/Search</c> and
/// is redirected there permanently with its values intact.
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
    IListIntake listIntake,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : UploadConfirmationPageModel(caseDecision)
{
    private const int PageSize = 25;

    /// <summary>
    /// Not ready and Unidentified merge independent sources into one list, so
    /// both are read whole rather than paged — the bounded exception-queue
    /// trade-off, not a second convention.
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
    private readonly IListIntake _listIntake =
        listIntake ?? throw new ArgumentNullException(nameof(listIntake));
    private readonly IStaffAccountQueries _staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));
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
        new("triage", "Triage", PreCaseGroup, "icon-file-text"),
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

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabFilter { get; set; }

    /// <summary>The pre-EPIC-011 name of <see cref="TabFilter"/>, accepted as an alias.</summary>
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

    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>The row the quick-detail pane is showing; the first row when unset.</summary>
    [BindProperty(SupportsGet = true, Name = "selected")]
    public Guid? SelectedId { get; set; }

    public bool ShowingNotReady => Queue == "not_ready";

    /// <summary>Whether the scope lists Case rows, so the Principal filter applies.</summary>
    public static bool ListsCases(string queue) =>
        queue is "not_ready" or "review" or "with_engineer" or "complete" or "held";

    public CaseStageCounts StageCounts { get; private set; } = new(0, 0, 0, 0);

    public int TriageCount { get; private set; }

    /// <summary>Open Unidentified items only — Blocked intake rows are listed but never counted (D14).</summary>
    public int UnidentifiedCount { get; private set; }

    public int Count(Tab tab) => tab.Key switch
    {
        "not_ready" => StageCounts.NotReady,
        "review" => StageCounts.Review,
        "with_engineer" => StageCounts.WithEngineer,
        "complete" => StageCounts.Complete,
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
        Unidentified,
        BlockedIntake
    }

    /// <summary>
    /// One row of the middle pane, whichever kind: the title line, its chip,
    /// the excerpt, the meta line, the right-hand time (a Case's due; the
    /// other kinds have none) and when it was received, which orders the
    /// newest first. Each kind fills the lines per §1.4, and
    /// <see cref="Facts"/> is the same row's quick-detail definition list,
    /// built here where the source item is in hand rather than recovered by
    /// unpicking the joined display strings later.
    /// </summary>
    public sealed record QueueRow(
        RowKind Kind,
        Guid Id,
        string Title,
        string Chip,
        string Excerpt,
        string Meta,
        string? Time,
        DateTimeOffset ReceivedAtUtc,
        string DetailHref,
        IReadOnlyList<(string Label, string Value)> Facts,
        Guid? OriginReceiptId = null);

    public IReadOnlyList<QueueRow> Rows { get; private set; } = [];

    public bool HasPreviousPage { get; private set; }

    public bool HasNextPage { get; private set; }

    /// <summary>The principals present in the loaded Case rows, for the Principal select.</summary>
    public IReadOnlyList<string> Principals { get; private set; } = [];

    /// <summary>
    /// The quick-detail pane. A Case carries its state for the stepper and
    /// its outstanding requirements; every kind carries a definition list and
    /// the link to its full record.
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
        Guid? OriginReceiptId = null);

    public QuickDetail? Selected { get; private set; }

    /// <summary>The compact stepper's four steps, in workflow order.</summary>
    public static readonly IReadOnlyList<(string Label, string Icon)> Steps =
    [
        (OperatorLabels.CaseStage(CaseLifecycleState.NotReady), "icon-clock"),
        (OperatorLabels.CaseStage(CaseLifecycleState.Review), "icon-check-circle"),
        (OperatorLabels.CaseStage(CaseLifecycleState.ReportPreparation), "icon-user"),
        (OperatorLabels.CaseStage(CaseLifecycleState.PostReportComplete), "icon-check")
    ];

    /// <summary>The step a state sits at, or -1 for Held and the excluded terminals.</summary>
    public static int StepIndex(CaseLifecycleState state) => state switch
    {
        CaseLifecycleState.NotReady => 0,
        CaseLifecycleState.Review => 1,
        CaseLifecycleState.ReportPreparation or CaseLifecycleState.PostReport => 2,
        CaseLifecycleState.PostReportComplete => 3,
        _ => -1
    };

    /// <summary>
    /// This page's address with the given overrides. Filters ride along per
    /// the target scope — the Principal select exists on Case queues, the
    /// Missing select on Not ready only — so switching scope never carries a
    /// filter the destination cannot use.
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
            || MissingFilter is not (null or "" or "instructions" or "images" or "both"))
        {
            return NotFound();
        }

        CurrentPage = Math.Max(1, CurrentPage);
        PrincipalFilter = EmptyToNull(PrincipalFilter);
        MissingFilter = ShowingNotReady ? EmptyToNull(MissingFilter) : null;

        // Every group carries its count whichever one is open. The three
        // count queries use their own DbContext each, so they run together.
        var stageCountsTask = _dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var triageTask = _listTriage.ExecuteAsync(new(actor, State: null, Page: 1, PageSize: 1), cancellationToken);
        var openUnidentifiedTask = _unidentifiedStore.ListQueueAsync(null, cancellationToken);
        await Task.WhenAll(stageCountsTask, triageTask, openUnidentifiedTask);
        StageCounts = stageCountsTask.Result;
        TriageCount = triageTask.Result.TotalCount;
        UnidentifiedCount = openUnidentifiedTask.Result.Count;

        var rows = Queue switch
        {
            "triage" => await LoadTriageAsync(actor, cancellationToken),
            "awaiting" => await LoadAwaitingAsync(cancellationToken),
            "unidentified" => await LoadUnidentifiedAsync(actor, openUnidentifiedTask.Result, cancellationToken),
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
        }

        LoadedAtUtc = _timeProvider.GetUtcNow();
        return Page();
    }

    private async Task<IReadOnlyList<QueueRow>> LoadCasesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        // With Engineer is two Core states read as one group (D3): both
        // pages are read and merged, so the group can carry up to two pages.
        CaseLifecycleState[] states = Queue switch
        {
            "review" => [CaseLifecycleState.Review],
            "with_engineer" => [CaseLifecycleState.ReportPreparation, CaseLifecycleState.PostReport],
            "complete" => [CaseLifecycleState.PostReportComplete],
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
        return items.Select(CaseRow).ToArray();
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
        return matchingCases.Select(CaseRow).ToArray();
    }

    private async Task<IReadOnlyList<QueueRow>> LoadAwaitingAsync(CancellationToken cancellationToken)
    {
        var images = await _imageIntakeQueries.ListAsync(false, cancellationToken);
        return images
            .Where(item => item.State == ImageInitiatedCaseState.AwaitingInstruction)
            .Select(ImageRow)
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
                : "Unassigned"))
            .ToArray();
    }

    /// <summary>
    /// The Unidentified group: the open Unidentified items (the counted rows,
    /// already read for the rail) plus the Blocked intake receipts, listed
    /// with their own chip and left out of the count (D14).
    /// </summary>
    private async Task<IReadOnlyList<QueueRow>> LoadUnidentifiedAsync(
        ActionActor actor,
        IReadOnlyList<UnidentifiedQueueRow> openRows,
        CancellationToken cancellationToken)
    {
        var blocked = await _listIntake.ExecuteAsync(
            new(actor, IntakeDecision.BlockedIntake, Page: 1, PageSize: MergedPageSize),
            cancellationToken);
        return openRows.Select(UnidentifiedRow)
            .Concat(blocked.Items.Select(BlockedRow))
            .ToArray();
    }

    /// <summary>
    /// The selected row's quick detail. The record kinds already carry their
    /// definition lists; only the Case's own facts need reading here.
    /// </summary>
    private async Task<QuickDetail> LoadDetailAsync(ActionActor actor, QueueRow row, CancellationToken cancellationToken)
    {
        if (row.Kind != RowKind.Case)
        {
            return RecordDetail(row, row.Facts);
        }

        var details = await _getCase.ExecuteAsync(new(row.Id, actor), cancellationToken)
            ?? throw new InvalidOperationException($"Case '{row.Id}' was listed but could not be read.");
        var completeness = details.Data?.Completeness.Values;
        var outstanding = details.Workflow.State == CaseLifecycleState.NotReady && completeness is not null
            ? OperatorLabels.CaseRequirements(!completeness.InstructionComplete, !completeness.ImagesComplete)
            : [];

        var facts = new List<(string Label, string Value)>(3);
        var dueWork = details.Workflow.DueWork;
        if (dueWork?.DueBy is { } dueBy)
        {
            facts.Add(("Due", OperatorLabels.OfficeDate(dueBy.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))));
        }
        else if (details.Summary.NextChaseAtUtc is { } nextChase)
        {
            facts.Add(("Due", OperatorLabels.OfficeDate(nextChase)));
        }

        if (details.Workflow.AssignedEngineerId is { } engineerId)
        {
            var names = await ActorDisplayNames.ResolveStaffNamesAsync(_staffAccounts, [engineerId], cancellationToken);
            facts.Add(("Engineer", ActorDisplayNames.Resolve(ActorKind.Staff, engineerId.ToString("D"), names)));
        }

        // Next action is the first outstanding requirement's resolve text,
        // else the due work's own state — never a sentence written here.
        if (outstanding.Count > 0)
        {
            facts.Add(("Next action", outstanding[0].Resolve));
        }
        else if (dueWork is not null)
        {
            facts.Add(("Next action", dueWork.NextChaseAtUtc is { } chase
                ? $"{OperatorLabels.ChaseState(dueWork.State)} · {OperatorLabels.OfficeDate(chase)}"
                : OperatorLabels.ChaseState(dueWork.State)));
        }

        return new(
            RowKind.Case,
            OperatorLabels.SourceChannel(details.Summary.Origin),
            row.Title,
            row.DetailHref,
            "Open full Case",
            facts,
            details.Workflow.State,
            outstanding);
    }

    private static QuickDetail RecordDetail(QueueRow row, IReadOnlyList<(string Label, string Value)> facts) =>
        new(
            row.Kind,
            row.Kind switch
            {
                RowKind.Image => "Image-initiated Case",
                RowKind.Triage => "Triage",
                RowKind.BlockedIntake => "Blocked intake",
                _ => "Unidentified"
            },
            row.Title,
            row.DetailHref,
            row.Kind switch
            {
                RowKind.Image => "Open image record",
                RowKind.Triage => "Open Triage",
                RowKind.BlockedIntake => "Open received item",
                _ => "Open Unidentified"
            },
            facts,
            OriginReceiptId: row.OriginReceiptId);

    private static QueueRow CaseRow(CaseSearchItem item) => new(
        RowKind.Case,
        item.CaseId,
        Join(item.Reference, item.Registration),
        OperatorLabels.CaseStage(item.State),
        Join(item.Claimant, item.Principal),
        $"{OperatorLabels.SourceChannel(item.Origin)} · received {OperatorLabels.OfficeDate(item.ReceivedAtUtc)}",
        item.NextChaseAtUtc is { } chase ? $"Due {OperatorLabels.OfficeDate(chase)}" : null,
        item.ReceivedAtUtc,
        $"/Cases/{item.CaseId:D}",
        []);

    private QueueRow ImageRow(ImageIntakeSummary item)
    {
        var imageCountLabel = $"{item.ImageCount} retained image{(item.ImageCount == 1 ? string.Empty : "s")}";
        var facts = new List<(string Label, string Value)>
        {
            ("Images", imageCountLabel),
        };
        if (item.Custody is { } custodyDetail)
        {
            facts.Add(("Custody", OperatorLabels.ImageCustodyState(custodyDetail)));
        }
        facts.Add(("Received", OperatorLabels.OfficeDate(item.RegisteredAtUtc)));
        facts.Add(("Source", OperatorLabels.SourceChannel(item.Source)));
        facts.Add(("Chase", OperatorLabels.ImageChaseState(
            ImageIntakeChaseSchedule.IsChaseDue(item.RegisteredAtUtc, _timeProvider.GetUtcNow()))));
        return new QueueRow(
            RowKind.Image,
            item.Id,
            Join(item.ImageIntakeReference, item.NormalizedVehicleRegistration),
            string.Empty,
            Join(
                imageCountLabel,
                item.Custody is { } custody ? OperatorLabels.ImageCustodyState(custody) : null),
            $"{OperatorLabels.SourceChannel(item.Source)} · received {OperatorLabels.OfficeDate(item.RegisteredAtUtc)}",
            null,
            item.RegisteredAtUtc,
            $"/VehicleImages/{item.Id:D}",
            facts,
            item.OriginReceiptId);
    }

    private static QueueRow TriageRow(TriageSummary item, string assignee)
    {
        var facts = new List<(string Label, string Value)>();
        if (item.Reference is { } reference)
        {
            facts.Add(("Reference", reference));
        }
        facts.Add(("Registration", item.NormalizedVehicleRegistration));
        if (item.Provider is { } provider)
        {
            facts.Add(("Provider", provider));
        }
        facts.Add(("State", OperatorLabels.TriageState(item.State)));
        facts.Add(("Assigned to", assignee));
        facts.Add(("Opened", OperatorLabels.OfficeDate(item.CreatedAtUtc)));
        return new QueueRow(
            RowKind.Triage,
            item.Id,
            Join(item.Reference, item.NormalizedVehicleRegistration),
            OperatorLabels.TriageState(item.State),
            Join(item.Provider, assignee),
            $"Opened {OperatorLabels.OfficeDate(item.CreatedAtUtc)}",
            null,
            item.CreatedAtUtc,
            $"/Triage/{item.Id:D}",
            facts);
    }

    private static QueueRow UnidentifiedRow(UnidentifiedQueueRow row) => new(
        RowKind.Unidentified,
        row.Id,
        Join(row.Reference, OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
        OperatorLabels.UnidentifiedState(Pegasus.Core.Intake.Unidentified.UnidentifiedState.Open),
        Handle(row),
        $"{OperatorLabels.OfficeTime(row.ReceivedAtUtc)} · {OperatorLabels.UnidentifiedReason(row.ReasonCode)}",
        null,
        row.ReceivedAtUtc,
        $"/Unidentified/{row.Id:D}",
        [
            ("Kind", OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
            ("Handle", Handle(row)),
            ("Received", OperatorLabels.OfficeTime(row.ReceivedAtUtc)),
            ("Reason", OperatorLabels.UnidentifiedReason(row.ReasonCode))
        ]);

    /// <summary>
    /// A Blocked intake receipt: counted nowhere on this page (D14), listed
    /// here because this is where the operator decides what to do with it.
    /// "Blocked intake" is the settled chip word for the kind itself.
    /// </summary>
    private static QueueRow BlockedRow(IntakeReceiptSummary item)
    {
        var handle = item.Sender is null
            ? string.Empty
            : OperatorLabels.EmailHandle(item.Subject, item.Sender);
        var facts = new List<(string Label, string Value)>
        {
            ("File", item.SourceFileName)
        };
        if (handle.Length > 0)
        {
            facts.Add(("E-mail", handle));
        }
        facts.Add(("Received", OperatorLabels.OfficeTime(item.ReceivedAtUtc)));
        facts.Add(("Reason", OperatorLabels.IntakeFailure(item.FailureReason)));
        return new QueueRow(
            RowKind.BlockedIntake,
            item.Id,
            item.SourceFileName,
            "Blocked intake",
            handle,
            $"{OperatorLabels.OfficeTime(item.ReceivedAtUtc)} · {OperatorLabels.IntakeFailure(item.FailureReason)}",
            null,
            item.ReceivedAtUtc,
            $"/Received/{item.Id:D}",
            facts);
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
