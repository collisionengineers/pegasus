using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads a Case's instruction email: the retained mailbox message whose
/// receipt is the Case's origin receipt.
/// </summary>
public sealed class EfCaseInstructionEmailQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseInstructionEmailQueries
{
    public async Task<CaseInstructionEmail?> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (from @case in context.Cases.AsNoTracking()
                          join receipt in context.IntakeReceipts.AsNoTracking()
                              on @case.OriginIntakeReceiptId equals receipt.Id
                          join retained in context.RetainedMailboxMessages.AsNoTracking()
                              on receipt.ExternalReceiptToken equals retained.ExternalReceiptToken
                          join route in context.IntakeMailRouteDecisions.AsNoTracking()
                              on receipt.Id equals route.IntakeReceiptId into routes
                          from route in routes.DefaultIfEmpty()
                          where @case.Id == caseId
                              && receipt.SourceChannel == "mailbox"
                              && receipt.ExternalReceiptToken != ""
                              && retained.MailboxId != null
                          select new CaseInstructionEmail(
                              retained.Id,
                              retained.MailboxId!.Value,
                              retained.ImmutableMessageId,
                              retained.InternetMessageIdentity,
                              retained.ConversationIdentity,
                              retained.Subject,
                              retained.SenderAddress,
                              route == null ? null : route.EffectiveSenderAddress))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (rows.Count > 1)
        {
            throw new InvalidOperationException(
                "The Case's origin receipt matches more than one retained mailbox message.");
        }

        return rows.SingleOrDefault();
    }
}
