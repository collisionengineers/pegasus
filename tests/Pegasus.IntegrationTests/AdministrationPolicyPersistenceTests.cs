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

    [Fact]
    public async Task AnApprovedMailboxIdentityBelongsToOneMailbox()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        foreach (var (column, length) in new[]
                 {
                     ("MailboxIdentity", 100),
                     ("InboxFolderIdentity", 200),
                     ("SentFolderIdentity", 200)
                 })
        {
            Assert.Equal(
                1,
                await database.ScalarAsync<int>(
                    $"""
                    SELECT COUNT(*) FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ApprovedMailboxes')
                      AND name = N'{column}'
                      AND is_nullable = 1
                      AND max_length = {length * 2}
                    """));
        }

        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                """
                SELECT COUNT(*) FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'ApprovedMailboxes')
                  AND name = N'IX_ApprovedMailboxes_MailboxIdentity'
                  AND is_unique = 1
                  AND has_filter = 1
                """));

        // The filter is what lets several rows wait for their identities at once while
        // still refusing two rows that claim the same mailbox.
        await database.ExecuteAsync(
            """
            INSERT INTO ApprovedMailboxes
                (Id, Address, AllowIncoming, AllowOutgoing, MailboxGeneration, State, Version)
            VALUES
                ('8b2b9a1c-4d20-4a0f-9a51-0d5a8e73c910',
                 'second@collisionengineers.co.uk', 1, 0, 0, 'Approved', 1);
            """);
        Assert.Equal(
            2,
            await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ApprovedMailboxes WHERE MailboxIdentity IS NULL"));
        await database.ExecuteAsync(
            """
            UPDATE ApprovedMailboxes SET MailboxIdentity = 'bound-mailbox'
            WHERE Address = 'instructions@collisionengineers.co.uk';
            """);
        var duplicate = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            database.ExecuteAsync(
                """
                UPDATE ApprovedMailboxes SET MailboxIdentity = 'bound-mailbox'
                WHERE Address = 'second@collisionengineers.co.uk';
                """));
        Assert.Contains(
            "IX_ApprovedMailboxes_MailboxIdentity",
            duplicate.Message,
            StringComparison.Ordinal);
    }
}
