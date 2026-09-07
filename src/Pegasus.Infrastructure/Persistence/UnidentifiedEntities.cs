namespace Pegasus.Infrastructure.Persistence;

internal sealed class UnidentifiedItemEntity
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public required string Reference { get; set; }
    public required string OriginKind { get; set; }
    public Guid OriginId { get; set; }
    public required string ReasonCode { get; set; }
    public required string SafeDetail { get; set; }
    public required string State { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public required string CreatedByActorKind { get; set; }
    public required string CreatedByActorSubjectId { get; set; }
    public required string CreatedByActorRolesJson { get; set; }
    public string? ResolvedByActorKind { get; set; }
    public string? ResolvedByActorSubjectId { get; set; }
    public string? ResolvedByActorRolesJson { get; set; }
    public string? ResolutionReason { get; set; }
    public string? ResolutionTargetKind { get; set; }
    public string? ResolutionTargetId { get; set; }
    public string? ResolutionTargetReference { get; set; }
    public long? ReconciledAssociationVersion { get; set; }
    public required string RegistrationOperationKey { get; set; }
    public required string RegistrationFingerprint { get; set; }
    public long Version { get; set; }
}

internal sealed class UnidentifiedSequenceEntity
{
    public int Id { get; set; }
    public long LastAllocatedSequence { get; set; }
}

internal sealed class UnidentifiedHistoryEntity
{
    public Guid Id { get; set; }
    public Guid UnidentifiedItemId { get; set; }
    public required string PreviousState { get; set; }
    public required string NewState { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string Reason { get; set; }
    public required string OperationKey { get; set; }
    public string? TargetKind { get; set; }
    public string? TargetId { get; set; }
    public string? TargetReference { get; set; }
}
