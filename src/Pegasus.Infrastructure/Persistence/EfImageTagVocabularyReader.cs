using Microsoft.EntityFrameworkCore;
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
/// are guarded.
/// </remarks>
internal sealed class EfImageTagVocabularyReader(
    IDbContextFactory<PegasusDbContext> dbContextFactory) : IReadImageTagVocabulary
{
    public async Task<IReadOnlyList<ImageTag>> ListAsync(CancellationToken cancellationToken = default)
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
