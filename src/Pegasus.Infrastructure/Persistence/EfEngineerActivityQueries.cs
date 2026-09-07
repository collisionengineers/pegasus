using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Engineer Report's two counts (MI-01, D12), read from the records that
/// hold each fact: observed staff-mail send operations for reports, credited
/// only to their recorded staff actor, and mailbox receipts whose classification
/// decision is post-report. Case association for a receipt is the same rule the Inbox applies
/// (<see cref="CurrentIntakeAssociations"/>), so a query an operator has
/// unlinked from a case is not counted against that case's Engineer.
/// </summary>
internal sealed class EfEngineerActivityQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IEngineerActivityQueries
{
    private readonly IDbContextFactory<PegasusDbContext> contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<IReadOnlyList<EngineerActivityCounts>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? engineerId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var reportActors = await context.Set<StaffMailSendOperationEntity>()
            .AsNoTracking()
            .Where(item => item.Purpose == StaffMailPurpose.CaseReport
                && item.State == StaffMailState.Sent
                && item.ObservedSentAtUtc >= fromUtc
                && item.ObservedSentAtUtc < toUtc)
            .Select(item => item.ActorSubjectId)
            .ToListAsync(cancellationToken);
        var reports = reportActors
            .Select(actor => Guid.TryParse(actor, out var id) ? (Guid?)id : null)
            .Where(id => id is not null)
            .GroupBy(id => id!.Value)
            .Select(group => new { EngineerId = group.Key, Count = group.Count() })
            .ToList();

        var mailboxChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var postReport = MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails);
        var queryReceiptIds = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == mailboxChannel
                && item.ReceivedAtUtc >= fromUtc
                && item.ReceivedAtUtc < toUtc
                && item.MailClassificationDecision != null
                && item.MailClassificationDecision.Family == postReport)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var associations = await CurrentIntakeAssociations.ReadAsync(
            context,
            queryReceiptIds,
            cancellationToken);
        var queryCaseIds = associations.Current.Values
            .Select(association => association.CaseId)
            .Distinct()
            .ToArray();
        var engineerByCase = queryCaseIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await context.CaseWorkflows
                .AsNoTracking()
                .Where(workflow => queryCaseIds.Contains(workflow.CaseId)
                    && workflow.AssignedEngineerId != null)
                .ToDictionaryAsync(
                    workflow => workflow.CaseId,
                    workflow => workflow.AssignedEngineerId!.Value,
                    cancellationToken);
        var queries = associations.Current.Values
            .Select(association => engineerByCase.TryGetValue(association.CaseId, out var id)
                ? id
                : (Guid?)null)
            .Where(id => id is not null)
            .GroupBy(id => id!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var reportsByEngineer = reports.ToDictionary(item => item.EngineerId, item => item.Count);
        return reportsByEngineer.Keys
            .Union(queries.Keys)
            .Where(id => engineerId is null || id == engineerId.Value)
            .OrderBy(id => id)
            .Select(id => new EngineerActivityCounts(
                id,
                reportsByEngineer.GetValueOrDefault(id),
                queries.GetValueOrDefault(id)))
            .ToList();
    }
}
