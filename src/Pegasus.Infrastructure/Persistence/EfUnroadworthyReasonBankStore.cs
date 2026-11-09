using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>The firm's own unroadworthy reason wordings (v28 P15), oldest first.</summary>
public sealed class EfUnroadworthyReasonBankStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IMemoryCache cache) : IUnroadworthyReasonBankStore
{
    // One Principal's list is one entry, kept in IMemoryCache until an add
    // evicts the family (#1950). SQL matches the Principal code without regard
    // to case and a key does, so the family, not the key, is what a write drops.
    private readonly record struct ReasonsKey(string PrincipalCode);

    private static readonly object ReasonsFamily = new();

    public Task<IReadOnlyList<UnroadworthyReason>> ListAsync(
        string principalCode, CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache,
            new ReasonsKey(principalCode),
            ReasonsFamily,
            StaffSessionPolicy.AccountReadLifetime,
            slidingWindow: null,
            token => ReadAsync(principalCode, token),
            cancellationToken);

    private async Task<IReadOnlyList<UnroadworthyReason>> ReadAsync(
        string principalCode, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.UnroadworthyReasons.AsNoTracking()
            .Where(item => item.PrincipalCode == principalCode)
            .OrderBy(item => item.CreatedAtUtc)
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
            PrincipalCode = request.PrincipalCode,
            Text = normalized,
            CreatedBy = request.Actor.SubjectId,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };
        context.UnroadworthyReasons.Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return null;
        }
        finally
        {
            // Whatever landed (this wording, or a concurrent add of the same
            // one), the kept lists are dropped.
            EvictableCacheEntry.EvictFamily(cache, ReasonsFamily);
        }
        return Map(entity);
    }

    private static UnroadworthyReason Map(UnroadworthyReasonEntity entity) =>
        new(entity.Id, entity.PrincipalCode, entity.Text, entity.CreatedBy, entity.CreatedAtUtc);
}
