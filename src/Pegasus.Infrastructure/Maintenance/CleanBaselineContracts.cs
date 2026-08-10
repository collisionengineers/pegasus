using System.Text.Json.Serialization;

namespace Pegasus.Infrastructure.Maintenance;

public enum CleanBaselineOperation
{
    ValidateAccess,
    Plan,
    Execute,
    Verify
}

public sealed record ProductionIntakeCleanBaselineInvocation
{
    public required CleanBaselineOperation Operation { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid SubscriptionId { get; init; }
    public required string ResourceGroup { get; init; }
    public required string SqlServer { get; init; }
    public required string SqlDatabase { get; init; }
    public required string StorageAccount { get; init; }
    public required string BlobContainer { get; init; }
    public required string MailboxIdentity { get; init; }
    public required string InboxFolderIdentity { get; init; }
    public required string NonTargetMailboxIdentity { get; init; }
    public required string OperatorUpn { get; init; }
    public required Guid PublicClientId { get; init; }
    public required string AccessEvidencePath { get; init; }
    public required string AccessEvidenceSha256 { get; init; }
    public string? ManifestPath { get; init; }
    public string? ManifestSha256 { get; init; }
    public string? ExecutionReceiptPath { get; init; }
    public DateTimeOffset? PreTestCutoffUtc { get; init; }
}

internal sealed record CleanBaselineAccessEvidence(
    int SchemaVersion,
    Guid TenantId,
    Guid SubscriptionId,
    Guid OperatorObjectId,
    string OperatorUpn,
    Guid PublicClientId,
    string ResourceGroup,
    string SqlServer,
    string SqlDatabase,
    string StorageAccount,
    DateTimeOffset CapturedAtUtc,
    CleanBaselinePublicClientEvidence PublicClient,
    CleanBaselineMailboxPermissionEvidence Mailbox,
    IReadOnlyList<CleanBaselineDirectoryRole> DirectoryRoles,
    IReadOnlyList<string> SqlRoles,
    IReadOnlyList<CleanBaselineRoleAssignment> RoleAssignments,
    IReadOnlyList<CleanBaselineRoleDefinition> RoleDefinitions);

internal sealed record CleanBaselinePublicClientEvidence(
    bool IsPublicClient,
    int PasswordCredentialCount,
    int KeyCredentialCount,
    IReadOnlyList<CleanBaselineDelegatedPermission> DelegatedPermissions);

internal sealed record CleanBaselineDelegatedPermission(
    Guid ResourceApplicationId,
    string Permission);

internal sealed record CleanBaselineMailboxPermissionEvidence(
    string MailboxIdentity,
    string InboxFolderIdentity,
    string NonTargetMailboxIdentity,
    string AccessRights,
    bool CanSendAs,
    bool CanSendOnBehalf,
    bool CanDeleteItems);

internal sealed record CleanBaselineDirectoryRole(
    Guid RoleTemplateId,
    string RoleName);

internal sealed record CleanBaselineRoleDefinition(
    Guid RoleDefinitionId,
    string RoleName,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> DataActions);

internal sealed record CleanBaselineAccessReport(
    int SchemaVersion,
    Guid TenantId,
    string OperatorUpn,
    Guid PublicClientId,
    Guid OperatorObjectId,
    string SubscriptionId,
    string ResourceGroup,
    string SqlServer,
    string SqlDatabase,
    string StorageAccount,
    string BlobContainer,
    string MailboxIdentity,
    string InboxFolderIdentity,
    string NonTargetMailboxIdentity,
    string AccessEvidenceSha256,
    IReadOnlyList<CleanBaselineRoleAssignment> RoleAssignments,
    IReadOnlyList<string> SqlRoles,
    IReadOnlyList<CleanBaselineCapabilityResult> Capabilities,
    string Result);

internal sealed record CleanBaselineRoleAssignment(
    Guid PrincipalId,
    string RoleName,
    string RoleDefinitionId,
    string Scope,
    bool Inherited,
    string PrincipalKind);

internal sealed record CleanBaselineCapabilityResult(
    string Capability,
    bool Passed,
    string ResultCode);

internal sealed record CleanBaselineManifest(
    int SchemaVersion,
    string OperationId,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset PreTestCutoffUtc,
    CleanBaselineScope Scope,
    string AccessCensusSha256,
    IReadOnlyList<CleanBaselineSqlRow> SqlRows,
    IReadOnlyList<CleanBaselineBlobItem> Blobs,
    IReadOnlyList<CleanBaselineQueueItem> QueueMessages,
    IReadOnlyList<Guid> TargetStagedReceiptIds,
    CleanBaselineRetainedFingerprint Retained,
    string PollCursorBeforeSha256,
    IReadOnlyList<CleanBaselineStopCondition> StopConditions,
    string SnapshotSha256);

internal sealed record CleanBaselineScope(
    Guid TenantId,
    Guid SubscriptionId,
    string ResourceGroup,
    string SqlServer,
    string SqlDatabase,
    string StorageAccount,
    string BlobContainer,
    string MailboxIdentity,
    string InboxFolderIdentity,
    string NonTargetMailboxIdentity,
    string OperatorUpn,
    Guid PublicClientId);

internal sealed record CleanBaselineSqlRow(
    string Schema,
    string Table,
    IReadOnlyList<CleanBaselineKeyValue> Key,
    string RowSha256,
    int DependencyDepth,
    string Classification);

internal sealed record CleanBaselineKeyValue(
    string Column,
    string Type,
    string Value);

internal sealed record CleanBaselineBlobItem(
    string Name,
    string ETag,
    long Length,
    string? ContentSha256,
    int TotalReferenceCount,
    int TargetReferenceCount);

internal sealed record CleanBaselineQueueItem(
    string Queue,
    string MessageId,
    string BodySha256,
    Guid StagedReceiptId,
    DateTimeOffset? InsertionTime,
    DateTimeOffset? ExpirationTime);

internal sealed record CleanBaselineRetainedFingerprint(
    int CaseCount,
    string CaseIdentitiesSha256,
    int TriageCount,
    string TriageIdentitiesSha256,
    int PrincipalCount,
    string PrincipalIdentitiesSha256);

internal sealed record CleanBaselineStopCondition(
    string Code,
    string ResourceType,
    string ResourceIdentityHash,
    string Detail);

internal sealed record CleanBaselineExecutionReceipt(
    int SchemaVersion,
    string ManifestSha256,
    DateTimeOffset CompletedAtUtc,
    string BaselineCursorSha256,
    int DeletedSqlRows,
    int DeletedQueueMessages,
    int DeletedBlobs,
    string Result);

internal sealed record CleanBaselineVerificationReport(
    int SchemaVersion,
    string ManifestSha256,
    DateTimeOffset VerifiedAtUtc,
    int RemainingSqlRows,
    int RemainingQueueMessages,
    int RemainingBlobs,
    bool RetainedFingerprintUnchanged,
    bool BaselineCursorMatches,
    string Result);

internal sealed record CleanBaselineSnapshot(
    IReadOnlyList<CleanBaselineSqlRow> SqlRows,
    IReadOnlyList<CleanBaselineBlobItem> Blobs,
    IReadOnlyList<CleanBaselineQueueItem> QueueMessages,
    IReadOnlyList<Guid> TargetStagedReceiptIds,
    CleanBaselineRetainedFingerprint Retained,
    string PollCursorSha256,
    IReadOnlyList<CleanBaselineStopCondition> StopConditions);

internal sealed record CleanBaselineGraphBaseline(string Cursor, string CursorSha256);

internal interface ICleanBaselineAccessValidator
{
    Task<CleanBaselineAccessReport> ValidateAsync(CancellationToken cancellationToken);
}

internal interface ICleanBaselineSqlSession
{
    Task<CleanBaselineSqlInventory> InventoryAsync(
        DateTimeOffset cutoffUtc,
        string mailboxIdentity,
        CancellationToken cancellationToken);

    Task<int> DeleteExactRowsAsync(
        IReadOnlyList<CleanBaselineSqlRow> rows,
        CancellationToken cancellationToken);

    Task<int> CountExistingRowsAsync(
        IReadOnlyList<CleanBaselineSqlRow> rows,
        CancellationToken cancellationToken);

    Task<string> ReadPollCursorHashAsync(
        string mailboxIdentity,
        CancellationToken cancellationToken);

    Task WritePollCursorAsync(
        string mailboxIdentity,
        string expectedCursorHash,
        string nextCursor,
        CancellationToken cancellationToken);

    Task<CleanBaselineRetainedFingerprint> ReadRetainedFingerprintAsync(
        CancellationToken cancellationToken);
}

internal interface ICleanBaselineSqlStore : ICleanBaselineSqlSession
{
    Task<ICleanBaselineSqlExecution> BeginLockedExecutionAsync(
        CancellationToken cancellationToken);
}

internal interface ICleanBaselineSqlExecution : ICleanBaselineSqlSession, IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

internal sealed record CleanBaselineSqlInventory(
    IReadOnlyList<CleanBaselineSqlRow> Rows,
    IReadOnlyDictionary<string, (int Total, int Target)> BlobReferences,
    IReadOnlySet<Guid> TargetStagedReceiptIds,
    IReadOnlyList<CleanBaselineStopCondition> StopConditions);

internal interface ICleanBaselineBlobStore
{
    Task<IReadOnlyList<CleanBaselineBlobItem>> InspectExactAsync(
        IReadOnlyDictionary<string, (int Total, int Target)> references,
        CancellationToken cancellationToken);

    Task<int> DeleteExactAsync(
        IReadOnlyList<CleanBaselineBlobItem> blobs,
        CancellationToken cancellationToken);

    Task<ICleanBaselinePreparedDeletion> PrepareDeleteAsync(
        IReadOnlyList<CleanBaselineBlobItem> blobs,
        CancellationToken cancellationToken);

    Task<int> CountExistingAsync(
        IReadOnlyList<CleanBaselineBlobItem> blobs,
        CancellationToken cancellationToken);
}

internal interface ICleanBaselineQueueStore
{
    Task<CleanBaselineQueueInventory> InspectAsync(
        IReadOnlySet<Guid> targetStagedReceiptIds,
        CancellationToken cancellationToken);

    Task<int> DeleteExactAsync(
        IReadOnlyList<CleanBaselineQueueItem> messages,
        CancellationToken cancellationToken);

    Task<ICleanBaselinePreparedDeletion> PrepareDeleteAsync(
        IReadOnlyList<CleanBaselineQueueItem> messages,
        CancellationToken cancellationToken);

    Task<int> CountTargetMessagesAsync(
        IReadOnlySet<Guid> targetStagedReceiptIds,
        CancellationToken cancellationToken);
}

internal interface ICleanBaselinePreparedDeletion : IAsyncDisposable
{
    Task<int> DeleteAsync(CancellationToken cancellationToken);
}

internal sealed record CleanBaselineQueueInventory(
    IReadOnlyList<CleanBaselineQueueItem> Messages,
    IReadOnlyList<CleanBaselineStopCondition> StopConditions);

internal interface ICleanBaselineGraphClient
{
    Task<CleanBaselineGraphBaseline> AcquireBaselineAsync(CancellationToken cancellationToken);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ProductionIntakeCleanBaselineInvocation))]
[JsonSerializable(typeof(CleanBaselineAccessEvidence))]
[JsonSerializable(typeof(CleanBaselineAccessReport))]
[JsonSerializable(typeof(CleanBaselineManifest))]
[JsonSerializable(typeof(CleanBaselineExecutionReceipt))]
[JsonSerializable(typeof(CleanBaselineVerificationReport))]
internal partial class CleanBaselineJsonContext : JsonSerializerContext;
