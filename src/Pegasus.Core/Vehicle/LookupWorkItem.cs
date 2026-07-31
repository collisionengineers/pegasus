namespace Pegasus.Core.Vehicle;

public enum VehicleLookupWorkState
{
    Pending,
    Processing,
    RetryScheduled,
    Completed,
    Failed,
    Poisoned
}

public sealed record VehicleLookupWorkItem(
    Guid Id,
    Guid CaseId,
    string Registration,
    string OperationKey,
    VehicleLookupWorkState State,
    int AttemptCount,
    DateTimeOffset DueAtUtc,
    string? LeaseToken,
    DateTimeOffset? LeaseExpiresAtUtc);

public sealed record VehicleLookupProcessedOutcome(
    VehicleLookupResult Result,
    VehicleMileageCalculation? Mileage);

public interface IVehicleLookupWorkStore
{
    Task<VehicleLookupWorkItem?> ClaimProcessingAsync(
        Guid workItemId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task RecordOutcomeAsync(
        Guid workItemId,
        string leaseToken,
        VehicleLookupProcessedOutcome outcome,
        VehicleLookupWorkState state,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken);
}

public interface IProcessQueuedVehicleLookup
{
    Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken);
}

public sealed class ProcessQueuedVehicleLookup(
    IVehicleLookupWorkStore workStore,
    IVehicleLookupAdapter lookupAdapter,
    TimeProvider timeProvider) : IProcessQueuedVehicleLookup
{
    private const int MaximumApplicationAttempts = 5;
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2)
    ];

    public async Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("A vehicle lookup work item identifier is required.", nameof(workItemId));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var workItem = await workStore.ClaimProcessingAsync(
            workItemId,
            nowUtc,
            ProcessingLease,
            cancellationToken);
        if (workItem is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(workItem.LeaseToken))
        {
            throw new InvalidOperationException("Claimed vehicle lookup work has no processing lease.");
        }

        if (workItem.AttemptCount < 1)
        {
            throw new InvalidOperationException("Claimed vehicle lookup work has an invalid attempt count.");
        }

        VehicleLookupRequest request;
        try
        {
            request = new VehicleLookupRequest(workItem.Registration);
        }
        catch (ArgumentException)
        {
            await RecordTerminalFailureAsync(
                workItem,
                nowUtc,
                "registration_invalid",
                cancellationToken);
            return;
        }

        VehicleLookupResult result;
        try
        {
            result = await lookupAdapter.LookupAsync(request, cancellationToken);
            result.EnsureValidFor(request);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                          or InvalidDataException
                                          or TimeoutException
                                          or UnauthorizedAccessException)
        {
            result = CreateFailure(
                request.Registration,
                nowUtc,
                "lookup_dependency_failure",
                retryable: true);
        }

        var processedOutcome = new VehicleLookupProcessedOutcome(
            result,
            VehicleMileagePolicy.Calculate(result.MotTests));
        var shouldRetry = result.Outcome == VehicleLookupOutcome.Throttled
            || result.Failure?.Retryable == true;
        if (shouldRetry && workItem.AttemptCount < MaximumApplicationAttempts)
        {
            var retryDelay = RetryDelays[Math.Clamp(workItem.AttemptCount - 1, 0, RetryDelays.Length - 1)];
            if (result.Failure?.RetryAfter is { } retryAfter && retryAfter > retryDelay)
            {
                retryDelay = retryAfter;
            }

            var maximumDelay = DateTimeOffset.MaxValue - nowUtc;
            if (retryDelay > maximumDelay)
            {
                retryDelay = maximumDelay;
            }

            await workStore.RecordOutcomeAsync(
                workItem.Id,
                workItem.LeaseToken,
                processedOutcome,
                VehicleLookupWorkState.RetryScheduled,
                nowUtc.Add(retryDelay),
                nowUtc,
                cancellationToken);
            return;
        }

        var terminalState = shouldRetry || result.Outcome == VehicleLookupOutcome.Failed
            ? VehicleLookupWorkState.Failed
            : VehicleLookupWorkState.Completed;
        await workStore.RecordOutcomeAsync(
            workItem.Id,
            workItem.LeaseToken,
            processedOutcome,
            terminalState,
            dueAtUtc: null,
            recordedAtUtc: nowUtc,
            cancellationToken);
    }

    private Task RecordTerminalFailureAsync(
        VehicleLookupWorkItem workItem,
        DateTimeOffset nowUtc,
        string failureCode,
        CancellationToken cancellationToken) =>
        workStore.RecordOutcomeAsync(
            workItem.Id,
            workItem.LeaseToken!,
            new(
                CreateFailure(workItem.Registration, nowUtc, failureCode, retryable: false),
                Mileage: null),
            VehicleLookupWorkState.Failed,
            dueAtUtc: null,
            recordedAtUtc: nowUtc,
            cancellationToken);

    private static VehicleLookupResult CreateFailure(
        string registration,
        DateTimeOffset nowUtc,
        string failureCode,
        bool retryable) =>
        new(
            registration,
            VehicleLookupOutcome.Failed,
            "vehicle-lookup",
            "1",
            $"failure:{failureCode}",
            nowUtc,
            EffectiveAtUtc: null,
            SourceObservedAtUtc: null,
            Vehicle: null,
            MotTests: [],
            Failure: new VehicleLookupFailure(failureCode, retryable));
}
