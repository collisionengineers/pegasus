using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Pages.Triage;

public sealed class DetailModel(ITriageQueries queries) : PageModel
{
    private readonly ITriageQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));

    public TriageDetail Triage { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var triage = await _queries.GetAsync(id, cancellationToken);
        if (triage is null)
        {
            return NotFound();
        }

        Triage = triage;
        return Page();
    }

    public static string StateLabel(TriageState state) => IndexModel.StateLabel(state);

    public static string SourceChannelLabel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        IntakeSourceChannel.Mailbox => "Approved inbox",
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };
}
