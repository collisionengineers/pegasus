using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffMailSendPersistenceTests
{
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
