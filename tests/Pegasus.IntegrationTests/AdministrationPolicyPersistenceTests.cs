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
            [ApprovedMailboxRouteScope.InboundIntake, ApprovedMailboxRouteScope.SentEvidence],
            ApprovedMailboxState.Approved,
            initial.Version,
            actor,
            "approved-mailbox-update-1",
            // Approving a row now requires the exact tenant identities its routes read.
            "instructions-mailbox",
            "instructions-inbox",
            "instructions-sent",
            [
                new(MailLogicalFolderType.Instructions, "folder-instructions"),
                new(MailLogicalFolderType.Audits, "folder-audits"),
                new(MailLogicalFolderType.Billing, "folder-billing")
            ]);

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
        Assert.Equal(request.FolderBindings, updated.FolderBindings);
        Assert.True(updated.IdentityIsBound);
        Assert.Equal(updated.MailboxIdentity, replay.MailboxIdentity);
        Assert.True(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.InboundIntake,
            default));
        Assert.True(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.SentEvidence,
            default));
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.StaffSend,
            default));
        await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => command.ExecuteAsync(
                request with { OperationKey = "approved-mailbox-stale-1" },
                default));

        var refreshed = await command.ExecuteAsync(
            request with
            {
                ExpectedVersion = updated.Version,
                FolderBindings =
                [
                    new(MailLogicalFolderType.Instructions, "folder-instructions-refreshed"),
                    new(MailLogicalFolderType.Billing, "folder-billing"),
                    new(MailLogicalFolderType.Other, "folder-other")
                ],
                OperationKey = "approved-mailbox-refresh-1"
            },
            default);
        Assert.Equal(updated.Version + 1, refreshed.Version);
        Assert.Equal(
            [
                new ApprovedMailboxFolderBinding(MailLogicalFolderType.Instructions, "folder-instructions-refreshed"),
                new ApprovedMailboxFolderBinding(MailLogicalFolderType.Billing, "folder-billing"),
                new ApprovedMailboxFolderBinding(MailLogicalFolderType.Other, "folder-other")
            ],
            refreshed.FolderBindings);

        var disabled = await command.ExecuteAsync(
            request with
            {
                State = ApprovedMailboxState.Disabled,
                ExpectedVersion = refreshed.Version,
                FolderBindings = null,
                OperationKey = "approved-mailbox-disable-1"
            },
            default);
        Assert.Equal(ApprovedMailboxState.Disabled, disabled.State);
        Assert.Equal(refreshed.FolderBindings, disabled.FolderBindings);
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.InboundIntake,
            default));
        Assert.False(await policy.IsApprovedAsync(
            initial.Address,
            ApprovedMailboxRouteScope.SentEvidence,
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
                    FolderBindings = [new(MailLogicalFolderType.Other, "folder-other")]
                },
                default));
        Assert.Equal(ApprovedMailboxUpdateError.OperationConflict, replayConflict.Error);
    }

    [Fact]
    public async Task DefaultStaffSendMailboxTransferIsAuthorizedVersionedAuditedAndReplaySafe()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var mailboxCommand = scope.ServiceProvider.GetRequiredService<UpdateApprovedMailbox>();
        var defaultCommand = scope.ServiceProvider.GetRequiredService<SetDefaultApprovedMailbox>();
        var list = scope.ServiceProvider.GetRequiredService<ListApprovedMailboxes>();
        var first = await CreateApprovedStaffSendMailboxAsync(
            mailboxCommand, administrator, "default-first@collisionengineers.co.uk", "first");
        var second = await CreateApprovedStaffSendMailboxAsync(
            mailboxCommand, administrator, "default-second@collisionengineers.co.uk", "second");

        var firstSelection = new SetDefaultApprovedMailboxRequest(
            first.Id,
            first.Version,
            null,
            null,
            administrator,
            "approved-mailbox-default-first");
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => defaultCommand.ExecuteAsync(
                firstSelection with
                {
                    Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                    OperationKey = "approved-mailbox-default-denied"
                },
                default));

        var firstDefault = await defaultCommand.ExecuteAsync(firstSelection, default);
        var firstReplay = await defaultCommand.ExecuteAsync(firstSelection, default);
        Assert.True(firstDefault.IsDefaultStaffSend);
        Assert.Equal(firstDefault.Id, firstReplay.Id);
        Assert.Equal(firstDefault.Version, firstReplay.Version);
        Assert.True(firstReplay.IsDefaultStaffSend);
        var replayConflict = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => defaultCommand.ExecuteAsync(
                firstSelection with { ExpectedVersion = firstSelection.ExpectedVersion + 1 },
                default));
        Assert.Equal(ApprovedMailboxUpdateError.OperationConflict, replayConflict.Error);

        var transfer = new SetDefaultApprovedMailboxRequest(
            second.Id,
            second.Version,
            firstDefault.Id,
            firstDefault.Version,
            administrator,
            "approved-mailbox-default-second");
        var secondDefault = await defaultCommand.ExecuteAsync(transfer, default);
        var mailboxes = await list.ExecuteAsync(administrator, default);
        var clearedFirst = Assert.Single(mailboxes, mailbox => mailbox.Id == first.Id);
        Assert.False(clearedFirst.IsDefaultStaffSend);
        Assert.Equal(firstDefault.Version + 1, clearedFirst.Version);
        Assert.True(secondDefault.IsDefaultStaffSend);
        Assert.Single(mailboxes, mailbox => mailbox.IsDefaultStaffSend);

        var removeStaffSendScope = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            async () => await mailboxCommand.ExecuteAsync(
                new(
                    secondDefault.Id,
                    secondDefault.Address,
                    [ApprovedMailboxRouteScope.InboundIntake],
                    ApprovedMailboxState.Approved,
                    secondDefault.Version,
                    administrator,
                    "approved-mailbox-default-remove-staff-send",
                    secondDefault.MailboxIdentity,
                    secondDefault.InboxFolderIdentity,
                    secondDefault.SentFolderIdentity,
                    secondDefault.FolderBindings,
                    secondDefault.VerifiedEncodedMessageSizeLimit),
                default));
        Assert.Equal(ApprovedMailboxUpdateError.DefaultStaffSendMailboxRequiresReplacement, removeStaffSendScope.Error);

        var stale = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => defaultCommand.ExecuteAsync(
                firstSelection with
                {
                    ExpectedVersion = clearedFirst.Version,
                    OperationKey = "approved-mailbox-default-stale"
                },
                default));
        Assert.Equal(ApprovedMailboxUpdateError.VersionConflict, stale.Error);

        var disableDefault = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => mailboxCommand.ExecuteAsync(
                new(
                    secondDefault.Id,
                    secondDefault.Address,
                    secondDefault.RouteScopes,
                    ApprovedMailboxState.Disabled,
                    secondDefault.Version,
                    administrator,
                    "approved-mailbox-default-disable",
                    secondDefault.MailboxIdentity,
                    secondDefault.InboxFolderIdentity,
                    secondDefault.SentFolderIdentity,
                    secondDefault.FolderBindings,
                    secondDefault.VerifiedEncodedMessageSizeLimit),
                default));
        Assert.Equal(ApprovedMailboxUpdateError.DefaultStaffSendMailboxRequiresReplacement, disableDefault.Error);

        await using var context = await database.CreateContextAsync();
        var defaultSelectionHistory = await context.ActionHistory
            .Where(item => item.EventKind == "approved_mailbox_default_staff_send_selected")
            .OrderBy(item => item.OccurredAtUtc)
            .ToArrayAsync();
        Assert.Equal(2, defaultSelectionHistory.Length);
        Assert.All(defaultSelectionHistory, item =>
        {
            Assert.Equal("approved_mailbox", item.AggregateType);
            Assert.Equal(administrator.SubjectId, item.ActorSubjectId);
            Assert.Equal(ActorKind.Staff.ToString(), item.ActorKind);
        });
        var transferHistory = Assert.Single(defaultSelectionHistory, item =>
            item.CorrelationId == transfer.OperationKey);
        Assert.Null(transferHistory.Reason);
        Assert.Contains(firstDefault.Id.ToString("D"), transferHistory.BeforeJson, StringComparison.Ordinal);
        Assert.Contains("\"IsDefaultStaffSend\":true", transferHistory.AfterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultStaffSendMailboxRequiresSentEvidenceAndResolvedFolder()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var mailboxCommand = scope.ServiceProvider.GetRequiredService<UpdateApprovedMailbox>();
        var defaultCommand = scope.ServiceProvider.GetRequiredService<SetDefaultApprovedMailbox>();
        var staged = await CreateApprovedStaffSendMailboxAsync(
            mailboxCommand,
            administrator,
            "staged-default@collisionengineers.co.uk",
            "staged",
            includeSentEvidence: false,
            includeSentFolder: false);
        var withoutSentEvidence = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => defaultCommand.ExecuteAsync(
                new(staged.Id, staged.Version, null, null, administrator,
                    "default-without-sent-evidence"),
                default));
        Assert.Equal(ApprovedMailboxUpdateError.DefaultStaffSendMailboxIneligible, withoutSentEvidence.Error);

        await using (var context = await database.CreateContextAsync())
        {
            var entity = await context.ApprovedMailboxes.SingleAsync(item => item.Id == staged.Id);
            entity.AllowSentEvidence = true;
            entity.Version++;
            await context.SaveChangesAsync();
        }
        var withoutSentFolder = await Assert.ThrowsAsync<ApprovedMailboxUpdateException>(
            () => defaultCommand.ExecuteAsync(
                new(staged.Id, staged.Version + 1, null, null, administrator,
                    "default-with-sent-folder"),
                default));
        Assert.Equal(ApprovedMailboxUpdateError.DefaultStaffSendMailboxIneligible, withoutSentFolder.Error);

        await using (var context = await database.CreateContextAsync())
        {
            var entity = await context.ApprovedMailboxes.SingleAsync(item => item.Id == staged.Id);
            entity.SentFolderIdentity = "staged-sent";
            entity.Version++;
            await context.SaveChangesAsync();
        }
        var selected = await defaultCommand.ExecuteAsync(
            new(staged.Id, staged.Version + 2, null, null, administrator,
                "default-with-sent-evidence"),
            default);
        Assert.True(selected.IsDefaultStaffSend);
    }

    private static Task<ApprovedMailbox> CreateApprovedStaffSendMailboxAsync(
        UpdateApprovedMailbox command,
        ActionActor actor,
        string address,
        string identity,
        bool includeSentEvidence = true,
        bool includeSentFolder = true) =>
        command.ExecuteAsync(
            new(
                Guid.NewGuid(),
                address,
                includeSentEvidence
                    ? [ApprovedMailboxRouteScope.InboundIntake, ApprovedMailboxRouteScope.SentEvidence, ApprovedMailboxRouteScope.StaffSend]
                    : [ApprovedMailboxRouteScope.InboundIntake, ApprovedMailboxRouteScope.StaffSend],
                ApprovedMailboxState.Approved,
                0,
                actor,
                $"approved-mailbox-{identity}",
                $"{identity}-mailbox",
                $"{identity}-inbox",
                includeSentFolder ? $"{identity}-sent" : null,
                null,
                10485760),
            default);
}
