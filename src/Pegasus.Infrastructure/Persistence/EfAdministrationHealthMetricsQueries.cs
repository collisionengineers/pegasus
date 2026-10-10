using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfAdministrationHealthMetricsQueries(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IDocumentContentCacheMetrics? cacheMetrics = null) : IAdministrationHealthMetricsQueries
{
    public async Task<AdministrationHealthMetrics> GetAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        // A DbContext is not thread-safe: each read takes its own so the eight run together.
        var mailboxFailuresRead = ReadAsync(cancellationToken, context => context.ApprovedSentPollStates.AsNoTracking()
            .CountAsync(x => x.LastFailureCode != null, cancellationToken));
        var unknownSendsRead = ReadAsync(cancellationToken, context => context.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .CountAsync(x => x.State == Pegasus.Core.Operations.StaffMailState.Unknown, cancellationToken));
        var workRead = ReadAsync(cancellationToken, context => context.IntakeWorkItems.AsNoTracking()
            .GroupBy(x => x.State)
            .Select(group => new { State = group.Key, Count = group.Count(), OldestDueAtUtc = group.Min(x => x.DueAtUtc) })
            .ToListAsync(cancellationToken));
        var mailboxHealthRead = ReadAsync(cancellationToken, context => context.ApprovedInboxPollStates.AsNoTracking()
            .Select(item => new MailPollHealth(
                item.ApprovedMailboxId,
                item.LastCompletedAtUtc,
                item.LastFailureCode,
                item.DueAtUtc))
            .ToListAsync(cancellationToken));
        var oldestPendingCustodyRead = ReadAsync(cancellationToken, context => context.Set<DocumentVersionEntity>().AsNoTracking()
            .Where(item => item.CustodyStatus == DocumentCustodyStatus.Pending)
            .Select(item => (DateTimeOffset?)item.CreatedAtUtc)
            .MinAsync(cancellationToken));
        var poisonedInboxMessagesRead = ReadAsync(cancellationToken, context => context.ApprovedInboxPoisonMessages.AsNoTracking()
            .CountAsync(cancellationToken));
        var cacheRead = ReadAsync(cancellationToken, context => context.Set<DocumentContentCacheEntryEntity>().AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Entries = group.Count(),
                Bytes = group.Sum(x => x.VerifiedSize),
                NextExpiryAtUtc = group.Min(x => (DateTimeOffset?)x.ExpiresAtUtc),
                ActiveReadLeases = group.Count(x => x.ReadLeaseExpiresAtUtc > nowUtc),
                CleanupFailures = group.Count(x => !string.IsNullOrWhiteSpace(x.LastCleanupOutcome))
            })
            .SingleOrDefaultAsync(cancellationToken));
        var oldestPendingAiJobRead = ReadAsync(cancellationToken, context => context.AiJobs.AsNoTracking()
            .Where(x => x.State == nameof(AiJobState.Queued)
                || x.State == nameof(AiJobState.Taken)
                || x.State == nameof(AiJobState.DraftReady))
            .Select(x => (DateTimeOffset?)x.CreatedAtUtc)
            .MinAsync(cancellationToken));
        await Task.WhenAll(
            mailboxFailuresRead, unknownSendsRead, workRead, mailboxHealthRead,
            oldestPendingCustodyRead, poisonedInboxMessagesRead, cacheRead, oldestPendingAiJobRead);
        var mailboxFailures = await mailboxFailuresRead;
        var unknownSends = await unknownSendsRead;
        var work = await workRead;
        var mailboxHealth = await mailboxHealthRead;
        var oldestPendingCustody = await oldestPendingCustodyRead;
        var poisonedInboxMessages = await poisonedInboxMessagesRead;
        var cache = await cacheRead;
        var oldestPendingAiJob = await oldestPendingAiJobRead;
        var processCache = cacheMetrics?.Snapshot() ?? new(0, 0);
        return new(
            mailboxFailures,
            unknownSends,
            work.Where(x => EfIntakeWorkStore.ParseState(x.State) == IntakeWorkState.Failed).Sum(x => x.Count),
            work.Where(x => EfIntakeWorkStore.ParseState(x.State) is not IntakeWorkState.Failed and not IntakeWorkState.Completed)
                .Select(x => (DateTimeOffset?)x.OldestDueAtUtc).Min(),
            oldestPendingCustody,
            poisonedInboxMessages,
            oldestPendingAiJob,
            cache?.Entries ?? 0,
            cache?.Bytes ?? 0,
            cache?.NextExpiryAtUtc,
            cache?.ActiveReadLeases ?? 0,
            cache?.CleanupFailures ?? 0,
            GetRetainedMailFreshness.Evaluate(mailboxHealth, nowUtc),
            processCache.HitsSinceStart,
            processCache.MissesSinceStart);
    }

    private async Task<T> ReadAsync<T>(CancellationToken cancellationToken, Func<PegasusDbContext, Task<T>> read)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await read(context);
    }
}
