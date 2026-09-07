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
    public const string SubmitCaseToEva = "submit_case_to_eva";
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

public interface IQueuedExternalWorkReader
{
    Task<QueuedExternalWork?> GetAsync(Guid workItemId, CancellationToken cancellationToken);
}

public interface IProcessQueuedExternalWork
{
    Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken);
}

/// <summary>
/// EXT-04: the durable arm of automatic EVA submission. A case whose principal
/// has automatic submission enabled reaches EVA through this, never through
/// the page, so an outage retries and recovers instead of failing in front of
/// an operator.
/// </summary>
public interface IProcessQueuedEvaSubmission
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
    IProcessQueuedEvaSubmission? eva = null,
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
            // EXT-04 composes only where EVA credentials exist, so a host
            // without them has no handler. A row of this kind reaching such a
            // host is the same fail-closed case as an unrecognized kind: it is
            // refused rather than quietly completed, because completing it
            // would record a case as dealt with that nothing ever sent.
            case ExternalWorkKinds.SubmitCaseToEva when eva is not null:
                await eva.ExecuteAsync(workItemId, cancellationToken);
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
