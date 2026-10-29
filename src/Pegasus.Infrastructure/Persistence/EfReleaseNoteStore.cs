using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfReleaseNoteStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IMemoryCache cache) : IReleaseNoteStore
{
    // Each person's unacknowledged note is kept in IMemoryCache. Publishing and
    // acknowledging are the only writes that change it, and the Web is their
    // only writer (ADR-0054) and runs as one instance (ADR-0058), so each
    // clears what it changes. A change made outside Pegasus (direct SQL) is
    // seen once the entry expires.
    private static readonly TimeSpan UnacknowledgedSlidingWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnacknowledgedCeiling = StaffSessionPolicy.AccountReadLifetime;

    // Every person's entry belongs to this family, which a publish expires at once.
    private static readonly object UnacknowledgedFamily = new();

    // A private key type, so no other IMemoryCache user can collide with an entry.
    private readonly record struct UnacknowledgedKey(Guid StaffId);

    public async Task<ReleaseNote?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ReleaseNoteEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    /// <summary>
    /// One window of the Administrator's list, selecting only the columns it
    /// draws: never <c>Body</c> (#1542). The status is parsed once the rows are
    /// read, because <c>Enum.Parse</c> does not translate to SQL.
    /// </summary>
    public async Task<IReadOnlyList<ReleaseNoteListItem>> ListAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<ReleaseNoteEntity>().AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip(offset)
            .Take(limit)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Status,
                item.Version,
                item.PublishedByStaffId,
                item.PublishedAtUtc
            })
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => new ReleaseNoteListItem(
                row.Id,
                row.Title,
                Enum.Parse<ReleaseNoteStatus>(row.Status),
                row.Version,
                row.PublishedByStaffId,
                row.PublishedAtUtc))
            .ToArray();
    }

    public async Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await Published(context)
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<ReleaseNote> AddAsync(NewReleaseNote note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(note);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Set<ReleaseNoteEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == note.OperationKey, cancellationToken);
        if (existing is not null) return Replay(existing, note);
        var entity = new ReleaseNoteEntity
        {
            Id = Guid.NewGuid(),
            Title = note.Title,
            Body = note.Body,
            Status = nameof(ReleaseNoteStatus.Draft),
            CreatedByStaffId = note.CreatedByStaffId,
            CreatedAtUtc = note.AtUtc,
            UpdatedAtUtc = note.AtUtc,
            RowVersion = 1,
            OperationKey = note.OperationKey,
            RequestHash = note.RequestHash
        };
        context.Set<ReleaseNoteEntity>().Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await using var replayContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var winner = await replayContext.Set<ReleaseNoteEntity>().AsNoTracking()
                .SingleAsync(item => item.OperationKey == note.OperationKey, cancellationToken);
            return Replay(winner, note);
        }
    }

    public async Task<ReleaseNote> UpdateDraftAsync(
        Guid id,
        long expectedRowVersion,
        string title,
        string body,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await LoadDraftAsync(context, id, expectedRowVersion, cancellationToken);
        entity.Title = title;
        entity.Body = body;
        entity.UpdatedAtUtc = atUtc;
        entity.RowVersion = expectedRowVersion + 1;
        await SaveAsync(context, id, cancellationToken);
        return Map(entity);
    }

    public async Task<ReleaseNote> PublishAsync(
        Guid id,
        long expectedRowVersion,
        string title,
        string body,
        ApplicationBuild build,
        Guid publishedByStaffId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ReleaseNoteEntity>()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ReleaseNoteConflictException(id);
        if (entity.Status == nameof(ReleaseNoteStatus.Published)
            && entity.RowVersion == expectedRowVersion + 1
            && entity.PublishedByStaffId == publishedByStaffId
            && entity.Title == title && entity.Body == body
            && entity.Version == build.Version && entity.SourceSha == build.SourceSha)
            return Map(entity);
        if (entity.Status != nameof(ReleaseNoteStatus.Draft)
            || entity.RowVersion != expectedRowVersion)
            throw new ReleaseNoteConflictException(id);
        entity.Title = title;
        entity.Body = body;
        entity.Status = nameof(ReleaseNoteStatus.Published);
        entity.Version = build.Version;
        entity.SourceSha = build.SourceSha;
        entity.PublishedByStaffId = publishedByStaffId;
        entity.PublishedAtUtc = atUtc;
        entity.UpdatedAtUtc = atUtc;
        entity.RowVersion = expectedRowVersion + 1;
        await SaveAsync(context, id, cancellationToken);
        ExpireEveryUnacknowledged();
        return Map(entity);
    }

    public Task<ReleaseNote?> GetUnacknowledgedAsync(Guid staffId, CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache,
            new UnacknowledgedKey(staffId),
            UnacknowledgedFamily,
            UnacknowledgedCeiling,
            UnacknowledgedSlidingWindow,
            token => ReadUnacknowledgedAsync(staffId, token),
            cancellationToken);

    private async Task<ReleaseNote?> ReadUnacknowledgedAsync(Guid staffId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Every full page asks this. One read names the newest note and says
        // whether this person has acknowledged it, and the body is read only
        // for the note that is about to be shown.
        var newest = await Published(context)
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id,
                Acknowledged = context.Set<ReleaseNoteAcknowledgementEntity>()
                    .Any(ack => ack.StaffId == staffId && ack.ReleaseNoteId == item.Id)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (newest is null || newest.Acknowledged)
        {
            return null;
        }

        return Map(await Published(context).SingleAsync(item => item.Id == newest.Id, cancellationToken));
    }

    public async Task AcknowledgeAsync(Guid staffId, Guid noteId, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await context.Set<ReleaseNoteAcknowledgementEntity>()
            .AnyAsync(item => item.StaffId == staffId && item.ReleaseNoteId == noteId, cancellationToken);
        if (exists)
        {
            ForgetUnacknowledged(staffId);
            return;
        }

        var published = await Published(context).AnyAsync(item => item.Id == noteId, cancellationToken);
        if (!published)
        {
            ForgetUnacknowledged(staffId);
            return;
        }

        context.Set<ReleaseNoteAcknowledgementEntity>().Add(new ReleaseNoteAcknowledgementEntity
        {
            StaffId = staffId,
            ReleaseNoteId = noteId,
            AcknowledgedAtUtc = atUtc
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // Two tabs acknowledged at once; the row is there, which is all that was asked.
        }

        ForgetUnacknowledged(staffId);
    }

    private void ForgetUnacknowledged(Guid staffId) =>
        EvictableCacheEntry.Evict(cache, new UnacknowledgedKey(staffId));

    private void ExpireEveryUnacknowledged() =>
        EvictableCacheEntry.EvictFamily(cache, UnacknowledgedFamily);

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }

    private static ReleaseNote Replay(ReleaseNoteEntity existing, NewReleaseNote note)
    {
        if (existing.CreatedByStaffId != note.CreatedByStaffId
            || !string.Equals(existing.RequestHash, note.RequestHash, StringComparison.Ordinal))
            throw new ReleaseNoteConflictException(existing.Id);
        return Map(existing);
    }

    private static IQueryable<ReleaseNoteEntity> Published(PegasusDbContext context) =>
        context.Set<ReleaseNoteEntity>().AsNoTracking()
            .Where(item => item.Status == nameof(ReleaseNoteStatus.Published));

    private static async Task<ReleaseNoteEntity> LoadDraftAsync(
        PegasusDbContext context,
        Guid id,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var entity = await context.Set<ReleaseNoteEntity>()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ReleaseNoteConflictException(id);
        if (entity.Status != nameof(ReleaseNoteStatus.Draft) || entity.RowVersion != expectedRowVersion)
        {
            throw new ReleaseNoteConflictException(id);
        }

        return entity;
    }

    private static async Task SaveAsync(PegasusDbContext context, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReleaseNoteConflictException(id);
        }
    }

    private static ReleaseNote Map(ReleaseNoteEntity entity) => new(
        entity.Id,
        entity.Title,
        entity.Body,
        Enum.Parse<ReleaseNoteStatus>(entity.Status),
        entity.Version,
        entity.SourceSha,
        entity.CreatedByStaffId,
        entity.CreatedAtUtc,
        entity.UpdatedAtUtc,
        entity.PublishedByStaffId,
        entity.PublishedAtUtc,
        entity.RowVersion);
}
