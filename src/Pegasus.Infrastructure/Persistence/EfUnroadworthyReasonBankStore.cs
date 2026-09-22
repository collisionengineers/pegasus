using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>The firm's own unroadworthy reason wordings (v28 P15), oldest first.</summary>
public sealed class EfUnroadworthyReasonBankStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IUnroadworthyReasonBankStore
{
    public async Task<IReadOnlyList<UnroadworthyReason>> ListAsync(
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
        return Map(entity);
    }

    private static UnroadworthyReason Map(UnroadworthyReasonEntity entity) =>
        new(entity.Id, entity.PrincipalCode, entity.Text, entity.CreatedBy, entity.CreatedAtUtc);
}
