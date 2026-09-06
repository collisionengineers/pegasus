using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed partial class QdosTriageIntegrationTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task NamedMutationRetriesReturnHistoricalResultsAndRetainConflictAndStateGates()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-replay.eml",
            "QDOS instruction\r\nClaimant Name: Replay Claimant\r\nClaim Number: TRIAGE-REPLAY\r\nVehicle Registration: AB12 CDE");
        _ = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content);

        var initial = await GetOnlyTriageAsync(factory.Services);
        var triageId = initial.Record.Id;
        var actor = DevelopmentOfflineIdentity.AdministratorId.ToString("D");
        var staffActor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recordFinding = services.GetRequiredService<IRecordTriageFinding>();
        var supersedeFinding = services.GetRequiredService<ISupersedeTriageFinding>();
        var awaitInformation = services.GetRequiredService<IAwaitTriageInformation>();
        var cancel = services.GetRequiredService<ICancelTriage>();
        var reopen = services.GetRequiredService<IReopenTriage>();
        var complete = services.GetRequiredService<ICompleteTriage>();

        var recordRequest = new RecordTriageFindingRequest(
            triageId,
            0,
            staffActor,
            "replay-record-finding",
            "Initial retained assessment",
            RoadworthinessFinding.Unroadworthy,
            AssessmentFinding.TotalLoss,
            null);
        var recorded = await recordFinding.ExecuteAsync(recordRequest, CancellationToken.None);
        Assert.Equal(1, recorded.Version);
        var firstFinding = Assert.Single(
            (await GetTriageAsync(factory.Services, triageId)).Findings);

        var supersedeRequest = new RecordTriageFindingRequest(
            triageId,
            1,
            staffActor,
            "replay-supersede-finding",
            "Corrected retained assessment",
            RoadworthinessFinding.Roadworthy,
            AssessmentFinding.Repairable,
            firstFinding.Id);
        var superseded = await supersedeFinding.ExecuteAsync(
            supersedeRequest,
            CancellationToken.None);
        Assert.Equal(2, superseded.Version);
        var secondFinding = Assert.Single(
            (await GetTriageAsync(factory.Services, triageId)).Findings,
            finding => finding.SupersedesFindingId == firstFinding.Id);

        var awaitRequest = new TriageMutationRequest(
            triageId,
            2,
            staffActor,
            "replay-await-information",
            "Further retained information is required");
        var awaiting = await awaitInformation.ExecuteAsync(awaitRequest, CancellationToken.None);
        Assert.Equal(3, awaiting.Version);
        Assert.Equal(TriageState.AwaitingInformation, awaiting.State);

        var invalidNewAwait = await Assert.ThrowsAsync<InvalidOperationException>(
            () => awaitInformation.ExecuteAsync(
                awaitRequest with
                {
                    ExpectedVersion = 3,
                    OperationKey = "new-await-information",
                    Reason = "A new request must still satisfy the current-state gate"
                },
                CancellationToken.None));
        Assert.Contains("only while open", invalidNewAwait.Message, StringComparison.OrdinalIgnoreCase);

        var cancelRequest = new TriageMutationRequest(
            triageId,
            3,
            staffActor,
            "replay-cancel",
            "The instruction was withdrawn");
        var cancelled = await cancel.ExecuteAsync(cancelRequest, CancellationToken.None);
        Assert.Equal(4, cancelled.Version);
        Assert.Equal(TriageState.Cancelled, cancelled.State);

        var reopenRequest = new TriageMutationRequest(
            triageId,
            4,
            staffActor,
            "replay-reopen",
            "Further retained evidence requires review");
        var reopened = await reopen.ExecuteAsync(reopenRequest, CancellationToken.None);
        Assert.Equal(5, reopened.Version);
        Assert.Equal(TriageState.Open, reopened.State);

        await Assert.ThrowsAsync<TriageVersionConflictException>(
            () => awaitInformation.ExecuteAsync(
                awaitRequest with
                {
                    ExpectedVersion = 4,
                    OperationKey = "new-stale-await-information",
                    Reason = "A new operation must retain optimistic concurrency"
                },
                CancellationToken.None));

        var postReopenCorrection = new RecordTriageFindingRequest(
            triageId,
            5,
            staffActor,
            "post-reopen-finding-correction",
            "Final retained assessment before completion",
            RoadworthinessFinding.Unroadworthy,
            AssessmentFinding.Repairable,
            secondFinding.Id);
        var corrected = await supersedeFinding.ExecuteAsync(
            postReopenCorrection,
            CancellationToken.None);
        Assert.Equal(6, corrected.Version);

        var sentEvidence = await services.GetRequiredService<IRecordSentEmailEvidence>().ExecuteAsync(
            new(
                triageId,
                6,
                "sent-item:triage-replay",
                "Triage response",
                ["recipient@example.test"],
                new string('c', 64),
                new DateTimeOffset(2031, 5, 6, 11, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2031, 5, 13, 11, 0, 0, TimeSpan.Zero),
                actor,
                "record-sent-triage-replay"),
            CancellationToken.None);
        var pollOutcomeId = Guid.NewGuid();
        await SeedReplyCandidateAsync(
            factory.Database,
            pollOutcomeId,
            sentEvidence.MessageIdentity);
        var candidateDetail = await services.GetRequiredService<IGetTriage>().ExecuteAsync(
            new(triageId, staffActor),
            CancellationToken.None);
        var candidate = Assert.Single(
            Assert.IsType<TriageDetail>(candidateDetail).ResponseEvidenceCandidates);
        Assert.Equal(pollOutcomeId, candidate.PollOutcomeId);
        Assert.Equal(sentEvidence.Id, candidate.SentEvidenceId);

        var linkResponse = services.GetRequiredService<ILinkTriageResponseEvidence>();
        var linkRequest = new TriageResponseEvidenceLinkRequest(
            triageId,
            pollOutcomeId,
            sentEvidence.Id,
            6,
            staffActor,
            "link-response-triage-replay",
            "Exact reply-chain evidence retained");
        await linkResponse.ExecuteAsync(linkRequest, CancellationToken.None);
        await linkResponse.ExecuteAsync(linkRequest, CancellationToken.None);
        await AssertReplayConflictAsync(
            () => linkResponse.ExecuteAsync(
                linkRequest with { SentEvidenceId = Guid.NewGuid() },
                CancellationToken.None));
        var secondSelection = await Assert.ThrowsAsync<TriageResponseEvidenceAlreadyLinkedException>(
            () => linkResponse.ExecuteAsync(
                linkRequest with
                {
                    ExpectedVersion = 7,
                    OperationKey = "link-response-triage-second-selection"
                },
                CancellationToken.None));
        Assert.Equal(triageId, secondSelection.TriageId);
        Assert.Equal(
            sentEvidence.Id,
            await factory.Database.ScalarAsync<Guid>(
                $"SELECT RelatedEvidenceId FROM ApprovedSentPollOutcomes WHERE Id = '{pollOutcomeId:D}'"));
        Assert.Equal(
            "TriageResponseRecorded",
            await factory.Database.ScalarAsync<string>(
                $"SELECT OutcomeKind FROM ApprovedSentPollOutcomes WHERE Id = '{pollOutcomeId:D}'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM EmailResponseEvidence WHERE PollOutcomeId = '{pollOutcomeId:D}'"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM TriageResponseEvidenceLinks WHERE TriageId = '{triageId:D}'"));

        var completeRequest = new TriageMutationRequest(
            triageId,
            7,
            staffActor,
            "replay-complete",
            "Finding and exact response evidence confirmed");
        var completed = await complete.ExecuteAsync(completeRequest, CancellationToken.None);
        Assert.Equal(8, completed.Version);
        Assert.Equal(TriageState.Completed, completed.State);

        Assert.Equal(recorded, await recordFinding.ExecuteAsync(recordRequest, CancellationToken.None));
        Assert.Equal(
            superseded,
            await supersedeFinding.ExecuteAsync(supersedeRequest, CancellationToken.None));
        Assert.Equal(
            awaiting,
            await awaitInformation.ExecuteAsync(awaitRequest, CancellationToken.None));
        Assert.Equal(cancelled, await cancel.ExecuteAsync(cancelRequest, CancellationToken.None));
        Assert.Equal(reopened, await reopen.ExecuteAsync(reopenRequest, CancellationToken.None));
        Assert.Equal(completed, await complete.ExecuteAsync(completeRequest, CancellationToken.None));
        await linkResponse.ExecuteAsync(linkRequest, CancellationToken.None);

        await AssertReplayConflictAsync(
            () => recordFinding.ExecuteAsync(
                recordRequest with { Reason = "Altered record request" },
                CancellationToken.None));
        await AssertReplayConflictAsync(
            () => supersedeFinding.ExecuteAsync(
                supersedeRequest with { Reason = "Altered supersession request" },
                CancellationToken.None));
        await AssertReplayConflictAsync(
            () => awaitInformation.ExecuteAsync(
                awaitRequest with { Reason = "Altered await request" },
                CancellationToken.None));
        await AssertReplayConflictAsync(
            () => cancel.ExecuteAsync(
                cancelRequest with { Reason = "Altered cancellation request" },
                CancellationToken.None));
        await AssertReplayConflictAsync(
            () => reopen.ExecuteAsync(
                reopenRequest with { Reason = "Altered reopen request" },
                CancellationToken.None));
        await AssertReplayConflictAsync(
            () => complete.ExecuteAsync(
                completeRequest with { Reason = "Altered completion request" },
                CancellationToken.None));
        // The actor kind is part of the command, not decoration: the same
        // subject acting as Automation rather than Staff is a different
        // command, so the committed key conflicts instead of replaying.
        await AssertReplayConflictAsync(
            () => complete.ExecuteAsync(
                completeRequest with { Actor = ActionActor.Automation(staffActor.SubjectId) },
                CancellationToken.None));

        var invalidNewCompletion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => complete.ExecuteAsync(
                completeRequest with
                {
                    ExpectedVersion = 8,
                    OperationKey = "new-invalid-completion",
                    Reason = "A new request must not bypass the terminal-state gate"
                },
                CancellationToken.None));
        Assert.Contains("only after a finding", invalidNewCompletion.Message, StringComparison.OrdinalIgnoreCase);

        var final = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(8, final.Record.Version);
        Assert.Equal(9, final.History.Count);
        Assert.All(
            new[]
            {
                recordRequest.OperationKey,
                supersedeRequest.OperationKey,
                awaitRequest.OperationKey,
                cancelRequest.OperationKey,
                reopenRequest.OperationKey,
                completeRequest.OperationKey
            },
            operationKey => Assert.Single(
                final.History,
                history => history.OperationKey == operationKey));
    }

    private static Task SeedReplyCandidateAsync(
        LocalDbTestDatabase database,
        Guid pollOutcomeId,
        string inReplyToIdentity)
    {
        var replyIdentity = inReplyToIdentity.Replace("'", "''", StringComparison.Ordinal);
        return database.ExecuteAsync(
            $"""
            INSERT INTO ApprovedSentPollStates
                (MailboxId, MailboxAddress, SentFolderIdentity, DueAtUtc)
            VALUES
                ('triage-replay-mailbox', 'replies@example.test', 'sent-folder:triage-replay', '2031-05-06T10:00:00+00:00');

            INSERT INTO ApprovedSentPollOutcomes
                (Id, MailboxId, MailboxAddress, SourceOccurrenceIdentity, SourceSha256,
                 CurrentLocationIdentity, ObservationKind, SentFolderIdentity,
                 ImmutableItemIdentity, InternetMessageIdentity, ConversationIdentity,
                 ReplyChainIdentity, InReplyToIdentitiesJson,
                 AuthoritativeCaseIdentitiesJson, SentAtUtc, MimeSha256, OutcomeKind,
                 RelatedEvidenceId, FailureCode, RecordedAtUtc, CursorAfterItem, OperationKey)
            VALUES
                ('{pollOutcomeId:D}', 'triage-replay-mailbox', 'replies@example.test',
                 'source-occurrence:triage-replay', '{new string('d', 64)}',
                 'sent-folder:triage-replay', 'Discovered', 'sent-folder:triage-replay',
                 'immutable-item:triage-replay', '<reply-triage-replay@example.test>',
                 'conversation:triage-replay', 'reply-chain:triage-replay',
                 '["{replyIdentity}"]', '[]', '2031-05-06T12:00:00+00:00',
                 '{new string('e', 64)}', 'Ambiguous', NULL, NULL,
                 '2031-05-06T12:01:00+00:00', 'cursor:triage-replay',
                 'poll-outcome:triage-replay');
            """);
    }

    private static async Task AssertReplayConflictAsync(Func<Task> action) =>
        _ = await Assert.ThrowsAsync<TriageOperationConflictException>(action);
}
