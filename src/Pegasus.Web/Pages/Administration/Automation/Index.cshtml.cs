using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration.Automation;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel : AdministrationPageModel
{
    public bool IngressComposed { get; private set; }

    public AutomationClientStatus? Status { get; private set; }

    public bool SendToAiComposed { get; private set; }

    public bool SendToAiEnabled { get; private set; }

    [BindProperty]
    public bool TargetEnabled { get; set; }

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSetEnabledAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        var registry = Registry();
        if (registry is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Automation is not part of this deployment.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid && registry is not null)
        {
            var status = await registry.SetEnabledAsync(
                TargetEnabled,
                actor,
                Reason,
                OperationKey,
                cancellationToken);
            TempData["AdministrationStatus"] = status.IsEnabled
                ? "The Automation client registration is enabled."
                : "The Automation client registration is disabled; new tokens are refused and in-flight tokens are rejected within seconds.";
            return RedirectToPage();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSetSendToAiEnabledAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            var control = HttpContext.RequestServices.GetRequiredService<ISendToAiControl>();
            var enabled = await control.SetEnabledAsync(
                TargetEnabled,
                actor,
                Reason,
                OperationKey,
                cancellationToken);
            TempData["AdministrationStatus"] = enabled
                ? "Sending to AI is enabled."
                : "Sending to AI is disabled; new hand-offs are refused immediately.";
            return RedirectToPage();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var registry = Registry();
        IngressComposed = registry is not null;
        Status = registry is null
            ? null
            : await registry.GetStatusAsync(actor, cancellationToken);
        SendToAiComposed = HttpContext.RequestServices.GetService<ISendCaseToAi>() is not null;
        SendToAiEnabled = await HttpContext.RequestServices
            .GetRequiredService<ISendToAiControl>()
            .IsEnabledAsync(cancellationToken);
    }

    private AutomationClientRegistry? Registry() =>
        HttpContext.RequestServices.GetService<AutomationClientRegistry>();
}
