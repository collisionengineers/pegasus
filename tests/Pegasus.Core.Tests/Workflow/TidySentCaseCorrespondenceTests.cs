using System.Collections.Concurrent;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class TidySentCaseCorrespondenceTests
{
    private const string Mailbox = "engineers@collisionengineers.example";
    private const string OtherMailbox = "info@collisionengineers.example";

    // A gate that is never opened fails the test after this long instead of hanging CI.
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);

    // Long enough for a send the loop wrongly let run beside another to arrive.
    private static readonly TimeSpan OverlapWindow = TimeSpan.FromMilliseconds(50);

    private static readonly DateTimeOffset NowUtc = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PreparedAtUtc = new(2026, 11, 10, 8, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor WorkerActor = ActionActor.SystemWorker("sent-mail-correspondence-tidy");

    [Fact]
    public async Task EveryLinkedMessageOfATickedSendIsMovedToDeletedItems()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"), Message("msg-2"), Message("msg-3"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(
            ["msg-1", "msg-2", "msg-3"],
            mover.Moves.Select(move => move.ImmutableMessageId).ToArray());
        Assert.Equal(new RetainedMessageMove(Mailbox, "inbox-id", "msg-1", "deleted-id"), mover.Moves.First());
        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(send.OperationId, recorded.OperationId);
        Assert.Equal(send.OperationVersion, recorded.ExpectedVersion);
        Assert.Equal(new SentCaseCorrespondenceCounts(3, 0, 0, 0), recorded.Counts);
        Assert.Null(recorded.FailureCode);
        Assert.Equal(NowUtc, recorded.NowUtc);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 3, 0, 0, 0), result);
    }

    [Fact]
    public async Task AMessageAlreadyInDeletedItemsCountsAsAlreadyMovedAndIsNotMovedAgain()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"), Message("msg-2"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            ParentOf = new Dictionary<string, string?> { ["msg-1"] = "deleted-id" }
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal("msg-2", Assert.Single(mover.Moves).ImmutableMessageId);
        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceCounts(1, 1, 0, 0), recorded.Counts);
        Assert.Null(recorded.FailureCode);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 1, 1, 0, 0), result);
    }

    [Fact]
    public async Task AMessageNoLongerInTheMailboxCountsAsMissingAndNothingIsMoved()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"));
        var mover = new FakeMover { Parent = null, DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Empty(mover.Moves);
        Assert.Equal(0, mover.ResolveCalls);
        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceCounts(0, 0, 1, 0), recorded.Counts);
        Assert.Null(recorded.FailureCode);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 0, 0, 1, 0), result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AMailboxPermissionRefusalIsRecordedWithItsOwnFailureCode(bool refusedByTheMailbox)
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"));
        Exception refusal = refusedByTheMailbox
            ? new ApprovedMailboxAccessDeniedException("403")
            : new UnauthorizedAccessException("401");
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailures = new Dictionary<string, Exception> { ["msg-1"] = refusal }
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceCounts(0, 0, 0, 1), recorded.Counts);
        Assert.Equal("mailbox_not_permitted", recorded.FailureCode);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 0, 0, 0, 1), result);
    }

    [Fact]
    public async Task AnyOtherFailureIsRecordedAsAFailedMove()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailures = new Dictionary<string, Exception> { ["msg-1"] = new HttpRequestException("boom") }
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceCounts(0, 0, 0, 1), recorded.Counts);
        Assert.Equal("move_failed", recorded.FailureCode);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 0, 0, 0, 1), result);
    }

    [Fact]
    public async Task AFailedMessageDoesNotStopTheSendsOtherMessages()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"), Message("msg-2"), Message("msg-3"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailures = new Dictionary<string, Exception> { ["msg-2"] = new HttpRequestException("boom") }
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(["msg-1", "msg-3"], mover.Moves.Select(move => move.ImmutableMessageId).ToArray());
        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceCounts(2, 0, 0, 1), recorded.Counts);
        Assert.Equal("move_failed", recorded.FailureCode);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 2, 0, 0, 1), result);
    }

    [Fact]
    public async Task APermissionRefusalBesideAnotherFailureIsTheRecordedFailureCode()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"), Message("msg-2", mailbox: OtherMailbox));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailures = new Dictionary<string, Exception>
            {
                ["msg-1"] = new HttpRequestException("boom"),
                ["msg-2"] = new ApprovedMailboxAccessDeniedException("403")
            }
        };

        await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceCounts(0, 0, 0, 2), recorded.Counts);
        Assert.Equal("mailbox_not_permitted", recorded.FailureCode);
    }

    [Fact]
    public async Task TwoTickedSendsOnOneCaseRunOneAfterAnother()
    {
        var caseId = Guid.NewGuid();
        var first = Candidate(caseId);
        var second = Candidate(caseId);
        var store = new FakeStore(first, second);
        store.Link(caseId, Message("msg-1"), Message("msg-2"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };
        // Each move is held until a third read of a message's folder is made, or the
        // overlap window passes: a loop that ran the two sends together would read the
        // Inbox for both before either moved.
        var thirdRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mover.OnParentRead = _ =>
        {
            if (mover.ParentCalls >= 3)
            {
                thirdRead.TrySetResult();
            }

            return Task.CompletedTask;
        };
        mover.OnMove = _ => Task.WhenAny(thirdRead.Task, Task.Delay(OverlapWindow));

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(["msg-1", "msg-2"], mover.Moves.Select(move => move.ImmutableMessageId).ToArray());
        Assert.Collection(
            store.Recorded,
            recorded =>
            {
                Assert.Equal(first.OperationId, recorded.OperationId);
                Assert.Equal(new SentCaseCorrespondenceCounts(2, 0, 0, 0), recorded.Counts);
            },
            recorded =>
            {
                Assert.Equal(second.OperationId, recorded.OperationId);
                Assert.Equal(new SentCaseCorrespondenceCounts(0, 2, 0, 0), recorded.Counts);
            });
        Assert.Equal(new SentCaseCorrespondenceTidyResult(2, 2, 2, 0, 0), result);
    }

    [Fact]
    public async Task SendsOnDifferentCasesAreTidiedTwoAtATimeAndNeverMoreThanTwo()
    {
        var sends = Enumerable.Range(1, 5).Select(_ => Candidate()).ToArray();
        var store = new FakeStore(sends);
        for (var index = 0; index < sends.Length; index++)
        {
            store.Link(sends[index].CaseId, Message($"msg-{index + 1}"));
        }

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
        Assert.Equal(new SentCaseCorrespondenceTidyResult(5, 5, 0, 0, 0), result);
    }

    [Fact]
    public async Task DeletedItemsIsResolvedOncePerMailboxInOneRun()
    {
        var first = Candidate();
        var second = Candidate();
        var store = new FakeStore(first, second);
        store.Link(first.CaseId, Message("msg-1"), Message("msg-2"));
        store.Link(second.CaseId, Message("msg-3"), Message("msg-4", mailbox: OtherMailbox));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };
        // The two sends are held at the first mailbox's lookup until both have read a
        // message's folder, so both are inside it together.
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
        Assert.Equal(4, mover.Moves.Count);
    }

    /// <summary>
    /// The mailbox asked Pegasus to wait: the throttled send records no attempt,
    /// and no further send is started. The send already under way finishes and
    /// is recorded.
    /// </summary>
    [Fact]
    public async Task AThrottleStopsTheBatchAndRecordsNothingForTheThrottledSend()
    {
        var first = Candidate();
        var throttledCase = Guid.NewGuid();
        var store = new FakeStore(first, Candidate(throttledCase), Candidate(throttledCase));
        store.Link(first.CaseId, Message("msg-1"));
        store.Link(throttledCase, Message("msg-2"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id", ThrottleMessageId = "msg-2" };
        // msg-1 is under way when msg-2 is throttled, and is held until then. The second
        // send on the throttled Case is the one further send that only the throttled run
        // can reach.
        mover.OnParentRead = id => id == "msg-1"
            ? mover.ThrottleRaised.WaitAsync(GateTimeout)
            : Task.CompletedTask;

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(first.OperationId, recorded.OperationId);
        Assert.Equal(new SentCaseCorrespondenceCounts(1, 0, 0, 0), recorded.Counts);
        Assert.Equal("msg-1", Assert.Single(mover.Moves).ImmutableMessageId);
        Assert.Equal(2, mover.ParentCalls);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 1, 0, 0, 0), result);
    }

    [Fact]
    public async Task AThrottlePartWayThroughASendRecordsNothingForIt()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"), Message("msg-2"), Message("msg-3"));
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id", ThrottleMessageId = "msg-2" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        // What it had moved is found already moved on the next run.
        Assert.Equal("msg-1", Assert.Single(mover.Moves).ImmutableMessageId);
        Assert.Equal(2, mover.ParentCalls);
        Assert.Empty(store.Recorded);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(0, 0, 0, 0, 0), result);
    }

    [Fact]
    public async Task CancellationIsNotRecordedAsAFailure()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            MoveFailures = new Dictionary<string, Exception> { ["msg-1"] = new OperationCanceledException() }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Use(store, mover).ExecuteAsync(10, WorkerActor));

        Assert.Empty(store.Recorded);
    }

    [Fact]
    public async Task AHostThatCannotReachTheMailboxReadsAndRecordsNothing()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"));

        var result = await Use(store, new UnavailableRetainedMessageMover()).ExecuteAsync(10, WorkerActor);

        Assert.Equal(new SentCaseCorrespondenceTidyResult(0, 0, 0, 0, 0), result);
        Assert.Null(store.RequestedMaximum);
        Assert.Empty(store.MessageRequests);
        Assert.Empty(store.Recorded);
    }

    [Fact]
    public async Task OnlyTheSystemWorkerMayRunIt()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        store.Link(send.CaseId, Message("msg-1"));
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
    public async Task ACaseWithNoLinkedMailRecordsZeroCountsAndNoFailure()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        var mover = new FakeMover { Parent = "inbox-id", DeletedItems = "deleted-id" };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Empty(mover.Moves);
        Assert.Equal(0, mover.ResolveCalls);
        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(send.OperationId, recorded.OperationId);
        Assert.Equal(new SentCaseCorrespondenceCounts(0, 0, 0, 0), recorded.Counts);
        Assert.Null(recorded.FailureCode);
        Assert.Equal(new SentCaseCorrespondenceTidyResult(1, 0, 0, 0, 0), result);
    }

    [Fact]
    public async Task TheStoreIsAskedForTheRequestedBatchAndForMailRetainedBeforeEachSendWasPrepared()
    {
        var send = Candidate();
        var store = new FakeStore(send);
        var mover = new FakeMover();

        await Use(store, mover).ExecuteAsync(7, WorkerActor);

        Assert.Equal(7, store.RequestedMaximum);
        var request = Assert.Single(store.MessageRequests);
        Assert.Equal(send.CaseId, request.CaseId);
        Assert.Equal(PreparedAtUtc, request.RetainedAtOrBeforeUtc);
    }

    [Fact]
    public async Task ResultTotalsCountSendsExaminedAndSumMessagesAcrossSends()
    {
        var first = Candidate();
        var second = Candidate();
        var store = new FakeStore(first, second);
        store.Link(first.CaseId, Message("msg-1"), Message("msg-2"), Message("msg-3"));
        store.Link(second.CaseId, Message("msg-4"), Message("msg-5"), Message("msg-6"));
        var mover = new FakeMover
        {
            Parent = "inbox-id",
            DeletedItems = "deleted-id",
            ParentOf = new Dictionary<string, string?> { ["msg-2"] = "deleted-id", ["msg-5"] = null },
            MoveFailures = new Dictionary<string, Exception> { ["msg-6"] = new HttpRequestException("boom") }
        };

        var result = await Use(store, mover).ExecuteAsync(10, WorkerActor);

        Assert.Equal(new SentCaseCorrespondenceTidyResult(2, 3, 1, 1, 1), result);
        Assert.Equal(
            new SentCaseCorrespondenceCounts(2, 1, 0, 0),
            Assert.Single(store.Recorded, recorded => recorded.OperationId == first.OperationId).Counts);
        Assert.Equal(
            new SentCaseCorrespondenceCounts(1, 0, 1, 1),
            Assert.Single(store.Recorded, recorded => recorded.OperationId == second.OperationId).Counts);
    }

    [Fact]
    public void TheAttemptBoundAndFailureCodesMatchTheInstructionTidy()
    {
        Assert.Equal(3, TidySentCaseCorrespondence.MaximumAttempts);
        Assert.Equal("mailbox_not_permitted", TidySentCaseCorrespondence.MailboxNotPermittedFailureCode);
        Assert.Equal("move_failed", TidySentCaseCorrespondence.MoveFailedFailureCode);
    }

    private static TidySentCaseCorrespondence Use(
        ISentCaseCorrespondenceTidyStore store,
        IRetainedMessageMover mover) =>
        new(store, mover, new FixedClock(NowUtc));

    private static SentCaseCorrespondenceCandidate Candidate(Guid? caseId = null) =>
        new(Guid.NewGuid(), 4, caseId ?? Guid.NewGuid(), PreparedAtUtc, 0);

    private static CaseCorrespondenceMessage Message(string immutableMessageId, string mailbox = Mailbox) =>
        new(Guid.NewGuid(), mailbox, immutableMessageId);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Recording(
        Guid OperationId,
        long ExpectedVersion,
        SentCaseCorrespondenceCounts Counts,
        string? FailureCode,
        DateTimeOffset NowUtc);

    // The loop calls the fakes from two threads, so every member is safe to call concurrently.
    private sealed class FakeStore(params SentCaseCorrespondenceCandidate[] due) : ISentCaseCorrespondenceTidyStore
    {
        private readonly ConcurrentDictionary<Guid, CaseCorrespondenceMessage[]> messagesByCase = new();

        public int? RequestedMaximum { get; private set; }
        public ConcurrentQueue<(Guid CaseId, DateTimeOffset RetainedAtOrBeforeUtc)> MessageRequests { get; } = new();
        public ConcurrentQueue<Recording> Recorded { get; } = new();

        public void Link(Guid caseId, params CaseCorrespondenceMessage[] messages) =>
            messagesByCase[caseId] = messages;

        public Task<IReadOnlyList<SentCaseCorrespondenceCandidate>> ListDueAsync(
            int maximumItems, CancellationToken cancellationToken)
        {
            RequestedMaximum = maximumItems;
            return Task.FromResult<IReadOnlyList<SentCaseCorrespondenceCandidate>>(due);
        }

        public Task<IReadOnlyList<CaseCorrespondenceMessage>> ListMessagesAsync(
            Guid caseId, DateTimeOffset retainedAtOrBeforeUtc, CancellationToken cancellationToken)
        {
            MessageRequests.Enqueue((caseId, retainedAtOrBeforeUtc));
            return Task.FromResult<IReadOnlyList<CaseCorrespondenceMessage>>(
                messagesByCase.TryGetValue(caseId, out var messages) ? messages : []);
        }

        public Task<bool> RecordAsync(
            Guid operationId,
            long expectedVersion,
            SentCaseCorrespondenceCounts counts,
            string? failureCode,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            Recorded.Enqueue(new Recording(operationId, expectedVersion, counts, failureCode, nowUtc));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMover : IRetainedMessageMover
    {
        private readonly Lock gate = new();
        private readonly ConcurrentDictionary<string, string> movedTo = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource throttleRaised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int parentCalls;
        private int resolveCalls;
        private int movesInFlight;
        private int maxMovesInFlight;

        public bool IsAvailable => true;

        // Where a message sits until it is moved: its entry in ParentOf (null is gone), else Parent.
        public string? Parent { get; init; }
        public Dictionary<string, string?> ParentOf { get; init; } = new();
        public string DeletedItems { get; init; } = "deleted-id";

        // The failure each named message's move meets, every time it is tried.
        public Dictionary<string, Exception> MoveFailures { get; init; } = new();
        public string? ThrottleMessageId { get; init; }

        // Awaited inside the matching call, so a test can hold a send there.
        public Func<string, Task>? OnParentRead { get; set; }
        public Func<string, Task>? OnResolve { get; set; }
        public Func<RetainedMessageMove, Task>? OnMove { get; set; }

        public int ParentCalls => Volatile.Read(ref parentCalls);
        public int ResolveCalls => Volatile.Read(ref resolveCalls);
        public int MovesInFlight => Volatile.Read(ref movesInFlight);
        public int MaxMovesInFlight => Volatile.Read(ref maxMovesInFlight);
        public Task ThrottleRaised => throttleRaised.Task;
        public ConcurrentQueue<RetainedMessageMove> Moves { get; } = new();

        public async Task MoveAsync(RetainedMessageMove move, CancellationToken cancellationToken)
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

                if (MoveFailures.TryGetValue(move.ImmutableMessageId, out var failure))
                {
                    throw failure;
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
