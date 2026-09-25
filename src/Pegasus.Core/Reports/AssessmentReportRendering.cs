using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json.Serialization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Reports;

public static class AssessmentReportContract
{
    public const string TemplateVersion = "rendererref1-v5";
    public const string VatNumber = "262 0937 10";
    public const decimal FeeVatRate = 0.20m;
    public const string AccountName = "Collision Engineers Ltd";
    public const string BankName = "Lloyds Bank";
    public const string SortCode = "30-12-80";
    public const string AccountNumber = "50858868";
    public const string RemittanceEmail = "accounts@collisionengineers.co.uk";
    public const string FeeTerms = "As per this agreement, following which we reserve the right to claim statutory interest at 8% above the Bank of England reference rate in force on the date the debt becomes overdue and at any subsequent rate where the reference rate changes and the debt remains unpaid, in accordance with the Late Payment of Commercial Debts (Interest) Act 1998 as amended and supplemented by the Late Payment of Commercial Debts Regulations 2002. Payment is due in full within 89 days from the date of this report unless otherwise stated. In addition, for unpaid debts up to £999.99 we are allowed to claim compensation of £40.00.";
    public const string AdditionalFeeTerms = "Any requests for addendum reports or letters, including those required for clarification, plus Counsel, Court or other meetings, will be subject to a further charge and subject to Civil Procedure Rule 35.6. The instructing party confirm to be liable for the charges of this report and any subsequent addendum reports on acceptance of this report by electronic mail. If you do not so wish to be bound by these terms you must reject the report and confirm so immediately.";
    public const string StatementOfTruth1 = "I declare that I understand my duty in providing this report to the court and I confirm that I have complied with that duty. I understand that this duty overrides any other obligation. The report is based upon instructions received.";
    public const string StatementOfTruth2 = "I confirm that I have made clear which facts and matters referred to in this report are within my own knowledge and which are not. Those that are within my own knowledge I confirm to be true. The opinions I have expressed represent my true and complete professional opinion on the matters to which they refer.";
    /// <summary>
    /// The accepted guide-disclosure sentence, verbatim from the accepted
    /// <c>StatementOfTruth3</c> paragraph. It names Glass's, so it is printed
    /// only when the operator has turned "Disclose guide source" on and a
    /// Glass's valuation guide was actually used. No replacement sentence is
    /// invented for another guide: the sentence is omitted (H5).
    /// </summary>
    public const string StatementOfTruthGuide = "We have used Glass's Evaluator to assist with the valuation of the vehicle and Thatcham and/or manufacturer's data to compile the repair specification.";
    public const string StatementOfTruth3 = "Parts prices are subject to fluctuation and further damage may be found upon dismantling the vehicle. Our valuation is based on the mileage information provided and assuming that the vehicle has a valid MOT certificate (where applicable) to support such.";
    public const string StatementOfTruth4 = "We appreciate your instructions and enclose our fee note for your kind attention, which we confirm remains payable irrespective of the outcome of this case. Please ensure this is passed to your accounts department.";

    /// <summary>
    /// Whether the accepted Glass's guide-disclosure sentence prints: the
    /// operator turned "Disclose guide source" on <em>and</em> a Glass's
    /// valuation guide was actually used. No sentence is substituted for
    /// another guide — the approved v3 specification supplies none.
    /// </summary>
    public static bool PrintsGuideDisclosure(CaseReportContentSwitches content, ReportGuideSources guides)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(guides);
        return content.DiscloseGuideSource && guides.UsesGlassesValuationGuide;
    }

    /// <summary>
    /// The accepted statement of truth in print order: the one owner. The PDF
    /// prints it and the Case's Report section shows it read-only; no Case
    /// edits it.
    /// </summary>
    public static IReadOnlyList<string> StatementOfTruth(CaseReportContentSwitches content, ReportGuideSources guides) =>
        PrintsGuideDisclosure(content, guides)
            ? [StatementOfTruth1, StatementOfTruth2, StatementOfTruthGuide, StatementOfTruth3, StatementOfTruth4]
            : [StatementOfTruth1, StatementOfTruth2, StatementOfTruth3, StatementOfTruth4];

    /// <summary>
    /// The statement this Case's report prints, from the content switches and
    /// guide sources a generation freezes; available before a report can be
    /// projected.
    /// </summary>
    public static IReadOnlyList<string> StatementOfTruthOf(AssessmentReportProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return StatementOfTruth(CaseReportReadiness.ContentOf(input.Assessment), input.Guides ?? ReportGuideSources.None);
    }

    /// <summary>
    /// The one salvage category a total-loss report prints: the active
    /// total-loss template has accepted wording for Category S only (operator,
    /// 24 September 2026). Readiness names any other category before a report
    /// is projected.
    /// </summary>
    public const string PrintableSalvageCategory = "S";

    /// <summary>Whether the active total-loss template has accepted wording for <paramref name="category"/>.</summary>
    public static bool PrintsSalvageCategory(string? category) =>
        string.Equals(category, PrintableSalvageCategory, StringComparison.Ordinal);
}

public enum AssessmentReportOutcome
{
    TotalLoss,
    Repairable,
    CashInLieu,
    ContractRepair,
}

/// <summary>
/// One custody-confirmed source document the report draws on. The printed
/// provenance triple is <see cref="Name"/>/<see cref="Version"/>/<see
/// cref="Sha256"/>; the logical and Box identifiers are carried so a frozen
/// generation snapshot pins the exact retained object, and are never printed.
/// </summary>
public sealed record AcceptedReportSource(
    string Name,
    string Version,
    string Sha256,
    Guid? DocumentId = null,
    Guid? VersionId = null,
    string? BoxFileId = null,
    string? BoxVersionId = null)
{
    public void Validate()
    {
        Required(Name, nameof(Name));
        Required(Version, nameof(Version));
        if (Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit))
        {
            throw new ReportRenderRejectedException("Every accepted source requires a SHA-256 hash.");
        }
    }

    internal static void Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReportRenderRejectedException($"{name} is required.");
        }
    }
}

public sealed record ReportVehicle(
    string Registration,
    string Make,
    string Model,
    string Year,
    string VehicleType,
    string Condition,
    string MileageDescription,
    string MileageSource,
    string? Vin,
    string? Engine,
    string? Fuel,
    string? Transmission,
    string? Colour,
    string? Body,
    DateOnly? TaxExpiry,
    DateOnly? MotExpiry,
    string? AirbagsDeployed,
    bool? TemporaryRepairsPossible,
    string? TemporaryRepairMethod,
    decimal? TemporaryRepairCost);

/// <summary>
/// One recorded damage as the report prints it: the area names, the severity
/// and note, the area codes, and the disc the operator drew (unit-plan terms;
/// null for a damage recorded by area alone, drawn from its codes).
/// </summary>
public sealed record ReportImpact(string Areas, string Severity, string Note, IReadOnlyList<string> Codes, DamageDisc? Disc = null);

public sealed record ReportDamage(
    IReadOnlyList<ReportImpact> Impacts,
    string? RightFrontTyre, string? LeftFrontTyre, string? RightRearTyre, string? LeftRearTyre,
    string? RightFrontBelt, string? LeftFrontBelt, string? RightRearBelt, string? LeftRearBelt,
    string? SpareTyre, string? CentreBelt,
    string? Unrelated, decimal? UnrelatedDeduction, string? MaterialTransfer);

public sealed record ReportSettlement(
    decimal? Excess,
    decimal? Betterment,
    bool? ClaimantVatRegistered,
    decimal? Reserve,
    decimal Equity,
    string? RepairDelays,
    string? ReportDelay,
    decimal? StoragePerDay,
    decimal? Recovery,
    DateOnly? HireStart,
    decimal? HireDailyCost,
    decimal? Diminution,
    string? SalvageAt,
    string? SalvageAgent,
    string? SalvageAgentReference,
    bool? SalvageMoved,
    bool? SalvageOwnerRetains,
    bool? SalvageValueAgreed,
    DateOnly? SalvageSettled,
    decimal? ContractSum = null);

/// <summary>
/// One prepared report image: the confirmed custody bytes plus the report
/// role, supporting order, rotation and crop an Engineer chose through
/// <see cref="CaseAssetPreparationPolicy"/>. The preparation values are
/// carried, never re-decided here. The retained source bytes are never
/// modified; the renderer prints a re-encoded print-resolution copy.
/// </summary>
public sealed record ReportImageEvidence(
    string CustodyReference,
    string ContentType,
    byte[] Content,
    string Sha256,
    CaseAssetReportRole Role = CaseAssetReportRole.Supporting,
    int? Order = null,
    CaseAssetRotation Rotation = CaseAssetRotation.None,
    CaseAssetCrop? Crop = null,
    Guid? OccurrenceId = null,
    Guid? VersionId = null,
    string? BoxFileId = null,
    string? BoxVersionId = null,
    bool FullPage = false)
{
    [JsonIgnore]
    public CaseAssetCrop AppliedCrop => Crop ?? CaseAssetCrop.Full;

    public static bool IsAcceptedContentType(string? contentType) =>
        contentType is "image/jpeg" or "image/png" or "image/webp";

    public void Validate()
    {
        AcceptedReportSource.Required(CustodyReference, nameof(CustodyReference));
        if (!IsAcceptedContentType(ContentType) || Content.Length == 0)
        {
            throw new ReportRenderRejectedException("Every report image requires accepted image bytes and content type.");
        }
        AppliedCrop.Validate();
        if (!Enum.IsDefined(Rotation))
        {
            throw new ReportRenderRejectedException(
                $"Report image '{CustodyReference}' carries an unrecognized rotation.");
        }
        var actual = Convert.ToHexStringLower(SHA256.HashData(Content));
        if (!actual.Equals(Sha256, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException("A report image did not match its custody hash.");
        }
    }
}

/// <summary>
/// The one owner of the renderer's wall-clock budget: the renderer adapter
/// and generation both cancel a render that runs past it, and neither keeps a
/// second copy. It bounds time only; every image the Engineer includes prints,
/// whatever their number or source file size (operator, 24 September 2026).
/// </summary>
public static class AssessmentReportRenderPolicy
{
    /// <summary>The wall-clock budget for one render.</summary>
    public static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(2);
}

/// <summary>
/// The report's repair-cost block: the Current estimate's canonical
/// <see cref="EstimateTotals"/> (the one owner of estimate money — EXT-09,
/// FRD-11 § Estimate VAT on the rendered report) plus the hours and rate the
/// report prints as descriptive quantities. Nothing here re-derives a figure,
/// and the VAT label is taken from the estimate's own percentage, never from
/// a repairer-registered boolean.
/// </summary>
public sealed record ReportRepairCosts(
    decimal LabourHours,
    decimal PaintHours,
    decimal HourlyRate,
    EstimateTotals Totals)
{
    /// <summary>The one mapping from a Current estimate to the report's cost block.</summary>
    public static ReportRepairCosts For(RepairSpecificationVersion estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        var hours = EstimateHours.Of(estimate);
        return new(
            hours.PricedPanel,
            hours.PricedPaint,
            estimate.Details.HourlyRate,
            EstimateTotals.ForProjection(estimate));
    }

    [JsonIgnore]
    public EstimatePrintedTotals Printed => Totals.Printed;

    [JsonIgnore]
    public decimal VatPercent => Totals.VatPercent;

    [JsonIgnore]
    public decimal Total => Totals.Printed.Gross;

    /// <summary>
    /// The printed VAT row's label, derived from the estimate's own
    /// percentage. The categories the percentage is charged on are the
    /// estimate's; this label never claims a different rule.
    /// </summary>
    [JsonIgnore]
    public string VatLabel =>
        $"VAT ({VatPercent.ToString("0.##", CultureInfo.InvariantCulture)}%)";

    /// <summary>
    /// Fails closed when the printed components do not reconcile to the
    /// printed total, or the report has no hourly rate to print.
    /// </summary>
    public void Validate()
    {
        if (HourlyRate <= 0m || LabourHours < 0m || PaintHours < 0m)
        {
            throw new ReportRenderRejectedException(
                "The report's labour hours and hourly rate are incomplete.");
        }
        var components = Printed.Parts + Printed.PanelLabour + Printed.PaintLabour
            + Printed.Materials + Printed.Specialist;
        if (components != Printed.Net || Printed.Net + Printed.Vat != Printed.Gross)
        {
            throw new ReportRenderRejectedException(
                "The printed repair-cost components do not reconcile to the printed total.");
        }
    }
}

public sealed record ReportSignatory(
    string PrintedName,
    string? Qualifications,
    byte[] SignatureContent,
    string SignatureContentType)
{
    [JsonIgnore]
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(PrintedName)
        && SignatureContent is { Length: > 0 }
        && ReportImageEvidence.IsAcceptedContentType(SignatureContentType);

    public void Validate()
    {
        if (!IsComplete)
        {
            throw new ReportRenderRejectedException(
                "The report signatory requires a printed name and accepted signature image.");
        }
    }
}

public sealed record AssessmentReportPresentation(
    string Title,
    string Badge,
    string SettlementHeading,
    string SettlementLabel,
    string SettlementText,
    decimal? RecommendedSettlement)
{
    public static string DamageAreas(IReadOnlyList<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        return string.Join(", ", codes.Select(code =>
            Assessment.AssessmentVocabulary.DamageAreas.TryGetValue(code, out var name)
                ? name
                : throw new ReportRenderRejectedException($"Unsupported damage area '{code}'.")));
    }

    public static string DamageSeverity(string code) =>
        Assessment.AssessmentVocabulary.DamageSeverities.TryGetValue(code, out var display)
            ? display.Display
            : throw new ReportRenderRejectedException($"Unsupported damage severity '{code}'.");

    public static string AssessmentCode(string? code) => code switch
    {
        null => "—",
        "semi_automatic" => "Semi-automatic",
        "cvt" => "CVT",
        "ok" => "OK",
        "repair_kit" => "Repair kit",
        "not_fitted" => "Not fitted",
        _ => CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(
            code.Replace('_', ' ').ToLowerInvariant()),
    };
}

public sealed record AssessmentReportSnapshot(
    string OurReference,
    string YourReference,
    DateOnly ReportDate,
    string ClaimantName,
    DateOnly IncidentDate,
    DateOnly InstructionsReceived,
    DateOnly Assessed,
    IReadOnlyList<string> ReportFor,
    ReportVehicle Vehicle,
    AssessmentReportOutcome Outcome,
    string LegalStatus,
    string? UnroadworthyReason,
    string ImpactSeverity,
    string ImpactLocation,
    string AssessmentMethod,
    string? LocationAddress,
    decimal EngineerValue,
    decimal RetailValue,
    decimal TradeValue,
    string? SalvageCategory,
    decimal? SalvageValue,
    ReportRepairCosts Costs,
    IReadOnlyList<string> NewParts,
    IReadOnlyList<string> Repairs,
    IReadOnlyList<string> Operations,
    ReportDamage Damage,
    ReportSettlement Settlement,
    string HistoryCheck,
    string? EngineerComments,
    ReportSignatory Signatory,
    decimal AgreedFee,
    IReadOnlyList<string> FeeDescriptionLines,
    IReadOnlyList<ReportImageEvidence> Photos,
    IReadOnlyList<AcceptedReportSource> Sources,
    CaseReportContentSwitches Content,
    ReportGuideSources Guides,
    string? ValuationCommentary = null,
    bool ReportDateOverridden = false,
    string PayloadVersion = AssessmentReportContract.TemplateVersion,
    bool IncludeFeeNote = false,
    string? SupplementaryStatement = null,
    IReadOnlyList<CaseReportWording>? Wording = null)
{
    /// <summary>
    /// The narrative the report prints, in the Engineer's order (v28 P30):
    /// the snapshot's own frozen facts composed under the Engineer's changes
    /// as they stood when it was frozen. A block the Engineer never touched
    /// composes from the snapshot, so the printed words can never disagree
    /// with the facts printed beside them; a snapshot frozen before the
    /// blocks existed holds no changes and composes them all.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ReportWordingBlock> PrintedWording =>
        ReportWordingComposition.Compose(this, Wording ?? []);

    /// <summary>Whether this report prints the Glass's guide-disclosure sentence (H5).</summary>
    [JsonIgnore]
    public bool PrintsGuideDisclosure => AssessmentReportContract.PrintsGuideDisclosure(Content, Guides);

    /// <summary>The accepted statement of truth this report prints, in order.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> StatementOfTruth => AssessmentReportContract.StatementOfTruth(Content, Guides);

    /// <summary>
    /// The images in printed order: Close-up first, Overview second, then
    /// Supporting by its persisted order.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ReportImageEvidence> OrderedPhotos => Photos
        .OrderBy(photo => photo.Role switch
        {
            CaseAssetReportRole.CloseUp => 0,
            CaseAssetReportRole.Overview => 1,
            _ => 2,
        })
        .ThenBy(photo => photo.Order ?? int.MaxValue)
        .ToArray();

    public void Validate()
    {
        AcceptedReportSource.Required(OurReference, nameof(OurReference));
        AcceptedReportSource.Required(YourReference, nameof(YourReference));
        AcceptedReportSource.Required(ClaimantName, nameof(ClaimantName));
        AcceptedReportSource.Required(Vehicle.Registration, nameof(Vehicle.Registration));
        AcceptedReportSource.Required(HistoryCheck, nameof(HistoryCheck));
        if (Signatory is null)
        {
            throw new ReportRenderRejectedException("The report signatory is required.");
        }
        Signatory.Validate();
        AcceptedReportSource.Required(PayloadVersion, nameof(PayloadVersion));
        if (ReportFor.Count == 0 || Photos.Count == 0 || Sources.Count == 0)
        {
            throw new ReportRenderRejectedException("Report addressee, photo custody and accepted source evidence are required.");
        }
        Costs.Validate();
        if (EngineerValue <= 0 || AgreedFee <= 0)
        {
            throw new ReportRenderRejectedException("Accepted report amounts are incomplete or invalid.");
        }
        if (Content.IncludeValuationCommentary && string.IsNullOrWhiteSpace(ValuationCommentary))
        {
            throw new ReportRenderRejectedException(
                "Valuation commentary was selected for the report but none is recorded.");
        }
        if (Content.IncludeUnrelatedDamage && string.IsNullOrWhiteSpace(Damage.Unrelated))
        {
            throw new ReportRenderRejectedException(
                "Unrelated damage was selected for the report but none is recorded.");
        }
        if (Outcome == AssessmentReportOutcome.TotalLoss &&
            (!AssessmentReportContract.PrintsSalvageCategory(SalvageCategory) || SalvageValue is null or < 0))
        {
            throw new ReportRenderRejectedException("The active total-loss report requires accepted Category S wording and salvage value.");
        }
        if (LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(UnroadworthyReason))
        {
            throw new ReportRenderRejectedException("An accepted unroadworthy reason is required.");
        }
        if (ReportFor.Any(string.IsNullOrWhiteSpace))
        {
            throw new ReportRenderRejectedException("Report inputs cannot contain blank entries.");
        }
        foreach (var source in Sources)
        {
            source.Validate();
        }
        foreach (var photo in Photos)
        {
            photo.Validate();
        }
        if (AssessmentMethod is not ("image_based" or "physical") ||
            AssessmentMethod == "physical" && string.IsNullOrWhiteSpace(LocationAddress))
        {
            throw new ReportRenderRejectedException("The accepted assessment method/location is incomplete.");
        }
        AcceptedReportSource.Required(ImpactSeverity, nameof(ImpactSeverity));
        AcceptedReportSource.Required(ImpactLocation, nameof(ImpactLocation));
        if (!PayloadVersion.Equals(AssessmentReportContract.TemplateVersion, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException($"Unsupported payload version '{PayloadVersion}'.");
        }
        if (Outcome == AssessmentReportOutcome.ContractRepair && Settlement.ContractSum is not > 0)
        {
            throw new ReportRenderRejectedException("Contract repair requires a confirmed positive agreed sum.");
        }
    }

    public AssessmentReportPresentation Presentation() => Outcome switch
    {
        AssessmentReportOutcome.TotalLoss => new(
            "TOTAL LOSS REPORT",
            $"TOTAL LOSS — CATEGORY {SalvageCategory}",
            "Settlement",
            "Recommended equitable settlement (pre-accident value less salvage)",
            $"We consider that an equitable settlement would be {Money(EngineerValue - SalvageValue!.Value)}, which represents the pre-accident engineer value of the vehicle of {Money(EngineerValue)} less the value of the salvage of {Money(SalvageValue.Value)}.",
            EngineerValue - SalvageValue!.Value),
        AssessmentReportOutcome.Repairable => new(
            "REPAIRABLE REPORT", "REPAIRABLE",
            "Settlement", "Recommended settlement (calculated repair cost)",
            $"This vehicle is considered a repairable proposition and we have calculated a repair cost of {Money(Costs.Total)}.",
            Costs.Total),
        AssessmentReportOutcome.CashInLieu => new(
            "CASH IN LIEU REPORT", "CASH IN LIEU",
            "Settlement", "Cash in lieu settlement",
            $"We recommend settlement by way of a cash in lieu payment based upon the estimated repair cost of {Money(Costs.Total)}.",
            Costs.Total),
        AssessmentReportOutcome.ContractRepair => new(
            "CONTRACT REPAIR REPORT", "CONTRACT REPAIR",
            "Contract Repair", "Agreed contract repair",
            $"A contract repair has been agreed for the sum of {Money(Settlement.ContractSum!.Value)} including VAT. Costs cannot increase above this figure.",
            Settlement.ContractSum!.Value),
        _ => throw new ReportRenderRejectedException("Unsupported assessment outcome."),
    };

    [JsonIgnore]
    public decimal FeeNet => AgreedFee;

    [JsonIgnore]
    public decimal FeeVat => decimal.Round(
        FeeNet * AssessmentReportContract.FeeVatRate, 2, MidpointRounding.AwayFromZero);
    [JsonIgnore]
    public decimal FeeTotal => FeeNet + FeeVat;

    private static string Money(decimal value) =>
        value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
}

public sealed record RenderedReportArtifact(
    string SuggestedFileName,
    byte[] Pdf,
    int PageCount,
    string Sha256,
    string TemplateVersion,
    string EngineVersion);

/// <summary>
/// Renders exactly the requested artifact kind. A caller that wants the
/// assessment report and a separate fee-note document asks twice from the
/// same frozen snapshot; nothing is rendered and discarded. When the frozen
/// snapshot's <see cref="AssessmentReportSnapshot.IncludeFeeNote"/> is set,
/// the <see cref="CaseReportArtifactKind.AssessmentReport"/> render carries
/// the fee note as its final pages instead, and no second document exists.
/// </summary>
public interface IAssessmentReportRenderer
{
    /// <summary>
    /// The rendering engine's own version, known without rendering, so a
    /// generation can freeze it before the renderer runs.
    /// </summary>
    string EngineVersion { get; }

    Task<RenderedReportArtifact> RenderAsync(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken = default);
}

public sealed class GenerateAssessmentReportDraft(IAssessmentReportRenderer renderer)
{
    public async Task<RenderedReportArtifact> ExecuteAsync(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        var artifact = await renderer.RenderAsync(snapshot, kind, cancellationToken).ConfigureAwait(false);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf));
        if (!actualHash.Equals(artifact.Sha256, StringComparison.Ordinal))
        {
            throw new ReportRenderRejectedException("The renderer returned an artifact with mismatched provenance.");
        }
        return artifact;
    }
}

public sealed class ReportRenderRejectedException(string message) : Exception(message);
