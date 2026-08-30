using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration.Automation;

/// <summary>
/// The Automation &amp; AI administration area (EPIC-011 §1.12): the
/// Automation panel — its state, the registered client, the ledger's active
/// and failed job counts and the kill switch — and the AI settings panel.
/// </summary>
/// <remarks>
/// The Automation client registration is gated composition, so when this
/// deployment does not carry it the whole panel is absent rather than
/// explained; the same holds for Send to AI. The AI settings panel has one
/// Save, as the design authority specifies, and it drives the three Core
/// operations behind it — connector bounds, channel token, and the outbound
/// switch — each of which still writes its own attributed history.
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

    /// <summary>
    /// The connector's recorded settings, and the page's one test for whether
    /// Send to AI is composed at all: <c>AddPegasusSendToAi</c> registers this
    /// store and <see cref="ISendCaseToAi"/> together, so a null here means the
    /// whole capability is absent from this deployment.
    /// </summary>
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
    /// The AI settings panel's one Save: connector address and timeout, the
    /// channel token when a replacement was entered, and the outbound switch
    /// when the checkbox differs from the stored state — so a save that
    /// changes nothing about the switch writes no switch history.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAiSettingsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        var store = ConnectorStore();
        if (store is null)
        {
            ModelState.AddModelError(string.Empty, "Sending to AI is not available.");
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
                "Enter the connector address exactly as supplied, without a path or query.");
        }
        var newChannelToken = string.IsNullOrWhiteSpace(NewChannelToken) ? null : NewChannelToken;
        if (newChannelToken is not null && !AiChannelConnectorRules.IsValidToken(newChannelToken))
        {
            ModelState.AddModelError(
                nameof(NewChannelToken),
                "The channel token must be at least 32 characters.");
        }

        if (ModelState.IsValid && store is not null)
        {
            var connector = await store.GetAsync(cancellationToken);
            if (!string.Equals(
                    connector.ChannelBaseUrl,
                    channelAddress,
                    StringComparison.Ordinal)
                || connector.TimeoutSeconds != ChannelTimeoutSeconds)
            {
                await store.UpdateAsync(
                    new(actor, Reason, OperationKey, channelAddress, ChannelTimeoutSeconds),
                    cancellationToken);
            }
            if (newChannelToken is not null)
            {
                await store.RotateTokenAsync(
                    new(actor, Reason, OperationKey, newChannelToken),
                    cancellationToken);
            }

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

        NewChannelToken = null;
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    /// <summary>
    /// Removes the administration-entered channel token, returning the
    /// connector to the configured one. Reached through the reason dialog.
    /// </summary>
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
            ModelState.AddModelError(string.Empty, "Sending to AI is not available.");
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
            TempData["AdministrationStatus"] = "Channel token removed.";
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
        }

        SendToAiEnabledNow = await HttpContext.RequestServices
            .GetRequiredService<ISendToAiControl>()
            .IsEnabledAsync(cancellationToken);
        // Reason-dialog forms do not post this checkbox. Seed it from the
        // stored state; ModelState still wins for a redisplayed AI settings form.
        SendToAiEnabled = SendToAiEnabledNow;
        ConnectorSettings = ConnectorStore() is { } connectorStore
            ? await connectorStore.GetAsync(cancellationToken)
            : null;
    }

    private IAiChannelConnectorStore? ConnectorStore() =>
        HttpContext.RequestServices.GetService<IAiChannelConnectorStore>();

    private AutomationClientRegistry? Registry() =>
        HttpContext.RequestServices.GetService<AutomationClientRegistry>();
}
