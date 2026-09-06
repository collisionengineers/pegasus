using System.Globalization;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// One maintained valuation addition. The amount held here is the manager's
/// suggestion, not a Case figure: selecting the preset copies its identity,
/// version, label and suggested amount onto the Case, where the Engineer may
/// change the copied amount without ever editing this record.
/// </summary>
public sealed record ValuationPreset(
    Guid Id,
    string Label,
    decimal SuggestedAmount,
    bool Active,
    long Version,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Creates or updates one preset. <see cref="ExpectedVersion"/> 0 creates the
/// record the caller has already minted an identity for, which is what makes
/// a repeated create idempotent; any other value updates the record at
/// exactly that version.
/// </summary>
public sealed record SaveValuationPresetRequest(
    Guid PresetId,
    string Label,
    decimal SuggestedAmount,
    bool Active,
    long ExpectedVersion,
    ActionActor Actor,
    string Reason,
    string OperationKey);

public enum ValuationPresetError
{
    NotFound,
    DuplicateLabel,
    VersionConflict,
    OperationConflict,
    NotSelectable
}

public sealed class ValuationPresetException(
    ValuationPresetError error,
    long? currentVersion = null)
    : InvalidOperationException("The valuation preset request could not be completed.")
{
    public ValuationPresetError Error { get; } = error;

    public long? CurrentVersion { get; } = currentVersion;
}

public interface IValuationPresetStore
{
    Task<IReadOnlyList<ValuationPreset>> ListAsync(CancellationToken cancellationToken);

    Task<ValuationPreset> SaveAsync(
        SaveValuationPresetRequest request,
        CancellationToken cancellationToken);
}

public interface IListValuationPresets
{
    Task<IReadOnlyList<ValuationPreset>> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken);
}

public interface ISaveValuationPreset
{
    Task<ValuationPreset> ExecuteAsync(
        SaveValuationPresetRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// One addition as the Case posts it. A preset selection carries the identity
/// and version the Engineer saw, so a maintained record that moved underneath
/// the form is caught rather than silently applied; a custom addition carries
/// no preset and must name itself.
/// </summary>
public sealed record ValuationAdditionSelection(
    Guid PresetId,
    long PresetVersion,
    string? Label,
    decimal Amount);

/// <summary>
/// One addition as it is calculated and recorded: the maintained label and
/// suggestion resolved from the preset record itself, beside the amount the
/// Engineer chose. <see cref="SuggestedAmount"/> is null for a custom
/// addition, which has no maintained suggestion to depart from.
/// </summary>
public sealed record ValuationAddition(
    Guid PresetId,
    long PresetVersion,
    string Label,
    decimal? SuggestedAmount,
    decimal Amount);

/// <summary>
/// Everything the Engineer chose on the calculator, and nothing else. The
/// preview and the Apply command carry exactly this shape, so what is shown
/// and what is adopted can never be two different selections.
/// </summary>
public sealed record ValuationCalculationSelection(
    Guid GuideValuationId,
    bool CommercialVat,
    decimal? PriorTotalLossPercentage,
    IReadOnlyList<ValuationAdditionSelection> Additions,
    decimal ConditionDeduction);

/// <summary>
/// The facts the selection is calculated against, all read from their own
/// owners rather than from the form: the guide card and the moment it was
/// last written, the claimant's own VAT position, and the maintained presets.
/// </summary>
public sealed record ValuationCalculationBasis(
    Guid GuideValuationId,
    DateTimeOffset GuideValuationStampUtc,
    decimal GuideRetailValue,
    bool ClaimantVatRegistered,
    IReadOnlyList<ValuationPreset> Presets);

public sealed record ValuationCalculationInput(
    decimal GuideRetailValue,
    bool CommercialVat,
    bool ClaimantVatRegistered,
    decimal? PriorTotalLossPercentage,
    IReadOnlyList<ValuationAddition> Additions,
    decimal ConditionDeduction);

/// <summary>
/// The ordered calculation, kept whole so the recorded snapshot and the
/// on-screen preview are the same numbers in the same order.
/// </summary>
public sealed record ValuationCalculation(
    decimal GuideRetailValue,
    bool CommercialVatApplied,
    decimal CommercialVatAmount,
    decimal ValueIncludingVat,
    decimal? PriorTotalLossPercentage,
    decimal PriorTotalLossAmount,
    IReadOnlyList<ValuationAddition> Additions,
    decimal AdditionsTotal,
    decimal ConditionDeduction,
    decimal Proposal);

/// <summary>
/// What the calculator shows before anything is adopted. It carries the guide
/// card's stamp so the Apply that follows pins itself to the very card the
/// figures were prepared from.
/// </summary>
public sealed record ValuationPreview(
    Guid GuideValuationId,
    DateTimeOffset GuideValuationStampUtc,
    ValuationCalculation Calculation);

public sealed record PreviewValuationRequest(
    Guid CaseId,
    ActionActor Actor,
    ValuationCalculationSelection Selection);

/// <summary>
/// One recorded adoption of an Engineer's Value: the basis card it was
/// calculated from, the whole ordered calculation, the value actually
/// accepted, and who accepted it when and why. <see cref="CaseVersion"/> is
/// the Case version this adoption produced, which is the version a later
/// report snapshot cites alongside <see cref="Id"/>. A later correction
/// records a further row against the same basis rather than rewriting this
/// one.
/// </summary>
public sealed record AppliedValuation(
    Guid Id,
    Guid CaseId,
    long CaseVersion,
    Guid GuideValuationId,
    DateTimeOffset GuideValuationStampUtc,
    ValuationCalculation Calculation,
    decimal AcceptedEngineerValue,
    string AcceptedBy,
    DateTimeOffset AcceptedAtUtc,
    string Reason,
    string CalculationPolicyVersion);

/// <summary>
/// Adopts a calculated value as the Case's Engineer's Value.
/// <see cref="GuideValuationStampUtc"/> is the basis card's own last-written
/// stamp: the card carries no separate version number, so its edit stamp is
/// what proves the Engineer applied the card they were shown.
/// <see cref="CorrectedEngineerValue"/> carries a later manual correction,
/// which keeps this same basis and adds its own reason and history row.
/// </summary>
public sealed record ApplyValuationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    ValuationCalculationSelection Selection,
    DateTimeOffset GuideValuationStampUtc,
    decimal? CorrectedEngineerValue = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface IAppliedValuationStore
{
    Task<ValuationCalculationBasis> ReadBasisAsync(
        Guid caseId,
        Guid guideValuationId,
        CancellationToken cancellationToken);

    Task<AppliedValuation> ApplyAsync(
        ApplyValuationRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AppliedValuation>> ListAppliedAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public interface IPreviewValuationCalculation
{
    Task<ValuationPreview> ExecuteAsync(
        PreviewValuationRequest request,
        CancellationToken cancellationToken);
}

public interface IApplyValuationCalculation
{
    Task<AppliedValuation> ExecuteAsync(
        ApplyValuationRequest request,
        CancellationToken cancellationToken);
}

public interface IListAppliedValuations
{
    Task<IReadOnlyList<AppliedValuation>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of valuation arithmetic and of the rules that decide which
/// additions may be selected. The Case preview and the Apply command call
/// exactly these members, so what the Engineer sees and what is recorded can
/// never be two calculations.
/// </summary>
public static class ValuationCalculationPolicy
{
    public const string PolicyKey = "case-valuation-calculation";
    public const int PolicyVersion = 1;
    public const decimal CommercialVatRate = 0.20m;
    public const int MaximumAdditions = 20;
    public const int MaximumAdditionLabelLength = 200;

    /// <summary>
    /// The two prior-total-loss reductions the business applies. There is no
    /// free percentage: an unrecognized value is refused rather than rounded
    /// into one of these.
    /// </summary>
    public static readonly IReadOnlyList<decimal> PriorTotalLossPercentages = [0.10m, 0.20m];

    public static string PolicyStamp => $"{PolicyKey}/v{PolicyVersion}";

    /// <summary>
    /// Printed currency. The value itself stays decimal; only what is shown
    /// is a string, and it is invariant so a report, a page and a test all
    /// print the same characters.
    /// </summary>
    public static string FormatMoney(decimal value) =>
        value.ToString("£#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// V = round(B x 20%) when commercial VAT applies; A = B + V;
    /// T = round(A x p) for a prior total loss; proposal =
    /// round(A - T + additions - condition deduction). Every step rounds to
    /// whole pounds away from zero, which is the order the business works in.
    /// </summary>
    public static ValuationCalculation Calculate(ValuationCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Additions);
        if (input.GuideRetailValue <= 0m)
        {
            throw new InvalidOperationException(
                "A guide retail value is required before a valuation can be calculated.");
        }

        RequireAmount(input.GuideRetailValue, "guide retail value", nameof(input));
        RequireAmount(input.ConditionDeduction, "condition deduction", nameof(input));
        if (input.Additions.Count > MaximumAdditions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"A valuation carries at most {MaximumAdditions} additions.");
        }
        foreach (var addition in input.Additions)
        {
            ArgumentNullException.ThrowIfNull(addition);
            RequireAmount(addition.Amount, "valuation addition", nameof(input));
        }

        var percentage = ValidatePriorTotalLossPercentage(
            input.PriorTotalLossPercentage,
            nameof(input));

        // Registration is the claimant's own VAT position: it does not
        // suppress a commercial addition, it means there was never one to
        // add. The flag is therefore cleared here rather than refused, and
        // the snapshot records that it was not applied.
        var commercialVatApplied = input.CommercialVat && !input.ClaimantVatRegistered;
        var vat = commercialVatApplied ? Whole(input.GuideRetailValue * CommercialVatRate) : 0m;
        var valueIncludingVat = input.GuideRetailValue + vat;
        var priorTotalLoss = percentage is null ? 0m : Whole(valueIncludingVat * percentage.Value);
        var additionsTotal = input.Additions.Sum(addition => addition.Amount);
        var proposal = Whole(
            valueIncludingVat - priorTotalLoss + additionsTotal - input.ConditionDeduction);
        if (proposal < 0m)
        {
            throw new InvalidOperationException(
                "The valuation deductions exceed the value, so there is no figure to apply.");
        }

        return new(
            input.GuideRetailValue,
            commercialVatApplied,
            vat,
            valueIncludingVat,
            percentage,
            priorTotalLoss,
            [.. input.Additions],
            additionsTotal,
            input.ConditionDeduction,
            proposal);
    }

    /// <summary>
    /// Turns what the Case posted into what is calculated and recorded. The
    /// maintained label and suggested amount come from the preset record, not
    /// from the form, so a stale or tampered copy cannot enter the snapshot;
    /// a disabled preset stays readable in history but cannot be selected
    /// again.
    /// </summary>
    public static ValuationCalculationInput Resolve(
        ValuationCalculationSelection selection,
        ValuationCalculationBasis basis)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(selection.Additions);
        ArgumentNullException.ThrowIfNull(basis.Presets);
        return new(
            basis.GuideRetailValue,
            selection.CommercialVat,
            basis.ClaimantVatRegistered,
            selection.PriorTotalLossPercentage,
            [.. selection.Additions.Select(item => Resolve(item, basis.Presets))],
            selection.ConditionDeduction);
    }

    /// <summary>
    /// The selection rules both the preview and the Apply command apply, so a
    /// figure that would be refused on adoption is refused while it is still
    /// being previewed.
    /// </summary>
    public static ValuationCalculationSelection ValidateSelection(
        ValuationCalculationSelection selection,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.Additions);
        if (selection.GuideValuationId == Guid.Empty)
        {
            throw new ArgumentException(
                "A recorded guide valuation must be selected as the basis.",
                parameterName);
        }
        if (selection.Additions.Count > MaximumAdditions)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"A valuation carries at most {MaximumAdditions} additions.");
        }
        RequireAmount(selection.ConditionDeduction, "condition deduction", parameterName);
        ValidatePriorTotalLossPercentage(selection.PriorTotalLossPercentage, parameterName);
        return selection with
        {
            Additions = [.. selection.Additions.Select(NormalizeSelection)]
        };
    }

    public static PreviewValuationRequest ValidatePreview(PreviewValuationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        return request with
        {
            Selection = ValidateSelection(request.Selection, nameof(request))
        };
    }

    public static ApplyValuationRequest ValidateApply(ApplyValuationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaseLifecycleRules.ValidateMutation(request);
        AssessmentPolicy.RequireFindingConfirmationAuthority(request.Actor);
        if (request.CorrectedEngineerValue is { } corrected)
        {
            RequireAmount(corrected, "corrected Engineer's value", nameof(request));
        }

        return request with
        {
            Selection = ValidateSelection(request.Selection, nameof(request))
        };
    }

    /// <summary>
    /// The figure adopted as the professional finding: the calculated
    /// proposal, or the Engineer's own corrected figure over the same basis.
    /// It must be a value the confirmed field can hold, so a zero adoption is
    /// refused here rather than at the field write.
    /// </summary>
    public static decimal AcceptedValue(
        ApplyValuationRequest request,
        ValuationCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(calculation);
        var accepted = request.CorrectedEngineerValue ?? calculation.Proposal;
        if (accepted <= 0m)
        {
            throw new InvalidOperationException(
                "An Engineer's Value must be greater than zero.");
        }

        return accepted;
    }

    private static ValuationAddition Resolve(
        ValuationAdditionSelection selection,
        IReadOnlyList<ValuationPreset> presets)
    {
        if (selection.PresetId == Guid.Empty)
        {
            return new(
                Guid.Empty,
                0,
                NormalizeLabel(selection.Label, nameof(selection)),
                null,
                selection.Amount);
        }

        var preset = presets.SingleOrDefault(item => item.Id == selection.PresetId);
        if (preset is null || !preset.Active)
        {
            throw new ValuationPresetException(ValuationPresetError.NotSelectable);
        }
        if (preset.Version != selection.PresetVersion)
        {
            throw new ValuationPresetException(
                ValuationPresetError.VersionConflict,
                preset.Version);
        }

        return new(
            preset.Id,
            preset.Version,
            preset.Label,
            preset.SuggestedAmount,
            selection.Amount);
    }

    private static ValuationAdditionSelection NormalizeSelection(
        ValuationAdditionSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        RequireAmount(selection.Amount, "valuation addition", nameof(selection));
        if (selection.PresetId == Guid.Empty)
        {
            return selection with
            {
                PresetVersion = 0,
                Label = NormalizeLabel(selection.Label, nameof(selection))
            };
        }
        if (selection.PresetVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                "A selected valuation preset carries the version it was read at.");
        }

        return selection with { Label = null };
    }

    internal static string NormalizeLabel(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A custom valuation addition requires a label.",
                parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > MaximumAdditionLabelLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"A label cannot exceed {MaximumAdditionLabelLength} characters "
                + "or contain control characters.");
        }

        return normalized;
    }

    private static decimal? ValidatePriorTotalLossPercentage(
        decimal? percentage,
        string parameterName)
    {
        if (percentage is null)
        {
            return null;
        }
        if (!PriorTotalLossPercentages.Contains(percentage.Value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A prior total loss is recorded at 10% or 20%.");
        }

        return percentage;
    }

    internal static void RequireAmount(decimal value, string description, string parameterName)
    {
        if (value < 0m || decimal.Round(value, 2) != value)
        {
            throw new ArgumentException(
                $"The {description} must be a non-negative amount with at most two decimal places.",
                parameterName);
        }
    }

    private static decimal Whole(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}

public sealed class ListValuationPresets(IValuationPresetStore store) : IListValuationPresets
{
    public Task<IReadOnlyList<ValuationPreset>> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return store.ListAsync(cancellationToken);
    }
}

public sealed class SaveValuationPreset(IValuationPresetStore store) : ISaveValuationPreset
{
    public Task<ValuationPreset> ExecuteAsync(
        SaveValuationPresetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageWorkflowConfiguration);
        if (request.PresetId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException(
                "A valuation preset identity and expected version are required.",
                nameof(request));
        }
        ValuationCalculationPolicy.RequireAmount(
            request.SuggestedAmount,
            "suggested amount",
            nameof(request));

        return store.SaveAsync(
            request with
            {
                Label = ValuationCalculationPolicy.NormalizeLabel(
                    request.Label,
                    nameof(request)),
                Reason = RequireText(request.Reason, 1000, nameof(request)),
                OperationKey = RequireText(request.OperationKey, 100, nameof(request))
            },
            cancellationToken);
    }

    private static string RequireText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters "
                + "or contain control characters.");
        }

        return normalized;
    }
}

/// <summary>
/// Shows the Engineer what the selection comes to. It reads the same basis
/// and runs the same arithmetic the adoption will, and writes nothing: an
/// Engineer's Value changes only when Apply is pressed.
/// </summary>
public sealed class PreviewValuationCalculation(IAppliedValuationStore store)
    : IPreviewValuationCalculation
{
    public async Task<ValuationPreview> ExecuteAsync(
        PreviewValuationRequest request,
        CancellationToken cancellationToken)
    {
        request = ValuationCalculationPolicy.ValidatePreview(request);
        var basis = await store.ReadBasisAsync(
            request.CaseId,
            request.Selection.GuideValuationId,
            cancellationToken);
        return new(
            basis.GuideValuationId,
            basis.GuideValuationStampUtc,
            ValuationCalculationPolicy.Calculate(
                ValuationCalculationPolicy.Resolve(request.Selection, basis)));
    }
}

public sealed class ApplyValuationCalculation(IAppliedValuationStore store)
    : IApplyValuationCalculation
{
    public Task<AppliedValuation> ExecuteAsync(
        ApplyValuationRequest request,
        CancellationToken cancellationToken) =>
        store.ApplyAsync(
            ValuationCalculationPolicy.ValidateApply(request),
            cancellationToken);
}

public sealed class ListAppliedValuations(IAppliedValuationStore store) : IListAppliedValuations
{
    public Task<IReadOnlyList<AppliedValuation>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return store.ListAppliedAsync(caseId, cancellationToken);
    }
}
