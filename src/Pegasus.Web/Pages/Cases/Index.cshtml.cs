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
/// Pre-Case work (Triage) and Exceptions (Held, Unidentified). With Engineer
/// and Complete are display groupings of Core states (D3); the other terminal
/// outcomes are not listed here. Blocked intake rows sit in the Unidentified
/// group with their own chip and are not counted (D14).
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
    IListIntake listIntake,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : StaffPageModel
{
    private const int PageSize = 25;

    /// <summary>
    /// Not ready merges two origins into one list, so it is read whole rather
    /// than paged — the bounded exception-queue trade-off Unidentified makes.
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

    /// <summary>The rail, in rail order; the group labels are the section labels.</summary>
    public static readonly IReadOnlyList<Tab> Tabs =
    [
        new("not_ready", OperatorLabels.CaseStage(CaseLifecycleState.NotReady), WorkflowGroup, "icon-clock"),
        new("review", OperatorLabels.CaseStage(CaseLifecycleState.Review), WorkflowGroup, "icon-check-circle"),
        new("with_engineer", OperatorLabels.CaseStage(CaseLifecycleState.ReportPreparation), WorkflowGroup, "icon-user"),
        new("complete", OperatorLabels.CaseStage(CaseLifecycleState.PostReportComplete), WorkflowGroup, "icon-check"),
        new("triage", "Triage", PreCaseGroup, "icon-file-text"),
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

    public string Queue => (string.IsNullOrWhiteSpace(TabFilter)
            ? string.IsNullOrWhiteSpace(QueueFilter) ? "not_ready" : QueueFilter
            : TabFilter)
        .Trim().ToLowerInvariant().Replace('-', '_');

    public Tab CurrentTab => Tabs.First(tab => tab.Key == Queue);

    [BindProperty(SupportsGet = true, Name = "principal")]
    public string? PrincipalFilter { get; set; }

    /// <summary>
    /// The Not ready group's Missing filter: <c>instructions</c>, <c>images</c>
    /// or <c>both</c>, read from each case's recorded completeness facts.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "missing")]
    public string? MissingFilter { get; set; }

    /// <summary>
    /// <c>received_desc</c> (default, newest first) or <c>received_asc</c>.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? SortParam { get; set; }

    public bool OldestFirst => SortParam == "received_asc";

    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "selected")]
    public Guid? SelectedId { get; set; }

    public bool ShowingNotReady => Queue == "not_ready";

    /// <summary>Whether the open group lists Cases, so the Principal filter applies.</summary>
    public bool ShowingCases => Queue is "not_ready" or "review" or "with_engineer" or "complete" or "held";

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
    /// two detail lines and the right-hand time. Each kind fills the lines
    /// per §1.4; an absent fact leaves its line empty rather than a dash.
    /// </summary>
    public sealed record QueueRow(
        RowKind Kind,
        Guid Id,
        string Title,
        string Chip,
        string Detail,
        string Meta,
        DateTimeOffset ReceivedAtUtc,
        string? Time,
        string OpenHref);

    public IReadOnlyList<QueueRow> Rows { get; private set; } = [];

    public bool HasPreviousPage { get; private set; }

    public bool HasNextPage { get; private set; }

    /// <summary>The principals present in the loaded rows, for the Principal select.</summary>
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
        string OpenHref,
        string OpenLabel,
        IReadOnlyList<KeyValuePair<string, string>> Facts,
        CaseLifecycleState? State = null,
        IReadOnlyList<OperatorLabels.CaseRequirement>? Outstanding = null);

    public QuickDetail? Selected { get; private set; }

    /// <summary>The four steps of the compact stepper, plus the Held exception cell.</summary>
    public static readonly IReadOnlyList<(string Label, string Icon)> Steps =
    [
        (OperatorLabels.CaseStage(CaseLifecycleState.NotReady), "icon-clock"),
        (OperatorLabels.CaseStage(CaseLifecycleState.Review), "icon-check-circle"),
        (OperatorLabels.CaseStage(CaseLifecycleState.ReportPreparation), "icon-user"),
        (OperatorLabels.CaseStage(CaseLifecycleState.PostReportComplete), "icon-check")
    ];

    /// <summary>The step index a state sits at, or -1 for Held and the excluded terminals.</summary>
    public static int StepIndex(CaseLifecycleState state) => state switch
    {
        CaseLifecycleState.NotReady => 0,
        CaseLifecycleState.Review => 1,
        CaseLifecycleState.ReportPreparation or CaseLifecycleState.PostReport => 2,
        CaseLifecycleState.PostReportComplete => 3,
        _ => -1
    };

    /// <summary>This page's address with the current filters and the given overrides.</summary>
    public string Href(
        string? tab = null,
        Guid? selected = null,
        string? sort = null,
        int? page = null,
        bool keepFilters = true)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tab"] = tab ?? Queue
        };
        if (keepFilters && ShowingCases)
        {
            values["principal"] = PrincipalFilter;
            values["missing"] = ShowingNotReady ? MissingFilter : null;
        }
        values["sort"] = sort ?? SortParam;
        var pageNumber = page ?? CurrentPage;
        values["page"] = pageNumber > 1 ? pageNumber.ToString() : null;
        values["selected"] = selected?.ToString("D");
        return QueryHelpers.AddQueryString(
            "/Cases",
            values.Where(item => !string.IsNullOrWhiteSpace(item.Value)));
    }

    public string SortToggleHref() => Href(sort: OldestFirst ? null : "received_asc", page: 1);

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
            || SortParam is not (null or "" or "received_desc" or "received_asc")
            || MissingFilter is not (null or "" or "instructions" or "images" or "both"))
        {
            return NotFound();
        }

        CurrentPage = Math.Max(1, CurrentPage);
        SortParam = string.IsNullOrWhiteSpace(SortParam) || SortParam == "received_desc" ? null : SortParam;
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
            "unidentified" => await LoadUnidentifiedAsync(actor, openUnidentifiedTask.Result, cancellationToken),
            "not_ready" => await LoadNotReadyAsync(actor, cancellationToken),
            _ => await LoadCasesAsync(actor, cancellationToken)
        };
        Rows = (OldestFirst
                ? rows.OrderBy(row => row.ReceivedAtUtc)
                : rows.OrderByDescending(row => row.ReceivedAtUtc))
            .ThenBy(row => row.Title, StringComparer.Ordinal)
            .ToArray();

        var selectedRow = SelectedId is { } selectedId
            ? Rows.FirstOrDefault(row => row.Id == selectedId)
            : Rows.FirstOrDefault();
        if (SelectedId is not null && selectedRow is null)
        {
            return NotFound();
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
                PageSize,
                OldestFirst ? CaseSearchOrder.ReceivedAsc : CaseSearchOrder.ReceivedDesc),
            cancellationToken)));
        HasPreviousPage = results.Any(result => result.HasPreviousPage);
        HasNextPage = results.Any(result => result.HasNextPage);
        var items = results.SelectMany(result => result.Items).ToArray();
        Principals = PrincipalsOf(items);
        return items.Select(CaseRow).ToArray();
    }

    /// <summary>
    /// Not ready's two origins — formal Cases and Image-initiated Cases still
    /// awaiting instruction — read together and filtered by what each is
    /// missing. An image-initiated row is missing its instruction by
    /// definition, so it is listed for All and Instructions only, and never
    /// for a named Principal, which it does not have yet.
    /// </summary>
    private async Task<IReadOnlyList<QueueRow>> LoadNotReadyAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var casesTask = _searchCases.ExecuteAsync(
            new(
                actor,
                new(State: CaseLifecycleState.NotReady, Principal: PrincipalFilter),
                Page: 1,
                PageSize: MergedPageSize,
                OldestFirst ? CaseSearchOrder.ReceivedAsc : CaseSearchOrder.ReceivedDesc),
            cancellationToken);
        var listImages = PrincipalFilter is null && MissingFilter is null or "instructions";
        var imagesTask = listImages
            ? _imageIntakeQueries.ListAsync(false, cancellationToken)
            : Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
        await Task.WhenAll(casesTask, imagesTask);

        Principals = PrincipalsOf(casesTask.Result.Items);
        var cases = casesTask.Result.Items
            .Where(item => MissingFilter switch
            {
                "instructions" => item.InstructionComplete == false,
                "images" => item.ImagesComplete == false,
                "both" => item.InstructionComplete == false && item.ImagesComplete == false,
                _ => true
            })
            .Select(CaseRow);
        var images = imagesTask.Result
            .Where(item => item.State == ImageInitiatedCaseState.AwaitingInstruction)
            .Select(item => new QueueRow(
                RowKind.Image,
                item.Id,
                Title(item.ImageIntakeReference, item.NormalizedVehicleRegistration),
                OperatorLabels.ImageIntakeLifecycleState(item.State),
                OperatorLabels.SourceChannel(IntakeSourceChannel.ManualUpload) is var _ ? string.Empty : string.Empty,
                OperatorLabels.OfficeDate(item.RegisteredAtUtc),
                item.RegisteredAtUtc,
                OperatorLabels.ImageChaseState(ImageIntakeChaseSchedule.IsChaseDue(item.RegisteredAtUtc, _timeProvider.GetUtcNow())),
                $"/VehicleImages/{item.Id:D}"));
        return cases.Concat(images).ToArray();
    }

    private async Task<IReadOnlyList<QueueRow>> LoadTriageAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var page = await _listTriage.ExecuteAsync(new(actor, State: null, CurrentPage, PageSize), cancellationToken);
        HasPreviousPage = page.Page > 1;
        HasNextPage = page.Page < page.TotalPages;
        return page.Items
            .Select(item => new QueueRow(
                RowKind.Triage,
                item.Id,
                item.NormalizedVehicleRegistration,
                OperatorLabels.TriageState(item.State),
                Assignee(item.AssigneeId),
                OperatorLabels.OfficeDate(item.CreatedAtUtc),
                item.CreatedAtUtc,
                null,
                $"/Triage/{item.Id:D}"))
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
        var unidentified = openRows.Select(row => new QueueRow(
            RowKind.Unidentified,
            row.Id,
            Title(row.Reference, OperatorLabels.UnidentifiedMediaKind(row.MediaKind)),
            "Unidentified",
            UnidentifiedHandle(row),
            Title(OperatorLabels.OfficeDate(row.ReceivedAtUtc), OperatorLabels.UnidentifiedReason(row.ReasonCode)),
            row.ReceivedAtUtc,
            null,
            $"/Unidentified/{row.Id:D}"));
        var blockedRows = blocked.Items.Select(item => new QueueRow(
            RowKind.BlockedIntake,
            item.Id,
            item.SourceFileName,
            "Blocked intake",
            OperatorLabels.EmailHandle(item.Subject, item.Sender) is var handle && item.Sender is not null ? handle : string.Empty,
            Title(OperatorLabels.OfficeDate(item.ReceivedAtUtc), OperatorLabels.IntakeFailure(item.FailureReason)),
            item.ReceivedAtUtc,
            null,
            $"/Received/{item.Id:D}"));
        return unidentified.Concat(blockedRows).ToArray();
    }

    private async Task<QuickDetail> LoadDetailAsync(ActionActor actor, QueueRow row, CancellationToken cancellationToken)
    {
        if (row.Kind != RowKind.Case)
        {
            return await LoadRecordDetailAsync(actor, row, cancellationToken);
        }

        var details = await _getCase.ExecuteAsync(new(row.Id, actor), cancellationToken)
            ?? throw new InvalidOperationException($"Case '{row.Id}' was listed but could not be read.");
        var completeness = details.Data?.Completeness.Values;
        var outstanding = details.Workflow.State == CaseLifecycleState.NotReady && completeness is not null
            ? OperatorLabels.CaseRequirements(!completeness.InstructionComplete, !completeness.ImagesComplete)
            : [];

        var facts = new List<KeyValuePair<string, string>>(3);
        var dueWork = details.Workflow.DueWork;
        if (dueWork?.DueBy is { } dueBy)
        {
            facts.Add(new("Due", OperatorLabels.OfficeDate(dueBy.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))));
        }
        else if (details.Summary.NextChaseAtUtc is { } nextChase)
        {
            facts.Add(new("Due", OperatorLabels.OfficeDate(nextChase)));
        }

        if (details.Workflow.AssignedEngineerId is { } engineerId)
        {
            var names = await ActorDisplayNames.ResolveStaffNamesAsync(_staffAccounts, [engineerId], cancellationToken);
            facts.Add(new("Engineer", ActorDisplayNames.Resolve(ActorKind.Staff, engineerId.ToString("D"), names)));
        }

        // Next action is the first outstanding requirement's resolve text,
        // else the due work's own state — never a sentence written here.
        if (outstanding.Count > 0)
        {
            facts.Add(new("Next action", outstanding[0].Resolve));
        }
        else if (dueWork is not null)
        {
            facts.Add(new("Next action", dueWork.NextChaseAtUtc is { } chase
                ? Title(OperatorLabels.ChaseState(dueWork.State), OperatorLabels.OfficeDate(chase))
                : OperatorLabels.ChaseState(dueWork.State)));
        }

        return new(
            RowKind.Case,
            OperatorLabels.SourceChannel(details.Summary.Origin),
            row.Title,
            row.OpenHref,
            "Open full Case",
            facts,
            details.Workflow.State,
            outstanding);
    }

    private async Task<QuickDetail> LoadRecordDetailAsync(ActionActor actor, QueueRow row, CancellationToken cancellationToken)
    {
        var facts = new List<KeyValuePair<string, string>>(5);
        switch (row.Kind)
        {
            case RowKind.Image:
                var images = await _imageIntakeQueries.ListImagesAsync(row.Id, cancellationToken);
                facts.Add(new("State", row.Chip));
                facts.Add(new("Registered", row.Meta));
                facts.Add(new("Images", images.Count.ToString()));
                if (row.Time is { } chase)
                {
                    facts.Add(new("Chase", chase));
                }
                return new(row.Kind, "Image-initiated Case", row.Title, row.OpenHref, "Open image record", facts);
            case RowKind.Triage:
                facts.Add(new("Registration", row.Title));
                facts.Add(new("State", row.Chip));
                facts.Add(new("Assigned to", row.Detail));
                facts.Add(new("Opened", row.Meta));
                return new(row.Kind, "Triage", row.Title, row.OpenHref, "Open Triage", facts);
            case RowKind.BlockedIntake:
                facts.Add(new("File", row.Title));
                if (row.Detail.Length > 0)
                {
                    facts.Add(new("E-mail", row.Detail));
                }
                facts.Add(new("Received", OperatorLabels.OfficeDate(row.ReceivedAtUtc)));
                facts.Add(new("Reason", row.Meta[(row.Meta.IndexOf(" · ", StringComparison.Ordinal) + 3)..]));
                return new(row.Kind, "Blocked intake", row.Title, row.OpenHref, "Open received item", facts);
            default:
                facts.Add(new("Kind", row.Title[(row.Title.IndexOf(" · ", StringComparison.Ordinal) + 3)..]));
                facts.Add(new("Handle", row.Detail));
                facts.Add(new("Received", OperatorLabels.OfficeDate(row.ReceivedAtUtc)));
                facts.Add(new("Reason", row.Meta[(row.Meta.IndexOf(" · ", StringComparison.Ordinal) + 3)..]));
                return new(row.Kind, "Unidentified", row.Title, row.OpenHref, "Open Unidentified", facts);
        }
    }

    private static QueueRow CaseRow(CaseSearchItem item) => new(
        RowKind.Case,
        item.CaseId,
        Title(item.Reference, item.Registration),
        OperatorLabels.CaseStage(item.State),
        Title(item.Claimant, item.Principal),
        Title(OperatorLabels.SourceChannel(item.Origin), OperatorLabels.OfficeDate(item.ReceivedAtUtc)),
        item.ReceivedAtUtc,
        item.NextChaseAtUtc is { } chase ? OperatorLabels.OfficeDate(chase) : null,
        $"/Cases/{item.CaseId:D}");

    /// <summary>"first · second", dropping whichever half is absent.</summary>
    private static string Title(string? first, string? second) =>
        string.Join(" · ", new[] { first, second }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Assignee(Guid? assigneeId) => assigneeId is null ? "Unassigned" : "Assigned";

    private static IReadOnlyList<string> PrincipalsOf(IEnumerable<CaseSearchItem> items) => items
        .Select(item => item.Principal)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The operator-meaningful handle for a queue row: the original filename
    /// for an image or document, or the subject and sender for an e-mail
    /// (formatted by the one shared rule, <see cref="OperatorLabels.EmailHandle"/>,
    /// that the Unidentified detail page also uses). Never a GUID or
    /// internal reference.
    /// </summary>
    public static string UnidentifiedHandle(UnidentifiedQueueRow row) => row.MediaKind switch
    {
        UnidentifiedMediaKind.Email => OperatorLabels.EmailHandle(row.EmailSubject, row.EmailSender),
        _ => row.FileName ?? "Not available"
    };
}
