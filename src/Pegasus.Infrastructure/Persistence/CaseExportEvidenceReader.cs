using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.CaseExport;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads one case into the thirteen export field values, with their evidence
/// status and provenance, for <see cref="EfCaseExportStore"/>.
/// </summary>
public static class CaseExportEvidenceReader
{
    /// <summary>
    /// The thirteen export fields read off one case, written once.
    ///
    /// A suggested value counts, and travels with its real suggested status —
    /// which is how the lookup-derived mileage the vehicle lookup writes
    /// reaches the archive.
    /// </summary>
    public static CaseExportEvidence Build(
        CaseDataProjection caseData,
        CaseVehicleEvidence? vehicle)
    {
        var caseId = caseData.Identity.CaseId;
        var inspection = ResolveInspection(caseData);
        var acceptedVehicle = vehicle?.CaseId == caseId
            ? vehicle.Confirmed
            : null;
        return new CaseExportEvidence(
            caseId,
            caseData.Version,
            caseData.AcceptedAtUtc != default,
            caseData.Completeness.Values.InstructionComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            caseData.Completeness.Values.ImagesComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            FromCaseField(caseData.Claim.Number, static value => value),
            FromCaseField(caseData.Principal.PrincipalCode, static value => value),
            Fallback(
                FromVehicleField(acceptedVehicle?.Registration, static value => value),
                caseData.Vehicle.Registration,
                static value => value),
            VehicleModel(acceptedVehicle, caseData),
            FromCaseField(caseData.Claimant.Name, static value => value),
            FromCaseField(caseData.Accident.IncidentDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            caseData.Instruction.ReceivedDate,
            FromCaseField(caseData.Inspection.InspectionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            inspection,
            FromCaseField(caseData.Accident.Circumstances, static value => value),
            FromCaseField(caseData.Instruction.VatStatus, static value => value),
            Fallback(
                FromVehicleField(acceptedVehicle?.Mileage, static value => value.ToString(CultureInfo.InvariantCulture)),
                caseData.Vehicle.Mileage,
                static value => value.ToString(CultureInfo.InvariantCulture)),
            Fallback(
                FromVehicleField(acceptedVehicle?.MileageUnit, MileageUnit),
                caseData.Vehicle.MileageUnit,
                MileageUnit));
    }

    private static CaseExportAddressResolution ResolveInspection(CaseDataProjection caseData)
    {
        var mode = Accepted(caseData.Inspection.Mode);
        var address = Accepted(caseData.Inspection.Address);
        if (mode is null || address is null)
        {
            return new(
                mode?.Value == CaseInspectionMode.ImageBasedAssessment
                    ? CaseExportInspectionMode.ImageBasedAssessment
                    : CaseExportInspectionMode.PhysicalAddress,
                MissingEvidence);
        }

        var modeEvidence = FromCaseValue(mode, static value => value.ToString());
        var addressEvidence = FromCaseValue(address, static value => value);
        var evidence = addressEvidence with
        {
            Status = CaseExportEvidenceStatus.Accepted,
            Source = $"{modeEvidence.Source}|{addressEvidence.Source}",
            SourceVersion = $"{modeEvidence.SourceVersion}|{addressEvidence.SourceVersion}"
        };
        return mode.Value switch
        {
            CaseInspectionMode.ImageBasedAssessment => new(
                CaseExportInspectionMode.ImageBasedAssessment,
                evidence),
            CaseInspectionMode.PhysicalAddress
                when !string.Equals(
                    address.Value.Trim(),
                    CaseExportMapping.ImageBasedAssessment,
                    StringComparison.Ordinal) => new(
                        CaseExportInspectionMode.PhysicalAddress,
                        evidence),
            _ => new(CaseExportInspectionMode.PhysicalAddress, evidence with
            {
                Status = CaseExportEvidenceStatus.Suggested
            })
        };
    }

    /// <summary>
    /// Make and model as one value, from whichever source the case has.
    ///
    /// Each staff-confirmed component wins independently. A missing confirmed
    /// make or model falls back to that component's accepted Case field, so a
    /// partial confirmation cannot drop the other source-backed component.
    /// </summary>
    private static CaseExportEvidenceValue VehicleModel(
        ConfirmedVehicleEvidence? vehicle,
        CaseDataProjection caseData)
        => Compose(
            Fallback(
                vehicle?.Make is null ? MissingEvidence : FromVehicleField(vehicle.Make, static value => value),
                caseData.Vehicle.Make,
                static value => value),
            Fallback(
                vehicle?.Model is null ? MissingEvidence : FromVehicleField(vehicle.Model, static value => value),
                caseData.Vehicle.Model,
                static value => value));

    /// <summary>Make and model joined, skipping whichever the case lacks.</summary>
    private static CaseExportEvidenceValue Compose(CaseExportEvidenceValue? make, CaseExportEvidenceValue? model)
    {
        var values = new[] { make, model }
            .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => value!)
            .ToArray();
        return values.Length == 0 ? MissingEvidence : values.Aggregate(Combine);
    }

    /// <summary>
    /// The export's two words for the mileage unit. The original
    /// extractor resolves this field to exactly "Miles" or "Km", so those are
    /// the only two values a bundle may carry — written once here so the
    /// confirmed-record branch and the case-field branch cannot drift.
    /// </summary>
    private static string MileageUnit(VehicleMileageUnit unit) =>
        unit == VehicleMileageUnit.Kilometres ? "Km" : "Miles";

    private static string MileageUnit(string value) =>
        Enum.TryParse<VehicleMileageUnit>(value, ignoreCase: true, out var unit)
            ? MileageUnit(unit)
            : value.Trim();

    private static CaseExportEvidenceValue FromCaseField<T>(
        CaseField<T> field,
        Func<T, string> format)
        where T : notnull =>
        Accepted(field) is { } value
            ? FromCaseValue(value, format)
            : field.Suggestion is { } suggestion
                ? FromCaseValue(suggestion, format) with { Status = CaseExportEvidenceStatus.Suggested }
                : MissingEvidence;

    /// <summary>
    /// The vehicle fields have their own confirmed record. The export falls
    /// back to the case's own field when that record has nothing — which is
    /// where the vehicle lookup writes what DVLA and DVSA found, so an export
    /// carries a mileage the documents never supplied. It never overrides a
    /// confirmed value.
    /// </summary>
    private static CaseExportEvidenceValue Fallback<T>(
        CaseExportEvidenceValue confirmed,
        CaseField<T> field,
        Func<T, string> format)
        where T : notnull =>
        string.IsNullOrWhiteSpace(confirmed.Value)
            ? FromCaseField(field, format)
            : confirmed;

    private static CaseDataValue<T>? Accepted<T>(CaseField<T> field)
        where T : notnull =>
        field.Confirmed is { IsAccepted: true } confirmed
            ? confirmed
            : field.Fact is { IsAccepted: true } fact
                ? fact
                : null;

    private static CaseExportEvidenceValue FromCaseValue<T>(
        CaseDataValue<T> value,
        Func<T, string> format)
        where T : notnull
    {
        var sourceVersion = !string.IsNullOrWhiteSpace(value.Source.PolicyKey)
                            && value.Source.PolicyVersion > 0
            ? $"{value.Source.PolicyKey.Trim()}/v{value.Source.PolicyVersion}"
            : string.Empty;
        var confirmed = value.ConfirmedByActor is null
            ? string.Empty
            : $";confirmed={value.ConfirmedByActor}@{value.ConfirmedAtUtc:O}";
        return new(
            format(value.Value),
            CaseExportEvidenceStatus.Accepted,
            $"case-data:{value.Source.Kind}:{value.Source.Identity}:{value.Source.Label}{confirmed}",
            sourceVersion);
    }

    private static CaseExportEvidenceValue FromVehicleField<T>(
        ConfirmedVehicleField<T>? field,
        Func<T, string> format)
        where T : notnull
    {
        if (field is null)
        {
            return MissingEvidence;
        }

        var external = field.ExternalProvenance is null
            ? string.Empty
            : $";provider={field.ExternalProvenance.Provider};response={field.ExternalProvenance.ResponseIdentity};observed={field.ExternalProvenance.RetrievedAtUtc:O}";
        var sourceVersion = !string.IsNullOrWhiteSpace(field.PolicyKey)
                            && field.PolicyVersion > 0
            ? $"{field.PolicyKey.Trim()}/v{field.PolicyVersion}"
            : string.Empty;
        return new(
            format(field.Value),
            CaseExportEvidenceStatus.Accepted,
            $"vehicle:{field.SourceKind}:{field.SourceIdentity}:{field.SourceLabel};confirmed={field.ConfirmedByActor}@{field.ConfirmedAtUtc:O}{external}",
            sourceVersion);
    }

    private static CaseExportEvidenceValue Combine(CaseExportEvidenceValue first, CaseExportEvidenceValue second) => new(
        string.Join(' ', new[] { first.Value, second.Value }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())),
        first.IsAccepted && second.IsAccepted
            ? CaseExportEvidenceStatus.Accepted
            : CaseExportEvidenceStatus.Suggested,
        $"{first.Source}|{second.Source}",
        $"{first.SourceVersion}|{second.SourceVersion}");

    private static CaseExportEvidenceValue MissingEvidence { get; } =
        new(null, CaseExportEvidenceStatus.Unrecorded, "unrecorded", "unrecorded");
}
