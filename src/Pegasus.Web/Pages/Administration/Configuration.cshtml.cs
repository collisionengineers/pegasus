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
    [BindProperty] public int UnidentifiedTargetDays { get; set; }
    [BindProperty] public int TriageTargetDays { get; set; } = 1;
    [BindProperty] public int HeldTargetDays { get; set; } = 7;
    [BindProperty] public int ReviewTargetDays { get; set; } = 1;
    [BindProperty] public int AiDraftTargetDays { get; set; } = 1;
    [BindProperty] public string? CardName { get; set; } = string.Empty;
    [BindProperty] public decimal HourlyRate { get; set; }
    [BindProperty] public bool Enabled { get; set; } = true;
    [BindProperty] public string? Reason { get; set; }
    public bool IsEditing => EditingId != Guid.Empty;

    /// <summary>One workflow setting: its posted field, label, range, the saved value and the value the form carries.</summary>
    public sealed record WorkflowSetting(string Field, string Label, int Minimum, int Maximum, int Current, int Posted);

    /// <summary>
    /// The six workflow settings in the planned order (Configuration, 13 September):
    /// the chase interval and the five Work Centre due targets.
    /// </summary>
    public IReadOnlyList<WorkflowSetting> WorkflowSettings => Configuration is null ? [] :
    [
        new(nameof(ChaseIntervalDays), "Chase interval", 1, 365, Configuration.ChaseIntervalDays, ChaseIntervalDays),
        new(nameof(UnidentifiedTargetDays), "Unidentified target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.UnidentifiedTargetDays, UnidentifiedTargetDays),
        new(nameof(TriageTargetDays), "Triage target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.TriageTargetDays, TriageTargetDays),
        new(nameof(HeldTargetDays), "Held decision target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.HeldTargetDays, HeldTargetDays),
        new(nameof(ReviewTargetDays), "Review target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.ReviewTargetDays, ReviewTargetDays),
        new(nameof(AiDraftTargetDays), "AI draft target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.AiDraftTargetDays, AiDraftTargetDays)
    ];

    public static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    /// <summary>Each setting outside its range is refused against its own input, naming the range.</summary>
    private void ValidateWorkflowRanges()
    {
        foreach (var setting in WorkflowSettings)
        {
            if (setting.Posted < setting.Minimum || setting.Posted > setting.Maximum)
            {
                ModelState.AddModelError(
                    setting.Field,
                    $"{setting.Label} must be between {setting.Minimum} and {setting.Maximum} days.");
            }
        }
    }

    private bool Posted(string field) => Request.HasFormContentType && Request.Form.ContainsKey(field);

    /// <summary>
    /// The record this operator is already editing in another window, offered
    /// with the take-over that ends the other window's claim.
    /// </summary>
    public Guid TakeOverRecordId { get; private set; }

    private EditScopeKind ScopeKind => EditingId == GetWorkflowConfiguration.RecordId
        ? EditScopeKind.NamedConfiguration : EditScopeKind.LabourRateCard;

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "record";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(
        Guid recordId,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        // Edit posts only the record id: the editor's bound fields are filled below,
        // so their missing-value binding errors are not the operator's.
        ModelState.Clear();
        EditingId = recordId;
        if (recordId == GetWorkflowConfiguration.RecordId)
        {
            ExpectedVersion = Configuration.PolicyVersion;
            RequireInstructions = Configuration.RequireInstructions;
            RequireImages = Configuration.RequireImages;
            ChaseIntervalDays = Configuration.ChaseIntervalDays;
            UnidentifiedTargetDays = Configuration.UnidentifiedTargetDays;
            TriageTargetDays = Configuration.TriageTargetDays;
            HeldTargetDays = Configuration.HeldTargetDays;
            ReviewTargetDays = Configuration.ReviewTargetDays;
            AiDraftTargetDays = Configuration.AiDraftTargetDays;
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
            LeaseToken = (await editScopes.ClaimAsync(
                new(ScopeKind, recordId, ExpectedVersion, actor, NewOperationKey())
                {
                    TakeOver = takeOver
                },
                cancellationToken)).Token;
            OperationKey = NewOperationKey();
        }
        catch (EditScopeHeldElsewhereException)
        {
            EditingId = Guid.Empty;
            TakeOverRecordId = recordId;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
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
                : EditModeDisplay.HeldBy(
                    RecordName,
                    active.HolderKind == ActorKind.Automation
                        ? CaseEditAuthorityHolder.Automation
                        : new CaseEditAuthorityHolder(ActorDisplayNames.Resolve(
                            active.HolderKind ?? ActorKind.Staff, active.Holder, names)),
                    isSelf: false));
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
            if (EditingId == GetWorkflowConfiguration.RecordId)
            {
                Configuration = await getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
                ValidateWorkflowRanges();
            }
            if (ModelState.IsValid)
            {
                if (EditingId == GetWorkflowConfiguration.RecordId)
                {
                    // A target the form does not yet render keeps its current value rather
                    // than falling back to the default.
                    var current = await getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
                    await updateWorkflowConfiguration.ExecuteAsync(new(checked((int)ExpectedVersion), actor, OperationKey)
                    {
                        RequireInstructions = RequireInstructions, RequireImages = RequireImages,
                        ChaseIntervalDays = ChaseIntervalDays,
                        UnidentifiedTargetDays = Posted(nameof(UnidentifiedTargetDays)) ? UnidentifiedTargetDays : current.UnidentifiedTargetDays,
                        TriageTargetDays = Posted(nameof(TriageTargetDays)) ? TriageTargetDays : current.TriageTargetDays,
                        HeldTargetDays = Posted(nameof(HeldTargetDays)) ? HeldTargetDays : current.HeldTargetDays,
                        ReviewTargetDays = Posted(nameof(ReviewTargetDays)) ? ReviewTargetDays : current.ReviewTargetDays,
                        AiDraftTargetDays = Posted(nameof(AiDraftTargetDays)) ? AiDraftTargetDays : current.AiDraftTargetDays,
                        EditLeaseToken = LeaseToken ?? string.Empty
                    }, cancellationToken);
                }
                else
                    await rateCards.SaveAsync(new(EditingId, CardName ?? string.Empty, HourlyRate, Enabled, ExpectedVersion,
                        actor, Reason, OperationKey, LeaseToken ?? string.Empty), cancellationToken);
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

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (EditingId == Guid.Empty || string.IsNullOrEmpty(LeaseToken)) return new NoContentResult();
        try
        {
            await editScopes.ReleaseAsync(
                new(ScopeKind, EditingId, actor, NewOperationKey(), LeaseToken), cancellationToken);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            // The scope has already gone or has already been re-claimed by a
            // newer window of this operator's own session.
        }
        return new NoContentResult();
    }

    public async Task<IActionResult> OnPostHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        try
        {
            await editScopes.HeartbeatAsync(new(ScopeKind, EditingId, actor, LeaseToken ?? string.Empty), cancellationToken);
            return new NoContentResult();
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
