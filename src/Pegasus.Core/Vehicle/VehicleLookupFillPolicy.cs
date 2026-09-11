namespace Pegasus.Core.Vehicle;

/// <summary>
/// Where a vehicle lookup answer is allowed to land on a Case, and how the two
/// providers' descriptions of the same vehicle combine into one.
/// </summary>
/// <remarks>
/// Mileage follows the same ordering and is owned elsewhere: mileage extracted
/// from the instruction or its accompanying engineer report ranks first, and
/// the DVSA-derived MOT reading fills only where the Case holds no mileage of
/// its own. A lookup never overwrites what the Case already knows.
/// </remarks>
public static class VehicleLookupFillPolicy
{
    public const string PolicyKey = "vehicle-lookup-fill";
    public const int PolicyVersion = 1;

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
    /// would fail <see cref="VehicleLookupResult.EnsureValidFor"/>.
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
            Text(dvla?.FuelType) ?? Text(dvsa?.FuelType));

        return merged is { Make: null, Model: null, ManufactureYear: null, EngineCapacityCc: null, FuelType: null }
            ? null
            : merged;
    }

    /// <summary>A provider's text, with an empty or blank value read as absent.</summary>
    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A provider's count, with a zero or negative value read as absent.</summary>
    private static int? Positive(int? value) => value is > 0 ? value : null;
}
