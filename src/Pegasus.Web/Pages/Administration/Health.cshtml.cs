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

        // The metrics read opens its own context, so it runs beside the snapshot.
        var snapshot = getServiceHealth.ExecuteAsync(actor, cancellationToken);
        var metrics = getMetrics.ExecuteAsync(actor, timeProvider.GetUtcNow(), cancellationToken);
        await Task.WhenAll(snapshot, metrics);
        Snapshot = await snapshot;
        Metrics = await metrics;
        return Page();
    }
}
