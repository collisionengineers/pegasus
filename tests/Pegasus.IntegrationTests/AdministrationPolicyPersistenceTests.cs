using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class AdministrationPolicyPersistenceTests
{
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
            false,
            true,
            false,
            true,
            initial.PolicyVersion,
            actor,
            "Review the Engineer-assignment gates",
            "workflow-policy-update-1");

        var updated = await command.ExecuteAsync(request, default);
        var replay = await command.ExecuteAsync(request, default);

        Assert.Equal(initial.PolicyVersion + 1, updated.PolicyVersion);
        Assert.Equal(updated, replay);
        Assert.False(updated.RequireCompleteInstructionsBeforeEngineerAssignment);
        Assert.False(updated.RequireStaffInstructionReviewBeforeEngineerAssignment);
        await Assert.ThrowsAsync<WorkflowConfigurationVersionConflictException>(
            () => command.ExecuteAsync(
                request with { OperationKey = "workflow-policy-stale-1" },
                default));

        await using var context = await database.CreateContextAsync();
        Assert.Equal(
            1,
            await context.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS [Value] FROM [ActionHistory] WHERE [AggregateType] = 'workflow_configuration'")
                .SingleAsync());
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
            [ApprovedMailboxRouteScope.InboundIntake, ApprovedMailboxRouteScope.SentEvidence],
            ApprovedMailboxState.Approved,
            initial.Version,
            actor,
            "Approve exact Sent evidence alongside inbound Intake",
            "approved-mailbox-update-1");

        var updated = await command.ExecuteAsync(request, default);
        var replay = await command.ExecuteAsync(request, default);

        Assert.Equal(initial.Version + 1, updated.Version);
        Assert.Equal(updated.Id, replay.Id);
        Assert.Equal(updated.Address, replay.Address);
        Assert.Equal(updated.State, replay.State);
        Assert.Equal(updated.Version, replay.Version);
        Assert.Equal(updated.RouteScopes, replay.RouteScopes);
        Assert.True(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.InboundIntake,
            default));
        Assert.True(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.SentEvidence,
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
                Reason = "Disable both approved read routes",
                OperationKey = "approved-mailbox-disable-1"
            },
            default);
        Assert.Equal(ApprovedMailboxState.Disabled, disabled.State);
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.InboundIntake,
            default));
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.SentEvidence,
            default));
    }
}
