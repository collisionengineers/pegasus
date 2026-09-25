using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The closed wire vocabulary of the assessment surface. Field paths are the
/// exact <c>name</c> attributes of the Engineers assessment screen (which
/// follow reference/rendererref1/report_data_schema.json); an unknown
/// path fails closed. Fields owned by the accepted case record (registration,
/// make, model, mileage, the claimant's name and claim reference, incident,
/// received and inspection dates, inspection mode and address) are readable
/// through the assessment projection but are written only through the
/// existing case-data edit path, keeping one owner per fact.
/// </summary>
public enum AssessmentFieldType
{
    Text,
    Enumerated,
    WholeNumber,
    Money,
    Flag,
    Date,
    Json
}

/// <summary>
/// One recorded damage (v28 P5): the areas it covers in the vocabulary's
/// order, a severity and a note. A damage drawn on the plan keeps its
/// <paramref name="Disc"/> as drawn (unit-plan terms, see
/// <see cref="DamageAreaGeometry"/>) and names exactly the plan areas that
/// disc touches (ruled 23 September 2026). A damage recorded by area alone,
/// and Underside, Interior and Mechanical, each recorded alone, have no disc.
/// </summary>
public sealed record AssessmentImpact(IReadOnlyList<string> Areas, string Severity, string Note, DamageDisc? Disc = null);

public sealed record AssessmentFieldDefinition(
    string Path,
    AssessmentFieldType Type,
    int MaximumLength,
    bool IsFinding,
    bool MustBePositive = false,
    IReadOnlyList<string>? Codes = null);

public static class AssessmentVocabulary
{
    public const string VehicleType = "vehicle.vehicle_type";
    public const string VehicleVin = "vehicle.vin";
    public const string VehicleEngineCc = "vehicle.engine_cc";
    public const string VehicleFuel = "vehicle.fuel";
    public const string VehicleMileageSource = "vehicle.mileage_source";
    public const string VehicleCondition = "vehicle.condition";
    public const string VehicleTransmission = "vehicle.transmission";
    public const string VehicleColour = "vehicle.colour";
    public const string VehicleBody = "vehicle.body";
    public const string VehicleTaxExpiry = "vehicle.tax_expiry";
    public const string VehicleMotExpiry = "vehicle.mot_expiry";
    public const string VehicleAirbagsDeployed = "vehicle.airbags_deployed";
    public const string VehicleTemporaryRepairsPossible = "vehicle.temporary_repairs_possible";
    public const string VehicleTemporaryRepairMethod = "vehicle.temporary_repair_method";
    public const string VehicleTemporaryRepairCost = "vehicle.temporary_repair_cost";
    public const string ImpactSeverity = "assessment.impact_severity";
    public const string ImpactLocation = "assessment.impact_location";
    public const string DamageImpacts = "damage.impacts";
    public const string DamageTyreRightFront = "damage.tyres.right_front.tyre";
    public const string DamageTyreLeftFront = "damage.tyres.left_front.tyre";
    public const string DamageTyreRightRear = "damage.tyres.right_rear.tyre";
    public const string DamageTyreLeftRear = "damage.tyres.left_rear.tyre";
    public const string DamageBeltRightFront = "damage.tyres.right_front.belt";
    public const string DamageBeltLeftFront = "damage.tyres.left_front.belt";
    public const string DamageBeltRightRear = "damage.tyres.right_rear.belt";
    public const string DamageBeltLeftRear = "damage.tyres.left_rear.belt";
    public const string DamageSpareTyre = "damage.tyres.spare";
    public const string DamageCentreBelt = "damage.tyres.centre_belt";
    public const string DamageUnrelated = "damage.unrelated";
    public const string DamageUnrelatedDeduction = "damage.unrelated_deduction";
    public const string DamageMaterialTransfer = "damage.material_transfer";
    public const string ValueRetail = "assessment.values.retail";
    public const string ValueTrade = "assessment.values.trade";
    public const string ValueEngineer = "assessment.values.engineer";
    public const string RateCard = "rates.card";
    public const string RateClass = "rates.class";
    public const string RateManufacturerApproved = "rates.manufacturer_approved";
    public const string RateRegionalUplift = "rates.regional_uplift";
    public const string CostRecoveryCharge = "costs.recovery_charge";
    public const string CostStorageCharge = "costs.storage_charge";
    public const string Outcome = "assessment.outcome";
    public const string LegalStatus = "assessment.legal_status";
    public const string UnroadworthyReason = "assessment.unroadworthy_reason";
    public const string SalvageCategory = "assessment.category";
    public const string SalvageValue = "assessment.salvage_value";
    public const string HistoryCheck = "narrative.history_check";
    public const string EngineersComments = "narrative.engineers_comments";
    public const string ReportDiscloseGuideSource = "report.disclose_guide_source";
    public const string ReportValuationCommentary = "report.valuation_commentary";

    /// <summary>
    /// The valuation commentary itself, beside the flag that prints it: the
    /// Engineer's own words, saved with the Case and frozen into the report.
    /// </summary>
    public const string ReportValuationCommentaryText = "report.valuation_commentary_text";
    public const string ReportIncludeUnrelatedDamage = "report.include_unrelated_damage";
    public const string ReportDateOverride = "report.date_override";
    public const string ReportDate = "report.report_date";
    public const string EngineerName = "engineer.name";
    public const string EngineerQualifications = "engineer.qualifications";
    public const string EngineerSignature = "engineer.signature";
    public const string AgreedFee = "fee.agreed_fee";
    public const string FeeDescriptionLines = "fee.description_lines";
    public const string SettlementExcess = "settlement.excess";
    public const string SettlementBetterment = "settlement.betterment";
    public const string SettlementClaimantVatRegistered = "settlement.claimant_vat_registered";
    public const string SettlementReserve = "settlement.reserve";
    public const string SettlementRepairDelays = "settlement.repair_delays";
    public const string SettlementReportDelay = "settlement.report_delay";
    public const string SettlementStoragePerDay = "settlement.storage_per_day";
    public const string SettlementHireStart = "settlement.hire_start";
    public const string SettlementHireDailyCost = "settlement.hire_daily_cost";
    public const string SettlementDiminution = "settlement.diminution";
    /// <summary>The agreed contract repair sum (v28 P35): the Engineer's figure the report prints as the cap.</summary>
    public const string SettlementContractSum = "settlement.contract_sum";
    public const string OriginalReportAssessor = "original_report.assessor";
    public const string OriginalReportDate = "original_report.report_date";
    public const string OriginalReportRoadworthiness = "original_report.roadworthiness";
    public const string OriginalReportOutcome = "original_report.outcome";
    public const string SettlementSalvageAt = "settlement.salvage.at";
    public const string SettlementSalvageAgent = "settlement.salvage.agent";
    public const string SettlementSalvageAgentReference = "settlement.salvage.agent_reference";
    public const string SettlementSalvageMoved = "settlement.salvage.moved";
    public const string SettlementSalvageOwnerRetains = "settlement.salvage.owner_retains";
    public const string SettlementSalvageValueAgreed = "settlement.salvage.value_agreed";
    public const string SettlementSalvageSettled = "settlement.salvage.settled";

    /// <summary>
    /// Every accepted damage zone with its display label and the headline
    /// impact location it rolls up to. This table is the one owner of that
    /// parent map: a broad region is its own headline, a detailed region
    /// carries its parent's headline, and the four wheels roll up to
    /// <c>wheel</c>. A broad impact recorded before the detailed diagram
    /// existed stays a broad fact and is never split into detailed regions.
    /// </summary>
    /// <summary>
    /// The eight areas of the plan (v28 P5): front, the sides and the rear,
    /// each split left, centre and right. A disc drawn on the vehicle records
    /// the areas under it.
    /// </summary>
    public static IReadOnlyList<string> DamagePlanAreas { get; } =
    [
        "front", "left_front", "right_front", "left_side",
        "right_side", "rear", "left_rear", "right_rear"
    ];

    /// <summary>The three areas the plan cannot show, each recorded alone.</summary>
    public static IReadOnlyList<string> DamageOtherAreas { get; } = ["underside", "interior", "mechanical"];

    /// <summary>Every recordable area with its name, as the record, the cells and the report print it.</summary>
    public static IReadOnlyDictionary<string, string> DamageAreas { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["front"] = "Front", ["left_front"] = "LH Front", ["right_front"] = "RH Front",
            ["left_side"] = "LH Side", ["right_side"] = "RH Side", ["rear"] = "Rear",
            ["left_rear"] = "LH Rear", ["right_rear"] = "RH Rear",
            ["underside"] = "Underside", ["interior"] = "Interior", ["mechanical"] = "Mechanical"
        };

    private static readonly string[] DamageAreaSequence = [.. DamagePlanAreas, .. DamageOtherAreas];

    /// <summary>An area's position in the record's order; an unknown code sorts last.</summary>
    public static int DamageAreaOrder(string code)
    {
        var index = Array.IndexOf(DamageAreaSequence, code);
        return index < 0 ? DamageAreaSequence.Length : index;
    }

    public static IReadOnlyDictionary<string, (string Display, int Rank)> DamageSeverities { get; } =
        new Dictionary<string, (string, int)>(StringComparer.Ordinal)
        {
            ["light"] = ("Light", 0), ["light_to_moderate"] = ("Light to moderate", 1),
            ["moderate"] = ("Moderate", 2), ["moderate_to_heavy"] = ("Moderate to heavy", 3),
            ["heavy"] = ("Heavy", 4)
        };

    private static readonly string[] TyreCodes = ["ok", "worn", "damaged", "illegal"];
    private static readonly string[] BeltCodes = ["ok", "locked", "deployed", "not_fitted"];

    private static readonly AssessmentFieldDefinition[] DefinitionList =
    [
        new(VehicleType, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["car", "van", "motorcycle", "scooter", "bicycle", "trailer", "caravan", "other"]),
        new(VehicleVin, AssessmentFieldType.Text, 30, IsFinding: false),
        new(VehicleEngineCc, AssessmentFieldType.WholeNumber, 10, IsFinding: false),
        new(VehicleFuel, AssessmentFieldType.Text, 40, IsFinding: false),
        new(VehicleMileageSource, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["online_data", "owner", "repairer", "principal", "average", "tbc"]),
        new(VehicleCondition, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["poor", "below_average", "average", "good", "excellent"]),
        new(VehicleTransmission, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["manual", "automatic", "semi_automatic", "cvt", "unknown"]),
        new(VehicleColour, AssessmentFieldType.Text, 40, IsFinding: false),
        new(VehicleBody, AssessmentFieldType.Text, 100, IsFinding: false),
        new(VehicleTaxExpiry, AssessmentFieldType.Date, 10, IsFinding: false),
        new(VehicleMotExpiry, AssessmentFieldType.Date, 10, IsFinding: false),
        new(VehicleAirbagsDeployed, AssessmentFieldType.Text, 200, IsFinding: false),
        new(VehicleTemporaryRepairsPossible, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(VehicleTemporaryRepairMethod, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(VehicleTemporaryRepairCost, AssessmentFieldType.Money, 20, IsFinding: false),
        new(ImpactSeverity, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: DamageSeverities.Keys.ToArray()),
        new(ImpactLocation, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: [.. DamagePlanAreas, .. DamageOtherAreas, "multiple"]),
        new(DamageImpacts, AssessmentFieldType.Json, 4000, IsFinding: false),
        new(DamageTyreRightFront, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: TyreCodes),
        new(DamageTyreLeftFront, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: TyreCodes),
        new(DamageTyreRightRear, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: TyreCodes),
        new(DamageTyreLeftRear, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: TyreCodes),
        new(DamageBeltRightFront, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: BeltCodes),
        new(DamageBeltLeftFront, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: BeltCodes),
        new(DamageBeltRightRear, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: BeltCodes),
        new(DamageBeltLeftRear, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: BeltCodes),
        new(DamageSpareTyre, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: ["ok", "repair_kit", "missing", "damaged"]),
        new(DamageCentreBelt, AssessmentFieldType.Enumerated, 20, IsFinding: false, Codes: ["ok", "locked", "not_fitted"]),
        new(DamageUnrelated, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(DamageUnrelatedDeduction, AssessmentFieldType.Money, 20, IsFinding: false),
        new(DamageMaterialTransfer, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(ValueRetail, AssessmentFieldType.Money, 20, IsFinding: true, MustBePositive: true),
        new(ValueTrade, AssessmentFieldType.Money, 20, IsFinding: true, MustBePositive: true),
        new(ValueEngineer, AssessmentFieldType.Money, 20, IsFinding: true, MustBePositive: true),
        new(RateCard, AssessmentFieldType.Text, 100, IsFinding: false),
        new(RateClass, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["standard", "prestige", "van"]),
        new(RateManufacturerApproved, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(RateRegionalUplift, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(CostRecoveryCharge, AssessmentFieldType.Money, 20, IsFinding: false),
        new(CostStorageCharge, AssessmentFieldType.Money, 20, IsFinding: false),
        new(Outcome, AssessmentFieldType.Enumerated, 20, IsFinding: true,
            Codes: ["total_loss", "repairable", "cash_in_lieu", "contract_repair"]),
        new(LegalStatus, AssessmentFieldType.Enumerated, 20, IsFinding: true,
            Codes: ["roadworthy", "unroadworthy"]),
        new(UnroadworthyReason, AssessmentFieldType.Text, 2000, IsFinding: true),
        new(SalvageCategory, AssessmentFieldType.Enumerated, 5, IsFinding: true,
            Codes: ["A", "B", "S", "N", "N/A"]),
        new(SalvageValue, AssessmentFieldType.Money, 20, IsFinding: true),
        new(HistoryCheck, AssessmentFieldType.Text, 4000, IsFinding: false),
        new(EngineersComments, AssessmentFieldType.Text, 4000, IsFinding: false),
        new(EngineerName, AssessmentFieldType.Text, 200, IsFinding: false),
        new(EngineerQualifications, AssessmentFieldType.Text, 200, IsFinding: false),
        new(EngineerSignature, AssessmentFieldType.Text, 200, IsFinding: false),
        new(AgreedFee, AssessmentFieldType.Money, 20, IsFinding: false, MustBePositive: true),
        new(FeeDescriptionLines, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(ReportDiscloseGuideSource, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportValuationCommentary, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportValuationCommentaryText, AssessmentFieldType.Text, 4000, IsFinding: false),
        new(ReportIncludeUnrelatedDamage, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportDateOverride, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportDate, AssessmentFieldType.Date, 10, IsFinding: false),
        new(SettlementExcess, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementBetterment, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementClaimantVatRegistered, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(SettlementReserve, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementRepairDelays, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(SettlementReportDelay, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(SettlementStoragePerDay, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementHireStart, AssessmentFieldType.Date, 10, IsFinding: false),
        new(SettlementHireDailyCost, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementDiminution, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementContractSum, AssessmentFieldType.Money, 20, IsFinding: false),
        // An Audit's original report (v28 P51): who wrote it, when, and what it found.
        new(OriginalReportAssessor, AssessmentFieldType.Text, 200, IsFinding: false),
        new(OriginalReportDate, AssessmentFieldType.Date, 10, IsFinding: false),
        new(OriginalReportRoadworthiness, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["roadworthy", "unroadworthy"]),
        new(OriginalReportOutcome, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["repairable", "total_loss", "cash_in_lieu", "contract_repair"]),
        new(SettlementSalvageAt, AssessmentFieldType.Text, 400, IsFinding: false),
        new(SettlementSalvageAgent, AssessmentFieldType.Text, 200, IsFinding: false),
        new(SettlementSalvageAgentReference, AssessmentFieldType.Text, 100, IsFinding: false),
        new(SettlementSalvageMoved, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(SettlementSalvageOwnerRetains, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(SettlementSalvageValueAgreed, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(SettlementSalvageSettled, AssessmentFieldType.Date, 10, IsFinding: false)
    ];

    public static IReadOnlyDictionary<string, AssessmentFieldDefinition> Definitions { get; } =
        DefinitionList.ToDictionary(definition => definition.Path, StringComparer.Ordinal);

    /// <summary>Derived from damage.impacts by the save's write set (AssessmentWriteSet); a field save never writes one.</summary>
    public static IReadOnlySet<string> DerivedPaths { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ImpactLocation,
        ImpactSeverity
    };

    /// <summary>
    /// Facts only the DVLA/DVSA vehicle lookup records (operator, 24 September
    /// 2026): engine capacity, fuel, colour, tax expiry and MOT expiry, each set by
    /// <see cref="Pegasus.Core.Vehicle.VehicleLookupFillPolicy.DerivedAssessmentWrites"/>
    /// and recorded by the lookup. The Vehicle section shows them read-only;
    /// no field save records or clears one.
    /// </summary>
    public static IReadOnlySet<string> LookupDerivedPaths { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        VehicleEngineCc,
        VehicleFuel,
        VehicleColour,
        VehicleTaxExpiry,
        VehicleMotExpiry
    };

    /// <summary>
    /// Whether the recorded roadworthiness makes the temporary repairs part of
    /// the assessment (operator, 24 September 2026): only an unroadworthy
    /// vehicle has them, so Decisions shows them and the report prints them
    /// only then.
    /// </summary>
    public static bool TemporaryRepairsApply(string? legalStatus) =>
        string.Equals(legalStatus, "unroadworthy", StringComparison.Ordinal);

    /// <summary>
    /// Findings a generic assessment save never writes or clears, because the
    /// Case Save's valuation adoption records them together (one Save, 23
    /// September 2026; operator, 24 September 2026): the accepted Engineer's
    /// Value and the retail and trade values of the guide card it was
    /// calculated from. A Web or MCP field save that touched one would rewrite
    /// a professional finding apart from the calculation that is its evidence.
    /// </summary>
    public static IReadOnlySet<string> AdoptedFindingPaths { get; } = new HashSet<string>(
        StringComparer.Ordinal)
    {
        ValueRetail,
        ValueTrade,
        ValueEngineer
    };

    /// <summary>
    /// Paths the assessment surface displays but the accepted case record
    /// owns. Writes through the assessment command fail closed and name the
    /// case-detail edit path instead.
    /// </summary>
    public static IReadOnlySet<string> CaseOwnedPaths { get; } = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "vehicle.registration",
        "vehicle.make",
        "vehicle.model",
        "vehicle.year",
        "vehicle.odometer_miles",
        "incident.date",
        "incident.instructions_received",
        // The report's assessed date is the Case's Inspection date (operator, 24 September 2026).
        "incident.assessed",
        "assessment.method",
        "assessment.location_address"
    };
}

public static class EstimateLineCodes
{
    public static IReadOnlyList<string> Types { get; } =
    [
        "rnr", "repair", "new_part", "check_labour", "paint_new", "paint_repair",
        "paint_blend", "paint_prep", "specialist_fixed", "specialist_wu"
    ];

    public static IReadOnlyList<string> EvidenceLabels { get; } =
        ["official", "reference", "case", "judgement"];
}

/// <summary>
/// One caller-supplied estimate line. A save that carries lines replaces the
/// whole ordered collection, matching the screen's estimate-section save; the
/// permanent history keeps the collection it replaced.
/// </summary>
public sealed record EstimateLineInput(
    string Type,
    string? GuideCode,
    string? Description,
    decimal? WorkUnits,
    decimal? Price,
    bool Unpriced,
    string? PartNumber,
    string? Betterment,
    string? EvidenceLabel,
    string? Justification,
    decimal? PaintWorkUnits = null,
    int? Quantity = null,
    decimal? Materials = null,
    EstimateLineOrigin? Origin = null,
    string? SourceDocumentIdentity = null,
    Guid? SourceDocumentVersionId = null,
    string? SourceDocumentSha256 = null,
    string? SourceRowIdentity = null,
    string? AmendedBy = null,
    DateTimeOffset? AmendedAtUtc = null);

public sealed record CaseEstimateLineRecord(
    Guid Id,
    int Position,
    string Type,
    string? GuideCode,
    string? Description,
    decimal? WorkUnits,
    decimal? Price,
    bool Unpriced,
    string? PartNumber,
    string? Betterment,
    string? EvidenceLabel,
    string? Justification,
    ActorKind RecordedByKind,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc,
    decimal? PaintWorkUnits = null,
    int? Quantity = null,
    decimal? Materials = null,
    EstimateLineOrigin? Origin = null,
    string? SourceDocumentIdentity = null,
    Guid? SourceDocumentVersionId = null,
    string? SourceDocumentSha256 = null,
    string? SourceRowIdentity = null,
    string? AmendedBy = null,
    DateTimeOffset? AmendedAtUtc = null);

/// <summary>
/// One recorded assessment field value with its provenance. A recorded value
/// is the Case's value whoever recorded it (operator, 25 September 2026):
/// there is no per-field review, and the provenance is shown as the value's
/// source tag. A professional finding is recorded only by staff. The
/// permanent action history carries every before and after value, so the
/// current row never erases evidence.
/// </summary>
public sealed record AssessmentFieldValue(
    string Path,
    string Value,
    ActorKind RecordedByKind,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc);

/// <summary>
/// The case-owned fields the assessment surface reads without owning:
/// current accepted values from the case-data projection, single-owner per
/// ADR-0031 / FRD-10 (docs/adr/0031-automation-actor-contract-without-eva-export-tools.md,
/// docs/frd/frd-10-mcp-automation-and-actor-boundary.md).
/// </summary>
public sealed record AssessmentCaseOwnedData(
    string? Registration,
    string? Make,
    string? Model,
    string? Year,
    long? Mileage,
    string? MileageUnit,
    // The report's mileage-source code, derived from where the case's mileage
    // came from. Never absent: a case with no mileage reads "tbc".
    string MileageSource,
    DateOnly? IncidentDate,
    // The London calendar date the Case was received, as its Received cell
    // shows it: its receipt's received time, or its creation for a manual
    // Case. The report prints it as the date instructions were received
    // (operator, 24 September 2026); every Case has one.
    DateOnly ReceivedDate,
    string? InspectionMode,
    string? InspectionAddress,
    // Appended, never inserted. The Inspection date is the date the report
    // says the damage was assessed (#834); the claimant's name and the
    // Principal's claim reference (printed as Your Ref) are read here so
    // readiness and the projection read one value.
    DateOnly? InspectionDate,
    string? ClaimantName,
    string? ClaimNumber);

/// <summary>
/// One named blocker (FRD-13). <paramref name="Field"/> is the one recorded
/// fact the blocker names: an <see cref="AssessmentVocabulary"/> path, or a
/// <see cref="Pegasus.Core.Cases.CaseDataFieldNames"/> name for a Case fact.
/// It is null when the blocker names other material (the sign-off account,
/// the Current repair spec, report images);
/// <see cref="Pegasus.Core.Reports.CaseReportReadiness"/> names those by its
/// public requirement constants. The Web decides which section clears a
/// blocker; Core holds no section list.
/// </summary>
public sealed record AssessmentReadinessItem(
    string Requirement,
    string Source,
    string WhyOutstanding,
    string HowToResolve,
    string? Field = null);

public sealed record CaseAssessmentProjection(
    Guid CaseId,
    string Reference,
    long CaseVersion,
    CaseLifecycleState State,
    Guid? AssignedEngineerId,
    IReadOnlyList<AssessmentFieldValue> Fields,
    IReadOnlyList<CaseEstimateLineRecord> EstimateLines,
    AssessmentCaseOwnedData CaseOwned)
{
    public IReadOnlyList<AssessmentReadinessItem> Readiness { get; init; } = [];

    public AssessmentFieldValue? Field(string path) =>
        Fields.FirstOrDefault(field => string.Equals(field.Path, path, StringComparison.Ordinal));
}

/// <summary>
/// One save over the assessment surface: scalar values keyed by the closed
/// path vocabulary (null clears), and the same actor, edit-lease,
/// expected-version, and operation-key guards as every case mutation. The
/// optional Send-to-AI work-request binding is correlation evidence only and
/// is never required (companion-plan decision D3).
/// </summary>
public sealed record SaveAssessmentRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    IReadOnlyDictionary<string, string?> Fields,
    Guid? AiWorkRequestId = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ICaseAssessmentStore
{
    Task<CaseAssessmentProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken);

    Task<CaseAssessmentProjection> SaveAsync(
        SaveAssessmentRequest request,
        CancellationToken cancellationToken);
}

public interface IGetCaseAssessment
{
    Task<CaseAssessmentProjection?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken);
}

public interface ISaveAssessment
{
    Task<CaseAssessmentProjection> ExecuteAsync(
        SaveAssessmentRequest request,
        CancellationToken cancellationToken);
}
