using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Reports;

/// <summary>
/// The result of comparing one accepted write with the facts a generated
/// report freezes. A non-staling change has no reason code; a staling change
/// always uses the governed report vocabulary.
/// </summary>
public sealed record CaseReportFreshnessDecision(bool IsStale, string? ReasonCode)
{
    public static readonly CaseReportFreshnessDecision NotStale = new(false, null);

    public static CaseReportFreshnessDecision Stale(string reasonCode) =>
        new(true, reasonCode);
}

/// <summary>
/// The valuation facts that can change an already generated report. These are
/// effective report values, not the full editable valuation card.
/// </summary>
public sealed record CaseReportValuationDependencies(
    bool UsesGlassesValuationGuide,
    string? EngineersValue,
    Guid? AppliedValuationId,
    decimal? AcceptedEngineerValue,
    string? AppliedValuationReason);

/// <summary>
/// The accepted estimate identity a generated report freezes. Accepted
/// estimates are immutable, so identity and version are the effective
/// dependency rather than the command used to select them.
/// </summary>
public sealed record CaseReportEstimateDependencies(
    Guid? CurrentEstimateId,
    int? CurrentEstimateVersion);

/// <summary>
/// The effective vehicle facts selected for report projection, including the
/// provenance-derived mileage source rather than the rows a lookup happened
/// to write.
/// </summary>
public sealed record CaseReportVehicleDependencies(
    string? Make,
    string? Model,
    string? Year,
    long? Mileage,
    string? MileageUnit,
    string MileageSource);

/// <summary>
/// One narrow owner for deciding whether an accepted Case write changed a
/// frozen report dependency. Inputs are the normalized effective before and
/// after values, not merely the fields a caller happened to submit.
/// </summary>
public static class CaseReportFreshness
{
    private static readonly IReadOnlySet<string> ReportContentPaths = new HashSet<string>(
        StringComparer.Ordinal)
    {
        AssessmentVocabulary.EngineersComments,
        AssessmentVocabulary.ReportValuationCommentaryText,
        AssessmentVocabulary.ReportDate,
        AssessmentVocabulary.AgreedFee,
        AssessmentVocabulary.FeeDescriptionLines,
    };

    private static readonly IReadOnlySet<string> ReportSwitchPaths = new HashSet<string>(
        StringComparer.Ordinal)
    {
        AssessmentVocabulary.ReportDiscloseGuideSource,
        AssessmentVocabulary.ReportValuationCommentary,
        AssessmentVocabulary.ReportIncludeUnrelatedDamage,
        AssessmentVocabulary.ReportDateOverride,
    };

    private static readonly IReadOnlySet<string> PrintedAssessmentPaths = new HashSet<string>(
        StringComparer.Ordinal)
    {
        AssessmentVocabulary.VehicleType,
        AssessmentVocabulary.VehicleVin,
        AssessmentVocabulary.VehicleEngineCc,
        AssessmentVocabulary.VehicleFuel,
        AssessmentVocabulary.VehicleCondition,
        AssessmentVocabulary.VehicleVinChecked,
        AssessmentVocabulary.VehicleTransmission,
        AssessmentVocabulary.VehicleColour,
        AssessmentVocabulary.VehicleBody,
        AssessmentVocabulary.VehicleTaxExpiry,
        AssessmentVocabulary.VehicleMotExpiry,
        AssessmentVocabulary.VehicleAirbagsDeployed,
        AssessmentVocabulary.VehicleFaultCodes,
        AssessmentVocabulary.VehicleTemporaryRepairsPossible,
        AssessmentVocabulary.VehicleTemporaryRepairMethod,
        AssessmentVocabulary.VehicleTemporaryRepairCost,
        AssessmentVocabulary.IncidentAssessed,
        AssessmentVocabulary.ImpactSeverity,
        AssessmentVocabulary.ImpactLocation,
        AssessmentVocabulary.DamageImpacts,
        AssessmentVocabulary.DamageTyreRightFront,
        AssessmentVocabulary.DamageTyreLeftFront,
        AssessmentVocabulary.DamageTyreRightRear,
        AssessmentVocabulary.DamageTyreLeftRear,
        AssessmentVocabulary.DamageBeltRightFront,
        AssessmentVocabulary.DamageBeltLeftFront,
        AssessmentVocabulary.DamageBeltRightRear,
        AssessmentVocabulary.DamageBeltLeftRear,
        AssessmentVocabulary.DamageSpareTyre,
        AssessmentVocabulary.DamageCentreBelt,
        AssessmentVocabulary.DamageUnrelated,
        AssessmentVocabulary.DamageUnrelatedDeduction,
        AssessmentVocabulary.DamageMaterialTransfer,
        AssessmentVocabulary.ValueRetail,
        AssessmentVocabulary.ValueTrade,
        AssessmentVocabulary.ValueEngineer,
        AssessmentVocabulary.CostRecoveryCharge,
        AssessmentVocabulary.Outcome,
        AssessmentVocabulary.LegalStatus,
        AssessmentVocabulary.UnroadworthyReason,
        AssessmentVocabulary.SalvageCategory,
        AssessmentVocabulary.SalvageValue,
        AssessmentVocabulary.HistoryCheck,
        AssessmentVocabulary.SettlementExcess,
        AssessmentVocabulary.SettlementBetterment,
        AssessmentVocabulary.SettlementClaimantVatRegistered,
        AssessmentVocabulary.SettlementReserve,
        AssessmentVocabulary.SettlementRepairDelays,
        AssessmentVocabulary.SettlementReportDelay,
        AssessmentVocabulary.SettlementStoragePerDay,
        AssessmentVocabulary.SettlementHireStart,
        AssessmentVocabulary.SettlementHireDailyCost,
        AssessmentVocabulary.SettlementDiminution,
        AssessmentVocabulary.SettlementSalvageAt,
        AssessmentVocabulary.SettlementSalvageAgent,
        AssessmentVocabulary.SettlementSalvageAgentReference,
        AssessmentVocabulary.SettlementSalvageMoved,
        AssessmentVocabulary.SettlementSalvageOwnerRetains,
        AssessmentVocabulary.SettlementSalvageValueAgreed,
        AssessmentVocabulary.SettlementSalvageSettled,
    };

    public static CaseReportFreshnessDecision ClassifyWorkspace(
        CaseEditableData beforeData,
        CaseEditableData afterData,
        IReadOnlyDictionary<string, string?> beforeAssessment,
        IReadOnlyDictionary<string, string?> afterAssessment,
        Guid? beforeSignOffEngineerId,
        Guid? afterSignOffEngineerId,
        IReadOnlyList<CaseAssetPreparation>? beforeImages = null,
        IReadOnlyList<CaseAssetPreparation>? afterImages = null,
        string? beforeMileageSource = null,
        string? afterMileageSource = null)
    {
        var signatory = ClassifySignatory(beforeSignOffEngineerId, afterSignOffEngineerId);
        if (signatory.IsStale)
        {
            return signatory;
        }
        if (beforeImages is not null && afterImages is not null && ImagesDiffer(beforeImages, afterImages))
        {
            return CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.ImagePreparationChanged);
        }

        var assessment = ClassifyAssessment(
            beforeAssessment,
            afterAssessment,
            beforeMileageSource,
            afterMileageSource);
        if (assessment.IsStale)
        {
            return assessment;
        }
        return CaseFactsDiffer(beforeData, afterData)
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.AssessmentFactsChanged)
            : CaseReportFreshnessDecision.NotStale;
    }

    public static CaseReportFreshnessDecision ClassifySignatory(
        Guid? beforeSignOffEngineerId,
        Guid? afterSignOffEngineerId) =>
        beforeSignOffEngineerId != afterSignOffEngineerId
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.SignatoryChanged)
            : CaseReportFreshnessDecision.NotStale;

    public static CaseReportFreshnessDecision ClassifyAssessment(
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after,
        string? beforeMileageSource = null,
        string? afterMileageSource = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (PathsDiffer(ReportContentPaths, before, after)
            || SwitchesDiffer(ReportSwitchPaths, before, after))
        {
            return CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.ReportContentChanged);
        }
        return PathsDiffer(PrintedAssessmentPaths, before, after)
            || !string.Equals(beforeMileageSource, afterMileageSource, StringComparison.Ordinal)
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.AssessmentFactsChanged)
            : CaseReportFreshnessDecision.NotStale;
    }

    public static CaseReportFreshnessDecision ClassifyValuation(
        CaseReportValuationDependencies before,
        CaseReportValuationDependencies after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return before != after
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.ValuationChanged)
            : CaseReportFreshnessDecision.NotStale;
    }

    public static CaseReportFreshnessDecision ClassifyEstimate(
        CaseReportEstimateDependencies before,
        CaseReportEstimateDependencies after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return before != after
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.EstimateChanged)
            : CaseReportFreshnessDecision.NotStale;
    }

    public static CaseReportFreshnessDecision ClassifyVehicle(
        CaseReportVehicleDependencies before,
        CaseReportVehicleDependencies after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return before != after
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.AssessmentFactsChanged)
            : CaseReportFreshnessDecision.NotStale;
    }

    public static CaseReportFreshnessDecision ClassifyCaseData(
        CaseEditableData before,
        CaseEditableData after) =>
        CaseFactsDiffer(before, after)
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.AssessmentFactsChanged)
            : CaseReportFreshnessDecision.NotStale;

    public static CaseReportFreshnessDecision ClassifyImages(
        IReadOnlyList<CaseAssetPreparation> before,
        IReadOnlyList<CaseAssetPreparation> after) =>
        ImagesDiffer(before, after)
            ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.ImagePreparationChanged)
            : CaseReportFreshnessDecision.NotStale;

    private static bool CaseFactsDiffer(CaseEditableData before, CaseEditableData after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        before = CaseDataPolicy.Normalize(before);
        after = CaseDataPolicy.Normalize(after);
        return before.ClaimantName != after.ClaimantName
            || before.ClaimNumber != after.ClaimNumber
            || before.VehicleRegistration != after.VehicleRegistration
            || before.VehicleMake != after.VehicleMake
            || before.VehicleModel != after.VehicleModel
            || before.VehicleYear != after.VehicleYear
            || before.VehicleMileage != after.VehicleMileage
            || before.VehicleMileageUnit != after.VehicleMileageUnit
            || before.IncidentDate != after.IncidentDate
            || before.InstructionDate != after.InstructionDate
            || before.InspectionMode != after.InspectionMode
            || before.InspectionAddress != after.InspectionAddress;
    }

    private static bool PathsDiffer(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after) =>
        paths.Any(path => !string.Equals(
            before.GetValueOrDefault(path), after.GetValueOrDefault(path), StringComparison.Ordinal));

    private static bool SwitchesDiffer(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after) =>
        paths.Any(path => IsOn(before.GetValueOrDefault(path)) != IsOn(after.GetValueOrDefault(path)));

    private static bool IsOn(string? value) => string.Equals(value, "true", StringComparison.Ordinal);

    private static bool ImagesDiffer(
        IReadOnlyList<CaseAssetPreparation> before,
        IReadOnlyList<CaseAssetPreparation> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var beforeReport = CaseAssetPreparationPolicy.ForReport(before);
        var afterReport = CaseAssetPreparationPolicy.ForReport(after);
        return !beforeReport.Select(MaterialImage).SequenceEqual(afterReport.Select(MaterialImage));
    }

    private static MaterialReportImage MaterialImage(PreparedReportImage image) => new(
        image.OccurrenceId,
        image.VersionId,
        image.Role,
        image.Order,
        image.Rotation,
        image.Crop,
        image.FullPage);

    private sealed record MaterialReportImage(
        Guid OccurrenceId,
        Guid VersionId,
        CaseAssetReportRole Role,
        int? Order,
        CaseAssetRotation Rotation,
        CaseAssetCrop Crop,
        bool FullPage);
}
