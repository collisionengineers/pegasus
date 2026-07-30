using Microsoft.AspNetCore.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class StaffAccount : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset? DisabledAtUtc { get; set; }
    public bool ForcePasswordChange { get; set; }
    public long Version { get; set; }
}

public sealed class StaffRoleEntity : IdentityRole<Guid>
{
    public StaffRoleEntity() { }
    public StaffRoleEntity(string name) : base(name) { }
}

internal sealed class TriageEntity
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public string Registration { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string State { get; set; } = "Open";
    public DateTimeOffset LastChangedAtUtc { get; set; }
    public long Version { get; set; }
    public TriageFindingEntity? CurrentFinding { get; set; }
    public List<TriageFindingEntity> Findings { get; set; } = [];
    public TriageEvidenceEntity? ReplyEvidence { get; set; }
    public List<TriageCaseLinkEntity> CaseLinks { get; set; } = [];
}

internal sealed class TriageFindingEntity
{
    public Guid Id { get; set; }
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public string? Roadworthiness { get; set; }
    public string? Assessment { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid ActorId { get; set; }
}

internal sealed class TriageEvidenceEntity
{
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public string ExternalMessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string ApprovedMailbox { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }
    public string ReplyHash { get; set; } = string.Empty;
}

internal sealed class TriageCaseLinkEntity
{
    public Guid Id { get; set; }
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public Guid CaseId { get; set; }
    public DateTimeOffset LinkedAtUtc { get; set; }
    public DateTimeOffset? UnlinkedAtUtc { get; set; }
    public string? Reason { get; set; }
}

internal sealed class CaseEntity
{
    public Guid Id { get; set; }
    public string PrincipalCode { get; set; } = string.Empty;
    public string BaseReference { get; set; } = string.Empty;
    public string DisplayReference { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string? SecondaryAuditReference { get; set; }
    public string? Claimant { get; set; }
    public string? ClaimNumber { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateOnly? InstructionDate { get; set; }
    public string? Origin { get; set; }
    public string State { get; set; } = "NotReady";
    public bool IsHeld { get; set; }
    public DateTimeOffset? NextDueAtUtc { get; set; }
    public bool DuePaused { get; set; }
    public int ChaseCount { get; set; }
    public Guid? EngineerId { get; set; }
    public string? EngineerName { get; set; }
    public string? TerminalOutcome { get; set; }
    public Guid? ReplacementCaseId { get; set; }
    public long Version { get; set; }
}

internal sealed class CaseSequenceEntity
{
    public Guid Id { get; set; }
    public string PrincipalCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int LastSequence { get; set; }
}

internal sealed class CaseLeaseEntity
{
    public Guid CaseId { get; set; }
    public Guid HolderId { get; set; }
    public string HolderName { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAtUtc { get; set; }
    public DateTimeOffset RenewedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public long Version { get; set; }
}

internal sealed class BusinessActionEntity
{
    public Guid Id { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? TriageId { get; set; }
    public string ActorKind { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string Caller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public Guid CorrelationId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
