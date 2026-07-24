using CollisionSpike.Core.Intake;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CollisionSpike.Web.Pages.Intake;

public sealed class QueueModel(IIntakeReceiptQueries queries) : PageModel
{
    public IReadOnlyList<IntakeReceiptSummary> Items { get; private set; } = [];

    public IntakeDecision? Decision { get; private set; }

    public async Task OnGetAsync(string? decision, CancellationToken cancellationToken)
    {
        Decision = decision switch
        {
            "draft_ready" => IntakeDecision.DraftReady,
            "needs_sorting" => IntakeDecision.NeedsSorting,
            "unsupported" => IntakeDecision.Unsupported,
            "ocr_required" => IntakeDecision.OcrRequired,
            "technical_failure" => IntakeDecision.TechnicalFailure,
            _ => null
        };

        Items = await queries.ListAsync(Decision, cancellationToken);
    }
}
