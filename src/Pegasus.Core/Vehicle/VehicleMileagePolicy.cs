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
