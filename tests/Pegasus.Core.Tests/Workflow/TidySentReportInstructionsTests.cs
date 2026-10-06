using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class TidySentReportInstructionsTests
{
    private const string Mailbox = "engineers@collisionengineers.example";
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor WorkerActor = ActionActor.SystemWorker("sent-report-instruction-tidy");

    [Fact]
    public async Task AnInstructionStillInTheInboxIsMovedToDeletedItems()
    {
        var store = new FakeStore(Candidate("msg-1"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var move = Assert.Single(mover.Moves);
        Assert.Equal(new RetainedMailFolderMoveCoordinates(Mailbox, "inbox-id", "msg-1", "deleted-id"), move);
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

        Assert.Collection(
            store.Recorded,
            first =>
            {
                Assert.Equal(SentReportInstructionTidyOutcome.Failed, first.Outcome);
                Assert.Equal("move_failed", first.FailureCode);
            },
            second => Assert.Equal(SentReportInstructionTidyOutcome.Moved, second.Outcome));
        Assert.Equal(new SentReportInstructionTidyResult(2, 1, 0, 0, 1), result);
    }

    [Fact]
    public async Task DeletedItemsIsResolvedOncePerMailboxInOneRun()
    {
        var store = new FakeStore(Candidate("msg-1"), Candidate("msg-2"), Candidate("msg-3"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };

        await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(1, mover.ResolveCalls);
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

        var result = await Use(store, new UnavailableRetainedMailFolderMover()).ExecuteAsync(10, WorkerActor);

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

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Use(store, mover).ExecuteAsync(10, WorkerActor));

        Assert.Empty(store.Recorded);
    }

    /// <summary>
    /// The mailbox asked Pegasus to wait: nothing was tried, so the candidate
    /// keeps its attempts, and the rest of the batch is left for the next run.
    /// </summary>
    [Fact]
    public async Task AThrottleStopsTheBatchAndRecordsNoAttemptForTheCandidate()
    {
        var store = new FakeStore(Candidate("msg-1"), Candidate("msg-2"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id", ThrottleOnParentCall = 1 };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Empty(store.Recorded);
        Assert.Empty(mover.Moves);
        Assert.Equal(1, mover.ParentCalls);
        Assert.Equal(0, result.MovedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task AThrottleOnALaterCandidateKeepsWhatWasAlreadyRecorded()
    {
        var first = Candidate("msg-1");
        var store = new FakeStore(first, Candidate("msg-2"), Candidate("msg-3"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id", ThrottleOnParentCall = 2 };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(first.OperationId, recorded.OperationId);
        Assert.Equal(SentReportInstructionTidyOutcome.Moved, recorded.Outcome);
        Assert.Equal("msg-1", Assert.Single(mover.Moves).ImmutableMessageId);
        Assert.Equal(2, mover.ParentCalls);
        Assert.Equal(1, result.MovedCount);
        Assert.Equal(0, result.FailedCount);
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
        IRetainedMailFolderMover mover) =>
        new(store, mover, new FixedClock(NowUtc));

    private static SentReportInstructionCandidate Candidate(string immutableMessageId) =>
        new(Guid.NewGuid(), 4, Guid.NewGuid(), Guid.NewGuid(), Mailbox, immutableMessageId, 0);

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

    private sealed class FakeStore(params SentReportInstructionCandidate[] due) : ISentReportInstructionTidyStore
    {
        public int? RequestedMaximum { get; private set; }
        public List<Recording> Recorded { get; } = [];

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
            Recorded.Add(new(operationId, expectedVersion, outcome, failureCode, nowUtc));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMover : IRetainedMailFolderMover
    {
        private bool failed;

        public bool IsAvailable => true;
        public string? Parent { get; init; }
        public string DeletedItems { get; init; } = "deleted-id";
        public Exception? MoveFailure { get; init; }
        public bool FailOnlyFirstMove { get; init; }
        public int? ThrottleOnParentCall { get; init; }
        public int ParentCalls { get; private set; }
        public int ResolveCalls { get; private set; }
        public List<RetainedMailFolderMoveCoordinates> Moves { get; } = [];

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            if (MoveFailure is not null && !(FailOnlyFirstMove && failed))
            {
                failed = true;
                return Task.FromException(MoveFailure);
            }

            Moves.Add(coordinates);
            return Task.CompletedTask;
        }

        public Task<string?> GetParentFolderIdAsync(
            string mailboxId, string immutableMessageId, CancellationToken cancellationToken)
        {
            ParentCalls++;
            return ParentCalls == ThrottleOnParentCall
                ? Task.FromException<string?>(new ApprovedSentSourceThrottledException(TimeSpan.FromSeconds(30)))
                : Task.FromResult(Parent);
        }

        public Task<string> ResolveDeletedItemsFolderIdAsync(
            string mailboxIdentity, CancellationToken cancellationToken)
        {
            ResolveCalls++;
            return Task.FromResult(DeletedItems);
        }
    }
}
