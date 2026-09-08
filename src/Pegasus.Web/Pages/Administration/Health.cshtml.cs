using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class HealthModel(
    GetServiceHealth getServiceHealth,
    GetAdministrationHealthMetrics getMetrics,
    TimeProvider timeProvider) : AdministrationPageModel
{
    public ServiceHealthSnapshot? Snapshot { get; private set; }
    public AdministrationHealthMetrics? Metrics { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Snapshot = await getServiceHealth.ExecuteAsync(actor, cancellationToken);
        Metrics = await getMetrics.ExecuteAsync(actor, timeProvider.GetUtcNow(), cancellationToken);
        return Page();
    }
}
