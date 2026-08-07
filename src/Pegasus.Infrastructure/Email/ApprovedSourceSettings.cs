namespace Pegasus.Infrastructure.Email;

/// <summary>
/// Deployment configuration for the single bootstrap mailbox. Since the approved
/// estate became the authority for which mailboxes are polled, this is the read-only
/// fallback that keeps the already-deployed mailbox polling before an administrator
/// saves its identities, and it remains the Sent route's own configuration.
/// </summary>
public interface IApprovedInboxSourceSettings
{
    string MailboxId { get; }
    string MailboxAddress { get; }
    string InboxFolderIdentity { get; }
}

/// <summary>
/// Sent-evidence polling remains configuration-driven for one mailbox and does not
/// inherit the Inbox settings: the two routes no longer answer the same question, so a
/// Sent-only local adapter is not obliged to name an Inbox folder.
/// </summary>
public interface IApprovedSentSourceSettings
{
    string MailboxId { get; }
    string MailboxAddress { get; }
    string SentFolderIdentity { get; }
}
