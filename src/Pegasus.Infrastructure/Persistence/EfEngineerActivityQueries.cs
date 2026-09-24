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

        // A report send's context is its generation, which names the Case: the
        // Case's type gives the Audit count and its origin receipt the turnaround.
        var sentOperations = await context.Set<StaffMailSendOperationEntity>()
            .AsNoTracking()
            .Where(item => item.Purpose == StaffMailPurpose.CaseReport
                && item.State == StaffMailState.Sent
                && item.ObservedSentAtUtc >= fromUtc
                && item.ObservedSentAtUtc < toUtc)
            .Select(item => new { item.ActorSubjectId, item.ContextId, SentAtUtc = item.ObservedSentAtUtc!.Value })
            .ToListAsync(cancellationToken);
        var generationIds = sentOperations.Select(item => item.ContextId).Distinct().ToArray();
        var sentCases = generationIds.Length == 0
            ? new Dictionary<Guid, (string Type, DateTimeOffset? ReceivedAtUtc)>()
            : await context.Set<CaseReportGenerationEntity>().AsNoTracking()
                .Where(generation => generationIds.Contains(generation.Id))
                .Join(context.Cases.AsNoTracking(), generation => generation.CaseId, @case => @case.Id,
                    (generation, @case) => new
                    {
                        GenerationId = generation.Id,
                        @case.Type,
                        ReceivedAtUtc = context.IntakeReceipts
                            .Where(receipt => receipt.Id == @case.OriginIntakeReceiptId)
                            .Select(receipt => (DateTimeOffset?)receipt.ReceivedAtUtc)
                            .FirstOrDefault()
                    })
                .ToDictionaryAsync(item => item.GenerationId, item => (item.Type, item.ReceivedAtUtc), cancellationToken);
        var reports = sentOperations
            .Select(item => new
            {
                EngineerId = Guid.TryParse(item.ActorSubjectId, out var id) ? (Guid?)id : null,
                item.ContextId,
                item.SentAtUtc
            })
            .Where(item => item.EngineerId is not null)
            .GroupBy(item => item.EngineerId!.Value)
            .Select(group => new
            {
                EngineerId = group.Key,
                Count = group.Count(),
                Audit = group.Count(item => sentCases.TryGetValue(item.ContextId, out var sentCase)
                    && sentCase.Type == CaseTypeCodes.Audit),
                Turnaround = Average(group
                    .Where(item => sentCases.TryGetValue(item.ContextId, out var sentCase) && sentCase.ReceivedAtUtc is not null)
                    .Select(item => item.SentAtUtc - sentCases[item.ContextId].ReceivedAtUtc!.Value))
            })
            .ToList();

        var mailboxChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var postReport = MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails);
        var queryReceipts = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == mailboxChannel
                && item.ReceivedAtUtc >= fromUtc
                && item.ReceivedAtUtc < toUtc
                && item.MailClassificationDecision != null
                && item.MailClassificationDecision.Family == postReport)
            .Select(item => new { item.Id, item.MailClassificationDecision!.Subtype })
            .ToDictionaryAsync(item => item.Id, item => item.Subtype, cancellationToken);
        var queryReceiptIds = queryReceipts.Keys.ToList();
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
        var queries = associations.Current
            .Select(pair => new
            {
                EngineerId = engineerByCase.TryGetValue(pair.Value.CaseId, out var id) ? id : (Guid?)null,
                Subtype = queryReceipts.GetValueOrDefault(pair.Key)
            })
            .Where(item => item.EngineerId is not null)
            .GroupBy(item => item.EngineerId!.Value)
            .ToDictionary(group => group.Key, group => new
            {
                Count = group.Count(),
                Disputes = group.Count(item => item.Subtype == "dispute"),
                Amendments = group.Count(item => item.Subtype == "amendment-request")
            });

        var reportsByEngineer = reports.ToDictionary(item => item.EngineerId);
        return reportsByEngineer.Keys
            .Union(queries.Keys)
            .Where(id => engineerId is null || id == engineerId.Value)
            .OrderBy(id => id)
            .Select(id => new EngineerActivityCounts(
                id,
                reportsByEngineer.TryGetValue(id, out var sent) ? sent.Count : 0,
                queries.TryGetValue(id, out var received) ? received.Count : 0,
                received?.Disputes ?? 0,
                received?.Amendments ?? 0,
                sent?.Audit ?? 0,
                sent?.Turnaround))
            .ToList();
    }

    private static TimeSpan? Average(IEnumerable<TimeSpan> durations)
    {
        var list = durations.ToList();
        return list.Count == 0 ? null : TimeSpan.FromTicks((long)list.Average(duration => duration.Ticks));
    }
}
