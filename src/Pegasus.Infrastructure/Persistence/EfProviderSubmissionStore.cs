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
    // The same map that wrote the column, rather than a second copy of it.
    private static readonly string ProviderApiSourceChannel =
        EfIntakeWorkStore.ToCode(IntakeSourceChannel.ProviderApi);

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
            ReceivedAtUtc = record.ReceivedAtUtc,
            DeclaredInstructionJson = ProviderInstructionJson.Serialize(
                record.Instruction
                    ?? throw new ArgumentException(
                        "A provider submission carries the instruction its Principal declared.",
                        nameof(record)))
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

    public async Task RecordStagedReceiptAsync(
        Guid submissionId,
        Guid stagedReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ProviderSubmissions
            .SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (entity is null || entity.StagedReceiptId == stagedReceiptId)
        {
            return;
        }

        entity.StagedReceiptId = stagedReceiptId;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await AcceptRecoveryStates(context)
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<string?> FindPrincipalCodeAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Principals
            .AsNoTracking()
            .Where(item => item.Id == principalId && item.IsActive)
            .Select(item => item.Code)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProviderSubmissionBinding?> FindAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        if (sourceIdentity.Channel != IntakeSourceChannel.ProviderApi
            || !Guid.TryParseExact(sourceIdentity.ExternalReceiptToken, "N", out var id))
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.ProviderSubmissions
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.PrincipalId,
                PrincipalCode = item.Principal.Code,
                item.DeclaredInstructionJson
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var instruction = ProviderInstructionJson.Deserialize(row.DeclaredInstructionJson)
            ?? throw new InvalidDataException(
                $"The retained provider submission '{row.Id:D}' has no readable declaration.");
        return new(row.Id, row.PrincipalId, row.PrincipalCode, instruction);
    }

    private static ProviderSubmissionRecord ToRecord(ProviderSubmissionEntity entity) => new(
        entity.Id,
        entity.PrincipalId,
        entity.KeyId,
        entity.IdempotencyKey,
        entity.ProviderReference,
        entity.ReceivedAtUtc,
        ProviderInstructionJson.Deserialize(entity.DeclaredInstructionJson),
        entity.StagedReceiptId);

    /// <summary>
    /// The accept state of a submission, read in one statement: the staged
    /// receipt its source was retained as, and whether its first
    /// <c>Accepted</c> history row exists.
    /// </summary>
    /// <remarks>
    /// The staged-receipt join is an inner join on purpose. A submission whose
    /// retention never happened is a bare reservation: the sweep cannot
    /// complete it, and nothing deletes it, so admitting it to a bounded
    /// oldest-first window would let one outage's worth of them occupy that
    /// window for good. Joining here also answers "which receipt?" in the same
    /// read, instead of one lookup per candidate.
    /// </remarks>
    private static IQueryable<ProviderSubmissionAcceptCandidate> AcceptRecoveryStates(
        PegasusDbContext context) =>
        from submission in context.ProviderSubmissions.AsNoTracking()
        join staged in context.IntakeStagedReceipts
                .AsNoTracking()
                .Where(item => item.SourceChannel == ProviderApiSourceChannel)
            // The token the accept path writes is the submission id in "N"
            // form, matched under the database's own collation exactly as the
            // history join below matches the "D" form.
            on submission.Id.ToString().Replace("-", string.Empty)
                equals staged.ExternalReceiptToken
        join acceptedHistory in context.ActionHistory
                .AsNoTracking()
                .Where(item =>
                    item.AggregateType == ProviderSubmissionPolicy.ActionHistoryAggregateType
                    && item.Outcome == "Accepted")
            on submission.Id.ToString() equals acceptedHistory.AggregateId into acceptedHistories
        from acceptedHistory in acceptedHistories.DefaultIfEmpty()
        where submission.StagedReceiptId == null || acceptedHistory == null
        orderby submission.ReceivedAtUtc, submission.Id
        select new ProviderSubmissionAcceptCandidate(
            submission.Id,
            submission.PrincipalId,
            submission.ReceivedAtUtc,
            submission.StagedReceiptId,
            staged.Id,
            acceptedHistory != null);
}
