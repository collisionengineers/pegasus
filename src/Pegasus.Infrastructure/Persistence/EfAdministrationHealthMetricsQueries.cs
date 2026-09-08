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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mailboxFailures = await context.ApprovedSentPollStates.AsNoTracking()
            .CountAsync(x => x.LastFailureCode != null, cancellationToken);
        var unknownSends = await context.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .CountAsync(x => x.State == Pegasus.Core.Operations.StaffMailState.Unknown, cancellationToken);
        var work = await context.IntakeWorkItems.AsNoTracking()
            .GroupBy(x => x.State)
            .Select(group => new { State = group.Key, Count = group.Count(), OldestDueAtUtc = group.Min(x => x.DueAtUtc) })
            .ToListAsync(cancellationToken);
        var mailboxHealth = await context.ApprovedInboxPollStates.AsNoTracking()
            .Select(item => new MailPollHealth(
                item.ApprovedMailboxId,
                item.LastCompletedAtUtc,
                item.LastFailureCode,
                item.DueAtUtc))
            .ToListAsync(cancellationToken);
        var oldestPendingCustody = await context.Set<DocumentVersionEntity>().AsNoTracking()
            .Where(item => item.CustodyStatus == DocumentCustodyStatus.Pending)
            .Select(item => (DateTimeOffset?)item.CreatedAtUtc)
            .MinAsync(cancellationToken);
        var poisonedInboxMessages = await context.ApprovedInboxPoisonMessages.AsNoTracking()
            .CountAsync(cancellationToken);
        var cache = await context.Set<DocumentContentCacheEntryEntity>().AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Entries = group.Count(),
                Bytes = group.Sum(x => x.VerifiedSize),
                NextExpiryAtUtc = group.Min(x => (DateTimeOffset?)x.ExpiresAtUtc),
                ActiveReadLeases = group.Count(x => x.ReadLeaseExpiresAtUtc > nowUtc),
                CleanupFailures = group.Count(x => !string.IsNullOrWhiteSpace(x.LastCleanupOutcome))
            })
            .SingleOrDefaultAsync(cancellationToken);
        var oldestPendingAiJob = await context.AiJobs.AsNoTracking()
            .Where(x => x.State == nameof(AiJobState.Queued)
                || x.State == nameof(AiJobState.Taken)
                || x.State == nameof(AiJobState.DraftReady))
            .Select(x => (DateTimeOffset?)x.CreatedAtUtc)
            .MinAsync(cancellationToken);
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
}
