using MimeKit;
using Pegasus.Infrastructure.Brego;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// Brego's sign-in mail read from the Brego account's own Inbox over the
/// Web's existing Graph reads (26 November 2026, R1294): the address is
/// resolved as the mailbox administration resolves one, the newest
/// <see cref="NewestMessages"/> Inbox messages are listed newest first, and
/// each received at or after the time asked for is read as MIME and matched
/// (<see cref="BregoSignInMail"/>). The mailbox is not an approved mailbox and
/// never becomes one: nothing is marked read, moved or deleted, nothing is
/// taken into intake and no subscription is made. A mailbox this host's Graph
/// identity may not read is "cannot read", never an error of the press.
/// </summary>
internal sealed class GraphBregoSignInMailReader(
    GraphApprovedMailboxResolver resolver,
    GraphMailClient client) : IBregoSignInMailReader
{
    internal const int NewestMessages = 10;

    public async Task<bool> CanReadAsync(string mailboxAddress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxAddress);
        try
        {
            var mailbox = await resolver.ResolveAsync(mailboxAddress, cancellationToken);
            return mailbox is not null && await resolver.CanReadInboxAsync(mailbox, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public Task<string?> ReadLinkAsync(
        string mailboxAddress,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken) =>
        ReadNewestAsync(
            mailboxAddress,
            sinceUtc,
            message => BregoSignInMail.ReadLink(message, mailboxAddress),
            cancellationToken);

    public Task<string?> ReadCodeAsync(
        string mailboxAddress,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken) =>
        ReadNewestAsync(mailboxAddress, sinceUtc, BregoSignInMail.ReadCode, cancellationToken);

    private async Task<string?> ReadNewestAsync(
        string mailboxAddress,
        DateTimeOffset sinceUtc,
        Func<MimeMessage, string?> read,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxAddress);
        try
        {
            var mailbox = await resolver.ResolveAsync(mailboxAddress, cancellationToken)
                ?? throw new BregoSignInMailUnreadableException();
            var page = await client.ReadFolderMessagesAsync(
                client.InitialFolderMessagesUri(mailbox.MailboxIdentity, mailbox.InboxFolderIdentity, NewestMessages),
                mailbox.MailboxIdentity,
                mailbox.InboxFolderIdentity,
                cancellationToken);
            foreach (var item in page.Items)
            {
                if (item.ReceivedAtUtc is not { } received || received < sinceUtc)
                {
                    continue;
                }

                var mime = await client.ReadFolderMimeAsync(
                    mailbox.MailboxIdentity,
                    mailbox.InboxFolderIdentity,
                    item.Id,
                    cancellationToken);
                using var stream = new MemoryStream(mime, writable: false);
                using var message = await MimeMessage.LoadAsync(stream, cancellationToken);
                if (read(message) is { } found)
                {
                    return found;
                }
            }

            return null;
        }
        catch (BregoSignInMailUnreadableException)
        {
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BregoSignInMailUnreadableException(exception);
        }
    }
}
