using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The two Service health facts no other port owns: the Sent-items poll
/// cursor rows and the queued-intake dispatcher by state. Both are aggregate
/// reads of rows the Worker already writes; nothing here contacts a service.
/// </summary>
internal sealed class EfServiceHealthQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IServiceHealthQueries
{
    private readonly IDbContextFactory<PegasusDbContext> contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<IReadOnlyList<SentEvidencePollStatus>> ListSentEvidencePollStatusAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ApprovedSentPollStates
            .AsNoTracking()
            .OrderBy(item => item.MailboxAddress)
            .Select(item => new SentEvidencePollStatus(
                item.MailboxAddress,
                item.DueAtUtc,
                item.LastCompletedAtUtc,
                item.LastFailureCode))
            .ToListAsync(cancellationToken);
    }

    public async Task<IntakeDispatchHealth> GetIntakeDispatchHealthAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Grouped at the store; the persisted code is parsed afterwards
        // because the store owns the vocabulary and Enum parsing has no SQL.
        var counts = await context.IntakeWorkItems
            .AsNoTracking()
            .GroupBy(item => item.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var latestCompleted = await context.IntakeWorkItems
            .AsNoTracking()
            .Where(item => item.CompletedAtUtc != null)
            .MaxAsync(item => item.CompletedAtUtc, cancellationToken);

        var active = 0;
        var retryScheduled = 0;
        var failed = 0;
        foreach (var entry in counts)
        {
            switch (EfIntakeWorkStore.ParseState(entry.State))
            {
                case IntakeWorkState.Pending:
                case IntakeWorkState.Dispatching:
                case IntakeWorkState.Dispatched:
                case IntakeWorkState.Processing:
                    active += entry.Count;
                    break;
                case IntakeWorkState.RetryScheduled:
                    retryScheduled += entry.Count;
                    break;
                case IntakeWorkState.Failed:
                    failed += entry.Count;
                    break;
                case IntakeWorkState.Completed:
                    break;
            }
        }

        return new(active, retryScheduled, failed, latestCompleted);
    }
}
