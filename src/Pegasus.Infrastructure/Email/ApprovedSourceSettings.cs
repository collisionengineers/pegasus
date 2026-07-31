namespace Pegasus.Infrastructure.Email;

public interface IApprovedInboxSourceSettings
{
    string MailboxId { get; }
    string MailboxAddress { get; }
}

public interface IApprovedSentSourceSettings : IApprovedInboxSourceSettings
{
    string SentFolderIdentity { get; }
}
