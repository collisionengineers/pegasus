using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The one production command for unresolved retained material. It reads the
/// immutable source, decides which provider's instruction it is FROM THE
/// DOCUMENT, records what the document says — and allocates nothing.
/// </summary>
public sealed class AnalyzeRetainedInstructionTests
{
    private static readonly byte[] SourceBytes = Encoding.UTF8.GetBytes(
        "QDOS\nOur Client’s Vehicle: Ford Focus\nRegistration: AB12 CDE");
    private static readonly string SourceHash = Convert.ToHexString(SHA256.HashData(SourceBytes));
    private const string ResponseHash = "bb112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
    private static readonly DateTimeOffset Now = new(2031, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AMatchingDocumentIsAnalysedAndItsFieldsRecorded()
    {
        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]) with
            {
                Fields =
                [
                    new("Claimant name", "Jane Smith",
                        [new("Jane Smith", IntakeEvidenceSource.PdfContent, "instruction.pdf, page 2")],
                        IsDefaulted: false, HasConflict: false)
                ]
            });

        var result = await harness.ExecuteAsync();

        Assert.Equal(RetainedInstructionAnalysisOutcome.Analyzed, result.Outcome);
        Assert.False(result.IsReplay);
        var analysis = Assert.IsType<RetainedInstructionAnalysis>(result.Analysis);
        Assert.Equal(harness.Receipt.Id, analysis.ReceiptId);
        Assert.Equal(harness.SourceAssetId, analysis.IntakeAssetId);
        Assert.Equal(SourceHash, analysis.SourceSha256);

        // The document PROPOSES a principal - as a candidate, never as an
        // allocation or a draft value.
        var principal = Assert.Single(
            analysis.Candidates,
            candidate => candidate.Field == AnalyzeRetainedInstruction.SuggestedPrincipalField);
        Assert.Equal("QDOS", principal.RawValue);
        Assert.Equal(AnalyzeRetainedInstruction.PrincipalPartyRole, principal.PartyRole);
        Assert.Equal(SourceCandidateDisposition.Usable, principal.Disposition);
        Assert.Equal("instruction", principal.DocumentRole);

        var claimant = Assert.Single(
            analysis.Candidates,
            candidate => candidate.Field == "Claimant name");
        Assert.Equal("Jane Smith", claimant.RawValue);
        Assert.Equal("Jane Smith", claimant.NormalizedValue);
        Assert.Equal(SourceCandidateDisposition.Usable, claimant.Disposition);
        // The page lives only inside the fragment's own source label; it is
        // parsed rather than guessed at.
        Assert.Equal(2, claimant.Page);
        Assert.Equal("instruction.pdf, page 2", claimant.SourceLabel);

        // The policy identity recorded is the DOCUMENT profile's, not a mail
        // route's: nothing downstream may mistake this for a route-established
        // principal.
        Assert.Equal("qdos_instruction_document", claimant.PolicyKey);
        var observed = harness.Policies[0].ObservedPrincipalContext;
        Assert.NotNull(observed);
        Assert.Equal("qdos_instruction_document", observed!.PolicyKey);
        Assert.Equal(1, observed.PolicyVersion);
        Assert.Equal("QDOS", observed.PrincipalCode);
        Assert.Equal(1, harness.SourceReader.Reads);
    }

    [Fact]
    public async Task AFieldTheDocumentDoesNotStateIsRecordedAsMissing()
    {
        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]) with
            {
                Fields = [new("Claim number", null, [], IsDefaulted: false, HasConflict: false)]
            });

        var result = await harness.ExecuteAsync();

        var missing = Assert.Single(
            result.Analysis!.Candidates,
            candidate => candidate.Field == "Claim number");
        Assert.Equal(SourceCandidateDisposition.Missing, missing.Disposition);
        Assert.Null(missing.RawValue);
    }

    [Fact]
    public async Task EveryCandidateOfAConflictingFieldIsRecorded()
    {
        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]) with
            {
                Fields =
                [
                    new("Vehicle registration", null,
                        [
                            new("AB12CDE", IntakeEvidenceSource.PdfContent, "instruction.pdf, page 1"),
                            new("XY34ZZZ", IntakeEvidenceSource.PdfContent, "instruction.pdf, page 3")
                        ],
                        IsDefaulted: false, HasConflict: true)
                ]
            });

        var result = await harness.ExecuteAsync();

        var conflicting = result.Analysis!.Candidates
            .Where(candidate => candidate.Field == "Vehicle registration")
            .OrderBy(candidate => candidate.Occurrence)
            .ToArray();
        Assert.Equal(2, conflicting.Length);
        // Two readings the document itself supports are Ambiguous. Conflicting
        // is reserved for a candidate that contradicts a confirmed fact (C02).
        Assert.All(conflicting, candidate =>
            Assert.Equal(SourceCandidateDisposition.Ambiguous, candidate.Disposition));
        Assert.Equal(["AB12CDE", "XY34ZZZ"], conflicting.Select(candidate => candidate.RawValue));
        Assert.Equal([0, 1], conflicting.Select(candidate => candidate.Occurrence));
        Assert.Equal([1, 3], conflicting.Select(candidate => candidate.Page));
        // Nothing is canonicalized: a normalized value here would make the
        // conflict look resolved.
        Assert.All(conflicting, candidate => Assert.Null(candidate.NormalizedValue));
    }

    [Fact]
    public async Task ADocumentNoProfileRecognisesRecordsAnAnalysisWithNoCandidates()
    {
        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["NOT IN THIS DOCUMENT"]));

        var result = await harness.ExecuteAsync();

        Assert.Equal(RetainedInstructionAnalysisOutcome.NoProfile, result.Outcome);
        Assert.Empty(result.Analysis!.Candidates);
        Assert.Empty(result.MatchingPrincipalCodes);
    }

    [Fact]
    public async Task TwoMatchingProfilesAreAmbiguousAndBothAreNamed()
    {
        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]),
            InstructionExtractionPolicySelectorTests.Profile("PCH", ["Registration:"]));

        var result = await harness.ExecuteAsync();

        Assert.Equal(RetainedInstructionAnalysisOutcome.Ambiguous, result.Outcome);
        Assert.Equal(["PCH", "QDOS"], result.MatchingPrincipalCodes);
        Assert.Empty(result.Analysis!.Candidates);
    }

    [Fact]
    public async Task ASourceThatCannotBeOpenedIsReportedRatherThanRecorded()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));
        harness.Documents.FailToOpen = true;

        var result = await harness.ExecuteAsync();

        Assert.Equal(RetainedInstructionAnalysisOutcome.SourceUnavailable, result.Outcome);
        Assert.Null(result.Analysis);
        // Nothing recorded, so the same key can still analyse later rather than
        // replaying a stored failure for ever.
        Assert.Empty(harness.Store.Records);
    }

    [Fact]
    public async Task AReceiptWithNoRetainedSourceIsSourceUnavailable()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));
        harness.SetReceipt(harness.Receipt with { Assets = [] });

        var result = await harness.ExecuteAsync();

        Assert.Equal(RetainedInstructionAnalysisOutcome.SourceUnavailable, result.Outcome);
        Assert.Empty(harness.Store.Records);
    }

    [Fact]
    public async Task AStaleReceiptVersionIsAConflictAndWritesNothing()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));

        var result = await harness.ExecuteAsync(expectedVersion: harness.Receipt.Version + 1);

        Assert.Equal(RetainedInstructionAnalysisOutcome.Conflict, result.Outcome);
        Assert.Null(result.Analysis);
        Assert.Empty(harness.Store.Records);
    }

    [Fact]
    public async Task AnOperationKeyReusedForADifferentRequestIsAConflict()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));
        await harness.ExecuteAsync(operationKey: "analysis-1");

        // Same key, different receipt: the key is consumed for one request and
        // never silently rebound to another.
        var other = harness.Receipt with { Id = Guid.NewGuid() };
        harness.Receipts[other.Id] = other;
        var result = await harness.Command.ExecuteAsync(
            new(harness.Actor, other.Id, other.Version, "analysis-1"));

        Assert.Equal(RetainedInstructionAnalysisOutcome.Conflict, result.Outcome);
        Assert.Single(harness.Store.Records);
    }

    [Fact]
    public async Task AReplayReturnsTheStoredAnalysisAndWritesNoDuplicateCandidates()
    {
        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]) with
            {
                Fields =
                [
                    new("Claimant name", "Jane Smith",
                        [new("Jane Smith", IntakeEvidenceSource.PdfContent, "instruction.pdf, page 2")],
                        IsDefaulted: false, HasConflict: false)
                ]
            });

        var first = await harness.ExecuteAsync(operationKey: "analysis-replay");
        var second = await harness.ExecuteAsync(operationKey: "analysis-replay");

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Analysis!.Id, second.Analysis!.Id);
        Assert.Single(harness.Store.Records);
        Assert.Equal(
            first.Analysis.Candidates.Count,
            harness.Store.Records.Single().Candidates.Count);
        // The replay never reaches the source or the extraction at all.
        Assert.Equal(1, harness.Documents.Opens);
    }

    [Fact]
    public async Task TheAnalysisAllocatesNothingAndWritesNoReceiptDecisionOrDraft()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));
        var before = harness.Receipt;

        var result = await harness.ExecuteAsync();

        Assert.Equal(RetainedInstructionAnalysisOutcome.Analyzed, result.Outcome);
        // The receipt is untouched: same decision, same draft, same version,
        // same case association. The command has no port that could change one.
        var after = harness.Receipts[before.Id];
        Assert.Equal(before, after);
        Assert.Null(after.InstructionDraft);
        Assert.Null(after.AcceptedCaseId);
        Assert.Null(after.ManualLinkedCaseId);
        Assert.Null(after.AllocationState);
    }

    /// <summary>
    /// PerformCasework admits exactly Staff and the Automation Actor. Every
    /// other typed actor fails closed in Core, not at a surface that might
    /// forget to ask.
    /// </summary>
    [Fact]
    public async Task ANonStaffNonAutomationActorIsForbidden()
    {
        ActionActor[] forbidden =
        [
            ActionActor.SystemWorker("intake-processing"),
            ActionActor.RequestLink(Guid.NewGuid()),
            ActionActor.Provider(Guid.NewGuid())
        ];

        foreach (var actor in forbidden)
        {
            var harness = new Harness(
                InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));

            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                harness.Command.ExecuteAsync(
                    new(actor, harness.Receipt.Id, harness.Receipt.Version, "analysis-forbidden")));
            Assert.Empty(harness.Store.Records);
        }
    }

    [Fact]
    public async Task TheAutomationActorMayAnalyseRetainedMaterial()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));

        var result = await harness.Command.ExecuteAsync(
            new(
                ActionActor.Automation("intake-processing"),
                harness.Receipt.Id,
                harness.Receipt.Version,
                "analysis-automation"));

        Assert.Equal(RetainedInstructionAnalysisOutcome.Analyzed, result.Outcome);
    }

    [Fact]
    public async Task CompletedOcrIsHashBoundLocatedAndAlwaysReviewOnly()
    {
        var evidence = OcrEvidence(SourceHash);
        var ocrRead = AnalyzeRetainedInstruction.CreateOcrReadResult(evidence);
        var fragment = Assert.Single(ocrRead.Content);
        Assert.Equal(2, fragment.Locator!.Page);
        Assert.Equal(SourceHash, fragment.Locator.Sha256);
        Assert.Contains(ResponseHash, fragment.Locator.Region, StringComparison.Ordinal);
        Assert.Contains("0.42", fragment.Locator.Region, StringComparison.Ordinal);
        Assert.Contains("0.99", fragment.Locator.Region, StringComparison.Ordinal);
        Assert.Contains("\"Confidence\":null", fragment.Locator.Region, StringComparison.Ordinal);
        Assert.Contains("pixel", fragment.Locator.Region, StringComparison.Ordinal);

        var harness = new Harness(
            InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]) with
            {
                Fields =
                [
                    new("Claimant name", "Jane Smith",
                        [new("Jane Smith", IntakeEvidenceSource.DocumentContent, fragment.SourceLabel, fragment.Locator)],
                        IsDefaulted: false, HasConflict: false)
                ]
            });
        var actor = ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId);
        var first = await harness.Command.ExecuteAsync(
            new(actor, harness.Receipt.Id, harness.Receipt.Version, "ocr-analysis", harness.SourceAssetId, evidence));
        var replay = await harness.Command.ExecuteAsync(
            new(actor, harness.Receipt.Id, harness.Receipt.Version, "ocr-analysis", harness.SourceAssetId, evidence));

        Assert.Equal(RetainedInstructionAnalysisOutcome.Analyzed, first.Outcome);
        Assert.True(replay.IsReplay);
        Assert.All(first.Analysis!.Candidates, candidate =>
            Assert.Equal(SourceCandidateDisposition.Ambiguous, candidate.Disposition));
        var claimant = Assert.Single(first.Analysis.Candidates, candidate => candidate.Field == "Claimant name");
        Assert.Equal(SourceHash, claimant.Locator!.Sha256);
        Assert.Equal($"{IntakeOcrProviderIdentity.Provider}/{IntakeOcrProviderIdentity.ModelId}", claimant.ReaderKey);
        Assert.Equal(IntakeOcrProviderIdentity.ApiVersion, claimant.ReaderVersion);
        Assert.Equal(1, harness.Documents.Opens);
        Assert.Single(harness.Store.Records);
    }

    [Fact]
    public async Task OcrFromStaffOrWithUnattributableProvenanceRecordsNothing()
    {
        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));
        var evidence = OcrEvidence(SourceHash);
        var staff = await harness.Command.ExecuteAsync(
            new(harness.Actor, harness.Receipt.Id, harness.Receipt.Version, "staff-ocr", harness.SourceAssetId, evidence));
        var malformed = await harness.Command.ExecuteAsync(
            new(
                ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId),
                harness.Receipt.Id,
                harness.Receipt.Version,
                "bad-ocr",
                harness.SourceAssetId,
                evidence with { SourceSha256 = new string('0', 64) }));

        Assert.Equal(RetainedInstructionAnalysisOutcome.SourceUnavailable, staff.Outcome);
        Assert.Equal(RetainedInstructionAnalysisOutcome.SourceUnavailable, malformed.Outcome);
        Assert.Empty(harness.Store.Records);
        Assert.Equal(0, harness.Documents.Opens);
    }

    [Fact]
    public async Task MalformedOcrIdentityAndPageSetsContributeNothing()
    {
        var valid = OcrEvidence(SourceHash);
        var invalid = new Func<CompletedOcrEvidence, CompletedOcrEvidence>[]
        {
            evidence => evidence with { Result = evidence.Result with { ResponseSha256 = "abcd" } },
            evidence => evidence with { Result = evidence.Result with { ResponseSha256 = new string('g', 64) } },
            evidence => evidence with { Result = evidence.Result with { Provider = "other" } },
            evidence => evidence with { Result = evidence.Result with { ModelId = "other" } },
            evidence => evidence with { Result = evidence.Result with { ApiVersion = "other" } },
            evidence => evidence with { QualifiedPages = [3] },
            evidence => evidence with { QualifiedPages = [2, 2] },
            evidence => evidence with { QualifiedPages = [0] },
            evidence => evidence with
            {
                Result = evidence.Result with
                {
                    Pages = [evidence.Result.PageResults[0] with { Text = " " }]
                }
            }
        };

        var harness = new Harness(InstructionExtractionPolicySelectorTests.Profile("QDOS", ["QDOS"]));
        var actor = ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId);
        for (var index = 0; index < invalid.Length; index++)
        {
            var result = await harness.Command.ExecuteAsync(new(
                actor,
                harness.Receipt.Id,
                harness.Receipt.Version,
                $"invalid-ocr-{index}",
                harness.SourceAssetId,
                invalid[index](valid)));
            Assert.Equal(RetainedInstructionAnalysisOutcome.SourceUnavailable, result.Outcome);
        }

        Assert.Empty(harness.Store.Records);
        Assert.Equal(0, harness.Documents.Opens);
        Assert.Equal(0, harness.SourceReader.Reads);
    }

    private static CompletedOcrEvidence OcrEvidence(string sourceHash) => new(
        sourceHash,
        [2],
        new(
            IntakeOcrState.Completed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            "provider-operation",
            ResponseHash,
            [
                new(
                    2,
                    "QDOS Jane Smith",
                    [new("QDOS Jane Smith", new(1, 2, 30, 8, "pixel"),
                        [
                            new("QDOS", 0.42, new(1, 2, 8, 8, "pixel")),
                            new("Jane", 0.99, new(9, 2, 18, 8, "pixel")),
                            new("Smith", null, new(19, 2, 30, 8, "pixel"))
                        ])],
                    [new(1, 1, 1, [new(0, 0, "Jane Smith", new(10, 20, 30, 40, "pixel"))])])
            ]));

    [Theory]
    [InlineData("instruction.pdf, page 4", 4)]
    [InlineData("uploaded instruction.pdf", null)]
    [InlineData("instruction.pdf, page x", null)]
    [InlineData("message body, page ", null)]
    public void ThePageIsReadFromTheFragmentLabelOrLeftUnknown(string label, int? expected) =>
        Assert.Equal(expected, AnalyzeRetainedInstruction.PageFrom(label));

    [Fact]
    public void TheLocatorRoundTripsThroughItsOwnEnvelope()
    {
        var json = AnalyzeRetainedInstruction.LocatorJson("instruction.pdf, page 2", 2);

        var (pageLabel, pageNumber, pageLocator) = AnalyzeRetainedInstruction.ReadLocator(json);
        Assert.Equal("instruction.pdf, page 2", pageLabel);
        Assert.Equal(2, pageNumber);
        Assert.Equal(IntakeSourceLocator.ForPage(2), pageLocator);

        var (bodyLabel, bodyPage, bodyLocator) = AnalyzeRetainedInstruction.ReadLocator(
            AnalyzeRetainedInstruction.LocatorJson("message body", null));
        Assert.Equal("message body", bodyLabel);
        Assert.Null(bodyPage);
        Assert.Null(bodyLocator);

        // A structured locator survives the round trip whole: the cell, the form
        // field, the region and the message part are the provenance, and a store
        // that dropped any of them would leave a candidate unlocatable.
        var cell = IntakeSourceLocator.ForCell(2, 3, 4, page: 5, occurrence: 1);
        var (cellLabel, cellPage, cellLocator) = AnalyzeRetainedInstruction.ReadLocator(
            AnalyzeRetainedInstruction.LocatorJson("instruction.docx, table 2 row 3 column 4", null, cell));
        Assert.Equal("instruction.docx, table 2 row 3 column 4", cellLabel);
        Assert.Equal(5, cellPage);
        Assert.Equal(cell, cellLocator);
        Assert.Equal("T2R3C4", cellLocator!.Cell);

        var formField = IntakeSourceLocator.ForFormField("ClaimNumber", page: 1, region: "10.00,20.00,30.00,40.00");
        var (_, _, formLocator) = AnalyzeRetainedInstruction.ReadLocator(
            AnalyzeRetainedInstruction.LocatorJson("instruction.pdf, form field ClaimNumber", null, formField));
        Assert.Equal(formField, formLocator);

        var quoted = IntakeSourceLocator.ForMessagePart(IntakeMessagePart.QuotedHistory, "chars 120-400");
        var (_, _, quotedLocator) = AnalyzeRetainedInstruction.ReadLocator(
            AnalyzeRetainedInstruction.LocatorJson("message, quoted history", null, quoted));
        Assert.Equal(quoted, quotedLocator);

        var quotedJson = AnalyzeRetainedInstruction.LocatorJson(
            "message, quoted history",
            null,
            quoted);
        Assert.Contains("\"Kind\":\"MessagePart\"", quotedJson, StringComparison.Ordinal);
        Assert.Contains("\"MessagePart\":\"QuotedHistory\"", quotedJson, StringComparison.Ordinal);

        var (_, _, legacyNumericLocator) = AnalyzeRetainedInstruction.ReadLocator(
            "{\"Version\":2,\"SourceLabel\":\"legacy\",\"Page\":null,\"Kind\":5,"
            + "\"MessagePart\":3,\"Occurrence\":0}");
        Assert.Equal(quoted with { Region = null }, legacyNumericLocator);
        Assert.Throws<InvalidDataException>(() => AnalyzeRetainedInstruction.ReadLocator(
            "{\"Version\":4,\"SourceLabel\":\"future\",\"Page\":null}"));
    }

    private sealed class Harness
    {
        public Harness(params InstructionExtractionPolicySelectorTests.StubProfilePolicy[] policies)
        {
            SourceAssetId = Guid.NewGuid();
            Receipt = BuildReceipt(SourceAssetId);
            Receipts[Receipt.Id] = Receipt;
            Policies = policies;
            SourceReader = new FakeSourceReader(this);
            Command = new AnalyzeRetainedInstruction(
                new FakeReceiptQueries(Receipts),
                Documents,
                SourceReader,
                new InstructionExtractionPolicySelector(policies),
                Store,
                new FixedTimeProvider(Now));
        }

        public Guid SourceAssetId { get; }

        public IntakeReceipt Receipt { get; private set; }

        public Dictionary<Guid, IntakeReceipt> Receipts { get; } = [];

        public InstructionExtractionPolicySelectorTests.StubProfilePolicy[] Policies { get; }

        public FakeLogicalDocumentReader Documents { get; } = new();

        public FakeAnalysisStore Store { get; } = new();

        public FakeSourceReader SourceReader { get; }

        public AnalyzeRetainedInstruction Command { get; }

        public ActionActor Actor { get; } =
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        public EstablishedPrincipalContext? ObservedPrincipalContext { get; set; }

        public void SetReceipt(IntakeReceipt receipt)
        {
            Receipt = receipt;
            Receipts[receipt.Id] = receipt;
        }

        public Task<AnalyzeRetainedInstructionResult> ExecuteAsync(
            long? expectedVersion = null,
            string operationKey = "analysis-1") =>
            Command.ExecuteAsync(
                new(Actor, Receipt.Id, expectedVersion ?? Receipt.Version, operationKey));
    }

    private static IntakeReceipt BuildReceipt(Guid sourceAssetId) =>
        new(
            Guid.NewGuid(),
            "instruction.pdf",
            "application/pdf",
            SourceBytes.Length,
            SourceHash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
            Now,
            Now,
            IntakeDecision.NeedsSorting,
            "Recorded by the pipeline.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            null,
            null,
            [
                new IntakeAssetRecord(
                    sourceAssetId,
                    "uploaded instruction.pdf",
                    "instruction.pdf",
                    "application/pdf",
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    SourceBytes.Length,
                    SourceHash,
                    "storage/0",
                    null,
                    null,
                    null,
                    null)
            ],
            Version: 3);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Serves the retained asset by identity and refuses a hash or length that
    /// does not match what the caller expected — the reader contract's own
    /// guarantee, stood up here so the command's dependence on it is real.
    /// </summary>
    private sealed class FakeLogicalDocumentReader : IReadLogicalDocumentVersion
    {
        public bool FailToOpen { get; set; }

        public int Opens { get; private set; }

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Opens++;
            if (FailToOpen)
            {
                throw new InvalidOperationException("The retained asset is not available.");
            }

            if (!string.Equals(request.ExpectedSha256, SourceHash, StringComparison.OrdinalIgnoreCase)
                || request.ExpectedContentLength != SourceBytes.Length)
            {
                throw new InvalidOperationException("The expected identity does not match.");
            }

            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(SourceBytes, writable: false),
                null,
                null,
                request.IntakeAssetId,
                SourceHash,
                SourceBytes.Length,
                "instruction.pdf",
                "application/pdf"));
        }
    }

    private sealed class FakeSourceReader(Harness harness) : IIntakeSourceReader
    {
        public int Reads { get; private set; }

        public Task<IntakeSourceReadResult> ReadAsync(
            IntakeSource source,
            CancellationToken cancellationToken)
        {
            Reads++;
            _ = harness;
            return Task.FromResult(new IntakeSourceReadResult(
                IntakeSourceReadStatus.Readable,
                [
                    new(
                        IntakeEvidenceSource.PdfContent,
                        "instruction.pdf, page 1",
                        Encoding.UTF8.GetString(source.Content.Span))
                ],
                [],
                [],
                RequiresOcr: false,
                ReaderKey: "fake_reader",
                ReaderVersion: "9"));
        }
    }

    private sealed class FakeAnalysisStore : IRetainedInstructionAnalysisStore
    {
        public List<RetainedInstructionAnalysis> Records { get; } = [];

        public Task<RetainedInstructionAnalysis?> FindByOperationKeyAsync(
            string operationKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Records.FirstOrDefault(item =>
                string.Equals(item.OperationKey, operationKey.Trim(), StringComparison.Ordinal)));

        public Task<RetainedInstructionAnalysis?> FindLatestForReceiptAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Records
                .Where(item => item.ReceiptId == receiptId)
                .OrderByDescending(item => item.CompletedAtUtc)
                .FirstOrDefault());

        public Task<(RetainedInstructionAnalysis Analysis, bool IsReplay)> RecordAsync(
            RetainedInstructionAnalysis analysis,
            CancellationToken cancellationToken = default)
        {
            var existing = Records.FirstOrDefault(item =>
                string.Equals(item.OperationKey, analysis.OperationKey, StringComparison.Ordinal));
            if (existing is not null)
            {
                if (existing.ReceiptId != analysis.ReceiptId
                    || existing.IntakeAssetId != analysis.IntakeAssetId
                    || existing.ExpectedReceiptVersion != analysis.ExpectedReceiptVersion)
                {
                    throw new RetainedInstructionAnalysisConflictException();
                }

                return Task.FromResult((existing, true));
            }

            Records.Add(analysis);
            return Task.FromResult((analysis, false));
        }
    }

    private sealed class FakeReceiptQueries(Dictionary<Guid, IntakeReceipt> receipts)
        : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0, 0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(receipts.TryGetValue(id, out var receipt) ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }
}
