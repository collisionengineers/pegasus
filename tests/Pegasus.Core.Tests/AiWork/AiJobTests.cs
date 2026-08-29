using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.AiWork;

public sealed class AiJobTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor Client = ActionActor.Automation("pegasus-automation");

    [Theory]
    [InlineData(AiJobState.Queued, AiJobState.Taken, true)]
    [InlineData(AiJobState.Queued, AiJobState.DraftReady, false)]
    [InlineData(AiJobState.Taken, AiJobState.Taken, true)]
    [InlineData(AiJobState.Taken, AiJobState.Queued, true)]
    [InlineData(AiJobState.Taken, AiJobState.DraftReady, true)]
    [InlineData(AiJobState.Taken, AiJobState.Completed, false)]
    [InlineData(AiJobState.DraftReady, AiJobState.Completed, true)]
    [InlineData(AiJobState.DraftReady, AiJobState.Cancelled, true)]
    [InlineData(AiJobState.Completed, AiJobState.Queued, false)]
    [InlineData(AiJobState.Failed, AiJobState.Taken, false)]
    [InlineData(AiJobState.Cancelled, AiJobState.Taken, false)]
    public void TheTransitionGraphIsClosed(AiJobState from, AiJobState to, bool legal) =>
        Assert.Equal(legal, AiJobPolicy.IsLegalTransition(from, to));

    [Fact]
    public void ALapsedLeaseReadsAsQueuedAndAnUntakenJobPastExpiryReadsAsExpired()
    {
        var expires = Now + AiJobPolicy.DefaultExpiry;
        Assert.Equal(
            AiJobState.Taken,
            AiJobPolicy.EffectiveState(AiJobState.Taken, expires, Now + TimeSpan.FromMinutes(1), Now));
        Assert.Equal(
            AiJobState.Queued,
            AiJobPolicy.EffectiveState(AiJobState.Taken, expires, Now, Now));
        Assert.Equal(
            AiJobState.Queued,
            AiJobPolicy.EffectiveState(AiJobState.Taken, Now, Now, Now));
        Assert.Equal(
            AiJobState.Expired,
            AiJobPolicy.EffectiveState(AiJobState.Queued, Now, null, Now));
        Assert.Equal(
            AiJobState.DraftReady,
            AiJobPolicy.EffectiveState(AiJobState.DraftReady, Now, Now, Now));
    }

    [Fact]
    public void TheAutomationActorCreatesOnlyQueuePasses()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AiJobPolicy.ValidateNew(NewJob(AiJobKind.QueryResponse, Client)));
        Assert.Contains("Unidentified-queue pass", exception.Message, StringComparison.Ordinal);
        AiJobPolicy.ValidateNew(NewJob(AiJobKind.UnidentifiedQueuePass, Client));
        Assert.Throws<StaffAuthorizationException>(() =>
            AiJobPolicy.ValidateNew(NewJob(
                AiJobKind.UnidentifiedQueuePass,
                ActionActor.SystemWorker("worker"))));
    }

    [Fact]
    public void AnEstimateJobNeedsAnEngineerValueAndATargetBetweenOneAndOneHundred()
    {
        var caseId = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiJobPolicy.ValidateNew(NewJob(AiJobKind.Estimate, Staff, caseId, target: 0, engineerValue: 5000m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiJobPolicy.ValidateNew(NewJob(AiJobKind.Estimate, Staff, caseId, target: 101, engineerValue: 5000m)));
        Assert.Throws<InvalidOperationException>(() =>
            AiJobPolicy.ValidateNew(NewJob(AiJobKind.Estimate, Staff, caseId, target: 60, engineerValue: null)));
        AiJobPolicy.ValidateNew(NewJob(AiJobKind.Estimate, Staff, caseId, target: 60, engineerValue: 5000m));
    }

    [Fact]
    public void ClientTransitionsAreAutomationOnlyAndStaffTransitionsAreStaffOnly()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.Taken, Staff, "op", LeaseExpiresAtUtc: Now)));
        Assert.Throws<InvalidOperationException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.Cancelled, Client, "op", "reason")));
        Assert.Throws<ArgumentException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.Cancelled, Staff, "op")));
        Assert.Throws<ArgumentException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.Failed, Client, "op")));
        Assert.Throws<ArgumentException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.DraftReady, Client, "op")));
        Assert.Throws<ArgumentException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.Taken, Client, "op")));
        Assert.Throws<ArgumentException>(() =>
            AiJobPolicy.ValidateTransition(new(
                Guid.NewGuid(), 0, AiJobState.Taken, Client, "op", ProgressNote: " ", LeaseExpiresAtUtc: Now)));
        AiJobPolicy.ValidateTransition(new(
            Guid.NewGuid(), 0, AiJobState.DraftReady, Client, "op",
            Result: new(AiJobResultKind.DraftReply, null, "Draft reply text.")));
        Assert.Throws<ArgumentException>(() =>
            AiJobPolicy.ValidateResult(new(AiJobResultKind.Estimate, null, null)));
    }

    [Fact]
    public async Task CreationAndClaimsAreRefusedWhileTheAdministratorSwitchIsOff()
    {
        var harness = new Harness { ControlEnabled = false };
        var create = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Create.ExecuteAsync(
                new(AiJobKind.UnidentifiedQueuePass, null, null, "Pass the queue.", null, Client, "op-1"),
                CancellationToken.None));
        Assert.Contains("disabled", create.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Transitions);

        var take = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Work.TakeAsync(new(Guid.NewGuid(), 0, Client, "op-2"), CancellationToken.None));
        Assert.Contains("disabled", take.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Work.ReportProgressAsync(
                new(Guid.NewGuid(), 0, Client, "op-3", "Working."),
                CancellationToken.None));

        // Finishing held work stays permitted so a stopped client is never
        // stranded holding a job.
        await harness.Work.FailAsync(new(Guid.NewGuid(), 0, Client, "op-4", "Gave up."), CancellationToken.None);
        var failed = Assert.Single(harness.Store.Transitions);
        Assert.Equal(AiJobState.Failed, failed.TargetState);
    }

    [Fact]
    public async Task ATakeCarriesAThirtyMinuteLeaseAndProgressRenewsIt()
    {
        var harness = new Harness();
        await harness.Work.TakeAsync(new(Guid.NewGuid(), 0, Client, "op-1"), CancellationToken.None);
        await harness.Work.ReportProgressAsync(
            new(Guid.NewGuid(), 1, Client, "op-2", "Half way."),
            CancellationToken.None);
        Assert.All(
            harness.Store.Transitions,
            transition =>
            {
                Assert.Equal(AiJobState.Taken, transition.TargetState);
                Assert.Equal(Now + AiJobPolicy.LeaseDuration, transition.LeaseExpiresAtUtc);
            });
        Assert.Equal("Half way.", harness.Store.Transitions[1].ProgressNote);
    }

    [Fact]
    public async Task EstimateJobsNeedAWithEngineerCaseAndCaptureTheConfirmedEngineerValue()
    {
        var harness = new Harness { CaseState = CaseLifecycleState.Review, EngineerValue = ("4200.00", true) };
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Create.ExecuteAsync(EstimateCommand(harness.CaseId), CancellationToken.None));
        Assert.Contains("With Engineer", refused.Message, StringComparison.Ordinal);

        harness = new Harness { EngineerValue = ("4200.00", false) };
        var unconfirmed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Create.ExecuteAsync(EstimateCommand(harness.CaseId), CancellationToken.None));
        Assert.Contains("Engineer's Value", unconfirmed.Message, StringComparison.Ordinal);

        harness = new Harness { EngineerValue = ("4200.00", true) };
        var created = await harness.Create.ExecuteAsync(EstimateCommand(harness.CaseId), CancellationToken.None);
        Assert.Equal(AiJobKind.Estimate, created.Kind);
        Assert.Equal(AiJobSubjectKind.Case, created.SubjectKind);
        Assert.Equal("CE-QDOS-31-00001", created.SubjectReference);
        Assert.Equal(4200m, created.EngineerValueAtSend);
        Assert.Equal(60, created.TargetPercentOfEngineerValue);
    }

    [Fact]
    public async Task QueryResponseJobsNeedPostReportWorkAndUnidentifiedJobsNeedAnOpenItem()
    {
        var harness = new Harness { CaseState = CaseLifecycleState.ReportPreparation };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Create.ExecuteAsync(
                new(AiJobKind.QueryResponse, harness.CaseId, null, "Draft a reply to message M-1.", null, Staff, "op-1"),
                CancellationToken.None));

        harness = new Harness { CaseState = CaseLifecycleState.PostReport };
        var query = await harness.Create.ExecuteAsync(
            new(AiJobKind.QueryResponse, harness.CaseId, null, "Draft a reply to message M-1.", null, Staff, "op-1"),
            CancellationToken.None);
        Assert.Equal(AiJobKind.QueryResponse, query.Kind);

        harness = new Harness { UnidentifiedState = UnidentifiedState.Resolved };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Create.ExecuteAsync(
                new(AiJobKind.UnidentifiedResolution, null, "U17", "Propose a destination.", null, Staff, "op-2"),
                CancellationToken.None));

        harness = new Harness();
        var resolution = await harness.Create.ExecuteAsync(
            new(AiJobKind.UnidentifiedResolution, null, "U17", "Propose a destination.", null, Staff, "op-2"),
            CancellationToken.None);
        Assert.Equal(AiJobSubjectKind.Unidentified, resolution.SubjectKind);
        Assert.Equal("U17", resolution.SubjectReference);
        Assert.Equal(harness.UnidentifiedId, resolution.SubjectId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.Create.ExecuteAsync(
                new(AiJobKind.UnidentifiedResolution, null, "U99", "Propose a destination.", null, Staff, "op-3"),
                CancellationToken.None));
    }

    private static CreateAiJobCommand EstimateCommand(Guid caseId) =>
        new(AiJobKind.Estimate, caseId, null, "Draft to target.", 60, Staff, "op-estimate");

    private static NewAiJob NewJob(
        AiJobKind kind,
        ActionActor actor,
        Guid? subjectId = null,
        int? target = null,
        decimal? engineerValue = null) =>
        new(
            kind,
            AiJobPolicy.SubjectKindFor(kind),
            kind == AiJobKind.UnidentifiedQueuePass ? null : subjectId ?? Guid.NewGuid(),
            kind == AiJobKind.UnidentifiedQueuePass ? AiJobPolicy.QueueSubjectReference : "REF-1",
            "Do the work.",
            target,
            engineerValue,
            actor,
            "op",
            AiJobPolicy.DefaultExpiry);

    private sealed class Harness
    {
        public Guid CaseId { get; } = Guid.NewGuid();
        public Guid UnidentifiedId { get; } = Guid.NewGuid();
        public bool ControlEnabled { get; init; } = true;
        public CaseLifecycleState CaseState { get; init; } = CaseLifecycleState.ReportPreparation;
        public (string Value, bool Confirmed)? EngineerValue { get; init; }
        public UnidentifiedState UnidentifiedState { get; init; } = UnidentifiedState.Open;
        public FakeStore Store { get; } = new();

        public CreateAiJob Create => new(
            Store,
            new FakeControl(ControlEnabled),
            new FakeWorkflow(CaseId, CaseState),
            new FakeAssessment(CaseId, CaseState, EngineerValue),
            new FakeUnidentified(UnidentifiedId, UnidentifiedState));

        public WorkAiJob Work => new(Store, new FakeControl(ControlEnabled), new FakeTime());
    }

    private sealed class FakeTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeControl(bool enabled) : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(enabled);

        public Task<bool> SetEnabledAsync(
            bool enabled,
            ActionActor actor,
            string reason,
            string operationKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeStore : IAiJobStore
    {
        public List<AiJobTransition> Transitions { get; } = [];

        public Task<AiJobRecord> CreateAsync(NewAiJob job, CancellationToken cancellationToken)
        {
            AiJobPolicy.ValidateNew(job);
            return Task.FromResult(new AiJobRecord(
                Guid.NewGuid(),
                job.Kind,
                job.SubjectKind,
                job.SubjectId,
                job.SubjectReference,
                job.Instruction,
                job.TargetPercentOfEngineerValue,
                job.EngineerValueAtSend,
                AiJobState.Queued,
                job.Actor.Kind,
                job.Actor.SubjectId,
                Now,
                Now + job.Expiry,
                null, null, null, null, null, null, null, null, null,
                0));
        }

        public Task<AiJobRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken) =>
            Task.FromResult<AiJobRecord?>(null);

        public Task<AiJobRecord> TransitionAsync(AiJobTransition transition, CancellationToken cancellationToken)
        {
            AiJobPolicy.ValidateTransition(transition);
            Transitions.Add(transition);
            return Task.FromResult(new AiJobRecord(
                transition.JobId,
                AiJobKind.UnidentifiedQueuePass,
                AiJobSubjectKind.Queue,
                null,
                AiJobPolicy.QueueSubjectReference,
                "Pass the queue.",
                null,
                null,
                transition.TargetState,
                ActorKind.Automation,
                "pegasus-automation",
                Now,
                Now + AiJobPolicy.DefaultExpiry,
                transition.Actor.SubjectId,
                Now,
                transition.LeaseExpiresAtUtc,
                transition.ProgressNote,
                transition.Result?.Kind,
                transition.Result?.Reference,
                transition.Result?.Text,
                null,
                transition.Reason,
                transition.ExpectedVersion + 1));
        }
    }

    private sealed class FakeWorkflow(Guid caseId, CaseLifecycleState state) : ICaseWorkflowQueries
    {
        public Task<CaseWorkflowRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<CaseWorkflowRecord?>(id == caseId
                ? new(
                    caseId,
                    new CaseIdentity(caseId, "QDOS", 31, 1, "CE-QDOS-31-00001"),
                    state,
                    null, null, null, null, null, null, null,
                    3)
                : null);

        public Task<bool> HasOperationAsync(Guid id, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class FakeAssessment(
        Guid caseId,
        CaseLifecycleState state,
        (string Value, bool Confirmed)? engineerValue) : ICaseAssessmentStore
    {
        public Task<CaseAssessmentProjection?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id != caseId)
            {
                return Task.FromResult<CaseAssessmentProjection?>(null);
            }

            IReadOnlyList<AssessmentFieldValue> fields = engineerValue is { } value
                ?
                [
                    new AssessmentFieldValue(
                        AssessmentVocabulary.ValueEngineer,
                        value.Value,
                        ActorKind.Staff,
                        "engineer",
                        Now,
                        value.Confirmed ? "engineer" : null,
                        value.Confirmed ? Now : null)
                ]
                : [];
            return Task.FromResult<CaseAssessmentProjection?>(new(
                caseId,
                "CE-QDOS-31-00001",
                3,
                state,
                null,
                fields,
                [],
                new(null, null, null, null, null, null, null, null, null)));
        }

        public Task<CaseAssessmentProjection> SaveAsync(
            SaveAssessmentRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeUnidentified(Guid id, UnidentifiedState state) : IUnidentifiedStore
    {
        private UnidentifiedItem Item => new(
            id,
            17,
            "U17",
            UnidentifiedOrigin.Receipt(Guid.NewGuid()),
            UnidentifiedReasonCode.NoUsableIdentification,
            "Recorded safe detail.",
            state,
            Now,
            null,
            ActionActor.SystemWorker("intake-processing"),
            null, null, null, null, null,
            0);

        public Task<UnidentifiedItem?> GetAsync(Guid itemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidentifiedItem?>(itemId == id ? Item : null);

        public Task<UnidentifiedItem?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidentifiedItem?>(reference == "U17" ? Item : null);

        public Task<UnidentifiedRegisterResult> RegisterAsync(RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedReopenResult> ReopenAsync(ReopenUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByOriginAsync(UnidentifiedOrigin origin, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(UnidentifiedState? state = UnidentifiedState.Open, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedItem>> ListResolutionsToRecheckAsync(int maximum, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(UnidentifiedMediaKind? mediaKind, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(Guid unidentifiedItemId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
