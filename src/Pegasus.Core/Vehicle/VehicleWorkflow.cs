using Pegasus.Core.Identity;

namespace Pegasus.Core.Vehicle;

public enum VehicleSuggestionDecision
{
    Accept
}

public enum VehicleSuggestionField
{
    Make,
    Model,
    Mileage
}

public sealed record VehicleConfirmationValues(
    string Registration,
    string? Make,
    string? Model,
    long? Mileage,
    VehicleMileageUnit? MileageUnit);

public sealed record VehicleEvidenceProvenance(
    string Provider,
    string ProviderVersion,
    string ResponseIdentity,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset? EffectiveAtUtc,
    DateTimeOffset? SourceObservedAtUtc);

public sealed record VehicleLookupObservation(
    Guid Id,
    Guid WorkItemId,
    Guid CaseId,
    int AttemptNumber,
    VehicleLookupOutcome Outcome,
    string Registration,
    VehicleEvidenceProvenance Provenance,
    VehicleDetails? Vehicle,
    IReadOnlyList<MotTestObservation> MotTests,
    VehicleMileageCalculation? Mileage,
    VehicleLookupFailure? Failure,
    DateTimeOffset RecordedAtUtc);

public sealed record VehicleConfirmationHistory(
    Guid Id,
    Guid CaseId,
    Guid LookupObservationId,
    VehicleSuggestionDecision Decision,
    VehicleConfirmationValues Values,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    DateTimeOffset OccurredAtUtc,
    long BeforeCaseVersion,
    long AfterCaseVersion,
    string PolicyKey,
    int PolicyVersion);

public sealed record ConfirmedVehicleField<T>(
    T Value,
    string SourceKind,
    string SourceIdentity,
    string SourceLabel,
    string PolicyKey,
    int PolicyVersion,
    string ConfirmedByActor,
    DateTimeOffset ConfirmedAtUtc,
    VehicleEvidenceProvenance? ExternalProvenance)
    where T : notnull;

public sealed record ConfirmedVehicleEvidence(
    ConfirmedVehicleField<string> Registration,
    ConfirmedVehicleField<string>? Make,
    ConfirmedVehicleField<string>? Model,
    ConfirmedVehicleField<long>? Mileage,
    ConfirmedVehicleField<VehicleMileageUnit>? MileageUnit);

public sealed record CaseVehicleEvidence(
    Guid CaseId,
    ConfirmedVehicleEvidence? Confirmed,
    VehicleLookupObservation? LatestObservation,
    IReadOnlyList<VehicleLookupObservation> Observations,
    IReadOnlyList<VehicleConfirmationHistory> ConfirmationHistory);

public sealed record RequestVehicleLookupCommand(
    Guid CaseId,
    long ExpectedCaseVersion,
    string Registration,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken);

public sealed record RequestedVehicleLookup(
    Guid WorkItemId,
    Guid CaseId,
    string Registration,
    VehicleLookupWorkState State,
    long ResultingCaseVersion,
    bool IsReplay);

public sealed record AcceptVehicleSuggestionCommand(
    Guid CaseId,
    long ExpectedCaseVersion,
    Guid LookupObservationId,
    VehicleSuggestionDecision Decision,
    VehicleConfirmationValues? Correction,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
{
    public VehicleSuggestionField Field { get; init; } = VehicleSuggestionField.Make;
}

public sealed record AcceptedVehicleSuggestion(
    Guid ConfirmationId,
    Guid CaseId,
    Guid LookupObservationId,
    VehicleSuggestionDecision Decision,
    VehicleConfirmationValues Values,
    VehicleEvidenceProvenance Provenance,
    long ResultingCaseVersion,
    bool IsReplay);

public sealed record VehicleLookupAvailability(bool RequestsEnabled, string Mode)
{
    public static VehicleLookupAvailability Unavailable { get; } =
        new(false, "unavailable");

    public static VehicleLookupAvailability DevelopmentOfflineReplay { get; } =
        new(true, "development_offline_replay");

    public static VehicleLookupAvailability ProductionLive { get; } =
        new(true, "production_live");
}

public interface IRequestVehicleLookup
{
    Task<RequestedVehicleLookup> ExecuteAsync(
        RequestVehicleLookupCommand command,
        CancellationToken cancellationToken);
}

public interface IAcceptVehicleSuggestion
{
    Task<AcceptedVehicleSuggestion> ExecuteAsync(
        AcceptVehicleSuggestionCommand command,
        CancellationToken cancellationToken);
}

public interface IRequestVehicleLookupStore
{
    Task<RequestedVehicleLookup> RequestAsync(
        RequestVehicleLookupCommand command,
        CancellationToken cancellationToken);
}

public interface IAcceptVehicleSuggestionStore
{
    Task<AcceptedVehicleSuggestion> AcceptAsync(
        AcceptVehicleSuggestionCommand command,
        CancellationToken cancellationToken);
}

public interface IVehicleEvidenceQueries
{
    Task<CaseVehicleEvidence?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

public sealed class RequestVehicleLookup(
    IRequestVehicleLookupStore store,
    VehicleLookupAvailability availability,
    Pegasus.Core.Custody.ICommittedExternalWorkPublisher committedExternalWorkPublisher) : IRequestVehicleLookup
{
    private readonly IRequestVehicleLookupStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly VehicleLookupAvailability availability =
        availability ?? throw new ArgumentNullException(nameof(availability));

    public async Task<RequestedVehicleLookup> ExecuteAsync(
        RequestVehicleLookupCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCaseMutation(
            command.CaseId,
            command.ExpectedCaseVersion,
            command.Actor,
            command.OperationKey,
            command.EditLeaseToken);
        if (!availability.RequestsEnabled)
        {
            throw new VehicleLookupUnavailableException(availability.Mode);
        }

        var registration = new VehicleLookupRequest(command.Registration).Registration;
        var outcome = await store.RequestAsync(
            command with
            {
                Registration = registration,
                OperationKey = command.OperationKey.Trim(),
                EditLeaseToken = command.EditLeaseToken.Trim()
            },
            cancellationToken);
        await committedExternalWorkPublisher.PublishAsync(outcome.WorkItemId, cancellationToken);
        return outcome;
    }

    internal static void ValidateCaseMutation(
        Guid caseId,
        long expectedCaseVersion,
        ActionActor actor,
        string operationKey,
        string editLeaseToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        if (expectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCaseVersion),
                "The expected case version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        RequireText(operationKey, 100, "An operation key is required.", nameof(operationKey));
        RequireText(
            editLeaseToken,
            128,
            "An active edit lease token is required.",
            nameof(editLeaseToken));
    }

    internal static void RequireText(
        string value,
        int maximumLength,
        string message,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }
        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}

public sealed class AcceptVehicleSuggestion(
    IAcceptVehicleSuggestionStore store) : IAcceptVehicleSuggestion
{
    private readonly IAcceptVehicleSuggestionStore store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<AcceptedVehicleSuggestion> ExecuteAsync(
        AcceptVehicleSuggestionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequestVehicleLookup.ValidateCaseMutation(
            command.CaseId,
            command.ExpectedCaseVersion,
            command.Actor,
            command.OperationKey,
            command.EditLeaseToken);
        if (command.LookupObservationId == Guid.Empty)
        {
            throw new ArgumentException(
                "A vehicle lookup observation identifier is required.",
                nameof(command));
        }
        if (!Enum.IsDefined(command.Decision))
        {
            throw new ArgumentOutOfRangeException(nameof(command), "The vehicle decision is invalid.");
        }
        if (!Enum.IsDefined(command.Field))
        {
            throw new ArgumentOutOfRangeException(nameof(command), "The vehicle field is invalid.");
        }
        if (command.Decision != VehicleSuggestionDecision.Accept || command.Correction is not null)
        {
            throw new ArgumentException(
                "Vehicle lookup suggestions are accepted one field at a time.",
                nameof(command));
        }

        RequestVehicleLookup.RequireText(
            command.Reason,
            500,
            "A reason for accepting or correcting the vehicle suggestion is required.",
            nameof(command));
        return store.AcceptAsync(
            command with
            {
                OperationKey = command.OperationKey.Trim(),
                Reason = command.Reason.Trim(),
                EditLeaseToken = command.EditLeaseToken.Trim()
            },
            cancellationToken);
    }
}

public static class VehicleSuggestionAcceptancePolicy
{
    public const string PolicyKey = "vehicle-suggestion-acceptance";
    public const int PolicyVersion = 1;

    public static VehicleConfirmationValues Resolve(
        VehicleLookupObservation observation,
        VehicleSuggestionDecision decision,
        VehicleConfirmationValues? correction)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Outcome is not (
            VehicleLookupOutcome.Current or
            VehicleLookupOutcome.Stale or
            VehicleLookupOutcome.Partial))
        {
            throw new VehicleSuggestionUnavailableException(observation.Id, observation.Outcome);
        }

        var values = decision switch
        {
            VehicleSuggestionDecision.Accept => new VehicleConfirmationValues(
                observation.Registration,
                observation.Vehicle?.Make,
                observation.Vehicle?.Model,
                observation.Mileage?.Value,
                observation.Mileage?.Unit),
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };
        ValidateValues(values);
        return Normalize(values);
    }

    public static void ValidateValues(VehicleConfirmationValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _ = new VehicleLookupRequest(values.Registration);
        ValidateOptional(values.Make, 100, nameof(values.Make));
        ValidateOptional(values.Model, 100, nameof(values.Model));
        if (values.Mileage is < 0
            || (values.Mileage is null) != (values.MileageUnit is null)
            || values.MileageUnit is { } unit && !Enum.IsDefined(unit))
        {
            throw new ArgumentException(
                "Vehicle mileage requires a non-negative value and recognized unit together.",
                nameof(values));
        }
    }

    public static VehicleConfirmationValues Normalize(VehicleConfirmationValues values) =>
        values with
        {
            Registration = new VehicleLookupRequest(values.Registration).Registration,
            Make = NormalizeOptional(values.Make),
            Model = NormalizeOptional(values.Model)
        };

    private static void ValidateOptional(string? value, int maximumLength, string parameterName)
    {
        if (value is null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameterName} must contain text no longer than {maximumLength} characters when supplied.",
                parameterName);
        }
    }

    private static string? NormalizeOptional(string? value) => value?.Trim();
}

public sealed class VehicleLookupUnavailableException(string mode)
    : InvalidOperationException("Vehicle lookup is unavailable in the current runtime profile.")
{
    public string Mode { get; } = mode;
}

public sealed class VehicleOperationConflictException(Guid caseId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to case '{caseId}' with different vehicle inputs.")
{
    public Guid CaseId { get; } = caseId;
    public string OperationKey { get; } = operationKey;
}

public sealed class VehicleSuggestionUnavailableException(
    Guid observationId,
    VehicleLookupOutcome outcome)
    : InvalidOperationException(
        $"Vehicle lookup observation '{observationId}' with outcome '{outcome}' cannot be accepted.")
{
    public Guid ObservationId { get; } = observationId;
    public VehicleLookupOutcome Outcome { get; } = outcome;
}

public sealed class ConfirmedVehicleRegistrationRequiredException(
    Guid caseId,
    int confirmedRegistrationCount)
    : InvalidOperationException(
        $"Case '{caseId}' must have exactly one confirmed canonical vehicle registration before lookup.")
{
    public Guid CaseId { get; } = caseId;
    public int ConfirmedRegistrationCount { get; } = confirmedRegistrationCount;
}

public sealed class ConfirmedVehicleRegistrationConflictException(
    Guid caseId,
    string confirmedRegistration,
    string proposedRegistration)
    : InvalidOperationException(
        $"Case '{caseId}' already has a different confirmed vehicle registration. Use an explicit correction operation.")
{
    public Guid CaseId { get; } = caseId;
    public string ConfirmedRegistration { get; } = confirmedRegistration;
    public string ProposedRegistration { get; } = proposedRegistration;
}

/// <summary>
/// The store boundary for the automatic-lookup sweep: enqueue one lookup for
/// every active case whose current registration has no request yet, up to the
/// batch limit, and report how many were enqueued.
/// </summary>
public interface IAutomaticVehicleLookupStore
{
    Task<int> EnqueueDueAsync(int maximumItems, CancellationToken cancellationToken);
}

/// <summary>
/// Enqueues a vehicle lookup automatically whenever a case has a known current
/// registration (confirmed, else an extracted fact) that has never been looked
/// up — so DVSA/DVLA evidence and the mileage estimate arrive without a staff
/// request. Idempotent per case and registration; a corrected registration is
/// a new pair and gets one new lookup. Does nothing where lookups are not
/// composed.
/// </summary>
public sealed class ReconcileAutomaticVehicleLookups(
    IAutomaticVehicleLookupStore store,
    VehicleLookupAvailability availability)
{
    private readonly IAutomaticVehicleLookupStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly VehicleLookupAvailability availability =
        availability ?? throw new ArgumentNullException(nameof(availability));

    public Task<int> ExecuteAsync(int maximumItems, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        return availability.RequestsEnabled
            ? store.EnqueueDueAsync(maximumItems, cancellationToken)
            : Task.FromResult(0);
    }
}
