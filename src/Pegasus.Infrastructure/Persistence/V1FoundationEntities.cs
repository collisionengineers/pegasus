namespace Pegasus.Infrastructure.Persistence;

internal sealed class UserExternalCredentialEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public required string Provider { get; set; }
    public Guid UserId { get; set; }
    public required string NormalizedAccountKey { get; set; }
    public bool Enabled { get; set; }
    public long CredentialGeneration { get; set; }
    public required string ProtectedCredential { get; set; }
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class StaffMailSendOperationEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public required string ActorSubjectId { get; set; }
    public Guid MailboxId { get; set; }
    public long MailboxGeneration { get; set; }
    public required string OperationKey { get; set; }
    public required string PayloadHash { get; set; }
    public Pegasus.Core.Operations.StaffMailPurpose Purpose { get; set; }
    public Guid ContextId { get; set; }
    public long ContextVersion { get; set; }
    public Pegasus.Core.Operations.StaffMailComposeMode ComposeMode { get; set; }
    public Guid? OriginalRetainedMessageId { get; set; }
    public string? OriginalImmutableMessageId { get; set; }
    public string? OriginalInternetMessageId { get; set; }
    public string? OriginalConversationId { get; set; }
    public required string RecipientsJson { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public required string AttachmentsJson { get; set; }
    public Pegasus.Core.Operations.StaffMailState State { get; set; }
    public Pegasus.Core.Operations.StaffMailAttemptStage? AttemptStage { get; set; }
    public string? DraftImmutableId { get; set; }
    public string? ProtectedUploadSession { get; set; }
    public DateTimeOffset? UploadSessionExpiresAtUtc { get; set; }
    public required string CorrelationMarker { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? ObservedSentAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? ReconciliationContinuation { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class TriageSequenceEntity
{
    public int Id { get; set; }
    public long LastAllocatedSequence { get; set; }
}

internal sealed class ValuationPresetEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public required string Label { get; set; }
    public decimal SuggestedAmount { get; set; }
    public bool Active { get; set; }
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class LabourRateCardEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public required string Label { get; set; }
    public decimal PanelRate { get; set; }
    public bool Active { get; set; }
    public long Version { get; set; }
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class AppliedValuationSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public required string SnapshotJson { get; set; }
    public required string CalculationPolicyVersion { get; set; }
    public required string GeneratedByKind { get; set; }
    public required string GeneratedBySubjectId { get; set; }
    public required string SnapshotHash { get; set; }
    public decimal AcceptedEngineerValue { get; set; }
    public required string AcceptedBy { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public required string Reason { get; set; }
    public required string PolicyVersion { get; set; }
}

internal sealed class GlassRepairEstimateSessionEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid UserId { get; set; }
    public long CredentialGeneration { get; set; }
    public required string NormalizedAccountKey { get; set; }
    public string? ActiveAccountKey { get; set; }
    public required string OperationKey { get; set; }
    public Pegasus.Core.Assessment.GlassRepairEstimateSessionState State { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? ProviderVehicleId { get; set; }
    public string? EreId { get; set; }
    public required string CallbackDigest { get; set; }
    public DateTimeOffset? CallbackConsumedAtUtc { get; set; }
    public required string ProtectedSession { get; set; }
    public string? ResultArtifactsJson { get; set; }
    public string? LastError { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class CaseReportGenerationEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public long CaseVersion { get; set; }
    public required string SnapshotHash { get; set; }
    public required string SnapshotJson { get; set; }
    public required string TemplateVersion { get; set; }
    public required string RendererVersion { get; set; }
    public required string State { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public long Version { get; set; }
    public Guid? SupersededById { get; set; }
}

internal sealed class GeneratedCaseArtifactEntity
{
    public Guid Id { get; set; }
    public Guid GenerationId { get; set; }
    public Guid? VersionId { get; set; }
    public required string Kind { get; set; }
    public string? Sha256 { get; set; }
    public required string State { get; set; }
    public required string OperationKey { get; set; }
    public string? FailureCode { get; set; }
}

internal sealed class CaseReportDeliveryIntentEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid GenerationId { get; set; }
    public long GenerationVersion { get; set; }
    public required string PayloadJson { get; set; }
    public required string PayloadHash { get; set; }
    public required string ActorSubjectId { get; set; }
    public DateTimeOffset PreparedAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class RetainedInstructionAnalysisEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public Guid IntakeAssetId { get; set; }
    public required string SourceSha256 { get; set; }
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public long ExpectedReceiptVersion { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}

internal sealed class IntakeSourceCandidateEntity
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public Guid? IntakeAssetId { get; set; }
    public required string SourceSha256 { get; set; }
    public int Occurrence { get; set; }
    public required string DocumentRole { get; set; }
    public required string Field { get; set; }
    public string? PartyRole { get; set; }
    public string? ReferenceRole { get; set; }
    public string? RawValue { get; set; }
    public string? NormalizedValue { get; set; }
    public string? Unit { get; set; }
    public string? Currency { get; set; }
    public required string LocatorJson { get; set; }
    public required string ReaderKey { get; set; }
    public required string ReaderVersion { get; set; }
    public required string PolicyKey { get; set; }
    public required string PolicyVersion { get; set; }
    public required string Disposition { get; set; }
}

internal sealed class IntakeOcrOperationEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public Guid? IntakeAssetId { get; set; }
    public required string SourceSha256 { get; set; }
    public required string QualifiedPagesJson { get; set; }
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public string? ProviderOperationId { get; set; }
    public string? ResponseSha256 { get; set; }
    public string? ResultJson { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? RetryAtUtc { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class DocumentContentCacheEntryEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public Guid? IntakeAssetId { get; set; }
    public required string BlobIdentity { get; set; }
    public string? ETag { get; set; }
    public required string VerifiedSha256 { get; set; }
    public long VerifiedSize { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ReadLeaseExpiresAtUtc { get; set; }
    public string? LastCleanupOutcome { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class ClaimSourceEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Contact { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class OrganizationDirectoryEntryEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public required string Role { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Contact { get; set; }
    public required string Address { get; set; }
    public string? Postcode { get; set; }
    public string? NormalizedPostcode { get; set; }
    public required string SourceKind { get; set; }
    public Guid? SourceRecordId { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public long SourceVersion { get; set; }
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool Active { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class PublicUploadSessionEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid RequestUploadLinkId { get; set; }
    public required string LimitsVersion { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinalizedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class PublicUploadOccurrenceEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? ReplacesOccurrenceId { get; set; }
    public required string OperationKey { get; set; }
    public required string ProposedName { get; set; }
    public required string MediaType { get; set; }
    public long Size { get; set; }
    public required string Sha256 { get; set; }
    public required string CustodyState { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? DocumentVersionId { get; set; }
}
