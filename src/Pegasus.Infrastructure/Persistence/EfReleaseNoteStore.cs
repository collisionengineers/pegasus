using Microsoft.EntityFrameworkCore;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfReleaseNoteStore(IDbContextFactory<PegasusDbContext> contextFactory) : IReleaseNoteStore
{
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
        var entity = new ReleaseNoteEntity
        {
            Id = Guid.NewGuid(),
            Title = note.Title,
            Body = note.Body,
            Status = nameof(ReleaseNoteStatus.Draft),
            CreatedByStaffId = note.CreatedByStaffId,
            CreatedAtUtc = note.AtUtc,
            UpdatedAtUtc = note.AtUtc,
            RowVersion = 1
        };
        context.Set<ReleaseNoteEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return Map(entity);
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
        ApplicationBuild build,
        Guid publishedByStaffId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await LoadDraftAsync(context, id, expectedRowVersion, cancellationToken);
        entity.Status = nameof(ReleaseNoteStatus.Published);
        entity.Version = build.Version;
        entity.SourceSha = build.SourceSha;
        entity.PublishedByStaffId = publishedByStaffId;
        entity.PublishedAtUtc = atUtc;
        entity.UpdatedAtUtc = atUtc;
        entity.RowVersion = expectedRowVersion + 1;
        await SaveAsync(context, id, cancellationToken);
        return Map(entity);
    }

    public async Task<ReleaseNote?> GetUnacknowledgedAsync(Guid staffId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var newest = await Published(context)
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (newest is null)
        {
            return null;
        }

        var acknowledged = await context.Set<ReleaseNoteAcknowledgementEntity>().AsNoTracking()
            .AnyAsync(item => item.StaffId == staffId && item.ReleaseNoteId == newest.Id, cancellationToken);
        return acknowledged ? null : Map(newest);
    }

    public async Task AcknowledgeAsync(Guid staffId, Guid noteId, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await context.Set<ReleaseNoteAcknowledgementEntity>()
            .AnyAsync(item => item.StaffId == staffId && item.ReleaseNoteId == noteId, cancellationToken);
        if (exists)
        {
            return;
        }

        var published = await Published(context).AnyAsync(item => item.Id == noteId, cancellationToken);
        if (!published)
        {
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
        catch (DbUpdateException)
        {
            // Two tabs acknowledged at once; the row is there, which is all that was asked.
        }
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
