using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// SQL-backed <see cref="IPrincipalSubmissionStore"/> and the
/// <see cref="IPrincipalSubmissionBindings"/> the intake processor reads. The
/// unique (PrincipalId, IdempotencyKey) index is the concurrency boundary:
/// the loser of a same-key race is told so and re-reads, never overwrites.
/// </summary>
internal sealed class EfPrincipalSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IPrincipalSubmissionStore, IPrincipalSubmissionBindings
{
    // The code the durable intake store writes into
    // IntakeStagedReceipts.SourceChannel for this channel. Its map is private
    // to that store and the accept path deliberately leaves it untouched, so
    // the agreement is held by the two SQL-level accept-recovery tests, which
    // find no candidate at all if these ever disagree.
    internal const string PrincipalApiSourceChannel = "principal_api";

    public async Task CreateAsync(PrincipalSubmissionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.PrincipalSubmissions.Add(new PrincipalSubmissionEntity
        {
            Id = record.Id,
            PrincipalId = record.PrincipalId,
            KeyId = record.KeyId,
            IdempotencyKey = record.IdempotencyKey,
            BodySha256 = record.BodySha256,
            PrincipalReference = record.PrincipalReference,
            ReceivedAtUtc = record.ReceivedAtUtc,
            DeclaredInstructionJson = PrincipalInstructionJson.Serialize(
                record.Instruction
                    ?? throw new ArgumentException(
                        "A Principal submission carries the instruction its Principal declared.",
                        nameof(record)))
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new PrincipalSubmissionException(PrincipalSubmissionError.OperationConflict);
        }
    }

    public async Task<PrincipalSubmissionRecord?> FindByIdempotencyKeyAsync(
        Guid principalId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.PrincipalSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.PrincipalId == principalId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PrincipalSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.PrincipalSubmissions
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
        var entity = await context.PrincipalSubmissions
            .SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (entity is null || entity.StagedReceiptId == stagedReceiptId)
        {
            return;
        }

        entity.StagedReceiptId = stagedReceiptId;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrincipalSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
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

    public async Task<PrincipalSubmissionBinding?> FindAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        if (sourceIdentity.Channel != IntakeSourceChannel.PrincipalApi
            || !Guid.TryParseExact(sourceIdentity.ExternalReceiptToken, "N", out var id))
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.PrincipalSubmissions
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

        return new(row.Id, row.PrincipalId, row.PrincipalCode, ReadDeclaration(row.Id, row.DeclaredInstructionJson));
    }

    /// <summary>
    /// The declaration a retained submission stored: one read for every caller
    /// that recovers it, the binding intake resolves and the declared verdict
    /// Case acceptance keeps.
    /// </summary>
    internal static PrincipalInstruction ReadDeclaration(Guid submissionId, string? declaredInstructionJson) =>
        PrincipalInstructionJson.Deserialize(declaredInstructionJson)
            ?? throw new InvalidDataException(
                $"The retained Principal submission '{submissionId:D}' has no readable declaration.");

    private static PrincipalSubmissionRecord ToRecord(PrincipalSubmissionEntity entity) => new(
        entity.Id,
        entity.PrincipalId,
        entity.KeyId,
        entity.IdempotencyKey,
        entity.PrincipalReference,
        entity.ReceivedAtUtc,
        entity.BodySha256,
        PrincipalInstructionJson.Deserialize(entity.DeclaredInstructionJson),
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
    private static IQueryable<PrincipalSubmissionAcceptCandidate> AcceptRecoveryStates(
        PegasusDbContext context) =>
        from submission in context.PrincipalSubmissions.AsNoTracking()
        join staged in context.IntakeStagedReceipts
                .AsNoTracking()
                .Where(item => item.SourceChannel == PrincipalApiSourceChannel)
            // The token the accept path writes is the submission id in "N"
            // form, matched under the database's own collation exactly as the
            // history join below matches the "D" form.
            on submission.Id.ToString().Replace("-", string.Empty)
                equals staged.ExternalReceiptToken
        join acceptedHistory in context.ActionHistory
                .AsNoTracking()
                .Where(item =>
                    item.AggregateType == PrincipalSubmissionPolicy.ActionHistoryAggregateType
                    && item.Outcome == "Accepted")
            on submission.Id.ToString() equals acceptedHistory.AggregateId into acceptedHistories
        from acceptedHistory in acceptedHistories.DefaultIfEmpty()
        where submission.StagedReceiptId == null || acceptedHistory == null
        orderby submission.ReceivedAtUtc, submission.Id
        select new PrincipalSubmissionAcceptCandidate(
            submission.Id,
            submission.PrincipalId,
            submission.ReceivedAtUtc,
            submission.StagedReceiptId,
            staged.Id,
            acceptedHistory != null);
}
