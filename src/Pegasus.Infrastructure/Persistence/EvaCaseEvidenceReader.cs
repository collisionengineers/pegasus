using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Eva;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads one case into the thirteen EVA field values, with their evidence
/// status and provenance.
///
/// This was private to <see cref="EvaHandoffStore"/> until EXT-04 gave the
/// case a second way to reach EVA. Both routes must state the case
/// identically — the same mileage, the same vehicle description, the same
/// resolved inspection address — so there is one reader, not two that agree
/// today. What the values then become is the caller's business: the export
/// writes them into the archive, the API submission renames them into EVA's
/// own field names.
/// </summary>
public static class EvaCaseEvidenceReader
{
    /// <summary>
    /// The thirteen EVA fields read off one case, written once.
    ///
    /// This used to take an <c>includeSuggestions</c> flag, which its own
    /// comment called "the whole difference between the hand-off and an
    /// operator export". With one act left there is one answer: a suggested
    /// value counts, and travels with its real suggested status — which is how
    /// the lookup-derived mileage ENG-013 writes reaches the archive.
    /// </summary>
    public static EvaAcceptedCaseEvidence Build(
        CaseDataProjection caseData,
        CaseVehicleEvidence? vehicle)
    {
        var caseId = caseData.Identity.CaseId;
        var inspection = ResolveInspection(caseData);
        var acceptedVehicle = vehicle?.CaseId == caseId
            ? vehicle.Confirmed
            : null;
        return new EvaAcceptedCaseEvidence(
            caseId,
            caseData.Version,
            caseData.AcceptedAtUtc != default,
            caseData.Completeness.Values.InstructionComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            caseData.Completeness.Values.ImagesComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            FromCaseField(caseData.Claim.Number, static value => value),
            FromCaseField(caseData.Provider.WorkProviderCode, static value => value),
            Fallback(
                FromVehicleField(acceptedVehicle?.Registration, static value => value),
                caseData.Vehicle.Registration,
                static value => value),
            VehicleModel(acceptedVehicle, caseData),
            FromCaseField(caseData.Claimant.Name, static value => value),
            FromCaseField(caseData.Accident.IncidentDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            FromCaseField(caseData.Instruction.InstructionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
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

    private static EvaAddressResolution ResolveInspection(CaseDataProjection caseData)
    {
        var mode = Accepted(caseData.Inspection.Mode);
        var address = Accepted(caseData.Inspection.Address);
        if (mode is null || address is null)
        {
            return new(
                mode?.Value == CaseInspectionMode.ImageBasedAssessment
                    ? EvaInspectionMode.ImageBasedAssessment
                    : EvaInspectionMode.PhysicalAddress,
                MissingEvidence);
        }

        var modeEvidence = FromCaseValue(mode, static value => value.ToString());
        var addressEvidence = FromCaseValue(address, static value => value);
        var evidence = addressEvidence with
        {
            Status = EvaEvidenceStatus.Accepted,
            Source = $"{modeEvidence.Source}|{addressEvidence.Source}",
            SourceVersion = $"{modeEvidence.SourceVersion}|{addressEvidence.SourceVersion}"
        };
        return mode.Value switch
        {
            CaseInspectionMode.ImageBasedAssessment => new(
                EvaInspectionMode.ImageBasedAssessment,
                evidence),
            CaseInspectionMode.PhysicalAddress
                when !string.Equals(
                    address.Value.Trim(),
                    CaseEvaMapping.ImageBasedAssessment,
                    StringComparison.Ordinal) => new(
                        EvaInspectionMode.PhysicalAddress,
                        evidence),
            _ => new(EvaInspectionMode.PhysicalAddress, evidence with
            {
                Status = EvaEvidenceStatus.Suggested
            })
        };
    }

    /// <summary>
    /// Make and model as one value, from whichever source the case has.
    ///
    /// The staff-confirmed vehicle record wins, exactly as before. What changed
    /// (ENG-015) is the fallback: it used to read <c>Vehicle.Model</c> alone, so
    /// an export carried "X5 SE - X DRIVE Type 5 DOOR SUV" where EVA is sent
    /// "BMW X5 …". Both branches now compose the same way, so the two cannot
    /// state the vehicle differently.
    /// </summary>
    private static EvaEvidenceValue VehicleModel(
        ConfirmedVehicleEvidence? vehicle,
        CaseDataProjection caseData)
    {
        var confirmed = Compose(
            vehicle?.Make is null ? null : FromVehicleField(vehicle.Make, static value => value),
            vehicle?.Model is null ? null : FromVehicleField(vehicle.Model, static value => value));
        return string.IsNullOrWhiteSpace(confirmed.Value)
            ? Compose(
                FromCaseField(caseData.Vehicle.Make, static value => value),
                FromCaseField(caseData.Vehicle.Model, static value => value))
            : confirmed;
    }

    /// <summary>Make and model joined, skipping whichever the case lacks.</summary>
    private static EvaEvidenceValue Compose(EvaEvidenceValue? make, EvaEvidenceValue? model)
    {
        var values = new[] { make, model }
            .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => value!)
            .ToArray();
        return values.Length == 0 ? MissingEvidence : values.Aggregate(Combine);
    }

    /// <summary>
    /// EVA's own two words for the mileage unit (ENG-015). The original
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

    private static EvaEvidenceValue FromCaseField<T>(
        CaseField<T> field,
        Func<T, string> format)
        where T : notnull =>
        Accepted(field) is { } value
            ? FromCaseValue(value, format)
            : field.Suggestion is { } suggestion
                ? FromCaseValue(suggestion, format) with { Status = EvaEvidenceStatus.Suggested }
                : MissingEvidence;

    /// <summary>
    /// The vehicle fields have their own confirmed record. The export falls
    /// back to the case's own field when that record has nothing — which is
    /// where ENG-013 writes what the DVLA and DVSA lookup found, so an export
    /// carries a mileage the documents never supplied. It never overrides a
    /// confirmed value.
    /// </summary>
    private static EvaEvidenceValue Fallback<T>(
        EvaEvidenceValue confirmed,
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

    private static EvaEvidenceValue FromCaseValue<T>(
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
            EvaEvidenceStatus.Accepted,
            $"case-data:{value.Source.Kind}:{value.Source.Identity}:{value.Source.Label}{confirmed}",
            sourceVersion);
    }

    private static EvaEvidenceValue FromVehicleField<T>(
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
            EvaEvidenceStatus.Accepted,
            $"vehicle:{field.SourceKind}:{field.SourceIdentity}:{field.SourceLabel};confirmed={field.ConfirmedByActor}@{field.ConfirmedAtUtc:O}{external}",
            sourceVersion);
    }

    private static EvaEvidenceValue Combine(EvaEvidenceValue first, EvaEvidenceValue second) => new(
        string.Join(' ', new[] { first.Value, second.Value }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())),
        first.IsAccepted && second.IsAccepted
            ? EvaEvidenceStatus.Accepted
            : EvaEvidenceStatus.Suggested,
        $"{first.Source}|{second.Source}",
        $"{first.SourceVersion}|{second.SourceVersion}");

    private static EvaEvidenceValue MissingEvidence { get; } =
        new(null, EvaEvidenceStatus.Unrecorded, "unrecorded", "unrecorded");
}
