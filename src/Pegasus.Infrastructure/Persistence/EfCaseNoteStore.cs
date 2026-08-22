using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// An operator note is a case history row like any other, so it lands on the
/// same timeline as the system's own entries and inherits their ordering,
/// attribution and append-only guarantees without a second store to keep in
/// step (CASE-017).
/// </summary>
internal sealed class EfCaseNoteStore(IDbContextFactory<PegasusDbContext> contextFactory)
    : ICaseNoteStore
{
    public async Task AddAsync(
        AddCaseNoteRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");

        // Replay protection is the operation key, as everywhere else: a resubmitted
        // form must not leave the case wearing the same note twice.
        var replayed = await context.Set<CaseHistoryEntity>()
            .AsNoTracking()
            .AnyAsync(
                item => item.CaseId == request.CaseId
                    && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replayed)
        {
            return;
        }

        context.Set<CaseHistoryEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            EventType = AddCaseNote.EventType,
            Actor = request.Actor.SubjectId,
            Reason = request.Note,
            OccurredAtUtc = occurredAtUtc,
            OperationKey = request.OperationKey,
            BeforeVersion = workflow.Version,
            AfterVersion = workflow.Version
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
