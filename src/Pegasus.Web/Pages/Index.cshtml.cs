using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;


namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public class IndexModel(IGetOperationsSnapshot getOperationsSnapshot) : PageModel
{
    public IntakeQueueCounts Counts { get; private set; } = new(0, 0);

    public int TriageCount { get; private set; }

    public IReadOnlyList<CaseDueWork> DueWork { get; private set; } = [];

    public StagedArtifactOperationsSnapshot StagedArtifacts { get; private set; } =
        new([]);

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        var snapshot = await getOperationsSnapshot.ExecuteAsync(actor, cancellationToken);
        LoadedAtUtc = snapshot.AsOfUtc;
        Counts = snapshot.Intake;
        TriageCount = snapshot.TriageCount;
        DueWork = snapshot.DueWork;
        StagedArtifacts = snapshot.StagedArtifacts;
        return Page();
    }
}
