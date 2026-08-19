using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The retained-mail read model against the real migration: what the poll writes,
/// what the workspace reads back, and the paging the list depends on.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RetainedMailPersistenceTests
{
    private const string MailboxId = "instructions";
    private const string MailboxAddress = "instructions@collisionengineers.co.uk";

    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2031, 7, 8, 9, 10, 0, TimeSpan.Zero);

    [Fact]
    public async Task ARetainedMessageRoundTripsThroughTheMigration()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);

        await RetainAsync(database, Message("message-1"));

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        var page = await queries.ListAsync(
            new(null, MailFolderScope.Inbox),
            1,
            25,
            CancellationToken.None);

        var summary = Assert.Single(page.Items);
        Assert.Equal(MailboxAddress, summary.MailboxAddress);
        Assert.Equal("A Sender", summary.SenderDisplayName);
        Assert.Equal("sender@example.invalid", summary.SenderAddress);
        Assert.Equal("An instruction", summary.Subject);
        Assert.Equal("Please inspect the vehicle.", summary.BodyExcerpt);
        Assert.False(summary.IsRead);
        Assert.Equal(1, summary.AttachmentCount);
        Assert.Null(summary.ProcessingOutcome);
        Assert.False(page.HasUnretainedHistory);

        var detail = Assert.IsType<RetainedMailDetail>(
            await queries.GetAsync(summary.Id, CancellationToken.None));
        Assert.Equal(["intake@collisionengineers.co.uk"], detail.ToAddresses);
        Assert.Equal(["copied@collisionengineers.co.uk"], detail.CcAddresses);
        Assert.Equal("Please inspect the vehicle.", detail.BodyPlainText);
        var attachment = Assert.Single(detail.Attachments);
        Assert.Equal("estimate.pdf", attachment.FileName);
        Assert.Equal(2048, attachment.ContentLength);
    }

    [Fact]
    public async Task RedeliveryIsRefusedByTheUniqueIndexAndRetainIsANoOperation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);

        await RetainAsync(database, Message("message-1"));
        // The same immutable identity with different display material: the row
        // that is already there wins, because a retained row records what
        // arrived and is never updated.
        await RetainAsync(database, Message("message-1", subject: "A different subject"));

        Assert.Equal(
            1L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
        await using var scope = database.CreateAsyncScope();
        var page = await scope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);
        Assert.Equal("An instruction", Assert.Single(page.Items).Subject);
    }

    [Fact]
    public async Task ChangedProviderItemIdentityDoesNotDuplicateTheSameRfcMessage()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);

        await RetainAsync(database, Message("provider-item-before-move"));
        await RetainAsync(database, Message("provider-item-after-move",
            internetMessageIdentity: "<provider-item-before-move@example.invalid>"));

        Assert.Equal(
            1L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
    }

    [Fact]
    public async Task EquivalentRfcRepresentationsReplayOnePollReceiptAndRetainedRow()
    {
        var source = new SequenceInboxSource(
            PolledMessage("provider-one", " <Case-Message@Example.Invalid> ", "cursor-1"),
            PolledMessage("provider-two", "<case-message@example.invalid>", "cursor-2"));
        await using var database = await PollDatabaseAsync(source);

        await using var scope = database.CreateAsyncScope();
        var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
        var actor = ActionActor.SystemWorker("approved-inbox-poller");
        Assert.Equal(1, await poll.ExecuteAsync(1, actor, CancellationToken.None));
        Assert.Equal(1, await poll.ExecuteAsync(1, actor, CancellationToken.None));

        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeWorkItems"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
    }

    [Fact]
    public async Task DistinctCanonicalRfcIdentitiesRemainDistinctThroughTheRealPoll()
    {
        var source = new SequenceInboxSource(
            PolledMessage("provider-one", "<message-one@example.invalid>", "cursor-1"),
            PolledMessage("provider-two", "<message-two@example.invalid>", "cursor-2"));
        await using var database = await PollDatabaseAsync(source);

        await using var scope = database.CreateAsyncScope();
        var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
        var actor = ActionActor.SystemWorker("approved-inbox-poller");
        Assert.Equal(1, await poll.ExecuteAsync(1, actor, CancellationToken.None));
        Assert.Equal(1, await poll.ExecuteAsync(1, actor, CancellationToken.None));

        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeWorkItems"));
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
    }

    [Fact]
    public async Task ContradictoryIdentityForAnImmutableItemFailsClosed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);

        await RetainAsync(database, Message("provider-item"));

        await Assert.ThrowsAsync<InvalidDataException>(() => RetainAsync(
            database,
            Message("provider-item", internetMessageIdentity: "<different@example.invalid>")));
    }

    [Fact]
    public async Task AThreadNeverCrossesMailboxScope()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        await SeedPollStateAsync(database, "desk", "desk@collisionengineers.co.uk");
        await RetainAsync(database, Message("instructions-item", subject: "instructions message"));
        await RetainAsync(database, Message(
            "desk-item",
            subject: "desk message",
            mailboxId: "desk",
            mailboxAddress: "desk@collisionengineers.co.uk"));

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        var page = await queries.ListAsync(
            new(MailboxId, MailFolderScope.Inbox),
            1,
            25,
            CancellationToken.None);
        var detail = Assert.IsType<RetainedMailDetail>(
            await queries.GetAsync(Assert.Single(page.Items).Id, CancellationToken.None));

        Assert.Single(detail.Thread);
        Assert.Equal("instructions message", detail.Thread[0].Subject);
    }

    [Fact]
    public async Task PagingIsStableAndComplete()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        for (var index = 0; index < 6; index++)
        {
            await RetainAsync(
                database,
                Message($"message-{index}", receivedAtUtc: ReceivedAtUtc.AddMinutes(index)));
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var slice = await queries.ListAsync(
                new(null, MailFolderScope.Inbox),
                page,
                2,
                CancellationToken.None);
            Assert.Equal(6, slice.TotalCount);
            Assert.Equal(3, slice.TotalPages);
            Assert.Equal(2, slice.Items.Count);
            seen.AddRange(slice.Items.Select(item => item.Id));
        }

        // No overlap and no gap: every retained message is reachable exactly once.
        Assert.Equal(6, seen.Distinct().Count());
        var newest = await queries.ListAsync(
            new(null, MailFolderScope.Inbox),
            1,
            2,
            CancellationToken.None);
        Assert.Equal(ReceivedAtUtc.AddMinutes(5), newest.Items[0].ReceivedAtUtc);
    }

    [Fact]
    public async Task TheReceiptAndCaseJoinLightsUpWithoutTouchingTheRetainedRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var message = Message("message-1");
        await RetainAsync(database, message);

        await database.StoreAsync(new(
            SourceFileName: "message-1.eml",
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: new string('A', 64),
            SourceIdentity: new(IntakeSourceChannel.Mailbox, message.ExternalReceiptToken),
            ReceivedAtUtc: ReceivedAtUtc,
            ProcessedAtUtc: ReceivedAtUtc,
            Actor: "system-worker:approved-inbox-poller",
            Decision: IntakeDecision.NeedsSorting,
            DecisionReason: "Fixture evaluation.",
            Evidence: [],
            Fields: [],
            InstructionDraft: null,
            MissingFields: [],
            FailureCode: null,
            FailureReason: null,
            SourceReaderKey: "protocol_reader",
            SourceReaderVersion: "1",
            ExtractionPolicyKey: "protocol_policy",
            ExtractionPolicyVersion: 1,
            Assets: []));

        await using var scope = database.CreateAsyncScope();
        var page = await scope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);

        var summary = Assert.Single(page.Items);
        Assert.Equal(IntakeDecision.NeedsSorting, summary.ProcessingOutcome);
        Assert.NotNull(summary.IntakeReceiptId);
        Assert.Null(summary.CaseId);
    }

    [Fact]
    public async Task AForwardedMessageUsesTheProvenOriginalSenderAndRetainsTheForwarder()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var message = Message(
            "message-forwarded",
            senderAddress: "desk@collisionengineers.co.uk",
            senderDisplayName: "Desk");
        await RetainAsync(database, message);

        await database.StoreAsync(new(
            SourceFileName: "message-forwarded.eml",
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: new string('B', 64),
            SourceIdentity: new(IntakeSourceChannel.Mailbox, message.ExternalReceiptToken),
            ReceivedAtUtc: ReceivedAtUtc,
            ProcessedAtUtc: ReceivedAtUtc,
            Actor: "system-worker:approved-inbox-poller",
            Decision: IntakeDecision.NeedsSorting,
            DecisionReason: "Fixture evaluation.",
            Evidence: [],
            Fields: [],
            InstructionDraft: null,
            MissingFields: [],
            FailureCode: null,
            FailureReason: null,
            SourceReaderKey: "protocol_reader",
            SourceReaderVersion: "1",
            ExtractionPolicyKey: "protocol_policy",
            ExtractionPolicyVersion: 1,
            Assets: [],
            MailRouteDecision: new(
                MailRouteDisposition.Accepted,
                new("QDOS", MailRouteKind.DirectProvider, "QDOS"),
                [],
                "Fixture accepted route.",
                "qdos_mail_route",
                1,
                [new("desk@collisionengineers.co.uk", "transport")],
                [new("original@qdosassist.co.uk", "inline forward")],
                new("original@qdosassist.co.uk", "inline forward"))));

        await using var scope = database.CreateAsyncScope();
        var page = await scope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);
        var summary = Assert.Single(page.Items);

        Assert.Equal("original@qdosassist.co.uk", summary.EffectiveSenderAddress);
        Assert.Equal("desk@collisionengineers.co.uk", summary.SenderAddress);
        Assert.Equal("Desk", summary.SenderDisplayName);
    }

    [Fact]
    public async Task ADisabledMailboxKeepsItsMessagesAndReportsThatItIsNoLongerPolled()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        await RetainAsync(database, Message("message-1"));

        await using (var context = await database.CreateContextAsync())
        {
            // The seed already approves this address, so this is the
            // administrator disabling a mailbox that has already collected mail.
            var mailbox = await context.ApprovedMailboxes
                .SingleAsync(item => item.Address == MailboxAddress);
            mailbox.State = ApprovedMailboxState.Disabled.ToString();
            mailbox.Version++;
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        var page = await queries.ListAsync(
            new(null, MailFolderScope.Inbox),
            1,
            25,
            CancellationToken.None);

        Assert.False(Assert.Single(page.Items).MailboxIsPolled);
        Assert.False(Assert.Single(await queries.ListMailboxesAsync(CancellationToken.None)).IsPolled);
    }

    [Fact]
    public async Task SentAndDeletedScopesHoldNothingAndDoNotClaimUnretainedHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        await RetainAsync(database, Message("message-1"));

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();

        foreach (var folder in new[] { MailFolderScope.Sent, MailFolderScope.DeletedItems })
        {
            var page = await queries.ListAsync(new(null, folder), 1, 25, CancellationToken.None);
            Assert.Empty(page.Items);
            Assert.False(page.HasUnretainedHistory);
        }
    }

    [Fact]
    public async Task AMailboxThatPolledBeforeRetentionReportsUnretainedHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);

        await using var scope = database.CreateAsyncScope();
        var page = await scope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.True(page.HasUnretainedHistory);
    }

    [Fact]
    public async Task ARealLocalPollRetainsOneRowFromAnEmlAndASecondRunRetainsNone()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.RetainedMailPersistenceTests",
            Guid.NewGuid().ToString("N"));
        var inboxRoot = Path.Combine(workingRoot, "approved-inbox");
        var inboxFolder = Path.Combine(inboxRoot, "inbox");
        var artifactRoot = Path.Combine(workingRoot, "artifacts");
        Directory.CreateDirectory(inboxFolder);
        await File.WriteAllBytesAsync(
            Path.Combine(inboxFolder, "0001-instruction.eml"),
            CreateMessageBytes());

        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => artifactRoot,
                configureServices: services =>
                {
                    services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
                    services.AddScoped<ReceiveIntake>();
                    services.AddLocalApprovedInbox(_ => new(
                        LocalApprovedInboxOptions.RequiredRuntimeProfile,
                        MailboxId,
                        MailboxAddress,
                        inboxRoot));
                });

            await using (var scope = database.CreateAsyncScope())
            {
                var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                var actor = ActionActor.SystemWorker("approved-inbox-poller");
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
                Assert.Equal(0, await poll.ExecuteAsync(10, actor, CancellationToken.None));
            }

            Assert.Equal(
                1L,
                await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
            await using var readScope = database.CreateAsyncScope();
            var page = await readScope.ServiceProvider
                .GetRequiredService<IRetainedMailQueries>()
                .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);
            var summary = Assert.Single(page.Items);
            Assert.Equal("original@example.invalid", summary.SenderAddress);
            Assert.Equal("A genuine instruction", summary.Subject);
            Assert.Contains("inspect", summary.BodyExcerpt!, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    private static byte[] CreateMessageBytes()
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Original Sender", "original@example.invalid"));
        message.To.Add(new MailboxAddress("Pegasus", MailboxAddress));
        message.Subject = "A genuine instruction";
        message.Body = new TextPart("plain")
        {
            Text = "Please inspect the vehicle at the address below."
        };
        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static RetainedMailboxMessage Message(
        string immutableMessageId,
        string? subject = "An instruction",
        DateTimeOffset? receivedAtUtc = null,
        string? senderAddress = "sender@example.invalid",
        string? senderDisplayName = "A Sender",
        string? internetMessageIdentity = null,
        string mailboxId = MailboxId,
        string mailboxAddress = MailboxAddress) => new(
        mailboxId,
        mailboxAddress,
        immutableMessageId,
        $"{mailboxId.Length}:{mailboxId}{immutableMessageId}",
        receivedAtUtc ?? ReceivedAtUtc,
        1024,
        new string('A', 64),
        new(
            "inbox",
            "conversation-1",
            internetMessageIdentity ?? $"<{immutableMessageId}@example.invalid>",
            senderAddress,
            senderDisplayName,
            ["intake@collisionengineers.co.uk"],
            ["copied@collisionengineers.co.uk"],
            subject,
            "Please inspect the vehicle.",
            [new("estimate.pdf", "application/pdf", 2048)],
            IsRead: false),
        receivedAtUtc ?? ReceivedAtUtc);

    private static async Task RetainAsync(
        LocalDbTestDatabase database,
        RetainedMailboxMessage message)
    {
        await using var scope = database.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<EfRetainedMailboxMessageStore>()
            .RetainAsync(message, CancellationToken.None);
    }

    private static ApprovedInboxMessage PolledMessage(
        string providerIdentity,
        string internetMessageIdentity,
        string cursor) => new(
            providerIdentity,
            $"{providerIdentity}.eml",
            "From: sender@example.invalid\r\nMessage-ID: <stable@example.invalid>\r\n\r\nBody"u8.ToArray(),
            ReceivedAtUtc,
            cursor)
        {
            RetainedMetadata = new(
                "inbox",
                "conversation-1",
                internetMessageIdentity,
                "sender@example.invalid",
                "Sender",
                [MailboxAddress],
                [],
                "Subject",
                "Body",
                [],
                IsRead: false)
        };

    private static async Task<LocalDbTestDatabase> PollDatabaseAsync(IApprovedInboxSource source)
    {
        var artifactRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.RetainedMailIdentityTests",
            Guid.NewGuid().ToString("N"));
        var database = await LocalDbTestDatabase.CreateAsync(
            localArtifactRootFactory: _ => artifactRoot,
            configureServices: services =>
        {
            services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
            services.AddScoped<ReceiveIntake>();
            services.AddLocalApprovedInbox(_ => new(
                LocalApprovedInboxOptions.RequiredRuntimeProfile,
                MailboxId,
                MailboxAddress,
                Path.GetTempPath()));
            services.AddSingleton(source);
        });
        return database;
    }

    /// <summary>
    /// The per-mailbox cursor row a retained message hangs off. The poll makes it
    /// on the way past; a test that writes retained rows directly has to make it
    /// itself, which is the foreign key doing its job.
    /// </summary>
    private static async Task SeedPollStateAsync(
        LocalDbTestDatabase database,
        string mailboxId = MailboxId,
        string mailboxAddress = MailboxAddress)
    {
        await using var context = await database.CreateContextAsync();
        context.ApprovedInboxPollStates.Add(new()
        {
            MailboxId = mailboxId,
            MailboxAddress = mailboxAddress,
            DueAtUtc = ReceivedAtUtc,
            LastCompletedAtUtc = ReceivedAtUtc
        });
        await context.SaveChangesAsync();
    }

    private sealed class SequenceInboxSource(params ApprovedInboxMessage[] messages)
        : IApprovedInboxSource
    {
        private readonly Queue<ApprovedInboxMessage> remaining = new(messages);

        public Task<ApprovedInboxPage> ReadAsync(
            ApprovedInboxPollLease lease,
            int maximumMessages,
            CancellationToken cancellationToken)
        {
            if (remaining.Count == 0)
            {
                return Task.FromResult(new ApprovedInboxPage([], lease.Cursor ?? "complete"));
            }

            var message = remaining.Dequeue();
            return Task.FromResult(new ApprovedInboxPage([message], message.NextCursor));
        }
    }
}
