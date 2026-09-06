using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
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
            new(1, actor, "  Reviewed policy  ", "  workflow-op  "),
            default);

        Assert.Equal(2, updated.PolicyVersion);
        var request = Assert.IsType<UpdateWorkflowConfigurationRequest>(store.UpdateRequest);
        Assert.Same(actor, request.Actor);
        Assert.Equal(1, request.ExpectedVersion);
        Assert.Equal("Reviewed policy", request.Reason);
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
                "  mailbox-op  ",
                "  mailbox-identity  ",
                "  inbox-folder  ",
                "  sent-folder  ",
                [
                    new(MailLogicalFolderType.Billing, "  billing-folder  "),
                    new(MailLogicalFolderType.Instructions, "instructions-folder")
                ]),
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
        Assert.Equal("mailbox-identity", request.MailboxIdentity);
        Assert.Equal("inbox-folder", request.InboxFolderIdentity);
        Assert.Equal("sent-folder", request.SentFolderIdentity);
        Assert.Collection(
            request.FolderBindings!,
            item =>
            {
                Assert.Equal(MailLogicalFolderType.Instructions, item.FolderType);
                Assert.Equal("instructions-folder", item.FolderIdentity);
            },
            item =>
            {
                Assert.Equal(MailLogicalFolderType.Billing, item.FolderType);
                Assert.Equal("billing-folder", item.FolderIdentity);
            });
        Assert.True(updated.IdentityIsBound);
    }

    [Fact]
    public async Task StaffSendCannotBeEnabledWithoutAdministratorVerifiedSizeCeiling()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);
        var request = new UpdateApprovedMailboxRequest(
            Guid.NewGuid(),
            "mail@collisionengineers.co.uk",
            [ApprovedMailboxRouteScope.StaffSend],
            ApprovedMailboxState.Approved,
            0,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
            "Enable staff send",
            "mailbox-send",
            "mailbox-id");

        var missing = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(() =>
            command.ExecuteAsync(request, default));
        Assert.Equal(ApprovedMailboxUpdateError.MissingVerifiedSendLimit, missing.Error);
        Assert.Null(store.UpdateRequest);

        await command.ExecuteAsync(
            request with { VerifiedEncodedMessageSizeLimit = 25_000_000 }, default);
        Assert.Equal(25_000_000, store.UpdateRequest!.VerifiedEncodedMessageSizeLimit);
    }

    [Fact]
    public async Task ApprovedMailboxRejectsDuplicateOrInexactFolderBindingsBeforeStore()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);
        var request = new UpdateApprovedMailboxRequest(
            Guid.NewGuid(),
            "instructions@collisionengineers.co.uk",
            [ApprovedMailboxRouteScope.InboundIntake],
            ApprovedMailboxState.Approved,
            0,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
            "Approve inbound intake",
            "mailbox-op",
            "mailbox-identity",
            "inbox-folder",
            null,
            [
                new(MailLogicalFolderType.Instructions, "folder-one"),
                new(MailLogicalFolderType.Instructions, "folder-two")
            ]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => command.ExecuteAsync(request, default));
        var invalidIdentity = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => command.ExecuteAsync(
                request with
                {
                    FolderBindings = [new(MailLogicalFolderType.Instructions, "has space")]
                },
                default));

        Assert.Equal(ApprovedMailboxUpdateError.InvalidMailboxIdentity, invalidIdentity.Error);
        Assert.Null(store.UpdateRequest);
    }

    [Theory]
    [InlineData(null, "inbox-folder", "sent-folder")]
    [InlineData("mailbox-identity", null, "sent-folder")]
    [InlineData("mailbox-identity", "inbox-folder", null)]
    public async Task ApprovedMailboxCannotBeApprovedWithoutTheIdentitiesItsRoutesNeed(
        string? mailboxIdentity,
        string? inboxFolderIdentity,
        string? sentFolderIdentity)
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);

        var exception = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(() =>
            command.ExecuteAsync(
                new(
                    Guid.NewGuid(),
                    "instructions@collisionengineers.co.uk",
                    [ApprovedMailboxRouteScope.InboundIntake, ApprovedMailboxRouteScope.SentEvidence],
                    ApprovedMailboxState.Approved,
                    0,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    "Approve both routes",
                    "mailbox-op",
                    mailboxIdentity,
                    inboxFolderIdentity,
                    sentFolderIdentity),
                default));

        Assert.Equal(ApprovedMailboxUpdateError.MissingMailboxIdentity, exception.Error);
        Assert.Null(store.UpdateRequest);
    }

    [Fact]
    public async Task DisabledMailboxIsSavedWhileItsTenantIdentitiesAreStillAwaited()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);

        var saved = await command.ExecuteAsync(
            new(
                Guid.NewGuid(),
                "later@collisionengineers.co.uk",
                [ApprovedMailboxRouteScope.InboundIntake],
                ApprovedMailboxState.Disabled,
                0,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "Awaiting the tenant application access policy",
                "mailbox-op"),
            default);

        Assert.Equal(ApprovedMailboxState.Disabled, saved.State);
        Assert.Null(saved.MailboxIdentity);
        Assert.False(saved.IdentityIsBound);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has\ttab")]
    public async Task ApprovedMailboxRejectsAnIdentityThatIsNotExact(string mailboxIdentity)
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);

        var exception = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(() =>
            command.ExecuteAsync(
                new(
                    Guid.NewGuid(),
                    "instructions@collisionengineers.co.uk",
                    [ApprovedMailboxRouteScope.InboundIntake],
                    ApprovedMailboxState.Approved,
                    0,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    "Approve inbound intake",
                    "mailbox-op",
                    mailboxIdentity,
                    "inbox-folder"),
                default));

        Assert.Equal(ApprovedMailboxUpdateError.InvalidMailboxIdentity, exception.Error);
        Assert.Null(store.UpdateRequest);
    }

    [Fact]
    public async Task ApprovedMailboxRejectsAMailboxIdentityLongerThanItsCursorKey()
    {
        var store = new MailboxStore();
        var command = new UpdateApprovedMailbox(store);

        var exception = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(() =>
            command.ExecuteAsync(
                new(
                    Guid.NewGuid(),
                    "instructions@collisionengineers.co.uk",
                    [ApprovedMailboxRouteScope.InboundIntake],
                    ApprovedMailboxState.Approved,
                    0,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    "Approve inbound intake",
                    "mailbox-op",
                    new string('m', 101),
                    "inbox-folder"),
                default));

        Assert.Equal(ApprovedMailboxUpdateError.InvalidMailboxIdentity, exception.Error);
        Assert.Null(store.UpdateRequest);
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
            new("case-workflow", version);
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
                request.MailboxIdentity,
                request.InboxFolderIdentity,
                request.SentFolderIdentity,
                request.MailboxIdentity is not null,
                request.State == ApprovedMailboxState.Approved ? DateTimeOffset.UtcNow : null,
                request.ExpectedVersion + 1,
                request.FolderBindings?.ToArray() ?? []));
        }

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
