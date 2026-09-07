using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The closed wire vocabulary of the assessment surface. Field paths are the
/// exact <c>name</c> attributes of the Engineers assessment screen (which
/// follow reference/rendererref1/report_data_schema.json); an unknown
/// path fails closed. Fields owned by the accepted case record (registration,
/// make, model, mileage, incident and instruction dates, inspection mode and
/// address) are readable through the assessment projection but are written
/// only through the existing case-data edit path, keeping one owner per fact.
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

public sealed record AssessmentImpact(string Zone, string Severity, string Note);

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
    public const string VehicleYear = "vehicle.year";
    public const string VehicleVin = "vehicle.vin";
    public const string VehicleEngineCc = "vehicle.engine_cc";
    public const string VehicleFuel = "vehicle.fuel";
    public const string VehicleMileageSource = "vehicle.mileage_source";
    public const string VehicleCondition = "vehicle.condition";
    public const string VehicleVinChecked = "vehicle.vin_checked";
    public const string VehicleTransmission = "vehicle.transmission";
    public const string VehicleColour = "vehicle.colour";
    public const string VehicleBody = "vehicle.body";
    public const string VehicleTaxExpiry = "vehicle.tax_expiry";
    public const string VehicleMotExpiry = "vehicle.mot_expiry";
    public const string VehicleAirbagsDeployed = "vehicle.airbags_deployed";
    public const string VehicleFaultCodes = "vehicle.fault_codes";
    public const string VehicleTemporaryRepairsPossible = "vehicle.temporary_repairs_possible";
    public const string VehicleTemporaryRepairMethod = "vehicle.temporary_repair_method";
    public const string VehicleTemporaryRepairCost = "vehicle.temporary_repair_cost";
    public const string IncidentAssessed = "incident.assessed";
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
    public const string NatureOfIncident = "narrative.nature_of_incident";
    public const string ValueRetail = "assessment.values.retail";
    public const string ValueTrade = "assessment.values.trade";
    public const string ValueEngineer = "assessment.values.engineer";
    public const string RateCard = "rates.card";
    public const string RateClass = "rates.class";
    public const string RateManufacturerApproved = "rates.manufacturer_approved";
    public const string RateRegionalUplift = "rates.regional_uplift";
    public const string CostRecoveryCharge = "costs.recovery_charge";
    public const string CostStorageCharge = "costs.storage_charge";
    public const string CostRepairerVatRegistered = "costs.repairer_vat_registered";
    public const string Outcome = "assessment.outcome";
    public const string LegalStatus = "assessment.legal_status";
    public const string UnroadworthyReason = "assessment.unroadworthy_reason";
    public const string SalvageCategory = "assessment.category";
    public const string SalvageValue = "assessment.salvage_value";
    public const string VehicleModifications = "vehicle.modifications";
    public const string VehicleHistoryNotes = "vehicle.history_notes";
    public const string VehicleEngineerNotes = "vehicle.engineer_notes";
    public const string HistoryCheck = "narrative.history_check";
    public const string EngineersComments = "narrative.engineers_comments";
    public const string ReportDiscloseGuideSource = "report.disclose_guide_source";
    public const string ReportValuationCommentary = "report.valuation_commentary";
    public const string ReportIncludeUnrelatedDamage = "report.include_unrelated_damage";
    public const string ReportDateOverride = "report.date_override";
    public const string ReportDate = "report.report_date";
    public const string EngineerName = "engineer.name";
    public const string EngineerQualifications = "engineer.qualifications";
    public const string EngineerSignature = "engineer.signature";
    public const string AgreedFee = "fee.agreed_fee";
    public const string FeeDescriptionLines = "fee.description_lines";
    public const string StatementOfTruth = "statement_of_truth";
    public const string SettlementExcess = "settlement.excess";
    public const string SettlementBetterment = "settlement.betterment";
    public const string SettlementClaimantVatRegistered = "settlement.claimant_vat_registered";
    public const string SettlementReserve = "settlement.reserve";
    public const string SettlementRepairDuration = "settlement.repair_duration";
    public const string SettlementRepairDelays = "settlement.repair_delays";
    public const string SettlementReportDelay = "settlement.report_delay";
    public const string SettlementStoragePerDay = "settlement.storage_per_day";
    public const string SettlementHireStart = "settlement.hire_start";
    public const string SettlementHireDailyCost = "settlement.hire_daily_cost";
    public const string SettlementDiminution = "settlement.diminution";
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
    public static IReadOnlyDictionary<string, (string Display, string ImpactLocation)> DamageZones { get; } =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["front"] = ("Front", "front"), ["left_front"] = ("Left front", "left_front"),
            ["right_front"] = ("Right front", "right_front"), ["left_side"] = ("Left side", "left_side"),
            ["right_side"] = ("Right side", "right_side"), ["rear"] = ("Rear", "rear"),
            ["left_rear"] = ("Left rear", "left_rear"), ["right_rear"] = ("Right rear", "right_rear"),
            ["roof"] = ("Roof", "roof"), ["wheel_right_front"] = ("Right front wheel", "wheel"),
            ["wheel_left_front"] = ("Left front wheel", "wheel"), ["wheel_right_rear"] = ("Right rear wheel", "wheel"),
            ["wheel_left_rear"] = ("Left rear wheel", "wheel"), ["underside"] = ("Underside", "underside"),
            ["interior"] = ("Interior", "interior"), ["mechanical"] = ("Mechanical", "mechanical"),
            ["front_left_corner"] = ("Front N/S corner", "left_front"),
            ["front_centre"] = ("Front centre", "front"),
            ["front_right_corner"] = ("Front O/S corner", "right_front"),
            ["left_front_wing"] = ("N/S front wing", "left_front"),
            ["left_front_door"] = ("N/S front door", "left_side"),
            ["left_rear_door"] = ("N/S rear door", "left_side"),
            ["left_quarter"] = ("N/S rear quarter", "left_rear"),
            ["right_front_wing"] = ("O/S front wing", "right_front"),
            ["right_front_door"] = ("O/S front door", "right_side"),
            ["right_rear_door"] = ("O/S rear door", "right_side"),
            ["right_quarter"] = ("O/S rear quarter", "right_rear"),
            ["rear_left_corner"] = ("Rear N/S corner", "left_rear"),
            ["rear_centre"] = ("Rear centre", "rear"),
            ["rear_right_corner"] = ("Rear O/S corner", "right_rear"),
            ["bonnet"] = ("Bonnet", "front"),
            ["windscreen"] = ("Windscreen", "front"),
            ["rear_screen"] = ("Rear screen", "rear"),
            ["tailgate"] = ("Boot / tailgate", "rear")
        };

    /// <summary>
    /// The eight broad regions the record kept before the detailed diagram.
    /// Each is its own headline, and each remains an independent entry: a
    /// detailed region beside its broad parent is two impacts, not one.
    /// </summary>
    public static IReadOnlySet<string> BroadDamageZones { get; } = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "front", "left_front", "right_front", "left_side",
        "right_side", "rear", "left_rear", "right_rear"
    };

    /// <summary>
    /// The twenty-three regions of the damage diagram. The four wheels and the
    /// roof keep the keys the record already persisted rather than gaining a
    /// second spelling.
    /// </summary>
    public static IReadOnlySet<string> DetailedDamageZones { get; } = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "front_left_corner", "front_centre", "front_right_corner",
        "left_front_wing", "left_front_door", "left_rear_door", "left_quarter",
        "right_front_wing", "right_front_door", "right_rear_door", "right_quarter",
        "rear_left_corner", "rear_centre", "rear_right_corner",
        "bonnet", "windscreen", "roof", "rear_screen", "tailgate",
        "wheel_left_front", "wheel_right_front", "wheel_left_rear", "wheel_right_rear"
    };

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
        new(VehicleYear, AssessmentFieldType.Text, 10, IsFinding: false),
        new(VehicleVin, AssessmentFieldType.Text, 30, IsFinding: false),
        new(VehicleEngineCc, AssessmentFieldType.WholeNumber, 10, IsFinding: false),
        new(VehicleFuel, AssessmentFieldType.Text, 40, IsFinding: false),
        new(VehicleMileageSource, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["online_data", "owner", "repairer", "principal", "average", "tbc"]),
        new(VehicleCondition, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["poor", "below_average", "average", "good", "excellent"]),
        new(VehicleVinChecked, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(VehicleTransmission, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["manual", "automatic", "semi_automatic", "cvt", "unknown"]),
        new(VehicleColour, AssessmentFieldType.Text, 40, IsFinding: false),
        new(VehicleBody, AssessmentFieldType.Text, 40, IsFinding: false),
        new(VehicleTaxExpiry, AssessmentFieldType.Date, 10, IsFinding: false),
        new(VehicleMotExpiry, AssessmentFieldType.Date, 10, IsFinding: false),
        new(VehicleAirbagsDeployed, AssessmentFieldType.Text, 200, IsFinding: false),
        new(VehicleFaultCodes, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(VehicleTemporaryRepairsPossible, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(VehicleTemporaryRepairMethod, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(VehicleTemporaryRepairCost, AssessmentFieldType.Money, 20, IsFinding: false),
        new(VehicleModifications, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(VehicleHistoryNotes, AssessmentFieldType.Text, 4000, IsFinding: false),
        new(VehicleEngineerNotes, AssessmentFieldType.Text, 4000, IsFinding: false),
        new(IncidentAssessed, AssessmentFieldType.Date, 10, IsFinding: false),
        new(ImpactSeverity, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: DamageSeverities.Keys.ToArray()),
        new(ImpactLocation, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: [.. DamageZones.Values.Select(zone => zone.ImpactLocation).Distinct(StringComparer.Ordinal), "multiple"]),
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
        new(NatureOfIncident, AssessmentFieldType.Text, 2000, IsFinding: false),
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
        new(CostRepairerVatRegistered, AssessmentFieldType.Flag, 5, IsFinding: false),
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
        new(StatementOfTruth, AssessmentFieldType.Text, 4000, IsFinding: false),
        new(ReportDiscloseGuideSource, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportValuationCommentary, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportIncludeUnrelatedDamage, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportDateOverride, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(ReportDate, AssessmentFieldType.Date, 10, IsFinding: false),
        new(SettlementExcess, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementBetterment, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementClaimantVatRegistered, AssessmentFieldType.Flag, 5, IsFinding: false),
        new(SettlementReserve, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementRepairDuration, AssessmentFieldType.WholeNumber, 10, IsFinding: false),
        new(SettlementRepairDelays, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(SettlementReportDelay, AssessmentFieldType.Text, 2000, IsFinding: false),
        new(SettlementStoragePerDay, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementHireStart, AssessmentFieldType.Date, 10, IsFinding: false),
        new(SettlementHireDailyCost, AssessmentFieldType.Money, 20, IsFinding: false),
        new(SettlementDiminution, AssessmentFieldType.Money, 20, IsFinding: false),
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

    public static IReadOnlySet<string> DerivedPaths { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ImpactLocation,
        ImpactSeverity
    };

    /// <summary>
    /// Findings a generic assessment save never writes or clears, because a
    /// named command owns the act of adopting them (AUTO-015). The accepted
    /// Engineer's value is adopted only by the valuation Apply command, which
    /// records the suggested and chosen amounts together; a Web or MCP field
    /// save that touched it would silently rewrite a professional finding
    /// without that evidence.
    /// </summary>
    public static IReadOnlySet<string> AdoptedFindingPaths { get; } = new HashSet<string>(
        StringComparer.Ordinal)
    {
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
        "vehicle.odometer_miles",
        "incident.date",
        "incident.instructions_received",
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

    public static IReadOnlyList<string> Statuses { get; } =
        ["confirmed", "estimated", "provisional"];

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
    string? Status,
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
    string? Status,
    string? EvidenceLabel,
    string? Justification,
    ActorKind RecordedByKind,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc,
    string? ConfirmedBy,
    DateTimeOffset? ConfirmedAtUtc,
    decimal? PaintWorkUnits = null,
    int? Quantity = null,
    decimal? Materials = null,
    EstimateLineOrigin? Origin = null,
    string? SourceDocumentIdentity = null,
    Guid? SourceDocumentVersionId = null,
    string? SourceDocumentSha256 = null,
    string? SourceRowIdentity = null,
    string? AmendedBy = null,
    DateTimeOffset? AmendedAtUtc = null)
{
    public bool IsConfirmed => ConfirmedBy is not null;
}

/// <summary>
/// One recorded assessment field value with its provenance. A value written
/// by the Automation actor is stored unconfirmed; a staff save records a
/// confirmed value, and confirmation of a professional-finding field is
/// staff-Engineer-only. The permanent action history carries every before and
/// after value, so the current row never erases evidence.
/// </summary>
public sealed record AssessmentFieldValue(
    string Path,
    string Value,
    ActorKind RecordedByKind,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc,
    string? ConfirmedBy,
    DateTimeOffset? ConfirmedAtUtc)
{
    public bool IsConfirmed => ConfirmedBy is not null;
}

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
    long? Mileage,
    string? MileageUnit,
    DateOnly? IncidentDate,
    DateOnly? InstructionDate,
    string? InspectionMode,
    string? InspectionAddress);

public sealed record AssessmentReadinessItem(
    string Requirement,
    string Source,
    string WhyOutstanding,
    string HowToResolve);

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
/// path vocabulary (null clears), optionally a full replacement of the
/// ordered estimate-line collection, and the same actor, edit-lease,
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
    IReadOnlyList<EstimateLineInput>? EstimateLines = null,
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
