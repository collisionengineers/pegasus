using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration.Automation;

/// <summary>
/// The Automation &amp; AI administration area: the
/// Automation panel — its state, the registered client, the ledger's active
/// and failed job counts and the kill switch — and the AI settings panel.
/// </summary>
/// <remarks>
/// The Automation client registration is gated composition, so when this
/// deployment does not carry it, the direct route reports that Automation and
/// AI configuration are unavailable. The AI settings panel has one Save and
/// drives the one Core operation behind it, the Send to AI switch, which
/// writes its own attributed history.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel : AdministrationPageModel
{
    /// <summary>
    /// The one registered Automation client (ADR-0011), and the page's test
    /// for whether the automation ingress is composed at all: null means the
    /// registry is absent from this deployment.
    /// </summary>
    public AutomationClientStatus? Status { get; private set; }

    /// <summary>
    /// The AI job ledger's live counters (ADR-0035), read only where the
    /// Automation panel renders.
    /// </summary>
    public AiJobCounts JobCounts { get; private set; } = new(0, 0);

    public bool SendToAiEnabledNow { get; private set; }

    public bool IsComposed => Status is not null;

    [BindProperty]
    public bool TargetEnabled { get; set; }

    [BindProperty]
    [StringLength(1000)]
    public string? Reason { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    /// <summary>The AI settings panel's enabled checkbox.</summary>
    [BindProperty]
    public bool SendToAiEnabled { get; set; }

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

    /// <summary>The automation kill switch, reached through the reason dialog.</summary>
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
            ModelState.AddModelError(string.Empty, "Automation is not available.");
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
                ? "Automation started."
                : "Automation stopped.";
            return RedirectToPage();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    /// <summary>
    /// The AI settings panel's one Save: the Send to AI switch, written only
    /// when the checkbox differs from the stored state, so a save that
    /// changes nothing writes no switch history.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAiSettingsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (Registry() is null)
        {
            ModelState.AddModelError(string.Empty, "Automation is not available.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            var control = HttpContext.RequestServices.GetRequiredService<ISendToAiControl>();
            if (await control.IsEnabledAsync(cancellationToken) != SendToAiEnabled)
            {
                await control.SetEnabledAsync(
                    SendToAiEnabled,
                    actor,
                    Reason,
                    OperationKey,
                    cancellationToken);
            }

            TempData["AdministrationStatus"] = "AI settings saved.";
            return RedirectToPage();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var registry = Registry();
        Status = registry is null
            ? null
            : await registry.GetStatusAsync(actor, cancellationToken);
        if (registry is not null)
        {
            JobCounts = await HttpContext.RequestServices
                .GetRequiredService<IAiJobQueries>()
                .GetCountsAsync(cancellationToken);
            SendToAiEnabledNow = await HttpContext.RequestServices
                .GetRequiredService<ISendToAiControl>()
                .IsEnabledAsync(cancellationToken);
        }

        // The kill-switch form does not post this checkbox. Seed it from the
        // stored state; ModelState still wins for a redisplayed AI settings form.
        SendToAiEnabled = SendToAiEnabledNow;
    }

    private AutomationClientRegistry? Registry() =>
        HttpContext.RequestServices.GetService<AutomationClientRegistry>();
}
