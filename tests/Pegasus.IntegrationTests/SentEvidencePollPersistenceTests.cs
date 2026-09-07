using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class SentEvidencePollPersistenceTests
{
    [Fact]
    public async Task LeaseCursorAndOutcomeReplayRemainDurable()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var nowUtc = DateTimeOffset.UtcNow;
        await database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes
            SET AllowSentEvidence = 1,
                MailboxIdentity = 'instructions',
                SentFolderIdentity = 'sent-items',
                ActivatedAtUtc = '{nowUtc.AddMinutes(-5):O}'
            WHERE Address = 'instructions@collisionengineers.co.uk'
            """);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure(
            (_, options) => options.UseSqlServer(database.ConnectionString));
        services.AddLocalApprovedSent(_ => new(
            LocalApprovedSentOptions.RequiredRuntimeProfile,
            "instructions",
            "instructions@collisionengineers.co.uk",
            "sent-items",
            Path.GetTempPath()));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISentEvidencePollStore>();
        var candidateQueries =
            scope.ServiceProvider.GetRequiredService<ISentEvidencePollOutcomeQueries>();
        var lease = Assert.IsType<ApprovedSentPollLease>(
            await store.ClaimAsync(nowUtc, TimeSpan.FromMinutes(1), default));
        var item = new ApprovedSentItem(
            "ambiguous-occurrence",
            new string('A', 64),
            "ambiguous.sent.json",
            ApprovedSentItemObservationKind.Discovered,
            new(
                "instructions",
                "instructions@collisionengineers.co.uk",
                "sent-items",
                "immutable-item",
                "<response@example.test>",
                "conversation-1",
                "reply-chain-1",
                ["<request@example.test>"],
                [],
                nowUtc.AddMinutes(-1),
                new string('B', 64)),
            MalformedReasonCode: null,
            "durable-cursor");
        var outcome = new SentEvidencePollOutcome(
            Guid.NewGuid(),
            SentEvidencePollOutcomeKind.Ambiguous,
            item,
            RelatedEvidenceId: null,
            FailureCode: null,
            nowUtc,
            "sent-poll:durable-replay");

        await store.RecordOutcomeAsync(lease.MailboxId, lease.LeaseToken, outcome, default);
        await store.RecordOutcomeAsync(
            lease.MailboxId,
            lease.LeaseToken,
            outcome with { RecordedAtUtc = nowUtc.AddSeconds(1) },
            default);
        var terminalItem = new ApprovedSentItem(
            "changed-occurrence",
            new string('D', 64),
            "changed.sent.json",
            ApprovedSentItemObservationKind.Changed,
            Provenance: null,
            "immutable_sent_source_changed",
            "terminal-cursor",
            new string('C', 64),
            new string('D', 64),
            "changed");
        var terminalOutcome = new SentEvidencePollOutcome(
            Guid.NewGuid(),
            SentEvidencePollOutcomeKind.MalformedQuarantined,
            terminalItem,
            RelatedEvidenceId: null,
            "immutable_sent_source_changed",
            nowUtc.AddSeconds(1),
            "sent-poll:durable-terminal");
        await store.RecordOutcomeAsync(
            lease.MailboxId,
            lease.LeaseToken,
            terminalOutcome,
            default);
        await store.CompleteAsync(
            lease.MailboxId,
            lease.LeaseToken,
            terminalItem.NextCursor,
            nowUtc.AddSeconds(1),
            hasRemainingItems: false,
            default);

        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT COUNT_BIG(*) FROM ApprovedSentPollOutcomes"));
        Assert.Equal(
            terminalItem.NextCursor,
            await database.ScalarAsync<string>("SELECT [Cursor] FROM ApprovedSentPollStates WHERE MailboxId = 'instructions'"));
        Assert.Equal(
            new string('C', 64),
            await database.ScalarAsync<string>(
                "SELECT OriginalSourceSha256 FROM ApprovedSentPollOutcomes WHERE EvidenceMarker = 'changed'"));
        Assert.Equal(
            new string('D', 64),
            await database.ScalarAsync<string>(
                "SELECT ObservedSourceSha256 FROM ApprovedSentPollOutcomes WHERE EvidenceMarker = 'changed'"));
        var candidate = Assert.Single(await candidateQueries.ListUnlinkedReplyCandidatesAsync(
            ["<request@example.test>"],
            20,
            default));
        Assert.Equal(outcome.Id, candidate.PollOutcomeId);
        Assert.Equal(item.Provenance!.InternetMessageIdentity, candidate.InternetMessageIdentity);
        Assert.Equal(item.SourceSha256, candidate.SourceSha256);
        Assert.Equal(nowUtc, candidate.DiscoveredAtUtc);
        Assert.Empty(await candidateQueries.ListUnlinkedReplyCandidatesAsync(
            ["<different-request@example.test>"],
            20,
            default));
        // The resumed claim is a real due claim: the completing store
        // schedules the next run a minute out, so the fixture asks past that
        // delay instead of inside it.
        var resumed = Assert.IsType<ApprovedSentPollLease>(
            await store.ClaimAsync(nowUtc.AddMinutes(2), TimeSpan.FromMinutes(1), default));
        Assert.Equal(terminalItem.NextCursor, resumed.Cursor);
    }

    [Fact]
    public async Task TwoApprovedMailboxesReceiveIndependentGenerationFolderCursorsFairly()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var secondId = Guid.NewGuid();
        await using (var db = await database.CreateContextAsync())
        {
            var first = await db.ApprovedMailboxes.SingleAsync(
                value => value.Address == "instructions@collisionengineers.co.uk");
            first.AllowSentEvidence = true;
            first.MailboxIdentity = "instructions";
            first.SentFolderIdentity = "sent-one";
            first.ActivatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            db.ApprovedMailboxes.Add(new ApprovedMailboxEntity
            {
                Id = secondId,
                Address = "reports@collisionengineers.co.uk",
                AllowSentEvidence = true,
                State = ApprovedMailboxState.Approved.ToString(),
                MailboxIdentity = "mailbox-two",
                InboxFolderIdentity = "inbox-two",
                SentFolderIdentity = "sent-two",
                MailboxGeneration = 7,
                ActivatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                Version = 1
            });
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddPegasusInfrastructure((_, options) => options.UseSqlServer(database.ConnectionString));
        services.AddScoped<ISentEvidencePollStore, EfSentEvidencePollStore>();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISentEvidencePollStore>();
        var now = DateTimeOffset.UtcNow;

        var firstLease = Assert.IsType<ApprovedSentPollLease>(
            await store.ClaimAsync(now, TimeSpan.FromMinutes(1), default));
        await store.CompleteAsync(
            firstLease.MailboxId, firstLease.LeaseToken, "cursor-one", now,
            hasRemainingItems: true, default);
        var secondLease = Assert.IsType<ApprovedSentPollLease>(
            await store.ClaimAsync(now, TimeSpan.FromMinutes(1), default));

        Assert.NotEqual(firstLease.ApprovedMailboxId, secondLease.ApprovedMailboxId);
        var leases = new[] { firstLease, secondLease };
        Assert.Contains(leases, value => value.MailboxId == "instructions"
            && value.SentFolderIdentity == "sent-one" && value.Generation > 0);
        Assert.Contains(leases, value => value.MailboxId == "mailbox-two"
            && value.SentFolderIdentity == "sent-two" && value.Generation == 7);
    }
    [Fact]
    public async Task ExactReplyPollAtomicallyLinksTriageAndReplayAllowsStaffCompletion()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-request.eml",
            "Triage Only Request\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-001\r\nVehicle Registration: AB12 CDE");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var activatedAtUtc = factory.Services.GetRequiredService<TimeProvider>()
            .GetUtcNow()
            .AddMinutes(-10);
        await factory.Database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes
            SET AllowSentEvidence = 1,
                MailboxIdentity = 'instructions',
                SentFolderIdentity = 'sent-items',
                ActivatedAtUtc = '{activatedAtUtc:O}'
            WHERE Address = 'instructions@collisionengineers.co.uk'
            """);

        var staffActor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var actorCode = DevelopmentOfflineIdentity.AdministratorId.ToString("D");
        Guid triageId;
        await using (var webScope = factory.Services.CreateAsyncScope())
        {
            var triageQueries = webScope.ServiceProvider.GetRequiredService<ITriageQueries>();
            var summary = Assert.Single(await triageQueries.ListAsync(null, default));
            var created = Assert.IsType<TriageDetail>(
                await triageQueries.GetAsync(summary.Id, default));
            Assert.Equal(receiptId, created.Record.Origin.ReceiptId);
            triageId = summary.Id;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure(
            (_, options) => options.UseSqlServer(factory.Database.ConnectionString));
        services.AddSingleton(factory.Services.GetRequiredService<TimeProvider>());
        services.AddLocalApprovedSent(_ => new(
            LocalApprovedSentOptions.RequiredRuntimeProfile,
            "instructions",
            "instructions@collisionengineers.co.uk",
            "sent-items",
            Path.GetTempPath()));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var finding = await scopedServices.GetRequiredService<IRecordTriageFinding>().ExecuteAsync(
            new(
                triageId,
                0,
                staffActor,
                "sent-poll-auto-link-finding",
                "Retained assessment before exact response",
                RoadworthinessFinding.Roadworthy,
                AssessmentFinding.Repairable,
                SupersedesFindingId: null),
            default);
        Assert.Equal(1, finding.Version);

        var sentAtUtc = scopedServices.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-5);
        var sentEvidence = await scopedServices.GetRequiredService<IRecordSentEmailEvidence>().ExecuteAsync(
            new(
                triageId,
                1,
                "<request-auto-link@example.test>",
                "Exact response requested",
                ["recipient@example.test"],
                new string('A', 64),
                sentAtUtc,
                sentAtUtc.AddDays(7),
                actorCode,
                "sent-poll-auto-link-request"),
            default);
        var responseItem = new ApprovedSentItem(
            "auto-link-occurrence",
            new string('B', 64),
            "auto-link.sent.json",
            ApprovedSentItemObservationKind.Discovered,
            new(
                "instructions",
                "instructions@collisionengineers.co.uk",
                "sent-items",
                "auto-link-immutable-item",
                "<response-auto-link@example.test>",
                "auto-link-conversation",
                "auto-link-reply-chain",
                [sentEvidence.MessageIdentity],
                [],
                sentAtUtc.AddMinutes(1),
                new string('C', 64)),
            MalformedReasonCode: null,
            "auto-link-cursor");
        var poll = new PollSentEvidence(
            scopedServices.GetRequiredService<ISentEvidencePollStore>(),
            new RepeatingApprovedSentSource(responseItem),
            scopedServices.GetRequiredService<IApprovedMailboxPolicy>(),
            scopedServices.GetRequiredService<IExactEmailResponseEvidenceQueries>(),
            scopedServices.GetRequiredService<IRecordEmailResponseEvidence>(),
            scopedServices.GetRequiredService<IRetainApprovedMailboxReportSentEvidence>(),
            scopedServices.GetRequiredService<IAutoLinkReportEvidence>(),
            scopedServices.GetRequiredService<TimeProvider>());

        var first = await poll.ExecuteAsync(
            1,
            10,
            ActionActor.SystemWorker("sent-evidence-poll"),
            default);
        // The completing poll schedules the next claim a minute out and the
        // fixture clock is fixed, so make this mailbox due again for the
        // replay: a real due claim, never a weakened idempotency assertion.
        await factory.Database.ExecuteAsync(
            "UPDATE ApprovedSentPollStates SET DueAtUtc = DATEADD(minute, -1, DueAtUtc) WHERE MailboxId = 'instructions'");
        var replay = await poll.ExecuteAsync(
            1,
            10,
            ActionActor.SystemWorker("sent-evidence-poll"),
            default);
        Assert.Equal(1, first.TriageResponsesRecorded);
        Assert.Equal(1, replay.TriageResponsesRecorded);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM EmailResponseEvidence WHERE SentEvidenceId = '{sentEvidence.Id:D}'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageResponseEvidenceLinks WHERE TriageId = '{triageId:D}'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageHistory WHERE TriageId = '{triageId:D}' AND EventType = 'triage_response_linked'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ApprovedSentPollOutcomes WHERE OutcomeKind = 'TriageResponseRecorded'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"""
                SELECT COUNT(*)
                FROM EmailResponseEvidence AS response
                INNER JOIN ApprovedSentPollOutcomes AS outcome
                    ON outcome.Id = response.PollOutcomeId
                WHERE response.SentEvidenceId = '{sentEvidence.Id:D}'
                    AND outcome.RelatedEvidenceId = '{sentEvidence.Id:D}'
                    AND outcome.OutcomeKind = 'TriageResponseRecorded'
                """));

        var detail = Assert.IsType<TriageDetail>(
            await scopedServices.GetRequiredService<IGetTriage>().ExecuteAsync(
                new(triageId, staffActor),
                default));
        Assert.Equal(TriageState.FindingRecorded, detail.Record.State);
        Assert.Equal(2, detail.Record.Version);
        Assert.Equal(sentEvidence.Id, Assert.Single(detail.ResponseEvidence).SentEvidenceId);
        var linkedHistory = Assert.Single(
            detail.History,
            item => item.EventType == "triage_response_linked");
        Assert.Equal("sent-evidence-poll", linkedHistory.Actor);
        Assert.Equal(nameof(ActorKind.SystemWorker), linkedHistory.ActorKind);
        Assert.Equal(1, linkedHistory.BeforeVersion);
        Assert.Equal(2, linkedHistory.AfterVersion);

        var pollOutcomeId = await factory.Database.ScalarAsync<Guid>(
            $"SELECT PollOutcomeId FROM EmailResponseEvidence WHERE SentEvidenceId = '{sentEvidence.Id:D}'");
        var unlinkRequest = new TriageResponseEvidenceUnlinkRequest(
            triageId,
            sentEvidence.Id,
            2,
            staffActor,
            "sent-poll-response-unlink",
            "Temporarily remove the current response association");
        var unlinkResponse = scopedServices.GetRequiredService<IUnlinkTriageResponseEvidence>();
        await unlinkResponse.ExecuteAsync(unlinkRequest, default);
        await unlinkResponse.ExecuteAsync(unlinkRequest, default);

        var unlinkedDetail = Assert.IsType<TriageDetail>(
            await scopedServices.GetRequiredService<IGetTriage>().ExecuteAsync(
                new(triageId, staffActor),
                default));
        Assert.Equal(3, unlinkedDetail.Record.Version);
        Assert.Equal(detail.Record.State, unlinkedDetail.Record.State);
        Assert.Equal(detail.Record.LinkedCaseId, unlinkedDetail.Record.LinkedCaseId);
        Assert.Equal(detail.Findings, unlinkedDetail.Findings);
        Assert.Empty(unlinkedDetail.ResponseEvidence);
        Assert.Equal(
            pollOutcomeId,
            Assert.Single(unlinkedDetail.ResponseEvidenceCandidates).PollOutcomeId);
        Assert.Equal(
            0,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageResponseEvidenceLinks WHERE TriageId = '{triageId:D}'"));

        var relinkRequest = new TriageResponseEvidenceLinkRequest(
            triageId,
            pollOutcomeId,
            sentEvidence.Id,
            3,
            staffActor,
            "sent-poll-response-relink",
            "Restore the retained exact response association");
        var linkResponse = scopedServices.GetRequiredService<ILinkTriageResponseEvidence>();
        await linkResponse.ExecuteAsync(relinkRequest, default);
        await linkResponse.ExecuteAsync(relinkRequest, default);

        var relinkedDetail = Assert.IsType<TriageDetail>(
            await scopedServices.GetRequiredService<IGetTriage>().ExecuteAsync(
                new(triageId, staffActor),
                default));
        Assert.Equal(4, relinkedDetail.Record.Version);
        Assert.Equal(detail.Record.State, relinkedDetail.Record.State);
        Assert.Equal(detail.Record.LinkedCaseId, relinkedDetail.Record.LinkedCaseId);
        Assert.Equal(detail.Findings, relinkedDetail.Findings);
        Assert.Equal(sentEvidence.Id, Assert.Single(relinkedDetail.ResponseEvidence).SentEvidenceId);
        Assert.Empty(relinkedDetail.ResponseEvidenceCandidates);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageResponseEvidenceLinks WHERE TriageId = '{triageId:D}'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM EmailResponseEvidence WHERE SentEvidenceId = '{sentEvidence.Id:D}'"));
        Assert.Equal(
            2,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageHistory WHERE TriageId = '{triageId:D}' AND EventType = 'triage_response_linked'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageHistory WHERE TriageId = '{triageId:D}' AND EventType = 'triage_response_unlinked'"));

        var completed = await scopedServices.GetRequiredService<ICompleteTriage>().ExecuteAsync(
            new(
                triageId,
                4,
                staffActor,
                "sent-poll-auto-link-complete",
                "Finding and exact response evidence confirmed"),
            default);
        Assert.Equal(TriageState.Completed, completed.State);
        Assert.Equal(5, completed.Version);
    }

    private sealed class RepeatingApprovedSentSource(ApprovedSentItem item) : IApprovedSentSource
    {
        public Task<ApprovedSentPage> ReadAsync(
            ApprovedSentPollLease lease,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ApprovedSentPage([item], item.NextCursor, HasMore: false));
        }
    }
}
