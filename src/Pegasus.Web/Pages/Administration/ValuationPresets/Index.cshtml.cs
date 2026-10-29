using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Labels = Pegasus.Web.Presentation.OperatorLabels.ValuationPresets;

namespace Pegasus.Web.Pages.Administration.ValuationPresets;

/// <summary>
/// Maintains the valuation additions an Engineer may select on a Case. The
/// amounts kept here are suggestions: a Case copies the label and amount it
/// selects, so editing a preset never reaches back into a recorded calculation.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListValuationPresets listValuationPresets,
    ISaveValuationPreset saveValuationPreset,
    IRemoveValuationPreset removeValuationPreset) : AdministrationPageModel
{
    /// <summary>Every preset, enabled and disabled, in the Core query's order.</summary>
    public IReadOnlyList<ValuationPreset> Presets { get; private set; } = [];

    public Guid CreatePresetId { get; private set; } = Guid.NewGuid();
    public string CreateOperationKey { get; private set; } = NewOperationKey();
    public string CreateLabel { get; private set; } = string.Empty;
    public string CreateAmountInput { get; private set; } = string.Empty;
    public bool CreateActive { get; private set; } = true;

    public Guid RowPresetId { get; private set; }
    public string RowLabel { get; private set; } = string.Empty;
    public string RowAmount { get; private set; } = string.Empty;
    public bool RowActive { get; private set; }
    public long RowExpectedVersion { get; private set; }
    public string RowOperationKey { get; private set; } = string.Empty;
    public bool RowPostFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
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
        CreateOperationKey = operationKey ?? string.Empty;
        CreateLabel = label?.Trim() ?? string.Empty;
        CreateAmountInput = Request.Form["amount"].ToString();
        CreateActive = active;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, label, amount)) return null;
                await saveValuationPreset.ExecuteAsync(
                    new(CreatePresetId, label!, amount!.Value, active, ExpectedVersion: 0, actor, operationKey!),
                    cancellationToken);
                return Labels.Created;
            },
            cancellationToken);
    }

    /// <summary>The save form belongs to one row and carries that row's version and replay key.</summary>
    public Task<IActionResult> OnPostSaveAsync(
        Guid presetId,
        long expectedVersion,
        string? label,
        decimal? amount,
        bool active,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        RowPresetId = presetId;
        RowLabel = label ?? string.Empty;
        RowAmount = Request.Form["amount"].ToString();
        RowActive = active;
        RowExpectedVersion = expectedVersion;
        RowOperationKey = operationKey ?? string.Empty;
        RowPostFailed = true;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, label, amount) | !RequirePreset(presetId)) return null;
                await saveValuationPreset.ExecuteAsync(
                    new(presetId, label!, amount!.Value, active, expectedVersion, actor, operationKey!),
                    cancellationToken);
                return active ? Labels.Saved : Labels.Disabled;
            },
            cancellationToken);
    }

    /// <summary>
    /// Removal is a soft change to this preset. Existing Case valuations keep
    /// their recorded snapshots.
    /// </summary>
    public Task<IActionResult> OnPostRemoveAsync(
        Guid presetId,
        long expectedVersion,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            async actor =>
            {
                if (!RequireOperationKey(operationKey) | !RequirePreset(presetId)) return null;
                await removeValuationPreset.ExecuteAsync(
                    new(presetId, expectedVersion, actor, operationKey!, Reason: null),
                    cancellationToken);
                return Labels.Removed;
            },
            cancellationToken);
    }

    private async Task<IActionResult> RunAsync(
        Func<ActionActor, Task<string?>> operation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();

        string? confirmation = null;
        try
        {
            confirmation = await operation(actor);
        }
        catch (ValuationPresetException exception)
        {
            if (exception.Error == ValuationPresetError.OperationConflict && RowPostFailed)
                RowOperationKey = NewOperationKey();
            ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(string.Empty, Labels.NotAccepted);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
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

    private bool Validate(string? operationKey, string? label, decimal? amount)
    {
        var valid = RequireOperationKey(operationKey);
        // The label and amount ranges are Core's; a refused value is answered
        // here, typed values kept, before anything reaches the store.
        if (string.IsNullOrWhiteSpace(label) || !ValuationCalculationPolicy.IsAdditionLabelInRange(label))
        {
            ModelState.AddModelError(string.Empty, Labels.LabelRequired);
            valid = false;
        }
        if (amount is not { } value || !ValuationCalculationPolicy.IsPresetAmountInRange(value))
        {
            ModelState.AddModelError(string.Empty, Labels.AmountRequired);
            valid = false;
        }

        return valid;
    }

    private bool RequireOperationKey(string? operationKey)
    {
        if (IsOperationKeyValid(operationKey)) return true;
        ModelState.AddModelError(string.Empty, Labels.Expired);
        return false;
    }

    private bool RequirePreset(Guid presetId)
    {
        if (presetId != Guid.Empty) return true;
        ModelState.AddModelError(string.Empty, Labels.Expired);
        return false;
    }

    private static string MutationErrorMessage(ValuationPresetError error) => error switch
    {
        ValuationPresetError.NotFound => Labels.NotFound,
        ValuationPresetError.Removed => Labels.AlreadyRemoved,
        ValuationPresetError.DuplicateLabel => Labels.DuplicateLabel,
        ValuationPresetError.VersionConflict => Labels.StaleVersion,
        ValuationPresetError.OperationConflict => Labels.OperationConflict,
        _ => Labels.NotAccepted
    };

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Presets = await listValuationPresets.ExecuteAsync(actor, cancellationToken);
    }
}
