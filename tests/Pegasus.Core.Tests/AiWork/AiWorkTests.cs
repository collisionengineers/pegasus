using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.AiWork;

public sealed class AiWorkTests
{
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public void CreateValidationRefusesAutomationSendersAndLongInstructions()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AiWorkPolicy.ValidateCreate(new(
                Guid.NewGuid(),
                "CE-QDOS-31-00001",
                0,
                ActionActor.Automation("pegasus-automation"),
                "op",
                "Work the case.",
                TimeSpan.FromHours(24))));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiWorkPolicy.ValidateCreate(new(
                Guid.NewGuid(),
                "CE-QDOS-31-00001",
                0,
                Staff,
                "op",
                new string('x', AiWorkPolicy.MaximumInstructionLength + 1),
                TimeSpan.FromHours(24))));
    }

    [Fact]
    public void CancellingRequiresAReason()
    {
        Assert.Throws<ArgumentException>(() =>
            AiWorkPolicy.ValidateTransition(new(
                Guid.NewGuid(),
                0,
                AiWorkRequestState.Cancelled,
                Staff,
                "op")));
    }

    [Theory]
    [InlineData(AiWorkRequestState.Created, AiWorkRequestState.HandedOff, true)]
    [InlineData(AiWorkRequestState.Created, AiWorkRequestState.Completed, false)]
    [InlineData(AiWorkRequestState.HandedOff, AiWorkRequestState.Completed, true)]
    [InlineData(AiWorkRequestState.HandedOff, AiWorkRequestState.Expired, true)]
    [InlineData(AiWorkRequestState.Completed, AiWorkRequestState.Failed, false)]
    [InlineData(AiWorkRequestState.Cancelled, AiWorkRequestState.HandedOff, false)]
    public void TheTransitionGraphIsClosed(
        AiWorkRequestState from,
        AiWorkRequestState to,
        bool legal) =>
        Assert.Equal(legal, AiWorkPolicy.IsLegalTransition(from, to));

    [Fact]
    public async Task SendRefusesWhenTheAdministratorSwitchIsOff()
    {
        var harness = new Harness { ControlEnabled = false };
        var result = await harness.SendAsync();
        Assert.Equal(SendCaseToAiOutcome.NotEligible, result.Outcome);
        Assert.Contains(result.Reasons, reason =>
            reason.Contains("disabled", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(harness.Store.Records);
    }

    [Fact]
    public async Task SendRefusesTerminalStateCases()
    {
        var harness = new Harness { CaseState = CaseLifecycleState.PostReportComplete };
        var result = await harness.SendAsync();
        Assert.Equal(SendCaseToAiOutcome.NotEligible, result.Outcome);
    }

    [Fact]
    public async Task SendRefusesWhileAnotherRequestIsInFlight()
    {
        var harness = new Harness();
        var first = await harness.SendAsync("op-1");
        Assert.Equal(SendCaseToAiOutcome.HandedOff, first.Outcome);
        var second = await harness.SendAsync("op-2");
        Assert.Equal(SendCaseToAiOutcome.NotEligible, second.Outcome);
        Assert.Contains(second.Reasons, reason =>
            reason.Contains("already in flight", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAcceptedHandOffBecomesHandedOffWithThePointerOnly()
    {
        var harness = new Harness();
        var result = await harness.SendAsync();
        Assert.Equal(SendCaseToAiOutcome.HandedOff, result.Outcome);
        Assert.Equal(AiWorkRequestState.HandedOff, result.Request!.State);
        var pointer = Assert.Single(harness.Transport.HandOffs);
        Assert.Equal("CE-QDOS-31-00001", pointer.CaseReference);
        Assert.Equal(AiWorkPolicy.SchemaVersion, pointer.SchemaVersion);
        Assert.Equal(result.Request.RequestId.ToString("D"), pointer.RequestId);
    }

    [Fact]
    public async Task ARefusedChannelIsAVisibleFailureWithTheCaseUnchanged()
    {
        var harness = new Harness
        {
            Transport = { HandOffResult = new(AiHandOffOutcomeKind.Refused, "401") }
        };
        var result = await harness.SendAsync();
        Assert.Equal(SendCaseToAiOutcome.Failed, result.Outcome);
        Assert.Equal(AiWorkRequestState.Failed, result.Request!.State);
    }

    [Fact]
    public async Task ReconcileCompletesOnADoneReplyAndFailsOnAFailedReply()
    {
        var harness = new Harness();
        var sent = await harness.SendAsync();
        harness.Transport.Reply = new("done", "Assessment recorded.", null);
        var completed = await harness.ReconcileAsync(sent.Request!.RequestId);
        Assert.Equal(AiWorkRequestState.Completed, completed.State);
        Assert.Equal("Assessment recorded.", completed.ReplyMessage);

        var second = await harness.SendAsync("op-2");
        harness.Transport.Reply = new("failed", "Could not read the case.", null);
        var failed = await harness.ReconcileAsync(second.Request!.RequestId);
        Assert.Equal(AiWorkRequestState.Failed, failed.State);
    }

    [Fact]
    public async Task ReconcileWithoutAReplyLeavesTheRequestHandedOff()
    {
        var harness = new Harness();
        var sent = await harness.SendAsync();
        var unchanged = await harness.ReconcileAsync(sent.Request!.RequestId);
        Assert.Equal(AiWorkRequestState.HandedOff, unchanged.State);
    }

    [Fact]
    public async Task ReconcileExpiresAnOverdueRequest()
    {
        var harness = new Harness();
        var sent = await harness.SendAsync();
        harness.Now += TimeSpan.FromHours(25);
        var expired = await harness.ReconcileAsync(sent.Request!.RequestId);
        Assert.Equal(AiWorkRequestState.Expired, expired.State);
    }

    [Fact]
    public async Task CancellationClosesTheTrackingRecordOnly()
    {
        var harness = new Harness();
        var sent = await harness.SendAsync();
        var cancel = new CancelAiWorkRequest(harness.Store);
        var cancelled = await cancel.ExecuteAsync(
            new(sent.Request!.RequestId, Staff, "cancel-op", "No longer needed"),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.Cancelled, cancelled.State);
        Assert.Equal("No longer needed", cancelled.ClosureReason);
    }

    private sealed class Harness
    {
        public FakeStore Store { get; } = new();
        public FakeTransport Transport { get; } = new();
        public bool ControlEnabled { get; init; } = true;
        public CaseLifecycleState CaseState { get; init; } = CaseLifecycleState.Review;
        public DateTimeOffset Now { get; set; } = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        public Task<SendCaseToAiResult> SendAsync(string operationKey = "op-1")
        {
            var send = new SendCaseToAi(
                new FakeCaseData(CaseState),
                Store,
                Transport,
                new FakeControl(ControlEnabled),
                new FakeTime(() => Now));
            return send.ExecuteAsync(
                new(CaseId, Staff, operationKey, "Work the assessment."),
                CancellationToken.None);
        }

        public Task<AiWorkRequestRecord> ReconcileAsync(Guid requestId)
        {
            var reconcile = new ReconcileAiWorkRequest(
                Store,
                Transport,
                new FakeTime(() => Now));
            return reconcile.ExecuteAsync(
                new(requestId, Staff, Guid.NewGuid().ToString("N")),
                CancellationToken.None);
        }

        private static readonly Guid CaseId = Guid.NewGuid();
    }

    private sealed class FakeTime(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now();
    }

    private sealed class FakeControl(bool enabled) : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult(enabled);

        public Task<bool> SetEnabledAsync(
            bool value,
            ActionActor actor,
            string reason,
            string operationKey,
            CancellationToken cancellationToken) => Task.FromResult(value);
    }

    private sealed class FakeTransport : IAiHandOffTransport
    {
        public List<AiHandOffPointer> HandOffs { get; } = [];
        public AiHandOffResult HandOffResult { get; set; } =
            new(AiHandOffOutcomeKind.Accepted, null);
        public AiChannelReply? Reply { get; set; }

        public Task<AiHandOffResult> HandOffAsync(
            AiHandOffPointer handOff,
            CancellationToken cancellationToken)
        {
            HandOffs.Add(handOff);
            return Task.FromResult(HandOffResult);
        }

        public Task<AiChannelReply?> TryReadReplyAsync(
            string requestId,
            CancellationToken cancellationToken) => Task.FromResult(Reply);
    }

    private sealed class FakeStore : IAiWorkRequestStore
    {
        public List<AiWorkRequestRecord> Records { get; } = [];

        public Task<AiWorkRequestRecord> CreateAsync(
            CreateAiWorkRequestCommand command,
            CancellationToken cancellationToken)
        {
            AiWorkPolicy.ValidateCreate(command);
            var created = new AiWorkRequestRecord(
                Guid.NewGuid(),
                command.CaseId,
                command.CaseReference,
                command.CaseVersion,
                AiWorkPolicy.CapabilityScope,
                command.Instruction,
                AiWorkRequestState.Created,
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
                command.Actor.SubjectId,
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero) + command.Expiry,
                null,
                null,
                null,
                null,
                null,
                0);
            Records.Add(created);
            return Task.FromResult(created);
        }

        public Task<AiWorkRequestRecord?> GetAsync(
            Guid requestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Records.FirstOrDefault(record => record.RequestId == requestId));

        public Task<AiWorkRequestRecord?> GetLatestForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Records.LastOrDefault(record => record.CaseId == caseId));

        public Task<AiWorkRequestRecord> TransitionAsync(
            AiWorkRequestTransition transition,
            CancellationToken cancellationToken)
        {
            AiWorkPolicy.ValidateTransition(transition);
            var index = Records.FindIndex(record => record.RequestId == transition.RequestId);
            var current = Records[index];
            if (current.State == transition.TargetState)
            {
                return Task.FromResult(current);
            }
            if (!AiWorkPolicy.IsLegalTransition(current.State, transition.TargetState))
            {
                throw new InvalidOperationException("Illegal transition.");
            }

            var updated = current with
            {
                State = transition.TargetState,
                ClosureReason = transition.Reason ?? current.ClosureReason,
                ReplyStatus = transition.ReplyStatus,
                ReplyMessage = transition.ReplyMessage,
                Version = current.Version + 1
            };
            Records[index] = updated;
            return Task.FromResult(updated);
        }
    }

    private sealed class FakeCaseData(CaseLifecycleState state) : ICaseDataQueries
    {
        public Task<CaseDataProjection?> GetAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseDataProjection?>(new(
                new(caseId, "QDOS", 2031, 1, "CE-QDOS-31-00001", null),
                new(
                    Guid.NewGuid(),
                    IntakeSourceChannel.ManualUpload,
                    "token",
                    new string('a', 64),
                    DateTimeOffset.UnixEpoch,
                    "reader",
                    "1",
                    null,
                    null),
                DateTimeOffset.UnixEpoch,
                0,
                state,
                new(new(true, true, true, true), new(true, "policy", 1)),
                new(Empty<string>()),
                new(Empty<string>()),
                new(Empty<string>()),
                new(
                    Empty<string>(),
                    Empty<string>(),
                    Empty<string>(),
                    Empty<long>(),
                    Empty<string>()),
                new(Empty<DateOnly>(), Empty<string>()),
                new(Empty<string>(), Empty<string>(), Empty<string>()),
                new(Empty<DateOnly>(), Empty<string>()),
                new(
                    Empty<DateOnly>(),
                    Empty<DateOnly>(),
                    Empty<string>(),
                    Empty<CaseInspectionMode>())));

        private static CaseField<T> Empty<T>()
            where T : notnull => new(null, null, null);
    }
}
