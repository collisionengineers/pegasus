using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;

namespace Pegasus.Core.Tests.Tasks;

public sealed class RunDueChasersTests
{
    [Fact]
    public void SevenCalendarDayScheduleUsesLondonWallClockAcrossSpringDst()
    {
        var enteredAtUtc = new DateTimeOffset(2026, 3, 22, 1, 30, 0, TimeSpan.Zero);

        var scheduledAtUtc = CaseChaseSchedule.NextChaseAt(enteredAtUtc);

        Assert.Equal(
            new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero),
            scheduledAtUtc);
    }

    [Fact]
    public void SevenCalendarDayScheduleUsesDeterministicLondonOccurrenceAcrossAutumnDst()
    {
        var previousAtUtc = new DateTimeOffset(2026, 10, 18, 0, 30, 0, TimeSpan.Zero);

        var scheduledAtUtc = CaseChaseSchedule.NextChaseAt(previousAtUtc);

        Assert.Equal(
            new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero),
            scheduledAtUtc);
    }

    [Fact]
    public void HeldRemainderResumesAsLondonWallClockTimeAcrossDst()
    {
        var nextChaseAtUtc = new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero);
        var heldAtUtc = new DateTimeOffset(2026, 3, 28, 2, 0, 0, TimeSpan.Zero);
        var releasedAtUtc = new DateTimeOffset(2026, 3, 30, 1, 0, 0, TimeSpan.Zero);

        var remainder = CaseChaseSchedule.RemainingInterval(nextChaseAtUtc, heldAtUtc);
        var resumedAtUtc = CaseChaseSchedule.ResumeAt(releasedAtUtc, remainder);

        Assert.Equal(TimeSpan.FromDays(1), remainder);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 31, 1, 0, 0, TimeSpan.Zero),
            resumedAtUtc);
    }

    [Fact]
    public async Task SweepUsesOneClockSnapshotAndPersistsOnlyBoundedCopyableDraftMetadata()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
        var scheduledAtUtc = nowUtc.AddMinutes(-1);
        var caseId = Guid.NewGuid();
        var queries = new RecordingQueries([
            new(
                caseId,
                4,
                "QDOS26001",
                "Vehicle images",
                scheduledAtUtc)
        ]);
        var store = new RecordingStore();
        var useCase = new RunDueChasers(queries, store, new FixedTimeProvider(nowUtc));

        var result = await useCase.ExecuteAsync(1, default);

        Assert.Equal(new RunDueChasersResult(1, 1, 0, 0), result);
        Assert.Equal((nowUtc, 1), queries.LastDueQuery);
        var transition = Assert.Single(store.Transitions);
        Assert.Equal(caseId, transition.CaseId);
        Assert.Equal(4, transition.ExpectedDueWorkVersion);
        Assert.Equal(scheduledAtUtc, transition.ScheduledAtUtc);
        Assert.Equal(nowUtc, transition.GeneratedAtUtc);
        Assert.Equal(CaseChaseSchedule.NextChaseAt(scheduledAtUtc), transition.NextChaseAtUtc);
        Assert.Equal(
            "Please provide the outstanding material for case QDOS26001: Vehicle images.",
            transition.CopyableText);
        Assert.Equal(ActorKind.SystemWorker, transition.Actor.Kind);
        Assert.Equal(RunDueChasers.WorkerSubjectId, transition.Actor.SubjectId);
        Assert.StartsWith($"due-chaser:{caseId:N}:", transition.OperationKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SweepRejectsAnUnboundedQueryResultBeforePersisting()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
        var queries = new RecordingQueries([
            Candidate(nowUtc, 1),
            Candidate(nowUtc, 2)
        ]);
        var store = new RecordingStore();
        var useCase = new RunDueChasers(queries, store, new FixedTimeProvider(nowUtc));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(1, default));

        Assert.Empty(store.Transitions);
    }

    [Fact]
    public async Task SweepClaimsAtMostThreeOccurrencesAtOnceAndCountsEveryOutcome()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
        var candidates = Enumerable.Range(1, 9).Select(sequence => Candidate(nowUtc, sequence)).ToArray();
        var store = new OverlapStore(candidates);
        var useCase = new RunDueChasers(
            new RecordingQueries(candidates),
            store,
            new FixedTimeProvider(nowUtc));

        var result = await useCase.ExecuteAsync(9, default).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(3, store.HighWater);
        Assert.Equal(9, store.Claimed);
        Assert.Equal(new RunDueChasersResult(9, 3, 3, 3), result);
    }

    [Fact]
    public async Task SweepFailureStillPropagatesAndLeavesAClaimUnderWayUncancelled()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
        var slow = Candidate(nowUtc, 1);
        var bad = Candidate(nowUtc, 2);
        var store = new FailingSiblingStore(slow.CaseId, bad.CaseId);
        var useCase = new RunDueChasers(
            new RecordingQueries([slow, bad]),
            store,
            new FixedTimeProvider(nowUtc));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(2, default).WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Equal(FailingSiblingStore.Failure, failure.Message);
        Assert.False(store.SlowTokenCancelled);
        Assert.True(store.SlowCompleted);
    }

    private static DueCaseChaser Candidate(DateTimeOffset nowUtc, int sequence) => new(
        Guid.NewGuid(),
        0,
        $"QDOS26{sequence:000}",
        "Outstanding material",
        nowUtc);

    private sealed class RecordingQueries(IReadOnlyList<DueCaseChaser> candidates)
        : ICaseDueChaserQueries
    {
        public (DateTimeOffset AsOfUtc, int MaximumResults)? LastDueQuery { get; private set; }

        public Task<IReadOnlyList<DueCaseChaser>> GetDueAsync(
            DateTimeOffset asOfUtc,
            int maximumResults,
            CancellationToken cancellationToken)
        {
            LastDueQuery = (asOfUtc, maximumResults);
            return Task.FromResult(candidates);
        }

        public Task<GeneratedCaseChaser?> GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<GeneratedCaseChaser?>(null);
    }

    private sealed class RecordingStore : ICaseDueChaserStore
    {
        public List<DueChaserTransition> Transitions { get; } = [];

        public Task<DueChaserClaimResult> TryClaimAndRecordAsync(
            DueChaserTransition transition,
            CancellationToken cancellationToken)
        {
            Transitions.Add(transition);
            return Task.FromResult(new DueChaserClaimResult(
                DueChaserClaimOutcome.Recorded,
                Draft(transition)));
        }
    }

    // The first three claims wait until all three are under way, so the high-water mark is
    // three if the sweep overlaps them and never more than three if it bounds them.
    private sealed class OverlapStore(IReadOnlyList<DueCaseChaser> candidates) : ICaseDueChaserStore
    {
        private readonly TaskCompletionSource _threeUnderWay =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _inFlight;
        private int _highWater;
        private int _claimed;

        public int HighWater => Volatile.Read(ref _highWater);

        public int Claimed => Volatile.Read(ref _claimed);

        public async Task<DueChaserClaimResult> TryClaimAndRecordAsync(
            DueChaserTransition transition,
            CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref _inFlight);
            int seen;
            while ((seen = Volatile.Read(ref _highWater)) < now
                && Interlocked.CompareExchange(ref _highWater, now, seen) != seen)
            {
            }
            if (now == 3)
            {
                _threeUnderWay.TrySetResult();
            }

            await _threeUnderWay.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Task.Yield();
            Interlocked.Increment(ref _claimed);
            Interlocked.Decrement(ref _inFlight);

            var index = candidates.ToList().FindIndex(item => item.CaseId == transition.CaseId);
            return (index % 3) switch
            {
                0 => new DueChaserClaimResult(DueChaserClaimOutcome.Recorded, Draft(transition)),
                1 => new DueChaserClaimResult(DueChaserClaimOutcome.Replay, Draft(transition)),
                _ => new DueChaserClaimResult(DueChaserClaimOutcome.Superseded, null)
            };
        }
    }

    // The failing claim throws only once the slow claim is under way; the slow claim then
    // finishes and reports whether the sweep cancelled its token.
    private sealed class FailingSiblingStore(Guid slowCaseId, Guid badCaseId) : ICaseDueChaserStore
    {
        public const string Failure = "The second claim failed.";

        private readonly TaskCompletionSource _slowStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _badThrown =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool SlowTokenCancelled { get; private set; }

        public bool SlowCompleted { get; private set; }

        public async Task<DueChaserClaimResult> TryClaimAndRecordAsync(
            DueChaserTransition transition,
            CancellationToken cancellationToken)
        {
            if (transition.CaseId == badCaseId)
            {
                await _slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                _badThrown.SetResult();
                throw new InvalidOperationException(Failure);
            }

            Assert.Equal(slowCaseId, transition.CaseId);
            _slowStarted.SetResult();
            await _badThrown.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            await Task.Yield();
            SlowTokenCancelled = cancellationToken.IsCancellationRequested;
            SlowCompleted = true;
            return new(DueChaserClaimOutcome.Recorded, Draft(transition));
        }
    }

    private static GeneratedCaseChaser Draft(DueChaserTransition transition) => new(
        transition.Id,
        transition.CaseId,
        transition.ScheduledAtUtc,
        transition.GeneratedAtUtc,
        transition.NextChaseAtUtc,
        transition.CopyableText,
        transition.ExpectedDueWorkVersion + 1);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
