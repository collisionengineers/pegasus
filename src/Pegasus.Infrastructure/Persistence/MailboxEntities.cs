namespace Pegasus.Infrastructure.Persistence;

internal sealed class ApprovedInboxPollStateEntity
{
    public required string MailboxId { get; set; }
    public required string MailboxAddress { get; set; }
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
    public required string MailboxId { get; set; }
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

internal sealed class ApprovedSentPollStateEntity
{
    public required string MailboxId { get; set; }
    public required string MailboxAddress { get; set; }
    public required string SentFolderIdentity { get; set; }
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

internal sealed class IntakeMailClassificationDecisionEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string Outcome { get; set; }
    public string? Direction { get; set; }
    public string? Family { get; set; }
    public string? Subtype { get; set; }
    public bool IsReplyContext { get; set; }
    public string? OtherName { get; set; }
    public string? OtherReasoning { get; set; }
    public required string AmbiguousCandidatesJson { get; set; }
    public required string PredicatesJson { get; set; }
    public required string Reason { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
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
