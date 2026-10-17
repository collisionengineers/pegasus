using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Vehicle;

/// <summary>
/// A save that replaced or cleared a Case mileage the MOT mileage estimate
/// filled: the estimate it set aside, the figure entered in its place (none
/// when cleared), who saved it and when. It is its own identity beside the
/// observation (FRD-06; operator, 16 October 2026); the figure entered reads
/// Custom like any other staff figure.
/// </summary>
public sealed record MotMileageEstimateDisposition(
    string MethodKey,
    int MethodVersion,
    DateOnly AsOf,
    long EstimatedMiles,
    long? EnteredMileage,
    ActorKind ActorKind,
    string ActorSubjectId,
    DateTimeOffset DisposedAtUtc)
{
    /// <summary>
    /// A save disposes of the estimate only when the Case mileage's source
    /// before it was the estimate (<see cref="CaseVehicleMileageSourcePolicy.IsEstimated"/>)
    /// and the save left another source, or none.
    /// </summary>
    public static bool Disposes(CaseDataSource? before, CaseDataSource? after) =>
        CaseVehicleMileageSourcePolicy.IsEstimated(before) && before != after;

    public static MotMileageEstimateDisposition For(
        MotMileageEstimate estimate,
        long? enteredMileage,
        ActionActor actor,
        DateTimeOffset disposedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(actor);
        if (estimate is not { Kind: MotMileageEstimateKind.Estimate, Miles: { } miles })
        {
            throw new ArgumentException("Only an estimate that filled a Case mileage is disposed of.", nameof(estimate));
        }

        return new(
            estimate.MethodKey,
            estimate.MethodVersion,
            estimate.AsOf,
            miles,
            enteredMileage,
            actor.Kind,
            actor.SubjectId,
            disposedAtUtc);
    }
}
