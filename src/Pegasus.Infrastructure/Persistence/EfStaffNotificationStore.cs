using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Notifications;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfStaffNotificationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IStaffNotificationStore
{
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

    public async Task<int> CountUnreadAsync(Guid staffId, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<StaffNotificationEntity>().AsNoTracking()
            .CountAsync(item => item.StaffId == staffId && item.RaisedAtUtc >= sinceUtc && item.ReadAtUtc == null, cancellationToken);
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
        }

        return Map(entity);
    }

    public async Task<int> MarkAllReadAsync(Guid staffId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<StaffNotificationEntity>()
            .Where(item => item.StaffId == staffId && item.ReadAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.ReadAtUtc, readAtUtc), cancellationToken);
    }

    public async Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<StaffNotificationEntity>()
            .Where(item => item.RaisedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
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
