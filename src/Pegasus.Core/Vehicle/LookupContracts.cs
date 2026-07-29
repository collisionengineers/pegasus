namespace Pegasus.Core.Vehicle;

public enum VehicleLookupOutcome
{
    Current,
    Stale,
    Partial,
    NotFound,
    Throttled,
    Unavailable,
    Failed
}

public enum VehicleMileageUnit
{
    Miles,
    Kilometres
}

public sealed record VehicleLookupRequest
{
    public VehicleLookupRequest(string registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        if (registration.Length > 20 || registration.Any(character =>
                !char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character)))
        {
            throw new ArgumentException(
                "The vehicle registration must be the normalized uppercase intake value.",
                nameof(registration));
        }

        Registration = registration;
    }

    public string Registration { get; }
}

public sealed record VehicleDetails(
    string? Make,
    string? Model,
    int? ManufactureYear,
    int? EngineCapacityCc,
    string? FuelType);

public sealed record MotTestObservation(
    DateOnly TestDate,
    string TestStatus,
    DateOnly? ExpiryDate,
    long? Mileage,
    VehicleMileageUnit? MileageUnit);

public sealed record VehicleLookupFailure(
    string Code,
    bool Retryable,
    TimeSpan? RetryAfter = null);

public sealed record VehicleLookupResult(
    string Registration,
    VehicleLookupOutcome Outcome,
    string Provider,
    string ProviderVersion,
    string ResponseIdentity,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset? EffectiveAtUtc,
    DateTimeOffset? SourceObservedAtUtc,
    VehicleDetails? Vehicle,
    IReadOnlyList<MotTestObservation> MotTests,
    VehicleLookupFailure? Failure)
{
    public TimeSpan? SourceAge =>
        SourceObservedAtUtc is { } observedAtUtc ? RetrievedAtUtc - observedAtUtc : null;

    public void EnsureValidFor(VehicleLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(Registration, request.Registration, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The lookup response registration does not match the request.");
        }

        if (!Enum.IsDefined(Outcome))
        {
            throw new InvalidDataException("The vehicle lookup outcome is invalid.");
        }

        if (string.IsNullOrWhiteSpace(Provider)
            || string.IsNullOrWhiteSpace(ProviderVersion)
            || string.IsNullOrWhiteSpace(ResponseIdentity))
        {
            throw new InvalidDataException("Vehicle lookup provenance is incomplete.");
        }

        if (RetrievedAtUtc.Offset != TimeSpan.Zero
            || EffectiveAtUtc is { } effectiveAtUtc && effectiveAtUtc.Offset != TimeSpan.Zero
            || SourceObservedAtUtc is { } observedAtUtc && observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Vehicle lookup instants must be UTC.");
        }

        if (MotTests is null)
        {
            throw new InvalidDataException("The MOT observation collection is required.");
        }

        var hasEvidence = Vehicle is not null || MotTests.Count > 0;
        if ((Outcome is VehicleLookupOutcome.Current
                or VehicleLookupOutcome.Stale
                or VehicleLookupOutcome.Partial)
            && !hasEvidence)
        {
            throw new InvalidDataException("A successful or partial vehicle lookup must contain evidence.");
        }

        if ((Outcome is VehicleLookupOutcome.NotFound
                or VehicleLookupOutcome.Throttled
                or VehicleLookupOutcome.Unavailable
                or VehicleLookupOutcome.Failed)
            && hasEvidence)
        {
            throw new InvalidDataException("A non-evidence lookup outcome cannot contain vehicle or MOT evidence.");
        }

        if (hasEvidence && SourceObservedAtUtc is null)
        {
            throw new InvalidDataException("Vehicle evidence must retain its source observation time.");
        }

        if (SourceObservedAtUtc is { } sourceObservedAtUtc && sourceObservedAtUtc > RetrievedAtUtc)
        {
            throw new InvalidDataException("Vehicle evidence cannot be observed after it was retrieved.");
        }

        var requiresFailure = Outcome is VehicleLookupOutcome.Throttled
            or VehicleLookupOutcome.Unavailable
            or VehicleLookupOutcome.Failed;
        var permitsFailure = requiresFailure || Outcome == VehicleLookupOutcome.Partial;
        if ((requiresFailure && Failure is null) || (!permitsFailure && Failure is not null))
        {
            throw new InvalidDataException("The lookup failure metadata does not match the outcome.");
        }

        if (Failure is { } failure
            && (string.IsNullOrWhiteSpace(failure.Code)
                || failure.RetryAfter is { } retryAfter && retryAfter <= TimeSpan.Zero))
        {
            throw new InvalidDataException("Vehicle lookup failure metadata is invalid.");
        }

        if (Vehicle is { } vehicle
            && ((vehicle.Make is not null && string.IsNullOrWhiteSpace(vehicle.Make))
                || (vehicle.Model is not null && string.IsNullOrWhiteSpace(vehicle.Model))
                || (vehicle.FuelType is not null && string.IsNullOrWhiteSpace(vehicle.FuelType))
                || vehicle.ManufactureYear is <= 0
                || vehicle.EngineCapacityCc is <= 0))
        {
            throw new InvalidDataException("Vehicle details are invalid.");
        }

        foreach (var observation in MotTests)
        {
            if (string.IsNullOrWhiteSpace(observation.TestStatus)
                || observation.Mileage is < 0
                || (observation.Mileage is null) != (observation.MileageUnit is null)
                || observation.MileageUnit is { } unit && !Enum.IsDefined(unit))
            {
                throw new InvalidDataException("An MOT observation is invalid.");
            }
        }
    }
}

public interface IVehicleLookupAdapter
{
    Task<VehicleLookupResult> LookupAsync(
        VehicleLookupRequest request,
        CancellationToken cancellationToken);
}
