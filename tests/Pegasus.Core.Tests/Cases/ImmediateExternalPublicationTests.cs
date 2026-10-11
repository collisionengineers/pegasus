using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class ImmediateExternalPublicationTests
{
    private sealed class RecordingTriagePairing : ITriageCasePairing
    {
        public List<Guid> CaseIds { get; } = [];
        public Task<TriageCasePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken)
        {
            CaseIds.Add(caseId);
            return Task.FromResult(new TriageCasePairingResult(0, 0, 0));
        }
        public Task<TriageCasePairingResult> PairTriageAsync(Guid triageCaseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TriageCasePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task AcceptancePublishesTheCustodyWorkCreatedByItsCommittedTransaction()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(workItemId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher,
            new RecordingTriagePairing());

        var result = await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    /// <summary>
    /// A Triage is never accepted from intake as a Case type: it is created by
    /// its own route, so acceptance refuses it before reaching the store.
    /// </summary>
    [Fact]
    public async Task AcceptanceRefusesTheTriageCaseType()
    {
        var store = new AcceptanceStore(Guid.NewGuid());
        var acceptance = new AcceptIntake(
            store,
            new ConfigurationStore(),
            new InspectionModeStore(),
            new RecordingPublisher(),
            new RecordingTriagePairing());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => acceptance.ExecuteAsync(
            AcceptanceRequest() with { CaseType = CaseType.Triage },
            CancellationToken.None));

        Assert.Null(store.LastRequest);
    }

    /// <summary>
    /// Accepting a received item is casework (ADR-0064): staff, the
    /// Automation Actor and the automatic route accept; a Principal does not.
    /// </summary>
    [Fact]
    public async Task TheAutomationActorAcceptsAndAPrincipalCannot()
    {
        var store = new AcceptanceStore(Guid.NewGuid());
        var acceptance = new AcceptIntake(
            store,
            new ConfigurationStore(),
            new InspectionModeStore(),
            new RecordingPublisher(),
            new RecordingTriagePairing());

        await Assert.ThrowsAsync<ArgumentException>(() => acceptance.ExecuteAsync(
            AcceptanceRequest() with { Actor = ActionActor.Principal(Guid.NewGuid()) },
            CancellationToken.None));
        Assert.Null(store.LastRequest);

        await acceptance.ExecuteAsync(
            AcceptanceRequest() with { Actor = ActionActor.Automation("grant-1") },
            CancellationToken.None);

        Assert.Equal(ActorKind.Automation, store.LastRequest!.Actor.Kind);
    }

    /// <summary>
    /// Acceptance also publishes the automatic vehicle lookup its transaction
    /// enqueued, so DVLA/MOT evidence arrives with the new Case rather than on
    /// the Worker's next reconciliation sweep (FRD-06 D34).
    /// </summary>
    [Fact]
    public async Task AcceptancePublishesTheVehicleLookupItsTransactionEnqueued()
    {
        var custodyWorkId = Guid.NewGuid();
        var vehicleLookupWorkId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(custodyWorkId, vehicleLookupWorkId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher,
            new RecordingTriagePairing());

        await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.Equal([custodyWorkId, vehicleLookupWorkId], publisher.WorkItemIds);
    }

    /// <summary>
    /// An acceptance that enqueued no lookup — no unambiguous registration, or
    /// lookups not composed — publishes only its custody work.
    /// </summary>
    [Fact]
    public async Task AcceptanceWithNoEnqueuedLookupPublishesOnlyCustody()
    {
        var custodyWorkId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(custodyWorkId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher,
            new RecordingTriagePairing());

        await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.Equal([custodyWorkId], publisher.WorkItemIds);
    }

    [Fact]
    public async Task AuditWithoutOriginalReportEvidenceIsAccepted()
    {
        var store = new AcceptanceStore(Guid.NewGuid());
        var acceptance = new AcceptIntake(
            store,
            new ConfigurationStore(),
            new InspectionModeStore(),
            new RecordingPublisher(),
            new RecordingTriagePairing());

        await acceptance.ExecuteAsync(
            AcceptanceRequest() with { CaseType = CaseType.Audit },
            CancellationToken.None);

        Assert.Equal(CaseType.Audit, store.LastRequest!.CaseType);
        Assert.Null(store.LastRequest.StandaloneAuditEvidenceId);
    }

    [Fact]
    public async Task AStandaloneAuditIsAcceptedWithItsOriginalReportReading()
    {
        var store = new AcceptanceStore(Guid.NewGuid());
        var reading = new OriginalReportReading(
            new string('a', 64), "Laird Assessors", "2026-09-01", "roadworthy", "repairable", false);
        var reader = new RecordingReader(reading);
        var evidenceId = Guid.NewGuid();
        var request = AcceptanceRequest() with { CaseType = CaseType.Audit, StandaloneAuditEvidenceId = evidenceId };
        var acceptance = new AcceptIntake(
            store,
            new ConfigurationStore(),
            new InspectionModeStore(),
            new RecordingPublisher(),
            new RecordingTriagePairing(),
            originalReport: reader);

        await acceptance.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal((request.ReceiptId, evidenceId), Assert.Single(reader.Reads));
        Assert.Equal(reading, store.LastRequest!.OriginalReport);
    }

    [Fact]
    public async Task AnAuditWithNoOriginalReportEvidenceReadsNoReport()
    {
        var store = new AcceptanceStore(Guid.NewGuid());
        var reader = new RecordingReader(null);
        var acceptance = new AcceptIntake(
            store,
            new ConfigurationStore(),
            new InspectionModeStore(),
            new RecordingPublisher(),
            new RecordingTriagePairing(),
            originalReport: reader);

        await acceptance.ExecuteAsync(
            AcceptanceRequest() with { CaseType = CaseType.Audit },
            CancellationToken.None);

        Assert.Empty(reader.Reads);
        Assert.Null(store.LastRequest!.OriginalReport);
    }

    private const string ReportText = """
        Mr D Roberton                                        Date:  09/03/2026
        Gravesend                                            Our Ref:  00077570/PK

                       Engineer Repairable Report

             Vehicle Value: £9,267.00      Repair Cost: £6,143.90 inc VAT     Roadworthy: No

        Phil Kendrick AQP CAE AMIMI
        Connexus Vehicle Assessors
        """;

    /// <summary>
    /// The same document is not read twice (operator, 11 October 2026): the
    /// reading intake recorded of the report seeds the Audit and the report
    /// is not read again.
    /// </summary>
    [Fact]
    public async Task AStandaloneAuditIsSeededFromTheReadingIntakeRecordedWithoutReadingTheReportAgain()
    {
        var harness = new RecordedReadingHarness();
        harness.Analyses.Record(harness.Analysis(RecordThirdPartyReportReading.OperationKey(harness.Asset)));

        await harness.AcceptAsync();

        Assert.Empty(harness.Reader.Reads);
        var seeded = harness.Store.LastRequest!.OriginalReport!;
        Assert.Equal("Connexus Vehicle Assessors", seeded.Assessor);
        Assert.Equal("2026-03-09", seeded.ReportDate);
        Assert.Equal("unroadworthy", seeded.Roadworthiness);
        Assert.Equal("repairable", seeded.Outcome);
        Assert.Equal(harness.Asset.ContentHash, seeded.Sha256);
    }

    [Fact]
    public async Task AScannedReportIsSeededFromItsOcrRecordingWithoutReadingTheReportAgain()
    {
        var harness = new RecordedReadingHarness();
        var ocrOperationId = Guid.NewGuid();
        harness.Ocr.CompletedOperationId = ocrOperationId;
        harness.Analyses.Record(harness.Analysis(RecordThirdPartyReportReading.OcrOperationKey(harness.Asset, ocrOperationId)));

        await harness.AcceptAsync();

        Assert.Empty(harness.Reader.Reads);
        Assert.Equal("Connexus Vehicle Assessors", harness.Store.LastRequest!.OriginalReport!.Assessor);
    }

    [Fact]
    public async Task AStandaloneAuditWithNoRecordedReadingReadsTheReportOnce()
    {
        var harness = new RecordedReadingHarness();

        await harness.AcceptAsync();

        Assert.Equal((harness.ReceiptId, harness.EvidenceId), Assert.Single(harness.Reader.Reads));
        Assert.Same(harness.Reader.Reading, harness.Store.LastRequest!.OriginalReport);
    }

    [Fact]
    public async Task AReadingRecordedForOtherBytesIsNotUsed()
    {
        var harness = new RecordedReadingHarness();
        harness.Analyses.Record(harness.Analysis(
            RecordThirdPartyReportReading.OperationKey(harness.Asset), sha256: new string('b', 64)));

        await harness.AcceptAsync();

        Assert.Single(harness.Reader.Reads);
    }

    [Fact]
    public async Task NonAuditWithOriginalReportEvidenceIsRefused()
    {
        var store = new AcceptanceStore(Guid.NewGuid());
        var acceptance = new AcceptIntake(
            store,
            new ConfigurationStore(),
            new InspectionModeStore(),
            new RecordingPublisher(),
            new RecordingTriagePairing());

        await Assert.ThrowsAsync<ArgumentException>(() => acceptance.ExecuteAsync(
            AcceptanceRequest() with { StandaloneAuditEvidenceId = Guid.NewGuid() },
            CancellationToken.None));

        Assert.Null(store.LastRequest);
    }

    [Fact]
    public async Task DuplicateAcceptanceRetriesPairingWithoutRepublishingAcceptanceCustody()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var pairing = new RecordingPairing();
        var triagePairing = new RecordingTriagePairing();
        var acceptance = new AcceptIntake(new AcceptanceStore(workItemId),
            new ConfigurationStore(), new InspectionModeStore(), publisher, triagePairing, pairing);
        var request = AcceptanceRequest();

        var first = await acceptance.ExecuteAsync(request, CancellationToken.None);
        var replay = await acceptance.ExecuteAsync(request, CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Identity, replay.Identity);
        Assert.Equal([first.Identity.CaseId, first.Identity.CaseId], pairing.CaseIds);
        Assert.Equal([first.Identity.CaseId, first.Identity.CaseId], triagePairing.CaseIds);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    [Fact]
    public async Task ReplacementPublishesTheCustodyWorkCreatedByItsCommittedTransaction()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var replacement = new CreateLinkedReplacement(new ReplacementStore(workItemId), publisher);

        var result = await replacement.ExecuteAsync(
            new(
                Guid.NewGuid(),
                0,
                Staff(),
                "replacement-1",
                "Correct source identity.",
                "lease-token",
                "QDOS"),
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    private static AcceptIntakeRequest AcceptanceRequest() =>
        new(
            Guid.NewGuid(),
            0,
            Staff(),
            "acceptance-1",
            CaseType.Inspection,
            "QDOS",
            new(true, true));

    private static ActionActor Staff() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class RecordingReader(OriginalReportReading? reading) : IReadOriginalReport
    {
        public OriginalReportReading? Reading => reading;

        public List<(Guid, Guid)> Reads { get; } = [];

        public Task<OriginalReportReading?> ForIntakeAsync(
            Guid receiptId, Guid standaloneAuditEvidenceId, CancellationToken cancellationToken)
        {
            Reads.Add((receiptId, standaloneAuditEvidenceId));
            return Task.FromResult(reading);
        }

        public Task<OriginalReportReading?> ForDocumentAsync(
            ActionActor actor, Guid caseId, Guid occurrenceId, Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OriginalReportRecognition> RecogniseFiledAssetAsync(
            Guid receiptId, IntakeAssetRecord asset, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordedReadingHarness
    {
        public Guid ReceiptId { get; } = Guid.NewGuid();
        public Guid EvidenceId { get; } = Guid.NewGuid();
        public IntakeAssetRecord Asset { get; } = new(
            Guid.NewGuid(), "attachment 2: report.pdf", "report.pdf", "application/pdf",
            IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment,
            100, new string('a', 64), "report-key", null, null, null, null);
        public AcceptanceStore Store { get; } = new(Guid.NewGuid());
        public RecordingReader Reader { get; } = new(
            new(new string('a', 64), "Read Again", "2026-01-01", "roadworthy", "total_loss", false));
        public FakeAnalyses Analyses { get; } = new();
        public FakeOcr Ocr { get; } = new();

        public Task AcceptAsync() =>
            new AcceptIntake(
                Store,
                new ConfigurationStore(),
                new InspectionModeStore(),
                new RecordingPublisher(),
                new RecordingTriagePairing(),
                originalReport: Reader,
                recordedReading: new RecordedOriginalReportReading(
                    new FakeEvidence(ReceiptId, EvidenceId, Asset.Id),
                    new FakeReceipts(ReceiptId, Asset),
                    Analyses,
                    Ocr))
                .ExecuteAsync(
                    AcceptanceRequest() with
                    {
                        ReceiptId = ReceiptId,
                        CaseType = CaseType.Audit,
                        StandaloneAuditEvidenceId = EvidenceId
                    },
                    CancellationToken.None);

        /// <summary>What intake records of the report under <paramref name="operationKey"/>.</summary>
        public RetainedInstructionAnalysis Analysis(string operationKey, string? sha256 = null)
        {
            var read = new IntakeSourceReadResult(
                IntakeSourceReadStatus.Readable,
                [new(IntakeEvidenceSource.PdfContent, "report.pdf, page 1", ReportText)],
                [],
                [],
                RequiresOcr: false);
            var extraction = ThirdPartyReportExtraction.Extract(
                read,
                new(ReceiptId, Asset.ContentHash, Occurrence: 0, IntakeAssetId: Asset.Id, SourceLabel: "report.pdf"));
            return new(
                Guid.NewGuid(),
                ReceiptId,
                Asset.Id,
                sha256 ?? Asset.ContentHash,
                operationKey,
                ThirdPartyReportAnalysis.Outcome(extraction.Selection),
                1,
                DateTimeOffset.UnixEpoch,
                ThirdPartyReportAnalysis.ToCandidates(extraction, "pdf", "1"));
        }
    }

    private sealed class FakeAnalyses : IRetainedInstructionAnalysisStore
    {
        private readonly Dictionary<string, RetainedInstructionAnalysis> recorded = [];

        public void Record(RetainedInstructionAnalysis analysis) => recorded[analysis.OperationKey] = analysis;

        public Task<(RetainedInstructionAnalysis Analysis, bool IsReplay)> RecordAsync(
            RetainedInstructionAnalysis analysis, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedInstructionAnalysis?> FindByOperationKeyAsync(
            string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(recorded.GetValueOrDefault(operationKey));
    }

    private sealed class FakeEvidence(Guid receiptId, Guid evidenceId, Guid assetId) : IStandaloneAuditEvidenceQueries
    {
        public Task<StandaloneAuditEvidence?> GetForReceiptAsync(Guid intakeReceiptId, CancellationToken cancellationToken) =>
            Task.FromResult<StandaloneAuditEvidence?>(intakeReceiptId == receiptId
                ? new(evidenceId, receiptId, assetId, AuditAssessment.Repairable, Guid.Empty,
                    DateTimeOffset.UnixEpoch, "The retained original report states Repairable.", 0, false)
                : null);
    }

    private sealed class FakeReceipts(Guid receiptId, IntakeAssetRecord asset) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeAssetRecord?> GetAssetAsync(Guid receipt, Guid assetId, CancellationToken cancellationToken) =>
            Task.FromResult(receipt == receiptId && assetId == asset.Id ? asset : null);
    }

    /// <summary>Holds at most one completed OCR operation, found by the source hash.</summary>
    private sealed class FakeOcr : IIntakeOcrOperationStore
    {
        public Guid? CompletedOperationId { get; set; }

        public Task<IntakeOcrOperation?> FindCompletedBySourceAsync(string sourceSha256, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeOcrOperation?>(CompletedOperationId is { } id
                ? new(id, Guid.NewGuid(), Guid.NewGuid(), sourceSha256, 100, [1], "ocr-k", IntakeOcrState.Completed, 1)
                : null);

        public Task<IntakeOcrOperation?> FindAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation?> ResumeRequestedRetryAsync(Guid operationId, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> BeginAsync(Guid operationId, IntakeOcrRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> RecordSubmitAttemptAsync(Guid operationId, long expectedVersion, DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> RecordSubmittedAsync(Guid operationId, long expectedVersion, string providerOperationId, DateTimeOffset submittedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> CompleteAsync(Guid operationId, long expectedVersion, IntakeOcrResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> CompleteAnalysisAsync(Guid operationId, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> RecordOutcomeAsync(Guid operationId, long expectedVersion, IntakeOcrState state, IntakeOcrFailure failure, DateTimeOffset? retryAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingPublisher : ICommittedExternalWorkPublisher
    {
        public List<Guid> WorkItemIds { get; } = [];

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            WorkItemIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptanceStore(Guid workItemId, Guid? vehicleLookupWorkId = null)
        : ICaseAcceptanceStore
    {
        private readonly CaseAcceptanceOutcome outcome = Outcome(workItemId, vehicleLookupWorkId);
        private bool accepted;
        public CaseAcceptanceRequest? LastRequest { get; private set; }

        public Task<CaseAcceptanceOutcome> AcceptAsync(
            CaseAcceptanceRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            var result = outcome with { IsDuplicate = accepted };
            accepted = true;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingPairing : IImageIntakeCasePairing
    {
        public List<Guid> CaseIds { get; } = [];

        public Task<ImageIntakePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken)
        {
            CaseIds.Add(caseId);
            return Task.FromResult(CaseIds.Count == 1
                ? new ImageIntakePairingResult(1, 0, 1, nameof(IntakeAssociationConflictException))
                : new ImageIntakePairingResult(1, 1, 0));
        }

        public Task<ImageIntakePairingResult> PairRegisteredReceiptAsync(Guid receiptId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ImageIntakePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ReplacementStore(Guid workItemId) : ILinkedCaseReplacementStore
    {
        public Task<CaseAcceptanceOutcome> CreateAsync(
            CreateLinkedReplacementRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Outcome(workItemId));
    }

    private sealed class ConfigurationStore : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseWorkflowConfiguration("test", 1));
    }

    private sealed class InspectionModeStore : IPrincipalInspectionModeStore
    {
        public Task<CaseInspectionMode?> GetForPrincipalAsync(
            string principalCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseInspectionMode?>(null);
    }

    private static CaseAcceptanceOutcome Outcome(
        Guid workItemId,
        Guid? vehicleLookupWorkId = null) =>
        new(
            new CaseIdentity(Guid.NewGuid(), "QDOS", 2031, 1, "QDS31001"),
            CaseInitialState.Review,
            CaseCustodyState.Pending,
            workItemId,
            false,
            vehicleLookupWorkId);
}
