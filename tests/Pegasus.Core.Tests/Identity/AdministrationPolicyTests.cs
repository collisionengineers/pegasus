using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Identity;

public sealed class AdministrationPolicyTests
{
    [Fact]
    public async Task WorkflowConfigurationQueryRequiresAdministrator()
    {
        var store = new WorkflowStore();
        var query = new GetWorkflowConfiguration(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => query.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            default));

        Assert.Equal(0, store.ReadCount);
    }

    [Fact]
    public async Task WorkflowConfigurationUpdateRequiresAdministrator()
    {
        var store = new WorkflowStore();
        var command = new UpdateWorkflowConfiguration(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            new(
                false,
                true,
                false,
                true,
                1,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                "Attempted gate change",
                "workflow-denied"),
            default));

        Assert.Null(store.UpdateRequest);
    }

    [Fact]
    public async Task WorkflowConfigurationUpdateCarriesExpectedVersionReasonAndActor()
    {
        var store = new WorkflowStore();
        var command = new UpdateWorkflowConfiguration(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        var updated = await command.ExecuteAsync(
            new(false, true, false, true, 1, actor, "  Reviewed gates  ", "  workflow-op  "),
            default);

        Assert.Equal(2, updated.PolicyVersion);
        var request = Assert.IsType<UpdateWorkflowConfigurationRequest>(store.UpdateRequest);
        Assert.Same(actor, request.Actor);
        Assert.Equal(1, request.ExpectedVersion);
        Assert.Equal("Reviewed gates", request.Reason);
        Assert.Equal("workflow-op", request.OperationKey);
    }

    [Fact]
    public async Task ApprovedMailboxQueryRequiresAdministrator()
    {
        var store = new MailboxStore();
        var query = new ListApprovedMailboxes(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => query.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            default));

        Assert.Equal(0, store.ListCount);
    }

    [Fact]
    public async Task ApprovedMailboxUpdateRequiresAdministrator()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                [ApprovedMailboxRouteScope.InboundIntake],
                ApprovedMailboxState.Approved,
                1,
                ActionActor.SystemWorker("mailbox-policy-test"),
                "Attempted mailbox change",
                "mailbox-denied"),
            default));

        Assert.Null(store.UpdateRequest);
    }

    [Fact]
    public async Task ApprovedMailboxUpdateNormalizesAddressAndRetainsExplicitScopes()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var mailboxId = Guid.NewGuid();

        var updated = await command.ExecuteAsync(
            new(
                mailboxId,
                "  Instructions@CollisionEngineers.co.uk  ",
                [ApprovedMailboxRouteScope.SentEvidence, ApprovedMailboxRouteScope.InboundIntake],
                ApprovedMailboxState.Approved,
                0,
                actor,
                "  Approve the fixed routes  ",
                "  mailbox-op  "),
            default);

        Assert.Equal("instructions@collisionengineers.co.uk", updated.Address);
        var request = Assert.IsType<UpdateApprovedMailboxRequest>(store.UpdateRequest);
        Assert.Equal(
            new[]
            {
                ApprovedMailboxRouteScope.InboundIntake,
                ApprovedMailboxRouteScope.SentEvidence
            },
            request.RouteScopes);
        Assert.Equal("Approve the fixed routes", request.Reason);
        Assert.Equal("mailbox-op", request.OperationKey);
    }

    [Fact]
    public async Task ApprovedMailboxUpdateRejectsUnsupportedAddressBeforeStore()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);

        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(
            new(
                Guid.NewGuid(),
                "not a mailbox",
                [ApprovedMailboxRouteScope.InboundIntake],
                ApprovedMailboxState.Approved,
                0,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "Approve inbound intake",
                "mailbox-op"),
            default));

        Assert.Null(store.UpdateRequest);
    }

    private sealed class WorkflowStore : IWorkflowConfigurationStore
    {
        public int ReadCount { get; private set; }

        public UpdateWorkflowConfigurationRequest? UpdateRequest { get; private set; }

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Current(1));
        }

        public Task<CaseWorkflowConfiguration> UpdateAsync(
            UpdateWorkflowConfigurationRequest request,
            CancellationToken cancellationToken)
        {
            UpdateRequest = request;
            return Task.FromResult(Current(request.ExpectedVersion + 1));
        }

        private static CaseWorkflowConfiguration Current(int version) =>
            new(true, true, true, true, "case-workflow", version);
    }

    private sealed class MailboxStore : IApprovedMailboxStore
    {
        public int ListCount { get; private set; }

        public UpdateApprovedMailboxRequest? UpdateRequest { get; private set; }

        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken)
        {
            ListCount++;
            return Task.FromResult<IReadOnlyList<ApprovedMailbox>>([]);
        }

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request,
            CancellationToken cancellationToken)
        {
            UpdateRequest = request;
            return Task.FromResult(new ApprovedMailbox(
                request.MailboxId,
                request.Address,
                request.RouteScopes.ToArray(),
                request.State,
                request.ExpectedVersion + 1));
        }

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
