using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using MimeKit;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class MailboxIntakeIntegrationTests
{
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 7, 8, 9, 10, 0, TimeSpan.Zero);

    private const string DefaultInboxFolderIdentity = "inbox";

    /// <summary>
    /// The mailbox envelope bound these tests run against.
    /// </summary>
    /// <remarks>
    /// The shipped bound is deliberately permissive, and writing a file of
    /// that size to prove the boundary would cost more than the boundary is
    /// worth. The adapter and the poll take their bound as a parameter for
    /// exactly this reason, so both sides of it stay covered here while
    /// production keeps <see cref="IntakeEnvelopeLimits.MaximumMailboxContentLength"/>.
    /// </remarks>
    private const long TestMailboxContentLength = 256 * 1024;

    [Fact]
    public async Task ReevaluationPreservesThePriorDecisionRecordsInPermanentHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var sourceHash = new string('E', 64);
        var sourceAsset = new IntakeAssetRecord(
            Guid.NewGuid(),
            "uploaded source",
            "reevaluated.eml",
            "message/rfc822",
            IntakeAssetKind.Source,
            IntakeAssetDisposition.Source,
            1,
            sourceHash,
            "reevaluation-storage-key",
            null,
            null,
            null,
            null);
        var originalClassification = MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "audit"),
            [new("attachment.audit-report-notification", true, "The generated title was present.")],
            "Exactly one accepted classification predicate family matched.",
            "qdos_mail_classification",
            1);
        var originalMatch = new CaseMatchEvaluationResult(
            CaseMatchOutcome.NoMatch,
            null,
            null,
            new("12345/1", null, null, null, null),
            [],
            "No case of the provider matches any extracted key.",
            "qdos_case_match",
            1);

        IntakeReceiptDraft Draft(
            IntakeDecision decision,
            MailClassificationResult? classification,
            CaseMatchEvaluationResult? match) => new(
            SourceFileName: "reevaluated.eml",
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: sourceHash,
            SourceIdentity: new(IntakeSourceChannel.Mailbox, "reevaluation-history-token"),
            ReceivedAtUtc: RecordedAtUtc,
            ProcessedAtUtc: RecordedAtUtc,
            Actor: "system-worker:approved-inbox-poller",
            Decision: decision,
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
            Assets: [sourceAsset],
            MailClassificationDecision: classification,
            CaseMatchDecision: match);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var stored = await store.StoreAsync(
            Draft(IntakeDecision.NeedsSorting, originalClassification, originalMatch),
            CancellationToken.None);

        var reevaluatedClassification = MailClassificationResult.Unclassified(
            [new("attachment.audit-report-notification", false, "No generated title was present.")],
            "No accepted classification predicate matched; the message fails closed for staff review.",
            "qdos_mail_classification",
            1);
        await store.ReplaceEvaluationAsync(
            Draft(IntakeDecision.NeedsSorting, reevaluatedClassification, null),
            CancellationToken.None);

        await using var context = await scope.ServiceProvider
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var reevaluationEvent = Assert.Single(
            await context.IntakeReceiptEvents
                .AsNoTracking()
                .Where(item => item.IntakeReceiptId == stored.Id
                    && item.EventType == "intake_receipt_reevaluated")
                .ToListAsync());
        Assert.Contains("priorDecisions", reevaluationEvent.DetailsJson, StringComparison.Ordinal);
        Assert.Contains("audit", reevaluationEvent.DetailsJson, StringComparison.Ordinal);
        Assert.Contains("12345/1", reevaluationEvent.DetailsJson, StringComparison.Ordinal);

        var reloaded = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(stored.Id, CancellationToken.None);
        Assert.Equal(
            MailClassificationOutcome.Unclassified,
            reloaded?.MailClassificationDecision?.Outcome);
        Assert.Null(reloaded?.CaseMatchDecision);
    }

    [Fact]
    public async Task FullVersionedMailClassificationDecisionReloadsWithoutLosingAuditEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var classified = MailClassificationResult.Classified(
            MailCategory.Received(
                ReceivedMailFamily.NewInstructionReceived,
                "audit",
                isReplyContext: true),
            [
                new("body.triage-only-request", false, "No email body contains the phrase."),
                new("attachment.audit-report-notification", true, "An attached document contains the generated title.")
            ],
            "Exactly one accepted classification predicate family matched.",
            "qdos_mail_classification",
            1);

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
            var stored = await store.StoreAsync(
                new(
                    SourceFileName: "classification-audit.eml",
                    MediaType: "message/rfc822",
                    SourceLength: 1,
                    SourceHash: new string('B', 64),
                    SourceIdentity: new(IntakeSourceChannel.Mailbox, "classification-audit-token"),
                    ReceivedAtUtc: RecordedAtUtc,
                    ProcessedAtUtc: RecordedAtUtc,
                    Actor: "system-worker:approved-inbox-poller",
                    Decision: IntakeDecision.NeedsSorting,
                    DecisionReason: "The accepted route did not contain a reviewable instruction.",
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
                    MailClassificationDecision: classified),
                CancellationToken.None);

            var reloaded = await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(stored.Id, CancellationToken.None);
            var audit = Assert.IsType<MailClassificationResult>(reloaded?.MailClassificationDecision);
            Assert.Equal(MailClassificationOutcome.Classified, audit.Outcome);
            var category = Assert.IsType<MailCategory>(audit.Category);
            Assert.Equal(ReceivedMailFamily.NewInstructionReceived, category.ReceivedFamily);
            Assert.Equal("audit", category.Subtype);
            Assert.True(category.IsReplyContext);
            Assert.Equal("qdos_mail_classification", audit.PolicyKey);
            Assert.Equal(1, audit.PolicyVersion);
            Assert.Equal(2, audit.Predicates.Count);
            Assert.Empty(audit.AmbiguousCandidates);
        }
    }

    [Fact]
    public async Task AmbiguousMailClassificationDecisionReloadsItsCandidates()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var ambiguous = MailClassificationResult.Ambiguous(
            ["pre-instruction-emails", "new-instruction-received/audit"],
            [new("body.triage-only-request", true, "An email body contains the phrase.")],
            "Predicates for more than one category matched simultaneously; no winner is invented.",
            "qdos_mail_classification",
            1);

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
            var stored = await store.StoreAsync(
                new(
                    SourceFileName: "classification-ambiguous.eml",
                    MediaType: "message/rfc822",
                    SourceLength: 1,
                    SourceHash: new string('C', 64),
                    SourceIdentity: new(IntakeSourceChannel.Mailbox, "classification-ambiguous-token"),
                    ReceivedAtUtc: RecordedAtUtc,
                    ProcessedAtUtc: RecordedAtUtc,
                    Actor: "system-worker:approved-inbox-poller",
                    Decision: IntakeDecision.NeedsSorting,
                    DecisionReason: "Competing classification candidates require manual sorting.",
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
                    MailClassificationDecision: ambiguous),
                CancellationToken.None);

            var reloaded = await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(stored.Id, CancellationToken.None);
            var audit = Assert.IsType<MailClassificationResult>(reloaded?.MailClassificationDecision);
            Assert.Equal(MailClassificationOutcome.Ambiguous, audit.Outcome);
            Assert.Null(audit.Category);
            Assert.Equal(
                ["pre-instruction-emails", "new-instruction-received/audit"],
                audit.AmbiguousCandidates);
        }
    }

    [Fact]
    public async Task OtherMailClassificationDecisionReloadsNameAndReasoning()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var classified = MailClassificationResult.Classified(
            MailCategory.Other(
                MailDirection.Received,
                "vendor-newsletter",
                "No settled Received family fits this mailing."),
            [new("staff.other", true, "Staff recorded an Other category with a name and reason.")],
            "An Other classification was recorded with a name and reason.",
            "staff_mail_classification",
            1);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var stored = await store.StoreAsync(
            ClassificationDraft(
                "classification-other-received.eml",
                new string('D', 64),
                "classification-other-received-token",
                classified),
            CancellationToken.None);

        var reloaded = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(stored.Id, CancellationToken.None);
        var decision = Assert.IsType<MailClassificationResult>(reloaded?.MailClassificationDecision);
        Assert.Equal(MailClassificationOutcome.Classified, decision.Outcome);
        var category = Assert.IsType<MailCategory>(decision.Category);
        Assert.True(category.IsOther);
        Assert.Equal(MailDirection.Received, category.Direction);
        Assert.Equal("vendor-newsletter", category.OtherName);
        Assert.Equal("No settled Received family fits this mailing.", category.OtherReasoning);
        Assert.Equal("vendor-newsletter", category.Name);
        Assert.Null(category.ReceivedFamily);
        Assert.Null(category.SentFamily);
    }

    [Fact]
    public async Task SentOtherMailClassificationDecisionReloads()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var classified = MailClassificationResult.Classified(
            MailCategory.Other(
                MailDirection.Sent,
                "fee-note-sent",
                "The settled Sent families have no fee-note category."),
            [new("staff.other", true, "Staff recorded an Other Sent category.")],
            "An Other Sent classification was recorded with a name and reason.",
            "staff_mail_classification",
            1);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var stored = await store.StoreAsync(
            ClassificationDraft(
                "classification-other-sent.eml",
                new string('F', 64),
                "classification-other-sent-token",
                classified),
            CancellationToken.None);

        var reloaded = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(stored.Id, CancellationToken.None);
        var category = Assert.IsType<MailCategory>(
            Assert.IsType<MailClassificationResult>(reloaded?.MailClassificationDecision).Category);
        Assert.True(category.IsOther);
        Assert.Equal(MailDirection.Sent, category.Direction);
        Assert.Equal("fee-note-sent", category.OtherName);
        Assert.Equal("The settled Sent families have no fee-note category.", category.OtherReasoning);
    }

    [Fact]
    public async Task SentFamilyClassificationReloadsWithAndWithoutReplyContext()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();

        var withoutReply = await store.StoreAsync(
            ClassificationDraft(
                "classification-sent.eml",
                new string('1', 64),
                "classification-sent-token",
                MailClassificationResult.Classified(
                    MailCategory.Sent(SentMailFamily.QuerySent),
                    [new("staff.sent", true, "Staff recorded a query-sent category.")],
                    "A settled Sent family was recorded.",
                    "staff_mail_classification",
                    1)),
            CancellationToken.None);
        var withReply = await store.StoreAsync(
            ClassificationDraft(
                "classification-sent-reply.eml",
                new string('2', 64),
                "classification-sent-reply-token",
                MailClassificationResult.Classified(
                    MailCategory.Sent(SentMailFamily.QuerySent, isReplyContext: true),
                    [new("staff.sent", true, "Staff recorded a query-sent reply.")],
                    "A settled Sent family was recorded with reply context.",
                    "staff_mail_classification",
                    1)),
            CancellationToken.None);

        var sent = Assert.IsType<MailCategory>(
            (await queries.GetAsync(withoutReply.Id, CancellationToken.None))
                ?.MailClassificationDecision?.Category);
        Assert.Equal(SentMailFamily.QuerySent, sent.SentFamily);
        Assert.False(sent.IsReplyContext);
        Assert.Equal("query-sent", sent.Name);
        Assert.Null(sent.ReceivedFamily);

        var reply = Assert.IsType<MailCategory>(
            (await queries.GetAsync(withReply.Id, CancellationToken.None))
                ?.MailClassificationDecision?.Category);
        Assert.Equal(SentMailFamily.QuerySent, reply.SentFamily);
        Assert.True(reply.IsReplyContext);
        Assert.Equal("query-sent", reply.Name);
    }

    [Fact]
    public async Task FullVersionedMailRouteDecisionReloadsWithoutLosingAuditEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var decision = new MailRouteEvaluationResult(
            MailRouteDisposition.Accepted,
            new("QDOS", MailRouteKind.DirectProvider, "QDOS"),
            [
                new("direct.sender-exactly-one", true, "One transport sender was proved."),
                new("forward.staff-transport", true, "The outer sender is retained staff transport."),
                new("forward.original-exactly-one", true, "One attached original sender was proved."),
                new("direct.qdos-domain", true, "The proved original uses the accepted route domain.")
            ],
            "The proved attached original selected the direct route.",
            "qdos_mail_route",
            2,
            [new("staff@collisionengineers.co.uk", "outer message")],
            [new("instructions@qdosassist.co.uk", "attached original")],
            new("instructions@qdosassist.co.uk", "attached original"));

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
            var stored = await store.StoreAsync(
                new(
                    SourceFileName: "route-audit.eml",
                    MediaType: "message/rfc822",
                    SourceLength: 1,
                    SourceHash: new string('A', 64),
                    SourceIdentity: new(IntakeSourceChannel.Mailbox, "route-audit-token"),
                    ReceivedAtUtc: RecordedAtUtc,
                    ProcessedAtUtc: RecordedAtUtc,
                    Actor: "system-worker:approved-inbox-poller",
                    Decision: IntakeDecision.NeedsSorting,
                    DecisionReason: "The accepted route did not contain a reviewable instruction.",
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
                    MailRouteDecision: decision),
                CancellationToken.None);

            var reloaded = await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(stored.Id, CancellationToken.None);
            var audit = Assert.IsType<MailRouteEvaluationResult>(reloaded?.MailRouteDecision);
            Assert.Equal(MailRouteDisposition.Accepted, audit.Disposition);
            Assert.Equal("QDOS", audit.SelectedRoute?.RouteOwnerCode);
            Assert.Equal(MailRouteKind.DirectProvider, audit.SelectedRoute?.Kind);
            Assert.Equal("QDOS", audit.SelectedRoute?.WorkProviderCode);
            Assert.Equal("qdos_mail_route", audit.PolicyKey);
            Assert.Equal(2, audit.PolicyVersion);
            Assert.Equal(4, audit.Predicates.Count);
            Assert.Equal("staff@collisionengineers.co.uk", Assert.Single(audit.TransportIdentities).Address);
            Assert.Equal("instructions@qdosassist.co.uk", Assert.Single(audit.OriginalIdentities).Address);
            Assert.Equal("instructions@qdosassist.co.uk", audit.EffectiveSender?.Address);
            Assert.Equal("attached original", audit.EffectiveSender?.SourceLabel);
        }
    }

    [Fact]
    public async Task ImmutableLocalMailboxPollIsIdempotentAndEntersNormalDurableIntake()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxIntakeIntegrationTests",
            Guid.NewGuid().ToString("N"));
        // The local root is now the root of a mailbox estate: each mailbox reads the
        // folder its lease names, directly beneath it.
        var inboxRoot = Path.Combine(workingRoot, "approved-inbox");
        var inboxFolder = Path.Combine(inboxRoot, DefaultInboxFolderIdentity);
        var artifactRoot = Path.Combine(workingRoot, "artifacts");
        Directory.CreateDirectory(inboxFolder);
        await File.WriteAllBytesAsync(
            Path.Combine(inboxFolder, "0001-forwarded.eml"),
            CreateInlineForwardedProtocolMessage());

        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => artifactRoot,
                configureServices: services =>
                {
                    services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
                    services.AddScoped<ReceiveIntake>();
                    services.AddScoped<ProcessQueuedIntake>();
                    services.AddLocalApprovedInbox(_ => new(
                        LocalApprovedInboxOptions.RequiredRuntimeProfile,
                        "instructions",
                        "instructions@collisionengineers.co.uk",
                        inboxRoot));
                });

            Guid stagedReceiptId;
            await using (var scope = database.CreateAsyncScope())
            {
                var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                var actor = ActionActor.SystemWorker("approved-inbox-poller");
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
                Assert.Equal(0, await poll.ExecuteAsync(10, actor, CancellationToken.None));

                Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
                Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeWorkItems"));
                Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM ApprovedInboxPollStates"));

                var workStore = scope.ServiceProvider.GetRequiredService<IIntakeWorkStore>();
                var nowUtc = DateTimeOffset.UtcNow.AddMinutes(1);
                var work = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
                    nowUtc,
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None));
                stagedReceiptId = work.StagedReceiptId;
                await workStore.MarkDispatchedAsync(
                    work.Id,
                    Assert.IsType<string>(work.LeaseToken),
                    nowUtc,
                    CancellationToken.None);
                await IntakeWebDriver.CreateProcessor(scope.ServiceProvider)
                    .ExecuteAsync(stagedReceiptId, CancellationToken.None);
            }

            await using (var scope = database.CreateAsyncScope())
            {
                var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
                var summary = Assert.Single((await queries.ListAsync(null, 1, 100, CancellationToken.None)).Items);
                var receipt = Assert.IsType<IntakeReceipt>(
                    await queries.GetAsync(summary.Id, CancellationToken.None));
                Assert.Equal(IntakeSourceChannel.Mailbox, receipt.SourceIdentity.Channel);
                Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
                var route = Assert.IsType<MailRouteEvaluationResult>(receipt.MailRouteDecision);
                Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
                Assert.Equal(
                    "desk@collisionengineers.co.uk",
                    Assert.Single(route.TransportIdentities).Address);
                Assert.Equal("instructions@qdosassist.co.uk", Assert.Single(route.OriginalIdentities).Address);
                Assert.Equal("instructions@qdosassist.co.uk", route.EffectiveSender?.Address);
            }

            Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeReceipts"));
            Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeEvaluations"));
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MalformedMailboxItemIsQuarantinedAndItsCursorDoesNotBlockLaterMail()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxPoisonIntegrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingRoot);
        var inboxRoot = Path.Combine(workingRoot, "approved-inbox");
        var inboxFolder = Path.Combine(inboxRoot, DefaultInboxFolderIdentity);
        Directory.CreateDirectory(inboxFolder);
        var poisonPath = Path.Combine(inboxFolder, "0001-poison.eml");
        await CreateSizedFileAsync(
            poisonPath,
            TestMailboxContentLength + 1L);
        var validContent = CreateForwardedProtocolMessage();
        await File.WriteAllBytesAsync(
            Path.Combine(inboxFolder, "0002-valid.eml"),
            validContent);

        try
        {
            var clock = new AdjustableTimeProvider(RecordedAtUtc);
            var artifactRoot = Path.Combine(workingRoot, "artifacts");
            var validHash = Convert.ToHexString(SHA256.HashData(validContent));
            using var artifactStore = new FailOnceForHashArtifactStore(artifactRoot, validHash);
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => artifactRoot,
                configureServices: services =>
                {
                    services.AddSingleton<TimeProvider>(clock);
                    services.AddSingleton<IIntakeArtifactStore>(artifactStore);
                    services.AddSingleton<IIntakeQuarantineArtifactStore>(artifactStore);
                    services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
                    services.AddScoped<ReceiveIntake>();
                    services.AddScoped<ProcessQueuedIntake>();
                    services.AddLocalApprovedInbox(_ => new(
                        LocalApprovedInboxOptions.RequiredRuntimeProfile,
                        "instructions",
                        "instructions@collisionengineers.co.uk",
                        inboxRoot,
                        maximumContentLength: TestMailboxContentLength));
                    services.AddScoped(provider => new PollApprovedInbox(
                        provider.GetRequiredService<IApprovedIntakeMailboxes>(),
                        provider.GetRequiredService<IApprovedMailboxPolicy>(),
                        provider.GetRequiredService<IApprovedInboxPollStore>(),
                        provider.GetRequiredService<IApprovedInboxSource>(),
                        provider.GetRequiredService<IIntakeArtifactStore>(),
                        provider.GetRequiredService<IIntakeQuarantineArtifactStore>(),
                        provider.GetRequiredService<ReceiveIntake>(),
                        provider.GetRequiredService<IRetainedMailboxMessageStore>(),
                        provider.GetRequiredService<TimeProvider>(),
                        TestMailboxContentLength));
                });

            string poisonStorageKey;
            await using (var firstScope = database.CreateAsyncScope())
            {
                var poll = firstScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                var actor = ActionActor.SystemWorker("approved-inbox-poller");
                await Assert.ThrowsAsync<IntakeArtifactRetentionException>(() =>
                    poll.ExecuteAsync(10, actor, CancellationToken.None));

                await using var connection = database.CreateConnection();
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT p.StorageKey, p.FailureCode, p.SourceLength, p.SourceHash,
                           p.CursorAfterMessage, s.[Cursor]
                    FROM ApprovedInboxPoisonMessages AS p
                    INNER JOIN ApprovedInboxPollStates AS s
                        ON s.MailboxId = p.MailboxId;
                    """;
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                poisonStorageKey = reader.GetString(0);
                Assert.StartsWith("sha256/", poisonStorageKey, StringComparison.Ordinal);
                Assert.Equal("message_too_large", reader.GetString(1));
                Assert.Equal(
                    TestMailboxContentLength + 1L,
                    reader.GetInt64(2));
                Assert.Equal(64, reader.GetString(3).Length);
                Assert.Equal(reader.GetString(4), reader.GetString(5));
                Assert.False(await reader.ReadAsync());
            }

            var retainedPoison = await artifactStore.ReadAsync(
                poisonStorageKey,
                CancellationToken.None);
            Assert.True(retainedPoison.HasValue);
            Assert.Equal(
                TestMailboxContentLength + 1L,
                retainedPoison.Value.Length);
            Assert.Equal(
                Path.GetFileName(poisonStorageKey),
                Convert.ToHexString(SHA256.HashData(retainedPoison.Value.Span)));
            Assert.Equal(
                TestMailboxContentLength + 1L,
                new FileInfo(poisonPath).Length);

            clock.Advance(TimeSpan.FromSeconds(31));
            await using (var restartedScope = database.CreateAsyncScope())
            {
                var poll = restartedScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                var actor = ActionActor.SystemWorker("approved-inbox-poller");
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
                Assert.Equal(0, await poll.ExecuteAsync(10, actor, CancellationToken.None));

                var workStore = restartedScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>();
                var work = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
                    clock.GetUtcNow(),
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None));
                await workStore.MarkDispatchedAsync(
                    work.Id,
                    Assert.IsType<string>(work.LeaseToken),
                    clock.GetUtcNow(),
                    CancellationToken.None);
                await IntakeWebDriver.CreateProcessor(restartedScope.ServiceProvider)
                    .ExecuteAsync(work.StagedReceiptId, CancellationToken.None);
            }

            Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeReceipts"));
            Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
            Assert.Equal(1L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM ApprovedInboxPoisonMessages"));
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalApprovedInboxMaterializesTheEnvelopeLimitAndStreamsOversizeRejection()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxEnvelopeLimitIntegrationTests",
            Guid.NewGuid().ToString("N"));
        var inboxFolder = Path.Combine(workingRoot, DefaultInboxFolderIdentity);
        Directory.CreateDirectory(inboxFolder);
        await CreateSizedFileAsync(
            Path.Combine(inboxFolder, "0001-boundary.eml"),
            TestMailboxContentLength);
        await CreateSizedFileAsync(
            Path.Combine(inboxFolder, "0002-oversize.eml"),
            TestMailboxContentLength + 1L);

        try
        {
            var artifactRoot = Path.Combine(workingRoot, "artifacts");
            string nextCursor;
            string retainedStorageKey;
            string retainedHash;
            {
                using var artifactStore = new FileSystemIntakeArtifactStore(artifactRoot);
                var services = new ServiceCollection();
                services.AddSingleton<IIntakeArtifactStore>(artifactStore);
                services.AddSingleton<IIntakeQuarantineArtifactStore>(artifactStore);
                services.AddLocalApprovedInbox(_ => new(
                    LocalApprovedInboxOptions.RequiredRuntimeProfile,
                    "instructions",
                    "instructions@collisionengineers.co.uk",
                    workingRoot,
                    maximumContentLength: TestMailboxContentLength));
                await using var provider = services.BuildServiceProvider(validateScopes: true);
                var source = provider.GetRequiredService<IApprovedInboxSource>();
                var page = await source.ReadAsync(
                    new(
                        "instructions",
                        "instructions@collisionengineers.co.uk",
                        DefaultInboxFolderIdentity,
                        null,
                        "boundary-lease"),
                    10,
                    CancellationToken.None);

                Assert.Equal(2, page.Messages.Count);
                var boundary = page.Messages[0];
                Assert.Equal(
                    TestMailboxContentLength,
                    boundary.MimeContent.Length);
                Assert.Null(boundary.SourceRejection);
                var oversize = page.Messages[1];
                Assert.True(oversize.MimeContent.IsEmpty);
                var rejection = Assert.IsType<ApprovedInboxSourceRejection>(
                    oversize.SourceRejection);
                Assert.Equal("message_too_large", rejection.FailureCode);
                Assert.Equal(
                    TestMailboxContentLength + 1L,
                    rejection.SourceLength);
                retainedHash = Assert.IsType<string>(rejection.SourceHash);
                Assert.Equal(64, retainedHash.Length);
                retainedStorageKey = Assert.IsType<string>(rejection.RetentionKey);
                Assert.Equal(
                    CreateArtifactStorageKey(retainedHash),
                    retainedStorageKey);
                nextCursor = page.NextCursor;
            }

            using var restartedArtifactStore = new FileSystemIntakeArtifactStore(artifactRoot);
            var restartedServices = new ServiceCollection();
            restartedServices.AddSingleton<IIntakeArtifactStore>(restartedArtifactStore);
            restartedServices.AddSingleton<IIntakeQuarantineArtifactStore>(
                restartedArtifactStore);
            restartedServices.AddLocalApprovedInbox(_ => new(
                LocalApprovedInboxOptions.RequiredRuntimeProfile,
                "instructions",
                "instructions@collisionengineers.co.uk",
                workingRoot,
                maximumContentLength: TestMailboxContentLength));
            await using var restartedProvider =
                restartedServices.BuildServiceProvider(validateScopes: true);
            var restartedSource =
                restartedProvider.GetRequiredService<IApprovedInboxSource>();
            var replay = await restartedSource.ReadAsync(
                new(
                    "instructions",
                    "instructions@collisionengineers.co.uk",
                    DefaultInboxFolderIdentity,
                    nextCursor,
                    "restarted-boundary-lease"),
                10,
                CancellationToken.None);
            Assert.Empty(replay.Messages);
            var retained = await restartedArtifactStore.ReadAsync(
                retainedStorageKey,
                CancellationToken.None);
            Assert.True(retained.HasValue);
            Assert.Equal(
                TestMailboxContentLength + 1L,
                retained.Value.Length);
            Assert.Equal(
                retainedHash,
                Convert.ToHexString(SHA256.HashData(retained.Value.Span)));
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task IdentityConflictIsRetainedAndQuarantinedAcrossRestartWithoutBlockingReplay()
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxIdentityConflictIntegrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingRoot);
        var artifactRoot = Path.Combine(workingRoot, "artifacts");
        var originalContent = CreateForwardedProtocolMessage();
        var conflictingContent = originalContent.ToArray();
        conflictingContent[^1] ^= 1;
        var laterContent = new byte[originalContent.Length + 2];
        originalContent.CopyTo(laterContent, 0);
        laterContent[^2] = (byte)'\r';
        laterContent[^1] = (byte)'\n';
        var originalHash = Convert.ToHexString(SHA256.HashData(originalContent));
        var conflictingHash = Convert.ToHexString(SHA256.HashData(conflictingContent));
        var laterHash = Convert.ToHexString(SHA256.HashData(laterContent));
        var inboxSource = new ConflictReplayApprovedInboxSource(
            originalContent,
            conflictingContent,
            laterContent);
        using var artifactStore = new FailOnceForHashArtifactStore(
            artifactRoot,
            conflictingHash);

        try
        {
            var clock = new AdjustableTimeProvider(RecordedAtUtc);
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => artifactRoot,
                configureServices: services =>
                {
                    services.AddSingleton<TimeProvider>(clock);
                    services.AddSingleton<IIntakeArtifactStore>(artifactStore);
                    services.AddSingleton<IIntakeQuarantineArtifactStore>(artifactStore);
                    services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
                    services.AddScoped<ReceiveIntake>();
                    services.AddLocalApprovedInbox(_ => new(
                        LocalApprovedInboxOptions.RequiredRuntimeProfile,
                        "instructions",
                        "instructions@collisionengineers.co.uk",
                        workingRoot));
                    services.AddSingleton<IApprovedInboxSource>(inboxSource);
                });
            var actor = ActionActor.SystemWorker("approved-inbox-poller");

            await using (var initialScope = database.CreateAsyncScope())
            {
                var poll = initialScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
                await Assert.ThrowsAsync<IntakeArtifactRetentionException>(() =>
                    poll.ExecuteAsync(10, actor, CancellationToken.None));
            }

            Assert.Equal(
                "cursor-1",
                await database.ScalarAsync<string>(
                    "SELECT [Cursor] FROM ApprovedInboxPollStates"));
            Assert.Equal(
                0L,
                await database.ScalarAsync<long>(
                    "SELECT COUNT(*) FROM ApprovedInboxPoisonMessages"));
            Assert.Equal(
                1L,
                await database.ScalarAsync<long>(
                    "SELECT COUNT(*) FROM IntakeStagedReceipts"));
            Assert.Null(await artifactStore.ReadAsync(
                CreateArtifactStorageKey(conflictingHash),
                CancellationToken.None));

            clock.Advance(TimeSpan.FromSeconds(31));
            await using (var restartedScope = database.CreateAsyncScope())
            {
                var poll = restartedScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                Assert.Equal(2, await poll.ExecuteAsync(10, actor, CancellationToken.None));
            }

            await using (var replayScope = database.CreateAsyncScope())
            {
                var poll = replayScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
                Assert.Equal(0, await poll.ExecuteAsync(10, actor, CancellationToken.None));
            }

            await using (var connection = database.CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT p.ImmutableMessageId, p.SourceLength, p.SourceHash,
                           p.OriginalSourceHash, p.EvidenceMarker, p.StorageKey,
                           p.FailureCode, s.[Cursor]
                    FROM ApprovedInboxPoisonMessages AS p
                    INNER JOIN ApprovedInboxPollStates AS s
                        ON s.MailboxId = p.MailboxId;
                    """;
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("shared-immutable-message", reader.GetString(0));
                Assert.Equal(conflictingContent.LongLength, reader.GetInt64(1));
                Assert.Equal(conflictingHash, reader.GetString(2));
                Assert.Equal(originalHash, reader.GetString(3));
                Assert.Equal("identity_conflict", reader.GetString(4));
                Assert.Equal(
                    CreateArtifactStorageKey(conflictingHash),
                    reader.GetString(5));
                Assert.Equal("source_identity_conflict", reader.GetString(6));
                Assert.Equal("cursor-4", reader.GetString(7));
                Assert.False(await reader.ReadAsync());
            }

            Assert.Equal(
                2L,
                await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
            Assert.Equal(
                1L,
                await database.ScalarAsync<long>("SELECT COUNT(*) FROM ApprovedInboxPoisonMessages"));
            Assert.True((await artifactStore.ReadAsync(
                CreateArtifactStorageKey(originalHash),
                CancellationToken.None)).HasValue);
            var retainedConflict = await artifactStore.ReadAsync(
                CreateArtifactStorageKey(conflictingHash),
                CancellationToken.None);
            Assert.True(retainedConflict.HasValue);
            Assert.True(retainedConflict.Value.Span.SequenceEqual(conflictingContent));
            Assert.True((await artifactStore.ReadAsync(
                CreateArtifactStorageKey(laterHash),
                CancellationToken.None)).HasValue);
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownSourceTerminalOutcomeSurvivesRestartAndDoesNotBlockLaterMail(
        bool deleteObserved)
    {
        var workingRoot = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.MailboxKnownSourceTerminalIntegrationTests",
            Guid.NewGuid().ToString("N"));
        var inboxFolder = Path.Combine(workingRoot, DefaultInboxFolderIdentity);
        Directory.CreateDirectory(inboxFolder);
        var itemPath = Path.Combine(inboxFolder, "0001-observed.eml");
        var laterPath = Path.Combine(inboxFolder, "0002-later.eml");
        var originalContent = CreateForwardedProtocolMessage();
        var changedContent = originalContent.ToArray();
        changedContent[^1] ^= 1;
        var originalHash = Convert.ToHexString(SHA256.HashData(originalContent));
        var changedHash = Convert.ToHexString(SHA256.HashData(changedContent));
        await File.WriteAllBytesAsync(itemPath, originalContent);

        try
        {
            var clock = new AdjustableTimeProvider(RecordedAtUtc);
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => Path.Combine(workingRoot, "artifacts"),
                configureServices: services =>
                {
                    services.AddSingleton<TimeProvider>(clock);
                    services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
                    services.AddScoped<ReceiveIntake>();
                    services.AddLocalApprovedInbox(_ => new(
                        LocalApprovedInboxOptions.RequiredRuntimeProfile,
                        "instructions",
                        "instructions@collisionengineers.co.uk",
                        workingRoot));
                });
            var actor = ActionActor.SystemWorker("approved-inbox-poller");
            await using (var initialScope = database.CreateAsyncScope())
            {
                var poll = initialScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                Assert.Equal(1, await poll.ExecuteAsync(10, actor, CancellationToken.None));
            }

            if (deleteObserved)
            {
                File.Delete(itemPath);
            }
            else
            {
                await File.WriteAllBytesAsync(itemPath, changedContent);
            }

            await File.WriteAllBytesAsync(laterPath, originalContent);
            await using (var restartedScope = database.CreateAsyncScope())
            {
                var poll = restartedScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
                Assert.Equal(2, await poll.ExecuteAsync(10, actor, CancellationToken.None));
            }

            await using var verificationScope = database.CreateAsyncScope();
            var restartedPoll =
                verificationScope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(
                0,
                await restartedPoll.ExecuteAsync(10, actor, CancellationToken.None));
            Assert.Equal(
                2L,
                await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
            Assert.Equal(
                1L,
                await database.ScalarAsync<long>("SELECT COUNT(*) FROM ApprovedInboxPoisonMessages"));

            string? storageKey;
            await using (var connection = database.CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT SourceLength, SourceHash, OriginalSourceHash,
                           EvidenceMarker, StorageKey, FailureCode
                    FROM ApprovedInboxPoisonMessages;
                    """;
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(originalHash, reader.GetString(2));
                if (deleteObserved)
                {
                    Assert.True(reader.IsDBNull(0));
                    Assert.True(reader.IsDBNull(1));
                    Assert.Equal("missing", reader.GetString(3));
                    Assert.True(reader.IsDBNull(4));
                    Assert.Equal("immutable_source_missing", reader.GetString(5));
                    storageKey = null;
                }
                else
                {
                    Assert.Equal(changedContent.LongLength, reader.GetInt64(0));
                    Assert.Equal(changedHash, reader.GetString(1));
                    Assert.Equal("changed", reader.GetString(3));
                    storageKey = reader.GetString(4);
                    Assert.Equal(
                        CreateArtifactStorageKey(changedHash),
                        storageKey);
                    Assert.Equal("immutable_source_changed", reader.GetString(5));
                }

                Assert.False(await reader.ReadAsync());
            }

            var artifactStore =
                verificationScope.ServiceProvider.GetRequiredService<IIntakeArtifactStore>();
            if (deleteObserved)
            {
                Assert.Null(storageKey);
            }
            else
            {
                var retainedChanged = await artifactStore.ReadAsync(
                    Assert.IsType<string>(storageKey),
                    CancellationToken.None);
                Assert.True(retainedChanged.HasValue);
                Assert.True(retainedChanged.Value.Span.SequenceEqual(changedContent));
            }
        }
        finally
        {
            if (Directory.Exists(workingRoot))
            {
                Directory.Delete(workingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LocalApprovedInboxActivationFailsClosedOutsideOfflineProfile()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new LocalApprovedInboxOptions(
            "Production",
            "instructions",
            "instructions@collisionengineers.co.uk",
            Path.GetTempPath()));

        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ConflictReplayApprovedInboxSource(
        ReadOnlyMemory<byte> originalContent,
        ReadOnlyMemory<byte> conflictingContent,
        ReadOnlyMemory<byte> laterContent) : IApprovedInboxSource
    {
        public Task<ApprovedInboxPage> ReadAsync(
            ApprovedInboxPollLease lease,
            int maximumMessages,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = lease.Cursor switch
            {
                null => new ApprovedInboxPage(
                    [
                        new(
                            "shared-immutable-message",
                            "shared.eml",
                            originalContent,
                            RecordedAtUtc,
                            "cursor-1")
                    ],
                    "cursor-1"),
                "cursor-1" => new ApprovedInboxPage(
                    [
                        new(
                            "shared-immutable-message",
                            "shared.eml",
                            conflictingContent,
                            RecordedAtUtc,
                            "cursor-2"),
                        new(
                            "later-immutable-message",
                            "later.eml",
                            laterContent,
                            RecordedAtUtc.AddMinutes(1),
                            "cursor-3")
                    ],
                    "cursor-3"),
                "cursor-3" => new ApprovedInboxPage(
                    [
                        new(
                            "shared-immutable-message",
                            "shared.eml",
                            originalContent,
                            RecordedAtUtc,
                            "cursor-4")
                    ],
                    "cursor-4"),
                "cursor-4" => new ApprovedInboxPage([], "cursor-4"),
                _ => throw new InvalidDataException("The test source received an unknown cursor.")
            };
            if (page.Messages.Count > maximumMessages)
            {
                throw new InvalidOperationException("The requested test page is too small.");
            }

            return Task.FromResult(page);
        }
    }

    private sealed class FailOnceForHashArtifactStore(
        string rootPath,
        string failureHash) : IIntakeArtifactStore, IIntakeQuarantineArtifactStore, IDisposable
    {
        private readonly FileSystemIntakeArtifactStore inner = new(rootPath);
        private int remainingFailures = 1;

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
            if (!string.Equals(actualHash, contentHash, StringComparison.Ordinal))
            {
                throw new IntakeArtifactIntegrityException();
            }

            if (string.Equals(contentHash, failureHash, StringComparison.Ordinal)
                && Interlocked.Exchange(ref remainingFailures, 0) == 1)
            {
                throw new IOException("Injected artifact retention failure.");
            }

            return inner.StoreAsync(contentHash, content, cancellationToken);
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            inner.ReadAsync(storageKey, cancellationToken);

        public Task<IntakeQuarantineArtifact> StoreStreamAsync(
            Stream content,
            long contentLength,
            CancellationToken cancellationToken) =>
            inner.StoreStreamAsync(content, contentLength, cancellationToken);

        public Task VerifyAsync(
            IntakeQuarantineArtifact artifact,
            CancellationToken cancellationToken) =>
            inner.VerifyAsync(artifact, cancellationToken);

        public void Dispose()
        {
            inner.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan duration) => currentUtcNow = currentUtcNow.Add(duration);
    }

    private static async Task CreateSizedFileAsync(string path, long length)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.Asynchronous);
        stream.SetLength(length);
        await stream.FlushAsync();
    }

    private static string CreateArtifactStorageKey(string contentHash) =>
        $"sha256/{contentHash[..2]}/{contentHash}";

    private static byte[] CreateForwardedProtocolMessage()
    {
        var original = new MimeMessage
        {
            Subject = "Attached protocol message",
            Body = new TextPart("plain") { Text = "Protocol-only attached content." }
        };
        original.From.Add(new MailboxAddress("Original", "original@example.invalid"));
        original.To.Add(new MailboxAddress("Inbox", "inbox@example.invalid"));

        var attached = new MessagePart
        {
            Message = original,
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
            {
                FileName = "original.eml"
            }
        };
        var outer = new MimeMessage
        {
            Subject = "Forwarded protocol container",
            Body = new Multipart("mixed")
            {
                new TextPart("plain") { Text = "Protocol-only outer content." },
                attached
            }
        };
        outer.From.Add(new MailboxAddress(
            "Technical Forwarder",
            "technical-forwarder@collisionengineers.co.uk"));
        outer.To.Add(new MailboxAddress("Approved Inbox", "instructions@collisionengineers.co.uk"));

        using var stream = new MemoryStream();
        outer.WriteTo(stream);
        return stream.ToArray();
    }

    private static byte[] CreateInlineForwardedProtocolMessage()
    {
        var outer = new MimeMessage
        {
            Subject = "Forwarded protocol container",
            Body = new TextPart("plain")
            {
                Text = "From: QDOS Instructions <instructions@qdosassist.co.uk>\r\n"
                    + "Sent: 11 August 2031 09:00\r\n"
                    + "To: Instructions <instructions@collisionengineers.co.uk>\r\n"
                    + "Subject: Protocol instruction\r\n\r\n"
                    + "Protocol-only outer content."
            }
        };
        outer.From.Add(new MailboxAddress("Desk", "desk@collisionengineers.co.uk"));
        outer.To.Add(new MailboxAddress("Approved Inbox", "instructions@collisionengineers.co.uk"));

        using var stream = new MemoryStream();
        outer.WriteTo(stream);
        return stream.ToArray();
    }

    private static IntakeReceiptDraft ClassificationDraft(
        string fileName,
        string sourceHash,
        string sourceToken,
        MailClassificationResult classification) =>
        new(
            SourceFileName: fileName,
            MediaType: "message/rfc822",
            SourceLength: 1,
            SourceHash: sourceHash,
            SourceIdentity: new(IntakeSourceChannel.Mailbox, sourceToken),
            ReceivedAtUtc: RecordedAtUtc,
            ProcessedAtUtc: RecordedAtUtc,
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
            MailClassificationDecision: classification);
}
