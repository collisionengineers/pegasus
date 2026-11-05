using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Engineer Report's counts (MI-01, D12), read from the records that
/// hold each fact: the report sends (<see cref="ReportSends"/>), each report
/// counted once on its first send and every later send a re-send (R673),
/// credited to their recorded staff sender, or for any other kind of sender
/// (Automation) to that kind's one row. Queries are CaseQueries rows that are
/// open or Done, not withdrawn, raised in the window and credited to the
/// Case's assigned Engineer (operator, 27 October 2026).
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
        // One staff member asked for reads only that sender's report sends and
        // the query Cases assigned to them; the sender's id is stored in the
        // "D" form (ActionActor.Staff). A send is told first or re-send over
        // every sender's sends before that narrowing.
        var staffSubjectId = engineerId?.ToString("D");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Every report send, so the total agrees with Reports by Principal: a
        // staff member's under their account, Automation's as its own row. A
        // send that carried both works' reports counts each (operator, 12
        // October 2026); the turnaround runs to each report's first send
        // (ReportSendPolicy.TurnaroundStart).
        var reports = (await ReportSends.InPeriodAsync(context, fromUtc, toUtc, cancellationToken))
            .Where(send => staffSubjectId == null
                || (send.ActorKind == ActorKind.Staff && send.ActorSubjectId == staffSubjectId))
            .Select(send => new
            {
                Sender = send.ActorKind != ActorKind.Staff
                    ? (send.ActorKind, Guid.Empty)
                    : Guid.TryParse(send.ActorSubjectId, out var id) ? (ActorKind.Staff, id) : ((ActorKind, Guid)?)null,
                Send = send
            })
            .Where(item => item.Sender is not null)
            .GroupBy(item => item.Sender!.Value)
            .Select(group => new
            {
                Sender = group.Key,
                Count = group.Count(item => item.Send.IsFirst),
                Audit = group.Count(item => item.Send.IsFirst && item.Send.IsAudit),
                Resends = group.Count(item => !item.Send.IsFirst),
                Turnaround = Average(group
                    .Where(item => item.Send.IsFirst && item.Send.TurnaroundStartUtc is not null)
                    .Select(item => item.Send.SentAtUtc - item.Send.TurnaroundStartUtc!.Value))
            })
            .ToList();

        var withdrawn = nameof(CaseQueryClosure.Withdrawn);
        var queryRows = await context.CaseQueries
            .AsNoTracking()
            .Where(item => item.RaisedAtUtc >= fromUtc
                && item.RaisedAtUtc < toUtc
                && item.ClosedAs != withdrawn)
            .Select(item => item.CaseId)
            .ToListAsync(cancellationToken);
        var queryCaseIds = queryRows.Distinct().ToArray();
        var engineerByCase = queryCaseIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await context.CaseWorkflows
                .AsNoTracking()
                .Where(workflow => queryCaseIds.Contains(workflow.CaseId)
                    && workflow.AssignedEngineerId != null
                    && (engineerId == null || workflow.AssignedEngineerId == engineerId))
                .ToDictionaryAsync(
                    workflow => workflow.CaseId,
                    workflow => workflow.AssignedEngineerId!.Value,
                    cancellationToken);
        var queries = queryRows
            .Select(caseId => engineerByCase.TryGetValue(caseId, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .GroupBy(id => id!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var reportsBySender = reports.ToDictionary(item => item.Sender);
        return reportsBySender.Keys
            .Union(queries.Keys.Select(id => (ActorKind.Staff, id)))
            .OrderBy(sender => sender.Item1)
            .ThenBy(sender => sender.Item2)
            .Select(sender => new EngineerActivityCounts(
                sender.Item2,
                reportsBySender.TryGetValue(sender, out var sent) ? sent.Count : 0,
                sender.Item1 == ActorKind.Staff && queries.TryGetValue(sender.Item2, out var received) ? received : 0,
                sent?.Audit ?? 0,
                sent?.Turnaround,
                sent?.Resends ?? 0)
            {
                SenderKind = sender.Item1
            })
            .ToList();
    }

    private static TimeSpan? Average(IEnumerable<TimeSpan> durations)
    {
        var list = durations.ToList();
        return list.Count == 0 ? null : TimeSpan.FromTicks((long)list.Average(duration => duration.Ticks));
    }
}
