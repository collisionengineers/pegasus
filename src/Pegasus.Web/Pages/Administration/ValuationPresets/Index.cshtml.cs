using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration.ValuationPresets;

/// <summary>
/// Maintains the valuation additions an Engineer may select on a Case. The
/// amounts kept here are suggestions: a Case copies the label and amount it
/// selects, so editing a preset never reaches back into a recorded
/// calculation.
/// </summary>
/// <remarks>
/// One form per preset row plus the create form post to this page, so the
/// submitted values arrive as handler parameters rather than bound
/// properties: a <c>[Required]</c> property belonging to one form would
/// invalidate every other form's post. The same rule the Accounts area
/// follows.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListValuationPresets listValuationPresets,
    ISaveValuationPreset saveValuationPreset) : AdministrationPageModel
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

    /// <summary>The reason typed into the create form, kept over a failed post.</summary>
    public string CreateReason { get; private set; } = string.Empty;

    /// <summary>The preset targeted by the most recent row post.</summary>
    public Guid RowPresetId { get; private set; }

    /// <summary>The reason submitted by the most recent row post.</summary>
    public string RowReason { get; private set; } = string.Empty;

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        RunAsync(_ => Task.FromResult<string?>(null), cancellationToken);

    public Task<IActionResult> OnPostCreateAsync(
        Guid presetId,
        string? label,
        decimal? amount,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        CreatePresetId = presetId == Guid.Empty ? Guid.NewGuid() : presetId;
        CreateLabel = label?.Trim() ?? string.Empty;
        CreateAmount = amount;
        CreateReason = reason ?? string.Empty;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason, label, amount))
                {
                    return null;
                }

                await saveValuationPreset.ExecuteAsync(
                    new(
                        CreatePresetId,
                        label!,
                        amount!.Value,
                        Active: true,
                        ExpectedVersion: 0,
                        actor,
                        reason!,
                        operationKey!),
                    cancellationToken);
                return ValuationPresetLabels.Created;
            },
            cancellationToken);
    }

    /// <summary>
    /// The one write a preset row performs. Enabling and disabling is the
    /// same save with a different <paramref name="active"/> value, which is
    /// why the row's buttons carry it rather than posting to a second
    /// handler.
    /// </summary>
    public Task<IActionResult> OnPostSaveAsync(
        Guid presetId,
        long expectedVersion,
        string? label,
        decimal? amount,
        bool active,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        RowPresetId = presetId;
        RowReason = reason ?? string.Empty;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason, label, amount) | !RequirePreset(presetId))
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
                        reason!,
                        operationKey!),
                    cancellationToken);
                return active ? ValuationPresetLabels.Saved : ValuationPresetLabels.Disabled;
            },
            cancellationToken);
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
        string? reason,
        string? label,
        decimal? amount)
    {
        var valid = true;
        if (string.IsNullOrWhiteSpace(operationKey) || !IsOperationKeyValid(operationKey))
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.Expired);
            valid = false;
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            ModelState.AddModelError(string.Empty, ValuationPresetLabels.ReasonRequired);
            valid = false;
        }
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
        ValuationPresetError.DuplicateLabel => ValuationPresetLabels.DuplicateLabel,
        ValuationPresetError.VersionConflict => ValuationPresetLabels.StaleVersion,
        ValuationPresetError.OperationConflict => ValuationPresetLabels.OperationConflict,
        _ => ValuationPresetLabels.NotAccepted
    };

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
    public const string Reason = "Reason";
    public const string Save = "Save";
    public const string Disable = "Disable";
    public const string Enable = "Enable";
    public const string Create = "Create preset";
    public const string Change = "Change";
    public const string Enabled = "Enabled";
    public const string DisabledState = "Disabled";
    public const string Created = "The valuation preset was created.";
    public const string Saved = "The valuation preset was saved.";
    public const string Disabled = "The valuation preset was disabled.";
    public const string Expired = "The form has expired. Retry the operation.";
    public const string ReasonRequired = "Enter a reason.";
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
