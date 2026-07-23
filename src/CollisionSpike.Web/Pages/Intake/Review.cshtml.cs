using CollisionSpike.Core.Intake.Qdos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CollisionSpike.Web.Pages.Intake;

public sealed class ReviewModel(IQdosIntakeQueries queries) : PageModel
{
    public QdosIntakeRecord Receipt { get; private set; } = null!;

    public bool IsDuplicate { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate = false,
        CancellationToken cancellationToken = default)
    {
        var receipt = await queries.GetAsync(id, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        Receipt = receipt;
        IsDuplicate = duplicate;
        return Page();
    }

    public static string DecisionLabel(QdosIntakeDecision decision) => decision switch
    {
        QdosIntakeDecision.ConfirmedQdos => "Confirmed QDOS",
        QdosIntakeDecision.NeedsSorting => "Needs sorting",
        QdosIntakeDecision.OcrRequired => "Document text required",
        QdosIntakeDecision.TechnicalFailure => "Technical failure",
        _ => "Unsupported"
    };
}
