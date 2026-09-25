using System.Globalization;
using Pegasus.Core.Assessment;

namespace Pegasus.Core.Vehicle;

/// <summary>
/// Where a vehicle lookup answer is allowed to land on a Case, and how the two
/// providers' descriptions of the same vehicle combine into one.
/// </summary>
/// <remarks>
/// Mileage follows the same ordering and is owned elsewhere: mileage extracted
/// from the instruction or its accompanying engineer report ranks first, and
/// the DVSA-derived MOT reading fills only where the Case holds no mileage of
/// its own. A lookup never overwrites what the Case already knows, except the
/// facts only it records (<see cref="AssessmentVocabulary.LookupDerivedPaths"/>),
/// which each answer sets (<see cref="DerivedAssessmentWrites"/>).
/// </remarks>
public static class VehicleLookupFillPolicy
{
    public const string PolicyKey = "vehicle-lookup-fill";
    public const int PolicyVersion = 1;

    /// <summary>
    /// The Automation actor a lookup answer records vehicle findings as, so a
    /// reader can tell a looked-up value from any other automation's.
    /// </summary>
    public const string RecorderId = "vehicle-lookup";

    /// <summary>
    /// A lookup fills a vehicle field only where the Case holds neither an
    /// extracted fact nor a staff-confirmed value.
    /// </summary>
    public static bool Fills(bool hasFact, bool hasConfirmed) => !hasFact && !hasConfirmed;

    /// <summary>
    /// DVLA answers first for every member, and DVSA fills each one DVLA left
    /// empty. DVLA's model is taken when it ever returns one; in practice VES
    /// never does, so the model comes from DVSA — which is why every
    /// production lookup held a null model until the MOT History vehicle
    /// object was read as well. Null when
    /// neither provider described the vehicle, and null when the merge would
    /// describe nothing: an all-null record is no evidence, and a blank member
    /// would fail <see cref="VehicleLookupResult.EnsureValidFor"/>. Colour is
    /// DVLA's, else DVSA's primary colour; the tax due date is DVLA's (DVSA
    /// supplies none).
    /// </summary>
    public static VehicleDetails? Merge(VehicleDetails? dvla, VehicleDetails? dvsa)
    {
        if (dvla is null && dvsa is null)
        {
            return null;
        }

        var merged = new VehicleDetails(
            Text(dvla?.Make) ?? Text(dvsa?.Make),
            Text(dvla?.Model) ?? Text(dvsa?.Model),
            Positive(dvla?.ManufactureYear) ?? Positive(dvsa?.ManufactureYear),
            Positive(dvla?.EngineCapacityCc) ?? Positive(dvsa?.EngineCapacityCc),
            Text(dvla?.FuelType) ?? Text(dvsa?.FuelType),
            Text(dvla?.TypeApproval) ?? Text(dvsa?.TypeApproval),
            Text(dvla?.Wheelplan) ?? Text(dvsa?.Wheelplan),
            Positive(dvla?.RevenueWeightKg) ?? Positive(dvsa?.RevenueWeightKg),
            Text(dvla?.Colour) ?? Text(dvsa?.Colour),
            dvla?.TaxDueDate ?? dvsa?.TaxDueDate);

        return merged is
        {
            Make: null,
            Model: null,
            ManufactureYear: null,
            EngineCapacityCc: null,
            FuelType: null,
            TypeApproval: null,
            Wheelplan: null,
            RevenueWeightKg: null,
            Colour: null,
            TaxDueDate: null
        }
            ? null
            : merged;
    }

    /// <summary>
    /// What one answer sets on the facts only the lookup records
    /// (<see cref="AssessmentVocabulary.LookupDerivedPaths"/>), keyed by path, each
    /// in the vocabulary's canonical form (operator, 24 September 2026). A fact the
    /// answer carries is its value. A complete answer (no provider failure; both
    /// providers' not-found included) maps a fact it does not carry to null, which
    /// clears the earlier value: the vehicle as now described has none. A partial or
    /// failed answer omits what it does not carry, so a failed provider's silence
    /// never erases an earlier answer. A provider value the vocabulary cannot hold
    /// is not carried.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> DerivedAssessmentWrites(VehicleLookupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var vehicle = result.Vehicle;
        var complete = result.Failure is null;
        var writes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in AssessmentVocabulary.LookupDerivedPaths)
        {
            var value = Carried(path, path switch
            {
                AssessmentVocabulary.VehicleEngineCc => vehicle?.EngineCapacityCc?.ToString(CultureInfo.InvariantCulture),
                AssessmentVocabulary.VehicleFuel => vehicle?.FuelType,
                AssessmentVocabulary.VehicleColour => vehicle?.Colour,
                AssessmentVocabulary.VehicleTaxExpiry => IsoDate(vehicle?.TaxDueDate),
                AssessmentVocabulary.VehicleMotExpiry => IsoDate(VehicleMotExpiryPolicy.Latest(result.MotTests)),
                _ => throw new InvalidOperationException($"The vehicle lookup derives no value for '{path}'.")
            });
            if (value is not null || complete)
            {
                writes[path] = value;
            }
        }

        return writes;
    }

    // The vocabulary owns the canonical form; a provider value it refuses (too
    // long, control characters, 0001-01-01) is not carried.
    private static string? Carried(string path, string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            return AssessmentPolicy.NormalizeFieldValue(path, raw);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? IsoDate(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A provider's text, with an empty or blank value read as absent.</summary>
    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A provider's count, with a zero or negative value read as absent.</summary>
    private static int? Positive(int? value) => value is > 0 ? value : null;
}
