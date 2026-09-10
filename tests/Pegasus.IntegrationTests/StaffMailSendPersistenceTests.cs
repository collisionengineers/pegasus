using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffMailSendPersistenceTests
{
    [Fact]
    public async Task FinalStaffSendMailboxRequiresSentEvidenceScopeAndResolvedFolder()
    {
        await using var database = await CreateDatabaseAsync();
        var missingScopeId = Guid.NewGuid();
        var missingFolderId = Guid.NewGuid();
        var configuredId = Guid.NewGuid();
        await using (var scope = database.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.ApprovedMailboxes.AddRange(
                StaffSendMailbox(missingScopeId, "missing-scope", allowSentEvidence: false, sentFolderIdentity: "sent"),
                StaffSendMailbox(missingFolderId, "missing-folder", allowSentEvidence: true, sentFolderIdentity: null),
                StaffSendMailbox(configuredId, "configured", allowSentEvidence: true, sentFolderIdentity: "sent"));
            await db.SaveChangesAsync();
        }

        await using var queryScope = database.CreateAsyncScope();
        var mailboxes = queryScope.ServiceProvider.GetRequiredService<EfStaffMailSendStore>();
        Assert.Null(await mailboxes.GetAsync(missingScopeId, CancellationToken.None));
        Assert.Null(await mailboxes.GetAsync(missingFolderId, CancellationToken.None));
        Assert.NotNull(await mailboxes.GetAsync(configuredId, CancellationToken.None));
    }

    [Fact]
    public async Task SameOperationAndPayloadReplaysButChangedPayloadConflicts()
    {
        await using var database = await CreateDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = Command();
        var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);

        var first = await store.PrepareAsync(command, new string('A', 64), now, CancellationToken.None);
        var replay = await store.PrepareAsync(command, new string('A', 64), now, CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        AssertOperationContext(first, command);
        AssertOperationContext(replay, command);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.PrepareAsync(command, new string('B', 64), now, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentDistinctRepliesToOneRetainedMessageCreateOneOperation()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var firstCommand = ReplyCommand(mailboxId, retainedMessageId, "reply-one");
        var secondCommand = ReplyCommand(mailboxId, retainedMessageId, "reply-two");
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstAttempt = PrepareAsync(firstScope, firstCommand);
        var secondAttempt = PrepareAsync(secondScope, secondCommand);
        start.SetResult();
        var attempts = await Task.WhenAll(firstAttempt, secondAttempt);

        var created = Assert.Single(attempts, value => value.Operation is not null);
        Assert.NotNull(created.Operation);
        var refused = Assert.Single(attempts, value => value.Error is not null);
        Assert.IsType<InvalidOperationException>(refused.Error);
        Assert.DoesNotContain(firstCommand.Actor.SubjectId, refused.Error!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secondCommand.Actor.SubjectId, refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE MailboxId = '{mailboxId:D}' AND OriginalRetainedMessageId = '{retainedMessageId:D}'"));

        var firstWon = attempts[0].Operation is not null;
        var winningCommand = firstWon
            ? firstCommand
            : secondCommand;
        var losingActor = firstWon
            ? secondCommand.Actor
            : firstCommand.Actor;
        await using var verifyScope = database.CreateAsyncScope();
        var verifyStore = verifyScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var replay = await verifyStore.PrepareAsync(
            winningCommand, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(created.Operation.Id, replay.Id);
        Assert.Null(await verifyStore
            .GetAsync(losingActor.SubjectId, created.Operation.Id, CancellationToken.None));

        async Task<(StaffMailOperation? Operation, Exception? Error)> PrepareAsync(
            AsyncServiceScope scope, StaffMailSendCommand command)
        {
            await start.Task;
            try
            {
                var operation = await scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
                    .PrepareAsync(command, new string('A', 64), DateTimeOffset.UtcNow,
                        CancellationToken.None);
                return (operation, null);
            }
            catch (InvalidOperationException exception)
            {
                return (null, exception);
            }
        }
    }

    [Fact]
    public async Task ConcurrentRepliesToDifferentOriginalsInOneMailboxBothPrepare()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var firstRetainedMessageId = Guid.NewGuid();
        var secondRetainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, firstRetainedMessageId);
        await using (var seedScope = database.CreateAsyncScope())
        {
            var factory = seedScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.Set<RetainedMailboxMessageEntity>().Add(new()
            {
                Id = secondRetainedMessageId,
                MailboxId = mailboxId,
                MailboxAddress = "mailbox@example.invalid",
                FolderScope = "Inbox",
                FolderIdentity = "inbox",
                ImmutableMessageId = "immutable-message-two",
                InternetMessageIdentity = "<message-two@example.invalid>",
                ConversationIdentity = "conversation-two",
                ExternalReceiptToken = $"retained:{secondRetainedMessageId:N}",
                ToAddressesJson = "[]",
                CcAddressesJson = "[]",
                SourceLength = 1,
                SourceSha256 = new string('B', 64),
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                RetainedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var firstCommand = ReplyCommand(mailboxId, firstRetainedMessageId, "first-original");
        var secondCommand = ReplyCommand(
            mailboxId, secondRetainedMessageId, "second-original") with
        {
            OriginalMessage = new(
                secondRetainedMessageId,
                mailboxId,
                "immutable-message-two",
                "<message-two@example.invalid>",
                "conversation-two")
        };
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = new[]
        {
            PrepareAsync(firstScope, firstCommand),
            PrepareAsync(secondScope, secondCommand)
        };
        start.SetResult();
        var operations = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(2, operations.Select(operation => operation.Id).Distinct().Count());
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE MailboxId = '{mailboxId:D}'"));

        async Task<StaffMailOperation> PrepareAsync(
            AsyncServiceScope scope, StaffMailSendCommand command)
        {
            await start.Task;
            return await scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
                .PrepareAsync(command, new string('A', 64), DateTimeOffset.UtcNow,
                    CancellationToken.None);
        }
    }

    [Fact]
    public async Task ConcurrentDifferentActorRepliesUseOneOperationDraftAndSend()
    {
        var transport = new RecordingStaffMailTransport();
        var mailboxId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync(services =>
        {
            services.AddSingleton<IApprovedStaffSendMailboxQueries>(
                new ApprovedMailboxQueries(mailboxId));
            services.AddSingleton<IReadLogicalDocumentVersion, UnusedLogicalDocumentReader>();
            services.AddSingleton<IStaffMailTransport>(transport);
            services.AddScoped<StaffMailSend>();
        });
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var firstActor = await SeedStaffAsync(database, "mail.concurrent.one");
        var secondActor = await SeedStaffAsync(database, "mail.concurrent.two");
        var firstCommand = ReplyCommand(firstActor, mailboxId, retainedMessageId, "reply-one");
        var secondCommand = ReplyCommand(secondActor, mailboxId, retainedMessageId, "reply-two");
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = new[]
        {
            SendAsync(firstScope, firstCommand),
            SendAsync(secondScope, secondCommand)
        };
        start.SetResult();
        var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Single(results, result => result.Operation is not null);
        Assert.Single(results, result => result.Error is InvalidOperationException);
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE OriginalRetainedMessageId = '{retainedMessageId:D}'"));
        Assert.Equal(1, transport.CreateDraftCount);
        Assert.Equal(1, transport.SendDraftCount);

        async Task<(StaffMailOperation? Operation, Exception? Error)> SendAsync(
            AsyncServiceScope scope, StaffMailSendCommand command)
        {
            await start.Task;
            try
            {
                return (await scope.ServiceProvider.GetRequiredService<StaffMailSend>()
                    .SendAsync(command, CancellationToken.None), null);
            }
            catch (InvalidOperationException exception)
            {
                return (null, exception);
            }
        }
    }

    [Theory]
    [InlineData(StaffMailState.Sent)]
    [InlineData(StaffMailState.Failed)]
    [InlineData(StaffMailState.Cancelled)]
    public async Task TerminalReplyAllowsANewOperation(StaffMailState terminalState)
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var firstCommand = ReplyCommand(mailboxId, retainedMessageId, "first-reply");
        var operation = await store.PrepareAsync(
            firstCommand, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
        operation = await MoveToTerminalAsync(store, firstCommand.Actor.SubjectId, operation, terminalState);

        var next = await store.PrepareAsync(
            ReplyCommand(mailboxId, retainedMessageId, "next-reply"),
            new string('B', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotEqual(operation.Id, next.Id);
        Assert.Equal(StaffMailState.Prepared, next.State);
    }

    [Fact]
    public async Task LatestOriginalQueryReturnsNewestOwnedOperationOnly()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var firstCommand = ReplyCommand(actor, mailboxId, retainedMessageId, "first-reply");
        var first = await store.PrepareAsync(
            firstCommand, new string('A', 64),
            new DateTimeOffset(2026, 9, 6, 10, 2, 0, TimeSpan.Zero), CancellationToken.None);
        _ = await MoveToTerminalAsync(store, actor.SubjectId, first, StaffMailState.Failed);
        var secondCommand = ReplyCommand(actor, mailboxId, retainedMessageId, "second-reply");
        var second = await store.PrepareAsync(
            secondCommand,
            new string('B', 64),
            new DateTimeOffset(2026, 9, 6, 10, 1, 0, TimeSpan.Zero), CancellationToken.None);

        var latest = await store.GetLatestForOriginalAsync(
            actor.SubjectId, retainedMessageId, CancellationToken.None);
        var otherActor = await store.GetLatestForOriginalAsync(
            Guid.NewGuid().ToString("D"), retainedMessageId, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(second.Id, latest.Id);
        Assert.Equal(StaffMailState.Prepared, latest.State);
        AssertOperationContext(latest, secondCommand);
        Assert.Null(otherActor);
    }

    [Fact]
    public async Task UnknownReplyStillBlocksANewOperation()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var firstCommand = ReplyCommand(mailboxId, retainedMessageId, "uncertain-reply");
        var operation = await store.PrepareAsync(
            firstCommand, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
        operation = await store.TransitionAsync(
            firstCommand.Actor.SubjectId, operation.Id, operation.Version,
            StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft,
            null, null, null, null, CancellationToken.None);
        _ = await store.TransitionAsync(
            firstCommand.Actor.SubjectId, operation.Id, operation.Version,
            StaffMailState.Unknown, StaffMailAttemptStage.CreateDraft,
            null, null, null, "provider outcome unknown", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PrepareAsync(
            ReplyCommand(mailboxId, retainedMessageId, "second-reply"),
            new string('B', 64), DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task SubmittedOperationIsAvailableAfterAStoreRestartForReadOnlyReconciliation()
    {
        await using var database = await CreateDatabaseAsync();
        var command = Command();
        Guid operationId;
        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
            var operation = await store.PrepareAsync(
                command, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
            AssertOperationContext(operation, command);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft, null, null, null, null,
                CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.DraftReady, StaffMailAttemptStage.Attach, "draft-id", null, null, null,
                CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Sending, StaffMailAttemptStage.Send, "draft-id", null, null, null,
                CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Submitted, StaffMailAttemptStage.ObserveSent, "draft-id",
                DateTimeOffset.UtcNow, null, null, CancellationToken.None);
            operationId = operation.Id;
        }

        var historyCount = await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = 'StaffMailSend' AND AggregateId = '{operationId:D}'");
        Assert.Equal(5, historyCount);

        await using var restarted = database.CreateAsyncScope();
        var candidate = await restarted.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
            .GetExecutionAsync(command.Actor.SubjectId, operationId, CancellationToken.None);

        Assert.NotNull(candidate);
        Assert.Equal(operationId, candidate!.Operation.Id);
        Assert.Equal("draft-id", candidate.DraftImmutableId);
        AssertOperationContext(candidate.Operation, command);
    }

    [Fact]
    public async Task ConfirmedReplyToTheCurrentRetainedPostReportQueryCompletesTheCase()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedPostReportQueryAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "complete-query-reply") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion
        };
        var operation = await MoveToSubmittedAsync(
            store,
            command,
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);

        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"),
            operation.Id,
            operation.Version,
            "confirmed-sent-item",
            observedAtUtc.AddMinutes(-1),
            observedAtUtc,
            CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.PostReportComplete), workflow.State);
        Assert.Equal(nameof(CaseClosureOutcome.PostReportComplete), workflow.ClosureOutcome);
        Assert.Equal(fixture.WorkflowVersion + 1, workflow.Version);
        var resolution = Assert.Single(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_query_replied")
            .ToListAsync());
        Assert.Contains("confirmed-sent-item", resolution.ResultJson, StringComparison.Ordinal);
        var sent = await db.Set<StaffMailSendOperationEntity>()
            .SingleAsync(item => item.Id == operation.Id);
        Assert.Equal("confirmed-sent-item", sent.ObservedSentImmutableMessageId);
        Assert.Equal(observedAtUtc.AddMinutes(-1), sent.ProviderSentAtUtc);
    }

    [Fact]
    public async Task ReceivedPostReportQueryAssociationMovesCompletedCaseToQuery()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedPostReportQueryAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: false);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var seed = await factory.CreateDbContextAsync())
        {
            var seededWorkflow = await seed.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
            seededWorkflow.State = nameof(CaseLifecycleState.PostReportComplete);
            seededWorkflow.ClosureOutcome = nameof(CaseClosureOutcome.PostReportComplete);
            await seed.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var workflowStore = new EfCaseWorkflowStore(factory, TimeProvider.System);
        var lease = await workflowStore.ClaimAsync(
            new(fixture.CaseId, fixture.WorkflowVersion, actor, "claim-received-query"),
            CancellationToken.None);
        var mutationStore = new EfIntakeMutationStore(factory);
        var receipt = await mutationStore.GetAsync(fixture.ReceiptId, CancellationToken.None);
        Assert.NotNull(receipt);

        await mutationStore.LinkAsync(
            new(
                fixture.ReceiptId,
                fixture.CaseId,
                receipt!.IntakeVersion,
                fixture.WorkflowVersion,
                lease.Token,
                actor,
                "link-received-query",
                null),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.Query), persisted.State);
        Assert.Null(persisted.ClosureOutcome);
        Assert.Equal(fixture.WorkflowVersion + 1, persisted.Version);
        Assert.Equal(1, await verify.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == fixture.CaseId && item.EventType == "intake_case_linked"));
    }

    [Fact]
    public async Task ObservedReplyBeforeAutomaticQueryAssociationConvergesToCompleted()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedPostReportQueryAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: false);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var seed = await factory.CreateDbContextAsync())
        {
            var seededWorkflow = await seed.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
            seededWorkflow.State = nameof(CaseLifecycleState.PostReportComplete);
            seededWorkflow.ClosureOutcome = nameof(CaseClosureOutcome.PostReportComplete);
            await seed.SaveChangesAsync();
        }

        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "observed-before-query-association") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion
        };
        var operation = await MoveToSubmittedAsync(
            store,
            command,
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);
        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"),
            operation.Id,
            operation.Version,
            "confirmed-sent-before-query-association",
            observedAtUtc.AddMinutes(-1),
            observedAtUtc,
            CancellationToken.None);

        var associated = await new EfIntakeMutationStore(factory).AssociateFromMatchAsync(
            new(
                fixture.ReceiptId,
                fixture.CaseId,
                "test-match",
                1,
                "system-worker:intake-processing",
                "automatic-query-after-observed-reply",
                "Link retained post-report query after its reply was observed."),
            observedAtUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, associated);
        await using var verify = await factory.CreateDbContextAsync();
        var workflow = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.PostReportComplete), workflow.State);
        Assert.Equal(nameof(CaseClosureOutcome.PostReportComplete), workflow.ClosureOutcome);
        Assert.Equal(fixture.WorkflowVersion + 1, workflow.Version);
        var resolution = Assert.Single(await verify.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_query_replied")
            .ToListAsync());
        Assert.Equal(observedAtUtc, resolution.OccurredAtUtc);
        Assert.Contains("confirmed-sent-before-query-association", resolution.ResultJson, StringComparison.Ordinal);
        var history = Assert.Single(await verify.ActionHistory
            .Where(item => item.AggregateType == "case"
                && item.AggregateId == fixture.CaseId.ToString("D")
                && item.EventKind == "case_query_replied")
            .ToListAsync());
        Assert.NotNull(history.BeforeJson);
        Assert.Contains("confirmed-sent-before-query-association", history.BeforeJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmittedOrUnrelatedRepliesDoNotCompleteAQuery()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedPostReportQueryAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: false, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "unrelated-query-reply") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion
        };
        var operation = await MoveToSubmittedAsync(
            store,
            command,
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            nameof(CaseLifecycleState.Query),
            await ReadCaseWorkflowStateAsync(database, fixture.CaseId));

        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);
        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"),
            operation.Id,
            operation.Version,
            "confirmed-unrelated-sent-item",
            observedAtUtc.AddMinutes(-1),
            observedAtUtc,
            CancellationToken.None);

        Assert.Equal(
            nameof(CaseLifecycleState.Query),
            await ReadCaseWorkflowStateAsync(database, fixture.CaseId));
    }

    [Fact]
    public async Task ReplicaTransitionsCannotBothClaimTheSameSendStage()
    {
        await using var database = await CreateDatabaseAsync();
        var command = Command();
        StaffMailOperation prepared;
        await using (var prepareScope = database.CreateAsyncScope())
        {
            prepared = await prepareScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
                .PrepareAsync(command, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
        }
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();

        var attempts = await Task.WhenAll(
            ClaimAsync(first),
            ClaimAsync(second));

        Assert.Equal(1, attempts.Count(value => value));

        async Task<bool> ClaimAsync(IStaffMailSendStore store)
        {
            try
            {
                await store.TransitionAsync(
                    command.Actor.SubjectId, prepared.Id, prepared.Version,
                    StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft,
                    null, null, null, null, CancellationToken.None);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    [Fact]
    public async Task UploadProgressRestartsFromProtectedPerAttachmentState()
    {
        await using var database = await CreateDatabaseAsync();
        var command = Command();
        Guid operationId;
        var attachmentVersionId = Guid.NewGuid();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(10);
        await using (var firstScope = database.CreateAsyncScope())
        {
            var operation = await firstScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
                .PrepareAsync(command, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
            operationId = operation.Id;
            await firstScope.ServiceProvider.GetRequiredService<IStaffMailUploadProgress>().SaveAsync(
                operationId, attachmentVersionId,
                new(new Uri("https://upload.example.test/session-secret"), expiry, 327680),
                CancellationToken.None);
        }

        var protectedValue = await database.ScalarAsync<string>(
            $"SELECT ProtectedUploadSession FROM StaffMailSendOperations WHERE Id = '{operationId:D}'");
        Assert.DoesNotContain("session-secret", protectedValue, StringComparison.Ordinal);
        await using var restarted = database.CreateAsyncScope();
        var resumed = await restarted.ServiceProvider.GetRequiredService<IStaffMailUploadProgress>()
            .GetAsync(operationId, attachmentVersionId, CancellationToken.None);

        Assert.NotNull(resumed);
        Assert.Equal(327680, resumed.NextOffset);
        Assert.Equal(expiry, resumed.ExpiresAtUtc);
        Assert.Equal("https://upload.example.test/session-secret", resumed.UploadUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task EnabledAccountWithoutCurrentCaseworkRoleCannotSend()
    {
        await using var database = await CreateDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var staffId = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Add(new PegasusIdentityUser
            {
                Id = staffId, UserName = "mail.user", NormalizedUserName = "MAIL.USER",
                IsEnabled = true, SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            await db.SaveChangesAsync();
        }
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.RequireCurrentStaffAsync(staffId.ToString("D"), CancellationToken.None));

        await using (var db = await factory.CreateDbContextAsync())
        {
            var role = await db.Roles.SingleOrDefaultAsync(value => value.NormalizedName == "USER");
            if (role is null)
            {
                role = new IdentityRole<Guid>(StaffRoleNames.User)
                {
                    Id = Guid.NewGuid(), NormalizedName = "USER"
                };
                db.Roles.Add(role);
            }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = role.Id });
            await db.SaveChangesAsync();
        }

        await store.RequireCurrentStaffAsync(staffId.ToString("D"), CancellationToken.None);
    }

    [Fact]
    public async Task ReplicaExecutionLockExcludesConcurrentAttachmentFlowAndReleases()
    {
        await using var database = await CreateDatabaseAsync();
        var operationId = Guid.NewGuid();
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IStaffMailExecutionLock>();
        var second = secondScope.ServiceProvider.GetRequiredService<IStaffMailExecutionLock>();
        await using (await first.AcquireAsync(operationId, CancellationToken.None))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                second.AcquireAsync(operationId, CancellationToken.None));
        }
        await using var reacquired = await second.AcquireAsync(operationId, CancellationToken.None);
    }

    private static async Task<QueryReplyFixture> SeedPostReportQueryAsync(
        LocalDbTestDatabase database,
        Guid mailboxId,
        Guid retainedMessageId,
        bool isQueryReceipt,
        bool isAssociated)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var nowUtc = new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Query reply test organization"}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {nowUtc})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {"QRY"}, {lineageId}, {true}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {1}, {"QRY260001"}, {"inspection"}, {"review"}, {"pending"}, {true}, {true}, {nowUtc}, {0L}, {Guid.NewGuid()})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.Query)}, {0L}, {Guid.NewGuid()})");
        var receipt = new IntakeReceiptEntity
        {
            Id = receiptId,
            SourceFileName = "post-report-query.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('A', 64),
            SourceChannel = "mailbox",
            ExternalReceiptToken = $"retained:{retainedMessageId:N}",
            ReceivedAtUtc = nowUtc,
            ProcessedAtUtc = nowUtc,
            SourceReaderKey = "staff-mail-test",
            SourceReaderVersion = "1",
            Version = 0,
            Decision = "case_created",
            DecisionReason = "post-report correspondence",
            EvidenceJson = "{\"version\":1,\"data\":[]}",
            FieldsJson = "{\"version\":1,\"data\":[]}",
            OcrCandidatesJson = "{\"version\":1,\"data\":[]}",
            MailClassificationDecision = new()
            {
                IntakeReceiptId = receiptId,
                Outcome = "classified",
                Direction = "received",
                Family = isQueryReceipt ? "post-report-emails" : "general-correspondence",
                IsReplyContext = false,
                AmbiguousCandidatesJson = "{\"version\":1,\"data\":[]}",
                PredicatesJson = "{\"version\":1,\"data\":[]}",
                Reason = "staff-mail test classification",
                PolicyKey = "staff-mail-test",
                PolicyVersion = 1,
                DecidedByActor = "test",
                DecidedAtUtc = nowUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            }
        };
        db.IntakeReceipts.Add(receipt);
        if (isAssociated)
        {
            db.IntakeManualAssociations.Add(new()
            {
                IntakeReceiptId = receiptId,
                CaseId = caseId,
                IsActive = true,
                Version = 0,
                LinkedAtUtc = nowUtc,
                ActorKind = "Staff",
                ActorSubjectId = Guid.NewGuid().ToString("D"),
                ActorRolesJson = "[]",
                LastOperationKey = $"staff-mail-query-link:{receiptId:N}"
            });
        }
        await db.SaveChangesAsync();
        return new(caseId, receiptId, 0);
    }

    private static async Task<StaffMailOperation> MoveToSubmittedAsync(
        IStaffMailSendStore store,
        StaffMailSendCommand command,
        DateTimeOffset nowUtc)
    {
        var operation = await store.PrepareAsync(
            command, new string('A', 64), nowUtc, CancellationToken.None);
        operation = await store.TransitionAsync(
            command.Actor.SubjectId, operation.Id, operation.Version,
            StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft,
            null, null, null, null, CancellationToken.None);
        operation = await store.TransitionAsync(
            command.Actor.SubjectId, operation.Id, operation.Version,
            StaffMailState.DraftReady, StaffMailAttemptStage.Attach,
            "draft-id", null, null, null, CancellationToken.None);
        operation = await store.TransitionAsync(
            command.Actor.SubjectId, operation.Id, operation.Version,
            StaffMailState.Sending, StaffMailAttemptStage.Send,
            "draft-id", null, null, null, CancellationToken.None);
        return await store.TransitionAsync(
            command.Actor.SubjectId, operation.Id, operation.Version,
            StaffMailState.Submitted, StaffMailAttemptStage.ObserveSent,
            "draft-id", nowUtc.AddMinutes(1), null, null, CancellationToken.None);
    }

    private static async Task<string> ReadCaseWorkflowStateAsync(
        LocalDbTestDatabase database,
        Guid caseId)
    {
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        return await db.CaseWorkflows
            .Where(item => item.CaseId == caseId)
            .Select(item => item.State)
            .SingleAsync();
    }

    private sealed record QueryReplyFixture(Guid CaseId, Guid ReceiptId, long WorkflowVersion);

    private static StaffMailSendCommand Command() => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), Guid.NewGuid(), 1,
        StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1,
        StaffMailComposeMode.New, null, [new("recipient@example.invalid", null)], [],
        "Subject", "Body", [], "operation-key");

    private static StaffMailSendCommand ReplyCommand(
        Guid mailboxId, Guid retainedMessageId, string operationKey) => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), mailboxId, 1,
        StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1,
        StaffMailComposeMode.Reply,
        new(retainedMessageId, mailboxId, "immutable-message", "<message@example.invalid>",
            "conversation"),
        [new("recipient@example.invalid", null)], [], "Subject", "Body", [], operationKey);

    private static StaffMailSendCommand ReplyCommand(
        ActionActor actor, Guid mailboxId, Guid retainedMessageId, string operationKey) => new(
        actor, mailboxId, 1, StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1,
        StaffMailComposeMode.Reply,
        new(retainedMessageId, mailboxId, "immutable-message", "<message@example.invalid>",
            "conversation"),
        [new("recipient@example.invalid", null)], [], "Subject", "Body", [], operationKey);

    private static void AssertOperationContext(
        StaffMailOperation operation, StaffMailSendCommand command)
    {
        Assert.Equal(command.Purpose, operation.Purpose);
        Assert.Equal(command.ContextId, operation.ContextId);
        Assert.Equal(command.ExpectedContextVersion, operation.ExpectedContextVersion);
        Assert.Equal(command.OriginalMessage?.RetainedMessageId, operation.OriginalRetainedMessageId);
    }

    private static async Task<ActionActor> SeedStaffAsync(
        LocalDbTestDatabase database, string userName)
    {
        var staffId = Guid.NewGuid();
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var role = await db.Roles.SingleAsync(value => value.NormalizedName == "USER");
        db.Users.Add(new PegasusIdentityUser
        {
            Id = staffId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = role.Id });
        await db.SaveChangesAsync();
        return ActionActor.Staff(staffId, [StaffRole.User]);
    }

    private static async Task SeedRetainedMessageAsync(
        LocalDbTestDatabase database, Guid mailboxId, Guid retainedMessageId)
    {
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var retainedAtUtc = DateTimeOffset.UtcNow;
        db.ApprovedMailboxes.Add(new()
        {
            Id = mailboxId,
            Address = "mailbox@example.invalid",
            AllowInboundIntake = true,
            AllowStaffSend = true,
            MailboxGeneration = 1,
            VerifiedEncodedMessageSizeLimit = 1_000_000,
            State = "Approved",
            MailboxIdentity = "mailbox",
            InboxFolderIdentity = "inbox",
            ActivatedAtUtc = retainedAtUtc.AddDays(-1),
            Version = 1
        });
        db.ApprovedInboxPollStates.Add(new()
        {
            ApprovedMailboxId = mailboxId,
            MailboxAddress = "mailbox@example.invalid",
            ScopeFingerprint = new string('A', 64),
            Generation = 1,
            ActivatedAtUtc = retainedAtUtc.AddDays(-1),
            StartBoundaryUtc = retainedAtUtc.AddDays(-1),
            DueAtUtc = retainedAtUtc,
            LastCompletedAtUtc = retainedAtUtc
        });
        db.Set<RetainedMailboxMessageEntity>().Add(new()
        {
            Id = retainedMessageId,
            MailboxId = mailboxId,
            MailboxAddress = "mailbox@example.invalid",
            FolderScope = "Inbox",
            FolderIdentity = "inbox",
            ImmutableMessageId = "immutable-message",
            InternetMessageIdentity = "<message@example.invalid>",
            ConversationIdentity = "conversation",
            ExternalReceiptToken = $"retained:{retainedMessageId:N}",
            ToAddressesJson = "[]",
            CcAddressesJson = "[]",
            SourceLength = 1,
            SourceSha256 = new string('A', 64),
            ReceivedAtUtc = retainedAtUtc,
            RetainedAtUtc = retainedAtUtc
        });
        await db.SaveChangesAsync();
    }

    private static async Task<StaffMailOperation> MoveToTerminalAsync(
        IStaffMailSendStore store, string actorSubjectId, StaffMailOperation operation,
        StaffMailState terminalState)
    {
        if (terminalState is StaffMailState.Failed or StaffMailState.Cancelled)
        {
            return await store.TransitionAsync(
                actorSubjectId, operation.Id, operation.Version, terminalState,
                StaffMailAttemptStage.CreateDraft, null, null, null,
                terminalState == StaffMailState.Failed ? "known failure" : null,
                CancellationToken.None);
        }

        operation = await store.TransitionAsync(
            actorSubjectId, operation.Id, operation.Version, StaffMailState.DraftCreating,
            StaffMailAttemptStage.CreateDraft, null, null, null, null, CancellationToken.None);
        operation = await store.TransitionAsync(
            actorSubjectId, operation.Id, operation.Version, StaffMailState.DraftReady,
            StaffMailAttemptStage.Attach, "draft-id", null, null, null, CancellationToken.None);
        operation = await store.TransitionAsync(
            actorSubjectId, operation.Id, operation.Version, StaffMailState.Sending,
            StaffMailAttemptStage.Send, "draft-id", null, null, null, CancellationToken.None);
        return await store.TransitionAsync(
            actorSubjectId, operation.Id, operation.Version, StaffMailState.Sent,
            StaffMailAttemptStage.ObserveSent, "draft-id", DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, null, CancellationToken.None);
    }

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync(
        Action<IServiceCollection>? configureServices = null) =>
        LocalDbTestDatabase.CreateAsync(configureServices: services =>
        {
            services.AddScoped<EfStaffMailSendStore>();
            services.AddScoped<IStaffMailSendStore>(provider =>
                provider.GetRequiredService<EfStaffMailSendStore>());
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddScoped<IStaffMailUploadProgress, EfStaffMailUploadProgress>();
            services.AddScoped<IStaffMailExecutionLock, SqlStaffMailExecutionLock>();
            configureServices?.Invoke(services);
        });

    private static ApprovedMailboxEntity StaffSendMailbox(
        Guid id,
        string identity,
        bool allowSentEvidence,
        string? sentFolderIdentity) => new()
    {
        Id = id,
        Address = $"{identity}@collisionengineers.co.uk",
        AllowStaffSend = true,
        AllowSentEvidence = allowSentEvidence,
        State = ApprovedMailboxState.Approved.ToString(),
        MailboxIdentity = $"{identity}-mailbox",
        SentFolderIdentity = sentFolderIdentity,
        ActivatedAtUtc = DateTimeOffset.UtcNow,
        MailboxGeneration = 1,
        VerifiedEncodedMessageSizeLimit = 10485760,
        Version = 1
    };

    private sealed class ApprovedMailboxQueries(Guid mailboxId) : IApprovedStaffSendMailboxQueries
    {
        public Task<ApprovedStaffSendMailbox?> GetAsync(
            Guid requestedMailboxId, CancellationToken cancellationToken) =>
            Task.FromResult<ApprovedStaffSendMailbox?>(requestedMailboxId == mailboxId
                ? new(mailboxId, "mailbox@example.invalid", 1, 1_000_000)
                : null);
    }

    private sealed class UnusedLogicalDocumentReader : IReadLogicalDocumentVersion
    {
        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The attachment reader must not be used.");
    }

    private sealed class RecordingStaffMailTransport : IStaffMailTransport
    {
        private int createDraftCount;
        private int sendDraftCount;

        public int CreateDraftCount => Volatile.Read(ref this.createDraftCount);
        public int SendDraftCount => Volatile.Read(ref this.sendDraftCount);

        public Task ValidateEncodedSizeAsync(
            ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
            StaffMailSendCommand command, IReadOnlyList<StaffMailAttachmentContent> attachments,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StaffMailDraftLookupResult> FindDraftAsync(
            ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StaffMailDraftLookupResult(null, null, true));

        public Task<StaffMailDraftResult> CreateDraftAsync(
            ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
            StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.createDraftCount);
            return Task.FromResult(new StaffMailDraftResult("draft-id"));
        }

        public Task AttachAsync(
            ApprovedStaffSendMailbox mailbox, Guid operationId, string immutableDraftId,
            StaffMailAttachment attachment, Stream content, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No attachment is expected.");

        public Task<StaffMailSubmitResult> SendDraftAsync(
            ApprovedStaffSendMailbox mailbox, string immutableDraftId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.sendDraftCount);
            return Task.FromResult(new StaffMailSubmitResult(DateTimeOffset.UtcNow));
        }
    }
}
