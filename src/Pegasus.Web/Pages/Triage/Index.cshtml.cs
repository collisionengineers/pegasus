using System.Security.Claims;
using Pegasus.Core.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Triage;

/// <summary>
/// Queues: the work waiting before a case reaches an Engineer.
/// </summary>
/// <remarks>
/// The screen used to be called "Triage queue", which spent a reserved
/// business term on a page that is mostly not about Triage-type work. Three of
/// its four tabs are Case stages — Not ready, Review, Held — and Triage is the
/// fourth: a separate pre-case entity with its own lifecycle, which is exactly
/// why it needs a tab of its own rather than being folded in as a stage.
///
/// "Needs sorting" is deliberately absent: it means unmatched e-mail, not a
/// case stage, and it lives in the Inbox.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    IListTriage listTriage,
    ISearchCases searchCases,
    IDashboardQueries dashboardQueries,
    TimeProvider timeProvider) : PageModel
{
    private const int PageSize = 25;

    private readonly IListTriage _listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly ISearchCases _searchCases =
        searchCases ?? throw new ArgumentNullException(nameof(searchCases));
    private readonly IDashboardQueries _dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
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
    /// Which queue is open: <c>not_ready</c>, <c>review</c>, <c>held</c> or
    /// <c>triage</c>. Not ready is the default because it is the largest and
    /// the one with work in it.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "queue")]
    public string? QueueFilter { get; set; }

    public string Queue => string.IsNullOrWhiteSpace(QueueFilter)
        ? "not_ready"
        : QueueFilter.ToLowerInvariant();

    public bool ShowingTriage => Queue == "triage";

    public CaseStageCounts StageCounts { get; private set; } = new(0, 0, 0);

    public SearchCasesResult Cases { get; private set; } = new([], 1, PageSize, false, false);

    [BindProperty(SupportsGet = true, Name = "state")]
    public string? StateFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    public TriageListPage Results { get; private set; } = new([], 1, PageSize, 0);

    public TriageState? State { get; private set; }

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

        if (Queue is not ("not_ready" or "review" or "held" or "triage"))
        {
            return NotFound();
        }

        State = parsedState;
        StateFilter = parsedState is null ? null : StateCode(parsedState.Value);
        CurrentPage = Math.Max(1, CurrentPage);

        // Every tab carries its count, whichever one is open: an operator
        // decides where to go by what is waiting, not by opening each in turn.
        StageCounts = await _dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        Results = await _listTriage.ExecuteAsync(
            new(actor, State, CurrentPage, PageSize),
            cancellationToken);

        if (!ShowingTriage)
        {
            Cases = await _searchCases.ExecuteAsync(
                new(
                    actor,
                    new(State: Queue switch
                    {
                        "review" => CaseLifecycleState.Review,
                        "held" => CaseLifecycleState.Held,
                        _ => CaseLifecycleState.NotReady
                    }),
                    CurrentPage,
                    PageSize),
                cancellationToken);
        }

        LoadedAtUtc = _timeProvider.GetUtcNow();
        return Page();
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
}
