using CollisionSpike.Core.Intake.Qdos;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CollisionSpike.Web.Pages.Intake;

public sealed class QueueModel(IQdosIntakeQueries queries) : PageModel
{
    public IReadOnlyList<QdosIntakeSummary> Items { get; private set; } = [];

    public QdosIntakeDecision? Decision { get; private set; }

    public async Task OnGetAsync(string? decision, CancellationToken cancellationToken)
    {
        if (Enum.TryParse<QdosIntakeDecision>(decision, ignoreCase: true, out var parsed))
        {
            Decision = parsed;
        }

        Items = await queries.ListAsync(Decision, cancellationToken);
    }
}
