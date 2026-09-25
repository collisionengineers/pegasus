namespace Pegasus.Core.Vehicle;

/// <summary>
/// The MOT expiry the Case records (operator, 24 September 2026): the latest
/// expiry date any recorded DVSA MOT test carries. DVSA writes an expiry only
/// on a pass, so a later failure leaves the last pass's certificate standing,
/// and a vehicle with no expiry on record (too new for an MOT, or no history)
/// has none. Like <see cref="VehicleMileagePolicy"/> it reads only what DVSA
/// recorded: no test status is interpreted and nothing is extrapolated.
/// </summary>
public static class VehicleMotExpiryPolicy
{
    public static DateOnly? Latest(IReadOnlyList<MotTestObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        return observations.Max(observation => observation.ExpiryDate);
    }
}
