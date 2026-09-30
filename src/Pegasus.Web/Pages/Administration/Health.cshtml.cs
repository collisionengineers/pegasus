using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class HealthModel(GetServiceHealth getServiceHealth) : AdministrationPageModel
{
    public ServiceHealthSnapshot? Snapshot { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        // The administration metrics are drawn on the Logs page, not here.
        Snapshot = await getServiceHealth.ExecuteAsync(actor, cancellationToken);
        return Page();
    }
}
