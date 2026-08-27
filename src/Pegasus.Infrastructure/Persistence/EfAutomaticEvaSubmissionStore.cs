using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Eva;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Finds the cases that should be sent to EVA automatically and queues them
/// (EXT-04).
///
/// Shaped like the automatic vehicle-lookup sweep in
/// <see cref="EfVehicleWorkflowStore"/>: read candidates, drop the ones
/// already marked, insert one durable row each, and let a duplicate key mean
/// "someone else already did this" rather than an error.
/// </summary>
public sealed class EfAutomaticEvaSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IAutomaticEvaSubmissionStore
{
    public async Task<int> EnqueueDueAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var reviewState = nameof(CaseLifecycleState.Review);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Cases in Review whose principal has automatic submission on. The
        // principal is joined through the case rather than read per case: the
        // setting lives on Principals and a per-case lookup would be one query
        // per candidate on every sweep.
        var candidates = await context.CaseWorkflows
            .AsNoTracking()
            .Where(workflow => workflow.State == reviewState && workflow.ArchivedAtUtc == null)
            .Join(
                context.Cases.AsNoTracking(),
                workflow => workflow.CaseId,
                caseEntity => caseEntity.Id,
                (workflow, caseEntity) => new { workflow.CaseId, caseEntity.PrincipalId })
            .Join(
                context.Principals.AsNoTracking().Where(principal => principal.EvaAutomaticSubmission),
                candidate => candidate.PrincipalId,
                principal => principal.Id,
                (candidate, _) => candidate.CaseId)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return 0;
        }

        // Two markers, both durable, and a case is skipped if either exists.
        // The work row covers "queued, running, or already given up on"; the
        // submission row covers "already reached EVA", which matters because a
        // manual send may have happened between sweeps.
        var queued = await context.ExternalWorkItems
            .AsNoTracking()
            .Where(item => item.Kind == ExternalWorkKinds.SubmitCaseToEva
                && item.CaseId != null
                && candidates.Contains(item.CaseId.Value))
            .Select(item => item.CaseId!.Value)
            .ToListAsync(cancellationToken);
        var submitted = await context.EvaSubmissions
            .AsNoTracking()
            .Where(item => item.IsSucceeded && candidates.Contains(item.CaseId))
            .Select(item => item.CaseId)
            .ToListAsync(cancellationToken);

        var skip = queued.Concat(submitted).ToHashSet();
        var due = candidates
            .Distinct()
            .Where(caseId => !skip.Contains(caseId))
            .Take(maximumItems)
            .ToArray();

        var nowUtc = timeProvider.GetUtcNow();
        var enqueued = 0;
        foreach (var caseId in due)
        {
            context.ExternalWorkItems.Add(new()
            {
                Id = Guid.CreateVersion7(),
                CaseId = caseId,
                Kind = ExternalWorkKinds.SubmitCaseToEva,
                // Stable per case, so a second sweep racing this one produces
                // the same operation key and the submission's own replay guard
                // recognises it rather than sending twice.
                OperationKey = OperationKey(caseId),
                State = "pending",
                AttemptCount = 0,
                DueAtUtc = nowUtc
            });
            enqueued++;
        }

        if (enqueued == 0)
        {
            return 0;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (enqueued > 0)
        {
            // A concurrent sweep inserted the same rows first. Nothing was
            // lost — the work exists — so this pass reports none rather than
            // failing. Any other database failure, a denied permission above
            // all, is not caught here and fails the sweep visibly.
            return 0;
        }

        return enqueued;
    }

    /// <summary>
    /// The operation key an automatic submission runs under. Derived from the
    /// case so it is the same on every sweep: the submission store's
    /// action-history replay check keys on it, which is what stops two sweeps
    /// from each sending the same case.
    /// </summary>
    private static string OperationKey(Guid caseId) => caseId.ToString("N");
}
