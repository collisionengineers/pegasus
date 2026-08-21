using Pegasus.Core.Cases;

namespace Pegasus.Core.Vehicle;

public sealed record VehicleMileageCalculation(
    long Value,
    VehicleMileageUnit Unit,
    DateOnly ObservedOn,
    string MethodKey,
    int MethodVersion,
    int SupportingObservationCount);

/// <summary>
/// Produces only the latest exact MOT odometer observation. It performs no extrapolation,
/// unit conversion, cohort inference, or valuation. Conflicting readings on the latest date
/// remain unresolved rather than selecting one by input order.
/// </summary>
public static class VehicleMileagePolicy
{
    public const string MethodKey = "latest-mot-observation";

    /// <summary>
    /// Version 2 states the derived mileage in miles whatever unit the MOT
    /// recorded, per operator direction (2026-08-21). A kilometre reading
    /// used to travel through unconverted, and every consumer that asks for
    /// miles — the Assessment prefill among them — simply ignored it, so an
    /// imported vehicle showed no mileage at all despite a full MOT history.
    /// </summary>
    public const int MethodVersion = 2;

    /// <summary>Exact, by definition: one mile is 1.609344 kilometres.</summary>
    private const double KilometresPerMile = 1.609344;

    /// <summary>
    /// The MOT reading expressed in miles. Rounds to the nearest whole mile
    /// away from zero, so a converted value never reads as more precise than
    /// the odometer it came from. The raw observation keeps its own unit —
    /// only the derived Case value is normalised.
    /// </summary>
    public static long ToMiles(long value, VehicleMileageUnit unit) =>
        unit == VehicleMileageUnit.Kilometres
            ? (long)Math.Round(value / KilometresPerMile, MidpointRounding.AwayFromZero)
            : value;

    public static VehicleMileageCalculation? Calculate(
        IReadOnlyList<MotTestObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        DateOnly? latestDate = null;
        long latestMiles = 0;
        var supportingCount = 0;
        var conflicting = false;

        foreach (var observation in observations)
        {
            if (observation.Mileage is not { } value
                || observation.MileageUnit is not { } unit
                || value < 0
                || !Enum.IsDefined(unit))
            {
                continue;
            }

            // Compared in miles, so the same reading recorded once in miles
            // and once in kilometres agrees with itself instead of reading
            // as a conflict and abstaining.
            var miles = ToMiles(value, unit);

            if (latestDate is null || observation.TestDate > latestDate.Value)
            {
                latestDate = observation.TestDate;
                latestMiles = miles;
                supportingCount = 1;
                conflicting = false;
                continue;
            }

            if (observation.TestDate != latestDate.Value)
            {
                continue;
            }

            if (miles != latestMiles)
            {
                conflicting = true;
            }
            else
            {
                supportingCount++;
            }
        }

        return latestDate is null || conflicting
            ? null
            : new(
                latestMiles,
                VehicleMileageUnit.Miles,
                latestDate.Value,
                MethodKey,
                MethodVersion,
                supportingCount);
    }
}

/// <summary>
/// The operator-facing evidence classes for a mileage figure. The enum names are the settled
/// operator words (design authority, Case section): a value written in instructions or entered
/// by staff is Supplied, a recorded MOT odometer reading is External, and a value produced by
/// <see cref="VehicleMileagePolicy"/> from MOT observations is Estimated.
/// </summary>
public enum VehicleMileageEvidenceClass
{
    Supplied,
    External,
    Estimated
}

/// <summary>
/// Classifies a case mileage value by its recorded source. A lookup-sourced case mileage is by
/// construction the derived <see cref="VehicleMileageCalculation"/> — accepting a vehicle
/// suggestion stores the calculation, never a raw reading — so it classifies as Estimated and
/// must never be presented as Supplied (operator truth: a mileage calculated from accepted MOT
/// observations is a derived estimate; never relabel it as supplied mileage). A raw
/// <see cref="MotTestObservation"/> reading displays as <see cref="VehicleMileageEvidenceClass.External"/>
/// at its own surface; it is not a case value and has no <see cref="CaseDataSourceKind"/>.
/// </summary>
public static class VehicleMileageEvidenceClassification
{
    public static VehicleMileageEvidenceClass Classify(CaseDataSourceKind sourceKind) =>
        sourceKind == CaseDataSourceKind.VehicleLookup
            ? VehicleMileageEvidenceClass.Estimated
            : VehicleMileageEvidenceClass.Supplied;
}
