using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The send journal's record of the instruction tidy after a confirmed report
/// send (ADR-0063). A row is due while its tidy state is unset and it has had
/// fewer than <see cref="TidySentReportInstructions.MaximumAttempts"/> attempts.
/// </summary>
internal sealed class EfSentReportInstructionTidyStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : ISentReportInstructionTidyStore
{
    private const string Actor = "sent-report-instruction-tidy";

    public async Task<IReadOnlyList<SentReportInstructionCandidate>> ListDueAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumItems, 1);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // A mailbox an Administrator has withdrawn is never written to.
        var approvedState = ApprovedMailboxState.Approved.ToString();
        var rows = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(operation => operation.Purpose == StaffMailPurpose.CaseReport
                && operation.State == StaffMailState.Sent
                && operation.OriginalRetainedMessageId != null
                && operation.InstructionMoveState == null
                && operation.InstructionMoveAttempts < TidySentReportInstructions.MaximumAttempts)
            .Join(
                db.RetainedMailboxMessages.AsNoTracking(),
                operation => operation.OriginalRetainedMessageId!.Value,
                message => message.Id,
                (operation, message) => new { operation, message })
            .Where(pair => pair.message.MailboxId != null)
            .Join(
                db.ApprovedMailboxes.AsNoTracking(),
                pair => pair.message.MailboxId!.Value,
                mailbox => mailbox.Id,
                (pair, mailbox) => new { pair.operation, pair.message, mailbox })
            .Where(trio => trio.mailbox.MailboxIdentity != null && trio.mailbox.State == approvedState)
            .Join(
                db.Set<CaseReportGenerationEntity>().AsNoTracking(),
                trio => trio.operation.ContextId,
                generation => generation.Id,
                (trio, generation) => new
                {
                    trio.operation.Id,
                    trio.operation.Version,
                    generation.CaseId,
                    RetainedMessageId = trio.message.Id,
                    MailboxIdentity = trio.mailbox.MailboxIdentity!,
                    trio.message.ImmutableMessageId,
                    trio.operation.InstructionMoveAttempts,
                    trio.operation.ObservedSentAtUtc
                })
            .OrderBy(row => row.ObservedSentAtUtc)
            .ThenBy(row => row.Id)
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken);
        return rows
            .Select(row => new SentReportInstructionCandidate(
                row.Id,
                row.Version,
                row.CaseId,
                row.RetainedMessageId,
                row.MailboxIdentity,
                row.ImmutableMessageId,
                row.InstructionMoveAttempts))
            .ToArray();
    }

    public async Task<bool> RecordAsync(
        Guid operationId,
        long expectedVersion,
        SentReportInstructionTidyOutcome outcome,
        string? failureCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        if (outcome == SentReportInstructionTidyOutcome.Failed
            && string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("A failed tidy needs a failure code.", nameof(failureCode));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Set<StaffMailSendOperationEntity>().SingleOrDefaultAsync(
            value => value.Id == operationId && value.Version == expectedVersion,
            cancellationToken);
        if (operation is null
            || operation.InstructionMoveState is not null
            || operation.InstructionMoveAttempts >= TidySentReportInstructions.MaximumAttempts)
        {
            return false;
        }

        operation.InstructionMoveAttempts++;
        operation.Version++;
        string? eventType = null;
        string reason = string.Empty;
        if (outcome == SentReportInstructionTidyOutcome.Failed)
        {
            operation.InstructionMoveFailureCode = failureCode!.Trim();
            if (operation.InstructionMoveAttempts >= TidySentReportInstructions.MaximumAttempts)
            {
                operation.InstructionMoveState = nameof(SentReportInstructionTidyOutcome.Failed);
            }
            eventType = "instruction_move_failed";
            reason = $"The answered instruction could not be moved to Deleted Items ({operation.InstructionMoveFailureCode}); attempt {operation.InstructionMoveAttempts} of {TidySentReportInstructions.MaximumAttempts}.";
        }
        else
        {
            operation.InstructionMoveState = outcome.ToString();
            operation.InstructionMoveFailureCode = null;
            if (outcome == SentReportInstructionTidyOutcome.Moved)
            {
                operation.InstructionMovedAtUtc = nowUtc;
            }
            // A message that is gone from the mailbox changed nothing on the Case, so it
            // is recorded on the send row alone.
            if (outcome != SentReportInstructionTidyOutcome.MessageMissing)
            {
                eventType = "instruction_moved_to_deleted_items";
                reason = outcome == SentReportInstructionTidyOutcome.Moved
                    ? "The answered instruction was moved to Deleted Items after the report was sent."
                    : "The answered instruction was already in Deleted Items after the report was sent.";
            }
        }

        var caseId = await db.Set<CaseReportGenerationEntity>().AsNoTracking()
            .Where(value => value.Id == operation.ContextId)
            .Select(value => (Guid?)value.CaseId)
            .SingleOrDefaultAsync(cancellationToken);
        if (eventType is not null && caseId is { } id)
        {
            db.CaseHistory.Add(new CaseHistoryEntity
            {
                Id = Guid.NewGuid(),
                CaseId = id,
                EventType = eventType,
                Actor = Actor,
                Reason = reason,
                OccurredAtUtc = nowUtc,
                OperationKey = $"instruction-tidy:{operation.Id:N}:{operation.InstructionMoveAttempts}",
                BeforeVersion = null,
                AfterVersion = 0
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }
}
