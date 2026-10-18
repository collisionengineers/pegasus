using System.Collections.Concurrent;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class TidySentReportInstructionsTests
{
    private const string Mailbox = "engineers@collisionengineers.example";
    private const string OtherMailbox = "info@collisionengineers.example";

    // A gate that is never opened fails the test after this long instead of hanging CI.
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);

    // Long enough for a candidate the loop wrongly let run beside another to arrive.
    private static readonly TimeSpan OverlapWindow = TimeSpan.FromMilliseconds(50);

    private static readonly DateTimeOffset NowUtc = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor WorkerActor = ActionActor.SystemWorker("sent-report-instruction-tidy");

    [Fact]
    public async Task AnInstructionStillInTheInboxIsMovedToDeletedItems()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var move = Assert.Single(mover.Moves);
        Assert.Equal(new SentReportInstructionMove(Mailbox, "inbox-id", "msg-1", "deleted-id"), move);
        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(SentReportInstructionTidyOutcome.Moved, recorded.Outcome);
        Assert.Null(recorded.FailureCode);
        Assert.Equal(NowUtc, recorded.NowUtc);
        Assert.Equal(new SentReportInstructionTidyResult(1, 1, 0, 0, 0), result);
    }

    [Fact]
    public async Task AnInstructionAlreadyInDeletedItemsIsRecordedAndNotMovedAgain()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover { Parent = "deleted-id", DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Empty(mover.Moves);
        Assert.Equal(SentReportInstructionTidyOutcome.AlreadyMoved, Assert.Single(store.Recorded).Outcome);
        Assert.Equal(1, result.AlreadyMovedCount);
    }

    [Fact]
    public async Task AMessageThatIsGoneIsRecordedAsMissingAndNothingIsMoved()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover { Parent = null, DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Empty(mover.Moves);
        Assert.Equal(0, mover.ResolveCalls);
        Assert.Equal(SentReportInstructionTidyOutcome.MessageMissing, Assert.Single(store.Recorded).Outcome);
        Assert.Equal(1, result.MessageMissingCount);
    }

    [Fact]
    public async Task APermissionRefusalIsRecordedWithItsOwnFailureCode()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailure = new ApprovedMailboxAccessDeniedException("403")
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(SentReportInstructionTidyOutcome.Failed, recorded.Outcome);
        Assert.Equal("mailbox_not_permitted", recorded.FailureCode);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task AnyOtherFailureIsRecordedAndDoesNotStopTheNextCandidate()
    {
        var store = new FakeStore(Candidate("msg-1"), Candidate("msg-2"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailure = new HttpRequestException("boom"),
            FailOnlyFirstMove = true
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        // The two run together, so which one meets the failure is not fixed.
        Assert.Equal(2, store.Recorded.Count);
        var failure = Assert.Single(store.Recorded, recording => recording.Outcome == SentReportInstructionTidyOutcome.Failed);
        Assert.Equal("move_failed", failure.FailureCode);
        Assert.Single(store.Recorded, recording => recording.Outcome == SentReportInstructionTidyOutcome.Moved);
        Assert.Equal(new SentReportInstructionTidyResult(2, 1, 0, 0, 1), result);
    }

    [Fact]
    public async Task DeletedItemsIsResolvedOncePerMailboxInOneRun()
    {
        var store = new FakeStore(
            Candidate("msg-1"),
            Candidate("msg-2"),
            Candidate("msg-3", mailbox: OtherMailbox));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };
        // The two candidates in one mailbox are held at the lookup until both have read
        // their message's folder, so both are inside it together.
        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mover.OnParentRead = _ =>
        {
            if (mover.ParentCalls >= 2)
            {
                bothRead.TrySetResult();
            }

            return Task.CompletedTask;
        };
        mover.OnResolve = mailbox => mailbox == Mailbox
            ? bothRead.Task.WaitAsync(GateTimeout)
            : Task.CompletedTask;

        await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(2, mover.ResolveCalls);
        Assert.Equal(3, mover.Moves.Count);
    }

    [Fact]
    public async Task TheStoreIsAskedForNoMoreThanTheRequestedBatch()
    {
        var store = new FakeStore();
        var mover = new FakeMover();

        await Use(store, mover).ExecuteAsync(7, WorkerActor);

        Assert.Equal(7, store.RequestedMaximum);
    }

    [Fact]
    public async Task AHostThatCannotReachTheMailboxReadsAndRecordsNothing()
    {
        var store = new FakeStore(Candidate("msg-1"));

        var result = await Use(store, new UnavailableSentReportInstructionMover()).ExecuteAsync(10, WorkerActor);

        Assert.Equal(new SentReportInstructionTidyResult(0, 0, 0, 0, 0), result);
        Assert.Null(store.RequestedMaximum);
        Assert.Empty(store.Recorded);
    }

    [Fact]
    public async Task CancellationIsNotRecordedAsAFailure()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailure = new OperationCanceledException()
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Use(store, mover).ExecuteAsync(10, WorkerActor));

        Assert.Empty(store.Recorded);
    }

    /// <summary>
    /// The mailbox asked Pegasus to wait: nothing was tried, so the candidate
    /// keeps its attempts, and no further candidate is started. The candidate
    /// already under way finishes and is recorded.
    /// </summary>
    [Fact]
    public async Task AThrottleStopsTheBatchAndRecordsNoAttemptForTheCandidate()
    {
        var first = Candidate("msg-1");
        var instruction = Guid.NewGuid();
        var store = new FakeStore(
            first,
            Candidate("msg-2", retainedMessageId: instruction),
            Candidate("msg-2", retainedMessageId: instruction));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id", ThrottleMessageId = "msg-2" };
        // msg-1 is under way when msg-2 is throttled, and is held until then. The second
        // send of msg-2 is the one further candidate that only the throttled run can reach.
        mover.OnParentRead = id => id == "msg-1"
            ? mover.ThrottleRaised.WaitAsync(GateTimeout)
            : Task.CompletedTask;

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(first.OperationId, recorded.OperationId);
        Assert.Equal(SentReportInstructionTidyOutcome.Moved, recorded.Outcome);
        Assert.Equal("msg-1", Assert.Single(mover.Moves).ImmutableMessageId);
        Assert.Equal(2, mover.ParentCalls);
        Assert.Equal(new SentReportInstructionTidyResult(1, 1, 0, 0, 0), result);
    }

    [Fact]
    public async Task AThrottleOnALaterCandidateKeepsWhatWasAlreadyRecorded()
    {
        var first = Candidate("msg-1");
        var instruction = Guid.NewGuid();
        var store = new FakeStore(
            first,
            Candidate("msg-2", retainedMessageId: instruction),
            Candidate("msg-2", retainedMessageId: instruction));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id", ThrottleMessageId = "msg-2" };
        // msg-2 is throttled only after msg-1 has been recorded.
        var firstRecorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnRecorded = _ => firstRecorded.TrySetResult();
        mover.OnParentRead = id => id == "msg-2"
            ? firstRecorded.Task.WaitAsync(GateTimeout)
            : Task.CompletedTask;

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(first.OperationId, recorded.OperationId);
        Assert.Equal(SentReportInstructionTidyOutcome.Moved, recorded.Outcome);
        Assert.Equal("msg-1", Assert.Single(mover.Moves).ImmutableMessageId);
        Assert.Equal(2, mover.ParentCalls);
        Assert.Equal(new SentReportInstructionTidyResult(1, 1, 0, 0, 0), result);
    }

    [Fact]
    public async Task TwoInstructionsAreTidiedAtOnceAndNeverMoreThanTwo()
    {
        var store = new FakeStore(
            Candidate("msg-1"), Candidate("msg-2"), Candidate("msg-3"), Candidate("msg-4"), Candidate("msg-5"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };
        // No move completes until two are inside the mover together; each is then held
        // for the overlap window so a wider loop would show a third.
        var twoInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mover.OnMove = async _ =>
        {
            if (mover.MovesInFlight >= 2)
            {
                twoInside.TrySetResult();
            }

            await twoInside.Task.WaitAsync(GateTimeout);
            await Task.Delay(OverlapWindow);
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(2, mover.MaxMovesInFlight);
        Assert.Equal(5, mover.Moves.Count);
        Assert.Equal(new SentReportInstructionTidyResult(5, 5, 0, 0, 0), result);
    }

    [Fact]
    public async Task TwoSendsThatAnsweredTheSameInstructionMoveItOnce()
    {
        var instruction = Guid.NewGuid();
        var store = new FakeStore(
            Candidate("msg-1", retainedMessageId: instruction),
            Candidate("msg-1", retainedMessageId: instruction));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };
        // The move is held until a second read of the message's folder is made, or the
        // overlap window passes: a loop that ran the two together would read the Inbox twice.
        var secondRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mover.OnParentRead = _ =>
        {
            if (mover.ParentCalls >= 2)
            {
                secondRead.TrySetResult();
            }

            return Task.CompletedTask;
        };
        mover.OnMove = _ => Task.WhenAny(secondRead.Task, Task.Delay(OverlapWindow));

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal("msg-1", Assert.Single(mover.Moves).ImmutableMessageId);
        Assert.Collection(
            store.Recorded,
            first => Assert.Equal(SentReportInstructionTidyOutcome.Moved, first.Outcome),
            second => Assert.Equal(SentReportInstructionTidyOutcome.AlreadyMoved, second.Outcome));
        Assert.Equal(new SentReportInstructionTidyResult(2, 1, 1, 0, 0), result);
    }

    [Fact]
    public async Task ResultCountsHoldAcrossMixedOutcomes()
    {
        var store = new FakeStore(
            Candidate("msg-1"), Candidate("msg-2"), Candidate("msg-3"), Candidate("msg-4"), Candidate("msg-5"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            ParentOf = new Dictionary<string, string?> { ["msg-2"] = "deleted-id", ["msg-3"] = null },
            MoveFailure = new HttpRequestException("boom"),
            FailMovesOf = "msg-4"
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(new SentReportInstructionTidyResult(5, 2, 1, 1, 1), result);
        Assert.Equal(5, store.Recorded.Count);
        Assert.Equal(2, store.Recorded.Count(recording => recording.Outcome == SentReportInstructionTidyOutcome.Moved));
        Assert.Equal(1, store.Recorded.Count(recording => recording.Outcome == SentReportInstructionTidyOutcome.AlreadyMoved));
        Assert.Equal(1, store.Recorded.Count(recording => recording.Outcome == SentReportInstructionTidyOutcome.MessageMissing));
        Assert.Equal(1, store.Recorded.Count(recording => recording.Outcome == SentReportInstructionTidyOutcome.Failed));
    }

    [Fact]
    public async Task OnlyTheSystemWorkerMayRunIt()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => Use(store, mover).ExecuteAsync(10, staff));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => Use(store, mover).ExecuteAsync(10, ActionActor.Automation("automation")));

        Assert.Empty(mover.Moves);
        Assert.Null(store.RequestedMaximum);
    }

    [Fact]
    public void TheAttemptBoundIsThree()
    {
        Assert.Equal(3, TidySentReportInstructions.MaximumAttempts);
    }

    private static TidySentReportInstructions Use(
        ISentReportInstructionTidyStore store,
        ISentReportInstructionMover mover) =>
        new(store, mover, new FixedClock(NowUtc));

    private static SentReportInstructionCandidate Candidate(
        string immutableMessageId,
        string mailbox = Mailbox,
        Guid? retainedMessageId = null) =>
        new(Guid.NewGuid(), 4, Guid.NewGuid(), retainedMessageId ?? Guid.NewGuid(), mailbox, immutableMessageId, 0);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Recording(
        Guid OperationId,
        long ExpectedVersion,
        SentReportInstructionTidyOutcome Outcome,
        string? FailureCode,
        DateTimeOffset NowUtc);

    // The loop calls the fakes from two threads, so every member is safe to call concurrently.
    private sealed class FakeStore(params SentReportInstructionCandidate[] due) : ISentReportInstructionTidyStore
    {
        public int? RequestedMaximum { get; private set; }
        public ConcurrentQueue<Recording> Recorded { get; } = new();
        public Action<Recording>? OnRecorded { get; set; }

        public Task<IReadOnlyList<SentReportInstructionCandidate>> ListDueAsync(
            int maximumItems, CancellationToken cancellationToken)
        {
            RequestedMaximum = maximumItems;
            return Task.FromResult<IReadOnlyList<SentReportInstructionCandidate>>(due);
        }

        public Task<bool> RecordAsync(
            Guid operationId,
            long expectedVersion,
            SentReportInstructionTidyOutcome outcome,
            string? failureCode,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            var recording = new Recording(operationId, expectedVersion, outcome, failureCode, nowUtc);
            Recorded.Enqueue(recording);
            OnRecorded?.Invoke(recording);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMover : ISentReportInstructionMover
    {
        private readonly Lock gate = new();
        private readonly ConcurrentDictionary<string, string> movedTo = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource throttleRaised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int failedOnce;
        private int parentCalls;
        private int resolveCalls;
        private int movesInFlight;
        private int maxMovesInFlight;

        public bool IsAvailable => true;

        // Where a message sits until it is moved: its entry in ParentOf (null is gone), else Parent.
        public string? Parent { get; init; }
        public IReadOnlyDictionary<string, string?> ParentOf { get; init; } = new Dictionary<string, string?>();
        public string DeletedItems { get; init; } = "deleted-id";
        public Exception? MoveFailure { get; init; }
        public bool FailOnlyFirstMove { get; init; }
        public string? FailMovesOf { get; init; }
        public string? ThrottleMessageId { get; init; }

        // Awaited inside the matching call, so a test can hold a candidate there.
        public Func<string, Task>? OnParentRead { get; set; }
        public Func<string, Task>? OnResolve { get; set; }
        public Func<SentReportInstructionMove, Task>? OnMove { get; set; }

        public int ParentCalls => Volatile.Read(ref parentCalls);
        public int ResolveCalls => Volatile.Read(ref resolveCalls);
        public int MovesInFlight => Volatile.Read(ref movesInFlight);
        public int MaxMovesInFlight => Volatile.Read(ref maxMovesInFlight);
        public Task ThrottleRaised => throttleRaised.Task;
        public ConcurrentQueue<SentReportInstructionMove> Moves { get; } = new();

        public async Task MoveAsync(SentReportInstructionMove move, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                movesInFlight++;
                maxMovesInFlight = Math.Max(maxMovesInFlight, movesInFlight);
            }

            try
            {
                if (OnMove is not null)
                {
                    await OnMove(move);
                }

                if (MoveFailure is not null
                    && (FailMovesOf is null || move.ImmutableMessageId == FailMovesOf)
                    && (!FailOnlyFirstMove || Interlocked.Exchange(ref failedOnce, 1) == 0))
                {
                    throw MoveFailure;
                }

                movedTo[move.ImmutableMessageId] = move.DestinationFolderId;
                Moves.Enqueue(move);
            }
            finally
            {
                lock (gate)
                {
                    movesInFlight--;
                }
            }
        }

        public async Task<string?> GetParentFolderIdAsync(
            string mailboxIdentity, string immutableMessageId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref parentCalls);
            if (OnParentRead is not null)
            {
                await OnParentRead(immutableMessageId);
            }

            if (immutableMessageId == ThrottleMessageId)
            {
                throttleRaised.TrySetResult();
                throw new ApprovedSentSourceThrottledException(TimeSpan.FromSeconds(30));
            }

            if (movedTo.TryGetValue(immutableMessageId, out var folder))
            {
                return folder;
            }

            return ParentOf.TryGetValue(immutableMessageId, out var parent) ? parent : Parent;
        }

        public async Task<string> ResolveDeletedItemsFolderIdAsync(
            string mailboxIdentity, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref resolveCalls);
            if (OnResolve is not null)
            {
                await OnResolve(mailboxIdentity);
            }

            return DeletedItems;
        }
    }
}
