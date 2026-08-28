using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;


namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public class IndexModel(IGetOperationsSnapshot getOperationsSnapshot) : StaffPageModel
{
    public IntakeQueueCounts Counts { get; private set; } = new(0, 0);

    public IReadOnlyList<CaseDueWork> DueWork { get; private set; } = [];

    public CaseStageCounts CaseStages { get; private set; } = new(0, 0, 0, 0);

    public CaseActivityCounts CaseActivity { get; private set; } = new(0, 0, 0, 0, 0);

    public MailActivityCounts MailActivity { get; private set; } = new(0, 0);

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var snapshot = await getOperationsSnapshot.ExecuteAsync(actor, cancellationToken);
        LoadedAtUtc = snapshot.AsOfUtc;
        Counts = snapshot.Intake;
        DueWork = snapshot.DueWork;
        CaseStages = snapshot.CaseStages;
        CaseActivity = snapshot.CaseActivity;
        MailActivity = snapshot.MailActivity;
        return Page();
    }
}
