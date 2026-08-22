using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// An operator note is a case workflow event like any other, so it lands on the
/// same timeline as the system's own entries and inherits their ordering,
/// attribution and append-only guarantees without a second store to keep in
/// step (CASE-017).
///
/// It must be <see cref="CaseWorkflowEventEntity"/> specifically: the Notes tab
/// reads `CaseWorkflowEvents` (`EfCaseQueryStore`), and the first version of
/// this store wrote to `CaseHistory` instead — a different table with a
/// different purpose. The note was persisted, the page reported success, and
/// the timeline stayed empty. Nothing failed loudly, which is why only running
/// the page found it.
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
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");

        // Replay protection is the operation key, as everywhere else: a resubmitted
        // form must not leave the case wearing the same note twice.
        var replayed = await context.CaseWorkflowEvents
            .AsNoTracking()
            .AnyAsync(
                item => item.CaseId == request.CaseId
                    && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replayed)
        {
            return;
        }

        // A note records itself and changes nothing about the case, so the
        // before and after versions are equal and the workflow row is untouched.
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Workflow = workflow,
            EventType = AddCaseNote.EventType,
            OperationKey = request.OperationKey,
            RequestHash = request.OperationKey,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
            Reason = request.Note,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = workflow.Version,
            AfterVersion = workflow.Version
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
