using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

/// <summary>
/// The long poll behind the Case page and the Automation Actor's waiting call (R551, 30 October
/// 2026): it answers at once on a change, holds on the Case's signal otherwise, and never holds
/// past the request's answer window.
/// </summary>
public sealed class CaseEditRequestWatchTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly Guid CaseId = Guid.NewGuid();

    [Fact]
    public async Task AnswersAtOnceWhenTheCallerKnowsNoStampOrADifferentOne()
    {
        var store = new ScriptedLeases(View("one"));
        var signal = new RecordingCaseEditSignal();
        var time = new ManualTimeProvider(Now);
        var watch = new CaseEditRequestWatch(store, signal, time);

        var first = await watch.WaitAsync(CaseId, Actor, null, CaseEditRequestWatch.MaximumHold, default);
        var second = await watch.WaitAsync(CaseId, Actor, "other", CaseEditRequestWatch.MaximumHold, default);

        Assert.Equal("one", first.Stamp);
        Assert.Equal("one", second.Stamp);
        Assert.Equal(2, store.Reads);
        Assert.Equal(2, signal.Waits);
        Assert.Empty(time.Timers);
    }

    /// <summary>
    /// The wake-up is taken before the read: a change raised while the first read is in flight is
    /// not slept through.
    /// </summary>
    [Fact]
    public async Task HoldsOnTheSameStampUntilTheCaseIsRaisedThenReadsAgain()
    {
        var store = new ScriptedLeases(View("same"), View("changed"));
        var signal = new RecordingCaseEditSignal();
        var time = new ManualTimeProvider(Now);
        var watch = new CaseEditRequestWatch(store, signal, time);

        var waiting = watch.WaitAsync(CaseId, Actor, "same", CaseEditRequestWatch.MaximumHold, default);
        await Task.Yield();

        Assert.False(waiting.IsCompleted);
        Assert.Equal(CaseEditRequestWatch.MaximumHold, Assert.Single(time.Timers).DueTime);
        signal.Raise(CaseId);
        var answer = await waiting;
        Assert.Equal("changed", answer.Stamp);
        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public async Task TheHoldEndsWhenItsTimeRunsOutAndAnswersTheCaseAsItStands()
    {
        var store = new ScriptedLeases(View("same"), View("same"));
        var signal = new RecordingCaseEditSignal();
        var time = new ManualTimeProvider(Now);
        var watch = new CaseEditRequestWatch(store, signal, time);

        var waiting = watch.WaitAsync(CaseId, Actor, "same", TimeSpan.FromSeconds(10), default);
        await Task.Yield();
        var timer = Assert.Single(time.Timers);
        Assert.Equal(TimeSpan.FromSeconds(10), timer.DueTime);
        timer.Fire();

        var answer = await waiting;
        Assert.Equal("same", answer.Stamp);
        Assert.Equal(2, store.Reads);
    }

    /// <summary>
    /// A pending request turns unanswered at exactly 45 seconds with no client timer: the hold
    /// is cut to the moment the window closes.
    /// </summary>
    [Fact]
    public async Task TheHoldNeverOutlastsAPendingRequestsAnswerWindow()
    {
        var request = new CaseEditRequestSnapshot(
            Guid.NewGuid(), ActorKind.Staff, "requester", "holder", Now.AddSeconds(-40), null);
        var store = new ScriptedLeases(
            View("pending") with { Request = request, RequestIsPending = true },
            View("unanswered") with { Request = request, RequestIsPending = false });
        var signal = new RecordingCaseEditSignal();
        var time = new ManualTimeProvider(Now);
        var watch = new CaseEditRequestWatch(store, signal, time);

        var waiting = watch.WaitAsync(CaseId, Actor, "pending", CaseEditRequestWatch.MaximumHold, default);
        await Task.Yield();
        var timer = Assert.Single(time.Timers);
        Assert.Equal(TimeSpan.FromSeconds(5), timer.DueTime);
        timer.Fire();

        Assert.Equal("unanswered", (await waiting).Stamp);
    }

    [Fact]
    public async Task AZeroHoldReadsTwiceAndNeverWaits()
    {
        var store = new ScriptedLeases(View("same"), View("same"));
        var time = new ManualTimeProvider(Now);
        var watch = new CaseEditRequestWatch(store, new RecordingCaseEditSignal(), time);

        await watch.WaitAsync(CaseId, Actor, "same", TimeSpan.Zero, default);

        Assert.Equal(2, store.Reads);
        Assert.Empty(time.Timers);
    }

    [Fact]
    public async Task CancellationEndsTheHoldAndAnInvalidHoldOrCaseIsRefused()
    {
        var store = new ScriptedLeases(View("same"), View("same"));
        var time = new ManualTimeProvider(Now);
        var watch = new CaseEditRequestWatch(store, new RecordingCaseEditSignal(), time);
        using var cancellation = new CancellationTokenSource();

        var waiting = watch.WaitAsync(CaseId, Actor, "same", CaseEditRequestWatch.MaximumHold, cancellation.Token);
        await Task.Yield();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            watch.WaitAsync(CaseId, Actor, null, CaseEditRequestWatch.MaximumHold + TimeSpan.FromSeconds(1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            watch.WaitAsync(Guid.Empty, Actor, null, TimeSpan.Zero, default));
    }

    private static CaseEditRequestView View(string stamp) =>
        new(stamp, 3, ActorKind.Staff, "holder", true, null, false, null);

    private sealed class ScriptedLeases(params CaseEditRequestView[] views) : ILeaseCaseForEdit
    {
        public int Reads { get; private set; }

        public Task<CaseEditRequestView> ReadRequestAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var view = views[Math.Min(Reads, views.Length - 1)];
            Reads += 1;
            return Task.FromResult(view);
        }

        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditRequestRaised> RaiseRequestAsync(RaiseCaseEditRequestRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AnswerRequestAsync(AnswerCaseEditRequestRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>A clock whose timers fire only when the test says so.</summary>
    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public List<ManualTimer> Timers { get; } = [];

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime);
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public TimeSpan DueTime { get; private set; } = dueTime;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            return true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
