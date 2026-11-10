using System.Collections.Concurrent;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Workflow;

/// <summary>
/// A confirmed send that carried the Delete case correspondence tick and whose
/// Case's received e-mails have not yet been tidied (ADR-0091).
/// <paramref name="PreparedAtUtc"/> is the cut-off: only mail Pegasus held
/// when the send was prepared is moved.
/// </summary>
public sealed record SentCaseCorrespondenceCandidate(
    Guid OperationId,
    long OperationVersion,
    Guid CaseId,
    DateTimeOffset PreparedAtUtc,
    int Attempts);

/// <summary>One received e-mail of the Case, in the approved mailbox it sits in.</summary>
public sealed record CaseCorrespondenceMessage(
    Guid RetainedMessageId,
    string MailboxIdentity,
    string ImmutableMessageId);

/// <summary>What one run did with one send's messages.</summary>
public sealed record SentCaseCorrespondenceCounts(
    int Moved,
    int AlreadyMoved,
    int Missing,
    int Failed);

public sealed record SentCaseCorrespondenceTidyResult(
    int ExaminedCount,
    int MovedCount,
    int AlreadyMovedCount,
    int MessageMissingCount,
    int FailedCount);

public interface ISentCaseCorrespondenceTidyStore
{
    /// <summary>
    /// Confirmed sends that carried the tick, whose correspondence has not
    /// been tidied and which have had fewer than
    /// <see cref="TidySentCaseCorrespondence.MaximumAttempts"/> failed
    /// attempts, oldest Sent item first.
    /// </summary>
    Task<IReadOnlyList<SentCaseCorrespondenceCandidate>> ListDueAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// The Case's received e-mails as they stand linked now: Inbox-scope
    /// retained mail in an approved mailbox, retained at or before
    /// <paramref name="retainedAtOrBeforeUtc"/>. Pegasus's own Sent items and
    /// uploaded files are never among them (R903).
    /// </summary>
    Task<IReadOnlyList<CaseCorrespondenceMessage>> ListMessagesAsync(
        Guid caseId,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one run under the operation's version guard. Returns false when
    /// the operation changed since it was listed. A run with a failed message
    /// counts one attempt and stays due until the bound is reached; a run with
    /// none ends the tidy.
    /// </summary>
    Task<bool> RecordAsync(
        Guid operationId,
        long expectedVersion,
        SentCaseCorrespondenceCounts counts,
        string? failureCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// After a send that carried the Delete case correspondence tick is confirmed
/// by its Sent item, moves the Case's received e-mails to Deleted Items,
/// recoverably (ADR-0091). It never deletes permanently, never touches a
/// message of another Case, and does nothing in a host that cannot reach the
/// mailbox.
/// </summary>
public sealed class TidySentCaseCorrespondence(
    ISentCaseCorrespondenceTidyStore store,
    IRetainedMessageMover mover,
    TimeProvider timeProvider)
{
    public const int MaximumAttempts = 3;
    public const string MailboxNotPermittedFailureCode = TidySentReportInstructions.MailboxNotPermittedFailureCode;
    public const string MoveFailedFailureCode = TidySentReportInstructions.MoveFailedFailureCode;

    // Outlook allows four concurrent requests per mailbox; two leaves the Worker headroom.
    private const int MaximumConcurrentCases = 2;

    private enum MoveOutcome
    {
        Moved,
        AlreadyMoved,
        Missing,
        NotPermitted,
        Failed
    }

    public async Task<SentCaseCorrespondenceTidyResult> ExecuteAsync(
        int maximumItems,
        ActionActor systemActor,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumItems, 1);
        ArgumentNullException.ThrowIfNull(systemActor);
        StaffAuthorization.Require(systemActor, StaffAccessRight.ExecuteSystemWork);
        if (systemActor.Kind != ActorKind.SystemWorker)
        {
            throw new UnauthorizedAccessException(
                "Tidying a sent mail's Case correspondence requires a system-worker actor.");
        }

        if (!mover.IsAvailable)
        {
            return new(0, 0, 0, 0, 0);
        }

        var candidates = await store.ListDueAsync(maximumItems, cancellationToken);
        var deletedItemsByMailbox = new ConcurrentDictionary<string, Lazy<Task<string>>>(StringComparer.Ordinal);
        var moved = 0;
        var alreadyMoved = 0;
        var missing = 0;
        var failed = 0;
        var examined = 0;
        var throttled = 0;
        // Two ticked sends on one Case name the same messages, so they run one after
        // another and the later send finds them already moved.
        await Parallel.ForEachAsync(
            candidates.GroupBy(candidate => candidate.CaseId),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentCases,
                CancellationToken = cancellationToken,
            },
            async (sends, token) =>
            {
                foreach (var candidate in sends)
                {
                    // A flag, not a cancellation, so sends already under way finish and record.
                    if (Volatile.Read(ref throttled) != 0)
                    {
                        return;
                    }

                    SentCaseCorrespondenceCounts counts;
                    string? failureCode;
                    try
                    {
                        (counts, failureCode) = await TidyOneSendAsync(
                            candidate, deletedItemsByMailbox, token);
                    }
                    catch (ApprovedSentSourceThrottledException)
                    {
                        // The mailbox asked us to wait: the send records no attempt and
                        // no further send is started. What it had moved is found already
                        // moved on the next run.
                        Volatile.Write(ref throttled, 1);
                        return;
                    }

                    Interlocked.Increment(ref examined);
                    await store.RecordAsync(
                        candidate.OperationId,
                        candidate.OperationVersion,
                        counts,
                        failureCode,
                        timeProvider.GetUtcNow(),
                        token);
                    Interlocked.Add(ref moved, counts.Moved);
                    Interlocked.Add(ref alreadyMoved, counts.AlreadyMoved);
                    Interlocked.Add(ref missing, counts.Missing);
                    Interlocked.Add(ref failed, counts.Failed);
                }
            });

        return new(examined, moved, alreadyMoved, missing, failed);
    }

    private async Task<(SentCaseCorrespondenceCounts Counts, string? FailureCode)> TidyOneSendAsync(
        SentCaseCorrespondenceCandidate candidate,
        ConcurrentDictionary<string, Lazy<Task<string>>> deletedItemsByMailbox,
        CancellationToken cancellationToken)
    {
        var messages = await store.ListMessagesAsync(
            candidate.CaseId, candidate.PreparedAtUtc, cancellationToken);
        var moved = 0;
        var alreadyMoved = 0;
        var missing = 0;
        var failed = 0;
        var notPermitted = false;
        // One message at a time: a send's messages share a mailbox more often than not.
        foreach (var message in messages)
        {
            switch (await MoveOneAsync(message, deletedItemsByMailbox, cancellationToken))
            {
                case MoveOutcome.Moved:
                    moved++;
                    break;
                case MoveOutcome.AlreadyMoved:
                    alreadyMoved++;
                    break;
                case MoveOutcome.Missing:
                    missing++;
                    break;
                case MoveOutcome.NotPermitted:
                    failed++;
                    notPermitted = true;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        var failureCode = failed == 0
            ? null
            : notPermitted ? MailboxNotPermittedFailureCode : MoveFailedFailureCode;
        return (new(moved, alreadyMoved, missing, failed), failureCode);
    }

    private async Task<MoveOutcome> MoveOneAsync(
        CaseCorrespondenceMessage message,
        ConcurrentDictionary<string, Lazy<Task<string>>> deletedItemsByMailbox,
        CancellationToken cancellationToken)
    {
        try
        {
            var parent = await mover.GetParentFolderIdAsync(
                message.MailboxIdentity,
                message.ImmutableMessageId,
                cancellationToken);
            if (parent is null)
            {
                return MoveOutcome.Missing;
            }

            // One lookup per mailbox per run, shared by sends in flight together; a
            // failed lookup is shared too, and each message counts its own failure.
            var deleted = await deletedItemsByMailbox.GetOrAdd(
                message.MailboxIdentity,
                mailbox => new Lazy<Task<string>>(
                    () => mover.ResolveDeletedItemsFolderIdAsync(mailbox, cancellationToken))).Value;

            if (string.Equals(parent, deleted, StringComparison.Ordinal))
            {
                return MoveOutcome.AlreadyMoved;
            }

            await mover.MoveAsync(
                new RetainedMessageMove(
                    message.MailboxIdentity,
                    parent,
                    message.ImmutableMessageId,
                    deleted),
                cancellationToken);
            return MoveOutcome.Moved;
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            and not ApprovedSentSourceThrottledException)
        {
            return exception is ApprovedMailboxAccessDeniedException or UnauthorizedAccessException
                ? MoveOutcome.NotPermitted
                : MoveOutcome.Failed;
        }
    }
}
