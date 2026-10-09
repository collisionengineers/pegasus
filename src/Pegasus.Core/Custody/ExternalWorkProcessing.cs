using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Custody;

public static class ExternalWorkKinds
{
    public const string CreateCaseCustody = "create_case_custody";
    public const string CreateAuditReferenceCustody = "create_audit_reference_custody";
    public const string CreateImageCaseCustody = "create_image_case_custody";
    public const string MergeImageCaseCustody = "merge_image_case_custody";
    public const string VehicleLookup = "vehicle_lookup";
    public const string IntakeOcr = "intake_ocr";
}

public sealed record QueuedExternalWork(Guid Id, string Kind);

/// <summary>
/// The one owner of image-case custody re-arm decisions: which persisted
/// failure codes may retry, the attempt cap, and the backoff schedule.
/// Image-case custody has no staff-facing case surface to re-arm it from, so
/// a dependency-shaped failure retries itself the same way vehicle lookup
/// does — pending with a future due time — until the cap makes it terminal.
/// </summary>
public static class ImageCustodyRetryPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    public static int MaximumAttempts => RetryDelays.Length + 1;

    public static TimeSpan? NextAttemptDelay(int attemptCount, string failureCode) =>
        attemptCount < 1
        || attemptCount >= MaximumAttempts
        || failureCode is not (
            "custody_dependency_failure" or "custody_lease_lost" or "custody_cancelled")
            ? null
            : RetryDelays[attemptCount - 1];
}

/// <summary>
/// The one owner of how long the ten-second pending-custody sweep leaves an
/// item alone after its filing failed. The wait grows with the number of
/// consecutive failed attempts, so a fault that no retry can cure costs a few
/// attempts an hour instead of one every tick. An attempt that only retained the
/// item (its Case had no folder yet, or the Case changed) is not a failure and
/// never delays anything.
/// </summary>
public static class PendingCustodyRetryPolicy
{
    /// <summary>
    /// How long the sweep leaves a newly recorded pending version to the
    /// request that recorded it. That request records the version first and
    /// then files it in Box under its fixed name; a sweep that filed it too
    /// would upload the same name at the same moment, and Box refuses one of
    /// the two as <c>name_temporarily_reserved</c> (a.QDOS26101's Glass's
    /// return, 9 October 2026). An upload takes seconds, so two minutes is
    /// ample, and a version its request never filed is still filed soon after.
    /// </summary>
    public static TimeSpan InlineFilingGrace { get; } = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan[] Waits =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(10)
    ];

    /// <summary>The longest wait; an item whose last failure is older is always due.</summary>
    public static TimeSpan LongestWait => Waits[^1];

    public static bool IsWaiting(
        int consecutiveFailures,
        DateTimeOffset lastFailedAtUtc,
        DateTimeOffset nowUtc) =>
        consecutiveFailures >= 1
        && nowUtc < lastFailedAtUtc + Waits[Math.Min(consecutiveFailures, Waits.Length) - 1];
}

public interface IQueuedExternalWorkReader
{
    Task<QueuedExternalWork?> GetAsync(Guid workItemId, CancellationToken cancellationToken);
}

public interface IProcessQueuedExternalWork
{
    Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves one ID-only durable external-work row and invokes exactly one typed handler.
/// Unknown persisted kinds fail closed and are never treated as custody by default.
/// </summary>
public sealed class ProcessQueuedExternalWork(
    IQueuedExternalWorkReader workReader,
    IProcessQueuedCustody custody,
    IProcessQueuedVehicleLookup vehicle,
    IProcessIntakeOcr? intakeOcr = null) : IProcessQueuedExternalWork
{
    public async Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        var work = await workReader.GetAsync(workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The external work item is unavailable.");
        if (work.Id != workItemId)
        {
            throw new InvalidDataException(
                "The external-work reader returned a different durable identifier.");
        }

        switch (work.Kind)
        {
            case ExternalWorkKinds.CreateCaseCustody:
            case ExternalWorkKinds.CreateAuditReferenceCustody:
            case ExternalWorkKinds.CreateImageCaseCustody:
            case ExternalWorkKinds.MergeImageCaseCustody:
                await custody.ExecuteAsync(workItemId, cancellationToken);
                return;
            case ExternalWorkKinds.VehicleLookup:
                await vehicle.ExecuteAsync(workItemId, cancellationToken);
                return;
            case ExternalWorkKinds.IntakeOcr when intakeOcr is not null:
                await intakeOcr.ExecuteAsync(workItemId, cancellationToken);
                return;
            default:
                throw new UnknownExternalWorkKindException(workItemId, work.Kind);
        }
    }
}

public sealed class UnknownExternalWorkKindException(Guid workItemId, string? kind)
    : InvalidOperationException(
        $"External work item '{workItemId}' has an unrecognized kind and was denied.")
{
    public Guid WorkItemId { get; } = workItemId;
    public string? Kind { get; } = kind;
}

public sealed record PendingWorkDispatchResult(
    int IntakeWorkCount,
    int ExternalWorkCount)
{
    public int TotalCount => checked(IntakeWorkCount + ExternalWorkCount);
}

/// <summary>
/// The single timer-facing Core use case for both durable work outboxes.
/// </summary>
public sealed class DispatchPendingWork(
    DispatchPendingIntakeWork intake,
    DispatchPendingExternalWork external)
{
    public async Task<PendingWorkDispatchResult> ExecuteAsync(
        int maximumItemsPerQueue,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItemsPerQueue);
        var intakeCount = await intake.ExecuteAsync(maximumItemsPerQueue, cancellationToken);
        var externalCount = await external.ExecuteAsync(maximumItemsPerQueue, cancellationToken);
        return new(intakeCount, externalCount);
    }
}

public enum PoisonedQueueWorkKind
{
    Intake,
    External
}

/// <summary>
/// The common poison-queue Core boundary. Each store atomically makes its identified durable
/// row terminal (or confirms an already-terminal replay) before this call returns.
/// </summary>
public sealed class ReconcilePoisonedQueueWork(
    ReconcilePoisonedIntakeWork intake,
    ReconcilePoisonedExternalWork external)
{
    public Task ExecuteAsync(
        PoisonedQueueWorkKind kind,
        Guid durableId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (durableId == Guid.Empty)
        {
            throw new ArgumentException("A durable work identifier is required.", nameof(durableId));
        }

        return kind switch
        {
            PoisonedQueueWorkKind.Intake => intake.ExecuteAsync(durableId, cancellationToken),
            PoisonedQueueWorkKind.External => external.ExecuteAsync(durableId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
