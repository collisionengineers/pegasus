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

    /// <summary>
    /// The use case reads no Case of its own (eff-07-l, register A3-06): the store refuses
    /// the state, reads the Engineer's eligibility and resolves the Sign-off Engineer under
    /// its lock, and answers a replay from its record. Core composes the history reason.
    /// #1213 rulings 1 and 3 (operator, 15 October 2026): a Review or a With Engineer Case
    /// is handed to a named Engineer with no typed reason, and Core records who it went to
    /// in place of the caller's words.
    /// </summary>
    [Fact]
    public async Task AHandOffIsOneStoreCallWithCoresReasonInPlaceOfTheCallers()
    {
        var store = new RecordingWorkflowStore();
        var sut = Assign(store);

        var assigned = await sut.ExecuteAsync(CreateAssignmentRequest() with { Reason = string.Empty }, default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, assigned.State);
        Assert.Equal(1, store.AssignmentCount);
        Assert.Equal("Assigned to ed.mawdsley", store.AppliedRequest!.Reason);
    }

    /// <summary>Take is the signed-in person assigning themself (v41 W8, K9).</summary>
    [Fact]
    public async Task TakeAssignsTheActorAndHistoryReadsTakenBy()
    {
        var store = new RecordingWorkflowStore();

        var taken = await Assign(store).ExecuteAsync(
            CreateAssignmentRequest() with { EngineerId = ActorStaffId, Reason = "Assign Engineer" },
            default);

        Assert.Equal(ActorStaffId, taken.AssignedEngineerId);
        Assert.Equal("Taken by alex", store.AppliedRequest!.Reason);
    }

    /// <summary>
    /// Unassigned is a target (#1213): the Case lands With Engineer with no Engineer,
    /// and the line has no full stop (R161 a).
    /// </summary>
    [Fact]
    public async Task UnassignedHistoryReadsSetToUnassigned()
    {
        var store = new RecordingWorkflowStore();

        var unassigned = await Assign(store).ExecuteAsync(
            CreateAssignmentRequest() with { EngineerId = null, Reason = string.Empty },
            default);

        Assert.Null(unassigned.AssignedEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, unassigned.State);
        Assert.Equal("Set to Unassigned", store.AppliedRequest!.Reason);
    }

    /// <summary>
    /// The replay's own reason is replaced too, so it composes the same request the first
    /// call committed and replays rather than conflicts.
    /// </summary>
    [Fact]
    public async Task AnExactReplayComposesTheSameRequestAndReturnsTheCommittedRecord()
    {
        var store = new RecordingWorkflowStore();
        var sut = Assign(store);
        var request = CreateAssignmentRequest();

        var assigned = await sut.ExecuteAsync(request, default);
        var replay = await sut.ExecuteAsync(request with { Reason = "A different reason" }, default);

        Assert.Equal(1L, assigned.Version);
        Assert.Equal(assigned, replay);
        Assert.Equal(2, store.AssignmentCount);
        Assert.Equal("Assigned to ed.mawdsley", store.AppliedRequest!.Reason);
    }

    [Fact]
    public async Task AnEmptyEngineerIdentifierIsRefusedAndAnEmptyReasonIsNot()
    {
        var store = new RecordingWorkflowStore();
        var sut = Assign(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(CreateAssignmentRequest() with { EngineerId = Guid.Empty }, default));
        var assigned = await sut.ExecuteAsync(CreateAssignmentRequest() with { Reason = string.Empty }, default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(1, store.AssignmentCount);
    }

    /// <summary>
    /// Work Centre D10 cause 2: the engineer is told once, on the assignment that
    /// committed, never again on an exact replay (the store says which it was).
    /// </summary>
    [Fact]
    public async Task AssignmentRaisesTheEngineersNotificationOnceAndNotOnReplay()
    {
        var store = new RecordingWorkflowStore();
        var notifier = new RecordingNotifier();
        var sut = new AssignCaseEngineer(
            store,
            new FixedWorkflowConfiguration(),
            new StubStaffAccounts([]),
            notifier);
        var request = CreateAssignmentRequest();

        await sut.ExecuteAsync(request, default);
        await sut.ExecuteAsync(request, default);

        var raised = Assert.Single(notifier.Raised);
        Assert.Equal((Pegasus.Core.Notifications.StaffNotificationCause.CaseAssigned, CaseId), raised);
        Assert.Equal((store.Current.Identity.Reference, (Guid?)EngineerId), notifier.Passed);
    }

    /// <summary>The words each Case command's state refusal has always used, now asked by the store.</summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.ReportSent)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    public void EveryOtherStateRefusesTheHandoff(CaseLifecycleState from)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => CaseLifecycleRules.RequireCanBeAssigned(from));

        Assert.Equal(
            "A staff member can be assigned only while the case is in Review or With Engineer.",
            refusal.Message);
    }

    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    public void ReviewAndWithEngineerAcceptTheHandoff(CaseLifecycleState from) =>
        CaseLifecycleRules.RequireCanBeAssigned(from);

    [Theory]
    [InlineData(CaseLifecycleState.Review, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.NotReady, true)]
    public void StartingWorkNeedsReviewAndAnAssignedEngineer(CaseLifecycleState from, bool assigned)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => CaseLifecycleRules.RequireCanStartCaseWork(from, assigned ? EngineerId : null));

        Assert.Equal("Case work can start only from Review after a staff member is assigned.", refusal.Message);
    }

    [Fact]
    public void ReviewWithAnEngineerMayStart() =>
        CaseLifecycleRules.RequireCanStartCaseWork(CaseLifecycleState.Review, EngineerId);

    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    public void OnlyReportSentReturnsToEngineer(CaseLifecycleState from)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => CaseLifecycleRules.RequireCanReturnToEngineer(from));

        Assert.Equal("A case can return to Engineer only from Report sent.", refusal.Message);
        CaseLifecycleRules.RequireCanReturnToEngineer(CaseLifecycleState.ReportSent);
    }

    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportSent)]
    public void OnlyATerminalCaseCanBeArchived(CaseLifecycleState from)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => CaseLifecycleRules.RequireCanBeArchived(from));

        Assert.Equal("Only a terminal case can be archived.", refusal.Message);
        CaseLifecycleRules.RequireCanBeArchived(CaseLifecycleState.PrincipalCancelled);
    }

    [Fact]
    public void ArchiveReadinessAsksCustodyThenExternalWork()
    {
        const string custody = "A case can be archived only after its required custody is confirmed.";
        const string work = "A case cannot be archived while required durable work is incomplete or unrecognized work exists.";

        Assert.Equal(
            custody,
            Assert.Throws<InvalidOperationException>(
                () => CaseLifecycleRules.RequireArchiveReadiness(new CaseArchiveReadiness(false, false))).Message);
        Assert.Equal(
            custody,
            Assert.Throws<InvalidOperationException>(
                () => CaseLifecycleRules.RequireArchiveReadiness(new CaseArchiveReadiness(false, true))).Message);
        Assert.Equal(
            work,
            Assert.Throws<InvalidOperationException>(
                () => CaseLifecycleRules.RequireArchiveReadiness(new CaseArchiveReadiness(true, true))).Message);
        CaseLifecycleRules.RequireArchiveReadiness(new CaseArchiveReadiness(true, false));
    }

    [Fact]
    public void UnflaggedAssignedEngineerDefaultsToAdministratorDesignatedSignOffEngineer()
    {
        var defaultSignOffEngineerId = Guid.NewGuid();

        var resolved = CaseSignOffEngineerResolver.Resolve(
            null,
            EngineerId,
            [Profile(defaultSignOffEngineerId, isDefault: true)]);

        Assert.Equal(defaultSignOffEngineerId, resolved?.StaffId);
        Assert.Equal(
            EngineerId,
            CaseSignOffEngineerResolver.Resolve(
                null, EngineerId, [Profile(EngineerId, isDefault: false), Profile(defaultSignOffEngineerId, isDefault: true)])?.StaffId);
    }

    private static AssignCaseEngineer Assign(RecordingWorkflowStore store) =>
        new(
            store,
            new FixedWorkflowConfiguration(),
            new StubStaffAccounts([Profile(EngineerId, isDefault: false)]));

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

    private sealed class RecordingWorkflowStore : ICaseWorkflowStore
    {
        private string? _appliedOperationKey;

        public AssignCaseEngineerRequest? AppliedRequest { get; private set; }

        public CaseWorkflowRecord Current { get; private set; } = new(
            CaseId,
            new(CaseId, "QDOS", 2026, 1, "QDOS260001"),
            CaseLifecycleState.Review,
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
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The use case reads no Case of its own.");

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(
            IReadOnlyCollection<Guid> caseIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasOperationAsync(
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The use case asks no replay probe of its own.");

        public Task<CaseEngineerAssignment> AssignEngineerAsync(
            AssignCaseEngineerRequest request,
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

                return Task.FromResult(new CaseEngineerAssignment(Current, true));
            }

            _appliedOperationKey = request.OperationKey;
            AppliedRequest = request;
            Current = Current with
            {
                AssignedEngineerId = request.EngineerId,
                State = targetState,
                Version = Current.Version + 1
            };
            return Task.FromResult(new CaseEngineerAssignment(Current, false));
        }

        public Task<CaseEditLease> ClaimAsync(
            ClaimCaseEditLeaseRequest request,
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

        public Task<CaseWorkflowRecord> StartWorkAsync(
            CaseMutationRequest request,
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

        public Task<Guid> OpenQueryAsync(OpenCaseQueryRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> MarkQueryDoneAsync(CaseQueryActRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReopenQueryAsync(CaseQueryActRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> AssignQueryAsync(AssignCaseQueryRequest request, CancellationToken cancellationToken) =>
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
