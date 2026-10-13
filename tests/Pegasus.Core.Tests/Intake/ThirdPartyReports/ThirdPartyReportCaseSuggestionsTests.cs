using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// A manually uploaded third-party report suggests the Cases its printed
/// registration and claim reference match in the case-match index (operator,
/// 12 October 2026). Staff still confirm; nothing here links.
/// </summary>
public sealed class ThirdPartyReportCaseSuggestionsTests
{
    private const string LairdSupplement = """
            Our Reference     Your Reference                       Date

             26-                   JF/48913/1                      12th Oct 2026
             3212567/2601234

         Supplementary Report

            Dear Sirs

             Re: Mr Synthetic Person
            Road Traffic Accident on 24/09/2026

             Further to our report regarding your client's , registration AK24UYD.

        Email: enquiries@laird-assessors.com   Web:  www.laird-assessors.com
        """;

    private static readonly Guid IndexedCase = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task AManualUploadsRecognisedReportSuggestsTheIndexedCase()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);
        harness.Analyses.Record(harness.Analysis(LairdSupplement));

        var cases = await harness.FindAsync();

        Assert.Equal(new[] { IndexedCase }, cases);
        // QDOS keeps the claim's NNNNN/N tail as its durable token.
        Assert.Equal("48913/1", harness.Queries.Keys?.DurableClaimToken);
        Assert.Equal("AK24UYD", harness.Queries.Keys?.NormalizedVrm);
    }

    [Fact]
    public async Task MailIsNotAManualUploadAndSuggestsNothing()
    {
        var harness = new Harness(IntakeSourceChannel.Mailbox);
        harness.Analyses.Record(harness.Analysis(LairdSupplement));

        Assert.Empty(await harness.FindAsync());
        Assert.Equal(0, harness.Analyses.Reads);
    }

    [Fact]
    public async Task AReadingThatIsNotAnalyzedSuggestsNothing()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);
        harness.Analyses.Record(harness.Analysis(LairdSupplement) with
        {
            Outcome = RetainedInstructionAnalysisOutcome.Ambiguous
        });

        Assert.Empty(await harness.FindAsync());
        Assert.Null(harness.Queries.Keys);
    }

    [Fact]
    public async Task NoRecordedReadingSuggestsNothing()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);

        Assert.Empty(await harness.FindAsync());
        Assert.Null(harness.Queries.Keys);
    }

    private sealed class Harness(IntakeSourceChannel channel)
    {
        private readonly Guid receiptId = Guid.NewGuid();
        private readonly IntakeAssetRecord report = new(
            Guid.NewGuid(),
            "uploaded source",
            "Bodyshopsuppreport.pdf",
            "application/pdf",
            IntakeAssetKind.Source,
            IntakeAssetDisposition.Source,
            1_000,
            new string('b', 64),
            "asset/report",
            null,
            null,
            null,
            null);

        public FakeAnalyses Analyses { get; } = new();

        public RecordingQueries Queries { get; } = new(IndexedCase);

        public RetainedInstructionAnalysis Analysis(string text)
        {
            var read = new IntakeSourceReadResult(
                IntakeSourceReadStatus.Readable,
                [new(IntakeEvidenceSource.PdfContent, "Bodyshopsuppreport.pdf, page 1", text)],
                [],
                [],
                RequiresOcr: false);
            var extraction = ThirdPartyReportExtraction.Extract(
                read,
                new(receiptId, report.ContentHash, Occurrence: 0, IntakeAssetId: report.Id,
                    SourceLabel: report.FileName));
            return new(
                Guid.NewGuid(),
                receiptId,
                report.Id,
                report.ContentHash,
                RecordThirdPartyReportReading.OperationKey(report),
                ThirdPartyReportAnalysis.Outcome(extraction.Selection),
                1,
                DateTimeOffset.UnixEpoch,
                ThirdPartyReportAnalysis.ToCandidates(extraction, "pdf", "1"));
        }

        public Task<IReadOnlyList<Guid>> FindAsync() =>
            new ThirdPartyReportCaseSuggestions(
                    Analyses,
                    new NoOcr(),
                    new EvaluateIntakeCaseMatch(
                        [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())],
                        Queries))
                .FindAsync(Receipt(), CancellationToken.None);

        private IntakeReceipt Receipt() => new(
            receiptId, report.FileName, report.MediaType, report.ContentLength, report.ContentHash,
            new(channel, $"upload-{receiptId:N}"),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            IntakeDecision.NeedsSorting, "No usable identification.", [], [], null, [],
            null, null, false, "pdf-reader", "1", null, null,
            Assets: [report]);
    }

    private sealed class FakeAnalyses : IRetainedInstructionAnalysisStore
    {
        private readonly Dictionary<string, RetainedInstructionAnalysis> recorded = [];

        public int Reads { get; private set; }

        public void Record(RetainedInstructionAnalysis analysis) => recorded[analysis.OperationKey] = analysis;

        public Task<(RetainedInstructionAnalysis Analysis, bool IsReplay)> RecordAsync(
            RetainedInstructionAnalysis analysis, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedInstructionAnalysis?> FindByOperationKeyAsync(
            string operationKey, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(recorded.GetValueOrDefault(operationKey));
        }
    }

    private sealed class RecordingQueries(Guid caseId) : ICaseMatchCandidateQueries
    {
        public CaseMatchKeys? Keys { get; private set; }

        public Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
            string principalCode,
            CaseMatchKeys keys,
            CancellationToken cancellationToken)
        {
            Keys = keys;
            return Task.FromResult<IReadOnlyList<CaseMatchCandidate>>(
            [
                new(caseId, principalCode, "48913/1", "AK24UYD", null, null, null,
                    CaseLifecycleState.Review, null)
            ]);
        }

        public Task<CaseMatchCandidate?> FindByCaseIdAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseMatchCandidate?>(null);
    }

    private sealed class NoOcr : IIntakeOcrOperationStore
    {
        public Task<IntakeOcrOperation?> FindCompletedBySourceAsync(string sourceSha256, CancellationToken cancellationToken) => Task.FromResult<IntakeOcrOperation?>(null);
        public Task<IntakeOcrOperation?> FindAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation?> ResumeRequestedRetryAsync(Guid operationId, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> BeginAsync(Guid operationId, IntakeOcrRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> RecordSubmitAttemptAsync(Guid operationId, long expectedVersion, DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> RecordSubmittedAsync(Guid operationId, long expectedVersion, string providerOperationId, DateTimeOffset submittedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> CompleteAsync(Guid operationId, long expectedVersion, IntakeOcrResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> CompleteAnalysisAsync(Guid operationId, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeOcrOperation> RecordOutcomeAsync(Guid operationId, long expectedVersion, IntakeOcrState state, IntakeOcrFailure failure, DateTimeOffset? retryAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
