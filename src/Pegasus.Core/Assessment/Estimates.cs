using System.Globalization;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Which repairer VAT position the estimate stands on (B04). Unknown is a
/// real state, not a missing value: it blocks Use as Current until the
/// operator records an explicit status or explicitly selects the VAT
/// categories. The claimant's VAT position never controls estimate VAT.
/// </summary>
public enum RepairerVatStatus
{
    Unknown,
    Registered,
    NotRegistered,
}

/// <summary>
/// The cost categories the estimate's VAT percentage applies to. Labour
/// covers both panel and paint labour; Materials covers row materials and
/// the estimate's additional materials.
/// </summary>
[Flags]
public enum EstimateVatCategories
{
    None = 0,
    Labour = 1,
    Parts = 2,
    Materials = 4,
    Specialist = 8,
    All = Labour | Parts | Materials | Specialist,
}

/// <summary>
/// Which categories the estimate's VAT percentage is charged on, and why.
/// The percentage itself stays on <see cref="EstimateDetails.VatPercent"/>
/// (D9) — this record never carries a second copy of it.
/// <see cref="CategoriesOverridden"/> records that the operator chose the
/// categories by hand instead of taking them from the repairer's status.
/// </summary>
public sealed record EstimateVatPolicy(
    RepairerVatStatus RepairerStatus,
    EstimateVatCategories Categories,
    bool CategoriesOverridden)
{
    /// <summary>Registered charges all four; not registered charges parts and materials.</summary>
    public static EstimateVatCategories DefaultFor(RepairerVatStatus status) => status switch
    {
        RepairerVatStatus.Registered => EstimateVatCategories.All,
        RepairerVatStatus.NotRegistered => EstimateVatCategories.Parts | EstimateVatCategories.Materials,
        RepairerVatStatus.Unknown => EstimateVatCategories.None,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static EstimateVatPolicy For(RepairerVatStatus status) =>
        new(status, DefaultFor(status), false);

    /// <summary>
    /// An unknown repairer VAT status blocks acceptance until the operator
    /// records the status or overrides the categories by hand.
    /// </summary>
    public bool BlocksAcceptance =>
        RepairerStatus == RepairerVatStatus.Unknown && !CategoriesOverridden;

    public bool Charges(EstimateVatCategories category) => (Categories & category) == category;
}

/// <summary>
/// The four estimate discounts as fractions in [0,1]: parts, materials,
/// specialist (which also carries off-pattern amounts), and the overall
/// discount applied after the other three.
/// </summary>
public sealed record EstimateDiscounts(
    decimal Parts,
    decimal Materials,
    decimal Specialist,
    decimal Overall)
{
    public static EstimateDiscounts None { get; } = new(0m, 0m, 0m, 0m);
}

/// <summary>
/// The labour rate the estimate was priced at, and the rate card version it
/// was taken from when one exists. One rate prices both panel and paint
/// hours (B04); there is no second paint rate in the arithmetic.
/// </summary>
public sealed record EstimateRateSnapshot(
    Guid? RateCardId,
    long? RateCardVersion,
    decimal HourlyRate);

/// <summary>
/// The editable header of one named estimate on a Case (EPIC-011 §1.9,
/// FRD-11 § Estimate VAT on the rendered report). Money is in pounds to two
/// places; the one labour rate is per hour; the VAT percentage is free per
/// estimate (D9).
/// <see cref="PaintMaterials"/> is the estimate's additional materials.
/// </summary>
/// <remarks>
/// D9 reconciliation: the B04 plan writes VAT as 20 %, this repository keeps
/// the free per-estimate percentage and computes VAT as
/// <c>Taxable × VatPercent / 100</c>. The percentage never states a
/// repairer's VAT position: an estimate that records no <see cref="Vat"/>
/// policy stands on <see cref="RepairerVatStatus.Unknown"/> and charges VAT
/// on nothing until an Engineer records the status or selects the
/// categories, which is also what blocks it from being made Current.
/// </remarks>
public sealed record EstimateDetails(
    string Name,
    int? RepairDays,
    decimal? LabourRate,
    decimal? PaintMaterials,
    decimal? OtherCosts,
    decimal VatPercent,
    string? Notes,
    EstimateDiscounts? Discounts = null,
    EstimateVatPolicy? Vat = null,
    EstimateRateSnapshot? Rate = null)
{
    /// <summary>The one rate that prices panel and paint hours alike.</summary>
    public decimal HourlyRate => Rate?.HourlyRate ?? LabourRate ?? 0m;

    public EstimateDiscounts AppliedDiscounts => Discounts ?? EstimateDiscounts.None;

    public EstimateVatPolicy VatPolicy => Vat ?? EstimateVatPolicy.For(RepairerVatStatus.Unknown);
}

/// <summary>
/// The estimate editor's line operations. The persisted vocabulary stays
/// <see cref="EstimateLineCodes.Types"/>; this is the one mapping between
/// the two, so neither the screen nor a parser invents its own.
/// </summary>
public enum EstimateOperation
{
    Replace,
    Repair,
    RemoveAndRefit,
    Paint,
    Blend,
    Specialist,
    Other,
}

public static class EstimateOperations
{
    /// <summary>The line type an operation lands as when nothing finer is known.</summary>
    public static string ToLineType(EstimateOperation operation) => operation switch
    {
        EstimateOperation.Replace => "new_part",
        EstimateOperation.Repair => "repair",
        EstimateOperation.RemoveAndRefit => "rnr",
        EstimateOperation.Paint => "paint_repair",
        EstimateOperation.Blend => "paint_blend",
        EstimateOperation.Specialist => "specialist_fixed",
        EstimateOperation.Other => "check_labour",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static EstimateOperation FromLineType(string lineType) => lineType switch
    {
        "new_part" => EstimateOperation.Replace,
        "repair" => EstimateOperation.Repair,
        "rnr" => EstimateOperation.RemoveAndRefit,
        "paint_new" or "paint_repair" or "paint_prep" => EstimateOperation.Paint,
        "paint_blend" => EstimateOperation.Blend,
        "specialist_fixed" or "specialist_wu" => EstimateOperation.Specialist,
        "check_labour" => EstimateOperation.Other,
        _ => throw new InvalidOperationException($"Unknown estimate line type '{lineType}'."),
    };

    public static bool TryParse(string? value, out EstimateOperation operation)
    {
        switch (value?.Trim())
        {
            case "Replace": operation = EstimateOperation.Replace; return true;
            case "Repair": operation = EstimateOperation.Repair; return true;
            case "R&I" or "RemoveAndRefit": operation = EstimateOperation.RemoveAndRefit; return true;
            case "Paint": operation = EstimateOperation.Paint; return true;
            case "Blend": operation = EstimateOperation.Blend; return true;
            case "Specialist": operation = EstimateOperation.Specialist; return true;
            case "Other": operation = EstimateOperation.Other; return true;
            default: operation = default; return false;
        }
    }
}

/// <summary>
/// One estimate line's values as they stood when the line was imported, so
/// an amendment never erases what the source document said.
/// </summary>
public sealed record EstimateLineOrigin(
    string Type,
    string? Description,
    string? PartNumber,
    int? Quantity,
    decimal? WorkUnits,
    decimal? PaintWorkUnits,
    decimal? Price,
    decimal? Materials);

/// <summary>
/// A value the estimate carries that its own line type does not price —
/// paint hours on a panel line, a unit amount on a labour line. The value is
/// retained and reported, never dropped and never silently re-bucketed.
/// </summary>
public sealed record EstimateAnomaly(
    int Position,
    string Field,
    decimal Value,
    string Reason);

/// <summary>
/// The unrounded arithmetic of one estimate, in the plan's own terms:
/// <c>Category = P(1-dP) + L + Q + M(1-dM) + (S+O)(1-dS)</c>,
/// <c>Net = Category(1-dA)</c>, <c>Taxable</c> the selected discounted
/// categories, <c>Vat = Taxable x VatPercent / 100</c>,
/// <c>Gross = Net + Vat</c>. The five components are already discounted,
/// overall discount included, so they sum to <see cref="Net"/> exactly.
/// </summary>
public sealed record EstimateRawTotals(
    decimal Parts,
    decimal PanelLabour,
    decimal PaintLabour,
    decimal Materials,
    decimal Specialist,
    decimal OffPattern,
    decimal Category,
    decimal Net,
    decimal Taxable,
    decimal Vat,
    decimal Gross);

/// <summary>
/// The printed projection of <see cref="EstimateRawTotals"/>: each of the
/// five components and the VAT rounded independently to two decimals away
/// from zero, printed Net the sum of the printed components, printed Gross
/// printed Net plus printed VAT. A residual penny is never moved into VAT or
/// into a category to make the raw and printed figures agree.
/// </summary>
public sealed record EstimatePrintedTotals(
    decimal Parts,
    decimal PanelLabour,
    decimal PaintLabour,
    decimal Materials,
    decimal Specialist,
    decimal Net,
    decimal Vat,
    decimal Gross);

/// <summary>
/// The single owner of estimate money (FRD-11 § Estimate VAT on the rendered
/// report, plan B04). Nothing else in the application adds up an estimate.
/// </summary>
public sealed record EstimateTotals(
    EstimateRawTotals Raw,
    EstimatePrintedTotals Printed,
    EstimateVatPolicy VatPolicy,
    decimal VatPercent,
    int CalculationPolicyVersion,
    IReadOnlyList<EstimateAnomaly> OffPattern)
{
    public static EstimateTotals Compute(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        var details = estimate.Details;
        var discounts = details.AppliedDiscounts;
        var rate = details.HourlyRate;
        var anomalies = new List<EstimateAnomaly>();

        decimal parts = 0m, panelHours = 0m, paintHours = 0m;
        decimal materials = details.PaintMaterials ?? 0m;
        decimal specialist = details.OtherCosts ?? 0m;
        decimal offPattern = 0m;
        foreach (var line in estimate.Lines)
        {
            var operation = EstimateOperations.FromLineType(line.Type);
            var quantity = line.Quantity is { } supplied && supplied > 0 ? supplied : 1;
            var amount = (line.Price ?? 0m) * quantity;
            materials += line.Materials ?? 0m;

            switch (operation)
            {
                case EstimateOperation.Replace:
                    parts += amount;
                    panelHours += line.WorkUnits ?? 0m;
                    break;
                case EstimateOperation.Specialist:
                    // Specialist hours are displayed, never multiplied by the rate.
                    specialist += amount;
                    break;
                case EstimateOperation.Paint or EstimateOperation.Blend:
                    paintHours += line.PaintWorkUnits ?? 0m;
                    panelHours += line.WorkUnits ?? 0m;
                    offPattern += OffPatternAmount(line, amount, anomalies);
                    break;
                default:
                    panelHours += line.WorkUnits ?? 0m;
                    offPattern += OffPatternAmount(line, amount, anomalies);
                    break;
            }

            if (operation is not (EstimateOperation.Paint or EstimateOperation.Blend)
                && line.PaintWorkUnits is { } strayPaintHours && strayPaintHours != 0m)
            {
                anomalies.Add(new(
                    line.Position, "paint hours", strayPaintHours,
                    "Paint hours on a line that is not Paint or Blend are retained but not priced."));
            }
        }

        var panelLabour = panelHours * rate;
        var paintLabour = paintHours * rate;
        var discountedParts = parts * (1m - discounts.Parts);
        var discountedMaterials = materials * (1m - discounts.Materials);
        var discountedSpecialist = (specialist + offPattern) * (1m - discounts.Specialist);
        var category = discountedParts + panelLabour + paintLabour + discountedMaterials + discountedSpecialist;
        var overall = 1m - discounts.Overall;

        var rawParts = discountedParts * overall;
        var rawPanelLabour = panelLabour * overall;
        var rawPaintLabour = paintLabour * overall;
        var rawMaterials = discountedMaterials * overall;
        var rawSpecialist = discountedSpecialist * overall;
        var net = category * overall;

        var policy = details.VatPolicy;
        var taxable =
            (policy.Charges(EstimateVatCategories.Parts) ? rawParts : 0m)
            + (policy.Charges(EstimateVatCategories.Labour) ? rawPanelLabour + rawPaintLabour : 0m)
            + (policy.Charges(EstimateVatCategories.Materials) ? rawMaterials : 0m)
            + (policy.Charges(EstimateVatCategories.Specialist) ? rawSpecialist : 0m);
        var vat = taxable * details.VatPercent / 100m;

        var raw = new EstimateRawTotals(
            rawParts, rawPanelLabour, rawPaintLabour, rawMaterials, rawSpecialist,
            offPattern, category, net, taxable, vat, net + vat);

        var printedParts = Pence(rawParts);
        var printedPanelLabour = Pence(rawPanelLabour);
        var printedPaintLabour = Pence(rawPaintLabour);
        var printedMaterials = Pence(rawMaterials);
        var printedSpecialist = Pence(rawSpecialist);
        var printedNet = printedParts + printedPanelLabour + printedPaintLabour
            + printedMaterials + printedSpecialist;
        var printedVat = Pence(vat);
        var printed = new EstimatePrintedTotals(
            printedParts, printedPanelLabour, printedPaintLabour, printedMaterials,
            printedSpecialist, printedNet, printedVat, printedNet + printedVat);

        return new(
            raw, printed, policy, details.VatPercent,
            RepairSpecificationPolicy.PolicyVersion, anomalies);
    }

    private static decimal OffPatternAmount(
        CaseEstimateLineRecord line, decimal amount, List<EstimateAnomaly> anomalies)
    {
        if (amount == 0m)
        {
            return 0m;
        }
        anomalies.Add(new(
            line.Position, "unit amount", amount,
            "A unit amount on a line that prices no part is retained in specialist treatment."));
        return amount;
    }

    private static decimal Pence(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Validation and actor rules for named estimates. Staff work is a staff
/// Engineer act (<see cref="RepairSpecificationPolicy.RequireEngineer"/>);
/// the Automation actor may only create or update <c>AiDraft</c> estimates
/// that cite the Estimate job they fulfil (FRD-10 § AI job and estimate
/// tools), and only a Draft is editable — an accepted estimate is duplicated,
/// never changed.
/// </summary>
public static class EstimatePolicy
{
    public const int MaximumNameLength = 100;
    public const int MaximumNotesLength = 4000;
    public const decimal DefaultVatPercent = 20m;
    public const string CopySuffix = " copy";

    /// <summary>
    /// The precision an estimate line's hours are kept to. A provider states
    /// time in its own unit — Glass's in sixtieths of an hour, Audatex to two
    /// places — so B04 retains the figure the document printed instead of
    /// rounding it to the editor's 0.1 step, and the persisted column is
    /// <c>decimal(18,6)</c>.
    /// </summary>
    public const int WorkUnitDecimals = 6;

    /// <summary>The bound on one line's hours; beyond it the figure is not time.</summary>
    public const decimal MaximumLineWorkUnits = 1_000m;

    public static EstimateDetails ValidateDetails(EstimateDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var name = details.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("An estimate name is required.", nameof(details));
        }
        if (name.Length > MaximumNameLength || name.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"An estimate name cannot exceed {MaximumNameLength} characters or contain control characters.",
                nameof(details));
        }
        if (details.RepairDays is < 0)
        {
            throw new ArgumentException("Repair days cannot be negative.", nameof(details));
        }
        Money(details.LabourRate, "labour rate");
        Money(details.PaintMaterials, "paint materials");
        Money(details.OtherCosts, "other costs");
        if (details.VatPercent is < 0 or > 100 || decimal.Round(details.VatPercent, 2) != details.VatPercent)
        {
            throw new ArgumentException(
                "The VAT percentage must be between 0 and 100 with at most two decimal places.",
                nameof(details));
        }
        var notes = string.IsNullOrWhiteSpace(details.Notes) ? null : details.Notes.Trim();
        if (notes is { Length: > MaximumNotesLength })
        {
            throw new ArgumentException(
                $"Estimate notes cannot exceed {MaximumNotesLength} characters.",
                nameof(details));
        }
        if (details.Discounts is { } discounts)
        {
            ValidateDiscounts(discounts);
        }
        if (details.Vat is { } vat)
        {
            ValidateVatPolicy(vat);
        }
        if (details.Rate is { } rate)
        {
            Money(rate.HourlyRate, "labour rate");
        }
        return details with { Name = name, Notes = notes };
    }

    /// <summary>
    /// Every discount is a fraction of one, in [0,1], to at most four
    /// decimal places — the precision the estimate header stores.
    /// </summary>
    public static EstimateDiscounts ValidateDiscounts(EstimateDiscounts discounts)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        Fraction(discounts.Parts, "parts discount");
        Fraction(discounts.Materials, "materials discount");
        Fraction(discounts.Specialist, "specialist discount");
        Fraction(discounts.Overall, "overall discount");
        return discounts;
    }

    /// <summary>
    /// Categories that were not overridden by hand must be the repairer
    /// status's own defaults, so the recorded policy and the status can
    /// never disagree about why VAT is charged.
    /// </summary>
    public static EstimateVatPolicy ValidateVatPolicy(EstimateVatPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if ((policy.Categories & ~EstimateVatCategories.All) != 0)
        {
            throw new ArgumentException("Unknown estimate VAT categories.", nameof(policy));
        }
        if (!policy.CategoriesOverridden
            && policy.Categories != EstimateVatPolicy.DefaultFor(policy.RepairerStatus))
        {
            throw new ArgumentException(
                "VAT categories that differ from the repairer's status must be recorded as an override.",
                nameof(policy));
        }
        return policy;
    }

    private static void Fraction(decimal value, string description)
    {
        if (value is < 0m or > 1m || decimal.Round(value, 4) != value)
        {
            throw new ArgumentException(
                $"The {description} must be a fraction between 0 and 1 with at most four decimal places.",
                nameof(value));
        }
    }

    public static SaveEstimateRequest ValidateSave(SaveEstimateRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Lines);
        if (request.EstimateId == Guid.Empty || request.AiJobId == Guid.Empty)
        {
            throw new ArgumentException("An identifier cannot be empty when supplied.", nameof(request));
        }
        var source = RepairSpecificationPolicy.ValidateSource(request.Source);
        switch (request.Actor.Kind)
        {
            case ActorKind.Automation when source.Route != RepairSpecificationSourceRoute.AiDraft:
                throw new InvalidOperationException(
                    "The Automation actor can only save AI-draft estimates.");
            case ActorKind.Automation when request.AiJobId is null:
                throw new InvalidOperationException(
                    "An AI-draft estimate must cite the Estimate job it fulfils.");
            case ActorKind.Automation:
                break;
            default:
                RepairSpecificationPolicy.RequireEngineer(request.Actor);
                break;
        }
        return request with
        {
            Details = ValidateDetails(request.Details),
            Lines = AssessmentPolicy.NormalizeRepairSpecificationLines(request.Lines),
            Source = source,
        };
    }

    /// <summary>
    /// One estimate line's money and time. The shared line normalizer calls
    /// this for every path that writes an estimate line, so panel hours,
    /// paint hours and row materials have exactly one rule between the
    /// editor, the importers and the assessment draft.
    /// </summary>
    public static void ValidateLineAmounts(EstimateLineInput line)
    {
        ArgumentNullException.ThrowIfNull(line);
        WorkUnits(line.WorkUnits, "work units");
        WorkUnits(line.PaintWorkUnits, "paint work units");
        Money(line.Materials, "line materials amount");
    }

    private static void WorkUnits(decimal? value, string description)
    {
        if (value is { } hours
            && (hours < 0 || hours > MaximumLineWorkUnits
                || decimal.Round(hours, WorkUnitDecimals) != hours))
        {
            throw new ArgumentException(
                $"Estimate {description} must be between 0 and {MaximumLineWorkUnits} "
                + $"with at most {WorkUnitDecimals} decimal places.",
                nameof(value));
        }
    }

    /// <summary>
    /// The job an AI draft cites must be an Estimate job on this case that
    /// the saving client currently holds (Taken under an unexpired lease).
    /// A staff Engineer editing an AI draft keeps its job reference; the
    /// job then only has to be an Estimate job on this case.
    /// </summary>
    public static void ValidateCitedJob(AiJobRecord? job, SaveEstimateRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (job is null)
        {
            throw new InvalidOperationException("The cited AI job was not found.");
        }
        if (job.Kind != AiJobKind.Estimate || job.SubjectId != request.CaseId)
        {
            throw new InvalidOperationException("The cited AI job is not an Estimate job on this case.");
        }
        if (request.Actor.Kind != ActorKind.Automation)
        {
            return;
        }
        var state = AiJobPolicy.EffectiveState(job.State, job.ExpiresAtUtc, job.LeaseExpiresAtUtc, now);
        if (state != AiJobState.Taken
            || !string.Equals(job.TakenBy, request.Actor.SubjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The cited AI job is not taken by this client.");
        }
    }

    public static void ValidateEditable(RepairSpecificationVersion estimate, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(actor);
        if (estimate.State != RepairSpecificationState.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft estimate can be changed; duplicate an accepted estimate to revise it.");
        }
        if (actor.Kind == ActorKind.Automation
            && estimate.Source.Route != RepairSpecificationSourceRoute.AiDraft)
        {
            throw new InvalidOperationException(
                "The Automation actor can only change AI-draft estimates.");
        }
    }

    public static void ValidateDuplicate(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (estimate.State == RepairSpecificationState.Discarded)
        {
            throw new InvalidOperationException("A discarded estimate cannot be duplicated.");
        }
    }

    public static void ValidateDiscard(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (estimate.State == RepairSpecificationState.Accepted || estimate.IsCurrent)
        {
            throw new InvalidOperationException("An accepted estimate cannot be discarded.");
        }
        if (estimate.State == RepairSpecificationState.Discarded)
        {
            throw new InvalidOperationException("The estimate is already discarded.");
        }
    }

    /// <summary>
    /// Making an estimate Current is the Engineer's acceptance (FRD-11 § AI
    /// Job List: "Use estimate"). A Draft passes
    /// <see cref="RepairSpecificationPolicy.ValidateAcceptance"/> with the
    /// basis derived by <see cref="EstimateTotals"/>; an already accepted
    /// estimate is simply switched to.
    /// </summary>
    public static RepairCalculationBasis BasisFor(RepairSpecificationVersion estimate) =>
        BasisFor(EstimateTotals.Compute(estimate));

    /// <summary>
    /// The same basis from a calculation already made, so a caller that also
    /// records the breakdown (the store, on acceptance) computes once.
    /// </summary>
    public static RepairCalculationBasis BasisFor(EstimateTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);
        var printed = totals.Printed;
        return new(
            printed.PanelLabour,
            printed.Parts,
            printed.PaintLabour + printed.Materials,
            printed.Specialist,
            totals.VatPolicy.RepairerStatus == RepairerVatStatus.Registered,
            printed.Vat,
            printed.Gross,
            $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}",
            totals.VatPolicy,
            printed);
    }

    public static void ValidateSetCurrent(RepairSpecificationVersion estimate, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        RepairSpecificationPolicy.RequireEngineer(actor);
        switch (estimate.State)
        {
            case RepairSpecificationState.Draft:
                if (estimate.Details.VatPolicy.BlocksAcceptance)
                {
                    throw new InvalidOperationException(
                        "Record the repairer's VAT status, or select the VAT categories, before using this estimate.");
                }
                RepairSpecificationPolicy.ValidateAcceptance(
                    estimate with { CalculationBasis = BasisFor(estimate) },
                    actor);
                break;
            case RepairSpecificationState.Accepted:
                break;
            default:
                throw new InvalidOperationException(
                    $"A {estimate.State.ToString().ToLowerInvariant()} estimate cannot be made current.");
        }
    }

    private static void Money(decimal? value, string description)
    {
        if (value is { } amount && (amount < 0 || decimal.Round(amount, 2) != amount))
        {
            throw new ArgumentException(
                $"The {description} must be a non-negative amount with at most two decimal places.",
                nameof(value));
        }
    }
}

/// <summary>
/// Create (<see cref="EstimateId"/> null) or replace the whole content of a
/// Draft estimate: header, ordered lines and source provenance, under the
/// same actor, lease, version and operation-key guards as every case
/// mutation. <see cref="AiJobId"/> is required for an AI draft.
/// </summary>
public sealed record SaveEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid? EstimateId,
    EstimateDetails Details,
    IReadOnlyList<EstimateLineInput> Lines,
    RepairSpecificationSource Source,
    Guid? AiJobId = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record DuplicateEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EstimateId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

/// <summary>The reason is the discard reason and is recorded on the estimate.</summary>
public sealed record DiscardEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EstimateId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record SetCurrentEstimateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EstimateId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ISaveEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(SaveEstimateRequest request, CancellationToken cancellationToken);
}

public interface IDuplicateEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(DuplicateEstimateRequest request, CancellationToken cancellationToken);
}

public interface IDiscardEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(DiscardEstimateRequest request, CancellationToken cancellationToken);
}

public interface ISetCurrentEstimate
{
    Task<RepairSpecificationVersion> ExecuteAsync(SetCurrentEstimateRequest request, CancellationToken cancellationToken);
}

public interface IListCaseEstimates
{
    Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// The bounded cursor-page projection of a <see
/// cref="RepairSpecificationVersion"/> (CASE-047, Stream A review): the
/// header fields a list surface needs, without embedding the
/// specification's <see cref="RepairSpecificationVersion.Lines"/> — a case
/// can carry many superseded versions and each an unbounded line list, so a
/// keyset page never grows with a specification's line count. A caller
/// wanting the lines reads the version directly
/// (<see cref="IRepairSpecificationStore.GetVersionAsync"/>).
/// </summary>
public sealed record CaseEstimatePageItem(
    Guid SpecificationId,
    Guid CaseId,
    int Version,
    RepairSpecificationState State,
    RepairSpecificationSource Source,
    string Name,
    bool IsCurrent,
    RepairCalculationBasis? CalculationBasis);

/// <summary>
/// The keyset-paged sibling of <see cref="IListCaseEstimates"/> (CASE-047,
/// requested by Stream A's MCP adapters): newest version first, then
/// estimate id.
/// </summary>
public interface IListCaseEstimatesByCursor
{
    Task<CursorPage<CaseEstimatePageItem>> ExecuteAsync(
        CaseListCursorQuery query,
        CancellationToken cancellationToken);
}

public sealed class SaveEstimate(
    IRepairSpecificationStore store,
    IAiJobStore jobs,
    TimeProvider timeProvider) : ISaveEstimate
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        SaveEstimateRequest request,
        CancellationToken cancellationToken)
    {
        var validated = EstimatePolicy.ValidateSave(request);
        if (validated.AiJobId is { } jobId)
        {
            var job = await jobs.GetAsync(jobId, cancellationToken);
            EstimatePolicy.ValidateCitedJob(job, validated, timeProvider.GetUtcNow());
        }
        return await store.SaveEstimateAsync(validated, cancellationToken);
    }
}

public sealed class DuplicateEstimate(IRepairSpecificationStore store) : IDuplicateEstimate
{
    public Task<RepairSpecificationVersion> ExecuteAsync(
        DuplicateEstimateRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        return store.DuplicateEstimateAsync(request, cancellationToken);
    }
}

public sealed class DiscardEstimate(IRepairSpecificationStore store) : IDiscardEstimate
{
    public Task<RepairSpecificationVersion> ExecuteAsync(
        DiscardEstimateRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        return store.DiscardEstimateAsync(request, cancellationToken);
    }
}

/// <summary>
/// The staff act that consumes an Estimate job's result: once the AI draft
/// is Current, the Draft-ready job it cites is confirmed Completed
/// (FRD-11 § AI Job List). A job in any other state is left as it is — the
/// Engineer's choice of estimate never depends on the ledger.
/// </summary>
public sealed class SetCurrentEstimate(
    IRepairSpecificationStore store,
    IAiJobStore jobs,
    IConfirmAiJob confirmJob,
    TimeProvider timeProvider) : ISetCurrentEstimate
{
    public async Task<RepairSpecificationVersion> ExecuteAsync(
        SetCurrentEstimateRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var current = await store.SetCurrentEstimateAsync(request, cancellationToken);
        if (current.AiJobId is not { } jobId)
        {
            return current;
        }
        var job = await jobs.GetAsync(jobId, cancellationToken);
        if (job is not null
            && AiJobPolicy.EffectiveState(job.State, job.ExpiresAtUtc, job.LeaseExpiresAtUtc, timeProvider.GetUtcNow())
                == AiJobState.DraftReady)
        {
            // Derived from the case operation key so a replayed set-current
            // replays the confirmation too; kept inside the ledger's own
            // 100-character key limit.
            var jobOperationKey = string.Concat(
                request.OperationKey.AsSpan(0, Math.Min(request.OperationKey.Length, 96)),
                ":job");
            await confirmJob.ExecuteAsync(
                new(job.JobId, job.Version, request.Actor, jobOperationKey),
                cancellationToken);
        }
        return current;
    }
}

public sealed class ListCaseEstimates(IRepairSpecificationStore store) : IListCaseEstimates
{
    public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        return store.ListEstimatesAsync(caseId, cancellationToken);
    }
}

/// <summary>
/// Applies the same actor boundary <see cref="Pegasus.Core.Cases.GetCase"/>
/// applies before reading a case's estimates, newest version first then
/// estimate id.
/// </summary>
public sealed class ListCaseEstimatesByCursor(IRepairSpecificationStore store, ICursorProtector protector)
    : IListCaseEstimatesByCursor
{
    public async Task<CursorPage<CaseEstimatePageItem>> ExecuteAsync(
        CaseListCursorQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }
        var limit = CursorPaging.NormalizeLimit(query.Limit);
        var scope = CaseListCursorScope.For("ListCaseEstimates", query.Actor, query.CaseId);

        int? afterVersion = null;
        Guid? afterId = null;
        if (query.Cursor is { Length: > 0 } cursor)
        {
            var position = protector.Unprotect(cursor, scope);
            if (!int.TryParse(position.SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
            {
                throw new CursorRejectedException("The cursor is malformed.");
            }
            afterVersion = version;
            afterId = position.Id;
        }

        var rows = await store.ListByCursorAsync(
            query.CaseId, afterVersion, afterId, limit + 1, cancellationToken);

        return CursorPageBuilder.Build(
            rows,
            limit,
            protector,
            scope,
            estimate => estimate.Version.ToString(CultureInfo.InvariantCulture),
            estimate => estimate.SpecificationId);
    }
}
