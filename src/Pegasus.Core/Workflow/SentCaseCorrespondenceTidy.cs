using System.Collections.Concurrent;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Workflow;

/// <summary>
/// A confirmed send that carried the Delete case correspondence tick and whose
/// Case's received e-mails have not yet been tidied (ADR-0071, R902).
/// <paramref name="PreparedAtUtc"/> is the cut-off: only mail Pegasus held
/// when the send was prepared is moved.
/// </summary>
public sealed record SentCaseCorrespondenceCandidate(
    Guid OperationId,
    long OperationVersion,
    Guid CaseId,
    DateTimeOffset PreparedAtUtc,
    int Attempts);

/// <summary>
/// One received e-mail of the Case, or the original a staff forward among them
/// was made from (R917), in the approved mailbox it sits in.
/// </summary>
public sealed record CaseCorrespondenceMessage(
    Guid RetainedMessageId,
    string MailboxIdentity,
    string ImmutableMessageId);

/// <summary>
/// The original of a staff forward among the Case's received e-mails that
/// Pegasus never retained, because the mailbox that forwarded it is not read
/// for Incoming mail or the original arrived before it was. It is named by
/// the forwarding mailbox and the Message-ID the forward answers, and is
/// found in that mailbox itself, so the original goes with its forward
/// whether or not the mailbox's Inbox is read (operator, 11 November 2026;
/// R975). <paramref name="SentFolderIdentity"/> is the mailbox's Sent Items
/// folder, whose copy is never touched (R903).
/// </summary>
public sealed record UnretainedForwardOriginal(
    string MailboxIdentity,
    string? SentFolderIdentity,
    string InternetMessageId);

/// <summary>One message of a mailbox and the folder it sits in now.</summary>
public sealed record MailboxMessageLocation(
    string ImmutableMessageId,
    string ParentFolderId);

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
    /// retained mail in an Approved, activated mailbox (R916), retained at or
    /// before <paramref name="retainedAtOrBeforeUtc"/>. Pegasus's own Sent
    /// items and uploaded files are never among them (R903). With a
    /// Collision Engineers staff forward held in instructions@ comes the
    /// original its reply headers name in the mailbox that forwarded it,
    /// linked to the Case or not, unless it holds another Case (R917).
    /// </summary>
    Task<IReadOnlyList<CaseCorrespondenceMessage>> ListMessagesAsync(
        Guid caseId,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// The originals of the staff forwards among those e-mails that Pegasus
    /// holds no Inbox copy of in the mailbox that forwarded them (R975): one
    /// for each forward, the first message its reply headers name (R957), in
    /// an Approved, activated mailbox (R916). An original Pegasus retained is
    /// never among them: it is <see cref="ListMessagesAsync"/>'s, which
    /// leaves one that holds another Case (R958).
    /// </summary>
    Task<IReadOnlyList<UnretainedForwardOriginal>> ListUnretainedForwardOriginalsAsync(
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
/// recoverably (ADR-0071, R902). It never deletes permanently, never touches a
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
        var originals = await store.ListUnretainedForwardOriginalsAsync(
            candidate.CaseId, candidate.PreparedAtUtc, cancellationToken);
        var moved = 0;
        var alreadyMoved = 0;
        var missing = 0;
        var failed = 0;
        var notPermitted = false;
        void Tally(MoveOutcome outcome)
        {
            switch (outcome)
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

        // One message at a time: a send's messages share a mailbox more often than not.
        foreach (var message in messages)
        {
            Tally(await MoveOneAsync(message, deletedItemsByMailbox, cancellationToken));
        }

        // Then each forward's original Pegasus never retained, found in the
        // mailbox that forwarded it (R975).
        foreach (var original in originals)
        {
            foreach (var outcome in await MoveOriginalAsync(original, deletedItemsByMailbox, cancellationToken))
            {
                Tally(outcome);
            }
        }

        var failureCode = failed == 0
            ? null
            : notPermitted ? MailboxNotPermittedFailureCode : MoveFailedFailureCode;
        return (new(moved, alreadyMoved, missing, failed), failureCode);
    }

    /// <summary>
    /// Moves a forward's unretained original: every message of the forwarding
    /// mailbox that carries the Message-ID the forward answers, wherever it
    /// is filed, except the mailbox's own Sent Items copy (R903). None found
    /// counts as one message no longer in the mailbox.
    /// </summary>
    private async Task<IReadOnlyList<MoveOutcome>> MoveOriginalAsync(
        UnretainedForwardOriginal original,
        ConcurrentDictionary<string, Lazy<Task<string>>> deletedItemsByMailbox,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<MoveOutcome>();
        try
        {
            var found = await mover.FindMessagesAsync(
                original.MailboxIdentity,
                original.InternetMessageId,
                cancellationToken);
            var received = found
                .Where(message => !string.Equals(message.ParentFolderId, original.SentFolderIdentity, StringComparison.Ordinal))
                .ToArray();
            if (received.Length == 0)
            {
                return [MoveOutcome.Missing];
            }

            var deleted = await DeletedItemsAsync(original.MailboxIdentity, deletedItemsByMailbox, cancellationToken);
            foreach (var message in received)
            {
                if (string.Equals(message.ParentFolderId, deleted, StringComparison.Ordinal))
                {
                    outcomes.Add(MoveOutcome.AlreadyMoved);
                    continue;
                }

                await mover.MoveAsync(
                    new RetainedMessageMove(
                        original.MailboxIdentity,
                        message.ParentFolderId,
                        message.ImmutableMessageId,
                        deleted),
                    cancellationToken);
                outcomes.Add(MoveOutcome.Moved);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            and not ApprovedSentSourceThrottledException)
        {
            outcomes.Add(Refused(exception));
        }

        return outcomes;
    }

    // One lookup per mailbox per run, shared by sends in flight together; a
    // failed lookup is shared too, and each message counts its own failure.
    private Task<string> DeletedItemsAsync(
        string mailboxIdentity,
        ConcurrentDictionary<string, Lazy<Task<string>>> deletedItemsByMailbox,
        CancellationToken cancellationToken) =>
        deletedItemsByMailbox.GetOrAdd(
            mailboxIdentity,
            mailbox => new Lazy<Task<string>>(
                () => mover.ResolveDeletedItemsFolderIdAsync(mailbox, cancellationToken))).Value;

    private static MoveOutcome Refused(Exception exception) =>
        exception is ApprovedMailboxAccessDeniedException or UnauthorizedAccessException
            ? MoveOutcome.NotPermitted
            : MoveOutcome.Failed;

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

            var deleted = await DeletedItemsAsync(message.MailboxIdentity, deletedItemsByMailbox, cancellationToken);

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
            return Refused(exception);
        }
    }
}
