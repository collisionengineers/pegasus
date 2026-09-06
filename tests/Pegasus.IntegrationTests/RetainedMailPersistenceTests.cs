using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
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
    public async Task LocalEmailDisplayUsesStructuredReplyToInsteadOfOtherAddressHeaders()
    {
        var display = await ReadDisplayAsync(message =>
        {
            message.From.Add(new MailboxAddress("From", "from@example.invalid"));
            message.Sender = new MailboxAddress("Transport", "transport@example.invalid");
            message.ReplyTo.Add(new MailboxAddress("Replies", "replies@example.invalid"));
            message.To.Add(new MailboxAddress("To", "to@example.invalid"));
            message.Cc.Add(new MailboxAddress("Copy", "copy@example.invalid"));
        });

        Assert.Equal(["replies@example.invalid"], display.ReplyToAddresses);
    }

    [Fact]
    public async Task LocalEmailDisplayUsesActualFromMailboxesOnlyWhenReplyToIsAbsent()
    {
        var display = await ReadDisplayAsync(message =>
        {
            message.From.Add(new MailboxAddress("First", "first@example.invalid"));
            message.From.Add(new MailboxAddress("Second", "second@example.invalid"));
            message.Sender = new MailboxAddress("Transport", "transport@example.invalid");
            message.To.Add(new MailboxAddress("To", "to@example.invalid"));
        });

        Assert.Equal(
            ["first@example.invalid", "second@example.invalid"],
            display.ReplyToAddresses);
    }

    [Fact]
    public async Task LocalEmailDisplayPreservesMultipleReplyToMailboxOrder()
    {
        var display = await ReadDisplayAsync(message =>
        {
            message.From.Add(new MailboxAddress("From", "from@example.invalid"));
            message.ReplyTo.Add(new MailboxAddress("First reply", "first-reply@example.invalid"));
            message.ReplyTo.Add(new MailboxAddress("Second reply", "second-reply@example.invalid"));
        });

        Assert.Equal(
            ["first-reply@example.invalid", "second-reply@example.invalid"],
            display.ReplyToAddresses);
    }

    [Fact]
    public async Task NamelessAttachmentsKeepTheirOccurrenceSoLaterAttachmentIdentityDoesNotShift()
    {
        var message = new MimeMessage();
        message.From.Add(MimeKit.MailboxAddress.Parse("sender@example.invalid"));
        message.To.Add(MimeKit.MailboxAddress.Parse(MailboxAddress));
        message.Subject = "Nameless attachment occurrence";
        message.MessageId = "<nameless@example.invalid>";

        var nameless = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream([1, 2, 3])),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64
        };
        var named = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream([4, 5, 6])),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = "named.bin"
        };
        var attachedText = new TextPart("plain")
        {
            Text = "Attached notes",
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            FileName = "notes.txt"
        };
        var contentIdImage = new MimePart("image", "png")
        {
            Content = new MimeContent(new MemoryStream(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nXQAAAAASUVORK5CYII="))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            ContentId = "attached-image@example.invalid",
            FileName = "attached.png"
        };
        var searchablePdf = new MimePart("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(AudatexEstimateFixture.Build())),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = "searchable.pdf"
        };
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "Body" },
            nameless,
            attachedText,
            contentIdImage,
            searchablePdf,
            named
        };

        await using var stream = new MemoryStream();
        await message.WriteToAsync(stream);
        var bytes = stream.ToArray();
        await using var displayStream = new MemoryStream(bytes);
        var display = await LocalEmailDisplayReader.ReadAsync(displayStream, CancellationToken.None);
        var canonical = await new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System)
            .ReadAsync(
                new(
                    "message.eml",
                    "message/rfc822",
                    bytes,
                    ReceivedAtUtc,
                    "test",
                    new(IntakeSourceChannel.Mailbox, "mailbox:test")),
                CancellationToken.None);

        Assert.Collection(
            Assert.IsAssignableFrom<IReadOnlyList<RetainedMailboxAttachment>>(display.Attachments),
            first => Assert.Equal("Unnamed attachment 1", first.FileName),
            second => Assert.Equal("notes.txt", second.FileName),
            third => Assert.Equal("attached.png", third.FileName),
            fourth => Assert.Equal("searchable.pdf", fourth.FileName),
            fifth => Assert.Equal("named.bin", fifth.FileName));
        Assert.Collection(
            canonical.AttachmentRecords,
            first => Assert.Equal(0, first.Ordinal),
            second =>
            {
                Assert.Equal(1, second.Ordinal);
                Assert.Equal("notes.txt", second.FileName);
            },
            third =>
            {
                Assert.Equal(2, third.Ordinal);
                Assert.Equal("attached.png", third.FileName);
            },
            fourth =>
            {
                Assert.Equal(3, fourth.Ordinal);
                Assert.Equal("searchable.pdf", fourth.FileName);
            },
            fifth =>
            {
                Assert.Equal(4, fifth.Ordinal);
                Assert.Equal("named.bin", fifth.FileName);
            });
        Assert.Contains(
            IntakeSearchProjection.Create(canonical, routeDecision: null),
            document => document.AttachmentOrdinal == 3 && document.IsSearchable);
    }

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
        Assert.Equal("message-1", detail.ImmutableMessageId);
        Assert.Equal("<message-1@example.invalid>", detail.InternetMessageId);
        Assert.Equal("conversation-1", detail.ConversationId);
        Assert.Equal(["intake@collisionengineers.co.uk"], detail.ToAddresses);
        Assert.Equal(["copied@collisionengineers.co.uk"], detail.CcAddresses);
        Assert.Equal("Please inspect the vehicle.", detail.BodyPlainText);
        var attachment = Assert.Single(detail.Attachments);
        Assert.Equal("estimate.pdf", attachment.FileName);
        Assert.Equal(2048, attachment.ContentLength);
    }

    [Fact]
    public async Task OperationalAndDetailedViewsUseTheCurrentClassificationProjection()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var fixtures = new[]
        {
            ("receiving", MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"), [], "fixture", "test", 1)),
            ("queries", MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.PostReportEmails, "query"), [], "fixture", "test", 1)),
            ("other", MailClassificationResult.Classified(
                MailCategory.Other(MailDirection.Received, "supplier-newsletter", "No known class fits."), [], "fixture", "test", 1)),
            ("unidentified", MailClassificationResult.Unclassified([], "fixture", "test", 1)),
            ("triage", MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.PreInstructionEmails, "triage-request"), [], "fixture", "test", 1)),
            ("detailed", MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.General, "autoreply"), [], "fixture", "test", 1))
        };
        foreach (var (key, classification) in fixtures)
        {
            var message = Message(key, subject: key);
            await RetainAsync(database, message);
            await StoreClassifiedReceiptAsync(database, message, classification);
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        foreach (var (key, destination) in new[]
        {
            ("receiving", MailOperationalDestination.ReceivingWork),
            ("queries", MailOperationalDestination.Queries),
            ("other", MailOperationalDestination.Other),
            ("unidentified", MailOperationalDestination.Unidentified),
            ("triage", MailOperationalDestination.Triage)
        })
        {
            var item = Assert.Single((await queries.ListAsync(
                new(null, MailFolderScope.Inbox, Destination: destination),
                1,
                25,
                CancellationToken.None)).Items);
            Assert.Equal(key, item.Subject);
            Assert.Equal(destination, item.OperationalDestination?.Destination);
            Assert.NotNull(item.Classification);
        }

        var detailed = Assert.Single((await queries.ListAsync(
            new(
                null,
                MailFolderScope.Inbox,
                DetailedClassification: MailCategory.Received(
                    ReceivedMailFamily.General,
                    "autoreply")),
            1,
            25,
            CancellationToken.None)).Items);
        Assert.Equal("detailed", detailed.Subject);
        Assert.Equal(
            MailOperationalDestination.DetailedClassification,
            detailed.OperationalDestination?.Destination);
    }

    [Fact]
    public async Task CaseQueryStoreProjectsCurrentlyLinkedQueryMailNewestFirst()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var (caseId, otherCaseId) = await SeedQueryCasesAsync(database);
        var query = Message("case-query", "Query", ReceivedAtUtc.AddMinutes(1));
        var dispute = Message("case-dispute", "Dispute", ReceivedAtUtc);
        var otherCase = Message("other-case-query", "Other case", ReceivedAtUtc.AddMinutes(8));
        var nonQuery = Message("case-update", "Case update", ReceivedAtUtc.AddMinutes(7));
        var reversed = Message("reversed-query", "Reversed", ReceivedAtUtc.AddMinutes(6));
        var billing = Message("billing-query", "Billing query", ReceivedAtUtc.AddMinutes(5));
        var unassociated = Message("unassociated-query", "Unassociated", ReceivedAtUtc.AddMinutes(4));
        var sharedToken = Message("shared-token-query", "Shared first", ReceivedAtUtc.AddMinutes(3));
        var fixtures = new[]
        {
            (query, ReceivedMailFamily.PostReportEmails, "query"),
            (dispute, ReceivedMailFamily.PostReportEmails, "dispute"),
            (otherCase, ReceivedMailFamily.PostReportEmails, "query"),
            (nonQuery, ReceivedMailFamily.InProgressCases, "case-update"),
            (reversed, ReceivedMailFamily.PostReportEmails, "query"),
            (billing, ReceivedMailFamily.Billing, "billing-query"),
            (unassociated, ReceivedMailFamily.PostReportEmails, "query"),
            (sharedToken, ReceivedMailFamily.PostReportEmails, "query")
        };
        foreach (var (message, family, subtype) in fixtures)
        {
            await RetainAsync(database, message);
            await StoreClassifiedReceiptAsync(
                database,
                message,
                MailClassificationResult.Classified(
                    MailCategory.Received(family, subtype),
                    [],
                    "Fixture classification.",
                    "query-test",
                    1));
        }

        Guid queryId;
        Guid disputeId;
        Guid billingId;
        Guid sharedFirstId;
        Guid sharedSecondId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            var receiptByToken = await context.IntakeReceipts
                .ToDictionaryAsync(item => item.ExternalReceiptToken, StringComparer.Ordinal);
            AddAssociation(context, receiptByToken[query.ExternalReceiptToken], caseId, true);
            AddAssociation(context, receiptByToken[dispute.ExternalReceiptToken], caseId, true);
            AddAssociation(context, receiptByToken[otherCase.ExternalReceiptToken], otherCaseId, true);
            AddAssociation(context, receiptByToken[nonQuery.ExternalReceiptToken], caseId, true);
            AddAssociation(context, receiptByToken[reversed.ExternalReceiptToken], caseId, false);
            AddAssociation(context, receiptByToken[billing.ExternalReceiptToken], caseId, true);
            AddAssociation(context, receiptByToken[sharedToken.ExternalReceiptToken], caseId, true);

            var retained = await context.RetainedMailboxMessages.ToDictionaryAsync(
                item => item.ImmutableMessageId,
                StringComparer.Ordinal);
            queryId = retained[query.ImmutableMessageId].Id;
            disputeId = retained[dispute.ImmutableMessageId].Id;
            billingId = retained[billing.ImmutableMessageId].Id;
            var sharedFirst = retained[sharedToken.ImmutableMessageId];
            sharedFirstId = sharedFirst.Id;
            context.RetainedMailboxMessages.Add(new()
            {
                Id = sharedSecondId,
                MailboxId = sharedFirst.MailboxId,
                MailboxAddress = sharedFirst.MailboxAddress,
                FolderScope = sharedFirst.FolderScope,
                FolderIdentity = sharedFirst.FolderIdentity,
                ImmutableMessageId = "shared-token-query-second",
                ConversationIdentity = "conversation-2",
                InternetMessageIdentity = "<shared-token-query-second@example.invalid>",
                CanonicalInternetMessageIdentity = "shared-token-query-second@example.invalid",
                ExternalReceiptToken = sharedFirst.ExternalReceiptToken,
                SenderAddress = "second@example.invalid",
                SenderDisplayName = "Second Sender",
                ToAddressesJson = "[]",
                CcAddressesJson = "[]",
                Subject = "Shared second",
                BodyExcerpt = "Second retained row.",
                BodyPlainText = "Second retained row.",
                IsRead = false,
                SourceLength = 2,
                SourceSha256 = new string('D', 64),
                ReceivedAtUtc = ReceivedAtUtc.AddMinutes(2),
                RetainedAtUtc = ReceivedAtUtc.AddMinutes(2)
            });
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var details = Assert.IsType<CaseDetails>(await scope.ServiceProvider
            .GetRequiredService<ICaseQueryStore>()
            .GetAsync(new(caseId, ActionActor.SystemWorker("query-test")), CancellationToken.None));

        Assert.Equal(
            [billingId, sharedFirstId, sharedSecondId, queryId, disputeId],
            details.QueryEmails.Select(item => item.RetainedMessageId));
        Assert.Equal(
            ["Billing query", "Shared first", "Shared second", "Query", "Dispute"],
            details.QueryEmails.Select(item => item.Subject));
        Assert.Equal(ReceivedAtUtc.AddMinutes(5), details.QueryEmails[0].ReceivedAtUtc);
        Assert.Equal("sender@example.invalid", details.QueryEmails[0].SenderAddress);
        Assert.Equal(ReceivedMailFamily.Billing, details.QueryEmails[0].Classification.ReceivedFamily);
        Assert.Equal("billing-query", details.QueryEmails[0].Classification.Subtype);
        Assert.Equal("second@example.invalid", details.QueryEmails[2].SenderAddress);
        Assert.DoesNotContain(details.QueryEmails, item => item.Subject is
            "Other case" or "Case update" or "Reversed" or "Unassociated");
    }

    [Fact]
    public async Task OperationalViewFiltersBeforeSqlCountAndPagingAndUsesCorrections()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var instruction = MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
            [],
            "fixture",
            "test",
            1);
        for (var index = 0; index < 6; index++)
        {
            var message = Message(
                $"instruction-{index}",
                subject: $"instruction-{index}",
                receivedAtUtc: ReceivedAtUtc.AddMinutes(index));
            await RetainAsync(database, message);
            await StoreClassifiedReceiptAsync(database, message, instruction);
        }
        var correctedMessage = Message(
            "corrected",
            subject: "corrected",
            receivedAtUtc: ReceivedAtUtc.AddMinutes(10));
        await RetainAsync(database, correctedMessage);
        var before = MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.General, "autoreply"),
            [],
            "fixture",
            "test",
            1);
        await StoreClassifiedReceiptAsync(database, correctedMessage, before);
        var unrelated = Message("unrelated", subject: "unrelated");
        await RetainAsync(database, unrelated);
        await StoreClassifiedReceiptAsync(database, unrelated, before);

        await using (var correctionScope = database.CreateAsyncScope())
        {
            var queries = correctionScope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
            var messageId = (await queries.ListAsync(
                new(null, MailFolderScope.Inbox),
                1,
                25,
                CancellationToken.None)).Items.Single(item => item.Subject == "corrected").Id;
            await correctionScope.ServiceProvider
                .GetRequiredService<IRetainedMailClassificationStore>()
                .AppendCorrectionAsync(
                    messageId,
                    1,
                    before,
                    instruction,
                    "staff:fixture",
                    "Corrected fixture.",
                    ReceivedAtUtc.AddMinutes(20),
                    CancellationToken.None);
        }

        await using var scope = database.CreateAsyncScope();
        var retained = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        var first = await retained.ListAsync(
            new(null, MailFolderScope.Inbox, Destination: MailOperationalDestination.ReceivingWork),
            1,
            3,
            CancellationToken.None);
        var third = await retained.ListAsync(
            new(null, MailFolderScope.Inbox, Destination: MailOperationalDestination.ReceivingWork),
            3,
            3,
            CancellationToken.None);

        Assert.Equal(7, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(3, first.Items.Count);
        Assert.Contains(first.Items, item => item.Subject == "corrected");
        Assert.Single(third.Items);
        Assert.DoesNotContain(first.Items.Concat(third.Items), item => item.Subject == "unrelated");
    }

    [Fact]
    public async Task SearchFiltersBeforePagingAndIdentifiesBodyFileNameAndProjectedContentMatches()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var message = Message(
            "message-search",
            bodyPlainText: "Canonical wrapper only. Please inspect the vehicle.");
        await RetainAsync(database, message);
        await database.StoreAsync(new(
            SourceFileName: "message-search.eml",
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: new string('D', 64),
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
            SearchDocuments:
            [
                new("message body", null, "Please inspect the vehicle."),
                new("message, attachment 1", "estimate.pdf", "Repair estimate for replacement wing", 0)
            ]));

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();

        var body = Assert.Single((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "inspect"), 1, 25, CancellationToken.None)).Items);
        Assert.Contains(body.Matches, match => match.Kind == MailSearchMatchKind.MessageBody);

        var fileName = Assert.Single((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "estimate"), 1, 25, CancellationToken.None)).Items);
        Assert.Contains(fileName.Matches, match =>
            match.Kind == MailSearchMatchKind.AttachmentFileName
            && match.AttachmentFileName == "estimate.pdf");

        var content = Assert.Single((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "replacement"), 1, 25, CancellationToken.None)).Items);
        Assert.Contains(content.Matches, match =>
            match.Kind == MailSearchMatchKind.AttachmentContent
            && match.AttachmentFileName == "estimate.pdf");
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "not present"), 1, 25, CancellationToken.None)).Items);
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "Canonical wrapper only"), 1, 25, CancellationToken.None)).Items);

        var detail = Assert.IsType<RetainedMailDetail>(
            await queries.GetAsync(content.Id, CancellationToken.None, "inspect"));
        Assert.Equal("Please inspect the vehicle.", detail.BodyPlainText);
        Assert.Contains(detail.Summary.Matches, match => match.Kind == MailSearchMatchKind.MessageBody);
        Assert.True(Assert.Single(detail.Attachments).IsSearchable);
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeSearchDocuments"));
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
            PolledMessage("provider-one", " <case@K.example> ", "cursor-1"),
            PolledMessage("provider-two", "<CASE@K.EXAMPLE>", "cursor-2"));
        await using var database = await PollDatabaseAsync(source);

        await using var scope = database.CreateAsyncScope();
        var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
        var actor = ActionActor.SystemWorker("approved-inbox-poller");
        Assert.Equal(1, await poll.ExecuteAsync(1, actor, CancellationToken.None));
        Assert.Equal(1, await poll.ExecuteAsync(1, actor, CancellationToken.None));

        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeWorkItems"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
        await using var context = await database.CreateContextAsync();
        var retained = await context.RetainedMailboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(" <case@K.example> ", retained.InternetMessageIdentity);
        Assert.Equal("<CASE@K.EXAMPLE>", retained.CanonicalInternetMessageIdentity);
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
            new(TestMailboxId.From(MailboxId), MailFolderScope.Inbox),
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

    /// <summary>
    /// MAIL-005: the automatic allocation route records its created case on the
    /// succeeded attempt and writes no CaseIntakeLinks row — the summary must
    /// still resolve the case, so the Inbox never shows an allocated message
    /// as waiting for allocation.
    /// </summary>
    [Fact]
    public async Task ASucceededAllocationAttemptResolvesTheCaseWithoutALinkRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var message = Message("message-1");
        await RetainAsync(database, message);

        var stored = await database.StoreAsync(new(
            SourceFileName: "message-1.eml",
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: new string('A', 64),
            SourceIdentity: new(IntakeSourceChannel.Mailbox, message.ExternalReceiptToken),
            ReceivedAtUtc: ReceivedAtUtc,
            ProcessedAtUtc: ReceivedAtUtc,
            Actor: "system-worker:approved-inbox-poller",
            Decision: IntakeDecision.CaseCreated,
            DecisionReason: "Fixture allocation-eligible instruction.",
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

        var caseId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeAllocationAttempts (Id, IntakeReceiptId, AttemptNumber, Kind, Status, ExpectedReceiptVersion, CaseType, PrincipalCode, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, ActorKind, ActorSubjectId, ActorRolesJson, OperationKey, CommandHash, Reason, StartedAtUtc, CompletedAtUtc, CaseId, CaseReference) VALUES ({Guid.NewGuid()}, {stored.Id}, {1L}, {"automatic"}, {"succeeded"}, {0L}, {"inspection"}, {"QDOS"}, {true}, {false}, {false}, {false}, {"Automation"}, {"intake-processing"}, {"[]"}, {"mail-005-fixture"}, {new string('B', 64)}, {"Automatic allocation fixture."}, {ReceivedAtUtc}, {ReceivedAtUtc}, {caseId}, {"QDOS26099"})");
        }

        await using var scope = database.CreateAsyncScope();
        var page = await scope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None);

        var summary = Assert.Single(page.Items);
        Assert.Equal(caseId, summary.CaseId);
        Assert.Equal("QDOS26099", summary.CaseReference);
        Assert.Equal("Case created", Pegasus.Web.Pages.Mail.MessageModel.OutcomeLabel(summary));
    }

    [Fact]
    public async Task CorrectionIsAtomicAppendOnlyAndProtectedFromAutomatedReevaluation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var message = Message("message-correction");
        await RetainAsync(database, message);
        var original = MailClassificationResult.Unclassified(
            [new("accepted-route", false, "No accepted route matched.")],
            "No supported category matched.",
            "shared-mail-policy",
            4);
        await StoreClassifiedReceiptAsync(database, message, original);

        Guid retainedId;
        await using (var readScope = database.CreateAsyncScope())
        {
            retainedId = Assert.Single((await readScope.ServiceProvider
                .GetRequiredService<IRetainedMailQueries>()
                .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items).Id;
            var service = readScope.ServiceProvider.GetRequiredService<CorrectRetainedMailClassification>();
            var corrected = await service.ExecuteAsync(
                ActionActor.Staff(Guid.Parse("11111111-1111-1111-1111-111111111111"), [StaffRole.User]),
                new(retainedId, 1, MailCategory.Received(ReceivedMailFamily.General, "acknowledgement"),
                    "The retained reply is an acknowledgement."));
            Assert.Equal(2, corrected!.Version);
            Assert.Equal(original.Predicates, corrected.Current.Predicates);
            Assert.Single(corrected.History);

            await Assert.ThrowsAsync<MailClassificationConcurrencyException>(() => service.ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                new(retainedId, 1, MailCategory.Received(ReceivedMailFamily.InternalCc), "Stale correction.")));
        }

        // A later processor replay must not erase an accepted human correction.
        await StoreClassifiedReceiptAsync(
            database,
            message,
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
                [],
                "Automated replay.",
                "shared-mail-policy",
                5));

        await using var finalScope = database.CreateAsyncScope();
        var detail = Assert.IsType<RetainedMailDetail>(await finalScope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .GetAsync(retainedId, CancellationToken.None));
        Assert.Equal("acknowledgement", detail.Classification!.Current.Category!.Subtype);
        Assert.Equal(4, detail.Classification.Current.PolicyVersion);
        Assert.Single(detail.Classification.History);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeMailClassificationHistory"));
    }

    [Fact]
    public async Task OneCorrectionPolicyAppliesIdenticallyAndIndependentlyAcrossMailboxes()
    {
        // engineers@collisionengineers.co.uk is one of the four documented
        // mailboxes (docs/operator-notes.md); the second mailbox identity
        // must never be a fabricated address.
        const string secondMailboxId = "engineers";
        const string secondMailboxAddress = "engineers@collisionengineers.co.uk";
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        await SeedPollStateAsync(database, secondMailboxId, secondMailboxAddress);
        var messages = new[]
        {
            Message("message-shared-policy-a"),
            Message(
                "message-shared-policy-b",
                mailboxId: secondMailboxId,
                mailboxAddress: secondMailboxAddress)
        };

        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // The registered policy decides the outcome, not a literal: a Triage
        // phrase in the body and an Audit notification title in an
        // attachment are two of the policy's real predicates, and they
        // legitimately match at once, so the registered QDOS policy itself
        // produces the Ambiguous outcome exercised below. If DI ever bound a
        // different policy, or the policy's predicate logic regressed, this
        // call -- not a fabricated result -- would change and the
        // assertions below would fail.
        var policy = services.GetRequiredService<IMailClassificationPolicy>();
        var original = policy.Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, "message body", "Triage Only Request. See attached."),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "message, attachment 1, instructions.pdf",
                    "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1")
            ],
            [],
            [],
            false));
        Assert.Equal(MailClassificationOutcome.Ambiguous, original.Outcome);
        Assert.Equal(QdosMailClassificationPolicy.Key, original.PolicyKey);
        Assert.Equal(QdosMailClassificationPolicy.Version, original.PolicyVersion);

        foreach (var message in messages)
        {
            await RetainAsync(database, message);
            await StoreClassifiedReceiptAsync(database, message, original);
        }

        var queries = services.GetRequiredService<IRetainedMailQueries>();
        var command = services.GetRequiredService<CorrectRetainedMailClassification>();
        var actor = ActionActor.Staff(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [StaffRole.User]);
        var retained = (await queries.ListAsync(
                new(null, MailFolderScope.Inbox),
                1,
                25,
                CancellationToken.None))
            .Items
            .OrderBy(item => item.MailboxId)
            .ToArray();

        Assert.Equal(
            new[] { TestMailboxId.From(secondMailboxId), TestMailboxId.From(MailboxId) }.Order(),
            retained.Select(item => item.MailboxId));
        foreach (var item in retained)
        {
            var corrected = await command.ExecuteAsync(
                actor,
                new(
                    item.Id,
                    1,
                    MailCategory.Received(ReceivedMailFamily.General, "acknowledgement"),
                    "The exact retained message is an acknowledgement."));

            Assert.Equal(2, corrected!.Version);
            Assert.Equal(policy.PolicyKey, corrected.Current.PolicyKey);
            Assert.Equal(policy.PolicyVersion, corrected.Current.PolicyVersion);
            Assert.Equal(original.Predicates, corrected.Current.Predicates);
            Assert.Single(corrected.History);
        }

        await Assert.ThrowsAsync<MailClassificationConcurrencyException>(() =>
            command.ExecuteAsync(
                actor,
                new(
                    retained[0].Id,
                    1,
                    MailCategory.Received(ReceivedMailFamily.InternalCc),
                    "A stale correction must not affect either mailbox.")));
        Assert.Null(await command.ExecuteAsync(
            actor,
            new(
                Guid.NewGuid(),
                1,
                MailCategory.Received(ReceivedMailFamily.InternalCc),
                "An unknown retained message must not create a decision.")));
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeMailClassificationHistory"));
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
    public async Task ConfirmedFolderMoveIsDurableReplayableAndPreservesArrivalEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await SeedPollStateAsync(database);
        var message = Message("message-move");
        await RetainAsync(database, message);
        await StoreClassifiedReceiptAsync(
            database,
            message,
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
                [],
                "Instruction identified.",
                "shared-mail-policy",
                4));

        int mailboxVersion;
        await using (var context = await database.CreateContextAsync())
        {
            var mailbox = await context.ApprovedMailboxes
                .Include(item => item.FolderBindings)
                .SingleAsync(item => item.Address == MailboxAddress);
            mailbox.MailboxIdentity = MailboxId;
            mailbox.InboxFolderIdentity = "inbox";
            mailbox.State = ApprovedMailboxState.Approved.ToString();
            mailbox.Version++;
            mailbox.FolderBindings.Add(new()
            {
                ApprovedMailboxId = mailbox.Id,
                ApprovedMailbox = mailbox,
                FolderType = MailLogicalFolderType.Instructions.ToString(),
                FolderIdentity = "folder-instructions"
            });
            mailboxVersion = mailbox.Version;
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        var retained = Assert.Single((await queries.ListAsync(
            new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
        var mover = new RecordingFolderMover("folder-instructions");
        var store = new EfRetainedMailFolderMoveStore(
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            mover,
            TimeProvider.System);
        var command = new MoveRetainedMailFolder(store);
        var request = new MoveRetainedMailFolderRequest(
            retained.Id,
            1,
            MailLogicalFolderPolicy.Key,
            MailLogicalFolderPolicy.Version,
            mailboxVersion,
            Guid.NewGuid().ToString("D"),
            "Confirmed by staff.");
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var moved = await command.ExecuteAsync(actor, request);
        var replay = await command.ExecuteAsync(actor, request);
        await Assert.ThrowsAsync<RetainedMailFolderMoveException>(() =>
            command.ExecuteAsync(actor, request with { Reason = "Different inputs." }));

        Assert.Equal(RetainedMailFolderMoveOutcome.Succeeded, moved!.Outcome);
        Assert.True(replay!.IsReplay);
        Assert.Equal(request.OperationKey, replay.OperationKey);
        Assert.Equal(1, mover.MoveCalls);
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
        var searchResult = Assert.Single((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "estimate"), 1, 25, CancellationToken.None)).Items);
        Assert.Equal(MailLogicalFolderType.Instructions, searchResult.CurrentFolderType);
        Assert.Empty((await queries.ListAsync(
            new(TestMailboxId.From("different-mailbox"), MailFolderScope.Inbox, "estimate"), 1, 25, CancellationToken.None)).Items);
        await using var verification = await database.CreateContextAsync();
        Assert.Equal("inbox", await verification.RetainedMailboxMessages
            .Where(item => item.Id == retained.Id)
            .Select(item => item.FolderIdentity)
            .SingleAsync());
        Assert.Equal(1, await verification.RetainedMailFolderMoves.CountAsync());
        Assert.Equal(1, await verification.ActionHistory.CountAsync(item =>
            item.AggregateId == retained.Id.ToString("D")
            && item.EventKind == "outlook-folder-move"));
    }

    [Fact]
    public async Task ConcurrentDifferentKeysHaveOneActiveClaimAndOneProviderMove()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                """
                SELECT COUNT(*) FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'RetainedMailFolderMoves')
                  AND name = N'IX_RetainedMailFolderMoves_RetainedMailboxMessageId'
                  AND is_unique = 1
                  AND has_filter = 1
                """));
        var seed = await SeedFolderMoveAsync(database);
        var mover = new BlockingFolderMover();
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var first = FolderMoveCommand(firstScope, mover).ExecuteAsync(seed.Actor, seed.Request);
        await mover.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            await Assert.ThrowsAsync<RetainedMailFolderMoveException>(() =>
                FolderMoveCommand(secondScope, mover).ExecuteAsync(
                    seed.Actor,
                    seed.Request with { OperationKey = Guid.NewGuid().ToString("D") }));
        }
        finally
        {
            mover.Release.TrySetResult();
        }

        Assert.Equal(RetainedMailFolderMoveOutcome.Succeeded, (await first)!.Outcome);
        Assert.Equal(1, mover.MoveCalls);
        await using var verification = await database.CreateContextAsync();
        Assert.Single(await verification.RetainedMailFolderMoves.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentSameKeyReplayCannotResolveOrReleaseThePendingMove()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var seed = await SeedFolderMoveAsync(database);
        var mover = new BlockingFolderMover();
        await using var firstScope = database.CreateAsyncScope();
        await using var replayScope = database.CreateAsyncScope();
        await using var newKeyScope = database.CreateAsyncScope();
        var first = FolderMoveCommand(firstScope, mover).ExecuteAsync(seed.Actor, seed.Request);
        await mover.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            var replayError = await Assert.ThrowsAsync<RetainedMailFolderMoveException>(() =>
                FolderMoveCommand(replayScope, mover).ExecuteAsync(seed.Actor, seed.Request));
            Assert.Equal("The folder move is still being processed.", replayError.Message);
            await Assert.ThrowsAsync<RetainedMailFolderMoveException>(() =>
                FolderMoveCommand(newKeyScope, mover).ExecuteAsync(
                    seed.Actor,
                    seed.Request with { OperationKey = Guid.NewGuid().ToString("D") }));

            await using var inFlight = await database.CreateContextAsync();
            var operation = Assert.Single(await inFlight.RetainedMailFolderMoves.ToListAsync());
            Assert.Equal("pending", operation.Outcome);
            Assert.Equal(1, mover.ParentCalls);
            Assert.Equal(1, mover.MoveCalls);
        }
        finally
        {
            mover.Release.TrySetResult();
        }

        Assert.Equal(RetainedMailFolderMoveOutcome.Succeeded, (await first)!.Outcome);
        await using var completedReplayScope = database.CreateAsyncScope();
        var completedReplay = await FolderMoveCommand(completedReplayScope, mover)
            .ExecuteAsync(seed.Actor, seed.Request);
        Assert.True(completedReplay!.IsReplay);
        Assert.Equal(RetainedMailFolderMoveOutcome.Succeeded, completedReplay.Outcome);
        Assert.Equal(1, mover.ParentCalls);
        Assert.Equal(1, mover.MoveCalls);
    }

    [Fact]
    public async Task CancellationDuringProviderMoveLeavesAnUncertainSameKeyRecovery()
    {
        using var cancellation = new CancellationTokenSource();
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var seed = await SeedFolderMoveAsync(database);
        var mover = new CancellationRecoveryFolderMover(cancellation.Cancel);
        await using var scope = database.CreateAsyncScope();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FolderMoveCommand(scope, mover).ExecuteAsync(seed.Actor, seed.Request, cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        await AssertUncertainBlocksNewKeyAndRecoversAsync(database, seed, mover);
    }

    [Fact]
    public async Task CancellationDuringSuccessSaveLeavesAnUncertainSameKeyRecovery()
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelFolderMoveSuccessSaveInterceptor();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(interceptor));
        var seed = await SeedFolderMoveAsync(database);
        var mover = new CancellationRecoveryFolderMover(static () => { });
        interceptor.CancelNextSuccessSave(cancellation);
        await using var scope = database.CreateAsyncScope();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FolderMoveCommand(scope, mover).ExecuteAsync(seed.Actor, seed.Request, cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        await AssertUncertainBlocksNewKeyAndRecoversAsync(database, seed, mover);
    }

    [Fact]
    public async Task FreshnessAndCurrentLocationRefusalsNeverIssueTheMove()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var seed = await SeedFolderMoveAsync(database);
        var mover = new StatefulFolderMover("unexpected-folder");
        await using var scope = database.CreateAsyncScope();
        var command = FolderMoveCommand(scope, mover);

        foreach (var stale in new[]
        {
            seed.Request with { ExpectedClassificationVersion = seed.Request.ExpectedClassificationVersion + 1 },
            seed.Request with { ExpectedRecommendationPolicyKey = "stale-policy" },
            seed.Request with { ExpectedMailboxVersion = seed.Request.ExpectedMailboxVersion + 1 }
        })
        {
            await Assert.ThrowsAsync<RetainedMailFolderMoveException>(() =>
                command.ExecuteAsync(seed.Actor, stale with { OperationKey = Guid.NewGuid().ToString("D") }));
        }

        Assert.Equal(0, mover.MoveCalls);
        Assert.Equal(0, mover.ParentCalls);
        var locationFailure = await command.ExecuteAsync(seed.Actor, seed.Request);
        Assert.Equal(RetainedMailFolderMoveOutcome.Failed, locationFailure!.Outcome);
        Assert.Equal(0, mover.MoveCalls);
        Assert.Equal(1, mover.ParentCalls);
    }

    [Fact]
    public async Task ProviderFailurePreservesClassificationAndAllowsANewKeyRetry()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var seed = await SeedFolderMoveAsync(database);
        var mover = new FailOnceFolderMover();
        await using var scope = database.CreateAsyncScope();
        var command = FolderMoveCommand(scope, mover);

        var failed = await command.ExecuteAsync(seed.Actor, seed.Request);
        var retried = await command.ExecuteAsync(
            seed.Actor,
            seed.Request with { OperationKey = Guid.NewGuid().ToString("D") });

        Assert.Equal(RetainedMailFolderMoveOutcome.Failed, failed!.Outcome);
        Assert.Equal("Provider move failed.", failed.FailureReason);
        Assert.Equal(RetainedMailFolderMoveOutcome.Succeeded, retried!.Outcome);
        Assert.Equal(2, mover.MoveCalls);
        await using var verification = await database.CreateContextAsync();
        var decision = await verification.IntakeReceipts
            .Where(item => item.ExternalReceiptToken == seed.ExternalReceiptToken)
            .Select(item => item.MailClassificationDecision!)
            .SingleAsync();
        Assert.Equal(1, decision.Version);
        Assert.Equal("new-instruction-received", decision.Family);
        Assert.Empty(verification.IntakeMailClassificationHistory);
        Assert.Equal(2, await verification.ActionHistory.CountAsync(item =>
            item.AggregateId == seed.MessageId.ToString("D")
            && item.EventKind == "outlook-folder-move"));
    }

    [Fact]
    public async Task ReclassificationUsesLatestSuccessfulDestinationAsTheNextSource()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var seed = await SeedFolderMoveAsync(
            database,
            (MailLogicalFolderType.Instructions, "folder-instructions"),
            (MailLogicalFolderType.Billing, "folder-billing"));
        var mover = new StatefulFolderMover("inbox");
        await using var scope = database.CreateAsyncScope();
        var command = FolderMoveCommand(scope, mover);

        Assert.Equal(
            RetainedMailFolderMoveOutcome.Succeeded,
            (await command.ExecuteAsync(seed.Actor, seed.Request))!.Outcome);
        var classificationStore = scope.ServiceProvider.GetRequiredService<IRetainedMailClassificationStore>();
        await new CorrectRetainedMailClassification(classificationStore, TimeProvider.System).ExecuteAsync(
            seed.Actor,
            new(seed.MessageId, 1, MailCategory.Received(ReceivedMailFamily.Billing), "Billing evidence reviewed."));
        Assert.Equal(
            RetainedMailFolderMoveOutcome.Succeeded,
            (await command.ExecuteAsync(
                seed.Actor,
                seed.Request with
                {
                    ExpectedClassificationVersion = 2,
                    OperationKey = Guid.NewGuid().ToString("D"),
                    Reason = "Confirmed after reclassification."
                }))!.Outcome);

        Assert.Collection(
            mover.Coordinates,
            first =>
            {
                Assert.Equal("inbox", first.SourceFolderId);
                Assert.Equal("folder-instructions", first.DestinationFolderId);
            },
            second =>
            {
                Assert.Equal("folder-instructions", second.SourceFolderId);
                Assert.Equal("folder-billing", second.DestinationFolderId);
            });
        await using var verification = await database.CreateContextAsync();
        Assert.Equal("inbox", await verification.RetainedMailboxMessages
            .Where(item => item.Id == seed.MessageId)
            .Select(item => item.FolderIdentity)
            .SingleAsync());
        await using var queryScope = database.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
        var found = Assert.Single((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "estimate"), 1, 1, CancellationToken.None)).Items);
        Assert.Equal(MailLogicalFolderType.Billing, found.CurrentFolderType);
        Assert.Equal(1, (await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "estimate"), 1, 1, CancellationToken.None)).TotalCount);
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox, "estimate"), 2, 1, CancellationToken.None)).Items);
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
            await ActivateSeededMailboxAsync(database);

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

    private static async Task<LocalEmailDisplay> ReadDisplayAsync(Action<MimeMessage> configure)
    {
        var message = new MimeMessage
        {
            Subject = "Reply target fixture",
            Body = new TextPart("plain") { Text = "Body" }
        };
        configure(message);
        await using var stream = new MemoryStream();
        await message.WriteToAsync(stream);
        stream.Position = 0;
        return await LocalEmailDisplayReader.ReadAsync(stream, CancellationToken.None);
    }

    private static RetainedMailboxMessage Message(
        string immutableMessageId,
        string? subject = "An instruction",
        DateTimeOffset? receivedAtUtc = null,
        string? senderAddress = "sender@example.invalid",
        string? senderDisplayName = "A Sender",
        string? internetMessageIdentity = null,
        string mailboxId = MailboxId,
        string mailboxAddress = MailboxAddress,
        string? bodyPlainText = "Please inspect the vehicle.") => new(
        TestMailboxId.From(mailboxId),
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
            bodyPlainText,
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

    private static Task<IntakeReceipt> StoreClassifiedReceiptAsync(
        LocalDbTestDatabase database,
        RetainedMailboxMessage message,
        MailClassificationResult classification) => database.StoreAsync(new(
            SourceFileName: "message-correction.eml",
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: new string('C', 64),
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
            MailClassificationDecision: classification));

    private static async Task<(Guid CaseId, Guid OtherCaseId)> SeedQueryCasesAsync(
        LocalDbTestDatabase database)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var originId = Guid.NewGuid();
        var otherOriginId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        await using var context = await database.CreateContextAsync();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "Query test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = ReceivedAtUtc },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "QDOS",
                IsActive = true,
                Version = 0
            },
            Receipt(originId, "origin:query-test"),
            Receipt(otherOriginId, "origin:query-test-other"),
            Case(caseId, principalId, lineageId, originId, 1),
            Case(otherCaseId, principalId, lineageId, otherOriginId, 2),
            Workflow(caseId),
            Workflow(otherCaseId));
        await context.SaveChangesAsync();
        return (caseId, otherCaseId);
    }

    private static IntakeReceiptEntity Receipt(Guid id, string token) => new()
    {
        Id = id,
        SourceFileName = "origin.pdf",
        MediaType = "application/pdf",
        SourceLength = 1,
        SourceHash = new string('0', 64),
        SourceChannel = "manual_upload",
        ExternalReceiptToken = token,
        ReceivedAtUtc = ReceivedAtUtc,
        ProcessedAtUtc = ReceivedAtUtc,
        SourceReaderKey = "query-test",
        SourceReaderVersion = "1",
        Version = 0,
        Decision = "case_created",
        DecisionReason = "Query test.",
        EvidenceJson = "[]",
        FieldsJson = "[]",
        OcrCandidatesJson = "[]"
    };

    private static CaseEntity Case(
        Guid id,
        Guid principalId,
        Guid lineageId,
        Guid originId,
        int sequence) => new()
    {
        Id = id,
        PrincipalId = principalId,
        SequenceLineageId = lineageId,
        Year = 2031,
        Sequence = sequence,
        Reference = $"QDOS3100{sequence}",
        Type = "Inspection",
        InitialState = "Review",
        CustodyState = "pending",
        OriginIntakeReceiptId = originId,
        CreatedAtUtc = ReceivedAtUtc,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkflowEntity Workflow(Guid caseId) => new()
    {
        CaseId = caseId,
        State = "Review",
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static void AddAssociation(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        Guid caseId,
        bool active)
    {
        receipt.ManualAssociation = new()
        {
            IntakeReceiptId = receipt.Id,
            CaseId = caseId,
            IsActive = active,
            Version = 0,
            LinkedAtUtc = ReceivedAtUtc,
            UnlinkedAtUtc = active ? null : ReceivedAtUtc.AddMinutes(1),
            ActorKind = "Staff",
            ActorSubjectId = Guid.NewGuid().ToString("D"),
            ActorRolesJson = "[]",
            Reason = "Query test.",
            LastOperationKey = $"query-association:{receipt.Id:N}"
        };
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
        await ActivateSeededMailboxAsync(database);
        return database;
    }

    private static Task ActivateSeededMailboxAsync(LocalDbTestDatabase database) =>
        database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes
            SET MailboxIdentity = '{MailboxId}', InboxFolderIdentity = 'inbox',
                ActivatedAtUtc = '1970-01-01T00:00:00+00:00'
            WHERE Id = '{TestMailboxId.From(MailboxId):D}';
            """);

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
        var approvedMailboxId = TestMailboxId.From(mailboxId);
        var approvedMailbox = await context.ApprovedMailboxes.FindAsync(approvedMailboxId);
        if (approvedMailbox is null)
        {
            context.ApprovedMailboxes.Add(new()
            {
                Id = approvedMailboxId,
                Address = mailboxAddress,
                AllowInboundIntake = true,
                State = ApprovedMailboxState.Approved.ToString(),
                MailboxIdentity = mailboxId,
                InboxFolderIdentity = "inbox",
                ActivatedAtUtc = ReceivedAtUtc.AddDays(-1),
                Version = 1
            });
        }
        else
        {
            approvedMailbox.MailboxIdentity = mailboxId;
            approvedMailbox.InboxFolderIdentity = "inbox";
            approvedMailbox.ActivatedAtUtc = ReceivedAtUtc.AddDays(-1);
        }
        context.ApprovedInboxPollStates.Add(new()
        {
            ApprovedMailboxId = approvedMailboxId,
            MailboxAddress = mailboxAddress,
            ScopeFingerprint = new string('A', 64),
            ActivatedAtUtc = ReceivedAtUtc.AddDays(-1),
            DueAtUtc = ReceivedAtUtc,
            LastCompletedAtUtc = ReceivedAtUtc
        });
        await context.SaveChangesAsync();
    }

    private static async Task<FolderMoveSeed> SeedFolderMoveAsync(
        LocalDbTestDatabase database,
        params (MailLogicalFolderType FolderType, string FolderIdentity)[] bindings)
    {
        if (bindings.Length == 0)
        {
            bindings = [(MailLogicalFolderType.Instructions, "folder-instructions")];
        }
        await SeedPollStateAsync(database);
        var message = Message("message-move-review");
        await RetainAsync(database, message);
        await StoreClassifiedReceiptAsync(
            database,
            message,
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
                [],
                "Instruction identified.",
                "shared-mail-policy",
                4));
        int mailboxVersion;
        await using (var context = await database.CreateContextAsync())
        {
            var mailbox = await context.ApprovedMailboxes
                .Include(item => item.FolderBindings)
                .SingleAsync(item => item.Address == MailboxAddress);
            mailbox.MailboxIdentity = MailboxId;
            mailbox.InboxFolderIdentity = "inbox";
            mailbox.State = ApprovedMailboxState.Approved.ToString();
            mailbox.Version++;
            foreach (var binding in bindings)
            {
                mailbox.FolderBindings.Add(new()
                {
                    ApprovedMailboxId = mailbox.Id,
                    ApprovedMailbox = mailbox,
                    FolderType = binding.FolderType.ToString(),
                    FolderIdentity = binding.FolderIdentity
                });
            }
            mailboxVersion = mailbox.Version;
            await context.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var retained = Assert.Single((await scope.ServiceProvider
            .GetRequiredService<IRetainedMailQueries>()
            .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
        return new(
            retained.Id,
            message.ExternalReceiptToken,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            new(
                retained.Id,
                1,
                MailLogicalFolderPolicy.Key,
                MailLogicalFolderPolicy.Version,
                mailboxVersion,
                Guid.NewGuid().ToString("D"),
                "Confirmed by staff."));
    }

    private static MoveRetainedMailFolder FolderMoveCommand(
        AsyncServiceScope scope,
        IRetainedMailFolderMover mover) => new(new EfRetainedMailFolderMoveStore(
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            mover,
            TimeProvider.System));

    private static async Task AssertUncertainBlocksNewKeyAndRecoversAsync(
        LocalDbTestDatabase database,
        FolderMoveSeed seed,
        CancellationRecoveryFolderMover mover)
    {
        await using (var verification = await database.CreateContextAsync())
        {
            var operation = Assert.Single(await verification.RetainedMailFolderMoves.ToListAsync());
            Assert.Equal("uncertain", operation.Outcome);
        }

        await using var newKeyScope = database.CreateAsyncScope();
        await Assert.ThrowsAsync<RetainedMailFolderMoveException>(() =>
            FolderMoveCommand(newKeyScope, mover).ExecuteAsync(
                seed.Actor,
                seed.Request with { OperationKey = Guid.NewGuid().ToString("D") }));

        await using var replayScope = database.CreateAsyncScope();
        var replay = await FolderMoveCommand(replayScope, mover).ExecuteAsync(seed.Actor, seed.Request);
        Assert.True(replay!.IsReplay);
        Assert.Equal(RetainedMailFolderMoveOutcome.Succeeded, replay.Outcome);
        Assert.Equal(1, mover.MoveCalls);
        Assert.Equal(2, mover.ParentCalls);
    }

    private sealed record FolderMoveSeed(
        Guid MessageId,
        string ExternalReceiptToken,
        ActionActor Actor,
        MoveRetainedMailFolderRequest Request);

    private sealed class BlockingFolderMover : IRetainedMailFolderMover
    {
        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }
        public int ParentCalls { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string currentFolder = "inbox";

        public async Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            currentFolder = coordinates.DestinationFolderId;
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken)
        {
            ParentCalls++;
            return Task.FromResult<string?>(currentFolder);
        }
    }

    private sealed class StatefulFolderMover(string currentFolder) : IRetainedMailFolderMover
    {
        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }
        public int ParentCalls { get; private set; }
        public List<RetainedMailFolderMoveCoordinates> Coordinates { get; } = [];

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            Coordinates.Add(coordinates);
            currentFolder = coordinates.DestinationFolderId;
            return Task.CompletedTask;
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken)
        {
            ParentCalls++;
            return Task.FromResult<string?>(currentFolder);
        }
    }

    private sealed class FailOnceFolderMover : IRetainedMailFolderMover
    {
        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }
        private string currentFolder = "inbox";

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            if (MoveCalls == 1)
            {
                throw new InvalidOperationException("Provider move failed.");
            }
            currentFolder = coordinates.DestinationFolderId;
            return Task.CompletedTask;
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(currentFolder);
    }

    private sealed class CancellationRecoveryFolderMover(Action afterMove) : IRetainedMailFolderMover
    {
        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }
        public int ParentCalls { get; private set; }
        private string currentFolder = "inbox";

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            currentFolder = coordinates.DestinationFolderId;
            afterMove();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken)
        {
            ParentCalls++;
            return Task.FromResult<string?>(currentFolder);
        }
    }

    private sealed class CancelFolderMoveSuccessSaveInterceptor : SaveChangesInterceptor
    {
        private CancellationTokenSource? cancellation;

        public void CancelNextSuccessSave(CancellationTokenSource source) =>
            cancellation = source;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null
                && eventData.Context.ChangeTracker.Entries<RetainedMailFolderMoveEntity>()
                    .Any(entry => entry.State == EntityState.Modified
                        && entry.Entity.Outcome == "succeeded"))
            {
                var source = Interlocked.Exchange(ref cancellation, null);
                if (source is not null)
                {
                    source.Cancel();
                    throw new OperationCanceledException(source.Token);
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
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

    private sealed class RecordingFolderMover(string parentFolderId) : IRetainedMailFolderMover
    {
        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }
        private bool moved;

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            Assert.Equal("inbox", coordinates.SourceFolderId);
            Assert.Equal("folder-instructions", coordinates.DestinationFolderId);
            moved = true;
            return Task.CompletedTask;
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(moved ? parentFolderId : "inbox");
    }
}
