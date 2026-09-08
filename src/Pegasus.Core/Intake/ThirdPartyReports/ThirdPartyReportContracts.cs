using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>A typed observation with original value, units and exact source locator.</summary>
public sealed record ThirdPartyReportFact<T>(T Value, SourceFieldCandidate Source);

public enum ThirdPartyEstimateRole { Initial, Claimed, Assessed, Agreed, Revised, Supplement, ContractRepair }

public sealed record ThirdPartyReportIdentity(
    ThirdPartyReportFact<string?>? Issuer,
    ThirdPartyReportFact<string?>? EngineerName,
    ThirdPartyReportFact<string?>? EngineerQualifications,
    ThirdPartyReportFact<string?>? ReportReference,
    ThirdPartyReportFact<string?>? ClaimReference,
    ThirdPartyReportFact<DateOnly?>? ReportDate,
    ThirdPartyReportFact<string?>? Revision,
    ThirdPartyReportFact<string?>? Amendment,
    Guid? BaseReportDocumentId);

public sealed record ThirdPartyReportVehicle(
    ThirdPartyReportFact<string?>? Registration,
    ThirdPartyReportFact<string?>? Make,
    ThirdPartyReportFact<string?>? Model,
    ThirdPartyReportFact<string?>? Variant,
    ThirdPartyReportFact<string?>? Vin,
    ThirdPartyReportFact<decimal?>? Mileage,
    ThirdPartyReportFact<string?>? MileageUnit);

public sealed record ThirdPartyReportParties(
    ThirdPartyReportFact<string?>? Claimant,
    ThirdPartyReportFact<string?>? Repairer,
    ThirdPartyReportFact<string?>? RepairerAddress,
    ThirdPartyReportFact<string?>? VehicleLocation,
    ThirdPartyReportFact<DateOnly?>? AccidentDate,
    ThirdPartyReportFact<DateOnly?>? InspectionDate);

public sealed record ThirdPartyReportDamage(
    ThirdPartyReportFact<string?>? Outcome,
    ThirdPartyReportFact<string?>? Repairability,
    ThirdPartyReportFact<string?>? Roadworthiness,
    ThirdPartyReportFact<string?>? OutcomeReason,
    ThirdPartyReportFact<string?>? Severity,
    ThirdPartyReportFact<string?>? Narrative,
    ThirdPartyReportFact<string?>? PriorDamage,
    ThirdPartyReportFact<string?>? Tyres,
    ThirdPartyReportFact<string?>? Restraints,
    ThirdPartyReportFact<string?>? Airbags,
    IReadOnlyList<ThirdPartyReportFact<string?>> Zones);

public sealed record ThirdPartyReportEstimate(
    ThirdPartyEstimateRole Role,
    ThirdPartyReportFact<decimal?>? LabourHours,
    ThirdPartyReportFact<decimal?>? LabourRate,
    ThirdPartyReportFact<decimal?>? LabourAmount,
    ThirdPartyReportFact<decimal?>? PaintMaterials,
    ThirdPartyReportFact<decimal?>? Parts,
    ThirdPartyReportFact<decimal?>? SpecialistCharges,
    ThirdPartyReportFact<decimal?>? AdditionalCharges,
    ThirdPartyReportFact<decimal?>? Discounts,
    ThirdPartyReportFact<decimal?>? Net,
    ThirdPartyReportFact<decimal?>? VatRate,
    ThirdPartyReportFact<decimal?>? VatAmount,
    ThirdPartyReportFact<decimal?>? Gross);

public sealed record ThirdPartyReportValuation(
    ThirdPartyReportFact<string?>? Guide,
    ThirdPartyReportFact<DateOnly?>? GuideDate,
    ThirdPartyReportFact<decimal?>? Trade,
    ThirdPartyReportFact<decimal?>? Retail,
    ThirdPartyReportFact<decimal?>? Mid,
    ThirdPartyReportFact<decimal?>? PreAccidentValue,
    ThirdPartyReportFact<decimal?>? MileageAdjustment,
    ThirdPartyReportFact<decimal?>? ConditionAdjustment,
    ThirdPartyReportFact<decimal?>? FinalValue,
    ThirdPartyReportFact<string?>? SalvageCategory,
    ThirdPartyReportFact<decimal?>? SalvageValue,
    ThirdPartyReportFact<decimal?>? SalvageBid,
    ThirdPartyReportFact<decimal?>? Excess,
    ThirdPartyReportFact<decimal?>? Reserve,
    ThirdPartyReportFact<decimal?>? CashInLieu,
    IReadOnlyList<ThirdPartyReportFact<decimal?>> Deductions);

public sealed record ThirdPartyReportDeclaration(
    ThirdPartyReportFact<decimal?>? MinimumRepairDays,
    ThirdPartyReportFact<decimal?>? MaximumRepairDays,
    ThirdPartyReportFact<string?>? RequestedInspectionMethod,
    ThirdPartyReportFact<string?>? ObservedInspectionMethod,
    ThirdPartyReportFact<string?>? Comments,
    ThirdPartyReportFact<string?>? SupplementReason,
    ThirdPartyReportFact<string?>? Declaration,
    ThirdPartyReportFact<string?>? Signatory,
    IReadOnlyList<ThirdPartyReportFact<Guid?>> Photographs,
    IReadOnlyList<ThirdPartyReportFact<Guid?>> Diagrams);

/// <summary>Source evidence owned by C; B accepts CE findings through its named commands.</summary>
public sealed record ThirdPartyReportCandidate(
    Guid? DocumentId, Guid? DocumentVersionId, Guid? IntakeAssetId, string Sha256, int Occurrence,
    ThirdPartyReportIdentity Identity, ThirdPartyReportVehicle Vehicle,
    ThirdPartyReportParties Parties, ThirdPartyReportDamage Damage,
    IReadOnlyList<ThirdPartyReportEstimate> Estimates,
    ThirdPartyReportValuation Valuation, ThirdPartyReportDeclaration Declaration);

public interface IThirdPartyReportCandidateQueries
{
    Task<IReadOnlyList<ThirdPartyReportCandidate>> GetAsync(
        ActionActor actor, Guid receiptId, Guid? documentVersionId, Guid? intakeAssetId,
        CancellationToken cancellationToken);
}
