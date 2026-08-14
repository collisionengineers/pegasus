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
    Date
}

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
    public const string IncidentAssessed = "incident.assessed";
    public const string ImpactSeverity = "assessment.impact_severity";
    public const string ImpactLocation = "assessment.impact_location";
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
    public const string HistoryCheck = "narrative.history_check";
    public const string EngineersComments = "narrative.engineers_comments";
    public const string EngineerName = "engineer.name";
    public const string EngineerQualifications = "engineer.qualifications";
    public const string EngineerSignature = "engineer.signature";
    public const string AgreedFee = "fee.agreed_fee";
    public const string FeeDescriptionLines = "fee.description_lines";
    public const string StatementOfTruth = "statement_of_truth";

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
        new(IncidentAssessed, AssessmentFieldType.Date, 10, IsFinding: false),
        new(ImpactSeverity, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes: ["light", "light_to_moderate", "moderate", "moderate_to_heavy", "heavy"]),
        new(ImpactLocation, AssessmentFieldType.Enumerated, 20, IsFinding: false,
            Codes:
            [
                "front", "left_front", "right_front", "left_side", "right_side", "rear",
                "left_rear", "right_rear", "roof", "underside", "wheel", "interior",
                "mechanical", "multiple"
            ]),
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
        new(StatementOfTruth, AssessmentFieldType.Text, 4000, IsFinding: false)
    ];

    public static IReadOnlyDictionary<string, AssessmentFieldDefinition> Definitions { get; } =
        DefinitionList.ToDictionary(definition => definition.Path, StringComparer.Ordinal);

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
    string? Justification);

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
    DateTimeOffset? ConfirmedAtUtc)
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
/// ADR-0021 / FRD-10 (docs/adr/0021-automation-actor-direct-write-assessment-contract.md,
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
