using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class PollSentEvidenceTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("sent-evidence-poll");

    [Fact]
    public async Task DuplicateReplayKeepsOneExactTriageResponseAndOneOutcome()
    {
        var lease = Lease();
        var item = Item(
            "occurrence-1",
            "copy.sent.json",
            "cursor-1",
            ["<request-1@example.test>"],
            []);
        var page = new ApprovedSentPage([item], item.NextCursor, HasMore: false);
        var pollStore = new RecordingPollStore(lease, lease);
        var source = new SequencedSource(page, page);
        var responsePort = new ResponsePort(
            new ExactEmailResponseEvidenceCandidate(
                Guid.NewGuid(),
                4,
                "<request-1@example.test>",
                RecordedResponseMessageIdentity: null));
        var reportPort = new ReportPort();
        var useCase = CreateUseCase(pollStore, source, responsePort, reportPort);

        var first = await useCase.ExecuteAsync(3, 25, WorkerActor);
        var replay = await useCase.ExecuteAsync(3, 25, WorkerActor);

        Assert.Equal(1, first.TriageResponsesRecorded);
        Assert.Equal(1, replay.TriageResponsesRecorded);
        Assert.Single(responsePort.Attempts);
        Assert.Single(responsePort.UniqueOperations);
        Assert.Equal(item.Provenance!.InternetMessageIdentity, responsePort.Attempts[0].MessageIdentity);
        Assert.Equal(item.SourceOccurrenceIdentity, responsePort.Attempts[0].SourceOccurrenceIdentity);
        Assert.Equal(item.SourceSha256, responsePort.Attempts[0].SourceSha256);
        Assert.Equal(item.Provenance.MimeSha256, responsePort.Attempts[0].MimeSha256);
        Assert.Equal(lease.LeaseToken, responsePort.Attempts[0].PollLeaseToken);
        Assert.Equal(item.CurrentLocationIdentity, responsePort.Attempts[0].CurrentLocationIdentity);
        Assert.Equal(item.NextCursor, responsePort.Attempts[0].CursorAfterItem);
        Assert.Equal(
            pollStore.OutcomeAttempts[0].OperationKey,
            responsePort.Attempts[0].PollOutcomeOperationKey);
        Assert.Equal(pollStore.OutcomeAttempts[0].Id, responsePort.Attempts[0].PollOutcomeId);
        Assert.Equal(2, pollStore.OutcomeAttempts.Count);
        Assert.Single(pollStore.UniqueOutcomes);
        Assert.All(
            pollStore.OutcomeAttempts,
            outcome => Assert.Equal(SentEvidencePollOutcomeKind.TriageResponseRecorded, outcome.Kind));
        Assert.Empty(reportPort.Requests);
    }

    [Fact]
    public async Task NoReplyAndAmbiguousReplyChainsRemainVisibleWithoutAutomaticEvidence()
    {
        var unmatched = Item("occurrence-unmatched", "unmatched.sent.json", "cursor-1", [], []);
        var ambiguousReply = Item(
            "occurrence-ambiguous-reply",
            "ambiguous-reply.sent.json",
            "cursor-2",
            ["<shared-request@example.test>"],
            []);
        var ambiguousCase = Item(
            "occurrence-ambiguous-case",
            "ambiguous-case.sent.json",
            "cursor-3",
            [],
            [Guid.NewGuid(), Guid.NewGuid()]);
        var pollStore = new RecordingPollStore(Lease());
        var source = new SequencedSource(
            new ApprovedSentPage([unmatched, ambiguousReply, ambiguousCase], "cursor-3", HasMore: false));
        var responsePort = new ResponsePort(
            new(Guid.NewGuid(), 1, "<shared-request@example.test>", null),
            new(Guid.NewGuid(), 2, "<shared-request@example.test>", null));
        var reportPort = new ReportPort();
        var autoLinkPort = AutoLinkPort.NotLinked();
        var useCase = CreateUseCase(
            pollStore,
            source,
            responsePort,
            reportPort,
            autoLinkPort: autoLinkPort);

        var result = await useCase.ExecuteAsync(2, 10, WorkerActor);

        Assert.Equal(3, result.ItemsHandled);
        Assert.Equal(3, result.UnlinkedItems);
        Assert.Equal(0, result.TriageResponsesRecorded);
        Assert.Empty(responsePort.Attempts);
        Assert.Single(reportPort.Requests);
        Assert.Equal(
            ambiguousCase.Provenance!.ImmutableItemIdentity,
            reportPort.Requests[0].ImmutableItemIdentity);
        Assert.Equal(
            [
                SentEvidencePollOutcomeKind.Unmatched,
                SentEvidencePollOutcomeKind.Ambiguous,
                SentEvidencePollOutcomeKind.Ambiguous
            ],
            pollStore.OutcomeAttempts.Select(outcome => outcome.Kind));
        Assert.All(
            pollStore.OutcomeAttempts,
            outcome => Assert.NotEqual(SentEvidencePollOutcomeKind.TriageResponseRecorded, outcome.Kind));
        Assert.Empty(autoLinkPort.Requests);
    }

    [Fact]
    public async Task ExistingTriageResponseLinkBecomesBoundedAmbiguityAndLaterItemsContinue()
    {
        var conflict = Item(
            "occurrence-existing-link",
            "existing-link.sent.json",
            "cursor-existing-link",
            ["<request-existing-link@example.test>"],
            []);
        var later = Item(
            "occurrence-after-existing-link",
            "after-existing-link.sent.json",
            "cursor-after-existing-link",
            [],
            []);
        var pollStore = new RecordingPollStore(Lease());
        var responsePort = new ResponsePort(
            new ExactEmailResponseEvidenceCandidate(
                Guid.NewGuid(),
                0,
                "<request-existing-link@example.test>",
                RecordedResponseMessageIdentity: null))
        {
            RecordingFailure = new TriageResponseEvidenceAlreadyLinkedException(Guid.NewGuid())
        };
        var useCase = CreateUseCase(
            pollStore,
            new SequencedSource(
                new ApprovedSentPage([conflict, later], later.NextCursor, HasMore: false)),
            responsePort,
            new ReportPort());

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(2, result.ItemsHandled);
        Assert.Equal(2, result.UnlinkedItems);
        Assert.Equal(0, result.TriageResponsesRecorded);
        Assert.Empty(pollStore.Releases);
        Assert.Single(pollStore.Completions);
        Assert.Equal(
            [
                SentEvidencePollOutcomeKind.Ambiguous,
                SentEvidencePollOutcomeKind.Unmatched
            ],
            pollStore.OutcomeAttempts.Select(outcome => outcome.Kind));
        Assert.Single(responsePort.Attempts);
    }

    [Fact]
    public async Task ExactCaseIdentityAutoLinksRetainedReportEvidence()
    {
        var caseId = Guid.NewGuid();
        var item = Item("occurrence-report", "report.sent.json", "cursor-report", [], [caseId]);
        var pollStore = new RecordingPollStore(Lease());
        var responsePort = new ResponsePort();
        var reportPort = new ReportPort();
        var autoLinkPort = AutoLinkPort.Linked();
        var useCase = CreateUseCase(
            pollStore,
            new SequencedSource(new ApprovedSentPage([item], item.NextCursor, HasMore: false)),
            responsePort,
            reportPort,
            autoLinkPort: autoLinkPort);

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(1, result.ReportEvidenceRetained);
        Assert.Equal(0, result.UnlinkedItems);
        Assert.Empty(responsePort.Attempts);
        var request = Assert.Single(reportPort.Requests);
        Assert.Equal(item.Provenance!.MailboxAddress, request.MailboxIdentity);
        Assert.Equal(item.Provenance.InternetMessageIdentity, request.InternetMessageIdentity);
        Assert.Equal(item.SourceOccurrenceIdentity, request.SourceOccurrenceIdentity);
        Assert.Equal(item.SourceSha256, request.SourceSha256);
        Assert.Equal(item.Provenance.MimeSha256, request.MimeSha256);
        var autoLink = Assert.Single(autoLinkPort.Requests);
        Assert.Equal(caseId, autoLink.CaseId);
        Assert.Equal(reportPort.Retained[0].EvidenceId, autoLink.EvidenceId);
        Assert.Equal(WorkerActor, autoLink.Actor);
        var outcome = Assert.Single(pollStore.OutcomeAttempts);
        Assert.Equal(SentEvidencePollOutcomeKind.ReportEvidenceAutoLinked, outcome.Kind);
        Assert.Equal(reportPort.Retained[0].EvidenceId, outcome.RelatedEvidenceId);
        Assert.Null(outcome.FailureCode);
    }

    [Fact]
    public async Task IneligibleExactCaseRetainsReportEvidenceVisibleAndUnlinked()
    {
        var caseId = Guid.NewGuid();
        var item = Item("occurrence-unready-report", "unready.sent.json", "cursor-unready", [], [caseId]);
        var pollStore = new RecordingPollStore(Lease());
        var reportPort = new ReportPort();
        var autoLinkPort = AutoLinkPort.NotLinked("case_not_report_preparation");
        var useCase = CreateUseCase(
            pollStore,
            new SequencedSource(new ApprovedSentPage([item], item.NextCursor, HasMore: false)),
            new ResponsePort(),
            reportPort,
            autoLinkPort: autoLinkPort);

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(1, result.ReportEvidenceRetained);
        Assert.Equal(1, result.UnlinkedItems);
        Assert.Single(reportPort.Requests);
        Assert.Single(autoLinkPort.Requests);
        var outcome = Assert.Single(pollStore.OutcomeAttempts);
        Assert.Equal(SentEvidencePollOutcomeKind.ReportEvidenceRetainedUnlinked, outcome.Kind);
        Assert.Equal("case_not_report_preparation", outcome.FailureCode);
        Assert.Equal(reportPort.Retained[0].EvidenceId, outcome.RelatedEvidenceId);
    }

    [Fact]
    public async Task AmbiguousCaseIdentitiesRetainOneVisibleUnlinkedReportItem()
    {
        var item = Item(
            "occurrence-ambiguous-report",
            "ambiguous-report.sent.json",
            "cursor-ambiguous-report",
            [],
            [Guid.NewGuid(), Guid.NewGuid()]);
        var pollStore = new RecordingPollStore(Lease());
        var reportPort = new ReportPort();
        var autoLinkPort = AutoLinkPort.NotLinked();
        var useCase = CreateUseCase(
            pollStore,
            new SequencedSource(new ApprovedSentPage([item], item.NextCursor, HasMore: false)),
            new ResponsePort(),
            reportPort,
            autoLinkPort: autoLinkPort);

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(1, result.ReportEvidenceRetained);
        Assert.Equal(1, result.UnlinkedItems);
        Assert.Single(reportPort.Requests);
        Assert.Empty(autoLinkPort.Requests);
        var outcome = Assert.Single(pollStore.OutcomeAttempts);
        Assert.Equal(SentEvidencePollOutcomeKind.Ambiguous, outcome.Kind);
        Assert.Equal(reportPort.Retained[0].EvidenceId, outcome.RelatedEvidenceId);
    }

    [Fact]
    public async Task MalformedCopyIsQuarantinedWithoutMatchingOrRetention()
    {
        var malformed = new ApprovedSentItem(
            "malformed-occurrence",
            new string('A', 64),
            "malformed.sent.json",
            ApprovedSentItemObservationKind.Discovered,
            Provenance: null,
            "invalid_sent_copy",
            "cursor-malformed");
        var pollStore = new RecordingPollStore(Lease());
        var responsePort = new ResponsePort();
        var reportPort = new ReportPort();
        var useCase = CreateUseCase(
            pollStore,
            new SequencedSource(new ApprovedSentPage([malformed], malformed.NextCursor, HasMore: false)),
            responsePort,
            reportPort);

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(1, result.QuarantinedItems);
        Assert.Empty(responsePort.Attempts);
        Assert.Empty(reportPort.Requests);
        var outcome = Assert.Single(pollStore.OutcomeAttempts);
        Assert.Equal(SentEvidencePollOutcomeKind.MalformedQuarantined, outcome.Kind);
        Assert.Equal("invalid_sent_copy", outcome.FailureCode);
        Assert.Null(outcome.RelatedEvidenceId);
    }

    [Fact]
    public async Task ThrottleReleasesLeaseAtRetryAfterAndRecoversOnLaterClaim()
    {
        var lease = Lease();
        var retryAfter = TimeSpan.FromMinutes(3);
        var timeProvider = new AdjustableTimeProvider(NowUtc);
        var pollStore = new RecordingPollStore(lease, null, lease);
        var source = new SequencedSource(
            new ApprovedSentSourceThrottledException(retryAfter),
            new ApprovedSentPage([], "cursor-recovered", HasMore: false));
        var useCase = CreateUseCase(
            pollStore,
            source,
            new ResponsePort(),
            new ReportPort(),
            timeProvider);

        await Assert.ThrowsAsync<ApprovedSentSourceThrottledException>(
            () => useCase.ExecuteAsync(2, 10, WorkerActor));

        var release = Assert.Single(pollStore.Releases);
        Assert.Equal(NowUtc.Add(retryAfter), release.DueAtUtc);
        Assert.Equal("sent_source_throttled", release.FailureCode);
        var beforeDue = await useCase.ExecuteAsync(2, 10, WorkerActor);
        Assert.Equal(PollSentEvidenceResult.Empty, beforeDue);
        Assert.Equal(1, source.CallCount);

        timeProvider.Advance(retryAfter);
        var recovered = await useCase.ExecuteAsync(2, 10, WorkerActor);

        Assert.Equal(1, recovered.PagesRead);
        Assert.Equal(0, recovered.ItemsHandled);
        Assert.Equal(2, source.CallCount);
        Assert.Equal("cursor-recovered", Assert.Single(pollStore.Completions).Cursor);
    }

    [Fact]
    public async Task CopyOutsideConfiguredSentFolderIsQuarantined()
    {
        var item = Item("occurrence-drafts", "drafts.sent.json", "cursor-drafts", [], []);
        item = item with
        {
            Provenance = item.Provenance! with { SentFolderIdentity = "drafts" }
        };
        var pollStore = new RecordingPollStore(Lease());
        var useCase = CreateUseCase(
            pollStore,
            new SequencedSource(new ApprovedSentPage([item], item.NextCursor, HasMore: false)),
            new ResponsePort(),
            new ReportPort());

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(1, result.QuarantinedItems);
        var outcome = Assert.Single(pollStore.OutcomeAttempts);
        Assert.Equal(SentEvidencePollOutcomeKind.MalformedQuarantined, outcome.Kind);
        Assert.Equal("invalid_sent_source_item", outcome.FailureCode);
    }

    [Fact]
    public async Task MoveAndDeleteAreFinalityOutcomesNotNewMatches()
    {
        var discovered = Item(
            "occurrence-finality",
            "archive/report.sent.json",
            "cursor-moved",
            ["<request-finality@example.test>"],
            []);
        var moved = discovered with
        {
            ObservationKind = ApprovedSentItemObservationKind.Moved
        };
        var deleted = discovered with
        {
            CurrentLocationIdentity = null,
            ObservationKind = ApprovedSentItemObservationKind.Deleted,
            NextCursor = "cursor-deleted"
        };
        var pollStore = new RecordingPollStore(Lease(), Lease());
        var source = new SequencedSource(
            new ApprovedSentPage([moved], moved.NextCursor, HasMore: false),
            new ApprovedSentPage([deleted], deleted.NextCursor, HasMore: false));
        var responsePort = new ResponsePort(
            new ExactEmailResponseEvidenceCandidate(
                Guid.NewGuid(),
                1,
                "<request-finality@example.test>",
                null));
        var reportPort = new ReportPort();
        var useCase = CreateUseCase(pollStore, source, responsePort, reportPort);

        await useCase.ExecuteAsync(1, 10, WorkerActor);
        await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(
            [SentEvidencePollOutcomeKind.MoveObserved, SentEvidencePollOutcomeKind.DeleteObserved],
            pollStore.OutcomeAttempts.Select(outcome => outcome.Kind));
        Assert.Empty(responsePort.Attempts);
        Assert.Empty(reportPort.Requests);
    }

    [Fact]
    public async Task SourceErrorUsesSafeRetryDelayAndLaterPollRecovers()
    {
        var lease = Lease();
        var timeProvider = new AdjustableTimeProvider(NowUtc);
        var pollStore = new RecordingPollStore(lease, lease);
        var source = new SequencedSource(
            new InvalidDataException("The local immutable copy could not be read."),
            new ApprovedSentPage([], "cursor-after-recovery", HasMore: false));
        var useCase = CreateUseCase(
            pollStore,
            source,
            new ResponsePort(),
            new ReportPort(),
            timeProvider);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => useCase.ExecuteAsync(1, 10, WorkerActor));

        var release = Assert.Single(pollStore.Releases);
        Assert.Equal(NowUtc.AddSeconds(30), release.DueAtUtc);
        Assert.Equal("invalid_sent_source_item", release.FailureCode);
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        var recovered = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(1, recovered.PagesRead);
        Assert.Equal("cursor-after-recovery", Assert.Single(pollStore.Completions).Cursor);
    }

    [Fact]
    public async Task NotApprovedMailboxIsHandledAsAnEmptyTickWithoutThrowing()
    {
        // Mirrors the exact shape of the production row while it was mid-administration:
        // address and state correct, but AllowSentEvidence false for that route scope
        // (ApprovedMailboxes Id 49f47eb9-c5b0-464f-b8f0-8c90ba061728, 2026-08-10 to
        // 2026-08-19 in production). That is a correct rejection, not a comparison bug,
        // but it must not surface as an unhandled exception every poll tick.
        var lease = Lease();
        var timeProvider = new AdjustableTimeProvider(NowUtc);
        var pollStore = new RecordingPollStore(lease);
        var source = new SequencedSource();
        var useCase = CreateUseCase(
            pollStore,
            source,
            new ResponsePort(),
            new ReportPort(),
            timeProvider,
            policy: new RejectingPolicy());

        var result = await useCase.ExecuteAsync(1, 10, WorkerActor);

        Assert.Equal(PollSentEvidenceResult.Empty, result);
        Assert.Equal(0, source.CallCount);
        Assert.Empty(pollStore.OutcomeAttempts);
        Assert.Empty(pollStore.Completions);
        var release = Assert.Single(pollStore.Releases);
        Assert.Equal(NowUtc.AddSeconds(30), release.DueAtUtc);
        Assert.Equal("sent_mailbox_not_approved", release.FailureCode);
    }

    [Fact]
    public async Task MaximumPagesBoundsOneInvocationAndLeavesBacklogDue()
    {
        var first = Item("occurrence-page-1", "page-1.sent.json", "cursor-page-1", [], []);
        var second = Item("occurrence-page-2", "page-2.sent.json", "cursor-page-2", [], []);
        var pollStore = new RecordingPollStore(Lease());
        var source = new SequencedSource(
            new ApprovedSentPage([first], first.NextCursor, HasMore: true),
            new ApprovedSentPage([second], second.NextCursor, HasMore: true),
            new ApprovedSentPage([], "must-not-be-read", HasMore: false));
        var useCase = CreateUseCase(
            pollStore,
            source,
            new ResponsePort(),
            new ReportPort());

        var result = await useCase.ExecuteAsync(2, 10, WorkerActor);

        Assert.Equal(2, result.PagesRead);
        Assert.Equal(2, source.CallCount);
        var completion = Assert.Single(pollStore.Completions);
        Assert.True(completion.HasRemainingItems);
        Assert.Equal(second.NextCursor, completion.Cursor);
    }

    [Fact]
    public async Task StaffOperationAttachmentMismatchDoesNotClaimSent()
    {
        var lease = Lease();
        var operationId = Guid.NewGuid();
        var operation = new StaffMailOperation(
            operationId, StaffMailState.Sending, StaffMailAttemptStage.Send, 4,
            NowUtc.AddMinutes(-5), NowUtc.AddMinutes(-2), null, null,
            lease.ApprovedMailboxId, lease.Generation, new string('C', 64), null, null);
        var frozen = new StaffMailAttachment(
            Guid.NewGuid(), Guid.NewGuid(), new string('D', 64), 10,
            "report.pdf", "application/pdf");
        var staffStore = new ObservationStore(new StaffMailExecution(
            Guid.NewGuid().ToString("D"), operation, "draft", [frozen],
            StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1, null));
        var item = Item("staff-mismatch", "sent-items", "cursor", [], []) with
        {
            Provenance = Item("staff-mismatch", "sent-items", "cursor", [], []).Provenance! with
            {
                StaffMailOperationId = operationId,
                AttachmentSha256 = [new string('E', 64)],
                StaffMailMailboxId = lease.ApprovedMailboxId,
                StaffMailMailboxGeneration = lease.Generation,
                StaffMailPayloadHash = new string('F', 64)
            }
        };
        var pollStore = new RecordingPollStore(lease);
        var response = new ResponsePort();
        var report = new ReportPort();
        var poll = CreateUseCase(
            pollStore, new SequencedSource(new ApprovedSentPage([item], "cursor", false)),
            response, report, staffMailStore: staffStore);

        var result = await poll.ExecuteAsync(
            1, 10, ActionActor.SystemWorker("test"), CancellationToken.None);

        Assert.Equal(1, result.ItemsHandled);
        var outcome = Assert.Single(pollStore.OutcomeAttempts);
        Assert.Equal(SentEvidencePollOutcomeKind.Ambiguous, outcome.Kind);
        Assert.Equal("staff_mail_sent_correlation_mismatch", outcome.FailureCode);
        Assert.Equal(0, staffStore.TransitionCount);
    }

    [Fact]
    public async Task RetainedMimeMatchMarksExactStaffOperationSentOnce()
    {
        var lease = Lease();
        var operationId = Guid.NewGuid();
        var hash = new string('D', 64);
        var operation = new StaffMailOperation(
            operationId, StaffMailState.Sending, StaffMailAttemptStage.Send, 4,
            NowUtc.AddMinutes(-5), NowUtc.AddMinutes(-2), null, null,
            lease.ApprovedMailboxId, lease.Generation, new string('C', 64), null, null);
        var staffStore = new ObservationStore(new StaffMailExecution(
            Guid.NewGuid().ToString("D"), operation, "draft",
            [new(Guid.NewGuid(), Guid.NewGuid(), hash, 10, "report.pdf", "application/pdf")],
            StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1, null));
        var template = Item("staff-match", "sent-items", "cursor", [], []);
        var item = template with
        {
            Provenance = template.Provenance! with
            {
                StaffMailOperationId = operationId,
                AttachmentSha256 = [hash],
                StaffMailMailboxId = lease.ApprovedMailboxId,
                StaffMailMailboxGeneration = lease.Generation,
                StaffMailPayloadHash = operation.PayloadHash
            }
        };
        var poll = CreateUseCase(
            new RecordingPollStore(lease),
            new SequencedSource(new ApprovedSentPage([item], "cursor", false)),
            new ResponsePort(), new ReportPort(), staffMailStore: staffStore);

        await poll.ExecuteAsync(1, 10, ActionActor.SystemWorker("test"), CancellationToken.None);

        Assert.Equal(1, staffStore.TransitionCount);
        Assert.Equal(item.Provenance!.ImmutableItemIdentity, staffStore.ImmutableMessageId);
        Assert.Equal(item.Provenance.SentAtUtc, staffStore.ProviderSentAtUtc);
        Assert.Equal(NowUtc, staffStore.ObservedAtUtc);
    }

    [Fact]
    public async Task BatchContinuesToSecondMailboxAfterFirstSourceFailure()
    {
        var first = Lease();
        var second = Lease() with
        {
            ApprovedMailboxId = Guid.NewGuid(),
            MailboxId = "reports",
            MailboxAddress = "reports@example.test",
            LeaseToken = "lease-two"
        };
        var store = new RecordingPollStore(first, second, null);
        var source = new SequencedSource(
            new IOException("first mailbox unavailable"),
            new ApprovedSentPage([], "second-cursor", false));
        var poll = CreateUseCase(
            store, source, new ResponsePort(), new ReportPort(),
            policy: new MultiMailboxPolicy());

        var result = await poll.ExecuteBatchAsync(
            3, 1, 10, ActionActor.SystemWorker("test"), CancellationToken.None);

        Assert.Equal(2, result.MailboxesAttempted);
        Assert.Equal(1, result.MailboxesFailed);
        Assert.Single(store.Releases);
        Assert.Single(store.Completions);
    }

    private static PollSentEvidence CreateUseCase(
        RecordingPollStore pollStore,
        SequencedSource source,
        ResponsePort responsePort,
        ReportPort reportPort,
        TimeProvider? timeProvider = null,
        AutoLinkPort? autoLinkPort = null,
        IApprovedMailboxPolicy? policy = null,
        IStaffMailSendStore? staffMailStore = null) => new(
        pollStore,
        source,
        policy ?? new ApprovedPolicy(),
        responsePort,
        responsePort,
        reportPort,
        autoLinkPort ?? AutoLinkPort.NotLinked(),
        timeProvider ?? new AdjustableTimeProvider(NowUtc),
        staffMailStore);

    private static ApprovedSentPollLease Lease() => new(
        "instructions",
        "instructions@example.test",
        "sent-items",
        Cursor: null,
        "lease-token",
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        1,
        NowUtc.AddHours(-1));

    private static ApprovedSentItem Item(
        string occurrenceIdentity,
        string locationIdentity,
        string nextCursor,
        IReadOnlyList<string> inReplyToIdentities,
        IReadOnlyList<Guid> caseIdentities) => new(
        occurrenceIdentity,
        new string('B', 64),
        locationIdentity,
        ApprovedSentItemObservationKind.Discovered,
        new(
            "instructions",
            "instructions@example.test",
            "sent-items",
            $"immutable-{occurrenceIdentity}",
            $"<message-{occurrenceIdentity}@example.test>",
            $"conversation-{occurrenceIdentity}",
            $"reply-chain-{occurrenceIdentity}",
            inReplyToIdentities,
            caseIdentities,
            NowUtc.AddMinutes(-1),
            new string('A', 64)),
        MalformedReasonCode: null,
        nextCursor);

    private sealed class ApprovedPolicy : IApprovedMailboxPolicy
    {
        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) => Task.FromResult(
            mailboxAddress == "instructions@example.test"
                && routeScope == ApprovedMailboxRouteScope.SentEvidence);
    }

    private sealed class RejectingPolicy : IApprovedMailboxPolicy
    {
        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class RecordingPollStore(params ApprovedSentPollLease?[] claims)
        : ISentEvidencePollStore
    {
        private readonly Queue<ApprovedSentPollLease?> _claims = new(claims);

        public List<SentEvidencePollOutcome> OutcomeAttempts { get; } = [];
        public Dictionary<string, SentEvidencePollOutcome> UniqueOutcomes { get; } =
            new(StringComparer.Ordinal);
        public List<(string Cursor, bool HasRemainingItems)> Completions { get; } = [];
        public List<(DateTimeOffset DueAtUtc, string FailureCode)> Releases { get; } = [];

        public Task<ApprovedSentPollLease?> ClaimAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) => Task.FromResult(
            _claims.Count == 0 ? null : _claims.Dequeue());

        public Task RecordOutcomeAsync(
            string mailboxId,
            string leaseToken,
            SentEvidencePollOutcome outcome,
            CancellationToken cancellationToken)
        {
            OutcomeAttempts.Add(outcome);
            UniqueOutcomes.TryAdd(outcome.OperationKey, outcome);
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            string mailboxId,
            string leaseToken,
            string nextCursor,
            DateTimeOffset completedAtUtc,
            bool hasRemainingItems,
            CancellationToken cancellationToken)
        {
            Completions.Add((nextCursor, hasRemainingItems));
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            string mailboxId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            string failureCode,
            CancellationToken cancellationToken)
        {
            Releases.Add((dueAtUtc, failureCode));
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedSource(params object[] results) : IApprovedSentSource
    {
        private readonly Queue<object> _results = new(results);

        public int CallCount { get; private set; }

        public Task<ApprovedSentPage> ReadAsync(
            ApprovedSentPollLease lease,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var result = _results.Dequeue();
            return result is Exception exception
                ? Task.FromException<ApprovedSentPage>(exception)
                : Task.FromResult((ApprovedSentPage)result);
        }
    }

    private sealed class ResponsePort(params ExactEmailResponseEvidenceCandidate[] candidates)
        : IExactEmailResponseEvidenceQueries, IRecordEmailResponseEvidence
    {
        private ExactEmailResponseEvidenceCandidate[] _candidates = candidates;

        public List<RecordEmailResponseEvidenceRequest> Attempts { get; } = [];
        public Dictionary<string, RecordEmailResponseEvidenceRequest> UniqueOperations { get; } =
            new(StringComparer.Ordinal);
        public Exception? RecordingFailure { get; init; }

        public Task<IReadOnlyList<ExactEmailResponseEvidenceCandidate>> FindExactCandidatesAsync(
            IReadOnlyList<string> replyChainIdentities,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExactEmailResponseEvidenceCandidate>>(
                _candidates
                    .Where(candidate => replyChainIdentities.Contains(
                        candidate.ReplyChainIdentity,
                        StringComparer.Ordinal))
                    .ToArray());

        public Task ExecuteAsync(
            RecordEmailResponseEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Attempts.Add(request);
            if (RecordingFailure is not null)
            {
                return Task.FromException(RecordingFailure);
            }

            if (UniqueOperations.TryAdd(request.OperationKey, request))
            {
                _candidates = _candidates
                    .Select(candidate => candidate.SentEvidenceId == request.SentEvidenceId
                        ? candidate with
                        {
                            ExpectedSentEvidenceVersion = candidate.ExpectedSentEvidenceVersion + 1,
                            RecordedResponseMessageIdentity = request.MessageIdentity
                        }
                        : candidate)
                    .ToArray();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ReportPort : IRetainApprovedMailboxReportSentEvidence
    {
        public List<RetainApprovedMailboxReportSentEvidenceRequest> Requests { get; } = [];
        public List<RetainedApprovedMailboxReportSentEvidence> Retained { get; } = [];

        public Task<RetainedApprovedMailboxReportSentEvidence> ExecuteAsync(
            RetainApprovedMailboxReportSentEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var retained = new RetainedApprovedMailboxReportSentEvidence(
                request.EvidenceId,
                request.MailboxIdentity,
                request.SentFolderIdentity,
                request.ImmutableItemIdentity,
                request.InternetMessageIdentity,
                request.ConversationIdentity,
                request.ReplyChainIdentity,
                request.SourceOccurrenceIdentity,
                request.SourceSha256,
                request.MimeSha256,
                request.SentAtUtc,
                request.DiscoveredAtUtc,
                request.DiscoveredBy);
            Retained.Add(retained);
            return Task.FromResult(retained);
        }
    }

    private sealed class AutoLinkPort(
        AutoLinkReportEvidenceDisposition disposition,
        string? notLinkedReasonCode)
        : IAutoLinkReportEvidence
    {
        public List<AutoLinkReportEvidenceRequest> Requests { get; } = [];

        public static AutoLinkPort Linked() =>
            new(AutoLinkReportEvidenceDisposition.Linked, notLinkedReasonCode: null);

        public static AutoLinkPort NotLinked(
            string reasonCode = "case_not_report_preparation") =>
            new(AutoLinkReportEvidenceDisposition.NotLinked, reasonCode);

        public Task<AutoLinkReportEvidenceResult> ExecuteAsync(
            AutoLinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var link = disposition == AutoLinkReportEvidenceDisposition.Linked
                ? new AutoLinkedReportEvidence(
                    request.CaseId,
                    request.EvidenceId,
                    CaseLifecycleState.PostReport,
                    Version: 1)
                : null;
            return Task.FromResult(new AutoLinkReportEvidenceResult(
                disposition,
                link,
                notLinkedReasonCode));
        }
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtc;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class MultiMailboxPolicy : IApprovedMailboxPolicy
    {
        public Task<bool> IsApprovedAsync(string mailboxAddress, ApprovedMailboxRouteScope routeScope, CancellationToken cancellationToken) =>
            Task.FromResult(routeScope == ApprovedMailboxRouteScope.SentEvidence);
    }

    private sealed class ObservationStore(StaffMailExecution execution) : IStaffMailSendStore
    {
        public int TransitionCount { get; private set; }
        public string? ImmutableMessageId { get; private set; }
        public DateTimeOffset? ProviderSentAtUtc { get; private set; }
        public DateTimeOffset? ObservedAtUtc { get; private set; }
        public Task<StaffMailExecution?> GetExecutionForObservationAsync(ActionActor systemActor, Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult<StaffMailExecution?>(operationId == execution.Operation.Id ? execution : null);
        public Task TransitionObservedSentAsync(ActionActor systemActor, Guid operationId, long expectedVersion, string immutableMessageId, DateTimeOffset providerSentAtUtc, DateTimeOffset observedAtUtc, CancellationToken cancellationToken) { TransitionCount++; ImmutableMessageId = immutableMessageId; ProviderSentAtUtc = providerSentAtUtc; ObservedAtUtc = observedAtUtc; return Task.CompletedTask; }
        public Task<StaffMailOperation> PrepareAsync(StaffMailSendCommand command, string payloadHash, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StaffMailOperation?> GetAsync(string actorSubjectId, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StaffMailOperation?> GetLatestForOriginalAsync(string actorSubjectId, Guid retainedMessageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StaffMailExecution?> GetExecutionAsync(string actorSubjectId, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RequireCurrentStaffAsync(string actorSubjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StaffMailOperation> TransitionAsync(string actorSubjectId, Guid operationId, long expectedVersion, StaffMailState state, StaffMailAttemptStage? stage, string? draftImmutableId, DateTimeOffset? submittedAtUtc, DateTimeOffset? observedSentAtUtc, string? failureCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StaffMailOperation> SetReconciliationContinuationAsync(string actorSubjectId, Guid operationId, long expectedVersion, string? continuation, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
