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

    public AiChannelConnectorSettings? ConnectorSettings { get; private set; }

    [BindProperty]
    public bool TargetEnabled { get; set; }

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    [BindProperty]
    [StringLength(200)]
    public string? ChannelAddress { get; set; }

    [BindProperty]
    [Range(AiChannelConnectorRules.MinimumTimeoutSeconds, AiChannelConnectorRules.MaximumTimeoutSeconds)]
    public double? ChannelTimeoutSeconds { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? NewChannelToken { get; set; }

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

    public async Task<IActionResult> OnPostUpdateConnectorAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        var store = ConnectorStore();
        if (store is null)
        {
            ModelState.AddModelError(string.Empty, "Send to AI is not part of this deployment.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        var channelAddress = string.IsNullOrWhiteSpace(ChannelAddress) ? null : ChannelAddress.Trim();
        if (channelAddress is not null
            && !AiChannelConnectorRules.TryParseBaseUrl(channelAddress, out _))
        {
            ModelState.AddModelError(
                nameof(ChannelAddress),
                "The channel address must be a loopback http origin without path or query.");
        }

        if (ModelState.IsValid && store is not null)
        {
            await store.UpdateAsync(
                new(actor, Reason, OperationKey, channelAddress, ChannelTimeoutSeconds),
                cancellationToken);
            TempData["AdministrationStatus"] =
                "The connector settings are saved and apply from the next hand-off.";
            return RedirectToPage();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRotateChannelTokenAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        var store = ConnectorStore();
        if (store is null)
        {
            ModelState.AddModelError(string.Empty, "Send to AI is not part of this deployment.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        if (!AiChannelConnectorRules.IsValidToken(NewChannelToken))
        {
            ModelState.AddModelError(
                nameof(NewChannelToken),
                "The channel token must be at least 32 characters.");
        }

        if (ModelState.IsValid && store is not null)
        {
            await store.RotateTokenAsync(
                new(actor, Reason, OperationKey, NewChannelToken),
                cancellationToken);
            TempData["AdministrationStatus"] =
                "The channel token is replaced and applies from the next hand-off; it cannot be viewed again.";
            return RedirectToPage();
        }

        NewChannelToken = null;
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostClearChannelTokenAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        var store = ConnectorStore();
        if (store is null)
        {
            ModelState.AddModelError(string.Empty, "Send to AI is not part of this deployment.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid && store is not null)
        {
            await store.RotateTokenAsync(
                new(actor, Reason, OperationKey, NewToken: null),
                cancellationToken);
            TempData["AdministrationStatus"] =
                "The administration-entered token is removed; the deployment configuration token applies from the next hand-off.";
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
        ConnectorSettings = ConnectorStore() is { } connectorStore
            ? await connectorStore.GetAsync(cancellationToken)
            : null;
    }

    private IAiChannelConnectorStore? ConnectorStore() =>
        HttpContext.RequestServices.GetService<IAiChannelConnectorStore>();

    private AutomationClientRegistry? Registry() =>
        HttpContext.RequestServices.GetService<AutomationClientRegistry>();
}
