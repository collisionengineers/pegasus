using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

public sealed record AdministrationHealthMetrics(
    int MailboxFailures,
    int UnknownSends,
    int IntakeFailed,
    DateTimeOffset? OldestPendingIntakeDueAtUtc,
    DateTimeOffset? OldestPendingCustodyCreatedAtUtc,
    int PoisonedInboxMessages,
    DateTimeOffset? OldestPendingAiJobCreatedAtUtc,
    int CacheEntries,
    long CacheBytes,
    DateTimeOffset? NextCacheExpiryAtUtc,
    int ActiveCacheReadLeases,
    int CacheCleanupFailures,
    MailFreshness MailboxFreshness,
    long CacheHitsSinceStart = 0,
    long CacheMissesSinceStart = 0);

public sealed record DocumentContentCacheMetricSnapshot(long HitsSinceStart, long MissesSinceStart);

public interface IDocumentContentCacheMetrics
{
    DocumentContentCacheMetricSnapshot Snapshot();
    void RecordHit();
    void RecordMiss();
}

public interface IAdministrationHealthMetricsQueries
{
    Task<AdministrationHealthMetrics> GetAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

public sealed class GetAdministrationHealthMetrics(IAdministrationHealthMetricsQueries queries)
{
    public async Task<AdministrationHealthMetrics> ExecuteAsync(
        ActionActor actor, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        return await queries.GetAsync(nowUtc, cancellationToken);
    }
}
