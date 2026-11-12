using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The one-minute recovery timer also runs the due-work sweep and the approved-inbox
/// recovery on every fifth minute, so they use its warm instance, and the pending-wake
/// poll on the other minutes. Every minute ends with the sent-report instruction tidy
/// and the sent-mail correspondence tidy (operator, 12 November 2026; R986). Each test
/// names the ports the steps read first, so the order of the steps is the order of the calls.
/// A step that throws never stops the others, and after all have run it fails the
/// invocation, so the failed request reaches the exception alert. With guided photo
/// capture on, the photo capture settle runs on every minute straight after the
/// dispatch (operator, 10 November 2026; R838).
/// </summary>
public sealed class PendingWorkRecoveryFunctionTests
{
    private const string IntakeDispatch = "IIntakeWorkStore.ClaimDispatchAsync";
    private const string ExternalDispatch = "IExternalWorkStore.ClaimDispatchAsync";
    private const string DueWorkSweep = "ICaseDueChaserQueries.GetDueAsync";
    private const string SubscriptionMaintenance = "IApprovedMailboxSubscriptionStore.ListMaintenanceCandidatesAsync";
    private const string InboxPoll = "IApprovedIntakeMailboxes.ListPollableAsync";
    private const string InstructionTidy = "ISentReportInstructionTidyStore.ListDueAsync";
    private const string CorrespondenceTidy = "ISentCaseCorrespondenceTidyStore.ListDueAsync";
    private const string PendingWakePoll = "IApprovedInboxPollStore.ListPendingWakeAsync";
    private const string SettleOwedBells = "IPhotoCaptureStore.ListBellsOwedAsync";
    private const string SettleReaches = "IPhotoCaptureStore.ListBatchesReachingCaseAsync";
    private const string SettleWalk = "IPhotoCaptureStore.ListAwaitingSettleAsync";

    [Fact]
    public void InfrastructureRegistersOneScopedDueChaserStoreAndCoreUseCase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure(
            (_, options) => options.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_DueChaserActivationOnly;" +
                "Integrated Security=true;Encrypt=false"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var store = scopedServices.GetRequiredService<EfCaseDueChaserStore>();

        Assert.Same(store, scopedServices.GetRequiredService<ICaseDueChaserQueries>());
        Assert.Same(store, scopedServices.GetRequiredService<ICaseDueChaserStore>());
        Assert.NotNull(scopedServices.GetRequiredService<RunDueChasers>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(55)]
    public async Task EveryFifthMinuteRunsDispatchThenSweepThenSubscriptionsThenInboxPollThenInstructionTidyThenCorrespondenceTidy(int minute)
    {
        using var rig = new Rig(minute);

        await rig.Function.RunAsync(null!, default);

        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(59)]
    public async Task OtherMinutesRunTheDispatchThenThePendingWakePollThenInstructionTidyThenCorrespondenceTidy(int minute)
    {
        using var rig = new Rig(minute);

        await rig.Function.RunAsync(null!, default);

        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, PendingWakePoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        // No wake was recorded, so the minute writes no inbox line.
        Assert.DoesNotContain(rig.Logger.Entries, entry =>
            entry.Message.Contains("approved-inbox messages", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailingPendingWakePollFailsTheInvocationAfterTheDispatch()
    {
        using var rig = new Rig(1);
        rig.PollStore.Answer("ListPendingWakeAsync", _ =>
            Task.FromException<IReadOnlyList<Guid>>(new InvalidOperationException("poll store is down")));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("poll store is down", failure.Message);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, PendingWakePoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "poll store is down" }
            && entry.Message.Contains("pending-wake poll", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADispatchThatCrossesIntoTheNextMinuteStillRunsTheFoldedJobs()
    {
        using var rig = new Rig(5);
        rig.IntakeWork.Answer("ClaimDispatchAsync", _ =>
        {
            rig.Clock.SetMinute(6);
            return Task.FromResult<IntakeWorkItem?>(null);
        });

        await rig.Function.RunAsync(null!, default);

        Assert.Contains(DueWorkSweep, rig.Calls);
        Assert.Contains(InboxPoll, rig.Calls);
    }

    [Fact]
    public async Task AFailingSweepFailsTheInvocationAfterInboxRecoveryHasRun()
    {
        using var rig = new Rig(5);
        FailSweep(rig);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("sweep failed", failure.Message);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "sweep failed" }
            && entry.Message.Contains("due-work sweep", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailingInboxRecoveryFailsTheInvocationAfterTheSweepHasRun()
    {
        using var rig = new Rig(5);
        FailInboxRecovery(rig);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("Graph is down", failure.Message);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "Graph is down" }
            && entry.Message.Contains("approved-inbox recovery", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BothFoldedJobsFailingFailTheInvocationWithBothFailuresSweepFirst()
    {
        using var rig = new Rig(5);
        FailSweep(rig);
        FailInboxRecovery(rig);

        var failure = await Assert.ThrowsAsync<AggregateException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Collection(
            failure.InnerExceptions,
            first => Assert.Equal("sweep failed", first.Message),
            second => Assert.Equal("Graph is down", second.Message));
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
    }

    [Fact]
    public async Task ADispatchFailureWinsOverAFoldedFailure()
    {
        using var rig = new Rig(5);
        FailDispatch(rig);
        FailSweep(rig);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("queue is down", failure.Message);
        Assert.Equal([IntakeDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy], rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "sweep failed" });
    }

    [Fact]
    public async Task ADispatchFailureWinsOverHostShutdownDuringAFoldedJob()
    {
        using var rig = new Rig(5);
        using var shutdown = new CancellationTokenSource();
        FailDispatch(rig);
        rig.DueQueries.Answer("GetDueAsync", _ =>
        {
            shutdown.Cancel();
            return Task.FromCanceled<IReadOnlyList<DueCaseChaser>>(shutdown.Token);
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, shutdown.Token));

        Assert.Equal("queue is down", failure.Message);
        Assert.DoesNotContain(InboxPoll, rig.Calls);
    }

    [Fact]
    public async Task ASweepOverItsBudgetIsCancelledLoggedAndFailsTheInvocationAfterInboxRecoveryHasRun()
    {
        using var rig = new Rig(5);
        var sweepStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.DueQueries.Answer("GetDueAsync", args => WaitUntilCancelled<DueCaseChaser>(args, sweepStarted));

        var run = rig.Function.RunAsync(null!, default);
        await sweepStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // The first budget belongs to the sweep: it is created before the sweep runs.
        var sweepBudget = Assert.Single(rig.Clock.Timers);
        Assert.Equal(TimeSpan.FromSeconds(60), sweepBudget.DueTime);
        await Task.Run(sweepBudget.Fire);
        // Not the wait's own timeout: this one names the job and its budget.
        var failure = await Assert.ThrowsAsync<TimeoutException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Contains("due-work sweep", failure.Message, StringComparison.Ordinal);
        Assert.Contains("60 second budget", failure.Message, StringComparison.Ordinal);
        Assert.Contains(InboxPoll, rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Exception is null
            && entry.Message.Contains("due-work sweep", StringComparison.Ordinal)
            && entry.Message.Contains("60 second budget", StringComparison.Ordinal));
        Assert.DoesNotContain(rig.Logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AnInboxRecoveryOverItsBudgetFailsTheInvocationAfterTheSweepHasRun()
    {
        using var rig = new Rig(5);
        var pollStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Mailboxes.Answer("ListPollableAsync", args => WaitUntilCancelled<ApprovedIntakeMailbox>(args, pollStarted));

        var run = rig.Function.RunAsync(null!, default);
        await pollStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // Two budgets exist by now: the sweep's, already finished, then the inbox recovery's.
        Assert.Equal(2, rig.Clock.Timers.Count);
        var inboxBudget = rig.Clock.Timers[1];
        Assert.Equal(TimeSpan.FromSeconds(60), inboxBudget.DueTime);
        await Task.Run(inboxBudget.Fire);
        // Not the wait's own timeout: this one names the job and its budget.
        var failure = await Assert.ThrowsAsync<TimeoutException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Contains("approved-inbox recovery", failure.Message, StringComparison.Ordinal);
        Assert.Contains("60 second budget", failure.Message, StringComparison.Ordinal);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("approved-inbox recovery", StringComparison.Ordinal)
            && entry.Message.Contains("60 second budget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailingDispatchStillRunsBothFoldedJobsAndThenFailsTheFunction()
    {
        using var rig = new Rig(5);
        FailDispatch(rig);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("queue is down", failure.Message);
        Assert.Equal([IntakeDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy], rig.Calls);
    }

    [Fact]
    public async Task HostShutdownStopsTheRunWithoutLoggingAFailure()
    {
        using var rig = new Rig(5);
        using var shutdown = new CancellationTokenSource();
        rig.DueQueries.Answer("GetDueAsync", _ =>
        {
            shutdown.Cancel();
            return Task.FromCanceled<IReadOnlyList<DueCaseChaser>>(shutdown.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => rig.Function.RunAsync(null!, shutdown.Token));

        Assert.DoesNotContain(InboxPoll, rig.Calls);
        Assert.DoesNotContain(rig.Logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task AFailingInstructionTidyFailsTheInvocationAfterEveryOtherStepHasRun()
    {
        using var rig = new Rig(5);
        FailInstructionTidy(rig);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("tidy failed", failure.Message);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "tidy failed" }
            && entry.Message.Contains("sent-report instruction tidy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailingSweepAndAFailingInstructionTidyAreRethrownTogetherInJobOrder()
    {
        using var rig = new Rig(5);
        FailSweep(rig);
        FailInstructionTidy(rig);

        var failure = await Assert.ThrowsAsync<AggregateException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Collection(
            failure.InnerExceptions,
            first => Assert.Equal("sweep failed", first.Message),
            second => Assert.Equal("tidy failed", second.Message));
    }

    [Fact]
    public async Task AFailingCorrespondenceTidyFailsTheInvocationAfterEveryOtherStepHasRun()
    {
        using var rig = new Rig(5);
        FailCorrespondenceTidy(rig);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("correspondence tidy failed", failure.Message);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "correspondence tidy failed" }
            && entry.Message.Contains("sent-mail correspondence tidy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailingInstructionTidyStillRunsTheCorrespondenceTidyAndBothAreRethrownInJobOrder()
    {
        using var rig = new Rig(5);
        FailInstructionTidy(rig);
        FailCorrespondenceTidy(rig);

        var failure = await Assert.ThrowsAsync<AggregateException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Collection(
            failure.InnerExceptions,
            first => Assert.Equal("tidy failed", first.Message),
            second => Assert.Equal("correspondence tidy failed", second.Message));
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, DueWorkSweep, SubscriptionMaintenance, InboxPoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
    }

    /// <summary>
    /// The settle runs on every minute, straight after the dispatch and before
    /// the minute's other jobs, so a Case moves within about a minute of Send
    /// (R838). An idle settle reads the owed bells, the reaches and the waiting
    /// captures, and nothing more.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(59)]
    public async Task ThePhotoCaptureSettleRunsAfterTheDispatchOnEveryMinute(int minute)
    {
        using var rig = new Rig(minute, new PhotoCaptureWorkerGate(true));

        await rig.Function.RunAsync(null!, default);

        string[] otherJobs = minute % 5 == 0
            ? [DueWorkSweep, SubscriptionMaintenance, InboxPoll]
            : [PendingWakePoll];
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, SettleOwedBells, SettleReaches, SettleWalk, .. otherJobs, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ThePhotoCaptureSettleDoesNotRunWithItsGateOff(int minute)
    {
        using var rig = new Rig(minute, new PhotoCaptureWorkerGate(false));

        await rig.Function.RunAsync(null!, default);

        Assert.DoesNotContain(rig.Calls, call => call.StartsWith("IPhotoCaptureStore.", StringComparison.Ordinal));
        Assert.Contains(minute == 0 ? DueWorkSweep : PendingWakePoll, rig.Calls);
    }

    [Fact]
    public async Task APhotoCaptureSettleOverItsBudgetIsCancelledAndTheMinutesOtherJobStillRuns()
    {
        using var rig = new Rig(1, new PhotoCaptureWorkerGate(true));
        var settleStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.PhotoCaptures.Answer("ListBellsOwedAsync", args => WaitUntilCancelled<PhotoCaptureBellOwed>(args, settleStarted));

        var run = rig.Function.RunAsync(null!, default);
        await settleStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // The first budget belongs to the settle: it runs before the minute's other job.
        var settleBudget = Assert.Single(rig.Clock.Timers);
        Assert.Equal(TimeSpan.FromSeconds(60), settleBudget.DueTime);
        await Task.Run(settleBudget.Fire);
        var failure = await Assert.ThrowsAsync<TimeoutException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Contains("photo-capture settle", failure.Message, StringComparison.Ordinal);
        Assert.Contains("60 second budget", failure.Message, StringComparison.Ordinal);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, SettleOwedBells, PendingWakePoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("photo-capture settle", StringComparison.Ordinal)
            && entry.Message.Contains("60 second budget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailingPhotoCaptureSettleFailsTheInvocationAfterTheMinutesOtherJob()
    {
        using var rig = new Rig(1, new PhotoCaptureWorkerGate(true));
        rig.PhotoCaptures.Answer("ListBellsOwedAsync", _ =>
            Task.FromException<IReadOnlyList<PhotoCaptureBellOwed>>(new InvalidOperationException("settle store is down")));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.Function.RunAsync(null!, default));

        Assert.Equal("settle store is down", failure.Message);
        Assert.Equal(
            [IntakeDispatch, ExternalDispatch, SettleOwedBells, PendingWakePoll, InstructionTidy, CorrespondenceTidy],
            rig.Calls);
        Assert.Contains(rig.Logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException { Message: "settle store is down" }
            && entry.Message.Contains("photo-capture settle", StringComparison.Ordinal));
    }

    private static void FailDispatch(Rig rig) =>
        rig.IntakeWork.Answer("ClaimDispatchAsync", _ =>
            Task.FromException<IntakeWorkItem?>(new InvalidOperationException("queue is down")));

    private static void FailSweep(Rig rig) =>
        rig.DueQueries.Answer("GetDueAsync", _ =>
            Task.FromException<IReadOnlyList<DueCaseChaser>>(new InvalidOperationException("sweep failed")));

    private static void FailInboxRecovery(Rig rig) =>
        rig.Mailboxes.Answer("ListPollableAsync", _ =>
            Task.FromException<IReadOnlyList<ApprovedIntakeMailbox>>(new InvalidOperationException("Graph is down")));

    private static void FailInstructionTidy(Rig rig) =>
        rig.TidyStore.Answer("ListDueAsync", _ =>
            Task.FromException<IReadOnlyList<SentReportInstructionCandidate>>(new InvalidOperationException("tidy failed")));

    private static void FailCorrespondenceTidy(Rig rig) =>
        rig.CorrespondenceTidyStore.Answer("ListDueAsync", _ =>
            Task.FromException<IReadOnlyList<SentCaseCorrespondenceCandidate>>(
                new InvalidOperationException("correspondence tidy failed")));

    private static async Task<IReadOnlyList<TItem>> WaitUntilCancelled<TItem>(
        object?[] args,
        TaskCompletionSource started)
    {
        var cancellationToken = args.OfType<CancellationToken>().Single();
        started.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }

    /// <summary>
    /// The steps built over recording ports. Nothing is answered except an empty
    /// result, so each step's first port read is the whole of its work. Given a
    /// photo capture gate, the function also holds the photo capture settle,
    /// whose idle run reads only its capture store.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        public readonly ClockAtMinute Clock;
        public readonly CallLog Log = new();
        public readonly PortRecorder IntakeWork;
        public readonly PortRecorder DueQueries;
        public readonly PortRecorder Mailboxes;
        public readonly PortRecorder PollStore;
        public readonly PortRecorder TidyStore;
        public readonly PortRecorder CorrespondenceTidyStore;
        public readonly PortRecorder PhotoCaptures;
        public readonly RecordingLogger<PendingWorkRecoveryFunction> Logger = new();
        public readonly PendingWorkRecoveryFunction Function;
        private readonly HttpClient graphClient = new(new RefusingHandler());

        public Rig(int minute, PhotoCaptureWorkerGate? photoCaptureGate = null)
        {
            Clock = new ClockAtMinute(minute);
            var intakeStore = PortRecorder.Of<IIntakeWorkStore>(Log, out IntakeWork);
            var externalStore = PortRecorder.Of<IExternalWorkStore>(Log, out _);
            var dueQueries = PortRecorder.Of<ICaseDueChaserQueries>(Log, out DueQueries);
            var dueStore = PortRecorder.Of<ICaseDueChaserStore>(Log, out _);
            var subscriptions = PortRecorder.Of<IApprovedMailboxSubscriptionStore>(Log, out _);
            var mailboxes = PortRecorder.Of<IApprovedIntakeMailboxes>(Log, out Mailboxes);
            var pollStore = PortRecorder.Of<IApprovedInboxPollStore>(Log, out PollStore);
            var tidyStore = PortRecorder.Of<ISentReportInstructionTidyStore>(Log, out TidyStore);
            var correspondenceTidyStore = PortRecorder.Of<ISentCaseCorrespondenceTidyStore>(Log, out CorrespondenceTidyStore);
            var photoCaptureStore = PortRecorder.Of<IPhotoCaptureStore>(Log, out PhotoCaptures);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Graph:ChangeNotificationUrl"] = "https://pegasus.example.test/hooks/microsoft-graph/mail",
                    ["Graph:ChangeNotificationClientState"] = "state"
                })
                .Build();
            var graph = new GraphMailboxChangeSubscriptions(
                new FixedCredential(),
                GraphApprovedMailboxOptions.Create("https://graph.microsoft.com/v1.0/"),
                graphClient);

            Function = new PendingWorkRecoveryFunction(
                new DispatchPendingWork(
                    new DispatchPendingIntakeWork(intakeStore, null!, Clock),
                    new DispatchPendingExternalWork(externalStore, null!, Clock)),
                new RunDueChasers(dueQueries, dueStore, Clock),
                new MaintainMailboxChangeSubscriptions(
                    subscriptions,
                    [graph],
                    configuration,
                    Clock,
                    NullLogger<MaintainMailboxChangeSubscriptions>.Instance),
                new PollApprovedInbox(
                    mailboxes, null!, pollStore, null!, null!, null!, null!, null!, Clock),
                new TidySentReportInstructions(tidyStore, new AvailableMover(), Clock),
                new TidySentCaseCorrespondence(correspondenceTidyStore, new AvailableMover(), Clock),
                Clock,
                Logger,
                photoCaptureGate,
                photoCaptureGate is null
                    ? null
                    : new SettlePhotoCaptures(
                        photoCaptureStore, null!, new RoutePhotoCapture(null!), null!, null!, null!, null!, null!, null!, Clock));
        }

        public string[] Calls => Log.Snapshot();

        public void Dispose() => graphClient.Dispose();
    }

    /// <summary>A reachable mailbox: the tidy reads its store, which is empty, so nothing moves.</summary>
    private sealed class AvailableMover : IRetainedMessageMover
    {
        public bool IsAvailable => true;

        public Task MoveAsync(RetainedMessageMove move, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> GetParentFolderIdAsync(string mailboxIdentity, string immutableMessageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ResolveDeletedItemsFolderIdAsync(string mailboxIdentity, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MailboxMessageLocation>> FindMessagesAsync(string mailboxIdentity, string internetMessageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>A clock stopped at one minute past ten on a fixed day, whose timers fire when a test says.</summary>
    private sealed class ClockAtMinute(int minute) : TimeProvider
    {
        private int minute = minute;

        public List<ManualTimer> Timers { get; } = [];

        public void SetMinute(int value) => minute = value;

        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 30, 10, Volatile.Read(ref minute), 3, TimeSpan.Zero);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime);
            lock (Timers)
            {
                Timers.Add(timer);
            }

            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public TimeSpan DueTime { get; } = dueTime;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

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
            lock (Entries)
            {
                Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }

    private sealed class FixedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An empty estate must not reach Microsoft Graph.");
    }
}
