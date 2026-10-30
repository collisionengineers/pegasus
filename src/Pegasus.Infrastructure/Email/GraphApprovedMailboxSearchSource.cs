using System.Text.Json;
using Azure.Identity;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// The approved-mailbox search port over Graph (R550). It asks each pollable
/// approved mailbox, or the one named, and reports a mailbox the tenant has
/// not admitted, or one that throttled or failed, as Unavailable rather than
/// empty. Read-only: nothing is marked read, moved or changed.
/// </summary>
internal sealed class GraphApprovedMailboxSearchSource(
    GraphMailClient client,
    IApprovedIntakeMailboxes approvedMailboxes) : IApprovedMailboxSearchSource
{
    public async Task<MailboxSearchResult> SearchAsync(
        Guid? mailboxId,
        string term,
        int maximumItemsPerMailbox,
        CancellationToken cancellationToken)
    {
        var approved = await approvedMailboxes.ListPollableAsync(cancellationToken);
        var selected = mailboxId is null
            ? approved
            : approved.Where(item => item.ApprovedMailboxId == mailboxId).ToArray();

        var items = new List<MailboxSearchItem>();
        var mailboxes = new List<MailboxSearchMailbox>();
        var truncated = false;
        foreach (var mailbox in selected.OrderBy(item => item.Address, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var hits = await client.SearchMessagesAsync(
                    mailbox.GraphMailboxId,
                    term,
                    maximumItemsPerMailbox,
                    cancellationToken);
                items.AddRange(hits.Select(hit => new MailboxSearchItem(
                    mailbox.ApprovedMailboxId,
                    mailbox.Address,
                    hit.Id,
                    hit.Subject,
                    hit.SenderAddress,
                    hit.ReceivedAtUtc,
                    hit.Preview)));
                truncated |= hits.Count >= maximumItemsPerMailbox;
                mailboxes.Add(new(mailbox.ApprovedMailboxId, mailbox.Address, OldSystemLookupState.Available));
            }
            catch (Exception exception) when (IsUnavailable(exception, cancellationToken))
            {
                mailboxes.Add(new(mailbox.ApprovedMailboxId, mailbox.Address, OldSystemLookupState.Unavailable));
            }
        }

        return new(
            items.OrderByDescending(item => item.ReceivedAtUtc).ThenBy(item => item.MessageId, StringComparer.Ordinal).ToArray(),
            mailboxes,
            truncated);
    }

    private static bool IsUnavailable(Exception exception, CancellationToken cancellationToken) =>
        exception is ApprovedMailboxAccessDeniedException
            or ApprovedSentSourceThrottledException
            or AuthenticationFailedException
            or HttpRequestException
            or InvalidDataException
            or JsonException
            or UnauthorizedAccessException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
