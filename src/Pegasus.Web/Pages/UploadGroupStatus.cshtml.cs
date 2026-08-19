using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadGroupStatusModel(
    IIntakeSubmissionGroupStore groups,
    IQueuedIntakeStatusQueries statuses) : PageModel
{
    public IntakeSubmissionGroup Group { get; private set; } = null!;
    public IReadOnlyDictionary<Guid, QueuedIntakeStatus?> Statuses { get; private set; } =
        new Dictionary<Guid, QueuedIntakeStatus?>();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        Group = group;
        var statusMap = new Dictionary<Guid, QueuedIntakeStatus?>();
        foreach (var member in group.Members)
        {
            statusMap[member.StagedReceiptId] = await statuses.GetAsync(
                member.StagedReceiptId,
                cancellationToken);
        }

        Statuses = statusMap;
        return Page();
    }
}
