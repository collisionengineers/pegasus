using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.Automation;

/// <summary>
/// The Automation &amp; AI administration page (R467): the Automation panel —
/// its state, the registered client and the kill switch — the AI settings
/// panel, and the AI jobs list with its active and failed counts and Stop.
/// </summary>
/// <remarks>
/// The Automation client registration is gated composition, so where this
/// deployment does not carry it the page states that Automation and AI
/// configuration are unavailable above the AI jobs list. The AI settings
/// panel has one Save and drives the one Core operation behind it, the Send to
/// AI switch, which writes its own attributed history.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed partial class IndexModel(
    GetAdministrationAiJobs getJobs,
    CancelAiJob cancelJob,
    ISendToAiControl sendToAi,
    ILogger<IndexModel> logger) : AdministrationPageModel
{
    /// <summary>
    /// The one registered Automation client (ADR-0011), and the page's test
    /// for whether the automation ingress is composed at all: null means the
    /// registry is absent from this deployment.
    /// </summary>
    public AutomationClientStatus? Status { get; private set; }

    /// <summary>
    /// One page of the AI job ledger (ADR-0035), with its live counters and
    /// the Send to AI switch state.
    /// </summary>
    public AdministrationAiJobPage Jobs { get; private set; } = new([], new(0, 0), false, false);

    public int CurrentPage { get; private set; } = 1;

    [BindProperty]
    public bool TargetEnabled { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    /// <summary>The AI settings panel's enabled checkbox.</summary>
    [BindProperty]
    public bool SendToAiEnabled { get; set; }

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "pageNumber")] int pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        CurrentPage = pageNumber == 0 ? 1 : pageNumber;
        try
        {
            Jobs = await getJobs.ExecuteAsync(actor, CurrentPage, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            return NotFound();
        }

        await LoadAutomationAsync(actor, cancellationToken);
        return Page();
    }

    /// <summary>The automation kill switch, posted on the click.</summary>
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
            ModelState.AddModelError(string.Empty, OperatorLabels.AutomationAdmin.NotAvailable);
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.AutomationAdmin.Expired);
        }

        if (ModelState.IsValid && registry is not null)
        {
            try
            {
                var status = await registry.SetEnabledAsync(
                    TargetEnabled,
                    actor,
                    reason: null,
                    OperationKey,
                    cancellationToken);
                StatusMessage = status.IsEnabled
                    ? OperatorLabels.AutomationAdmin.StartedNotice
                    : OperatorLabels.AutomationAdmin.StoppedNotice;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The switch is one transaction, so a failure changed nothing.
                // The page is read again on a fresh request, never from this
                // request's state.
                LogSwitchFailed(logger, TargetEnabled, exception);
                ErrorMessage = OperatorLabels.AutomationAdmin.NotAccepted;
            }

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
            ModelState.AddModelError(string.Empty, OperatorLabels.AutomationAdmin.NotAvailable);
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.AutomationAdmin.Expired);
        }

        if (ModelState.IsValid)
        {
            if (await sendToAi.IsEnabledAsync(cancellationToken) != SendToAiEnabled)
            {
                await sendToAi.SetEnabledAsync(
                    SendToAiEnabled,
                    actor,
                    reason: null,
                    OperationKey,
                    cancellationToken);
            }

            StatusMessage = OperatorLabels.AutomationAdmin.Saved;
            return RedirectToPage();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    /// <summary>One AI job's Stop, posted on the click, returning to its page.</summary>
    public async Task<IActionResult> OnPostStopAsync(
        Guid jobId,
        long expectedVersion,
        string operationKey,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var returnPage = Math.Max(pageNumber, 1);
        if (jobId == Guid.Empty || !IsOperationKeyValid(operationKey))
        {
            ErrorMessage = OperatorLabels.AiJobs.NotStopped;
            return RedirectToPage(new { pageNumber = returnPage });
        }

        try
        {
            await cancelJob.ExecuteAsync(new(jobId, expectedVersion, actor, operationKey, Reason: null), cancellationToken);
            StatusMessage = OperatorLabels.AiJobs.Stopped;
        }
        catch (InvalidOperationException)
        {
            // A newer version, or a job already settled: the job changed.
            ErrorMessage = OperatorLabels.AiJobs.ChangedBeforeStop;
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            // Core refused the request itself, or there is no such job.
            ErrorMessage = OperatorLabels.AiJobs.NotStopped;
        }

        return RedirectToPage(new { pageNumber = returnPage });
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Jobs = await getJobs.ExecuteAsync(actor, CurrentPage, cancellationToken);
        await LoadAutomationAsync(actor, cancellationToken);
    }

    private async Task LoadAutomationAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var registry = Registry();
        Status = registry is null
            ? null
            : await registry.GetStatusAsync(actor, cancellationToken);

        // The kill-switch and Stop forms do not post this checkbox. Seed it
        // from the stored state; ModelState still wins for a redisplayed AI
        // settings form.
        SendToAiEnabled = Jobs.SendToAiSwitchEnabled;
    }

    private AutomationClientRegistry? Registry() =>
        HttpContext.RequestServices.GetService<AutomationClientRegistry>();

    [LoggerMessage(
        EventId = 1571,
        Level = LogLevel.Error,
        Message = "The Automation switch to enabled={Enabled} failed and changed nothing.")]
    private static partial void LogSwitchFailed(ILogger logger, bool enabled, Exception exception);
}
