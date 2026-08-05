namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One current assessment field value with recording and confirmation
/// provenance. The current row never erases evidence: every change is
/// recorded with its before and after values in the permanent action
/// history written by the same transaction.
/// </summary>
internal sealed class CaseAssessmentFieldEntity
{
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string FieldPath { get; set; }
    public required string Value { get; set; }
    public required string RecordedByKind { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}

internal sealed class CaseEstimateLineEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public int Position { get; set; }
    public required string LineType { get; set; }
    public string? GuideCode { get; set; }
    public string? Description { get; set; }
    public decimal? WorkUnits { get; set; }
    public decimal? Price { get; set; }
    public bool Unpriced { get; set; }
    public string? PartNumber { get; set; }
    public string? Betterment { get; set; }
    public string? Status { get; set; }
    public string? EvidenceLabel { get; set; }
    public string? Justification { get; set; }
    public required string RecordedByKind { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}

/// <summary>
/// The Send to AI work request (AI-09): the Core-owned tracking record for
/// one pointer hand-off. Idempotent per (case, operation key); state
/// transitions are optimistic on Version.
/// </summary>
internal sealed class AiWorkRequestEntity
{
    public Guid RequestId { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string CaseReference { get; set; }
    public long CaseVersionAtSend { get; set; }
    public required string CapabilityScope { get; set; }
    public required string Instruction { get; set; }
    public required string State { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? HandedOffAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureReason { get; set; }
    public string? ReplyStatus { get; set; }
    public string? ReplyMessage { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// Single-row Administrator switch for the Send to AI outbound hand-off,
/// mirroring the Automation client kill-switch pattern.
/// </summary>
internal sealed class SendToAiControlEntity
{
    public const string SingletonId = "send-to-ai";

    public required string Id { get; set; }
    public bool Enabled { get; set; }
    public int Version { get; set; }
}
