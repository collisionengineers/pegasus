using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// The Glass's window from the press until the launch answers into it: the
/// Case page opens the window here, so it is never blank, and then posts the
/// launch into it (v52 G, R1234). It starts nothing and reads only the Case's
/// reference and registration. A host that does not reach Glass's, and a Case
/// that does not exist, are not found.
/// </summary>
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StartingModel(
    GlassRepairEstimateAvailability glassAvailability,
    IGetCaseSectionFrame caseFrames) : StaffPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    public string CaseLine { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (!glassAvailability.Enabled
            || !TryGetActor(out var actor)
            || await GlassWaitingView.CaseLineAsync(caseFrames, actor, caseId, cancellationToken) is not { } line)
        {
            return NotFound();
        }

        CaseLine = line;
        return Page();
    }
}
