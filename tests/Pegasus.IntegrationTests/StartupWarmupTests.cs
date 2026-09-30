using System.Data.Common;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Web.Health;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The startup warm-up is best effort: nothing it meets may end the host,
/// and readiness is always released.
/// </summary>
public sealed class StartupWarmupTests
{
    [Fact]
    public async Task FailingReadsNeitherStopTheHostNorHoldReadiness()
    {
        var services = new ServiceCollection();
        // No context factory is registered, so the model step fails; the Work
        // Centre step meets a cancelled database command's error, and the
        // recent-cases step an arbitrary one.
        services.AddScoped<IGetOperationsSnapshot>(_ => throw new CancelledCommandException());
        services.AddScoped<IListRecentCases>(_ => throw new InvalidOperationException("Unreadable."));
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true, keepWarmInterval: TimeSpan.Zero);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        Assert.False(state.IsReady);
        await warmup.StartAsync(CancellationToken.None);
        var run = Assert.IsAssignableFrom<Task>(warmup.ExecuteTask);
        await run.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(run.IsCompletedSuccessfully);
        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheKeyRingLoadsInTheWarmUpNotBeforeThePortBinds()
    {
        var keyRing = new CountingKeyRing();
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(keyRing);
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true, keepWarmInterval: TimeSpan.Zero);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(1, keyRing.Loads);
        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheWarmUpWaitsForTheOAuthCertificatesAndThenEnds()
    {
        var store = new OAuthCertificateStore();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true, keepWarmInterval: TimeSpan.Zero);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        var run = Assert.IsAssignableFrom<Task>(warmup.ExecuteTask);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(run.IsCompleted);
        Assert.False(state.IsReady);

        store.Complete(new OAuthCertificateSet([], []));
        await run.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheWarmUpRunsTheMailWebhookSubscriptionRead()
    {
        var subscriptions = new CountingSubscriptionStore();
        var services = new ServiceCollection();
        services.AddSingleton<IApprovedMailboxSubscriptionStore>(subscriptions);
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true, keepWarmInterval: TimeSpan.Zero);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(1, subscriptions.ActiveReads);
        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheModelStepLineCarriesTheModelBuildAndTheFirstConnectionTimings()
    {
        // A connection that is refused at once: the model is real, the login
        // never happens, and CanConnectAsync answers false in about a second.
        var services = new ServiceCollection();
        services.AddDbContextFactory<PegasusDbContext>(options => options.UseSqlServer(
            "Server=tcp:127.0.0.1,1;Database=WarmupTimings;Connect Timeout=2;ConnectRetryCount=0;Encrypt=False"));
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();
        var state = new StartupWarmupState(warms: true, keepWarmInterval: TimeSpan.Zero);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            logger);

        await warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));
        await warmup.StopAsync(CancellationToken.None);

        var line = Assert.Single(
            logger.Messages,
            message => message.StartsWith("Startup warm-up step model finished in ", StringComparison.Ordinal));
        Assert.Matches(@" ms \(model build \d+ ms, first connection \d+ ms\)$", line);
    }

    [Fact]
    public async Task TheKeepWarmPassRepeatsEveryIntervalAfterTheFirstPass()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        // The first pass has run and readiness was released after it.
        Assert.Equal(1, reads.Calls("snapshot"));
        Assert.True(run.State.IsReady);

        // Short of the interval nothing runs.
        run.Time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(59));
        Assert.Equal(1, reads.Calls("snapshot"));

        run.Time.Advance(TimeSpan.FromSeconds(1));
        await run.Time.WaitForTimerAsync();
        Assert.Equal(2, reads.Calls("snapshot"));

        await run.PassAsync(TimeSpan.FromMinutes(3));
        Assert.Equal(3, reads.Calls("snapshot"));
        Assert.Equal(2, reads.Calls("mailboxes"));
    }

    [Fact]
    public async Task AnIntervalOfZeroRunsTheFirstPassOnly()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.Zero);

        await run.Warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(run.Warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(1, reads.Calls("snapshot"));
        Assert.Equal(0, reads.Calls("mailboxes"));
        Assert.True(run.State.IsReady);
    }

    [Fact]
    public async Task NoPassEverMarksTheRecentCasesFeedSeen()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();
        await run.PassAsync(TimeSpan.FromMinutes(3));
        await run.PassAsync(TimeSpan.FromMinutes(3));

        var markSeen = reads.MarkSeenValues;
        Assert.Equal(3, markSeen.Length);
        Assert.All(markSeen, seen => Assert.False(seen));
    }

    [Fact]
    public async Task TheKeepWarmPassReadsTheInboxListAsAFreshVisitDoes()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();
        Assert.Equal(0, reads.Calls("mailboxes"));

        await run.PassAsync(TimeSpan.FromMinutes(3));

        Assert.Equal(1, reads.Calls("mailboxes"));
        Assert.Equal(1, reads.Calls("inbox-page"));
        Assert.Equal(1, reads.Calls("mail-freshness"));
        var (scope, page, pageSize) = Assert.Single(reads.InboxPages);
        Assert.Equal(new MailWorkspaceScope(null, MailFolderScope.Inbox), scope);
        Assert.Equal(1, page);
        Assert.Equal(Pegasus.Web.Pages.Mail.IndexModel.PageSize, pageSize);
    }

    [Fact]
    public async Task TheKeepWarmPassReadsTheCasePageOfTheNewestCase()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();
        Assert.Equal(1, reads.Calls("case-kind"));

        await run.PassAsync(TimeSpan.FromMinutes(3));

        Assert.Equal(2, reads.Calls("case-kind"));
        Assert.Equal(2, reads.Calls("case-frame"));
        Assert.Equal(2, reads.Calls("case-access"));
        Assert.Equal(2, reads.Calls("case-workspace"));
        Assert.All(reads.CaseIdsRead, id => Assert.Equal(RecordingReads.NewestCaseId, id));
    }

    [Fact]
    public async Task TheWorkCentreStepStartsItsThreeReadsBeforeAnyOfThemEnds()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        // From here the snapshot, the recent-Cases read and the open jobs read
        // each wait until all three have been entered.
        reads.RequireWorkCentreOverlap();
        await run.PassAsync(TimeSpan.FromMinutes(3));

        Assert.True(reads.WorkCentreReadsOverlapped);
        Assert.DoesNotContain(
            run.Logger.Entries,
            entry => entry.Message.StartsWith("Warm-up step ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AStepThatFailsInAPassDoesNotEndTheLoopOrTheNextSteps()
    {
        var reads = new RecordingReads { InboxFails = true };
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        await run.PassAsync(TimeSpan.FromMinutes(3));
        await run.PassAsync(TimeSpan.FromMinutes(3));
        reads.InboxFails = false;
        await run.PassAsync(TimeSpan.FromMinutes(3));

        // The inbox step failed twice; the steps after it and the next passes still ran.
        Assert.Equal(3, reads.Calls("mailboxes"));
        Assert.Equal(1, reads.Calls("inbox-page"));
        Assert.Equal(4, reads.Calls("snapshot"));
        Assert.Equal(4, reads.Calls("case-kind"));
        Assert.False(Assert.IsAssignableFrom<Task>(run.Warmup.ExecuteTask).IsCompleted);
    }

    [Fact]
    public async Task TheFirstFailureOfAKeepWarmStepIsAWarningAndLaterOnesAreDebug()
    {
        var reads = new RecordingReads { InboxFails = true };
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        await run.PassAsync(TimeSpan.FromMinutes(3));
        await run.PassAsync(TimeSpan.FromMinutes(3));
        await run.PassAsync(TimeSpan.FromMinutes(3));

        var inbox = run.Logger.Entries
            .Where(entry => entry.Message.StartsWith("Warm-up step inbox failed after ", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            [LogLevel.Warning, LogLevel.Debug, LogLevel.Debug],
            inbox.Select(entry => entry.Level).ToArray());
        var passes = run.Logger.Entries
            .Where(entry => entry.Message.StartsWith("Warm-up pass finished in ", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, passes.Length);
        Assert.All(passes, entry => Assert.Equal(LogLevel.Debug, entry.Level));
    }

    [Fact]
    public async Task EveryPassWarmsTheReportRenderer()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        // Once after the first pass, then once in each keep-warm pass.
        Assert.Equal(1, reads.Calls("renderer"));
        await run.PassAsync(TimeSpan.FromMinutes(3));
        await run.PassAsync(TimeSpan.FromMinutes(3));
        Assert.Equal(3, reads.Calls("renderer"));
    }

    [Fact]
    public async Task TheRendererIsWarmedOnlyOnceTheFirstPassHasEnded()
    {
        // The certificates are never ready, so the first pass waits on them.
        var store = new OAuthCertificateStore();
        var state = new StartupWarmupState(warms: true, keepWarmInterval: TimeSpan.Zero);
        var renderer = new ReadinessRecordingRenderer(state);
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton<IWarmReportRenderer>(renderer);
        await using var provider = services.BuildServiceProvider();
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(renderer.Called.IsCompleted);
        Assert.False(state.IsReady);

        store.Complete(new OAuthCertificateSet([], []));
        var readyWhenWarmed = await renderer.Called.WaitAsync(TimeSpan.FromMinutes(1));

        // Readiness was released first, so the renderer is not on its path.
        Assert.True(readyWhenWarmed);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StoppingTheHostEndsTheKeepWarmLoopAtOnce()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        await run.Warmup.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(Assert.IsAssignableFrom<Task>(run.Warmup.ExecuteTask).IsCompletedSuccessfully);
        run.Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(1, reads.Calls("snapshot"));
    }

    [Fact]
    public async Task StoppingTheHostWhileAKeepWarmStepRunsEndsThePassAndTheLoopPromptly()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        // The next pass's mailbox read waits until its token is cancelled, as a
        // SQL read does.
        var held = reads.HoldInbox();
        run.Time.Advance(TimeSpan.FromMinutes(3));
        await held.WaitAsync(TimeSpan.FromSeconds(30));

        // Well inside the step's 15 s bound, so the stop ended the step, not the bound.
        await run.Warmup.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(Assert.IsAssignableFrom<Task>(run.Warmup.ExecuteTask).IsCompletedSuccessfully);
        // The held step ended quietly and the steps after it did not start.
        Assert.Equal(0, reads.Calls("inbox-page"));
        Assert.Equal(1, reads.Calls("case-kind"));
        Assert.Equal(1, reads.Calls("renderer"));
        Assert.DoesNotContain(
            run.Logger.Entries,
            entry => entry.Message.StartsWith("Warm-up step ", StringComparison.Ordinal));
        // No pass follows.
        run.Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(2, reads.Calls("snapshot"));
    }

    [Fact]
    public async Task AFailedWorkCentreReadStillLeavesThePassTheNewestCasePage()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();

        reads.SnapshotFails = true;
        await run.PassAsync(TimeSpan.FromMinutes(3));

        // The attention read failed the Work Centre step, but the recent-Cases
        // read beside it named the newest Case, so its page was still read.
        Assert.Single(
            run.Logger.Entries,
            entry => entry.Message.StartsWith("Warm-up step work-centre failed after ", StringComparison.Ordinal));
        Assert.Equal(2, reads.Calls("case-kind"));
        Assert.Equal(2, reads.Calls("case-workspace"));
        Assert.All(reads.CaseIdsRead, id => Assert.Equal(RecordingReads.NewestCaseId, id));
    }

    [Fact]
    public async Task EachPassRunsUnderItsOwnWarmupActivityAndItsReadsShareIt()
    {
        var reads = new RecordingReads();
        await using var run = new KeepWarmRun(reads, TimeSpan.FromMinutes(3));
        await run.StartAsync();
        var afterFirstPass = reads.ActivitySeen.Length;

        await run.PassAsync(TimeSpan.FromMinutes(3));
        var afterSecondPass = reads.ActivitySeen.Length;
        await run.PassAsync(TimeSpan.FromMinutes(3));
        var stamps = reads.ActivitySeen;
        var firstKeepWarmPass = stamps[afterFirstPass..afterSecondPass];
        var secondKeepWarmPass = stamps[afterSecondPass..];

        Assert.NotEmpty(firstKeepWarmPass);
        Assert.NotEmpty(secondKeepWarmPass);
        Assert.All(firstKeepWarmPass, seen => Assert.Equal(StartupWarmup.PassActivityName, seen.Name));
        Assert.All(secondKeepWarmPass, seen => Assert.Equal(StartupWarmup.PassActivityName, seen.Name));
        Assert.Single(firstKeepWarmPass.Select(seen => seen.TraceId).Distinct());
        Assert.Single(secondKeepWarmPass.Select(seen => seen.TraceId).Distinct());
        Assert.NotEqual(firstKeepWarmPass[0].TraceId, secondKeepWarmPass[0].TraceId);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public void AHostThatDoesNotWarmIsReadyAtOnce()
    {
        Assert.True(new StartupWarmupState(warms: false).IsReady);
    }

    private readonly record struct LogEntry(LogLevel Level, string Message);

    private readonly record struct ActivityStamp(string? Name, string? TraceId);

    /// <summary>
    /// A warm-up wired to fakes and a clock the test moves: it starts the
    /// service, waits for the first pass to end and the loop to wait on its
    /// timer, and moves time on to run each keep-warm pass.
    /// </summary>
    private sealed class KeepWarmRun : IAsyncDisposable, IDisposable
    {
        private readonly ServiceProvider provider;

        public KeepWarmRun(RecordingReads reads, TimeSpan interval)
        {
            Time = new ManualTimeProvider();
            var services = new ServiceCollection();
            services.AddSingleton<IGetOperationsSnapshot>(reads);
            services.AddSingleton<IListRecentCases>(reads);
            services.AddSingleton<IGetCaseKind>(reads);
            services.AddSingleton<IGetCasePageFrame>(reads);
            services.AddSingleton<IGetAssessmentAccess>(reads);
            services.AddSingleton<IGetAssessmentWorkspace>(reads);
            services.AddSingleton<IAiJobQueries>(reads);
            services.AddSingleton<IAiDraftQueries>(reads);
            services.AddSingleton<IWarmReportRenderer>(reads);
            services.AddSingleton(new ListRetainedMail(reads));
            services.AddSingleton(new GetRetainedMailFreshness(reads, Time));
            provider = services.BuildServiceProvider();
            State = new StartupWarmupState(warms: true, keepWarmInterval: interval);
            Logger = new RecordingLogger();
            Warmup = new StartupWarmup(provider.GetRequiredService<IServiceScopeFactory>(), State, Time, Logger);
        }

        public ManualTimeProvider Time { get; }

        public StartupWarmupState State { get; }

        public RecordingLogger Logger { get; }

        public StartupWarmup Warmup { get; }

        /// <summary>Starts the service and waits until its first pass has ended and the loop is waiting.</summary>
        public async Task StartAsync()
        {
            await Warmup.StartAsync(CancellationToken.None);
            await Time.WaitForTimerAsync();
        }

        /// <summary>Moves time on and waits until the pass that follows has ended and the loop is waiting again.</summary>
        public async Task PassAsync(TimeSpan by)
        {
            Time.Advance(by);
            await Time.WaitForTimerAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Warmup.StopAsync(CancellationToken.None);
            Dispose();
        }

        public void Dispose()
        {
            Warmup.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>
    /// A clock the test moves. A delay on it ends only when <see cref="Advance"/>
    /// takes the time past it, and each timer the code under test creates is
    /// announced so the test can wait for the loop to be waiting again.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = [];
        private readonly Channel<bool> created = Channel.CreateUnbounded<bool>();
        private DateTimeOffset now = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                return now;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate)
            {
                timers.Add(timer);
            }

            timer.Change(dueTime, period);
            created.Writer.TryWrite(true);
            return timer;
        }

        /// <summary>Waits until the code under test has created a timer since the last wait.</summary>
        public async Task WaitForTimerAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await created.Reader.ReadAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The warm-up did not wait on its next timer.");
            }
        }

        /// <summary>Moves the clock on and fires every timer that has come due.</summary>
        public void Advance(TimeSpan by)
        {
            ManualTimer[] due;
            lock (gate)
            {
                now += by;
                due = timers.Where(timer => timer.IsDue(now)).ToArray();
            }

            foreach (var timer in due)
            {
                timer.Fire();
            }
        }

        private void Remove(ManualTimer timer)
        {
            lock (gate)
            {
                timers.Remove(timer);
            }
        }

        private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset? dueAt;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.gate)
                {
                    dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.now + dueTime;
                }

                return true;
            }

            public bool IsDue(DateTimeOffset at) => dueAt <= at;

            public void Fire()
            {
                lock (owner.gate)
                {
                    dueAt = null;
                }

                callback(state);
            }

            public void Dispose() => owner.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// One fake for every read a warm-up pass makes. It records what it was
    /// asked, in order, with the activity each read ran under; it can refuse
    /// the attention read or the mailbox read of the Inbox, can hold the
    /// mailbox read until its token is cancelled, and can hold the three reads
    /// of the Work Centre until all three have been started.
    /// </summary>
    private sealed class RecordingReads :
        IGetOperationsSnapshot,
        IListRecentCases,
        IGetCaseKind,
        IGetCasePageFrame,
        IGetAssessmentAccess,
        IGetAssessmentWorkspace,
        IAiJobQueries,
        IAiDraftQueries,
        IRetainedMailQueries,
        IWarmReportRenderer
    {
        internal static readonly Guid NewestCaseId = new("5c0e6b0a-3f0a-4c0e-9e0f-000000000001");

        private readonly object gate = new();
        private readonly List<string> calls = [];
        private readonly List<bool> markSeenValues = [];
        private readonly List<(MailWorkspaceScope Scope, int Page, int PageSize)> inboxPages = [];
        private readonly List<Guid> caseIds = [];
        private readonly List<ActivityStamp> activities = [];
        private TaskCompletionSource? overlap;
        private int overlapEntered;
        private TaskCompletionSource? inboxHeld;

        /// <summary>Whether the mailbox read of the Inbox throws.</summary>
        public bool InboxFails { get; set; }

        /// <summary>Whether the Work Centre's attention read throws once it has been entered.</summary>
        public bool SnapshotFails { get; set; }

        public bool[] MarkSeenValues => Snapshot(markSeenValues);

        public (MailWorkspaceScope Scope, int Page, int PageSize)[] InboxPages => Snapshot(inboxPages);

        public Guid[] CaseIdsRead => Snapshot(caseIds);

        public ActivityStamp[] ActivitySeen => Snapshot(activities);

        public bool WorkCentreReadsOverlapped => overlap is { Task.IsCompletedSuccessfully: true };

        public int Calls(string call)
        {
            lock (gate)
            {
                return calls.Count(seen => seen == call);
            }
        }

        /// <summary>From now on the snapshot, recent-Cases and open-jobs reads each wait for the other two.</summary>
        public void RequireWorkCentreOverlap()
        {
            lock (gate)
            {
                overlap = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                overlapEntered = 0;
            }
        }

        /// <summary>
        /// From now on the Inbox's mailbox read waits until its token is
        /// cancelled. The task ends when the read has been entered.
        /// </summary>
        public Task HoldInbox()
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                inboxHeld = entered;
            }

            return entered.Task;
        }

        public Task<OperationsSnapshot> ExecuteAsync(
            ActionActor actor, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<OperationsSnapshot> ExecuteAsync(
            NeedsAttentionQuery query, CancellationToken cancellationToken = default)
        {
            Note("snapshot");
            await EnterWorkCentreReadAsync();
            if (SnapshotFails)
            {
                throw new InvalidOperationException("The attention read is unreadable.");
            }

            return null!;
        }

        public async Task<RecentCasesFeed> ExecuteAsync(
            ActionActor actor, int page, bool markSeen, CancellationToken cancellationToken,
            DateTimeOffset? asOfUtc = null)
        {
            Note("recent-cases");
            lock (gate)
            {
                markSeenValues.Add(markSeen);
            }

            await EnterWorkCentreReadAsync();
            var newest = new RecentCaseRow(
                RecentCaseRowKind.NewCase, NewestCaseId, "QD-1", null, null, "Principal",
                DateTimeOffset.UnixEpoch, CaseArrival.Manual);
            return new RecentCasesFeed(
                new RecentCasesPage([newest], 1, RecentCasesPolicy.PageSize, 1),
                DateTimeOffset.UnixEpoch,
                null);
        }

        public Task<CaseType?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Note("case-kind");
            lock (gate)
            {
                caseIds.Add(caseId);
            }

            return Task.FromResult<CaseType?>(CaseType.Inspection);
        }

        public Task<CasePageFrame?> ExecuteAsync(GetCaseSectionQuery query, CancellationToken cancellationToken)
        {
            Note("case-frame");
            lock (gate)
            {
                caseIds.Add(query.CaseId);
            }

            return Task.FromResult<CasePageFrame?>(null);
        }

        public Task<AssessmentAccessState?> ExecuteAsync(
            GetAssessmentAccessQuery query, CancellationToken cancellationToken = default)
        {
            Note("case-access");
            lock (gate)
            {
                caseIds.Add(query.CaseId);
            }

            return Task.FromResult<AssessmentAccessState?>(null);
        }

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken = default)
        {
            Note("case-workspace");
            lock (gate)
            {
                caseIds.Add(query.CaseId);
            }

            return Task.FromResult<AssessmentWorkspace?>(null);
        }

        async Task<IReadOnlyList<AiJobRecord>> IAiJobQueries.ListOpenAsync(CancellationToken cancellationToken)
        {
            Note("ai-jobs");
            await EnterWorkCentreReadAsync();
            return [];
        }

        Task<AiJobQueryPage> IAiJobQueries.ListOpenPageAsync(
            AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<AiJobRecord>> IAiJobQueries.ListForSubjectAsync(
            Guid subjectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<AiJobRecord>> IAiJobQueries.ListRecentAsync(
            int max, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<AiJobCounts> IAiJobQueries.GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<AiDraft>> IAiDraftQueries.ListForCaseAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<AiDraft>> IAiDraftQueries.ListOpenAsync(CancellationToken cancellationToken)
        {
            Note("ai-drafts");
            return Task.FromResult<IReadOnlyList<AiDraft>>([]);
        }

        public Task<RetainedMailPage> ListAsync(
            MailWorkspaceScope scope, int page, int pageSize, CancellationToken cancellationToken)
        {
            Note("inbox-page");
            lock (gate)
            {
                inboxPages.Add((scope, page, pageSize));
            }

            return Task.FromResult(new RetainedMailPage([], page, pageSize, 0, false));
        }

        public async Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(CancellationToken cancellationToken)
        {
            Note("mailboxes");
            if (InboxFails)
            {
                throw new InvalidOperationException("The mailbox list is unreadable.");
            }

            TaskCompletionSource? held;
            lock (gate)
            {
                held = inboxHeld;
            }

            if (held is not null)
            {
                held.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return [];
        }

        public Task<IReadOnlyList<MailPollHealth>> ListPollHealthAsync(CancellationToken cancellationToken)
        {
            Note("mail-freshness");
            return Task.FromResult<IReadOnlyList<MailPollHealth>>([]);
        }

        public Task<RetainedMailCursorPage> ListByCursorAsync(
            MailWorkspaceScope scope, DateTimeOffset? beforeReceivedAtUtc, Guid? beforeId, int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountAsync(MailWorkspaceScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> CountManyAsync(
            IReadOnlyList<MailWorkspaceScope> scopes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedMailDetail?> GetAsync(
            Guid id, CancellationToken cancellationToken, string? searchTerm = null) =>
            throw new NotSupportedException();

        public Task<RetainedMailDetail?> GetByOriginReceiptAsync(
            Guid originReceiptId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task WarmAsync(CancellationToken cancellationToken = default)
        {
            Note("renderer");
            return Task.CompletedTask;
        }

        private void Note(string call)
        {
            var current = Activity.Current;
            lock (gate)
            {
                calls.Add(call);
                activities.Add(new ActivityStamp(current?.OperationName, current?.TraceId.ToHexString()));
            }
        }

        private async Task EnterWorkCentreReadAsync()
        {
            TaskCompletionSource? entered;
            lock (gate)
            {
                entered = overlap;
                if (entered is not null && ++overlapEntered == 3)
                {
                    entered.TrySetResult();
                }
            }

            if (entered is not null)
            {
                // Read one by one, the first of the three would wait here for ever.
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        private T[] Snapshot<T>(List<T> list)
        {
            lock (gate)
            {
                return list.ToArray();
            }
        }
    }

    private sealed class RecordingLogger : ILogger<StartupWarmup>
    {
        private readonly object gate = new();
        private readonly List<LogEntry> entries = [];

        public LogEntry[] Entries
        {
            get
            {
                lock (gate)
                {
                    return entries.ToArray();
                }
            }
        }

        public string[] Messages => Entries.Select(entry => entry.Message).ToArray();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (gate)
            {
                entries.Add(new LogEntry(logLevel, formatter(state, exception)));
            }
        }
    }

    /// <summary>A renderer warm-up that says whether the instance was ready when it was asked.</summary>
    private sealed class ReadinessRecordingRenderer(StartupWarmupState state) : IWarmReportRenderer
    {
        private readonly TaskCompletionSource<bool> called = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> Called => called.Task;

        public Task WarmAsync(CancellationToken cancellationToken = default)
        {
            called.TrySetResult(state.IsReady);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingKeyRing : IDataProtectionProvider
    {
        private int loads;

        public int Loads => Volatile.Read(ref loads);

        public IDataProtector CreateProtector(string purpose)
        {
            Interlocked.Increment(ref loads);
            return new EphemeralDataProtectionProvider().CreateProtector(purpose);
        }
    }

    private sealed class CountingSubscriptionStore : IApprovedMailboxSubscriptionStore
    {
        private int activeReads;

        public int ActiveReads => Volatile.Read(ref activeReads);

        public Task<ApprovedMailboxSubscription?> GetActiveAsync(string subscriptionId,
            DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref activeReads);
            return Task.FromResult<ApprovedMailboxSubscription?>(null);
        }

        public Task<IReadOnlyList<ApprovedMailboxSubscription>> ListAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>
            ListMaintenanceCandidatesAsync(DateTimeOffset nowUtc,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(ApprovedMailboxSubscription value,
            string? expectedPriorSubscriptionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordMaintenanceFailureAsync(Guid approvedMailboxId, long expectedGeneration,
            string? expectedSubscriptionId, string failureCode,
            DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>What SQL Server raises when a command is cancelled mid-flight.</summary>
    private sealed class CancelledCommandException() : DbException("Operation cancelled by user.");
}
