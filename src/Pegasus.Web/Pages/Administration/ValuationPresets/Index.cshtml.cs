using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration.ValuationPresets;

/// <summary>
/// Maintains the valuation additions an Engineer may select on a Case. The
/// amounts kept here are suggestions: a Case copies the label and amount it
/// selects, so editing a preset never reaches back into a recorded
/// calculation.
/// </summary>
/// <remarks>
/// The create and edit editor forms post to this page, so their submitted
/// values arrive as handler parameters rather than bound properties: a
/// <c>[Required]</c> property belonging to one form would invalidate the
/// other form's post. The same rule the Accounts area follows.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListValuationPresets listValuationPresets,
    ISaveValuationPreset saveValuationPreset,
    IRemoveValuationPreset removeValuationPreset,
    IEditScopeLeases editScopes,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder) : AdministrationPageModel
{
    /// <summary>Every preset, enabled and disabled, in the Core query's order.</summary>
    public IReadOnlyList<ValuationPreset> Presets { get; private set; } = [];

    /// <summary>Whether the Automation ingress exists, for the area rail.</summary>
    public bool AutomationComposed { get; private set; }

    /// <summary>The identity the create form mints, kept over a failed post.</summary>
    public Guid CreatePresetId { get; private set; } = Guid.NewGuid();

    /// <summary>The operation key the create form carries.</summary>
    public string CreateOperationKey { get; private set; } = NewOperationKey();

    /// <summary>The label typed into the create form, kept over a failed post.</summary>
    public string CreateLabel { get; private set; } = string.Empty;

    /// <summary>The amount typed into the create form, kept over a failed post.</summary>
    public decimal? CreateAmount { get; private set; }

    /// <summary>The active state selected in the create editor.</summary>
    public bool CreateActive { get; private set; } = true;

    /// <summary>The preset targeted by the most recent row post.</summary>
    public Guid RowPresetId { get; private set; }

    /// <summary>The label submitted to the targeted row, kept after a failed post.</summary>
    public string RowLabel { get; private set; } = string.Empty;

    /// <summary>The raw amount submitted to the targeted row, kept after a failed post.</summary>
    public string RowAmount { get; private set; } = string.Empty;

    /// <summary>The active state submitted by the most recent row post.</summary>
    public bool RowActive { get; private set; }

    public Guid EditingPresetId { get; private set; }
    public long EditingPresetVersion { get; private set; }
    public string EditingLeaseToken { get; private set; } = string.Empty;

    /// <summary>
    /// The preset this operator is already editing in another window, offered
    /// with the take-over that ends the other window's claim.
    /// </summary>
    public Guid TakeOverPresetId { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid? editPresetId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await LoadAsync(actor, cancellationToken);
        if (editPresetId is not { } presetId || expectedVersion is not { } version)
        {
            return Page();
        }

        var preset = Presets.SingleOrDefault(item => item.Id == presetId);
        if (preset is null)
        {
            return NotFound();
        }
        if (preset.Version != version)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.StaleVersion);
            return Page();
        }

        try
        {
            // A plain GET never takes over another window's claim; that is a
            // mutating act and stays behind the posted Edit handler below.
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.ValuationPreset, preset.Id, preset.Version, actor, NewOperationKey())
                {
                    TakeOver = false
                },
                cancellationToken);
            EditingPresetId = preset.Id;
            EditingPresetVersion = preset.Version;
            EditingLeaseToken = lease.Token;
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverPresetId = preset.Id;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                await EditConflictMessageAsync(preset.Id, actor, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.StaleVersion);
        }

        return Page();
    }

    /// <summary>
    /// The take-over claim: the same edit-scope acquisition the Edit link
    /// performs, but posted rather than followed as a plain link, because
    /// taking over ends another window's claim and a GET must never do that.
    /// </summary>
    public async Task<IActionResult> OnPostEditAsync(
        Guid presetId,
        long expectedVersion,
        string? operationKey,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (presetId == Guid.Empty || !IsOperationKeyValid(operationKey)) return BadRequest();

        await LoadAsync(actor, cancellationToken);
        var preset = Presets.SingleOrDefault(item => item.Id == presetId);
        if (preset is null)
        {
            return NotFound();
        }
        if (preset.Version != expectedVersion)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.StaleVersion);
            return Page();
        }

        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.ValuationPreset, preset.Id, preset.Version, actor, operationKey!)
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            EditingPresetId = preset.Id;
            EditingPresetVersion = preset.Version;
            EditingLeaseToken = lease.Token;
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverPresetId = preset.Id;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                await EditConflictMessageAsync(preset.Id, actor, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.StaleVersion);
        }

        return Page();
    }

    public Task<IActionResult> OnPostCreateAsync(
        Guid presetId,
        string? label,
        decimal? amount,
        bool active,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        CreatePresetId = presetId == Guid.Empty ? Guid.NewGuid() : presetId;
        CreateLabel = label?.Trim() ?? string.Empty;
        CreateAmount = amount;
        CreateActive = active;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, label, amount))
                {
                    return null;
                }

                await saveValuationPreset.ExecuteAsync(
                    new(
                        CreatePresetId,
                        label!,
                        amount!.Value,
                        active,
                        ExpectedVersion: 0,
                        actor,
                        operationKey!),
                    cancellationToken);
                return ValuationPresetLabels.Created;
            },
            cancellationToken);
    }

    /// <summary>
    /// The one write the preset editor performs. Its enabled checkbox is part
    /// of the save rather than a separate state-changing command.
    /// </summary>
    public Task<IActionResult> OnPostSaveAsync(
        Guid presetId,
        long expectedVersion,
        string? label,
        decimal? amount,
        bool active,
        string? editLeaseToken,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        RowPresetId = presetId;
        RowLabel = label ?? string.Empty;
        RowAmount = Request.Form["amount"].ToString();
        RowActive = active;
        EditingPresetId = presetId;
        EditingPresetVersion = expectedVersion;
        EditingLeaseToken = editLeaseToken ?? string.Empty;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, label, amount) | !RequirePreset(presetId))
                {
                    return null;
                }

                await saveValuationPreset.ExecuteAsync(
                    new(
                        presetId,
                        label!,
                        amount!.Value,
                        active,
                        expectedVersion,
                        actor,
                        operationKey!)
                    {
                        EditLeaseToken = editLeaseToken ?? string.Empty
                    },
                    cancellationToken);
                return active ? ValuationPresetLabels.Saved : ValuationPresetLabels.Disabled;
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes one preset from the maintained list. A removal is a change to
    /// the record like a save, so it claims the same edit lease and the store
    /// closes that scope with the removal: a Remove is therefore refused
    /// while the preset is being edited elsewhere. Recorded valuations keep
    /// their own snapshot of what was selected.
    /// </summary>
    public Task<IActionResult> OnPostRemoveAsync(
        Guid presetId,
        long expectedVersion,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        RowPresetId = presetId;
        return RunAsync(
            async actor =>
            {
                if (!RequireOperationKey(operationKey) | !RequireReason(reason)
                    | !RequirePreset(presetId))
                {
                    return null;
                }

                var lease = await editScopes.ClaimAsync(
                    new(
                        EditScopeKind.ValuationPreset,
                        presetId,
                        expectedVersion,
                        actor,
                        NewOperationKey()),
                    cancellationToken);
                await removeValuationPreset.ExecuteAsync(
                    new(presetId, expectedVersion, actor, operationKey!, reason!)
                    {
                        EditLeaseToken = lease.Token
                    },
                    cancellationToken);
                return ValuationPresetLabels.Removed;
            },
            cancellationToken);
    }

    public async Task<IActionResult> OnPostCancelEditAsync(
        Guid presetId,
        string? editLeaseToken,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (presetId == Guid.Empty || !IsOperationKeyValid(operationKey)
            || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return RedirectToPage();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.ValuationPreset, presetId, actor, operationKey!, editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // Closing an already-expired edit session has no remaining work.
        }
        return RedirectToPage();
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(
        Guid presetId,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (presetId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.ValuationPreset, presetId, actor, NewOperationKey(), editLeaseToken),
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            // The scope has already gone or has already been re-claimed by a
            // newer window of this operator's own session.
        }
        return new NoContentResult();
    }

    public async Task<IActionResult> OnPostHeartbeatEditAsync(
        Guid presetId,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (presetId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new ConflictObjectResult("Editing this valuation preset has ended. Reload it before making further changes.");
        }

        try
        {
            await editScopes.HeartbeatAsync(
                new(EditScopeKind.ValuationPreset, presetId, actor, editLeaseToken),
                cancellationToken);
            return new OkResult();
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult("Editing this valuation preset has ended. Reload it before making further changes.");
        }
    }

    /// <summary>
    /// The one place an operation is authorised, run, translated into an
    /// operator message and followed by a reload, so each handler holds only
    /// what differs between them. A rejected post re-mints the create form's
    /// operation key, which is what stops a retry replaying the refused one.
    /// </summary>
    private async Task<IActionResult> RunAsync(
        Func<ActionActor, Task<string?>> operation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        string? confirmation = null;
        try
        {
            confirmation = await operation(actor);
        }
        catch (ValuationPresetException exception)
        {
            ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.NotAccepted);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                await EditConflictMessageAsync(RowPresetId, actor, cancellationToken));
        }
        catch (EditScopeExpiredException)
        {
            ModelState.AddModelError(string.Empty, "Your preset edit session expired. Reopen it and try again.");
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.StaleVersion);
        }

        if (confirmation is not null)
        {
            TempData["Confirmation"] = confirmation;
            return RedirectToPage();
        }

        CreateOperationKey = NewOperationKey();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private bool Validate(
        string? operationKey,
        string? label,
        decimal? amount)
    {
        var valid = RequireOperationKey(operationKey);
        if (string.IsNullOrWhiteSpace(label))
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.LabelRequired);
            valid = false;
        }
        if (amount is not { } value || value < 0m || decimal.Round(value, 2) != value)
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.AmountRequired);
            valid = false;
        }

        return valid;
    }

    private bool RequireOperationKey(string? operationKey)
    {
        if (IsOperationKeyValid(operationKey))
        {
            return true;
        }

        ModelState.AddModelError(string.Empty, ValuationPresetLabels.Expired);
        return false;
    }

    private bool RequireReason(string? reason)
    {
        if (!string.IsNullOrWhiteSpace(reason)
            && reason.Trim().Length <= ValuationCalculationPolicy.MaximumReasonLength)
        {
            return true;
        }

        ModelState.AddModelError(string.Empty, ValuationPresetLabels.ReasonRequired);
        return false;
    }

    private bool RequirePreset(Guid presetId)
    {
        if (presetId != Guid.Empty)
        {
            return true;
        }

        ModelState.AddModelError(string.Empty, ValuationPresetLabels.Expired);
        return false;
    }

    private static string MutationErrorMessage(ValuationPresetError error) => error switch
    {
        ValuationPresetError.NotFound => ValuationPresetLabels.NotFound,
        ValuationPresetError.Removed => ValuationPresetLabels.AlreadyRemoved,
        ValuationPresetError.DuplicateLabel => ValuationPresetLabels.DuplicateLabel,
        ValuationPresetError.VersionConflict => ValuationPresetLabels.StaleVersion,
        ValuationPresetError.OperationConflict => ValuationPresetLabels.OperationConflict,
        _ => ValuationPresetLabels.NotAccepted
    };

    private async Task<string> EditConflictMessageAsync(
        Guid presetId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var active = await editScopes.GetActiveAsync(
            EditScopeKind.ValuationPreset,
            presetId,
            actor,
            cancellationToken);
        if (active is null)
        {
            return "Another member of staff is editing this preset. Reload to try again.";
        }

        var isSelf = EditScopeAuthority.IsHolder(active.HolderKind, active.Holder, actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await describeEditAuthorityHolder.ExecuteAsync(
                active.HolderKind,
                active.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.HeldBy(RecordName, holder, isSelf);
    }

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "preset";

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        Presets = await listValuationPresets.ExecuteAsync(actor, cancellationToken);
    }
}

/// <summary>
/// The operator-facing words this administration area uses. They live here
/// rather than in <c>OperatorLabels</c> because that file is owned elsewhere
/// in this change set; the members below are the list to fold into its
/// <c>Admin</c> group when the two meet.
/// </summary>
internal static class ValuationPresetLabels
{
    public const string Title = "Valuation presets";
    public const string PresetLabel = "Label";
    public const string Amount = "Amount";
    public const string State = "State";
    public const string Version = "Version";
    public const string Save = "Save";
    public const string Disable = "Disable";
    public const string Enable = "Enable";
    public const string Create = "Create preset";
    public const string Change = "Change";
    public const string Enabled = "Enabled";
    public const string DisabledState = "Disabled";
    public const string Remove = "Remove";
    public const string RemoveTitle = "Remove preset";
    public const string Created = "The valuation preset was created.";
    public const string Saved = "The valuation preset was saved.";
    public const string Disabled = "The valuation preset was disabled.";
    public const string Removed = "The valuation preset was removed.";
    public const string AlreadyRemoved = "That valuation preset was removed.";
    public const string ReasonRequired = "Enter a reason.";
    public const string Expired = "The form has expired. Retry the operation.";
    public const string LabelRequired = "Enter a label.";
    public const string AmountRequired = "Enter an amount of £0.00 or more.";
    public const string NotFound = "The valuation preset no longer exists.";
    public const string DuplicateLabel = "That label is already assigned to a valuation preset.";
    public const string StaleVersion =
        "The preset changed after this page was loaded. Review the current version and retry.";
    public const string OperationConflict =
        "The form was already used for a different operation. Retry from the current page.";
    public const string NotAccepted = "The change was not accepted.";

    public static string StateName(bool active) => active ? Enabled : DisabledState;
}
