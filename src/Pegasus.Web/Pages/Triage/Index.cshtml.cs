using System.Security.Claims;
using Pegasus.Core.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Triage;

/// <summary>
/// Queues: the work waiting before a case reaches an Engineer.
/// </summary>
/// <remarks>
/// The screen used to be called "Triage queue", which spent a reserved
/// business term on a page that is mostly not about Triage-type work. Its
/// tabs are Not ready, Review, Held, and Triage — a separate pre-case entity
/// with its own lifecycle, which is exactly why it needs a tab of its own
/// rather than being folded in as a stage.
///
/// Unidentified joined as a fifth tab in INTK-009, replacing the standalone
/// <c>/Unidentified</c> nav page: it is unresolved retained material, not a
/// case stage, but it is queue work the same way the other four tabs are.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    IListTriage listTriage,
    ISearchCases searchCases,
    IDashboardQueries dashboardQueries,
    IUnidentifiedStore unidentifiedStore,
    IImageIntakeQueries imageIntakeQueries,
    TimeProvider timeProvider) : PageModel
{
    private const int PageSize = 25;

    private readonly IListTriage _listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly ISearchCases _searchCases =
        searchCases ?? throw new ArgumentNullException(nameof(searchCases));
    private readonly IDashboardQueries _dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly IUnidentifiedStore _unidentifiedStore =
        unidentifiedStore ?? throw new ArgumentNullException(nameof(unidentifiedStore));
    private readonly IImageIntakeQueries _imageIntakeQueries =
        imageIntakeQueries ?? throw new ArgumentNullException(nameof(imageIntakeQueries));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// When these counts and rows were last read, so the screen can say how
    /// current it is. FRD-12 requires every count and query to expose its last
    /// successful update time; it is set only after the queries return, so a
    /// failed load never claims to be fresh.
    /// </summary>
    public DateTimeOffset? LoadedAtUtc { get; private set; }

    /// <summary>
    /// Which queue is open: <c>not_ready</c>, <c>review</c>, <c>held</c>,
    /// <c>triage</c> or <c>unidentified</c>. Not ready is the default because
    /// it is the largest and the one with work in it.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "queue")]
    public string? QueueFilter { get; set; }

    public string Queue => string.IsNullOrWhiteSpace(QueueFilter)
        ? "not_ready"
        : QueueFilter.ToLowerInvariant();

    public bool ShowingTriage => Queue == "triage";

    public bool ShowingUnidentified => Queue == "unidentified";

    public bool ShowingNotReady => Queue == "not_ready";

    public CaseStageCounts StageCounts { get; private set; } = new(0, 0, 0);

    /// <summary>Open Unidentified items, so the tab always carries its count.</summary>
    public int UnidentifiedCount { get; private set; }

    public SearchCasesResult Cases { get; private set; } = new([], 1, PageSize, false, false);

    [BindProperty(SupportsGet = true, Name = "state")]
    public string? StateFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    public TriageListPage Results { get; private set; } = new([], 1, PageSize, 0);

    public TriageState? State { get; private set; }

    /// <summary>
    /// The Unidentified tab's media-kind filter: <c>all</c>, <c>images</c> or
    /// <c>emails</c>. Only consulted when <see cref="ShowingUnidentified"/>.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "kind")]
    public string? KindFilter { get; set; }

    public string UnidentifiedKind => string.IsNullOrWhiteSpace(KindFilter)
        ? "all"
        : KindFilter.ToLowerInvariant();

    public IReadOnlyList<UnidentifiedQueueRow> UnidentifiedRows { get; private set; } = [];

    /// <summary>
    /// The Not ready tab's case-origin filter: <c>all</c>, <c>instruction</c>
    /// or <c>image</c> — the two case origins INTK-008 settled. Only
    /// consulted when <see cref="ShowingNotReady"/>.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "origin")]
    public string? OriginFilter { get; set; }

    public string NotReadyOrigin => string.IsNullOrWhiteSpace(OriginFilter)
        ? "all"
        : OriginFilter.ToLowerInvariant();

    /// <summary>
    /// Image-initiated Cases still awaiting instruction, for the Not ready
    /// tab's Image-initiated filter. Not paginated: this is the same bounded
    /// exception-queue trade-off the Unidentified tab already makes, not a
    /// second convention.
    /// </summary>
    public IReadOnlyList<ImageIntakeSummary> ImageInitiatedRows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        TriageState? parsedState = null;
        if (!string.IsNullOrWhiteSpace(StateFilter)
            && !TryParseState(StateFilter, out parsedState))
        {
            return NotFound();
        }

        if (CurrentPage > 10_000)
        {
            return NotFound();
        }

        if (Queue is not ("not_ready" or "review" or "held" or "triage" or "unidentified"))
        {
            return NotFound();
        }

        if (KindFilter is not (null or "" or "all" or "images" or "emails"))
        {
            return NotFound();
        }

        if (OriginFilter is not (null or "" or "all" or "instruction" or "image"))
        {
            return NotFound();
        }

        State = parsedState;
        StateFilter = parsedState is null ? null : StateCode(parsedState.Value);
        CurrentPage = Math.Max(1, CurrentPage);

        // Every tab carries its count, whichever one is open: an operator
        // decides where to go by what is waiting, not by opening each in
        // turn. Both queries use their own DbContext, so they run
        // concurrently rather than paying the sum of their latencies.
        var stageCountsTask = _dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var openUnidentifiedTask = _unidentifiedStore.ListQueueAsync(null, cancellationToken);
        await Task.WhenAll(stageCountsTask, openUnidentifiedTask);
        StageCounts = stageCountsTask.Result;
        var openUnidentifiedRows = openUnidentifiedTask.Result;
        UnidentifiedCount = openUnidentifiedRows.Count;

        if (ShowingTriage)
        {
            Results = await _listTriage.ExecuteAsync(
                new(actor, State, CurrentPage, PageSize),
                cancellationToken);
        }
        else if (ShowingUnidentified)
        {
            // Filters the count query's own result rather than re-querying:
            // the join behind ListQueueAsync is the same whichever kind is
            // asked for, so there is nothing a second call would learn.
            UnidentifiedMediaKind? mediaKind = UnidentifiedKind switch
            {
                "images" => UnidentifiedMediaKind.Image,
                "emails" => UnidentifiedMediaKind.Email,
                _ => null
            };
            UnidentifiedRows = mediaKind is null
                ? openUnidentifiedRows
                : openUnidentifiedRows.Where(row => row.MediaKind == mediaKind.Value).ToArray();
        }
        else if (ShowingNotReady)
        {
            await LoadNotReadyAsync(actor, cancellationToken);
        }
        else
        {
            Cases = await _searchCases.ExecuteAsync(
                new(
                    actor,
                    new(State: Queue == "held" ? CaseLifecycleState.Held : CaseLifecycleState.Review),
                    CurrentPage,
                    PageSize),
                cancellationToken);
        }

        LoadedAtUtc = _timeProvider.GetUtcNow();
        return Page();
    }

    /// <summary>
    /// The Not ready tab's two independent origin queries — a formal Case
    /// search and an Image-initiated Case list — run concurrently and each
    /// is skipped outright when the origin filter excludes it, rather than
    /// fetched and then discarded.
    /// </summary>
    private async Task LoadNotReadyAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Task<SearchCasesResult>? casesTask = NotReadyOrigin != "image"
            ? _searchCases.ExecuteAsync(
                new(actor, new(State: CaseLifecycleState.NotReady), CurrentPage, PageSize),
                cancellationToken)
            : null;
        Task<IReadOnlyList<ImageIntakeSummary>>? imageInitiatedTask = NotReadyOrigin != "instruction"
            ? _imageIntakeQueries.ListAsync(false, cancellationToken)
            : null;

        var pending = new Task?[] { casesTask, imageInitiatedTask }
            .Where(task => task is not null)
            .Select(task => task!)
            .ToArray();
        await Task.WhenAll(pending);

        if (casesTask is not null)
        {
            Cases = casesTask.Result;
        }

        if (imageInitiatedTask is not null)
        {
            ImageInitiatedRows = imageInitiatedTask.Result
                .Where(item => item.State == ImageInitiatedCaseState.AwaitingInstruction)
                .ToArray();
        }
    }

    public static bool TryParseState(string value, out TriageState? state)
    {
        state = value.ToLowerInvariant() switch
        {
            "open" => TriageState.Open,
            "awaiting_information" => TriageState.AwaitingInformation,
            "finding_recorded" => TriageState.FindingRecorded,
            "completed" => TriageState.Completed,
            "cancelled" => TriageState.Cancelled,
            _ => null
        };

        return state is not null;
    }

    public static string StateLabel(TriageState state) => state switch
    {
        TriageState.Open => "Open",
        TriageState.AwaitingInformation => "Awaiting information",
        TriageState.FindingRecorded => "Finding recorded",
        TriageState.Completed => "Completed",
        TriageState.Cancelled => "Cancelled",
        _ => throw new InvalidOperationException($"Unknown triage state '{(int)state}'.")
    };

    private static string StateCode(TriageState state) => state switch
    {
        TriageState.Open => "open",
        TriageState.AwaitingInformation => "awaiting_information",
        TriageState.FindingRecorded => "finding_recorded",
        TriageState.Completed => "completed",
        TriageState.Cancelled => "cancelled",
        _ => throw new InvalidOperationException($"Unknown triage state '{(int)state}'.")
    };

    public static string UnidentifiedKindLabel(UnidentifiedQueueRow row) =>
        OperatorLabels.UnidentifiedMediaKind(row.MediaKind);

    public static string UnidentifiedReasonLabel(UnidentifiedQueueRow row) =>
        OperatorLabels.UnidentifiedReason(row.ReasonCode);

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
