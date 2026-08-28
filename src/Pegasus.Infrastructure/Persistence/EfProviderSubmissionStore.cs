using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// SQL-backed <see cref="IProviderSubmissionStore"/> and the
/// <see cref="IProviderSubmissionBindings"/> the intake processor reads. The
/// unique (PrincipalId, IdempotencyKey) index is the concurrency boundary:
/// the loser of a same-key race is told so and re-reads, never overwrites.
/// </summary>
internal sealed class EfProviderSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IProviderSubmissionStore, IProviderSubmissionBindings
{
    public async Task CreateAsync(ProviderSubmissionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.ProviderSubmissions.Add(new ProviderSubmissionEntity
        {
            Id = record.Id,
            PrincipalId = record.PrincipalId,
            KeyId = record.KeyId,
            IdempotencyKey = record.IdempotencyKey,
            ProviderReference = record.ProviderReference,
            ReceivedAtUtc = record.ReceivedAtUtc
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
        }
    }

    public async Task<ProviderSubmissionRecord?> FindByIdempotencyKeyAsync(
        Guid principalId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ProviderSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.PrincipalId == principalId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<ProviderSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ProviderSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<string?> FindPrincipalCodeAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        if (sourceIdentity.Channel != IntakeSourceChannel.ProviderApi)
        {
            return null;
        }

        // A member's token is the submission id, suffixed by ordinal past the
        // first (GroupedIntakeMemberToken); the parent candidates name the row.
        var ids = GroupedIntakeMemberToken.ParentTokenCandidates(sourceIdentity.ExternalReceiptToken)
            .Select(candidate => Guid.TryParseExact(candidate, "N", out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .ToArray();
        if (ids.Length == 0)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ProviderSubmissions
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => item.Principal.Code)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static ProviderSubmissionRecord ToRecord(ProviderSubmissionEntity entity) => new(
        entity.Id,
        entity.PrincipalId,
        entity.KeyId,
        entity.IdempotencyKey,
        entity.ProviderReference,
        entity.ReceivedAtUtc);
}
