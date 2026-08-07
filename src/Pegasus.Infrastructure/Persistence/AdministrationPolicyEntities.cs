namespace Pegasus.Infrastructure.Persistence;

internal sealed class WorkflowConfigurationEntity
{
    public required string Id { get; set; }
    public bool RequireCompleteInstructionsBeforeEngineerAssignment { get; set; }
    public bool RequireCompleteImagesBeforeEngineerAssignment { get; set; }
    public bool RequireStaffInstructionReviewBeforeEngineerAssignment { get; set; }
    public bool RequireStaffImageReviewBeforeEngineerAssignment { get; set; }
    public int Version { get; set; }
}

internal sealed class ApprovedMailboxEntity
{
    public Guid Id { get; set; }
    public required string Address { get; set; }
    public bool AllowInboundIntake { get; set; }
    public bool AllowSentEvidence { get; set; }
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
    public int Version { get; set; }
}
