using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Tasks;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads the addresses a Case chaser opens addressed to: the origin receipt's
/// route sender, the route sender of each paired image intake and the e-mail
/// on the accepted repairer's directory record.
/// </summary>
public sealed class EfCaseChaserRecipientQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseChaserRecipientQueries
{
    public async Task<CaseChaserRecipients?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The instruction sender is read whatever the Principal's report
        // delivery setting says; that setting governs report delivery only.
        var row = await (from @case in context.Cases.AsNoTracking()
                         join receipt in context.Set<IntakeReceiptEntity>().AsNoTracking() on @case.OriginIntakeReceiptId equals receipt.Id into receipts
                         from receipt in receipts.DefaultIfEmpty()
                         join route in context.IntakeMailRouteDecisions.AsNoTracking() on receipt.Id equals route.IntakeReceiptId into routes
                         from route in routes.DefaultIfEmpty()
                         where @case.Id == caseId
                         select new
                         {
                             OriginalInstructionSender = route == null
                                 ? null
                                 : route.EffectiveSenderAddress
                         })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        // The same pairing the Case's image intakes are listed by: an active
        // manual association, else the Case link when no association exists.
        var imageSenders = await context.ImageIntakes.AsNoTracking()
            .Where(intake =>
                context.IntakeManualAssociations.Any(association =>
                    association.IntakeReceiptId == intake.OriginReceiptId
                    && association.IsActive && association.CaseId == caseId)
                || (!context.IntakeManualAssociations.Any(association =>
                        association.IntakeReceiptId == intake.OriginReceiptId)
                    && context.CaseIntakeLinks.Any(link =>
                        link.IntakeReceiptId == intake.OriginReceiptId && link.CaseId == caseId)))
            .OrderBy(intake => intake.CreatedAtUtc).ThenBy(intake => intake.Id)
            .Select(intake => intake.OriginReceipt.MailRouteDecision == null
                ? null
                : intake.OriginReceipt.MailRouteDecision.EffectiveSenderAddress)
            .ToArrayAsync(cancellationToken);

        var workIds = CaseWorkScope.SelectedIds(context, caseId, CaseWorkSelector.Current);
        var repairerFields = await context.CaseDataFields.AsNoTracking()
            .Where(field => workIds.Contains(field.WorkId) && field.FieldName == CaseDataFieldNames.RepairerId)
            .ToArrayAsync(cancellationToken);
        var repairerId = CaseDataFieldValues.Accepted(repairerFields, CaseDataFieldNames.RepairerId) is { } value
            ? Guid.ParseExact(value, "D")
            : (Guid?)null;
        var repairerEmail = repairerId is { } id
            ? await context.Organizations.AsNoTracking()
                .Where(organization => organization.Id == id)
                .Select(organization => organization.Email)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new(
            row.OriginalInstructionSender,
            [.. imageSenders.OfType<string>().Distinct(StringComparer.Ordinal)],
            string.IsNullOrEmpty(repairerEmail) ? null : repairerEmail);
    }
}
