using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfAdministrationAiJobQueries(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IAdministrationAiJobQueries
{
    public async Task<IReadOnlyList<AiJobRecord>> ListAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 51)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var rows = await context.AiJobs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.JobId)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(x => EfAiJobStore.Map(x, now)).ToArray();
    }
}
