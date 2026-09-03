using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfigurationModel(GetWorkflowConfiguration getWorkflowConfiguration)
    : AdministrationPageModel
{
    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// Whether the Automation ingress exists in this deployment, so the
    /// administration rail lists the same areas here as on every sibling
    /// administration page.
    /// </summary>
    public bool AutomationComposed { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageWorkflowConfiguration);
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        Configuration = await getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
    }
}
