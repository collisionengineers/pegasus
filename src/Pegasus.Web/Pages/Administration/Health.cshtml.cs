using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed partial class HealthModel(
    GetServiceHealth getServiceHealth,
    ILogger<HealthModel> logger) : AdministrationPageModel
{
    /// <summary>
    /// The snapshot; <see langword="null"/> when any of its reads failed, which
    /// the page renders as its unavailable notice, never an empty table
    /// (FRD-17 § Service health; R414).
    /// </summary>
    public ServiceHealthSnapshot? Snapshot { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            Snapshot = await getServiceHealth.ExecuteAsync(actor, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogSnapshotFailed(logger, exception);
        }

        return Page();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service health could not be read.")]
    private static partial void LogSnapshotFailed(ILogger logger, Exception exception);
}
