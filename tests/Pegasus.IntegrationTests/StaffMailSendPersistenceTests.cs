using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Email;
using Pegasus.IntegrationTests.Reports;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffMailSendPersistenceTests
{
    private static readonly JsonSerializerOptions ReportSnapshotJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task FinalStaffSendMailboxRequiresOutgoingAndResolvedSentFolder()
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
                StaffSendMailbox(missingScopeId, "missing-scope", allowOutgoing: false, sentFolderIdentity: "sent"),
                StaffSendMailbox(missingFolderId, "missing-folder", allowOutgoing: true, sentFolderIdentity: null),
                StaffSendMailbox(configuredId, "configured", allowOutgoing: true, sentFolderIdentity: "sent"));
            await db.SaveChangesAsync();
        }

        await using var queryScope = database.CreateAsyncScope();
        var mailboxes = queryScope.ServiceProvider.GetRequiredService<EfStaffMailSendStore>();
        Assert.Null(await mailboxes.GetAsync(missingScopeId, CancellationToken.None));
        Assert.Null(await mailboxes.GetAsync(missingFolderId, CancellationToken.None));
        var configured = await mailboxes.GetAsync(configuredId, CancellationToken.None);
        Assert.NotNull(configured);
        // Every Outgoing mailbox sends under the one fixed ceiling (operator, 12 October 2026).
        Assert.Equal(ApprovedStaffSendMailbox.OutgoingEncodedMessageSizeLimit, configured.EncodedMessageSizeLimit);
    }

    [Fact]
    public async Task ReportExecutionKeepsTheCaseForASelectedSubsetOfGenerationArtifacts()
    {
        await using var database = await CreateDatabaseAsync();
        var fixture = await SeedReportSentCaseAsync(
            database, Guid.NewGuid(), Guid.NewGuid(), isQueryReceipt: false, isAssociated: false);
        var generationId = Guid.NewGuid();
        var reportDocumentId = Guid.NewGuid();
        var reportVersionId = Guid.NewGuid();
        var reportSha256 = new string('A', 64);
        var companionDocumentId = Guid.NewGuid();
        var companionVersionId = Guid.NewGuid();
        var companionSha256 = new string('B', 64);
        var nowUtc = new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
        var input = AssessmentReportDraftWebTests.ReadyInput(fixture.CaseId);
        var estimate = input.CurrentEstimate!;
        var report = AssessmentReportProjection.Project(input).Snapshot!;
        var frozenGeneration = new CaseReportGenerationSnapshot(
            fixture.CaseId, 0, "report-case", "report-subset", CaseReportActor.None, nowUtc,
            Guid.Empty, new string('0', 64), "image/png",
            estimate.SpecificationId, estimate.Version, report.Costs, report.EngineerValue,
            Guid.Empty, report.Content, report.Guides, report.ReportDate,
            report.AgreedFee, report.FeeDescriptionLines,
            [], [], report.PayloadVersion, "renderer/test", report with { Photos = [] })
        {
            CurrentEstimate = estimate
        };

        await using (var scope = database.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.Set<CaseDocumentEntity>().AddRange(
                new()
                {
                    Id = reportDocumentId, CaseId = fixture.CaseId, Ordinal = 1,
                    SourceOccurrenceIdentity = "report-document"
                },
                new()
                {
                    Id = companionDocumentId, CaseId = fixture.CaseId, Ordinal = 2,
                    SourceOccurrenceIdentity = "companion-document"
                });
            db.Set<DocumentVersionEntity>().AddRange(
                new()
                {
                    Id = reportVersionId, DocumentId = reportDocumentId, Version = 1,
                    FileName = "AssessmentReport.pdf", MediaType = "application/pdf",
                    ContentLength = 10, Sha256 = reportSha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = nowUtc, CreatedBy = "test", IsCurrent = true
                },
                new()
                {
                    Id = companionVersionId, DocumentId = companionDocumentId, Version = 1,
                    FileName = "images.pdf", MediaType = "application/pdf",
                    ContentLength = 11, Sha256 = companionSha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = nowUtc, CreatedBy = "test", IsCurrent = true
                });
            db.Set<CaseReportGenerationEntity>().Add(new()
            {
                Id = generationId, CaseId = fixture.CaseId, WorkId = fixture.CaseId, CaseVersion = 0,
                SnapshotHash = new string('C', 64), SnapshotJson = JsonSerializer.Serialize(
                    frozenGeneration, ReportSnapshotJson),
                TemplateVersion = "test", RendererVersion = "test",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = nowUtc, Version = 1
            });
            db.CaseRepairSpecifications.Add(new()
            {
                Id = estimate.SpecificationId,
                WorkId = fixture.CaseId,
                Version = estimate.Version,
                State = nameof(RepairSpecificationState.Draft),
                SourceRoute = nameof(RepairSpecificationSourceRoute.Manual),
                CreatedBy = "Staff:test",
                CreationOperationKey = "mail-snapshot-estimate",
                CreatedAtUtc = nowUtc,
                Name = estimate.Details.Name,
                VatPercent = estimate.Details.VatPercent
            });
            db.Set<GeneratedCaseArtifactEntity>().AddRange(
                new()
                {
                    Id = Guid.NewGuid(), GenerationId = generationId,
                    VersionId = reportVersionId,
                    Kind = nameof(CaseReportArtifactKind.AssessmentReport),
                    Sha256 = reportSha256,
                    State = nameof(CaseReportArtifactStatus.Confirmed),
                    OperationKey = "report-artifact"
                },
                new()
                {
                    Id = Guid.NewGuid(), GenerationId = generationId,
                    VersionId = companionVersionId,
                    Kind = nameof(CaseReportArtifactKind.ImagePack),
                    Sha256 = companionSha256,
                    State = nameof(CaseReportArtifactStatus.Confirmed),
                    OperationKey = "companion-artifact"
                });
            await db.SaveChangesAsync();
        }

        await using var sendScope = database.CreateAsyncScope();
        var store = sendScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var command = new StaffMailSendCommand(
            actor, Guid.NewGuid(), 1, StaffMailPurpose.CaseReport, generationId, 1,
            StaffMailComposeMode.New, null, [new("recipient@example.invalid", null)], [],
            "Subject", "Body",
            [new(reportDocumentId, reportVersionId, reportSha256, 10,
                "AssessmentReport.pdf", "application/pdf")],
            "report-subset");

        var operation = await store.PrepareAsync(
            command, new string('A', 64), nowUtc, CancellationToken.None);
        var execution = await store.GetExecutionAsync(
            actor.SubjectId, operation.Id, CancellationToken.None);

        Assert.NotNull(execution);
        Assert.Equal(fixture.CaseId, execution!.CaseId);
        Assert.Single(execution.Attachments);

        // A submitted send leaves no Sent mark. The later live Draft is not
        // the estimate the immutable report generation delivered.
        var submitted = await MoveToSubmittedAsync(store, command, nowUtc);
        await using (var changed = await sendScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync())
        {
            Assert.Empty(await changed.CaseRepairSpecificationSnapshots.ToListAsync());
            var live = await changed.CaseRepairSpecifications.SingleAsync(item => item.Id == estimate.SpecificationId);
            live.Name = "Later draft";
            var generation = await changed.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId);
            generation.SnapshotJson = "null";
            await changed.SaveChangesAsync();
        }

        var observedAtUtc = nowUtc.AddMinutes(5);
        var observer = ActionActor.SystemWorker("sent-evidence-poll");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.TransitionObservedSentAsync(
            observer, submitted.Id, submitted.Version, "confirmed-report-message",
            observedAtUtc.AddMinutes(-1), observedAtUtc, CancellationToken.None));
        await using (var rolledBack = await sendScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync())
        {
            Assert.Empty(await rolledBack.CaseRepairSpecificationSnapshots.ToListAsync());
            Assert.Equal(StaffMailState.Submitted,
                (await rolledBack.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == submitted.Id)).State);
            var generation = await rolledBack.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId);
            generation.SnapshotJson = JsonSerializer.Serialize(frozenGeneration, ReportSnapshotJson);
            await rolledBack.SaveChangesAsync();
        }

        await Task.WhenAll(
            store.TransitionObservedSentAsync(observer, submitted.Id, submitted.Version,
                "confirmed-report-message", observedAtUtc.AddMinutes(-1), observedAtUtc,
                CancellationToken.None),
            store.TransitionObservedSentAsync(observer, submitted.Id, submitted.Version,
                "confirmed-report-message", observedAtUtc.AddMinutes(-1), observedAtUtc,
                CancellationToken.None));
        await store.TransitionObservedSentAsync(observer, submitted.Id, submitted.Version,
            "confirmed-report-message", observedAtUtc.AddMinutes(-1), observedAtUtc,
            CancellationToken.None);

        await using var verified = await sendScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var sentSnapshot = Assert.Single(await verified.CaseRepairSpecificationSnapshots.ToListAsync());
        Assert.Equal(nameof(RepairSpecificationSnapshotKind.Sent), sentSnapshot.Kind);
        Assert.Contains(submitted.Id.ToString("N"), sentSnapshot.Origin, StringComparison.Ordinal);
        Assert.Equal(estimate.Details.Name,
            EfRepairSpecificationSnapshotStore.Map(sentSnapshot).Details.Name);

        var failedCommand = command with { OperationKey = "report-subset-failed" };
        var failed = await store.PrepareAsync(failedCommand, new string('D', 64), nowUtc, CancellationToken.None);
        failed = await store.TransitionAsync(actor.SubjectId, failed.Id, failed.Version,
            StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft,
            null, null, null, null, CancellationToken.None);
        failed = await store.TransitionAsync(actor.SubjectId, failed.Id, failed.Version,
            StaffMailState.DraftReady, StaffMailAttemptStage.Attach,
            "failed-draft", null, null, null, CancellationToken.None);
        failed = await store.TransitionAsync(actor.SubjectId, failed.Id, failed.Version,
            StaffMailState.Sending, StaffMailAttemptStage.Send,
            "failed-draft", null, null, null, CancellationToken.None);
        _ = await store.TransitionAsync(actor.SubjectId, failed.Id, failed.Version,
            StaffMailState.Failed, StaffMailAttemptStage.Send,
            "failed-draft", null, null, "known failure", CancellationToken.None);
        await using var afterFailed = await sendScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        Assert.Single(await afterFailed.CaseRepairSpecificationSnapshots.ToListAsync());
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

    /// <summary>
    /// Operator, 12 October 2026: one retained message may be answered more than
    /// once, so two replies to it prepare two operations.
    /// </summary>
    [Fact]
    public async Task ConcurrentDistinctRepliesToOneRetainedMessageBothPrepare()
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

        var attempts = new[]
        {
            PrepareAsync(firstScope, firstCommand),
            PrepareAsync(secondScope, secondCommand)
        };
        start.SetResult();
        var operations = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(2, operations.Select(operation => operation.Id).Distinct().Count());
        Assert.All(operations, operation => Assert.Equal(StaffMailState.Prepared, operation.State));
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE MailboxId = '{mailboxId:D}' AND OriginalRetainedMessageId = '{retainedMessageId:D}'"));

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
                FolderScope = "inbox",
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
    public async Task ConcurrentDifferentActorRepliesEachDraftAndSend()
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

        Assert.All(results, result => Assert.Null(result.Error));
        Assert.Equal(2, results.Select(result => result.Operation!.Id).Distinct().Count());
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE OriginalRetainedMessageId = '{retainedMessageId:D}'"));
        Assert.Equal(2, transport.CreateDraftCount);
        Assert.Equal(2, transport.SendDraftCount);

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
    public async Task ASecondReplyIsAcceptedWhileTheFirstIsUnresolved()
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

        var next = await store.PrepareAsync(
            ReplyCommand(mailboxId, retainedMessageId, "second-reply"),
            new string('B', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotEqual(operation.Id, next.Id);
        Assert.Equal(StaffMailState.Prepared, next.State);
    }

    /// <summary>
    /// A report send is never held up by another unfinished operation on the
    /// instruction it answers (operator, 6 October 2026).
    /// </summary>
    [Fact]
    public async Task AReportSendIsNotHeldUpByAnUnfinishedReplyToTheSameMessage()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var reply = await store.PrepareAsync(
            ReplyCommand(mailboxId, retainedMessageId, "unfinished-reply"),
            new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        var report = await store.PrepareAsync(
            ReportReplyCommand(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), mailboxId, retainedMessageId, "report-send"),
            new string('B', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(StaffMailState.Prepared, reply.State);
        Assert.NotEqual(reply.Id, report.Id);
        Assert.Equal(StaffMailPurpose.CaseReport, report.Purpose);
        Assert.Equal(retainedMessageId, report.OriginalRetainedMessageId);
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE OriginalRetainedMessageId = '{retainedMessageId:D}'"));
    }

    /// <summary>
    /// An unfinished report send never holds up a reply to the instruction
    /// it answers.
    /// </summary>
    [Fact]
    public async Task AnUnfinishedReportSendDoesNotHoldUpAReply()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var report = await store.PrepareAsync(
            ReportReplyCommand(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), mailboxId, retainedMessageId, "report-send"),
            new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        var reply = await store.PrepareAsync(
            ReplyCommand(mailboxId, retainedMessageId, "first-reply"),
            new string('B', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(StaffMailState.Prepared, report.State);
        Assert.Equal(StaffMailState.Prepared, reply.State);
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM StaffMailSendOperations WHERE OriginalRetainedMessageId = '{retainedMessageId:D}'"));
    }

    /// <summary>
    /// The message page's latest operation is its own correspondence: a
    /// report reply to the same message is the Case's send and is never
    /// returned, even when it is the newer and the unfinished one.
    /// </summary>
    [Fact]
    public async Task TheLatestOperationForAMessageIsNeverAReportSend()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var reply = await store.PrepareAsync(
            ReplyCommand(actor, mailboxId, retainedMessageId, "earlier-reply"),
            new string('A', 64),
            new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero), CancellationToken.None);
        reply = await MoveToTerminalAsync(store, actor.SubjectId, reply, StaffMailState.Failed);
        _ = await store.PrepareAsync(
            ReportReplyCommand(actor, mailboxId, retainedMessageId, "later-report"),
            new string('B', 64),
            new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero), CancellationToken.None);
        var reportOnly = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        _ = await store.PrepareAsync(
            ReportReplyCommand(reportOnly, mailboxId, retainedMessageId, "report-only"),
            new string('C', 64),
            new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero), CancellationToken.None);

        var latest = await store.GetLatestForOriginalAsync(
            actor.SubjectId, retainedMessageId, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(reply.Id, latest.Id);
        Assert.Equal(StaffMailPurpose.GeneralCorrespondence, latest.Purpose);
        Assert.Null(await store.GetLatestForOriginalAsync(
            reportOnly.SubjectId, retainedMessageId, CancellationToken.None));
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

    /// <summary>
    /// FRD-16 Notes, FRD-21: a general correspondence send, once its Sent item
    /// is observed, leaves one Notes line on its Case (the staff sender, the
    /// provider's sent time, the subject, at the Case's current version), and
    /// the Case's Correspondence lists the retained Sent item as Sent. A
    /// replayed observation writes nothing more.
    /// </summary>
    [Fact]
    public async Task AnObservedCorrespondenceSendIsNotedOnTheCaseAndListedAsSentCorrespondence()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        // The fixture's receipt stays unassociated: only the Sent item is
        // this Case's correspondence here.
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: false, isAssociated: false);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "noted-correspondence") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion,
            Subject = "Following up on your claim"
        };
        var operation = await MoveToSubmittedAsync(
            store, command, new DateTimeOffset(2026, 10, 5, 7, 8, 0, TimeSpan.Zero));
        var sentAtUtc = new DateTimeOffset(2026, 10, 5, 7, 8, 59, TimeSpan.Zero);
        var observer = ActionActor.SystemWorker("sent-evidence-poll");

        await store.TransitionObservedSentAsync(
            observer, operation.Id, operation.Version, "observed-sent-item",
            sentAtUtc, sentAtUtc.AddMinutes(1), CancellationToken.None);
        await store.TransitionObservedSentAsync(
            observer, operation.Id, operation.Version, "observed-sent-item",
            sentAtUtc, sentAtUtc.AddMinutes(1), CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IRetainedMailboxMessageStore>().RetainAsync(
            new RetainedMailboxMessage(
                mailboxId,
                "mailbox@example.invalid",
                "observed-sent-item",
                "sent:observed-sent-item",
                sentAtUtc,
                1024,
                new string('B', 64),
                new(
                    "sent-items",
                    "conversation",
                    $"<{StaffMailCorrelationHeaders.MessageId(operation.Id)}>",
                    "mailbox@example.invalid",
                    null,
                    ["recipient@example.invalid"],
                    [],
                    [],
                    "Following up on your claim",
                    "Body",
                    [],
                    IsRead: true),
                sentAtUtc.AddMinutes(1),
                MailFolderScope.Sent),
            CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var noted = Assert.Single(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "correspondence_sent")
            .ToListAsync());
        Assert.Equal("Following up on your claim", noted.Reason);
        Assert.Equal(command.Actor.SubjectId, noted.ActorSubjectId);
        Assert.Equal(nameof(ActorKind.Staff), noted.ActorKind);
        Assert.Equal(sentAtUtc, noted.OccurredAtUtc);
        Assert.Equal(noted.BeforeVersion, noted.AfterVersion);
        Assert.Contains(operation.Id.ToString("D"), noted.ResultJson, StringComparison.OrdinalIgnoreCase);

        var files = await verify.ServiceProvider.GetRequiredService<Pegasus.Core.Cases.ICaseQueryStore>()
            .GetFilesSectionAsync(fixture.CaseId, includeDocuments: false, frame: null, CancellationToken.None);
        Assert.NotNull(files);
        var sentRow = Assert.Single(files!.CorrespondenceEmails, item => item.IsSent);
        Assert.Equal("Following up on your claim", sentRow.Subject);
        Assert.Equal(sentAtUtc, sentRow.ReceivedAtUtc);
        Assert.Equal("mailbox@example.invalid", sentRow.SenderAddress);
        Assert.Null(sentRow.Classification);
    }

    /// <summary>
    /// The Automation Actor sends as staff do (ADR-0064 phase 4): its send
    /// records the Automation Actor as sender on the operation row, on every
    /// history row of the send (with no staff roles) and on the Case's Notes
    /// line once the Sent item is observed. Nothing reads it back as staff.
    /// </summary>
    [Fact]
    public async Task AnAutomationSendRecordsTheAutomationActorOnItsOperationHistoryAndNote()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: false, isAssociated: false);
        var automation = ActionActor.Automation("pegasus-automation");
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(automation, mailboxId, retainedMessageId, "mcp:automation-reply") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion,
            Subject = "Automation follow-up"
        };
        var operation = await MoveToSubmittedAsync(
            store, command, new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        var sentAtUtc = new DateTimeOffset(2026, 10, 7, 9, 1, 0, TimeSpan.Zero);
        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"), operation.Id, operation.Version,
            "automation-sent-item", sentAtUtc, sentAtUtc.AddMinutes(1), CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id);
        Assert.Equal(ActorKind.Automation, row.ActorKind);
        Assert.Equal("pegasus-automation", row.ActorSubjectId);
        Assert.Equal(nameof(ActorKind.Automation), await database.ScalarAsync<string>(
            $"SELECT ActorKind FROM StaffMailSendOperations WHERE Id = '{operation.Id:D}'"));

        var senderHistory = await db.ActionHistory
            .Where(item => item.AggregateType == "StaffMailSend"
                && item.AggregateId == operation.Id.ToString("D")
                && item.EventKind != "staff-mail-sent-observed")
            .ToListAsync();
        Assert.Equal(
            ["staff-mail-draftcreating", "staff-mail-draftready", "staff-mail-prepared", "staff-mail-sending", "staff-mail-submitted"],
            senderHistory.Select(item => item.EventKind).Order(StringComparer.Ordinal).ToArray());
        Assert.All(senderHistory, item =>
        {
            Assert.Equal(nameof(ActorKind.Automation), item.ActorKind);
            Assert.Equal("pegasus-automation", item.ActorSubjectId);
            Assert.Equal("[]", item.ActorRolesJson);
        });

        var noted = Assert.Single(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "correspondence_sent")
            .ToListAsync());
        Assert.Equal(nameof(ActorKind.Automation), noted.ActorKind);
        Assert.Equal("pegasus-automation", noted.ActorSubjectId);
        Assert.Equal("Automation follow-up", noted.Reason);
    }

    /// <summary>Every send recorded before the Automation Actor could send was a staff send.</summary>
    [Fact]
    public async Task AStaffSendRecordsTheStaffActorKind()
    {
        await using var database = await CreateDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var operation = await store.PrepareAsync(
            Command(), new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(nameof(ActorKind.Staff), await database.ScalarAsync<string>(
            $"SELECT ActorKind FROM StaffMailSendOperations WHERE Id = '{operation.Id:D}'"));
    }

    /// <summary>
    /// FRD-13 "Report sent and queries" (operator, 11 October 2026): a reply
    /// ticked "Also resolve query" resolves every open query on its Case once
    /// its Sent item is observed, including one raised after the reply was
    /// submitted. The resolution is the Case's own event, keyed to the send;
    /// replaying the observation writes nothing more.
    /// </summary>
    [Fact]
    public async Task AFlaggedReplyResolvesEveryOpenQueryWhenItsSentItemIsObserved()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "resolve-query-reply") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion,
            ResolvesCaseQueries = true
        };
        var operation = await MoveToSubmittedAsync(
            store, command, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        // Logged after the reply was submitted, before its Sent item is seen.
        await SeedOpenStaffQueryAsync(factory, fixture.CaseId);
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);
        var observer = ActionActor.SystemWorker("sent-evidence-poll");

        await store.TransitionObservedSentAsync(
            observer, operation.Id, operation.Version, "confirmed-sent-item",
            observedAtUtc.AddMinutes(-1), observedAtUtc, CancellationToken.None);
        await store.TransitionObservedSentAsync(
            observer, operation.Id, operation.Version, "confirmed-sent-item",
            observedAtUtc.AddMinutes(-1), observedAtUtc, CancellationToken.None);

        await using var db = await factory.CreateDbContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), workflow.State);
        Assert.False(workflow.HasOpenQuery);
        Assert.Null(workflow.ClosureOutcome);
        Assert.Equal(fixture.WorkflowVersion + 1, workflow.Version);
        var queries = await db.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync();
        Assert.Equal(
            [nameof(CaseQuerySource.Mail), nameof(CaseQuerySource.Staff)],
            queries.Select(item => item.Source).Order(StringComparer.Ordinal).ToArray());
        Assert.All(queries, query =>
        {
            Assert.Equal(nameof(CaseQueryClosure.Resolved), query.ClosedAs);
            Assert.Equal(observedAtUtc, query.ClosedAtUtc);
            Assert.Equal(nameof(ActorKind.Staff), query.ClosedByKind);
            Assert.Equal(command.Actor.SubjectId, query.ClosedBySubjectId);
            Assert.Equal(operation.Id, query.ResolvedByMailOperationId);
        });
        var resolution = Assert.Single(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_queries_resolved")
            .ToListAsync());
        Assert.Equal($"query-reply:{operation.Id:N}", resolution.OperationKey);
        Assert.Equal(command.Actor.SubjectId, resolution.ActorSubjectId);
        Assert.Equal(fixture.WorkflowVersion, resolution.BeforeVersion);
        Assert.Equal(workflow.Version, resolution.AfterVersion);
        Assert.Single(await db.ActionHistory
            .Where(item => item.AggregateType == "case"
                && item.AggregateId == fixture.CaseId.ToString("D")
                && item.EventKind == "case_queries_resolved")
            .ToListAsync());
        var sent = await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id);
        Assert.True(sent.ResolvesCaseQueries);
        Assert.Equal(StaffMailState.Sent, sent.State);
        Assert.Equal("confirmed-sent-item", sent.ObservedSentImmutableMessageId);
    }

    /// <summary>A reply without the tick resolves nothing, before or after its Sent item is seen.</summary>
    [Fact]
    public async Task AnUnflaggedReplyLeavesTheOpenQueryOpen()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "unflagged-query-reply") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion
        };
        var operation = await MoveToSubmittedAsync(
            store, command, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);

        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"), operation.Id, operation.Version,
            "confirmed-unflagged-sent-item", observedAtUtc.AddMinutes(-1), observedAtUtc,
            CancellationToken.None);

        await using var db = await factory.CreateDbContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), workflow.State);
        Assert.True(workflow.HasOpenQuery);
        Assert.Equal(fixture.WorkflowVersion, workflow.Version);
        var query = Assert.Single(await db.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync());
        Assert.Null(query.ClosedAtUtc);
        Assert.Empty(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_queries_resolved")
            .ToListAsync());
        Assert.False((await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id))
            .ResolvesCaseQueries);
    }

    /// <summary>A flagged reply on a Case with nothing open reaches Sent and changes nothing on the Case.</summary>
    [Fact]
    public async Task AFlaggedReplyWithNothingOpenLeavesTheCaseUnchanged()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: false, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ReplyCommand(mailboxId, retainedMessageId, "flagged-nothing-open") with
        {
            ContextId = fixture.CaseId,
            ExpectedContextVersion = fixture.WorkflowVersion,
            ResolvesCaseQueries = true
        };
        var operation = await MoveToSubmittedAsync(
            store, command, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);

        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"), operation.Id, operation.Version,
            "confirmed-nothing-open", observedAtUtc.AddMinutes(-1), observedAtUtc,
            CancellationToken.None);

        await using var db = await factory.CreateDbContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), workflow.State);
        Assert.False(workflow.HasOpenQuery);
        Assert.Equal(fixture.WorkflowVersion, workflow.Version);
        Assert.Empty(await db.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync());
        Assert.Empty(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_queries_resolved")
            .ToListAsync());
        Assert.Equal(StaffMailState.Sent,
            (await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id)).State);
    }

    /// <summary>
    /// A received post-report message linked by staff raises a query only on a
    /// Report sent Case (operator, 11 October 2026); on a Case still with the
    /// Engineer it is plain correspondence.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.ReportSent, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, false)]
    public async Task AReceivedPostReportMessageLinkedToTheCaseRaisesAQueryOnlyOnceTheReportIsSent(
        CaseLifecycleState state, bool raises)
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: false, state);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();

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
        Assert.Equal(state.ToString(), persisted.State);
        Assert.Equal(raises, persisted.HasOpenQuery);
        Assert.Equal(fixture.WorkflowVersion + 1, persisted.Version);
        var queries = await verify.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync();
        Assert.Equal(raises ? 1 : 0, await verify.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == fixture.CaseId && item.EventType == "case_query_received"));
        if (!raises)
        {
            Assert.Empty(queries);
            Assert.Equal(1, await verify.CaseWorkflowEvents.CountAsync(item =>
                item.CaseId == fixture.CaseId && item.EventType == "intake_case_linked"));
            return;
        }
        var query = Assert.Single(queries);
        Assert.Equal(nameof(CaseQuerySource.Mail), query.Source);
        Assert.Equal(fixture.ReceiptId, query.IntakeReceiptId);
        Assert.Null(query.ClosedAtUtc);
        Assert.Equal(nameof(ActorKind.Staff), query.RaisedByKind);
        Assert.Equal(actor.SubjectId, query.RaisedBySubjectId);
        Assert.Equal("link-received-query", query.RaisedOperationKey);
        Assert.Single(await verify.ActionHistory
            .Where(item => item.AggregateType == "case"
                && item.AggregateId == fixture.CaseId.ToString("D")
                && item.EventKind == "case_query_received")
            .ToListAsync());
    }

    [Fact]
    public async Task UnlinkingTheOnlyPostReportMessageWithdrawsItsQuery()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        // The fixture's Report sent Case holds this linked message's open query.
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var workflowStore = new EfCaseWorkflowStore(factory, TimeProvider.System);
        var lease = await workflowStore.ClaimAsync(
            new(fixture.CaseId, fixture.WorkflowVersion, actor, "claim-unlink-query"),
            CancellationToken.None);
        var mutationStore = new EfIntakeMutationStore(factory);
        var receipt = await mutationStore.GetAsync(fixture.ReceiptId, CancellationToken.None);
        Assert.NotNull(receipt);

        await mutationStore.ReverseLinkAsync(
            new(
                fixture.ReceiptId,
                fixture.CaseId,
                receipt!.IntakeVersion,
                fixture.WorkflowVersion,
                lease.Token,
                actor,
                "unlink-received-query",
                "This message is not a query on this Case."),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), persisted.State);
        Assert.False(persisted.HasOpenQuery);
        Assert.Null(persisted.ClosureOutcome);
        Assert.Equal(fixture.WorkflowVersion + 1, persisted.Version);
        var query = Assert.Single(await verify.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync());
        Assert.Equal(nameof(CaseQueryClosure.Withdrawn), query.ClosedAs);
        Assert.NotNull(query.ClosedAtUtc);
        Assert.Equal(nameof(ActorKind.Staff), query.ClosedByKind);
        Assert.Equal(actor.SubjectId, query.ClosedBySubjectId);
        Assert.Null(query.ResolvedByMailOperationId);
        var withdrawn = Assert.Single(await verify.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_query_withdrawn")
            .ToListAsync());
        Assert.Equal(fixture.WorkflowVersion, withdrawn.BeforeVersion);
        Assert.Equal(persisted.Version, withdrawn.AfterVersion);
        Assert.Single(await verify.ActionHistory
            .Where(item => item.AggregateType == "case"
                && item.AggregateId == fixture.CaseId.ToString("D")
                && item.EventKind == "case_query_withdrawn")
            .ToListAsync());
    }

    [Fact]
    public async Task UnlinkingOnePostReportMessageKeepsTheCaseMarkedWhileAnotherQueryIsOpen()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var secondReceiptId = await SeedSecondLinkedPostReportReceiptAsync(factory, fixture.CaseId);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var workflowStore = new EfCaseWorkflowStore(factory, TimeProvider.System);
        var lease = await workflowStore.ClaimAsync(
            new(fixture.CaseId, fixture.WorkflowVersion, actor, "claim-unlink-one-query"),
            CancellationToken.None);
        var mutationStore = new EfIntakeMutationStore(factory);
        var receipt = await mutationStore.GetAsync(fixture.ReceiptId, CancellationToken.None);

        await mutationStore.ReverseLinkAsync(
            new(
                fixture.ReceiptId,
                fixture.CaseId,
                receipt!.IntakeVersion,
                fixture.WorkflowVersion,
                lease.Token,
                actor,
                "unlink-one-received-query",
                "Not this one."),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), persisted.State);
        Assert.True(persisted.HasOpenQuery);
        var queries = await verify.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync();
        Assert.Equal(
            nameof(CaseQueryClosure.Withdrawn),
            Assert.Single(queries, item => item.IntakeReceiptId == fixture.ReceiptId).ClosedAs);
        Assert.Null(Assert.Single(queries, item => item.IntakeReceiptId == secondReceiptId).ClosedAtUtc);
        Assert.Single(await verify.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_query_withdrawn")
            .ToListAsync());
    }

    [Fact]
    public async Task CorrectingTheOnlyLinkedPostReportMessageAwayWithdrawsTheQuery()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: true, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var dossier = await scope.ServiceProvider.GetRequiredService<CorrectRetainedMailClassification>().ExecuteAsync(
            actor,
            new(retainedMessageId, 1, MailCategory.General,
                "A summary, not a query."));

        Assert.Equal(2, dossier!.Version);
        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), persisted.State);
        Assert.False(persisted.HasOpenQuery);
        Assert.Equal(fixture.WorkflowVersion + 1, persisted.Version);
        Assert.Equal(
            nameof(CaseQueryClosure.Withdrawn),
            Assert.Single(await verify.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync()).ClosedAs);
        var withdrawn = Assert.Single(await verify.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_query_withdrawn")
            .ToListAsync());
        Assert.Equal(actor.SubjectId, withdrawn.ActorSubjectId);
        Assert.StartsWith("mail-correction:", withdrawn.OperationKey, StringComparison.Ordinal);
        Assert.Equal(1L, await verify.IntakeMailClassificationHistory.LongCountAsync());
    }

    [Fact]
    public async Task CorrectingALinkedMessageToQueryOnAReportSentCaseRaisesAQuery()
    {
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        // A Report sent Case whose linked message was read as general correspondence.
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: false, isAssociated: true);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var dossier = await scope.ServiceProvider.GetRequiredService<CorrectRetainedMailClassification>().ExecuteAsync(
            actor,
            new(retainedMessageId, 1, MailCategory.Query,
                "The client is querying the delivered report."));

        Assert.Equal(2, dossier!.Version);
        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), persisted.State);
        Assert.True(persisted.HasOpenQuery);
        Assert.Null(persisted.ClosureOutcome);
        Assert.Equal(fixture.WorkflowVersion + 1, persisted.Version);
        var query = Assert.Single(await verify.CaseQueries.Where(item => item.CaseId == fixture.CaseId).ToListAsync());
        Assert.Equal(nameof(CaseQuerySource.Mail), query.Source);
        Assert.Equal(fixture.ReceiptId, query.IntakeReceiptId);
        Assert.Null(query.ClosedAtUtc);
        var received = Assert.Single(await verify.CaseWorkflowEvents
            .Where(item => item.CaseId == fixture.CaseId && item.EventType == "case_query_received")
            .ToListAsync());
        Assert.Equal(actor.SubjectId, received.ActorSubjectId);
        Assert.Single(await verify.ActionHistory
            .Where(item => item.AggregateType == "case"
                && item.AggregateId == fixture.CaseId.ToString("D")
                && item.EventKind == "case_query_received")
            .ToListAsync());
    }

    /// <summary>
    /// What a correction tells its caller about the linked Case's queries:
    /// raised when a message becomes a query on a Report sent Case,
    /// withdrawn when it stops being one, and nothing for a correction that
    /// keeps it a query or on a Case whose report is not yet sent.
    /// </summary>
    [Theory]
    [InlineData("to-query", CaseLifecycleState.ReportSent, false, MailCategory.Query, true, false)]
    [InlineData("away", CaseLifecycleState.ReportSent, true, MailCategory.General, false, true)]
    [InlineData("stays-query", CaseLifecycleState.ReportSent, true, MailCategory.Query, false, false)]
    [InlineData("before-report-sent", CaseLifecycleState.ReportPreparation, false, MailCategory.Query, false, false)]
    public async Task ACorrectionReportsWhetherItRaisedOrWithdrewTheLinkedCasesQuery(
        string row,
        CaseLifecycleState state,
        bool startsPostReport,
        MailCategory category,
        bool raised,
        bool withdrawn)
    {
        _ = row;
        await using var database = await CreateDatabaseAsync();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await SeedRetainedMessageAsync(database, mailboxId, retainedMessageId);
        var fixture = await SeedReportSentCaseAsync(
            database, mailboxId, retainedMessageId, isQueryReceipt: startsPostReport, isAssociated: true, state);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var store = scope.ServiceProvider.GetRequiredService<IRetainedMailClassificationStore>();
        var current = (await store.GetClassificationAsync(retainedMessageId, CancellationToken.None))!;
        var after = MailClassificationResult.Classified(
            category,
            current.Current.Predicates,
            "Corrected by staff.",
            current.Current.PolicyKey,
            current.Current.PolicyVersion);

        var result = await store.AppendCorrectionAsync(
            retainedMessageId, current.Version, current.Current, after,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), "Corrected by staff.",
            DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(fixture.CaseId, result.LinkedCaseId);
        Assert.Equal(raised, result.QueryRaised);
        Assert.Equal(withdrawn, result.QueryWithdrawn);
        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.CaseWorkflows.SingleAsync(item => item.CaseId == fixture.CaseId);
        Assert.Equal(state.ToString(), persisted.State);
        Assert.Equal(
            fixture.WorkflowVersion + (raised || withdrawn ? 1 : 0),
            persisted.Version);
        var openQuery = await verify.CaseQueries.AnyAsync(item =>
            item.CaseId == fixture.CaseId && item.ClosedAtUtc == null);
        Assert.Equal(openQuery, persisted.HasOpenQuery);
        // The open query stays open across a correction that keeps it a query.
        Assert.Equal(raised || (startsPostReport && !withdrawn), openQuery);
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
    public async Task CaseChaserReachingSentRecordsTheChaseOnTheCase()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedChaserCaseAsync(database, CaseLifecycleState.NotReady);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = ChaserCommand(caseId, "case-chaser-sent",
            [new("instruction@example.invalid", null), new("repairer@example.invalid", "Repairer")]);
        var operation = await MoveToSubmittedAsync(
            store, command, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);
        var sentAtUtc = observedAtUtc.AddMinutes(-1);

        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"),
            operation.Id, operation.Version, "chaser-sent-item",
            sentAtUtc, observedAtUtc, CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var chase = Assert.Single(await db.CaseManualChases.Where(item => item.CaseId == caseId).ToListAsync());
        Assert.Equal($"chaser-sent:{operation.Id:N}", chase.OperationKey);
        Assert.Equal("E-mail", chase.Channel);
        Assert.Equal("Sent", chase.Outcome);
        Assert.Equal(sentAtUtc, chase.AttemptedAtUtc);
        Assert.Equal("instruction@example.invalid; repairer@example.invalid", chase.TargetPartyOrAddress);
        Assert.Equal(nameof(ActorKind.Staff), chase.ActorKind);
        Assert.Equal(command.Actor.SubjectId, chase.ActorSubjectId);
        var interval = (await EfWorkflowConfigurationStore.ReadAsync(db, CancellationToken.None)).ChaseIntervalDays;
        var due = await db.CaseDueWork.SingleAsync(item => item.CaseId == caseId);
        Assert.Equal("Sent", due.MostRecentOutcome);
        Assert.Equal("E-mail", due.MostRecentChannel);
        Assert.Equal(CaseChaseSchedule.NextChaseAt(sentAtUtc, interval), due.NextChaseAtUtc);
        Assert.Equal(1L, due.Version);
        var workflow = await db.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        Assert.Equal(1L, workflow.Version);
        Assert.Equal(nameof(CaseLifecycleState.NotReady), workflow.State);
        var recorded = Assert.Single(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == caseId && item.EventType == "chaser_sent")
            .ToListAsync());
        Assert.Equal(0L, recorded.BeforeVersion);
        Assert.Equal(1L, recorded.AfterVersion);
        Assert.Equal("E-mail to instruction@example.invalid; repairer@example.invalid: Sent", recorded.Reason);
        Assert.Equal(StaffMailState.Sent,
            (await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id)).State);
    }

    [Fact]
    public async Task CaseChaserSentOutsideNotReadyRecordsNoChase()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedChaserCaseAsync(database, CaseLifecycleState.Review);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var operation = await MoveToSubmittedAsync(
            store,
            ChaserCommand(caseId, "case-chaser-review", [new("instruction@example.invalid", null)]),
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);

        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"),
            operation.Id, operation.Version, "chaser-review-sent-item",
            observedAtUtc.AddMinutes(-1), observedAtUtc, CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(StaffMailState.Sent,
            (await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id)).State);
        Assert.Empty(await db.CaseManualChases.Where(item => item.CaseId == caseId).ToListAsync());
        Assert.Equal(0L, (await db.CaseWorkflows.SingleAsync(item => item.CaseId == caseId)).Version);
        Assert.Null((await db.CaseDueWork.SingleAsync(item => item.CaseId == caseId)).MostRecentOutcome);
    }

    [Fact]
    public async Task CaseChaserSentObservedAgainRecordsOneChase()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedChaserCaseAsync(database, CaseLifecycleState.NotReady);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var operation = await MoveToSubmittedAsync(
            store,
            ChaserCommand(caseId, "case-chaser-replay", [new("instruction@example.invalid", null)]),
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);
        var observer = ActionActor.SystemWorker("sent-evidence-poll");
        await using var secondScope = database.CreateAsyncScope();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();

        await Task.WhenAll(
            store.TransitionObservedSentAsync(observer, operation.Id, operation.Version,
                "chaser-replay-sent-item", observedAtUtc.AddMinutes(-1), observedAtUtc,
                CancellationToken.None),
            secondStore.TransitionObservedSentAsync(observer, operation.Id, operation.Version,
                "chaser-replay-sent-item", observedAtUtc.AddMinutes(-1), observedAtUtc,
                CancellationToken.None));
        await store.TransitionObservedSentAsync(observer, operation.Id, operation.Version,
            "chaser-replay-sent-item", observedAtUtc.AddMinutes(-1), observedAtUtc,
            CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.Single(await db.CaseManualChases.Where(item => item.CaseId == caseId).ToListAsync());
        Assert.Single(await db.CaseWorkflowEvents
            .Where(item => item.CaseId == caseId && item.EventType == "chaser_sent")
            .ToListAsync());
        Assert.Equal(1L, (await db.CaseWorkflows.SingleAsync(item => item.CaseId == caseId)).Version);
    }

    [Fact]
    public async Task CaseChaserToManyAddressesStoresABoundedTarget()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedChaserCaseAsync(database, CaseLifecycleState.NotReady);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        StaffMailRecipient[] recipients =
        [
            .. Enumerable.Range(1, 30).Select(index => new StaffMailRecipient(
                $"recipient-{index:D2}-with-a-long-mailbox-name@example.invalid", null))
        ];
        Assert.True(string.Join("; ", recipients.Select(value => value.Address)).Length > 500);
        var operation = await MoveToSubmittedAsync(
            store,
            ChaserCommand(caseId, "case-chaser-many", recipients),
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        var observedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 5, 0, TimeSpan.Zero);

        await store.TransitionObservedSentAsync(
            ActionActor.SystemWorker("sent-evidence-poll"),
            operation.Id, operation.Version, "chaser-many-sent-item",
            observedAtUtc.AddMinutes(-1), observedAtUtc, CancellationToken.None);

        await using var verify = database.CreateAsyncScope();
        var factory = verify.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(StaffMailState.Sent,
            (await db.Set<StaffMailSendOperationEntity>().SingleAsync(item => item.Id == operation.Id)).State);
        var chase = Assert.Single(await db.CaseManualChases.Where(item => item.CaseId == caseId).ToListAsync());
        Assert.True(chase.TargetPartyOrAddress.Length <= 500);
        Assert.Equal($"{recipients[0].Address} +29 more", chase.TargetPartyOrAddress);
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

    // A Case in the given state with one retained receipt. A post-report
    // receipt linked to a Report sent Case holds its open Mail query, as the
    // link would have raised it.
    private static async Task<QueryReplyFixture> SeedReportSentCaseAsync(
        LocalDbTestDatabase database,
        Guid mailboxId,
        Guid retainedMessageId,
        bool isQueryReceipt,
        bool isAssociated,
        CaseLifecycleState state = CaseLifecycleState.ReportSent)
    {
        var openQuery = isQueryReceipt && isAssociated && state == CaseLifecycleState.ReportSent;
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
            $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {"Query reply test organization"}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {nowUtc})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, ReportSendingRulesJson, Version) VALUES ({principalId}, {organizationId}, {"QRY"}, {lineageId}, {true}, {EfOrganizationAdministration.DefaultReportSendingJson}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {1}, {"QRY260001"}, {"inspection"}, {"review"}, {"pending"}, {true}, {true}, {nowUtc}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(db);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, HasOpenQuery, Version, ConcurrencyToken) VALUES ({caseId}, {state.ToString()}, {openQuery}, {0L}, {Guid.NewGuid()})");
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
                Category = isQueryReceipt ? "query" : "general",
                IsReplyContext = false,
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
        if (openQuery)
        {
            db.CaseQueries.Add(OpenMailQuery(caseId, receiptId, nowUtc));
        }
        await db.SaveChangesAsync();
        return new(caseId, receiptId, 0);
    }

    private static CaseQueryEntity OpenMailQuery(Guid caseId, Guid receiptId, DateTimeOffset raisedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        Source = nameof(CaseQuerySource.Mail),
        IntakeReceiptId = receiptId,
        RaisedAtUtc = raisedAtUtc,
        RaisedByKind = nameof(ActorKind.Staff),
        RaisedBySubjectId = Guid.NewGuid().ToString("D"),
        RaisedOperationKey = $"staff-mail-query-link:{receiptId:N}"
    };

    // A staff-logged open query on the Case, marking it as the ledger would.
    private static async Task SeedOpenStaffQueryAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.CaseQueries.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Source = nameof(CaseQuerySource.Staff),
            Channel = nameof(CaseQueryChannel.Phone),
            Details = "Rang about the report.",
            RaisedAtUtc = new DateTimeOffset(2026, 9, 10, 9, 2, 0, TimeSpan.Zero),
            RaisedByKind = nameof(ActorKind.Staff),
            RaisedBySubjectId = Guid.NewGuid().ToString("D"),
            RaisedOperationKey = $"log-query:{Guid.NewGuid():N}"
        });
        var workflow = await db.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.HasOpenQuery = true;
        await db.SaveChangesAsync();
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

    private sealed record QueryReplyFixture(Guid CaseId, Guid ReceiptId, long WorkflowVersion);

    // A Case in the given state with scheduled due work and nothing linked.
    private static async Task<Guid> SeedChaserCaseAsync(
        LocalDbTestDatabase database,
        CaseLifecycleState state)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var nowUtc = new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {"Chaser test organization"}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {nowUtc})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, ReportSendingRulesJson, Version) VALUES ({principalId}, {organizationId}, {"CHS"}, {lineageId}, {true}, {EfOrganizationAdministration.DefaultReportSendingJson}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {1}, {"CHS260001"}, {"inspection"}, {"review"}, {"pending"}, {true}, {true}, {nowUtc}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(db);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {state.ToString()}, {0L}, {Guid.NewGuid()})");
        db.CaseDueWork.Add(new()
        {
            CaseId = caseId,
            MissingMaterialReason = "Waiting for images",
            State = nameof(CaseDueWorkState.Scheduled),
            NextChaseAtUtc = nowUtc.AddDays(1),
            Version = 0
        });
        await db.SaveChangesAsync();
        return caseId;
    }

    private static StaffMailSendCommand ChaserCommand(
        Guid caseId, string operationKey, IReadOnlyList<StaffMailRecipient> to) => Command() with
    {
        Purpose = StaffMailPurpose.CaseChaser,
        ContextId = caseId,
        ExpectedContextVersion = 1,
        To = to,
        OperationKey = operationKey
    };

    // A second received post-report receipt, linked by staff to the same Report
    // sent Case and holding its own open query.
    private static async Task<Guid> SeedSecondLinkedPostReportReceiptAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId)
    {
        var receiptId = Guid.NewGuid();
        var nowUtc = new DateTimeOffset(2026, 9, 10, 8, 30, 0, TimeSpan.Zero);
        await using var db = await factory.CreateDbContextAsync();
        db.IntakeReceipts.Add(new IntakeReceiptEntity
        {
            Id = receiptId,
            SourceFileName = "second-post-report-query.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('B', 64),
            SourceChannel = "mailbox",
            ExternalReceiptToken = $"retained:{Guid.NewGuid():N}",
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
                Category = "query",
                IsReplyContext = false,
                PredicatesJson = "{\"version\":1,\"data\":[]}",
                Reason = "staff-mail test classification",
                PolicyKey = "staff-mail-test",
                PolicyVersion = 1,
                DecidedByActor = "test",
                DecidedAtUtc = nowUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            }
        });
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
        db.CaseQueries.Add(OpenMailQuery(caseId, receiptId, nowUtc));
        await db.SaveChangesAsync();
        return receiptId;
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

    /// <summary>A report send that replies to the retained instruction, as SendCaseReport composes it.</summary>
    private static StaffMailSendCommand ReportReplyCommand(
        ActionActor actor, Guid mailboxId, Guid retainedMessageId, string operationKey) =>
        ReplyCommand(actor, mailboxId, retainedMessageId, operationKey) with
        {
            Purpose = StaffMailPurpose.CaseReport,
            ReportDispatch = new([], [], null, null, ["Send the WhatsApp."])
        };

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
            AllowIncoming = true,
            AllowOutgoing = true,
            MailboxGeneration = 1,
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
            FolderScope = "inbox",
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
        bool allowOutgoing,
        string? sentFolderIdentity) => new()
    {
        Id = id,
        Address = $"{identity}@collisionengineers.co.uk",
        AllowOutgoing = allowOutgoing,
        State = ApprovedMailboxState.Approved.ToString(),
        MailboxIdentity = $"{identity}-mailbox",
        SentFolderIdentity = sentFolderIdentity,
        ActivatedAtUtc = DateTimeOffset.UtcNow,
        MailboxGeneration = 1,
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
