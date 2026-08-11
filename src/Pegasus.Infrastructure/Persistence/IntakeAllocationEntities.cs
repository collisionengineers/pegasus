namespace Pegasus.Infrastructure.Persistence;

internal sealed class IntakeAllocationAttemptEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public long AttemptNumber { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string Kind { get; set; }
    public required string Status { get; set; }
    public long ExpectedReceiptVersion { get; set; }
    public string? CaseType { get; set; }
    public required string PrincipalCode { get; set; }
    public bool InstructionComplete { get; set; }
    public bool ImagesComplete { get; set; }
    public bool InstructionConfirmedByStaff { get; set; }
    public bool ImagesConfirmedByStaff { get; set; }
    public Guid? StandaloneAuditEvidenceId { get; set; }
    public DateOnly? AcceptedInspectionDeadline { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string OperationKey { get; set; }
    public required string CommandHash { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? FailureKind { get; set; }
    public string? RecoveryDisposition { get; set; }
    public string? SafeReason { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseReference { get; set; }
    public string? AuditReference { get; set; }
}
