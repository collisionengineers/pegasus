using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
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
    private static readonly Lock PublishSignalGate = new();

    // Private key types, so no other IMemoryCache user can collide with an entry.
    private readonly record struct UnacknowledgedKey(Guid StaffId);

    private readonly record struct PublishSignalKey;

    public async Task<ReleaseNote?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ReleaseNoteEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ReleaseNote>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<ReleaseNoteEntity>().AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
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

    public async Task<ReleaseNote?> GetUnacknowledgedAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var key = new UnacknowledgedKey(staffId);
        if (cache.TryGetValue(key, out ReleaseNote? kept))
        {
            return kept;
        }

        // Taken before the read: a publish that lands during it cancels the
        // token, and an entry stored with a cancelled token is already expired.
        var publishes = TakePublishSignal();
        var note = await ReadUnacknowledgedAsync(staffId, cancellationToken);
        cache.Set(
            key,
            note,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = UnacknowledgedSlidingWindow,
                AbsoluteExpirationRelativeToNow = UnacknowledgedCeiling
            }.AddExpirationToken(new CancellationChangeToken(publishes)));
        return note;
    }

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

    private void ForgetUnacknowledged(Guid staffId) => cache.Remove(new UnacknowledgedKey(staffId));

    // Entries cannot be listed, so each is stored with the token of the one
    // signal a publish cancels.
    private CancellationToken TakePublishSignal()
    {
        lock (PublishSignalGate)
        {
            if (cache.Get(default(PublishSignalKey)) is not CancellationTokenSource signal)
            {
                signal = new CancellationTokenSource();
                cache.Set(
                    default(PublishSignalKey),
                    signal,
                    new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            }

            return signal.Token;
        }
    }

    private void ExpireEveryUnacknowledged()
    {
        lock (PublishSignalGate)
        {
            if (cache.Get(default(PublishSignalKey)) is CancellationTokenSource signal)
            {
                cache.Remove(default(PublishSignalKey));
                signal.Cancel();
            }
        }
    }

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
