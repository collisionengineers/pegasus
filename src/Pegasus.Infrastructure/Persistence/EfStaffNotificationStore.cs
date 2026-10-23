using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Notifications;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfStaffNotificationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IMemoryCache cache) : IStaffNotificationStore
{
    // Every full page asks the bell's unread count, so each person's count is
    // kept for 30 seconds (R409, A1-03). Web is the only writer that marks a
    // row read and runs as one instance (ADR-0058), so its writes evict the
    // person's entry; a row the Worker raises shows once the entry expires,
    // which is the ruled lag. The count is kept for the window the first
    // reader asked, which differs from a later reader's by seconds.
    private static readonly TimeSpan UnreadLifetime = TimeSpan.FromSeconds(30);

    // Counts every eviction, so a count read before a write is never stored
    // after that write's eviction (as StaffAccountReads.Generation does).
    private static long evictions;

    // A private key type, so no other IMemoryCache user can collide with an entry.
    private readonly record struct UnreadKey(Guid StaffId);

    public async Task<StaffNotification> AddAsync(
        NewStaffNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new StaffNotificationEntity
        {
            Id = Guid.NewGuid(),
            StaffId = notification.StaffId,
            CaseId = notification.CaseId,
            Reference = notification.Reference,
            Registration = notification.Registration,
            Cause = notification.Cause.ToString(),
            Route = notification.Route,
            ActorSubjectId = notification.ActorSubjectId,
            RaisedAtUtc = timeProvider.GetUtcNow(),
            ReadAtUtc = null
        };
        context.Set<StaffNotificationEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        Forget(notification.StaffId);
        return Map(entity);
    }

    public async Task<IReadOnlyList<StaffNotification>> ListAsync(
        Guid staffId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<StaffNotificationEntity>().AsNoTracking()
            .Where(item => item.StaffId == staffId && item.RaisedAtUtc >= sinceUtc)
            .OrderByDescending(item => item.RaisedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<int> CountUnreadAsync(
        Guid staffId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        var key = new UnreadKey(staffId);
        if (cache.TryGetValue(key, out int kept))
        {
            return kept;
        }

        // Taken before the read: a write that lands during it evicts, and the
        // count is then not stored.
        var generation = Interlocked.Read(ref evictions);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var unread = await context.Set<StaffNotificationEntity>().AsNoTracking()
            .CountAsync(
                item => item.StaffId == staffId && item.RaisedAtUtc >= sinceUtc && item.ReadAtUtc == null,
                cancellationToken);
        cache.Set(key, unread, UnreadLifetime);
        if (Interlocked.Read(ref evictions) != generation)
        {
            cache.Remove(key);
        }

        return unread;
    }

    public async Task<StaffNotification?> MarkReadAsync(
        Guid staffId,
        Guid notificationId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<StaffNotificationEntity>()
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.StaffId == staffId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.ReadAtUtc is null)
        {
            entity.ReadAtUtc = readAtUtc;
            await context.SaveChangesAsync(cancellationToken);
            Forget(staffId);
        }

        return Map(entity);
    }

    public async Task<int> MarkAllReadAsync(Guid staffId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var marked = await context.Set<StaffNotificationEntity>()
            .Where(item => item.StaffId == staffId && item.ReadAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.ReadAtUtc, readAtUtc), cancellationToken);
        Forget(staffId);
        return marked;
    }

    public async Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<StaffNotificationEntity>()
            .Where(item => item.RaisedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void Forget(Guid staffId)
    {
        Interlocked.Increment(ref evictions);
        cache.Remove(new UnreadKey(staffId));
    }

    private static StaffNotification Map(StaffNotificationEntity entity) => new(
        entity.Id,
        entity.StaffId,
        entity.CaseId,
        entity.Reference,
        entity.Registration,
        Enum.Parse<StaffNotificationCause>(entity.Cause),
        entity.Route,
        entity.RaisedAtUtc,
        entity.ReadAtUtc)
    {
        ActorSubjectId = entity.ActorSubjectId
    };
}
