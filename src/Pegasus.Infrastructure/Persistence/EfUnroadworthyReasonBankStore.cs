using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The unroadworthy reason bank (v28 P15): one list for every Case, oldest
/// first (operator, 11 November 2026; R952).
/// </summary>
public sealed class EfUnroadworthyReasonBankStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IMemoryCache cache) : IUnroadworthyReasonBankStore
{
    // The list is one entry, kept in IMemoryCache until a write evicts it (#1950).
    private static readonly object ReasonsKey = new();

    public Task<IReadOnlyList<UnroadworthyReason>> ListAsync(CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache,
            ReasonsKey,
            StaffSessionPolicy.AccountReadLifetime,
            slidingWindow: null,
            ReadAsync,
            cancellationToken);

    private async Task<IReadOnlyList<UnroadworthyReason>> ReadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.UnroadworthyReasons.AsNoTracking()
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<UnroadworthyReason?> AddAsync(
        SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new UnroadworthyReasonEntity
        {
            Id = Guid.NewGuid(),
            Text = normalized,
            CreatedBy = request.Actor.SubjectId,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };
        context.UnroadworthyReasons.Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            return null;
        }
        finally
        {
            // Whatever landed (this wording, or a concurrent add of the same
            // one), the kept list is dropped.
            EvictableCacheEntry.Evict(cache, ReasonsKey);
        }
        return Map(entity);
    }

    public async Task<UnroadworthyReasonChange> RewordAsync(
        Guid id, string renderedText, string normalized, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            // The rendered text is the version: the row changes only while it
            // still reads as the page showed it.
            var changed = await context.UnroadworthyReasons
                .Where(item => item.Id == id && item.Text == renderedText)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.Text, normalized),
                    cancellationToken);
            if (changed == 1)
            {
                return UnroadworthyReasonChange.Done;
            }

            // A repeated Save finds its own wording already in place.
            return await context.UnroadworthyReasons.AnyAsync(
                    item => item.Id == id && item.Text == normalized, cancellationToken)
                ? UnroadworthyReasonChange.Done
                : UnroadworthyReasonChange.Stale;
        }
        catch (Exception exception) when (IsDuplicate(exception))
        {
            return UnroadworthyReasonChange.AlreadyOffered;
        }
        finally
        {
            EvictableCacheEntry.Evict(cache, ReasonsKey);
        }
    }

    public async Task<UnroadworthyReasonChange> RemoveAsync(
        Guid id, string renderedText, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var removed = await context.UnroadworthyReasons
                .Where(item => item.Id == id && item.Text == renderedText)
                .ExecuteDeleteAsync(cancellationToken);
            return removed == 1 ? UnroadworthyReasonChange.Done : UnroadworthyReasonChange.Stale;
        }
        finally
        {
            EvictableCacheEntry.Evict(cache, ReasonsKey);
        }
    }

    private static bool IsDuplicate(Exception exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private static UnroadworthyReason Map(UnroadworthyReasonEntity entity) =>
        new(entity.Id, entity.Text, entity.CreatedBy, entity.CreatedAtUtc);
}
