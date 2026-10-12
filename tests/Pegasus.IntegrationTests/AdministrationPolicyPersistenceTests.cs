using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AdministrationPolicyPersistenceTests
{
    [Fact]
    public async Task WorkflowConfigurationStoresOnlyTheReadOnlyPolicyIdentity()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var query = scope.ServiceProvider.GetRequiredService<GetWorkflowConfiguration>();
        var configuration = await query.ExecuteAsync(actor, default);

        Assert.Equal("case-workflow", configuration.PolicyKey);
        Assert.Equal(1, configuration.PolicyVersion);

        await using var context = await database.CreateContextAsync();
        Assert.Equal(
            0,
            await context.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS [Value] FROM [ActionHistory] WHERE [AggregateType] = 'workflow_configuration'")
                .SingleAsync());
    }

    [Fact]
    public async Task WorkflowConfigurationUpdateIsVersionedAuditedAndReplaySafe()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var query = scope.ServiceProvider.GetRequiredService<GetWorkflowConfiguration>();
        var command = scope.ServiceProvider.GetRequiredService<UpdateWorkflowConfiguration>();
        var initial = await query.ExecuteAsync(actor, default);
        var request = new UpdateWorkflowConfigurationRequest(
            initial.PolicyVersion,
            actor,
            "workflow-policy-update-1");

        var updated = await command.ExecuteAsync(request, default);
        var replay = await command.ExecuteAsync(request, default);

        Assert.Equal(initial.PolicyVersion + 1, updated.PolicyVersion);
        Assert.Equal(updated, replay);
        await Assert.ThrowsAsync<WorkflowConfigurationVersionConflictException>(
            () => command.ExecuteAsync(
                request with
                {
                    OperationKey = "workflow-policy-stale-1"
                },
                default));

        await using var context = await database.CreateContextAsync();
        var history = await context.ActionHistory.SingleAsync(
            item => item.AggregateType == "workflow_configuration");
        Assert.Null(history.Reason);
        Assert.NotNull(history.BeforeJson);
        Assert.NotNull(history.AfterJson);
    }

    [Fact]
    public async Task ApprovedMailboxUpdateControlsOnlyItsVersionedReadRoutes()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var list = scope.ServiceProvider.GetRequiredService<ListApprovedMailboxes>();
        var command = scope.ServiceProvider.GetRequiredService<UpdateApprovedMailbox>();
        var policy = scope.ServiceProvider.GetRequiredService<IApprovedMailboxPolicy>();
        var initial = Assert.Single(await list.ExecuteAsync(actor, default));
        var request = new UpdateApprovedMailboxRequest(
            initial.Id,
            initial.Address,
            [ApprovedMailboxRouteScope.Incoming],
            ApprovedMailboxState.Approved,
            initial.Version,
            actor,
            "approved-mailbox-update-1",
            // Approving a row now requires the exact tenant identities its routes read.
            "instructions-mailbox",
            "instructions-inbox",
            "instructions-sent");

        var updated = await command.ExecuteAsync(request, default);
        var replay = await command.ExecuteAsync(request, default);

        Assert.Equal(initial.Version + 1, updated.Version);
        Assert.Equal(updated.Id, replay.Id);
        Assert.Equal(updated.Address, replay.Address);
        Assert.Equal(updated.State, replay.State);
        Assert.Equal(updated.Version, replay.Version);
        Assert.Equal(updated.RouteScopes, replay.RouteScopes);
        Assert.Equal("instructions-mailbox", updated.MailboxIdentity);
        Assert.Equal("instructions-inbox", updated.InboxFolderIdentity);
        Assert.Equal("instructions-sent", updated.SentFolderIdentity);
        Assert.True(updated.IdentityIsBound);
        Assert.Equal(updated.MailboxIdentity, replay.MailboxIdentity);
        Assert.True(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.Incoming,
            default));
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.Outgoing,
            default));
        await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => command.ExecuteAsync(
                request with { OperationKey = "approved-mailbox-stale-1" },
                default));

        var disabled = await command.ExecuteAsync(
            request with
            {
                State = ApprovedMailboxState.Disabled,
                ExpectedVersion = updated.Version,
                OperationKey = "approved-mailbox-disable-1"
            },
            default);
        Assert.Equal(ApprovedMailboxState.Disabled, disabled.State);
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.Incoming,
            default));
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.Outgoing,
            default));

        // Disabling preserves the bound identities; rebinding one is refused, because it
        // would orphan or alias this mailbox's cursor row.
        Assert.Equal("instructions-mailbox", disabled.MailboxIdentity);
        var rebind = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            async () => await command.ExecuteAsync(
                request with
                {
                    ExpectedVersion = disabled.Version,
                    MailboxIdentity = "a-different-mailbox",
                    OperationKey = "approved-mailbox-rebind-1"
                },
                default));
        Assert.Equal(ApprovedMailboxUpdateError.MailboxIdentityImmutable, rebind.Error);
        Assert.Equal(
            "instructions-mailbox",
            Assert.Single(await list.ExecuteAsync(actor, default)).MailboxIdentity);

        var replayConflict = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => command.ExecuteAsync(
                request with
                {
                    InboxFolderIdentity = "a-different-inbox"
                },
                default));
        Assert.Equal(ApprovedMailboxUpdateError.OperationConflict, replayConflict.Error);
    }
}
