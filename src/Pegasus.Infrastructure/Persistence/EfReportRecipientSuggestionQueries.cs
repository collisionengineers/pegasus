using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>Reads delivery suggestions from the Principal and origin receipt.</summary>
public sealed class EfReportRecipientSuggestionQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IReportRecipientSuggestionQueries
{
    public async Task<ReportRecipientSuggestions?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (from @case in context.Cases.AsNoTracking()
                         join principal in context.Principals.AsNoTracking() on @case.PrincipalId equals principal.Id
                         join receipt in context.Set<IntakeReceiptEntity>().AsNoTracking() on @case.OriginIntakeReceiptId equals receipt.Id into receipts
                         from receipt in receipts.DefaultIfEmpty()
                         join route in context.IntakeMailRouteDecisions.AsNoTracking() on receipt.Id equals route.IntakeReceiptId into routes
                         from route in routes.DefaultIfEmpty()
                         where @case.Id == caseId
                         select new
                         {
                             @case.Reference,
                             principal.IncludeOriginalInstructionSender,
                             principal.ReportRecipientAddressesJson,
                             OriginalInstructionSender = route == null
                                 ? null
                                 : route.EffectiveSenderAddress
                         })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var settings = PrincipalReportRecipientSettings.Normalize(
            row.IncludeOriginalInstructionSender,
            JsonSerializer.Deserialize<string[]>(
                row.ReportRecipientAddressesJson,
                EfOrganizationAdministration.SerializerOptions));
        return new(row.Reference, settings,
            settings.IncludeOriginalInstructionSender
                ? row.OriginalInstructionSender
                : null);
    }
}
