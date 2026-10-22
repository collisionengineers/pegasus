using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class AssignCaseEngineerTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly Guid ActorStaffId = Guid.NewGuid();
    private static readonly ActionActor Actor =
        ActionActor.Staff(ActorStaffId, [StaffRole.User]);
    private static readonly Dictionary<Guid, string> Names = new()
    {
        [EngineerId] = "ed.mawdsley",
        [ActorStaffId] = "alex",
    };

    [Theory]
    [InlineData(false, false, "does not exist")]
    [InlineData(true, false, "is disabled")]
    public async Task IneligibleStaffCannotBeAssignedOrUnlockWork(
        bool accountExists,
        bool isEnabled,
        string expectedMessage)
    {
        var store = new RecordingWorkflowStore();
        var eligibility = new StubEligibility(
            new(accountExists, isEnabled));
        var sut = new AssignCaseEngineer(
            store,
            new FixedWorkflowConfiguration(),
            eligibility,
            new StubStaffAccounts([]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(CreateAssignmentRequest(), default));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, eligibility.CallCount);
        Assert.Equal(0, store.AssignmentCount);
        Assert.Null(store.Current.AssignedEngineerId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StartCaseWork(store, eligibility).ExecuteAsync(
                new ChangeCaseStateRequest(
                    CaseId,
                    0,
                    Actor,
                    "start-after-rejected-assignment",
                    "Attempt to start after rejected assignment",
                    "lease-token"),
                default));
    }

    [Fact]
    public async Task NativeHandoffEntersReportPreparationAndExactReplayDoesNotRecheckEligibility()
    {
        var store = new RecordingWorkflowStore();
        var eligibility = new StubEligibility(new(true, true));
        var sut = new AssignCaseEngineer(
            store,
            new FixedWorkflowConfiguration(),
            eligibility,
            new StubStaffAccounts([Profile(EngineerId, isDefault: false)]));
        var request = CreateAssignmentRequest();

        var assigned = await sut.ExecuteAsync(request, default);
        eligibility.Current = new(true, false);
        // The replay's own reason is replaced too, so it composes the same
        // request the first call committed and replays rather than conflicts.
        var replay = await sut.ExecuteAsync(request with { Reason = "A different reason" }, default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(EngineerId, assigned.SignOffEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, assigned.State);
        Assert.Equal(1L, assigned.Version);
        Assert.Equal(assigned, replay);
        Assert.Equal(1, eligibility.CallCount);
        Assert.Equal(2, store.AssignmentCount);
        Assert.Equal("Assigned to ed.mawdsley", store.AppliedRequest!.Reason);
    }

    /// <summary>
    /// #1213 rulings 1 and 3 (operator, 15 October 2026): a Review or a With
    /// Engineer Case is handed to a named Engineer with no typed reason, and
    /// Core records who it went to in place of the caller's words.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    public async Task ANamedEngineerIsAssignedFromReviewOrWithEngineerAndHistoryNamesThem(CaseLifecycleState from)
    {
        var store = new RecordingWorkflowStore(from);
        var eligibility = new StubEligibility(new(true, true));
        var sut = Assign(store, eligibility);

        var assigned = await sut.ExecuteAsync(CreateAssignmentRequest() with { Reason = string.Empty }, default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, assigned.State);
        Assert.Equal(1, eligibility.CallCount);
        Assert.Equal(EngineerId, eligibility.LastStaffId);
        Assert.Equal("Assigned to ed.mawdsley", store.AppliedRequest!.Reason);
    }

    /// <summary>Take is the signed-in person assigning themself (v41 W8, K9).</summary>
    [Fact]
    public async Task TakeAssignsTheActorAndHistoryReadsTakenBy()
    {
        var store = new RecordingWorkflowStore(CaseLifecycleState.ReportPreparation);
        var eligibility = new StubEligibility(new(true, true));
        var sut = Assign(store, eligibility);

        var taken = await sut.ExecuteAsync(
            CreateAssignmentRequest() with { EngineerId = ActorStaffId, Reason = "Assign Engineer" },
            default);

        Assert.Equal(ActorStaffId, taken.AssignedEngineerId);
        Assert.Equal(ActorStaffId, eligibility.LastStaffId);
        Assert.Equal("Taken by alex", store.AppliedRequest!.Reason);
    }

    /// <summary>
    /// Unassigned is a target (#1213): no account to check, the Case lands
    /// With Engineer with no Engineer, and the line has no full stop (R161 a).
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    public async Task UnassignedChecksNoAccountAndHistoryReadsSetToUnassigned(CaseLifecycleState from)
    {
        var defaultSignOffEngineerId = Guid.NewGuid();
        var store = new RecordingWorkflowStore(from);
        var eligibility = new StubEligibility(new(true, true));
        var sut = new AssignCaseEngineer(
            store,
            new FixedWorkflowConfiguration(),
            eligibility,
            new StubStaffAccounts([Profile(defaultSignOffEngineerId, isDefault: true)]));

        var unassigned = await sut.ExecuteAsync(
            CreateAssignmentRequest() with { EngineerId = null, Reason = string.Empty },
            default);

        Assert.Null(unassigned.AssignedEngineerId);
        Assert.Equal(defaultSignOffEngineerId, unassigned.SignOffEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, unassigned.State);
        Assert.Equal(0, eligibility.CallCount);
        Assert.Equal("Set to Unassigned", store.AppliedRequest!.Reason);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.ReportSent)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    public async Task EveryOtherStateRefusesTheHandoff(CaseLifecycleState from)
    {
        var store = new RecordingWorkflowStore(from);
        var eligibility = new StubEligibility(new(true, true));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Assign(store, eligibility).ExecuteAsync(CreateAssignmentRequest(), default));

        Assert.Equal(
            "A staff member can be assigned only while the case is in Review or With Engineer.",
            refusal.Message);
        Assert.Equal(0, store.AssignmentCount);
        Assert.Equal(0, eligibility.CallCount);
    }

    [Fact]
    public async Task AnEmptyEngineerIdentifierIsRefusedAndAnEmptyReasonIsNot()
    {
        var store = new RecordingWorkflowStore();
        var sut = Assign(store, new StubEligibility(new(true, true)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(CreateAssignmentRequest() with { EngineerId = Guid.Empty }, default));
        var assigned = await sut.ExecuteAsync(CreateAssignmentRequest() with { Reason = string.Empty }, default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(1, store.AssignmentCount);
    }

    private static AssignCaseEngineer Assign(RecordingWorkflowStore store, StubEligibility eligibility) =>
        new(
            store,
            new FixedWorkflowConfiguration(),
            eligibility,
            new StubStaffAccounts([Profile(EngineerId, isDefault: false)]));

    [Fact]
    public async Task UnflaggedAssignedEngineerDefaultsToAdministratorDesignatedSignOffEngineer()
    {
        var defaultSignOffEngineerId = Guid.NewGuid();
        var store = new RecordingWorkflowStore();
        var sut = new AssignCaseEngineer(
            store,
            new FixedWorkflowConfiguration(),
            new StubEligibility(new(true, true)),
            new StubStaffAccounts([Profile(defaultSignOffEngineerId, isDefault: true)]));

        var assigned = await sut.ExecuteAsync(CreateAssignmentRequest(), default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(defaultSignOffEngineerId, assigned.SignOffEngineerId);
    }

    /// <summary>
    /// Work Centre D10 cause 2: the engineer is told once, on the assignment that
    /// committed, never again on an exact replay.
    /// </summary>
    [Fact]
    public async Task AssignmentRaisesTheEngineersNotificationOnceAndNotOnReplay()
    {
        var store = new RecordingWorkflowStore();
        var notifier = new RecordingNotifier();
        var sut = new AssignCaseEngineer(
            store,
            new FixedWorkflowConfiguration(),
            new StubEligibility(new(true, true)),
            new StubStaffAccounts([Profile(EngineerId, isDefault: false)]),
            notifier);
        var request = CreateAssignmentRequest();

        await sut.ExecuteAsync(request, default);
        await sut.ExecuteAsync(request, default);

        var raised = Assert.Single(notifier.Raised);
        Assert.Equal((Pegasus.Core.Notifications.StaffNotificationCause.CaseAssigned, CaseId), raised);
        Assert.Equal((store.Current.Identity.Reference, (Guid?)EngineerId), notifier.Passed);
    }

    private sealed class RecordingNotifier : Pegasus.Core.Notifications.ICaseStaffNotifier
    {
        public List<(Pegasus.Core.Notifications.StaffNotificationCause Cause, Guid CaseId)> Raised { get; } = [];

        public (string Reference, Guid? EngineerId)? Passed { get; private set; }

        public Task<Pegasus.Core.Notifications.StaffNotification?> NotifyAsync(
            Pegasus.Core.Notifications.StaffNotificationCause cause, Guid caseId, string reference, Guid? assignedEngineerId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            Raised.Add((cause, caseId));
            Passed = (reference, assignedEngineerId);
            return Task.FromResult<Pegasus.Core.Notifications.StaffNotification?>(null);
        }

        public Task<Pegasus.Core.Notifications.StaffNotification?> NotifyAsync(
            Pegasus.Core.Notifications.StaffNotificationCause cause, Guid caseId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            Raised.Add((cause, caseId));
            return Task.FromResult<Pegasus.Core.Notifications.StaffNotification?>(null);
        }

        public Task<Pegasus.Core.Notifications.StaffNotification?> NotifyMailArrivalAsync(Guid caseId, Guid intakeReceiptId, ActionActor? actor, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Pegasus.Core.Notifications.StaffNotification?> NotifyCancellationReceivedAsync(Guid caseId, ActionActor? actor, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Pegasus.Core.Notifications.StaffNotification?> NotifyAiDraftReadyAsync(Pegasus.Core.AiWork.AiJobRecord job, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static SignOffEngineerProfile Profile(Guid staffId, bool isDefault) =>
        new(staffId, "Named Signatory", null, isDefault);

    private static AssignCaseEngineerRequest CreateAssignmentRequest() =>
        new(
            CaseId,
            0,
            Actor,
            "assign-engineer",
            "Assign eligible Engineer",
            "lease-token",
            EngineerId);

    private sealed class StubEligibility(CaseEngineerEligibility current)
        : ICaseEngineerEligibility
    {
        public CaseEngineerEligibility Current { get; set; } = current;

        public int CallCount { get; private set; }

        public Guid? LastStaffId { get; private set; }

        public Task<CaseEngineerEligibility> GetAsync(
            Guid staffId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastStaffId = staffId;
            return Task.FromResult(Current);
        }
    }

    private sealed class RecordingWorkflowStore(CaseLifecycleState state = CaseLifecycleState.Review)
        : ICaseWorkflowStore
    {
        private string? _appliedOperationKey;

        public AssignCaseEngineerRequest? AppliedRequest { get; private set; }

        public CaseWorkflowRecord Current { get; private set; } = new(
            CaseId,
            new(CaseId, "QDOS", 2026, 1, "QDOS260001"),
            state,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0);

        public int AssignmentCount { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(
            Guid caseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? Current : null);
        }

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(
            IReadOnlyCollection<Guid> caseIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasOperationAsync(
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                caseId == CaseId
                && string.Equals(_appliedOperationKey, operationKey, StringComparison.Ordinal));
        }

        public Task<CaseWorkflowRecord> AssignEngineerAsync(
            AssignCaseEngineerRequest request,
            Guid? signOffEngineerId,
            CaseLifecycleState targetState,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(CaseLifecycleState.ReportPreparation, targetState);
            AssignmentCount++;
            if (_appliedOperationKey is not null)
            {
                if (AppliedRequest != request)
                {
                    throw new InvalidOperationException("The operation key was reused for different input.");
                }

                return Task.FromResult(Current);
            }

            _appliedOperationKey = request.OperationKey;
            AppliedRequest = request;
            Current = Current with
            {
                AssignedEngineerId = request.EngineerId,
                SignOffEngineerId = signOffEngineerId,
                State = targetState,
                Version = Current.Version + 1
            };
            return Task.FromResult(Current);
        }

        public Task<CaseEditLease> ClaimAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> RenewAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> HeartbeatAsync(
            HeartbeatCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease?> ResumeAsync(
            ResumeCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ChangeStateAsync(
            CaseMutationRequest request,
            CaseLifecycleState targetState,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> HoldAsync(
            PutCaseOnHoldRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReleaseHoldAsync(
            CaseMutationRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReturnToReviewAsync(
            ReturnCaseToReviewRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(
            RecordCaseReportApprovalRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LinkReportEvidenceResult> LinkReportEvidenceAsync(
            LinkReportEvidenceRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(
            UnlinkReportEvidenceRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> CloseAsync(
            CloseCaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReopenAsync(
            ReopenCaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReturnToEngineerAsync(
            ReturnCaseToEngineerRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> RaiseQueryAsync(
            RaiseCaseQueryRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ResolveQueriesAsync(
            ResolveCaseQueriesRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedWorkflowConfiguration : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new CaseWorkflowConfiguration(PolicyKey: "case-workflow", PolicyVersion: 1));
    }

    private sealed class StubStaffAccounts(IReadOnlyList<SignOffEngineerProfile> profiles)
        : IStaffAccountQueries
    {
        public Task<SignOffSignature?> GetSignOffSignatureAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) => Task.FromResult(profiles);

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StaffAccountSummary?> GetAsync(
            Guid staffId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        // The composed history reason names the Engineer through the one
        // name read every history reader uses.
        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
            IReadOnlyCollection<Guid> staffIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccountSummary>>(
            [
                .. staffIds
                    .Where(Names.ContainsKey)
                    .Select(id => new StaffAccountSummary(id, Names[id], true, false, StaffRole.Engineer))
            ]);
    }
}
