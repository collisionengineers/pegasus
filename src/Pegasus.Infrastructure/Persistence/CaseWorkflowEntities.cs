namespace Pegasus.Infrastructure.Persistence;

internal sealed class CaseWorkflowEntity : IApplicationManagedConcurrencyToken
{
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string State { get; set; }
    public string? PreHoldState { get; set; }
    public Guid? AssignedEngineerId { get; set; }
    public Guid? SignOffEngineerId { get; set; }
    public Guid? ReportApprovalId { get; set; }
    public CaseReportApprovalEntity? ReportApproval { get; set; }
    public Guid? ReportSentEvidenceId { get; set; }
    public CaseReportSentEvidenceEntity? ReportSentEvidence { get; set; }
    public CaseDueWorkEntity? DueWork { get; set; }
    public string? ClosureOutcome { get; set; }
    public Guid? ReplacementCaseId { get; set; }
    public CaseEntity? ReplacementCase { get; set; }
    public Guid? OriginalCaseId { get; set; }
    public CaseEntity? OriginalCase { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public string? ArchivedByKind { get; set; }
    public string? ArchivedBySubjectId { get; set; }
    public string? ArchivedByRolesJson { get; set; }
    public string? ArchiveReason { get; set; }
    public long Version { get; set; }
    public long EditLeaseGeneration { get; set; }
    // Server-only, short-lived replay recovery material; never project, log, or copy to history.
    public string? EditLeaseToken { get; set; }
    public string? EditLeaseTokenHash { get; set; }
    public string? EditLeaseRequestHash { get; set; }
    public string? EditLeaseHolder { get; set; }
    public string? EditLeaseHolderKind { get; set; }
    public string? EditLeaseOperationKey { get; set; }
    public DateTimeOffset? EditLeaseExpiresAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class CaseWorkflowEventEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseWorkflowEntity Workflow { get; set; } = null!;
    public required string EventType { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public long BeforeVersion { get; set; }
    public long AfterVersion { get; set; }
    public string? ResultJson { get; set; }
}

internal sealed class CaseEditLeaseOperationEntity
{
    public Guid CaseId { get; set; }
    public CaseWorkflowEntity Workflow { get; set; } = null!;
    public required string OperationKey { get; set; }
    public required string OperationKind { get; set; }
    public required string RequestHash { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public long ResultVersion { get; set; }
    public string? ResultTokenHash { get; set; }
    public DateTimeOffset? ResultExpiresAtUtc { get; set; }
}

internal sealed class CaseReportApprovalEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public required string ArtifactIdentity { get; set; }
    public required string ArtifactSha256 { get; set; }
    public required string ApprovedByKind { get; set; }
    public required string ApprovedBySubjectId { get; set; }
    public required string ApprovedByRolesJson { get; set; }
    public DateTimeOffset ApprovedAtUtc { get; set; }
}

internal sealed class CaseReportSentEvidenceEntity
{
    public Guid Id { get; set; }
    public Guid? CaseId { get; set; }
    public required string MailboxIdentity { get; set; }
    public required string SentFolderIdentity { get; set; }
    public required string ImmutableItemIdentity { get; set; }
    public required string InternetMessageIdentity { get; set; }
    public required string ConversationIdentity { get; set; }
    public required string ReplyChainIdentity { get; set; }
    public required string SourceOccurrenceIdentity { get; set; }
    public required string SourceSha256 { get; set; }
    public required string MimeSha256 { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
    public DateTimeOffset DiscoveredAtUtc { get; set; }
    public required string DiscoveredByKind { get; set; }
    public required string DiscoveredBySubjectId { get; set; }
    public required string RetentionOperationKey { get; set; }
    public required string RetentionRequestHash { get; set; }
    public DateTimeOffset? LinkedAtUtc { get; set; }
    public string? LinkedByKind { get; set; }
    public string? LinkedBySubjectId { get; set; }
    public string? LinkedByRolesJson { get; set; }
}

internal sealed class CaseDueWorkEntity : IApplicationManagedConcurrencyToken
{
    private DateTimeOffset? nextChaseAtUtc;

    public Guid CaseId { get; set; }
    public CaseWorkflowEntity Workflow { get; set; } = null!;
    public required string MissingMaterialReason { get; set; }
    public DateOnly? DueBy { get; set; }
    public required string State { get; set; }
    public DateTimeOffset? NextChaseAtUtc
    {
        get => nextChaseAtUtc;
        set
        {
            nextChaseAtUtc = value?.ToUniversalTime();
            NextChaseAtUtcTicks = nextChaseAtUtc?.UtcDateTime.Ticks;
        }
    }
    public long? NextChaseAtUtcTicks { get; private set; }
    public DateTimeOffset? HeldAtUtc { get; set; }
    public long? RemainingChaseIntervalTicks { get; set; }
    public string? MostRecentChannel { get; set; }
    public string? MostRecentOutcome { get; set; }
    public string? MostRecentNote { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class CaseManualChaseEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseDueWorkEntity DueWork { get; set; } = null!;
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public required string Channel { get; set; }
    public required string TargetPartyOrAddress { get; set; }
    public DateTimeOffset AttemptedAtUtc { get; set; }
    public required string Outcome { get; set; }
    public string? Note { get; set; }
    public long ResultingVersion { get; set; }
}

internal sealed class CaseTaskEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseWorkflowEntity Workflow { get; set; } = null!;
    public required string Description { get; set; }
    public Guid? AssigneeId { get; set; }
    public required string State { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
