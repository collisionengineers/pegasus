using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class ProcessIntakeTests
{
    private static readonly DateTimeOffset ProcessedAtUtc = new(2031, 4, 5, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedAtUtc = new(2030, 12, 31, 16, 45, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(IntakeSourceReadStatus.Unsupported, IntakeDecision.Unsupported, "unsupported_test", "The test source is unsupported.")]
    [InlineData(IntakeSourceReadStatus.TechnicalFailure, IntakeDecision.TechnicalFailure, "technical_test", "The test source could not be read.")]
    public async Task UnreadableResultIsPersistedWithItsFailure(
        IntakeSourceReadStatus status,
        IntakeDecision expectedDecision,
        string failureCode,
        string failureReason)
    {
        var readResult = new IntakeSourceReadResult(
            status,
            [],
            [],
            [new("reader_issue", "The reader supplied diagnostic context.", IntakeEvidenceSource.FileName)],
            false,
            failureCode,
            failureReason);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(expectedDecision, draft.Decision);
        Assert.Equal(failureCode, draft.FailureCode);
        Assert.Equal(failureReason, draft.FailureReason);
        Assert.Contains(draft.Evidence, evidence =>
            evidence.Signal == "reader_issue" &&
            evidence.Finding == IntakeEvidenceFinding.Information);
        Assert.Equal(expectedDecision, result.Decision);
        Assert.Equal(failureCode, result.FailureCode);
    }

    [Fact]
    public async Task ReaderExceptionIsSanitisedBeforePersistence()
    {
        const string sensitiveDetail = "storage-account-secret-detail";
        var reader = new StubReader((_, _) => throw new InvalidOperationException(sensitiveDetail));
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(IntakeDecision.TechnicalFailure, draft.Decision);
        Assert.Equal("source_reader_failure", draft.FailureCode);
        Assert.Equal("The uploaded source could not be read because of a technical failure.", draft.FailureReason);
        Assert.DoesNotContain(sensitiveDetail, draft.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveDetail, result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReaderCancellationIsPropagatedWithoutPersistence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new StubReader((_, token) => throw new OperationCanceledException(token));
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(CreateSource(), cancellation.Token));

        Assert.Empty(store.Drafts);
    }

#pragma warning disable CA2201 // These tests verify that runtime-reserved terminal exceptions are never swallowed.
    [Fact]
    public void ExceptionPolicyRejectsTerminalExceptionsAndAcceptsRecoverableExceptions()
    {
        Assert.False(IntakeExceptionPolicy.IsRecoverable(new OperationCanceledException()));
        Assert.False(IntakeExceptionPolicy.IsRecoverable(new OutOfMemoryException()));
        Assert.False(IntakeExceptionPolicy.IsRecoverable(new AccessViolationException()));
        Assert.True(IntakeExceptionPolicy.IsRecoverable(new InvalidOperationException()));
    }

    [Fact]
    public async Task ReaderOutOfMemoryIsPropagatedWithoutPersistence()
    {
        var reader = new StubReader((_, _) => throw new OutOfMemoryException());
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        await Assert.ThrowsAsync<OutOfMemoryException>(() => sut.ExecuteAsync(CreateSource()));

        Assert.Empty(store.Drafts);
    }
#pragma warning restore CA2201

    [Fact]
    public async Task StoreCancellationIsPropagatedWithoutRetry()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new RecordingStore((_, token) => throw new OperationCanceledException(token));
        var sut = CreateSut(new StubReader(Readable()), store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(CreateSource(), cancellation.Token));

        Assert.Single(store.Drafts);
    }

    [Fact]
    public async Task ArtifactFailureIsRetryableWithSameTokenAndCreatesNoReceipt()
    {
        var artifactStore = new RecordingArtifactStore(failuresBeforeSuccess: 1);
        var reader = new StubReader(Readable());
        var store = new RecordingStore();
        var sut = CreateSut(reader, store, artifactStore);
        var source = CreateSource();

        await Assert.ThrowsAsync<IntakeArtifactRetentionException>(() => sut.ExecuteAsync(source));

        Assert.Empty(reader.Sources);
        Assert.Empty(store.Drafts);

        var retried = await sut.ExecuteAsync(source);

        Assert.Equal(source.SourceIdentity, retried.SourceIdentity);
        Assert.Single(store.Drafts);
        Assert.Equal(2, artifactStore.StoredHashes.Count);
        Assert.Equal(artifactStore.StoredHashes[0], artifactStore.StoredHashes[1]);
    }

    [Fact]
    public async Task PostArtifactPersistenceFailureLeavesReusableBytesForRetry()
    {
        var attempts = 0;
        var store = new RecordingStore((draft, _) =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<IntakeReceipt>(new InvalidOperationException("controlled database failure"))
                : Task.FromResult(RecordingStore.RecordFrom(draft));
        });
        var artifactStore = new RecordingArtifactStore();
        var sut = CreateSut(new StubReader(Readable()), store, artifactStore);
        var source = CreateSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(source));
        var retried = await sut.ExecuteAsync(source);

        Assert.Equal(source.SourceIdentity, retried.SourceIdentity);
        Assert.Equal(2, store.Drafts.Count);
        Assert.Equal(2, artifactStore.StoredHashes.Count);
        Assert.Equal(artifactStore.StoredHashes[0], artifactStore.StoredHashes[1]);
    }

    [Fact]
    public async Task IncompleteReaderResultOverridesConfirmingQdosContent()
    {
        var readResult = Readable(content:
        [
            new(
                IntakeEvidenceSource.DocumentContent,
                "controlled incomplete content",
                "QDOS instruction\nClaimant Name: A\nClaim Number: B")
        ]) with { IsIncomplete = true };
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
        Assert.Null(result.InstructionDraft);
        Assert.Null(result.ExtractionPolicyKey);
    }

    [Fact]
    public async Task IncompleteReaderResultRetainsCustodyWithoutConsultingExtractionPolicy()
    {
        var derivedAsset = new IntakeAssetCandidate(
            "embedded vehicle image",
            "vehicle.jpg",
            "image/jpeg",
            new byte[] { 0x02, 0x03 },
            IntakeAssetKind.EmbeddedImage,
            IntakeAssetDisposition.Embedded,
            PageNumber: 1);
        var readResult = Readable(content:
        [
            new(
                IntakeEvidenceSource.DocumentContent,
                "controlled incomplete content",
                "QDOS instruction\nClaimant Name: A\nClaim Number: B")
        ]) with
        {
            Assets = [derivedAsset],
            IsIncomplete = true
        };
        var store = new RecordingStore();
        var artifactStore = new RecordingArtifactStore();
        var sut = CreateSut(
            new StubReader(readResult),
            store,
            artifactStore,
            new ThrowingPolicy());

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        var assets = Assert.IsAssignableFrom<IReadOnlyList<IntakeAssetRecord>>(draft.Assets);
        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
        Assert.Equal(IntakeDecision.NeedsSorting, draft.Decision);
        Assert.Null(draft.InstructionDraft);
        Assert.Null(draft.ExtractionPolicyKey);
        Assert.Empty(draft.Evidence);
        Assert.Collection(
            assets,
            source =>
            {
                Assert.Equal(IntakeAssetKind.Source, source.Kind);
                Assert.Equal(IntakeAssetDisposition.Source, source.Disposition);
            },
            derived =>
            {
                Assert.Equal("embedded vehicle image", derived.SourceLabel);
                Assert.Equal("vehicle.jpg", derived.FileName);
                Assert.Equal(IntakeAssetKind.EmbeddedImage, derived.Kind);
                Assert.Equal(IntakeAssetDisposition.Embedded, derived.Disposition);
            });
        Assert.Equal(2, artifactStore.StoredHashes.Count);
    }

    [Fact]
    public async Task MatchingReceiptReplayDoesNotReadOrRetainSourceAgain()
    {
        var reader = new StubReader(Readable());
        var store = new RecordingStore();
        var artifactStore = new RecordingArtifactStore();
        var sut = CreateSut(reader, store, artifactStore);
        var source = CreateSource();

        var first = await sut.ExecuteAsync(source);
        store.ExistingRecord = first;

        var replay = await sut.ExecuteAsync(source);

        Assert.Equal(first.Id, replay.Id);
        Assert.True(replay.IsDuplicate);
        Assert.Single(reader.Sources);
        Assert.Single(store.Drafts);
        Assert.Single(artifactStore.StoredHashes);
    }

    [Fact]
    public async Task OcrRequirementWithoutConfirmingContentIsPersistedForReview()
    {
        var readResult = Readable(requiresOcr: true);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(IntakeDecision.CaseCreated, draft.Decision);
        Assert.Null(draft.FailureCode);
        Assert.NotEmpty(draft.Fields);
        Assert.NotEmpty(draft.MissingFields);
        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        Assert.Contains(result.Evidence, item => item.Signal == "additional-scanned-content");
    }

    [Fact]
    public async Task ApplicableQdosContentRemainsDraftReadyWhenAdditionalPagesRequireOcr()
    {
        var content = new IntakeContentFragment(
            IntakeEvidenceSource.DocumentContent,
            "controlled readable page",
            "QDOS instruction\nClaimant Name: Review Claimant\nClaim Number: Q-2");
        var sut = CreateSut(
            new StubReader(Readable(requiresOcr: true, content: [content])),
            new RecordingStore());

        var result = await sut.ExecuteAsync(CreateSource());

        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        Assert.Equal("QDOS", Assert.IsType<InstructionDraft>(result.InstructionDraft).SuggestedPrincipalCode);
        Assert.Contains(result.Evidence, item => item.Signal == "additional-scanned-content");
    }

    [Fact]
    public async Task MailboxStaffForwardUsesOriginalSenderAndPersistsCompleteRouteDecision()
    {
        var readResult = Readable(
            transportEvidence:
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "staff@collisionengineers.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message"),
                new(
                    IntakeEvidenceSource.Sender,
                    "instructions@qdosassist.co.uk",
                    IntakeSenderIdentityKind.AttachedOriginal,
                    "attached original")
            ],
            content:
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "attached original body",
                    "QDOS instruction\nClaimant Name: Review Claimant\nClaim Number: Q-ROUTE")
            ]);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);
        var source = CreateSource() with
        {
            FileName = "mailbox-message.eml",
            MediaType = "message/rfc822",
            SourceIdentity = new(IntakeSourceChannel.Mailbox, "mailbox-route-accepted")
        };

        var result = await sut.ExecuteAsync(source);

        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        var route = Assert.IsType<MailRouteEvaluationResult>(result.MailRouteDecision);
        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
        Assert.Equal("staff@collisionengineers.co.uk", Assert.Single(route.TransportIdentities).Address);
        Assert.Equal("instructions@qdosassist.co.uk", Assert.Single(route.OriginalIdentities).Address);
        Assert.Equal("instructions@qdosassist.co.uk", route.EffectiveSender?.Address);
        Assert.NotEmpty(route.Predicates);
        Assert.Same(route, Assert.Single(store.Drafts).MailRouteDecision);
    }

    [Theory]
    [InlineData("qdosassist.co.uk")]
    [InlineData("qdosassists.co.uk")]
    [InlineData("qdoslaw.co.uk")]
    public async Task AcceptedDirectSenderEstablishesQdosWithoutContentMarker(string domain)
    {
        var readResult = Readable(
            transportEvidence:
            [
                new(
                    IntakeEvidenceSource.Sender,
                    $"instructions@{domain}",
                    IntakeSenderIdentityKind.Transport,
                    "outer message")
            ],
            content:
            [
                new(IntakeEvidenceSource.EmailBody, "message body", "Please process the attachment."),
                new(IntakeEvidenceSource.DocumentContent, "attachment", "Claimant Name: Direct Claimant"),
                new(IntakeEvidenceSource.DocumentContent, "attachment", "Claim Number: DIRECT-1")
            ]);

        var result = await CreateSut(new StubReader(readResult), new RecordingStore())
            .ExecuteAsync(CreateSource());

        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        Assert.Equal("QDOS", Assert.IsType<InstructionDraft>(result.InstructionDraft).SuggestedPrincipalCode);
        Assert.DoesNotContain(result.Evidence, item =>
            item.Signal is "qdos-content-marker" or "qdos-transport-marker" or "instruction-structure");
    }

    [Fact]
    public async Task StaffForwardPriorSenderEstablishesQdosWithoutContentMarker()
    {
        var readResult = Readable(
            transportEvidence:
            [
                new(IntakeEvidenceSource.Sender, "staff@collisionengineers.co.uk", IntakeSenderIdentityKind.Transport, "outer message"),
                new(IntakeEvidenceSource.Sender, "prior@qdoslaw.co.uk", IntakeSenderIdentityKind.InlineForwardedOriginal, "prior message")
            ],
            content:
            [
                new(IntakeEvidenceSource.EmailBody, "forward body", "Please process the attached instruction."),
                new(IntakeEvidenceSource.DocumentContent, "attachment one", "Claimant Name: Forwarded Claimant"),
                new(IntakeEvidenceSource.DocumentContent, "attachment two", "Claim Number: FORWARD-1")
            ]);

        var result = await CreateSut(new StubReader(readResult), new RecordingStore())
            .ExecuteAsync(CreateSource());

        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        Assert.Equal("prior@qdoslaw.co.uk", result.MailRouteDecision?.EffectiveSender?.Address);
        Assert.Equal("QDOS", Assert.IsType<InstructionDraft>(result.InstructionDraft).SuggestedPrincipalCode);
    }

    [Theory]
    [InlineData(IntakeSourceChannel.ManualUpload)]
    [InlineData(IntakeSourceChannel.Automation)]
    public async Task ContentOnlyQdosCannotEstablishPrincipalOnNonMailboxChannel(
        IntakeSourceChannel channel)
    {
        var readResult = Readable(
            transportEvidence: [],
            content:
            [
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "untrusted content",
                    "QDOS instruction\nClaimant Name: Content Claimant\nClaim Number: CONTENT-1")
            ]);
        var source = CreateSource() with
        {
            SourceIdentity = new(channel, $"content-only-{channel}")
        };

        var result = await CreateSut(new StubReader(readResult), new RecordingStore())
            .ExecuteAsync(source);

        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
        Assert.Null(result.InstructionDraft);
        Assert.Null(result.MailRouteDecision);
    }

    [Fact]
    public async Task ContentOnlyQdosCannotOverrideNonMatchingMailboxSender()
    {
        var readResult = Readable(
            transportEvidence:
            [
                new(IntakeEvidenceSource.Sender, "sender@example.invalid", IntakeSenderIdentityKind.Transport, "outer message")
            ],
            content:
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "QDOS instruction\nClaimant Name: Content Claimant\nClaim Number: CONTENT-2")
            ]);

        var result = await CreateSut(new StubReader(readResult), new RecordingStore())
            .ExecuteAsync(CreateSource());

        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
        Assert.Null(result.InstructionDraft);
        Assert.Equal(MailRouteDisposition.NoMatch, result.MailRouteDecision?.Disposition);
    }

    [Fact]
    public async Task ExtractionDraftPrincipalMustMatchAcceptedRoute()
    {
        var store = new RecordingStore();
        var policy = new StubPolicy(new(
            InstructionPolicyApplicability.Applicable,
            [],
            [],
            new("OTHER", null, null, null, null, null, null, null, null, null, null),
            [],
            "adversarial_policy",
            1));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(new StubReader(Readable()), store, extractionPolicy: policy)
                .ExecuteAsync(CreateSource()));

        Assert.Contains("does not match", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Drafts);
    }

    [Fact]
    public async Task AuditWithSeparateOriginalReportRecordsLiteralOutcomeBeforeAllocation()
    {
        const string instructionLabel = "message, attachment 1: audit-instructions.pdf";
        const string reportLabel = "message, attachment 2: original-report.pdf";
        var automaticEvidence = new RecordingAutomaticAuditEvidence();
        var readResult = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.DocumentContent,
                    instructionLabel,
                    "AUDIT REPORT NOTIFICATION\nQDOS instruction\nClaimant Name: Review Claimant\nClaim Number: Q-AUDIT"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    $"{reportLabel}, page 1",
                    "The vehicle is repairable.")
            ],
            [new(IntakeEvidenceSource.Sender, "instructions@qdosassist.co.uk", IntakeSenderIdentityKind.Transport, "outer message")],
            [],
            false,
            Assets:
            [
                new(instructionLabel, "audit-instructions.pdf", "application/pdf", new byte[] { 1 }, IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment),
                new(reportLabel, "original-report.pdf", "application/pdf", new byte[] { 2 }, IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment)
            ]);
        var store = new RecordingStore();
        var sut = CreateSut(
            new StubReader(readResult),
            store,
            automaticStandaloneAuditEvidence: automaticEvidence);

        var source = CreateSource() with
        {
            FileName = "audit.eml",
            MediaType = "message/rfc822",
            SourceIdentity = new(IntakeSourceChannel.Mailbox, "audit-with-original-report")
        };
        var result = await sut.ExecuteAsync(source);

        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        var recorded = Assert.Single(automaticEvidence.Requests);
        Assert.Equal(AuditAssessment.Repairable, recorded.Assessment);
        Assert.Equal(Assert.Single(Assert.Single(store.Drafts).Assets!, asset => asset.SourceLabel == reportLabel).Id, recorded.OriginalReportAssetId);

        // A transient evidence-store failure after receipt persistence is
        // retried from the existing durable receipt, without rereading email.
        store.ExistingRecord = result;
        var replay = await sut.ExecuteAsync(source);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(2, automaticEvidence.Requests.Count);
    }

    [Fact]
    public async Task AmbiguousCaseMatchForcesNeedsSortingOnAnOtherwiseDraftReadyMessage()
    {
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();
        var readResult = Readable(
            transportEvidence:
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "instructions@qdosassist.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message")
            ],
            content:
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "QDOS instruction\nClaimant Name: Review Claimant\nClaim Number: 12345/1")
            ]);
        var store = new RecordingStore();
        var evaluator = new EvaluateIntakeCaseMatch(
            [new FixedKeysMatchPolicy(new("12345/1", "AB12CDE", null, null, null))],
            new FixedCandidatesQueries(
            [
                new(caseA, "QDOS", "12345/1", null, null, null, null,
                    Pegasus.Core.Workflow.CaseLifecycleState.Review, null),
                new(caseB, "QDOS", null, "AB12CDE", null, null, null,
                    Pegasus.Core.Workflow.CaseLifecycleState.Review, null)
            ]));
        var sut = CreateSut(new StubReader(readResult), store, caseMatchEvaluator: evaluator);
        var source = CreateSource() with
        {
            FileName = "ambiguous-match.eml",
            MediaType = "message/rfc822",
            SourceIdentity = new(IntakeSourceChannel.Mailbox, "mailbox-ambiguous-match")
        };

        var result = await sut.ExecuteAsync(source);

        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
        var match = Assert.IsType<CaseMatchEvaluationResult>(result.CaseMatchDecision);
        Assert.Equal(CaseMatchOutcome.Ambiguous, match.Outcome);
        Assert.Null(match.MatchedCaseId);
        Assert.Equal(2, match.Candidates.Count);
        Assert.Contains("Competing candidate cases", result.DecisionReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassificationIsRecordedOnlyAndNeverChangesTheIntakeDecision()
    {
        // The same message processed with and without the classification
        // policy must land on the identical decision: a classification is a
        // recorded observation, never a queue, Triage, or destination change.
        IntakeSourceReadResult ReadResult() => Readable(
            transportEvidence:
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "instructions@qdosassist.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message")
            ],
            content:
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Triage Only Request. Please find attached our client's images.")
            ]);
        IntakeSource Source(string key) => CreateSource() with
        {
            FileName = "classification-separation.eml",
            MediaType = "message/rfc822",
            SourceIdentity = new(IntakeSourceChannel.Mailbox, key)
        };

        var classifiedStore = new RecordingStore();
        var classified = await CreateSut(new StubReader(ReadResult()), classifiedStore)
            .ExecuteAsync(Source("classification-separation-on"));
        var unclassifiedStore = new RecordingStore();
        var unclassified = await CreateSut(
                new StubReader(ReadResult()),
                unclassifiedStore,
                classificationPolicies: [])
            .ExecuteAsync(Source("classification-separation-off"));

        var recorded = Assert.IsType<MailClassificationResult>(classified.MailClassificationDecision);
        Assert.Equal(MailClassificationOutcome.Classified, recorded.Outcome);
        Assert.Null(unclassified.MailClassificationDecision);
        Assert.Equal(unclassified.Decision, classified.Decision);
        Assert.Equal(unclassified.DecisionReason, classified.DecisionReason);
    }

    [Fact]
    public async Task AmbiguousClassificationIsRecordedOnlyAndNeverChangesTheIntakeDecision()
    {
        IntakeSourceReadResult ReadResult() => Readable(
            transportEvidence:
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "instructions@qdosassist.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message")
            ],
            content:
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Triage Only Request. Please provide an initial assessment."),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "attached letter",
                    "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1")
            ]);
        IntakeSource Source(string key) => CreateSource() with
        {
            FileName = "classification-ambiguity-separation.eml",
            MediaType = "message/rfc822",
            SourceIdentity = new(IntakeSourceChannel.Mailbox, key)
        };

        var ambiguousStore = new RecordingStore();
        var ambiguous = await CreateSut(new StubReader(ReadResult()), ambiguousStore)
            .ExecuteAsync(Source("classification-ambiguity-on"));
        var withoutStore = new RecordingStore();
        var without = await CreateSut(
                new StubReader(ReadResult()),
                withoutStore,
                classificationPolicies: [])
            .ExecuteAsync(Source("classification-ambiguity-off"));

        var recorded = Assert.IsType<MailClassificationResult>(ambiguous.MailClassificationDecision);
        Assert.Equal(MailClassificationOutcome.Ambiguous, recorded.Outcome);
        Assert.Null(without.MailClassificationDecision);
        Assert.Equal(without.Decision, ambiguous.Decision);
        Assert.Equal(without.DecisionReason, ambiguous.DecisionReason);
    }

    private sealed class FixedKeysMatchPolicy(CaseMatchKeys keys) : IProviderCaseMatchPolicy
    {
        public string WorkProviderCode => "QDOS";
        public string PolicyKey => "qdos_case_match";
        public int PolicyVersion => 1;
        public CaseMatchKeys ExtractMatchKeys(IntakeSourceReadResult readResult) => keys;
        public CaseMatchIndexKeys DeriveIndexKeys(CaseMatchSourceData caseData) =>
            new(null, null, null, null, null);
    }

    private sealed class FixedCandidatesQueries(IReadOnlyList<CaseMatchCandidate> candidates)
        : ICaseMatchCandidateQueries
    {
        public Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
            string workProviderCode,
            CaseMatchKeys keys,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);

        public Task<CaseMatchCandidate?> FindByCaseIdAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates.FirstOrDefault(item => item.CaseId == caseId));
    }

    [Fact]
    public async Task MailboxStaffForwardWithAmbiguousOriginalsFailsBeforeInstructionExtraction()
    {
        var readResult = Readable(
            transportEvidence:
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "staff@collisionengineers.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message"),
                new(
                    IntakeEvidenceSource.Sender,
                    "first@qdosassist.co.uk",
                    IntakeSenderIdentityKind.AttachedOriginal,
                    "attached original one"),
                new(
                    IntakeEvidenceSource.Sender,
                    "second@qdosassist.co.uk",
                    IntakeSenderIdentityKind.AttachedOriginal,
                    "attached original two")
            ],
            content:
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "attached original body",
                    "QDOS instruction\nClaimant Name: Review Claimant\nClaim Number: Q-AMBIGUOUS")
            ]);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);
        var source = CreateSource() with
        {
            FileName = "ambiguous-mailbox-message.eml",
            MediaType = "message/rfc822",
            SourceIdentity = new(IntakeSourceChannel.Mailbox, "mailbox-route-ambiguous")
        };

        var result = await sut.ExecuteAsync(source);

        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
        Assert.Null(result.InstructionDraft);
        var route = Assert.IsType<MailRouteEvaluationResult>(result.MailRouteDecision);
        Assert.Equal(MailRouteDisposition.NeedsSorting, route.Disposition);
        Assert.Null(route.SelectedRoute);
        Assert.Equal(2, route.OriginalIdentities.Count);
        var draft = Assert.Single(store.Drafts);
        Assert.Null(draft.ExtractionPolicyKey);
        Assert.Same(route, draft.MailRouteDecision);
    }

    [Fact]
    public async Task ApplicablePolicyResultWithoutDraftFailsBeforePersistence()
    {
        var store = new RecordingStore();
        var policy = new StubPolicy(new(
            InstructionPolicyApplicability.Applicable,
            [],
            [new("Claimant name", "Review Claimant", [], false, false)],
            null,
            ["Claim number"],
            "adversarial_policy",
            1));
        var sut = CreateSut(new StubReader(Readable()), store, extractionPolicy: policy);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(CreateSource()));

        Assert.Contains("Applicable", error.Message, StringComparison.Ordinal);
        Assert.Contains("draft", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Drafts);
    }

    [Theory]
    [InlineData(InstructionPolicyApplicability.NotApplicable)]
    [InlineData(InstructionPolicyApplicability.Indeterminate)]
    public async Task NonApplicablePolicyResultWithDraftFailsBeforePersistence(
        InstructionPolicyApplicability applicability)
    {
        var store = new RecordingStore();
        var policy = new StubPolicy(new(
            applicability,
            [],
            [new("Claimant name", "Review Claimant", [], false, false)],
            new("SHOULD_NOT_PERSIST", null, null, null, null, null, null, null, null, null, null),
            ["Claim number"],
            "adversarial_policy",
            1));
        var sut = CreateSut(new StubReader(Readable()), store, extractionPolicy: policy);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(CreateSource()));

        Assert.Contains(applicability.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("draft", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Drafts);
    }

    [Fact]
    public async Task WeakTransportMarkerAloneDoesNotConfirmInstructionContent()
    {
        var readResult = Readable(
            transportEvidence: [new(IntakeEvidenceSource.FileName, "QDOS-upload.pdf")]);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(readResult), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        Assert.Equal(IntakeDecision.NeedsSorting, draft.Decision);
        Assert.Empty(draft.Evidence);
        Assert.Equal(IntakeDecision.NeedsSorting, result.Decision);
    }

    [Fact]
    public async Task PersistenceDraftUsesSafeBasenameHashClockActorAndSourceIdentity()
    {
        byte[] content = [0x10, 0x20, 0x30, 0x40];
        var source = new IntakeSource(
            Path.Combine("untrusted", "nested", "selected.pdf"),
            "application/pdf",
            content,
            ReceivedAtUtc,
            "operator-123",
            new(IntakeSourceChannel.ManualUpload, "11111111111111111111111111111111"));
        var reader = new StubReader(Readable());
        var store = new RecordingStore();
        var sut = CreateSut(reader, store);

        await sut.ExecuteAsync(source);

        Assert.Equal("selected.pdf", Assert.Single(reader.Sources).FileName);
        var draft = Assert.Single(store.Drafts);
        Assert.Equal("selected.pdf", draft.SourceFileName);
        Assert.Equal("application/pdf", draft.MediaType);
        Assert.Equal(content.Length, draft.SourceLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)), draft.SourceHash);
        Assert.Equal(ReceivedAtUtc, draft.ReceivedAtUtc);
        Assert.Equal(ProcessedAtUtc, draft.ProcessedAtUtc);
        Assert.Equal("operator-123", draft.Actor);
        Assert.Equal(source.SourceIdentity, draft.SourceIdentity);
    }

    [Fact]
    public async Task ConfirmedContentCreatesTypedReviewDraftWithoutCaseSemantics()
    {
        var content = new IntakeContentFragment(
            IntakeEvidenceSource.DocumentContent,
            "controlled protocol fixture",
            """
            QDOS instruction
            Claimant Name: Review Claimant
            Claim Number: PROTOCOL-001
            Vehicle Registration: AB12 CDE
            Vehicle Make: Example Make
            Vehicle Model: Example Model
            Vehicle Mileage: 12,345 miles
            Accident Circumstances: Controlled fixture circumstances
            Date of Incident: 04/03/2031
            Instruction Date: 05/03/2031
            Inspection Address: Image Based Assessment
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var draft = Assert.Single(store.Drafts);
        var typed = Assert.IsType<InstructionDraft>(draft.InstructionDraft);
        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        Assert.Equal("QDOS", typed.SuggestedPrincipalCode);
        Assert.Equal("Review Claimant", typed.ClaimantName);
        Assert.Equal("PROTOCOL-001", typed.ClaimNumber);
        Assert.Equal("AB12CDE", typed.VehicleRegistration);
        Assert.Equal("Example Make", typed.VehicleMake);
        Assert.Equal("Example Model", typed.VehicleModel);
        Assert.Equal(12345L, typed.VehicleMileage);
        Assert.Equal("Controlled fixture circumstances", typed.AccidentCircumstances);
        Assert.Equal(new DateOnly(2031, 3, 4), typed.DateOfIncident);
        Assert.Equal(new DateOnly(2031, 3, 5), typed.InstructionDate);
        Assert.Equal("Image Based Assessment", typed.InspectionAddress);
        Assert.Equal(
            "AB12 CDE",
            Assert.Single(draft.Fields, field => field.Name == "Vehicle registration").SuggestedValue);
        Assert.Equal(
            "12,345 miles",
            Assert.Single(draft.Fields, field => field.Name == "Vehicle mileage").SuggestedValue);
        Assert.Equal(
            "04/03/2031",
            Assert.Single(draft.Fields, field => field.Name == "Date of incident").SuggestedValue);
    }

    [Theory]
    [InlineData("Claim Number | PROTOCOL-BLANK-001")]
    [InlineData("Claim Number PROTOCOL-BLANK-001")]
    public async Task BlankFieldDoesNotConsumeTheNextFieldLabel(string claimNumberLine)
    {
        var content = new IntakeContentFragment(
            IntakeEvidenceSource.DocumentContent,
            "controlled blank-field fixture",
            $$"""
            QDOS instruction
            Claimant Name:
            {{claimNumberLine}}
            Vehicle Registration: AB12 CDE
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        Assert.Equal(IntakeDecision.CaseCreated, result.Decision);
        Assert.Contains("Claimant name", result.MissingFields);
        var claimantName = Assert.Single(result.Fields, field => field.Name == "Claimant name");
        Assert.Null(claimantName.SuggestedValue);
        Assert.Empty(claimantName.Candidates);
        Assert.False(claimantName.HasConflict);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).ClaimantName);
        Assert.Equal(
            "PROTOCOL-BLANK-001",
            Assert.Single(result.Fields, field => field.Name == "Claim number").SuggestedValue);
    }

    [Fact]
    public async Task FieldValueMayRemainOnTheNextLine()
    {
        var content = new IntakeContentFragment(
            IntakeEvidenceSource.DocumentContent,
            "controlled next-line fixture",
            """
            QDOS instruction
            Claimant Name:
            Review Claimant
            Claim Number: PROTOCOL-NEXT-LINE-001
            Vehicle Registration: AB12 CDE
            """);
        var sut = CreateSut(new StubReader(Readable(content: [content])), new RecordingStore());

        var result = await sut.ExecuteAsync(CreateSource());

        Assert.Equal("Review Claimant", Assert.IsType<InstructionDraft>(result.InstructionDraft).ClaimantName);
        Assert.DoesNotContain("Claimant name", result.MissingFields);
    }

    [Fact]
    public async Task InvalidAndConflictingTypedValuesRetainCandidatesWithNullTypedValues()
    {
        var content = new IntakeContentFragment(
            IntakeEvidenceSource.EmailBody,
            "controlled email body",
            """
            QDOS instruction
            Claim Number: PROTOCOL-INVALID
            Vehicle Registration: AB12 CDE
            Vehicle Mileage: unknown pending review
            Date of Incident: 04/03/2031
            Date of Incident: 05/03/2031
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var typed = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(typed.VehicleMileage);
        Assert.Null(typed.DateOfIncident);
        var mileage = Assert.Single(result.Fields, field => field.Name == "Vehicle mileage");
        Assert.Equal("unknown pending review", mileage.SuggestedValue);
        Assert.Single(mileage.Candidates);
        Assert.Equal("controlled email body", mileage.Candidates[0].SourceLabel);
        var incidentDate = Assert.Single(result.Fields, field => field.Name == "Date of incident");
        Assert.True(incidentDate.HasConflict);
        Assert.Null(incidentDate.SuggestedValue);
        Assert.Equal(2, incidentDate.Candidates.Count);
    }

    [Fact]
    public async Task OverlongStringsAndInvalidRegistrationRemainFullCandidatesButTypedValuesAreNull()
    {
        var claimant = new string('C', 301);
        var claimNumber = new string('N', 101);
        var make = new string('K', 101);
        var model = new string('M', 101);
        var circumstances = new string('A', 2001);
        var address = new string('I', 1001);
        const string registration = "INVALID!* REGISTRATION";
        var content = new IntakeContentFragment(
            IntakeEvidenceSource.DocumentContent,
            "controlled overlong document",
            $"""
            QDOS instruction
            Claimant Name: {claimant}
            Claim Number: {claimNumber}
            Vehicle Registration: {registration}
            Vehicle Make: {make}
            Vehicle Model: {model}
            Accident Circumstances: {circumstances}
            Inspection Address: {address}
            """);
        var store = new RecordingStore();
        var sut = CreateSut(new StubReader(Readable(content: [content])), store);

        var result = await sut.ExecuteAsync(CreateSource());

        var typed = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(typed.ClaimantName);
        Assert.Null(typed.ClaimNumber);
        Assert.Null(typed.VehicleRegistration);
        Assert.Null(typed.VehicleMake);
        Assert.Null(typed.VehicleModel);
        Assert.Null(typed.AccidentCircumstances);
        Assert.Null(typed.InspectionAddress);
        foreach (var (fieldName, expectedValue) in new[]
                 {
                     ("Claimant name", claimant),
                     ("Claim number", claimNumber),
                     ("Vehicle registration", registration),
                     ("Vehicle make", make),
                     ("Vehicle model", model),
                     ("Accident circumstances", circumstances),
                     ("Inspection address", address)
                 })
        {
            var field = Assert.Single(result.Fields, item => item.Name == fieldName);
            Assert.Equal(expectedValue, field.SuggestedValue);
            var candidate = Assert.Single(field.Candidates);
            Assert.Equal(expectedValue, candidate.Value);
            Assert.Equal(IntakeEvidenceSource.DocumentContent, candidate.Source);
            Assert.Equal("controlled overlong document", candidate.SourceLabel);
        }
    }

    private static ProcessIntake CreateSut(
        IIntakeSourceReader reader,
        IIntakeReceiptStore store,
        IIntakeArtifactStore? artifactStore = null,
        IInstructionExtractionPolicy? extractionPolicy = null,
        IMailRoutePolicy? mailRoutePolicy = null,
        EvaluateIntakeCaseMatch? caseMatchEvaluator = null,
        IReadOnlyList<IMailClassificationPolicy>? classificationPolicies = null,
        IRecordAutomaticStandaloneAuditEvidence? automaticStandaloneAuditEvidence = null) =>
        new(reader, store, artifactStore ?? new RecordingArtifactStore(),
            extractionPolicy ?? new QdosInstructionExtractionPolicy(),
            mailRoutePolicy ?? new QdosMailRoutePolicy(),
            classificationPolicies ?? [new QdosMailClassificationPolicy()],
            caseMatchEvaluator ?? new EvaluateIntakeCaseMatch([], new NoCaseMatchCandidates()),
            new FixedTimeProvider(ProcessedAtUtc),
            automaticStandaloneAuditEvidence);

    private sealed class NoCaseMatchCandidates : ICaseMatchCandidateQueries
    {
        public Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
            string workProviderCode,
            CaseMatchKeys keys,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseMatchCandidate>>([]);

        public Task<CaseMatchCandidate?> FindByCaseIdAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseMatchCandidate?>(null);
    }

    private static IntakeSource CreateSource() =>
        new(
            "selected.pdf",
            "application/pdf",
            new byte[] { 0x01 },
            ReceivedAtUtc,
            "operator",
            new(IntakeSourceChannel.Mailbox, "22222222222222222222222222222222"));

    private static IntakeSourceReadResult Readable(
        bool requiresOcr = false,
        IReadOnlyList<IntakeTransportEvidence>? transportEvidence = null,
        IReadOnlyList<IntakeContentFragment>? content = null) =>
        new(
            IntakeSourceReadStatus.Readable,
            content ?? [],
            transportEvidence ??
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "instructions@qdosassist.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message")
            ],
            [],
            requiresOcr);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubReader : IIntakeSourceReader
    {
        private readonly Func<IntakeSource, CancellationToken, Task<IntakeSourceReadResult>> read;

        public StubReader(IntakeSourceReadResult result)
            : this((_, _) => Task.FromResult(result))
        {
        }

        public StubReader(Func<IntakeSource, CancellationToken, Task<IntakeSourceReadResult>> read)
        {
            this.read = read;
        }

        public List<IntakeSource> Sources { get; } = [];

        public Task<IntakeSourceReadResult> ReadAsync(
            IntakeSource source,
            CancellationToken cancellationToken)
        {
            Sources.Add(source);
            return read(source, cancellationToken);
        }
    }

    private sealed class StubPolicy(InstructionExtractionResult result) : IInstructionExtractionPolicy
    {
        public string PrincipalCode => "QDOS";

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext) => result;
    }

    private sealed class ThrowingPolicy : IInstructionExtractionPolicy
    {
        public string PrincipalCode => "QDOS";

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext) =>
            throw new InvalidOperationException(
                "The extraction policy must not run for an incomplete reader result.");
    }

    private sealed class RecordingStore : IIntakeReceiptStore
    {
        private readonly Func<IntakeReceiptDraft, CancellationToken, Task<IntakeReceipt>> store;

        public RecordingStore()
            : this((draft, _) => Task.FromResult(RecordFrom(draft)))
        {
        }

        public RecordingStore(Func<IntakeReceiptDraft, CancellationToken, Task<IntakeReceipt>> store)
        {
            this.store = store;
        }

        public List<IntakeReceiptDraft> Drafts { get; } = [];

        public IntakeReceipt? ExistingRecord { get; set; }

        public Task<IntakeReceipt?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                ExistingRecord?.SourceIdentity == sourceIdentity
                    ? ExistingRecord
                    : null);

        public Task<IntakeReceipt> StoreAsync(IntakeReceiptDraft draft, CancellationToken cancellationToken)
        {
            Drafts.Add(draft);
            return store(draft, cancellationToken);
        }
        public Task<IntakeReceipt> ReplaceEvaluationAsync(
            IntakeReceiptDraft draft,
            CancellationToken cancellationToken)
        {
            Drafts.Add(draft);
            return store(draft, cancellationToken);
        }

        public static IntakeReceipt RecordFrom(IntakeReceiptDraft draft) =>
            new(
                new Guid("eb239fbc-cfd4-46c9-87dd-c784404ff3f6"),
                draft.SourceFileName,
                draft.MediaType,
                draft.SourceLength,
                draft.SourceHash,
                draft.SourceIdentity,
                draft.ReceivedAtUtc,
                draft.ProcessedAtUtc,
                draft.Decision,
                draft.DecisionReason,
                draft.Evidence,
                draft.Fields,
                draft.InstructionDraft,
                draft.MissingFields,
                draft.FailureCode,
                draft.FailureReason,
                false,
                draft.SourceReaderKey,
                draft.SourceReaderVersion,
                draft.ExtractionPolicyKey,
                draft.ExtractionPolicyVersion,
                Assets: draft.Assets,
                MailRouteDecision: draft.MailRouteDecision,
                MailClassificationDecision: draft.MailClassificationDecision,
                CaseMatchDecision: draft.CaseMatchDecision);
    }

    private sealed class RecordingArtifactStore(int failuresBeforeSuccess = 0) : IIntakeArtifactStore
    {
        private int remainingFailures = failuresBeforeSuccess;

        public List<string> StoredHashes { get; } = [];

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            StoredHashes.Add(contentHash);
            if (remainingFailures > 0)
            {
                remainingFailures--;
                throw new IOException("controlled artifact failure");
            }

            return Task.FromResult($"sha256/{contentHash[..2]}/{contentHash}");
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    private sealed class RecordingAutomaticAuditEvidence : IRecordAutomaticStandaloneAuditEvidence
    {
        public List<RecordAutomaticStandaloneAuditEvidenceRequest> Requests { get; } = [];

        public Task<StandaloneAuditEvidence> ExecuteAsync(
            RecordAutomaticStandaloneAuditEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new StandaloneAuditEvidence(
                Guid.NewGuid(),
                request.IntakeReceiptId,
                request.OriginalReportAssetId,
                request.Assessment,
                Guid.Empty,
                ProcessedAtUtc,
                "The retained original report states the literal outcome.",
                request.ExpectedIntakeVersion + 1,
                false));
        }
    }
}
