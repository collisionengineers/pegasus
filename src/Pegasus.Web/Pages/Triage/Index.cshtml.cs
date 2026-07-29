using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Pages.Triage;

public sealed class IndexModel(ITriageQueries queries) : PageModel
{
    private readonly ITriageQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));

    public IReadOnlyList<TriageSummary> Items { get; private set; } = [];

    public TriageState? State { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? state, CancellationToken cancellationToken)
    {
        TriageState? parsedState = null;

        if (state is not null && !TryParseState(state, out parsedState))
        {
            return NotFound();
        }

        State = parsedState;
        Items = await _queries.ListAsync(State, cancellationToken);
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
}
