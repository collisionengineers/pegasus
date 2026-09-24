using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class AssignToMeTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid TriageCaseId = Guid.NewGuid();
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task EveryStaffRoleTakesAnUnassignedReviewCaseAsThemself(StaffRole role)
    {
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [role]);
        var assign = new RecordingAssign();
        var sut = new AssignCaseToMe(new Queries(Workflow(CaseLifecycleState.Review, null)), assign);

        await sut.ExecuteAsync(new(CaseId, 3, actor, "take-1", "lease-token"), default);

        var request = Assert.Single(assign.Requests);
        Assert.Equal(staffId, request.EngineerId);
        Assert.Same(actor, request.Actor);
        Assert.Equal(3, request.ExpectedVersion);
        Assert.Equal("take-1", request.OperationKey);
        Assert.Equal("lease-token", request.EditLeaseToken);
        Assert.Equal(AssignCaseToMe.Reason, request.Reason);
    }

    [Fact]
    public async Task ACaseWithAnEngineerIsNotTaken()
    {
        var assign = new RecordingAssign();
        var sut = new AssignCaseToMe(new Queries(Workflow(CaseLifecycleState.Review, Guid.NewGuid())), assign);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new(CaseId, 3, Staff(StaffRole.Engineer), "take-3", "lease-token"), default));

        Assert.Contains("already has an assigned staff member", exception.Message, StringComparison.Ordinal);
        Assert.Empty(assign.Requests);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
    public async Task OnlyAReviewCaseCanBeTaken(CaseLifecycleState state)
    {
        var assign = new RecordingAssign();
        var sut = new AssignCaseToMe(new Queries(Workflow(state, null)), assign);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new(CaseId, 3, Staff(StaffRole.Engineer), "take-4", "lease-token"), default));

        Assert.Empty(assign.Requests);
        Assert.False(CaseLifecycleRules.CanAssignToSelf(Workflow(state, null)));
    }

    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task EveryStaffRoleTakesAnUnassignedOpenTriage(StaffRole role)
    {
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [role]);
        var assign = new RecordingTriageAssign();
        var sut = new AssignTriageToMe(new TriageQueries(Triage(TriageState.Open, null)), assign);

        await sut.ExecuteAsync(new(TriageCaseId, 2, actor, "take-t1") { EditLeaseToken = "lease" }, default);

        var request = Assert.Single(assign.Requests);
        Assert.Equal(staffId, request.AssigneeId);
        Assert.Equal(2, request.ExpectedVersion);
        Assert.Equal("lease", request.EditLeaseToken);
        Assert.Equal(AssignTriageToMe.Reason, request.Reason);
    }

    [Fact]
    public async Task AnAssignedOrSettledTriageIsNotTaken()
    {
        var assign = new RecordingTriageAssign();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AssignTriageToMe(new TriageQueries(Triage(TriageState.Open, Guid.NewGuid())), assign)
                .ExecuteAsync(new(TriageCaseId, 2, Staff(StaffRole.Engineer), "take-t2"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AssignTriageToMe(new TriageQueries(Triage(TriageState.Completed, null)), assign)
                .ExecuteAsync(new(TriageCaseId, 2, Staff(StaffRole.Engineer), "take-t3"), default));

        Assert.Empty(assign.Requests);
        Assert.True(TriageLifecycleRules.CanAssignToSelf(Triage(TriageState.Open, null)));
        Assert.False(TriageLifecycleRules.CanAssignToSelf(Triage(TriageState.Cancelled, null)));
    }

    private static CaseWorkflowRecord Workflow(CaseLifecycleState state, Guid? engineerId) => new(
        CaseId,
        new(CaseId, "QDOS", 2026, 1, "QDOS260001"),
        state,
        engineerId,
        null,
        null,
        null,
        null,
        null,
        null,
        3);

    private static ActionActor Staff(StaffRole role) =>
        ActionActor.Staff(Guid.NewGuid(), [role]);

    private static TriageRecord Triage(TriageState state, Guid? assigneeId) => new(
        TriageCaseId,
        new(Guid.NewGuid(), new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "token"), new string('a', 64), Guid.NewGuid()),
        "AB12CDE",
        state,
        assigneeId,
        LinkedInstructionCaseId: null,
        2,
        "t.QDOS26001",
        Guid.NewGuid());

    private sealed class Queries(CaseWorkflowRecord current) : ICaseWorkflowQueries
    {
        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseWorkflowRecord?>(caseId == current.CaseId ? current : null);

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class RecordingAssign : IAssignCaseEngineer
    {
        public List<AssignCaseEngineerRequest> Requests { get; } = [];

        public Task<CaseWorkflowRecord> ExecuteAsync(AssignCaseEngineerRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Workflow(CaseLifecycleState.ReportPreparation, request.EngineerId));
        }
    }

    private sealed class TriageQueries(TriageRecord current) : ITriageQueries
    {
        public Task<IReadOnlyList<TriageSummary>> ListAsync(TriageState? state, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountAsync(TriageState? state, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<TriageDetail?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<TriageDetail?>(caseId == current.CaseId
                ? new TriageDetail(current, DateTimeOffset.UnixEpoch, [], [], [], [])
                : null);

        public Task<TriageSummary?> GetByOriginReceiptAsync(Guid originReceiptId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTriageAssign : IAssignTriage
    {
        public List<AssignTriageRequest> Requests { get; } = [];

        public Task<TriageRecord> ExecuteAsync(AssignTriageRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Triage(TriageState.Open, request.AssigneeId));
        }
    }
}
