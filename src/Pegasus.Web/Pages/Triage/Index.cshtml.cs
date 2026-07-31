using System.Security.Claims;
using Pegasus.Core.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Pages.Triage;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(IListTriage listTriage) : PageModel
{
    private const int PageSize = 25;

    private readonly IListTriage _listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));

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

        State = parsedState;
        StateFilter = parsedState is null ? null : StateCode(parsedState.Value);
        CurrentPage = Math.Max(1, CurrentPage);
        Results = await _listTriage.ExecuteAsync(
            new(actor, State, CurrentPage, PageSize),
            cancellationToken);
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
