using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfigurationModel(
    GetWorkflowConfiguration getWorkflowConfiguration,
    UpdateWorkflowConfiguration updateWorkflowConfiguration,
    LabourRateCardAdministration rateCards,
    IEditScopeLeases editScopes,
    IStaffAccountQueries staffAccounts) : AdministrationPageModel
{
    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;
    public IReadOnlyList<LabourRateCard> RateCards { get; private set; } = [];
    public bool AutomationComposed { get; private set; }
    [BindProperty] public Guid EditingId { get; set; }
    [BindProperty] public long ExpectedVersion { get; set; }
    [BindProperty] public string? LeaseToken { get; set; } = string.Empty;
    [BindProperty] public string OperationKey { get; set; } = NewOperationKey();
    [BindProperty] public bool RequireInstructions { get; set; }
    [BindProperty] public bool RequireImages { get; set; }
    [BindProperty] public int ChaseIntervalDays { get; set; } = 7;
    [BindProperty] public string? CardName { get; set; } = string.Empty;
    [BindProperty] public decimal HourlyRate { get; set; }
    [BindProperty] public bool Enabled { get; set; } = true;
    [BindProperty] public string? Reason { get; set; } = string.Empty;
    public bool IsEditing => EditingId != Guid.Empty;
    private EditScopeKind ScopeKind => EditingId == GetWorkflowConfiguration.RecordId
        ? EditScopeKind.NamedConfiguration : EditScopeKind.LabourRateCard;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(Guid recordId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        EditingId = recordId;
        if (recordId == GetWorkflowConfiguration.RecordId)
        {
            ExpectedVersion = Configuration.PolicyVersion;
            RequireInstructions = Configuration.RequireInstructions;
            RequireImages = Configuration.RequireImages;
            ChaseIntervalDays = Configuration.ChaseIntervalDays;
        }
        else
        {
            var card = RateCards.SingleOrDefault(x => x.Id == recordId);
            if (card is null) return NotFound();
            ExpectedVersion = card.Version;
            CardName = card.Name;
            HourlyRate = card.HourlyRate;
            Enabled = card.Enabled;
        }
        try
        {
            LeaseToken = (await editScopes.ClaimAsync(new(ScopeKind, recordId, ExpectedVersion, actor,
                NewOperationKey()), cancellationToken)).Token;
            OperationKey = NewOperationKey();
        }
        catch (EditScopeConflictException)
        {
            var active = await editScopes.GetActiveAsync(ScopeKind, recordId, actor, cancellationToken);
            IReadOnlyDictionary<Guid, string> names = active is { HolderKind: ActorKind.Staff }
                && Guid.TryParse(active.Holder, out var holderId)
                ? await ActorDisplayNames.ResolveStaffNamesAsync(
                    staffAccounts, [holderId], cancellationToken)
                : new Dictionary<Guid, string>();
            EditingId = Guid.Empty;
            ModelState.AddModelError(string.Empty, active is null
                ? "The record is being edited. Reload to try again."
                : $"{ActorDisplayNames.Resolve(active.HolderKind ?? ActorKind.Staff, active.Holder, names)} is editing this record.");
        }
        catch (EditScopeVersionConflictException)
        {
            EditingId = Guid.Empty;
            ModelState.AddModelError(string.Empty, "The record changed. Reload to edit the current settings.");
        }
        ModelState.ClearValidationState(nameof(EditingId));
        return Page();
    }

    public async Task<IActionResult> OnPostNewCardAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        EditingId = Guid.NewGuid();
        ExpectedVersion = 0;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        try
        {
            if (EditingId == GetWorkflowConfiguration.RecordId && (ExpectedVersion < 1 || ExpectedVersion > int.MaxValue))
                ModelState.AddModelError(string.Empty, "The settings version is invalid. Reload to try again.");
            if (ModelState.IsValid)
            {
                if (EditingId == GetWorkflowConfiguration.RecordId)
                    await updateWorkflowConfiguration.ExecuteAsync(new(checked((int)ExpectedVersion), actor, OperationKey)
                    {
                        RequireInstructions = RequireInstructions, RequireImages = RequireImages,
                        ChaseIntervalDays = ChaseIntervalDays, EditLeaseToken = LeaseToken ?? string.Empty
                    }, cancellationToken);
                else
                    await rateCards.SaveAsync(new(EditingId, CardName ?? string.Empty, HourlyRate, Enabled, ExpectedVersion,
                        actor, Reason ?? string.Empty, OperationKey, LeaseToken ?? string.Empty), cancellationToken);
                TempData["Confirmation"] = "Settings saved.";
                return RedirectToPage();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or WorkflowConfigurationVersionConflictException
            or WorkflowConfigurationOperationConflictException or EditScopeConflictException
            or EditScopeExpiredException or EditScopeVersionConflictException or LabourRateCardConflictException)
        {
            ModelState.AddModelError(string.Empty, exception is ArgumentException
                ? exception.Message : "The edit could not be saved. Cancel and reopen the current record.");
        }
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (ExpectedVersion > 0 && !string.IsNullOrEmpty(LeaseToken))
        {
            try
            {
            await editScopes.ReleaseAsync(new(ScopeKind, EditingId, actor, NewOperationKey(), LeaseToken), cancellationToken);
            }
            catch (Exception exception) when (exception is EditScopeExpiredException or EditScopeConflictException)
            {
                // No mutation is requested; an expired or replaced lease leaves nothing to release.
            }
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        try
        {
            var lease = await editScopes.HeartbeatAsync(new(ScopeKind, EditingId, actor, LeaseToken ?? string.Empty), cancellationToken);
            return new JsonResult(new { expiresAtUtc = lease.ExpiresAtUtc });
        }
        catch (Exception exception) when (exception is EditScopeExpiredException or EditScopeConflictException)
        { return StatusCode(409); }
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        AutomationComposed = HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        Configuration = await getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
        RateCards = await rateCards.ListAsync(actor, cancellationToken);
    }
}
