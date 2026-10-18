using System.Collections.Concurrent;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Workflow;

/// <summary>
/// A Report sent whose instruction e-mail has not yet been tidied: the answered
/// instruction still sits where Pegasus found it (ADR-0071).
/// </summary>
public sealed record SentReportInstructionCandidate(
    Guid OperationId,
    long OperationVersion,
    Guid CaseId,
    Guid RetainedMessageId,
    string MailboxIdentity,
    string ImmutableMessageId,
    int Attempts);

public enum SentReportInstructionTidyOutcome
{
    Moved,
    AlreadyMoved,
    MessageMissing,
    Failed
}

public sealed record SentReportInstructionTidyResult(
    int ExaminedCount,
    int MovedCount,
    int AlreadyMovedCount,
    int MessageMissingCount,
    int FailedCount);

public interface ISentReportInstructionTidyStore
{
    /// <summary>
    /// Confirmed report sends that answered a retained instruction, whose
    /// instruction has not been tidied and has had fewer than
    /// <see cref="TidySentReportInstructions.MaximumAttempts"/> failed attempts,
    /// oldest Sent item first.
    /// </summary>
    Task<IReadOnlyList<SentReportInstructionCandidate>> ListDueAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one attempt under the operation's version guard. Returns false
    /// when the operation changed since it was listed. A failure counts one
    /// attempt and stays due until the bound is reached.
    /// </summary>
    Task<bool> RecordAsync(
        Guid operationId,
        long expectedVersion,
        SentReportInstructionTidyOutcome outcome,
        string? failureCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>One Outlook move of an answered instruction: from its folder to Deleted Items.</summary>
public sealed record SentReportInstructionMove(
    string MailboxIdentity,
    string SourceFolderId,
    string ImmutableMessageId,
    string DestinationFolderId);

/// <summary>
/// The mailbox side of the tidy (ADR-0071). Only the Worker composes the Graph
/// mover; every other host has <see cref="UnavailableSentReportInstructionMover"/>.
/// </summary>
public interface ISentReportInstructionMover
{
    bool IsAvailable { get; }

    /// <summary>The message's current folder, or null when it is no longer in the mailbox.</summary>
    Task<string?> GetParentFolderIdAsync(
        string mailboxIdentity,
        string immutableMessageId,
        CancellationToken cancellationToken);

    /// <summary>The mailbox's Deleted Items folder identity.</summary>
    Task<string> ResolveDeletedItemsFolderIdAsync(
        string mailboxIdentity,
        CancellationToken cancellationToken);

    Task MoveAsync(
        SentReportInstructionMove move,
        CancellationToken cancellationToken);
}

public sealed class UnavailableSentReportInstructionMover : ISentReportInstructionMover
{
    public bool IsAvailable => false;

    public Task<string?> GetParentFolderIdAsync(string mailboxIdentity, string immutableMessageId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string> ResolveDeletedItemsFolderIdAsync(string mailboxIdentity, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Outlook moves are unavailable in this runtime.");

    public Task MoveAsync(SentReportInstructionMove move, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Outlook moves are unavailable in this runtime.");
}

/// <summary>
/// After a report send is confirmed by its Sent item, moves the answered
/// instruction e-mail to Deleted Items, once, recoverably (ADR-0071). It never
/// deletes permanently, never touches an unrelated message, and does nothing in
/// a host that cannot reach the mailbox.
/// </summary>
public sealed class TidySentReportInstructions(
    ISentReportInstructionTidyStore store,
    ISentReportInstructionMover mover,
    TimeProvider timeProvider)
{
    public const int MaximumAttempts = 3;
    public const string MailboxNotPermittedFailureCode = "mailbox_not_permitted";
    public const string MoveFailedFailureCode = "move_failed";

    // Outlook allows four concurrent requests per mailbox; two leaves the Worker headroom.
    private const int MaximumConcurrentMoves = 2;

    public async Task<SentReportInstructionTidyResult> ExecuteAsync(
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
                "Tidying a sent report's instruction requires a system-worker actor.");
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
        // Sends that answered one retained instruction run one after another, so the
        // later send finds the instruction already moved.
        await Parallel.ForEachAsync(
            candidates.GroupBy(candidate => candidate.RetainedMessageId),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentMoves,
                CancellationToken = cancellationToken,
            },
            async (sends, token) =>
            {
                foreach (var candidate in sends)
                {
                    // A flag, not a cancellation, so moves already under way finish and record.
                    if (Volatile.Read(ref throttled) != 0)
                    {
                        return;
                    }

                    SentReportInstructionTidyOutcome outcome;
                    string? failureCode;
                    try
                    {
                        (outcome, failureCode) = await TidyOneAsync(
                            candidate, deletedItemsByMailbox, token);
                    }
                    catch (ApprovedSentSourceThrottledException)
                    {
                        // The mailbox asked us to wait: nothing was tried, so nothing is
                        // recorded as an attempt and no further candidate is started.
                        Volatile.Write(ref throttled, 1);
                        return;
                    }

                    Interlocked.Increment(ref examined);
                    await store.RecordAsync(
                        candidate.OperationId,
                        candidate.OperationVersion,
                        outcome,
                        failureCode,
                        timeProvider.GetUtcNow(),
                        token);
                    switch (outcome)
                    {
                        case SentReportInstructionTidyOutcome.Moved:
                            Interlocked.Increment(ref moved);
                            break;
                        case SentReportInstructionTidyOutcome.AlreadyMoved:
                            Interlocked.Increment(ref alreadyMoved);
                            break;
                        case SentReportInstructionTidyOutcome.MessageMissing:
                            Interlocked.Increment(ref missing);
                            break;
                        default:
                            Interlocked.Increment(ref failed);
                            break;
                    }
                }
            });

        return new(examined, moved, alreadyMoved, missing, failed);
    }

    private async Task<(SentReportInstructionTidyOutcome Outcome, string? FailureCode)> TidyOneAsync(
        SentReportInstructionCandidate candidate,
        ConcurrentDictionary<string, Lazy<Task<string>>> deletedItemsByMailbox,
        CancellationToken cancellationToken)
    {
        try
        {
            var parent = await mover.GetParentFolderIdAsync(
                candidate.MailboxIdentity,
                candidate.ImmutableMessageId,
                cancellationToken);
            if (parent is null)
            {
                return (SentReportInstructionTidyOutcome.MessageMissing, null);
            }

            // One lookup per mailbox per run, shared by candidates in flight together; a
            // failed lookup is shared too, and each candidate records its own failure.
            var deleted = await deletedItemsByMailbox.GetOrAdd(
                candidate.MailboxIdentity,
                mailbox => new Lazy<Task<string>>(
                    () => mover.ResolveDeletedItemsFolderIdAsync(mailbox, cancellationToken))).Value;

            if (string.Equals(parent, deleted, StringComparison.Ordinal))
            {
                return (SentReportInstructionTidyOutcome.AlreadyMoved, null);
            }

            await mover.MoveAsync(
                new SentReportInstructionMove(
                    candidate.MailboxIdentity,
                    parent,
                    candidate.ImmutableMessageId,
                    deleted),
                cancellationToken);
            return (SentReportInstructionTidyOutcome.Moved, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            and not ApprovedSentSourceThrottledException)
        {
            return (SentReportInstructionTidyOutcome.Failed, FailureCode(exception));
        }
    }

    private static string FailureCode(Exception exception) => exception switch
    {
        ApprovedMailboxAccessDeniedException or UnauthorizedAccessException => MailboxNotPermittedFailureCode,
        _ => MoveFailedFailureCode
    };
}
