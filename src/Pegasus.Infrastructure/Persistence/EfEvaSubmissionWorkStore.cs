using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The durable row behind one automatic EVA submission (EXT-04).
///
/// Deliberately the same shape as <see cref="EfVehicleLookupWorkStore"/>: the
/// same <c>ExternalWorkItems</c> table, the same lease token, the same
/// optimistic claim, the same state words. Two background queues that behave
/// differently would be two things for an operator to learn on the Operations
/// page, and there is no reason for them to differ.
///
/// It carries no payload table of its own. A vehicle lookup persists the
/// registration it was queued for, because the case's registration may change
/// afterwards and the lookup must remain the one that was asked for. An EVA
/// submission has no such payload: the case at submission time *is* the
/// payload, and reading it fresh is the point.
/// </summary>
public sealed class EfEvaSubmissionWorkStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IEvaSubmissionWorkStore
{
    public async Task<EvaSubmissionWorkItem?> ClaimProcessingAsync(
        Guid workItemId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An EVA submission work item identifier is required.",
                nameof(workItemId));
        }
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var leaseToken = Guid.NewGuid().ToString("N");
        while (true)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
                ?? throw new InvalidOperationException("The EVA submission work item is unavailable.");
            if (!string.Equals(work.Kind, ExternalWorkKinds.SubmitCaseToEva, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The external work item is not an EVA submission.");
            }
            var state = ExternalWorkStatePersistence.ParseEvaSubmission(
                work.State,
                work.AttemptCount);
            if (state is EvaSubmissionWorkState.Completed or EvaSubmissionWorkState.Failed)
            {
                return null;
            }
            if (work.State == ExternalWorkStatePersistence.Pending && work.DueAtUtc > nowUtc)
            {
                return null;
            }
            if (state == EvaSubmissionWorkState.Processing && work.LeaseExpiresAtUtc > nowUtc)
            {
                throw new InvalidOperationException("The EVA submission work item is already leased.");
            }
            if (work.CaseId is not { } caseId)
            {
                throw new InvalidDataException("The EVA submission work item names no case.");
            }

            // Optimistic: the claim only lands if nothing about the row moved
            // between reading it and writing it, so two workers racing for the
            // same message cannot both believe they hold the lease.
            var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
            var claimed = await context.ExternalWorkItems
                .Where(item => item.Id == work.Id
                    && item.State == work.State
                    && item.AttemptCount == work.AttemptCount
                    && item.LeaseToken == work.LeaseToken
                    && item.LeaseExpiresAtUtc == work.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, ExternalWorkStatePersistence.Processing)
                    .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                    .SetProperty(item => item.LeaseToken, leaseToken)
                    .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(item => item.FailureCode, (string?)null)
                    .SetProperty(item => item.FailureReason, (string?)null),
                    cancellationToken);
            if (claimed == 1)
            {
                return new(
                    work.Id,
                    caseId,
                    work.OperationKey,
                    EvaSubmissionWorkState.Processing,
                    checked(work.AttemptCount + 1),
                    leaseToken);
            }
        }
    }

    public async Task RecordOutcomeAsync(
        Guid workItemId,
        string leaseToken,
        EvaSubmissionWorkState state,
        string? failureCode,
        string? failureReason,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An EVA submission work item identifier is required.",
                nameof(workItemId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
        if (state is not (
            EvaSubmissionWorkState.Completed or
            EvaSubmissionWorkState.Failed or
            EvaSubmissionWorkState.RetryScheduled))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        if ((state == EvaSubmissionWorkState.RetryScheduled) != dueAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Only retry-scheduled EVA submission work requires a due time.",
                nameof(dueAtUtc));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The EVA submission work item is unavailable.");
        if (!string.Equals(work.Kind, ExternalWorkKinds.SubmitCaseToEva, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The external work item is not an EVA submission.");
        }

        // Already terminal: a duplicate queue delivery arriving after the work
        // finished is not an error, and rewriting the row would replace a real
        // outcome with a stale one.
        if (work.State is ExternalWorkStatePersistence.Completed or ExternalWorkStatePersistence.Failed)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        // The lease is the authority to write this row. Losing it means
        // another worker took over — recording anyway would overwrite their
        // outcome with ours, and a swallowed conflict here is a submission
        // recorded against the wrong attempt.
        if (!string.Equals(
                work.State,
                ExternalWorkStatePersistence.Processing,
                StringComparison.Ordinal)
            || !string.Equals(work.LeaseToken, leaseToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The EVA submission processing lease was lost before its outcome was recorded.");
        }

        work.State = ExternalWorkStatePersistence.FormatEvaSubmission(state);
        work.FailureCode = failureCode;
        work.FailureReason = Truncate(failureReason);
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.DueAtUtc = dueAtUtc ?? work.DueAtUtc;
        work.CompletedAtUtc = state == EvaSubmissionWorkState.RetryScheduled
            ? null
            : recordedAtUtc;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// EVA's failure text can be an unbounded <c>text/plain</c> body, and the
    /// column it lands in is not.
    /// </summary>
    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= 400 ? value : value[..400];
    }
}
