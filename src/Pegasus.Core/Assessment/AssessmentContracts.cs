using Pegasus.Core.Cases;
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
/// One impact as staff enter it on the Damage section (v46, operator,
/// 3 November 2026): a severity, a location and a free-text note, each blank
/// until chosen. An impact names both a severity and a location before the
/// report prints it (<see cref="IsComplete"/>); the note never prints.
/// </summary>
public sealed record AssessmentImpact(string Severity, string Location, string Note)
{
    public bool IsComplete => Severity.Length > 0 && Location.Length > 0;
}

/// <summary>
/// One assessment field. <paramref name="Format"/> describes the value a
/// structured field accepts, for a caller with no editor to shape it.
/// </summary>
public sealed record AssessmentFieldDefinition(
    string Path,
    AssessmentFieldType Type,
    int MaximumLength,
    bool MustBePositive = false,
    IReadOnlyList<string>? Codes = null,
    string? Format = null);

public static class AssessmentVocabulary
{
    public const string VehicleType = "vehicle.vehicle_type";
    public const string VehicleVin = "vehicle.vin";
    public const string VehicleEngineCc = "vehicle.engine_cc";
    public const string VehicleFuel = "vehicle.fuel";
    public const string VehicleCondition = "vehicle.condition";
    public const string VehicleTransmission = "vehicle.transmission";
    public const string VehicleColour = "vehicle.colour";
    public const string VehicleBody = "vehicle.body";
    public const string VehicleTaxExpiry = "vehicle.tax_expiry";
    public const string VehicleMotExpiry = "vehicle.mot_expiry";
    /// <summary>The impacts staff enter, severity, location and note (v46).</summary>
    public const string Impacts = "assessment.impacts";
    /// <summary>
    /// Skip impacts (v47, operator, 3 November 2026): the deliberate choice to
    /// send the report with no impact, offered only while no impact row exists.
    /// </summary>
    public const string ImpactsSkipped = "assessment.impacts_skipped";
    /// <summary>
    /// The damage markers drawn on the plan for the report's picture, positions
    /// only (v46). The path keeps the name it had when a marker also carried
    /// its areas, severity and note, so a caller's path is unchanged.
    /// </summary>
    public const string DamageMarkers = "damage.impacts";
    public const string DamageUnrelated = "damage.unrelated";
    public const string ValueRetail = "assessment.values.retail";
    public const string ValueTrade = "assessment.values.trade";
    public const string ValueFromMarketResearch = "assessment.values.from_market_research";
    public const string ValueEngineer = "assessment.values.engineer";
    public const string RateCard = "rates.card";
    public const string RateClass = "rates.class";
    public const string RateManufacturerApproved = "rates.manufacturer_approved";
    public const string RateRegionalUplift = "rates.regional_uplift";
    public const string CostRecoveryCharge = "costs.recovery_charge";
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

    /// <summary>
    /// The two report choices that are on until recorded off (operator,
    /// 2 November 2026; R610, R611): the "FAO The Court" line above the
    /// report's addressee, and the statement of truth.
    /// </summary>
    public const string ReportFaoTheCourt = "report.fao_the_court";
    public const string ReportIncludeStatementOfTruth = "report.include_statement_of_truth";
    public const string ReportDate = "report.report_date";
    public const string EngineerName = "engineer.name";
    public const string EngineerQualifications = "engineer.qualifications";
    public const string EngineerSignature = "engineer.signature";
    public const string AgreedFee = "fee.agreed_fee";
    public const string FeeDescriptionLines = "fee.description_lines";
    public const string SettlementClaimantVatRegistered = "settlement.claimant_vat_registered";
    public const string SettlementStoragePerDay = "settlement.storage_per_day";
    /// <summary>The agreed contract repair sum (v28 P35): the Engineer's figure the report prints as the cap.</summary>
    public const string SettlementContractSum = "settlement.contract_sum";
    public const string OriginalReportAssessor = "original_report.assessor";
    public const string OriginalReportDate = "original_report.report_date";
    public const string OriginalReportRoadworthiness = "original_report.roadworthiness";
    public const string OriginalReportOutcome = "original_report.outcome";

    /// <summary>
    /// The locations an impact names, in the order the Case page lists them:
    /// the eight areas of the plan and the three it cannot show (v46 item B,
    /// operator, 3 November 2026). There is no "multiple": each impact is its
    /// own row.
    /// </summary>
    public static IReadOnlyList<string> DamageAreaCodes { get; } =
    [
        "front", "left_front", "right_front", "left_side", "right_side", "rear",
        "left_rear", "right_rear", "underside", "interior", "mechanical"
    ];

    /// <summary>
    /// Every location with its name, as the Case page shows it. The report
    /// names a location in its own words
    /// (<see cref="Pegasus.Core.Reports.ReportWordingComposition.Words"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, string> DamageAreas { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["front"] = "Front", ["left_front"] = "LH Front", ["right_front"] = "RH Front",
            ["left_side"] = "LH Side", ["right_side"] = "RH Side", ["rear"] = "Rear",
            ["left_rear"] = "LH Rear", ["right_rear"] = "RH Rear",
            ["underside"] = "Underside", ["interior"] = "Interior", ["mechanical"] = "Mechanical"
        };

    /// <summary>Every severity with its name; <c>Rank</c> is the order the Case page lists them in.</summary>
    public static IReadOnlyDictionary<string, (string Display, int Rank)> DamageSeverities { get; } =
        new Dictionary<string, (string, int)>(StringComparer.Ordinal)
        {
            ["light"] = ("Light", 0), ["light_to_moderate"] = ("Light to moderate", 1),
            ["moderate"] = ("Moderate", 2), ["moderate_to_heavy"] = ("Moderate to heavy", 3),
            ["heavy"] = ("Heavy", 4)
        };

    /// <summary>The value <see cref="Impacts"/> accepts, as its reader enforces it.</summary>
    public static string ImpactsFormat { get; } =
        "A JSON array of impacts in the order the report prints them, each an object with exactly these members: "
        + "\"severity\", one of " + string.Join(", ", DamageSeverities.Keys) + ", or empty; "
        + "\"location\", one of " + string.Join(", ", DamageAreaCodes) + ", or empty; "
        + "\"note\", a string of up to 200 characters, empty when there is nothing to add. "
        + "An impact the report prints names both a severity and a location; the note does not print. "
        + "An impact with all three empty is dropped. "
        + "Example: [{\"severity\":\"moderate\",\"location\":\"rear\",\"note\":\"\"}]";

    /// <summary>The value <see cref="DamageMarkers"/> accepts, as its reader enforces it.</summary>
    public static string DamageMarkersFormat { get; } =
        "A JSON array of the damage markers drawn on the plan for the report's picture, each an object "
        + "with exactly the numeric members \"x\", \"y\" and \"r\": x and y from 0 to 1 across and down the plan, "
        + "r from 0.0641 to 0.5 of its width. A marker records a position only; the impacts are assessment.impacts. "
        + "Example: [{\"x\":0.5,\"y\":0.92,\"r\":0.12}]";


    private static readonly AssessmentFieldDefinition[] DefinitionList =
    [
        new(VehicleType, AssessmentFieldType.Enumerated, 20,
            Codes: ["car", "van", "motorcycle", "scooter", "bicycle", "trailer", "caravan", "other"]),
        new(VehicleVin, AssessmentFieldType.Text, 30),
        new(VehicleEngineCc, AssessmentFieldType.WholeNumber, 10),
        new(VehicleFuel, AssessmentFieldType.Text, 40),
        new(VehicleCondition, AssessmentFieldType.Enumerated, 20,
            Codes: ["poor", "below_average", "average", "good", "excellent"]),
        new(VehicleTransmission, AssessmentFieldType.Enumerated, 20,
            Codes: ["manual", "automatic", "semi_automatic", "cvt", "unknown"]),
        new(VehicleColour, AssessmentFieldType.Text, 40),
        new(VehicleBody, AssessmentFieldType.Text, 100),
        new(VehicleTaxExpiry, AssessmentFieldType.Date, 10),
        new(VehicleMotExpiry, AssessmentFieldType.Date, 10),
        new(Impacts, AssessmentFieldType.Json, 4000, Format: ImpactsFormat),
        new(ImpactsSkipped, AssessmentFieldType.Flag, 5),
        new(DamageMarkers, AssessmentFieldType.Json, 4000, Format: DamageMarkersFormat),
        new(DamageUnrelated, AssessmentFieldType.Text, 2000),
        new(ValueRetail, AssessmentFieldType.Money, 20, MustBePositive: true),
        new(ValueTrade, AssessmentFieldType.Money, 20, MustBePositive: true),
        new(ValueFromMarketResearch, AssessmentFieldType.Flag, 5),
        new(ValueEngineer, AssessmentFieldType.Money, 20, MustBePositive: true),
        new(RateCard, AssessmentFieldType.Text, 100),
        new(RateClass, AssessmentFieldType.Enumerated, 20,
            Codes: ["standard", "prestige", "van"]),
        new(RateManufacturerApproved, AssessmentFieldType.Flag, 5),
        new(RateRegionalUplift, AssessmentFieldType.Flag, 5),
        new(CostRecoveryCharge, AssessmentFieldType.Money, 20),
        new(Outcome, AssessmentFieldType.Enumerated, 20,
            Codes: ["total_loss", "repairable", "cash_in_lieu", "contract_repair"]),
        new(LegalStatus, AssessmentFieldType.Enumerated, 20,
            Codes: ["roadworthy", "unroadworthy"]),
        new(UnroadworthyReason, AssessmentFieldType.Text, 2000),
        new(SalvageCategory, AssessmentFieldType.Enumerated, 5,
            Codes: ["A", "B", "S", "N", "N/A"]),
        new(SalvageValue, AssessmentFieldType.Money, 20),
        new(HistoryCheck, AssessmentFieldType.Text, 4000),
        new(EngineersComments, AssessmentFieldType.Text, 4000),
        new(EngineerName, AssessmentFieldType.Text, 200),
        new(EngineerQualifications, AssessmentFieldType.Text, 200),
        new(EngineerSignature, AssessmentFieldType.Text, 200),
        new(AgreedFee, AssessmentFieldType.Money, 20, MustBePositive: true),
        new(FeeDescriptionLines, AssessmentFieldType.Text, 2000),
        new(ReportDiscloseGuideSource, AssessmentFieldType.Flag, 5),
        new(ReportValuationCommentary, AssessmentFieldType.Flag, 5),
        new(ReportValuationCommentaryText, AssessmentFieldType.Text, 4000),
        new(ReportIncludeUnrelatedDamage, AssessmentFieldType.Flag, 5),
        new(ReportFaoTheCourt, AssessmentFieldType.Flag, 5),
        new(ReportIncludeStatementOfTruth, AssessmentFieldType.Flag, 5),
        new(ReportDate, AssessmentFieldType.Date, 10),
        new(SettlementClaimantVatRegistered, AssessmentFieldType.Flag, 5),
        new(SettlementStoragePerDay, AssessmentFieldType.Money, 20),
        new(SettlementContractSum, AssessmentFieldType.Money, 20),
        // An Audit's original report (v28 P51): who wrote it, when, and what it found.
        new(OriginalReportAssessor, AssessmentFieldType.Text, 200),
        new(OriginalReportDate, AssessmentFieldType.Date, 10),
        new(OriginalReportRoadworthiness, AssessmentFieldType.Enumerated, 20,
            Codes: ["roadworthy", "unroadworthy"]),
        new(OriginalReportOutcome, AssessmentFieldType.Enumerated, 20,
            Codes: ["repairable", "total_loss", "cash_in_lieu", "contract_repair"]),
    ];

    public static IReadOnlyDictionary<string, AssessmentFieldDefinition> Definitions { get; } =
        DefinitionList.ToDictionary(definition => definition.Path, StringComparer.Ordinal);

    /// <summary>
    /// The report's Retail and Trade values: the chosen guide card's figures
    /// (operator, 8 October 2026), and whether that card is AI market research,
    /// which carries no Trade (R311). The Case save writes them from the card a
    /// calculation is chosen against
    /// (<see cref="ValuationPolicy.ReportValues"/>); no field save types or
    /// clears one.
    /// </summary>
    public static IReadOnlySet<string> GuideCardDerivedPaths { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ValueRetail,
        ValueTrade,
        ValueFromMarketResearch
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

    /// <summary>
    /// What each evidence label says about where a line's figure came from
    /// (operator, 7 October 2026). The label is the kind of source; the
    /// source itself is written in the line's justification.
    /// </summary>
    public static IReadOnlyDictionary<string, string> EvidenceLabelMeanings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["official"] = "Manufacturer or official repair/price data.",
            ["reference"] = "A published reference or guide (Glass's/Audatex times, ABP).",
            ["case"] = "Evidence on this Case (photos, documents, repairer estimate).",
            ["judgement"] = "The assessor's professional judgement.",
        };
}

/// <summary>
/// The one table that says what a Glass's paint level is: the export's
/// (<c>PaintMatKind</c>, <c>PaintLevel</c>) pair, the level the calculation
/// sheet prints for it, and the estimate line type it lands as. Both Glass's
/// readers use it, so the same row lands as the same line whichever document
/// it came from. The legend is the sheet's own: <c>I</c> new part, <c>B</c>
/// adjacent panel blend, <c>III</c> repair up to 50%, <c>IV</c> repair over
/// 50%, <c>SP</c> spot-repair, <c>II</c> inner surface (a repair of an
/// existing panel; operator reading, 5 October 2026), and for plastic
/// <c>K1R</c>, <c>K1N</c> and <c>K1G</c> raw or primed parts (new),
/// <c>K2</c> surface spraying (repair), and plastic level 5, which the sheet
/// prints as <c>B</c> (adjacent panel blend). A pair or printed level outside
/// the table is unknown, and each reader refuses the document.
/// </summary>
public static class GlassPaintLevels
{
    private sealed record Level(string MaterialKind, int Number, string Printed, string LineType);

    private static readonly Level[] Levels =
    [
        new("B", 3, "I", "paint_new"),
        new("B", 4, "B", "paint_blend"),
        new("B", 1, "III", "paint_repair"),
        new("B", 2, "IV", "paint_repair"),
        new("B", 6, "SP", "paint_repair"),
        new("B", 0, "II", "paint_repair"),
        new("K", 2, "K1R", "paint_new"),
        new("K", 3, "K1N", "paint_new"),
        new("K", 4, "K1G", "paint_new"),
        new("K", 0, "K2", "paint_repair"),
        // Two export pairs print the same code (B): a sheet's printed B is
        // read through the first entry, and both pairs land as a blend.
        new("K", 5, "B", "paint_blend"),
    ];

    /// <summary>The line type of an export's paint level, or null when the pair is not in the table.</summary>
    public static string? LineTypeOfExport(string? materialKind, int level) => Levels
        .FirstOrDefault(entry => string.Equals(entry.MaterialKind, materialKind, StringComparison.Ordinal)
            && entry.Number == level)?.LineType;

    /// <summary>The line type of a level as the calculation sheet prints it, or null when it is not in the table.</summary>
    public static string? LineTypeOfPrinted(string? printed) => Levels
        .FirstOrDefault(entry => string.Equals(entry.Printed, printed, StringComparison.Ordinal))?.LineType;
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
/// ADR-0031 / FRD-10 (docs/frd/frd-10-mcp-automation-and-actor-boundary.md).
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
    string? ClaimNumber,
    // A one-off Case for a private customer (R574): the report and fee note
    // are addressed to the claimant at this address, and the claim number
    // plays no part in readiness or Your Ref (R576, R578).
    string? ClaimantAddress = null,
    bool PrivateCustomer = false);

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
/// expected-version, and operation-key guards as every case mutation.
/// </summary>
public sealed record SaveAssessmentRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    IReadOnlyDictionary<string, string?> Fields)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ICaseAssessmentStore
{
    Task<CaseAssessmentProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken);

    Task<CaseAssessmentProjection> SaveAsync(
        SaveAssessmentRequest request,
        CancellationToken cancellationToken);
}

public interface IGetCaseAssessment
{
    Task<CaseAssessmentProjection?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken);
}
