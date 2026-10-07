using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// SQL-backed <see cref="IPrincipalSubmissionStore"/> and the
/// <see cref="IPrincipalSubmissionBindings"/> the intake processor reads. The
/// submission id is derived from the Principal and the idempotency key, so the
/// primary key is the concurrency boundary: the loser of a same-key race reads
/// the winner's row, never overwrites it.
/// </summary>
internal sealed class EfPrincipalSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IPrincipalSubmissionStore, IPrincipalSubmissionBindings
{
    public async Task<PrincipalSubmissionRecord> GetOrCreateAsync(
        PrincipalSubmissionRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (await GetAsync(record.Id, cancellationToken) is { } existing)
        {
            return existing;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.PrincipalSubmissions.Add(new PrincipalSubmissionEntity
        {
            Id = record.Id,
            PrincipalId = record.PrincipalId,
            ReceivedAtUtc = record.ReceivedAtUtc,
            DeclaredInstructionJson = PrincipalInstructionJson.Serialize(record.Instruction)
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return record;
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request with the same key wrote the row first.
            return await GetAsync(record.Id, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"The Principal submission '{record.Id:D}' conflicted on insert and then could not be read.");
        }
    }

    public async Task<PrincipalSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.PrincipalSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null
            ? null
            : new(entity.Id, entity.PrincipalId, entity.ReceivedAtUtc, ReadDeclaration(entity.Id, entity.DeclaredInstructionJson));
    }

    public async Task<IReadOnlyList<PrincipalSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var channel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.PrincipalApi);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The staged-receipt join is an inner join on purpose: a submission
        // whose retention never happened is a bare reservation, which the sweep
        // cannot complete and nothing deletes, so admitting it to a bounded
        // oldest-first window would let one outage's worth of them occupy that
        // window for good.
        return await (
                from submission in context.PrincipalSubmissions.AsNoTracking()
                join staged in context.IntakeStagedReceipts
                        .AsNoTracking()
                        .Where(item => item.SourceChannel == channel)
                    // The token the accept path writes is the submission id in
                    // "N" form, matched under the database's own collation
                    // exactly as the history join below matches the "D" form.
                    on submission.Id.ToString().Replace("-", string.Empty)
                        equals staged.ExternalReceiptToken
                join acceptedHistory in context.ActionHistory
                        .AsNoTracking()
                        .Where(item =>
                            item.AggregateType == PrincipalSubmissionPolicy.ActionHistoryAggregateType
                            && item.Outcome == "Accepted")
                    on submission.Id.ToString() equals acceptedHistory.AggregateId into acceptedHistories
                from acceptedHistory in acceptedHistories.DefaultIfEmpty()
                where acceptedHistory == null
                orderby submission.ReceivedAtUtc, submission.Id
                select new PrincipalSubmissionAcceptCandidate(
                    submission.Id,
                    submission.PrincipalId,
                    submission.ReceivedAtUtc))
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken);
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
}
