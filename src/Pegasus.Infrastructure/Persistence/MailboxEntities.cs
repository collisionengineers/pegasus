namespace Pegasus.Infrastructure.Persistence;

internal sealed class ApprovedInboxPollStateEntity
{
    public Guid ApprovedMailboxId { get; set; }
    public ApprovedMailboxEntity ApprovedMailbox { get; set; } = null!;
    public required string MailboxAddress { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public long Generation { get; set; }
    public DateTimeOffset ActivatedAtUtc { get; set; }
    public DateTimeOffset StartBoundaryUtc { get; set; }
    public string? Cursor { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? LastCompletedAtUtc { get; set; }
    public string? LastFailureCode { get; set; }
}

internal sealed class ApprovedInboxPoisonMessageEntity
{
    public Guid Id { get; set; }
    public Guid ApprovedMailboxId { get; set; }
    public required string OccurrenceKey { get; set; }
    public required string ImmutableMessageId { get; set; }
    public required string FileName { get; set; }
    public long? SourceLength { get; set; }
    public string? SourceHash { get; set; }
    public string? OriginalSourceHash { get; set; }
    public string? EvidenceMarker { get; set; }
    public string? StorageKey { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public required string FailureCode { get; set; }
    public required string CursorAfterMessage { get; set; }
    public DateTimeOffset QuarantinedAtUtc { get; set; }
}

/// <summary>
/// One retained message, as the workspace displays it.
/// </summary>
/// <remarks>
/// Written once by the poll and never updated. Recipients are JSON rather than a
/// table because nothing queries them — they are read back whole with the message,
/// exactly as <c>ApprovedSentPollOutcomeEntity.InReplyToIdentitiesJson</c> is.
/// Attachments are a table because the list view counts them.
/// </remarks>
internal sealed class RetainedMailboxMessageEntity
{
    public Guid Id { get; set; }
    public Guid MailboxId { get; set; }
    public required string MailboxAddress { get; set; }

    /// <summary>
    /// Which of the operator's folder scopes this row belongs to. Distinct from
    /// <see cref="FolderIdentity"/>: the operator picks "Inbox", the tenant folder
    /// identity is the exact place it was read from.
    /// </summary>
    public required string FolderScope { get; set; }
    public required string FolderIdentity { get; set; }
    public required string ImmutableMessageId { get; set; }
    public string? ConversationIdentity { get; set; }
    public string? InternetMessageIdentity { get; set; }
    public string? CanonicalInternetMessageIdentity { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public string? SenderAddress { get; set; }
    public string? SenderDisplayName { get; set; }
    public required string ToAddressesJson { get; set; }
    public required string CcAddressesJson { get; set; }
    public string? ReplyToAddressesJson { get; set; }
    public string? Subject { get; set; }
    public string? BodyExcerpt { get; set; }
    public string? BodyPlainText { get; set; }
    public bool IsRead { get; set; }
    public long SourceLength { get; set; }
    public required string SourceSha256 { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset RetainedAtUtc { get; set; }
    public List<RetainedMailboxAttachmentEntity> Attachments { get; } = [];
}

internal sealed class RetainedMailboxAttachmentEntity
{
    public Guid Id { get; set; }
    public Guid RetainedMailboxMessageId { get; set; }
    public RetainedMailboxMessageEntity RetainedMailboxMessage { get; set; } = null!;
    public int Ordinal { get; set; }
    public required string FileName { get; set; }
    public required string MediaType { get; set; }
    public long ContentLength { get; set; }
}

internal sealed class RetainedMailFolderMoveEntity
{
    public Guid Id { get; set; }
    public Guid RetainedMailboxMessageId { get; set; }
    public RetainedMailboxMessageEntity RetainedMailboxMessage { get; set; } = null!;
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public int ExpectedClassificationVersion { get; set; }
    public required string ExpectedRecommendationPolicyKey { get; set; }
    public int ExpectedRecommendationPolicyVersion { get; set; }
    public int ExpectedMailboxVersion { get; set; }
    public required string MailboxId { get; set; }
    public required string ImmutableMessageId { get; set; }
    public required string SourceFolderId { get; set; }
    public required string DestinationFolderId { get; set; }
    public required string FolderType { get; set; }
    public required string Actor { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public required string Outcome { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

internal sealed class ApprovedSentPollStateEntity
{
    public required string MailboxId { get; set; }
    public required string MailboxAddress { get; set; }
    public required string SentFolderIdentity { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public long Generation { get; set; }
    public DateTimeOffset StartBoundaryUtc { get; set; }
    public string? Cursor { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? LastCompletedAtUtc { get; set; }
    public string? LastFailureCode { get; set; }
}

internal sealed class ApprovedSentPollOutcomeEntity
{
    public Guid Id { get; set; }
    public required string MailboxId { get; set; }
    public required string MailboxAddress { get; set; }
    public required string SourceOccurrenceIdentity { get; set; }
    public required string SourceSha256 { get; set; }
    public string? OriginalSourceSha256 { get; set; }
    public string? ObservedSourceSha256 { get; set; }
    public string? EvidenceMarker { get; set; }
    public string? CurrentLocationIdentity { get; set; }
    public required string ObservationKind { get; set; }
    public string? SentFolderIdentity { get; set; }
    public string? ImmutableItemIdentity { get; set; }
    public string? InternetMessageIdentity { get; set; }
    public string? ConversationIdentity { get; set; }
    public string? ReplyChainIdentity { get; set; }
    public string? InReplyToIdentitiesJson { get; set; }
    public string? AuthoritativeCaseIdentitiesJson { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string? MimeSha256 { get; set; }
    public required string OutcomeKind { get; set; }
    public Guid? RelatedEvidenceId { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public required string CursorAfterItem { get; set; }
    public required string OperationKey { get; set; }
}

internal sealed class IntakeMailClassificationDecisionEntity : IApplicationManagedConcurrencyToken
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string Outcome { get; set; }
    public string? Direction { get; set; }
    public string? Family { get; set; }
    public string? Subtype { get; set; }
    public string? CaseType { get; set; }
    public bool IsReplyContext { get; set; }
    public string? OtherName { get; set; }
    public string? OtherReasoning { get; set; }
    public required string AmbiguousCandidatesJson { get; set; }
    public required string PredicatesJson { get; set; }
    public required string Reason { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
    public string? StandaloneAuditReportAssetSourceLabel { get; set; }
    public string? StandaloneAuditReportAssessment { get; set; }
    public required string DecidedByActor { get; set; }
    public DateTimeOffset DecidedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public Guid ConcurrencyToken { get; set; }
    public List<IntakeMailClassificationHistoryEntity> History { get; } = [];
}

internal sealed class IntakeMailClassificationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeMailClassificationDecisionEntity ClassificationDecision { get; set; } = null!;
    public int Version { get; set; }
    public required string BeforeJson { get; set; }
    public required string AfterJson { get; set; }
    public required string Actor { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CorrectedAtUtc { get; set; }
}

internal sealed class IntakeMailRouteDecisionEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string Disposition { get; set; }
    public string? RouteOwnerCode { get; set; }
    public string? RouteKind { get; set; }
    public string? WorkProviderCode { get; set; }
    public required string PredicatesJson { get; set; }
    public required string Reason { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
    public required string TransportIdentitiesJson { get; set; }
    public required string OriginalIdentitiesJson { get; set; }
    public string? EffectiveSenderAddress { get; set; }
    public string? EffectiveSenderSourceLabel { get; set; }
}
