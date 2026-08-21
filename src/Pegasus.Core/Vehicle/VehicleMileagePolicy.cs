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
    public const int MethodVersion = 1;

    public static VehicleMileageCalculation? Calculate(
        IReadOnlyList<MotTestObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        DateOnly? latestDate = null;
        long latestValue = 0;
        VehicleMileageUnit latestUnit = default;
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

            if (latestDate is null || observation.TestDate > latestDate.Value)
            {
                latestDate = observation.TestDate;
                latestValue = value;
                latestUnit = unit;
                supportingCount = 1;
                conflicting = false;
                continue;
            }

            if (observation.TestDate != latestDate.Value)
            {
                continue;
            }

            if (value != latestValue || unit != latestUnit)
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
                latestValue,
                latestUnit,
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
