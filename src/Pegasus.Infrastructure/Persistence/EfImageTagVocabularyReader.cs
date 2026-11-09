using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The shared image-tag vocabulary, built-in entries first and then by name.
/// </summary>
/// <remarks>
/// Read-only and content-free, so it is composed for every profile that has a
/// database rather than only for those with durable document storage: the Case
/// workspace draws the picker wherever it draws a Case. Writing a tag is the
/// custody store's, because that is where the case, its lease and its version
/// are guarded. The list is kept in <see cref="IMemoryCache"/> until that
/// store's tag create evicts it (#1950).
/// </remarks>
internal sealed class EfImageTagVocabularyReader(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IMemoryCache cache) : IReadImageTagVocabulary
{
    // A private key object, so no other IMemoryCache user can collide with the entry.
    private static readonly object VocabularyKey = new();

    /// <summary>Drops the kept vocabulary, so a read begun before this call is never kept.</summary>
    internal static void EvictVocabulary(IMemoryCache memoryCache) =>
        EvictableCacheEntry.Evict(memoryCache, VocabularyKey);

    public Task<IReadOnlyList<ImageTag>> ListAsync(CancellationToken cancellationToken = default) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache, VocabularyKey, StaffSessionPolicy.AccountReadLifetime, slidingWindow: null, ReadAsync, cancellationToken);

    private async Task<IReadOnlyList<ImageTag>> ReadAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<ImageTagEntity>()
            .AsNoTracking()
            .OrderByDescending(value => value.IsBuiltIn)
            .ThenBy(value => value.Name)
            .ToArrayAsync(cancellationToken);
        return [.. rows.Select(ToImageTag)];
    }

    internal static ImageTag ToImageTag(ImageTagEntity value) => new(
        value.Id,
        value.Name,
        Enum.Parse<ImageTagColour>(value.Colour),
        value.IsBuiltIn,
        value.Version);
}
