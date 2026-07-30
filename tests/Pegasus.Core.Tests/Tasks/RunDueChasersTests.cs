using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;

namespace Pegasus.Core.Tests.Tasks;

public sealed class RunDueChasersTests
{
    [Fact]
    public void SevenCalendarDayScheduleUsesLondonWallClockAcrossSpringDst()
    {
        var enteredAtUtc = new DateTimeOffset(2026, 3, 22, 1, 30, 0, TimeSpan.Zero);

        var scheduledAtUtc = CaseChaseSchedule.FirstChaseAt(enteredAtUtc);

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
        var requestLinkReference = Guid.NewGuid();
        var queries = new RecordingQueries([
            new(
                caseId,
                4,
                "QDOS26001",
                "Vehicle images",
                scheduledAtUtc,
                requestLinkReference)
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
        Assert.Equal(requestLinkReference, transition.RequestLinkReference);
        Assert.Equal(
            RunDueChasers.MissingMaterialRequestLinkPurpose,
            transition.RequestLinkPurpose);
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

    private static DueCaseChaser Candidate(DateTimeOffset nowUtc, int sequence) => new(
        Guid.NewGuid(),
        0,
        $"QDOS26{sequence:000}",
        "Outstanding material",
        nowUtc,
        null);

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
                new GeneratedCaseChaser(
                    transition.Id,
                    transition.CaseId,
                    transition.ScheduledAtUtc,
                    transition.GeneratedAtUtc,
                    transition.NextChaseAtUtc,
                    transition.CopyableText,
                    transition.RequestLinkReference,
                    transition.RequestLinkPurpose,
                    transition.ExpectedDueWorkVersion + 1)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
