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
}

public sealed record QueuedExternalWork(Guid Id, string Kind);

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
    IProcessQueuedVehicleLookup vehicle) : IProcessQueuedExternalWork
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
