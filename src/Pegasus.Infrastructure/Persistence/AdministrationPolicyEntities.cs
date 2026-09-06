namespace Pegasus.Infrastructure.Persistence;

internal sealed class WorkflowConfigurationEntity
{
    public required string Id { get; set; }
    public int Version { get; set; }
}

internal sealed class ApprovedMailboxEntity
{
    public Guid Id { get; set; }
    public required string Address { get; set; }
    public bool AllowInboundIntake { get; set; }
    public bool AllowSentEvidence { get; set; }
    public bool AllowStaffSend { get; set; }
    public long MailboxGeneration { get; set; }
    public long? VerifiedEncodedMessageSizeLimit { get; set; }
    public DateTimeOffset? SendLimitVerifiedAtUtc { get; set; }
    public string? SendLimitVerifiedBy { get; set; }
    public required string State { get; set; }

    /// <summary>
    /// The exact tenant identities the poll needs. Null means an administrator has not
    /// supplied them yet, which is only allowed while the row is Disabled. Once
    /// written they are immutable, because the mailbox identity is the primary key of
    /// the per-mailbox cursor row.
    /// </summary>
    public string? MailboxIdentity { get; set; }
    public string? InboxFolderIdentity { get; set; }
    public string? SentFolderIdentity { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public int Version { get; set; }
    public ICollection<ApprovedMailboxFolderBindingEntity> FolderBindings { get; } = [];
}

internal sealed class ApprovedMailboxFolderBindingEntity
{
    public Guid ApprovedMailboxId { get; set; }
    public required string FolderType { get; set; }
    public required string FolderIdentity { get; set; }
    public ApprovedMailboxEntity ApprovedMailbox { get; set; } = null!;
}

internal sealed class ApprovedMailboxSubscriptionEntity
{
    public Guid ApprovedMailboxId { get; set; }
    public ApprovedMailboxEntity ApprovedMailbox { get; set; } = null!;
    public required string SubscriptionId { get; set; }
    public required string Resource { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public required string LifecycleState { get; set; }
    public long Generation { get; set; }
    public DateTimeOffset? LastMaintainedAtUtc { get; set; }
    public string? LastMaintenanceFailureCode { get; set; }
}

internal sealed class ApprovedOutlookCategoryEntity
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required string NormalizedDisplayName { get; set; }
    public required string State { get; set; }
    public int Version { get; set; }
}
