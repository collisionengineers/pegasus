using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// Reading the one filed original report for its Original report cells (#840):
/// the retained intake attachment at acceptance, the Case document at Mark as
/// original report. A report that cannot be read yields no reading and never
/// an exception, so neither the Case nor the Mark is refused for it.
/// </summary>
public sealed class ReadOriginalReportTests
{
    private const string Report = """
        Mr D Roberton                                        Date:  09/03/2026
        Gravesend                                            Our Ref:  00077570/PK

                       Engineer Repairable Report

             Vehicle Value: £9,267.00      Repair Cost: £6,143.90 inc VAT     Roadworthy: No

        Phil Kendrick AQP CAE AMIMI
        Connexus Vehicle Assessors
        """;

    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("%PDF original report");
    private static readonly string Hash = Convert.ToHexStringLower(SHA256.HashData(Bytes));
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task TheRetainedReportAttachmentIsReadAsAutomationNotAsTheProviderApi()
    {
        var harness = new Harness();

        var reading = await harness.Sut.ForIntakeAsync(harness.ReceiptId, harness.EvidenceId, default);

        Assert.NotNull(reading);
        Assert.Equal(Hash, reading.Sha256);
        Assert.Equal("Connexus Vehicle Assessors", reading.Assessor);
        Assert.Equal("2026-03-09", reading.ReportDate);
        Assert.Equal("unroadworthy", reading.Roadworthiness);
        Assert.Equal("repairable", reading.Outcome);
        var source = Assert.Single(harness.Reader.Sources);
        Assert.Equal(IntakeSourceChannel.Automation, source.SourceIdentity.Channel);
        Assert.Equal("report.pdf", source.FileName);
    }

    [Fact]
    public async Task EvidenceOtherThanTheAcceptedOneReadsNothing()
    {
        var harness = new Harness();

        Assert.Null(await harness.Sut.ForIntakeAsync(harness.ReceiptId, Guid.NewGuid(), default));
        Assert.Empty(harness.Reader.Sources);
    }

    [Fact]
    public async Task BytesThatDoNotMatchTheRetainedHashAreNotRead()
    {
        var harness = new Harness(storedBytes: Encoding.UTF8.GetBytes("tampered"));

        Assert.Null(await harness.Sut.ForIntakeAsync(harness.ReceiptId, harness.EvidenceId, default));
        Assert.Empty(harness.Reader.Sources);
    }

    [Fact]
    public async Task AReaderFailureYieldsNoReadingRatherThanAnException()
    {
        var harness = new Harness(readerFailure: new IOException("The PDF could not be opened."));

        Assert.Null(await harness.Sut.ForIntakeAsync(harness.ReceiptId, harness.EvidenceId, default));
    }

    [Fact]
    public async Task AFiledCaseDocumentIsReadAsTheActingStaffMember()
    {
        var harness = new Harness();

        var reading = await harness.Sut.ForDocumentAsync(
            Staff, harness.CaseId, harness.OccurrenceId, harness.VersionId, default);

        Assert.NotNull(reading);
        Assert.Equal("Connexus Vehicle Assessors", reading.Assessor);
        Assert.Equal(Staff, Assert.Single(harness.Documents.Requests).Actor);
    }

    [Fact]
    public async Task AnotherVersionOfTheDocumentReadsNothing()
    {
        var harness = new Harness();

        Assert.Null(await harness.Sut.ForDocumentAsync(
            Staff, harness.CaseId, harness.OccurrenceId, Guid.NewGuid(), default));
        Assert.Empty(harness.Documents.Requests);
    }

    private sealed class Harness
    {
        public Harness(byte[]? storedBytes = null, Exception? readerFailure = null)
        {
            var assetId = Guid.NewGuid();
            Reader = new(readerFailure);
            Documents = new(Bytes);
            Sut = new ReadOriginalReport(
                Reader,
                new EvidenceQueries(ReceiptId, EvidenceId, assetId),
                new ReceiptQueries(ReceiptId, new(
                    assetId, "attachment 2: report.pdf", "report.pdf", "application/pdf",
                    IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment,
                    Bytes.Length, Hash, "report-key", null, null, null, null)),
                new ArtifactStore("report-key", storedBytes ?? Bytes),
                new Metadata(CaseId, OccurrenceId, VersionId),
                Documents,
                TimeProvider.System);
        }

        public Guid ReceiptId { get; } = Guid.NewGuid();
        public Guid EvidenceId { get; } = Guid.NewGuid();
        public Guid CaseId { get; } = Guid.NewGuid();
        public Guid OccurrenceId { get; } = Guid.NewGuid();
        public Guid VersionId { get; } = Guid.NewGuid();
        public SourceReader Reader { get; }
        public DocumentReader Documents { get; }
        public ReadOriginalReport Sut { get; }
    }

    private sealed class SourceReader(Exception? failure) : IIntakeSourceReader
    {
        public List<IntakeSource> Sources { get; } = [];

        public Task<IntakeSourceReadResult> ReadAsync(IntakeSource source, CancellationToken cancellationToken)
        {
            Sources.Add(source);
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult(new IntakeSourceReadResult(
                IntakeSourceReadStatus.Readable,
                [new(IntakeEvidenceSource.PdfContent, $"{source.FileName}, page 1", Report)],
                [],
                [],
                RequiresOcr: false));
        }
    }

    private sealed class EvidenceQueries(Guid receiptId, Guid evidenceId, Guid assetId)
        : IStandaloneAuditEvidenceQueries
    {
        public Task<StandaloneAuditEvidence?> GetForReceiptAsync(Guid intakeReceiptId, CancellationToken cancellationToken) =>
            Task.FromResult<StandaloneAuditEvidence?>(intakeReceiptId == receiptId
                ? new(evidenceId, receiptId, assetId, AuditAssessment.Repairable, Guid.Empty,
                    DateTimeOffset.UtcNow, "The retained original report states Repairable.", 0, false)
                : null);
    }

    private sealed class ReceiptQueries(Guid receiptId, IntakeAssetRecord asset) : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeAssetRecord?> GetAssetAsync(Guid receipt, Guid assetId, CancellationToken cancellationToken) =>
            Task.FromResult(receipt == receiptId && assetId == asset.Id ? asset : null);
    }

    private sealed class ArtifactStore(string storageKey, byte[] content) : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream value, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(key == storageKey ? content : null);
    }

    private sealed class Metadata(Guid caseId, Guid occurrenceId, Guid versionId) : IGetCaseDocumentMetadata
    {
        public Task<CaseDocumentMetadata?> ExecuteAsync(
            GetCaseDocumentMetadataQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<CaseDocumentMetadata?>(
                query.CaseId == caseId && query.OccurrenceId == occurrenceId && query.VersionId == versionId
                    ? new(caseId, occurrenceId, Guid.NewGuid(), versionId, "report.pdf", "application/pdf",
                        Bytes.Length, Hash)
                    : null);
    }

    private sealed class DocumentReader(byte[] content) : IReadLogicalDocumentVersion
    {
        public List<ReadLogicalDocumentVersionRequest> Requests { get; } = [];

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(content), request.DocumentId, request.VersionId, null, request.ExpectedSha256,
                content.Length, "report.pdf", "application/pdf"));
        }
    }
}
