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
        DateTimeOffset? receivedAtUtc = null) => new(
        MailboxId,
        MailboxAddress,
        immutableMessageId,
        $"{MailboxId.Length}:{MailboxId}{immutableMessageId}",
        receivedAtUtc ?? ReceivedAtUtc,
        1024,
        new string('A', 64),
        new(
            "inbox",
            "conversation-1",
            $"<{immutableMessageId}@example.invalid>",
            "sender@example.invalid",
            "A Sender",
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

    /// <summary>
    /// The per-mailbox cursor row a retained message hangs off. The poll makes it
    /// on the way past; a test that writes retained rows directly has to make it
    /// itself, which is the foreign key doing its job.
    /// </summary>
    private static async Task SeedPollStateAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        context.ApprovedInboxPollStates.Add(new()
        {
            MailboxId = MailboxId,
            MailboxAddress = MailboxAddress,
            DueAtUtc = ReceivedAtUtc,
            LastCompletedAtUtc = ReceivedAtUtc
        });
        await context.SaveChangesAsync();
    }
}
