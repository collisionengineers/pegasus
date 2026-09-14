namespace Pegasus.Core.Intake;

/// <summary>
/// How an email a member of staff uploaded is retained as correspondence. It
/// has no polled mailbox, so its row carries none, and it is filed under
/// <see cref="MailFolderScope.Upload"/>, which the mailbox workspace leaves
/// out. The Case's Correspondence tab is where an uploaded email is read.
/// </summary>
public static class UploadedCorrespondence
{
    /// <summary>
    /// The mailbox identity a summary shows for an uploaded email: the read
    /// model names a mailbox on every message, and this one is held by no
    /// approved mailbox.
    /// </summary>
    public static readonly Guid MailboxId = new("0f5c7a2e-6b1d-4c3a-9e8f-2d4b6a8c0e1f");

    public const string MailboxAddress = "upload";

    public const string FolderIdentity = "upload";

    /// <summary>
    /// The retained row for an uploaded email. Its identity is the source bytes:
    /// the same file uploaded twice is one row, and a re-saved copy of the same
    /// message is another, so the Message-ID is deliberately not retained as an
    /// identity to contradict.
    /// </summary>
    public static RetainedMailboxMessage Retained(
        IntakeSource source,
        string sourceSha256,
        IntakeEmailSummary email,
        DateTimeOffset retainedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);
        ArgumentNullException.ThrowIfNull(email);
        return new(
            null,
            MailboxAddress,
            sourceSha256,
            source.SourceIdentity.ExternalReceiptToken,
            email.SentAtUtc ?? source.ReceivedAtUtc,
            source.Content.Length,
            sourceSha256,
            new RetainedMailboxMessageMetadata(
                FolderIdentity,
                email.ThreadIdentity,
                InternetMessageIdentity: null,
                email.SenderAddress,
                email.SenderDisplayName,
                email.ToAddresses,
                email.CcAddresses,
                email.ReplyToAddresses,
                email.Subject,
                email.BodyPlainText,
                email.Attachments,
                IsRead: false),
            retainedAtUtc,
            MailFolderScope.Upload);
    }
}
