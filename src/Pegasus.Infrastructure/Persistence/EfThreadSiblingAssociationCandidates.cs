using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The thread sibling sweep's page, read in one statement. A receipt holds no
/// Case as the No Case Found scope reads it
/// (<see cref="CurrentIntakeAssociations.ReceiptsHoldingNoCase"/>), with no
/// association row at all. Its exact mailbox conversation has another message
/// that stands linked to a Case or opened a Triage, as the mail association
/// evidence reads a thread (<see cref="CurrentIntakeAssociations.ThreadReceipts"/>).
/// The window's start is the one Core passes in, so this adapter reads no
/// clock of its own.
/// </summary>
internal sealed class EfThreadSiblingAssociationCandidates(
    IDbContextFactory<PegasusDbContext> contextFactory) : IThreadSiblingAssociationCandidates
{
    private const string Mailbox = "mailbox";
    private const string Classified = "classified";

    public async Task<IReadOnlyList<Guid>> ListAsync(
        DateTimeOffset completedAfterUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var prePegasusCase = MailCategories.Code(MailCategory.PrePegasusCase);
        var receiptOrigin = nameof(UnidentifiedOriginKind.Receipt);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var holdingNoCase = CurrentIntakeAssociations.ReceiptsHoldingNoCase(context);
        var linked = CurrentIntakeAssociations.LinkedReceiptIds(context);
        var listed =
            from work in context.IntakeWorkItems.AsNoTracking()
            join receipt in context.IntakeReceipts.AsNoTracking()
                on work.ProcessedReceiptId equals (Guid?)receipt.Id
            join message in context.RetainedMailboxMessages.AsNoTracking()
                on receipt.ExternalReceiptToken equals message.ExternalReceiptToken
            where work.State == "completed"
                && work.CompletedAtUtc > completedAfterUtc
                && receipt.SourceChannel == Mailbox
                && message.ConversationIdentity != null
                && holdingNoCase.Any(item => item.Id == receipt.Id)
                && !context.IntakeManualAssociations.Any(item => item.IntakeReceiptId == receipt.Id)
                && receipt.Decision != "image_intake_registered"
                && !(receipt.MailClassificationDecision != null
                    && receipt.MailClassificationDecision.Outcome == Classified
                    && receipt.MailClassificationDecision.Category == prePegasusCase)
                && !context.UnidentifiedItems.Any(item =>
                    item.OriginKind == receiptOrigin && item.OriginId == receipt.Id)
                && !context.IntakeSubmissionGroups.Any(submissionGroup => submissionGroup.DiscardedAtUtc != null
                    && context.IntakeSubmissionGroupMembers.Any(member => member.GroupId == submissionGroup.Id
                        && member.StagedReceiptId == work.StagedReceiptId))
                && context.RetainedMailboxMessages.Any(sibling => sibling.MailboxId == message.MailboxId
                    && sibling.ConversationIdentity == message.ConversationIdentity
                    && sibling.ExternalReceiptToken != message.ExternalReceiptToken
                    && context.IntakeReceipts.Any(other => other.SourceChannel == Mailbox
                        && other.ExternalReceiptToken == sibling.ExternalReceiptToken
                        && (linked.Contains(other.Id)
                            || context.Triage.Any(triage => triage.OriginReceiptId == other.Id))))
            select new { receipt.Id, work.CompletedAtUtc };
        return await listed
            .OrderBy(row => row.CompletedAtUtc)
            .ThenBy(row => row.Id)
            .Select(row => row.Id)
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken);
    }
}
