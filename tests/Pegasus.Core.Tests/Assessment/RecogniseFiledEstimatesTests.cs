using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The Worker reads each filed version once for whether it is an estimate:
/// the import's own parse decides, a PDF's name never does, and a read that
/// fails for now records nothing.
/// </summary>
public sealed class RecogniseFiledEstimatesTests
{
    [Fact]
    public async Task OnlyAFileTheImportWouldReadIsRecordedAsAnEstimate()
    {
        var estimate = Filed("Audatex report.pdf", "ESTIMATE");
        var letter = Filed("37765_1_LtrtoEngineerIn.pdf", "Dear Engineer");
        var photograph = Filed("front.jpg", "jpeg", mediaType: "image/jpeg");
        var generated = Filed("QDOS report.pdf", "ESTIMATE", isGenerated: true);
        var store = new Store([estimate, letter, photograph, generated]);

        var result = await Sweep(store).ExecuteAsync(4);

        Assert.Equal(new RecogniseFiledEstimatesResult(4, 1, 3, 0, null), result);
        Assert.True(store.Recorded[estimate.Candidate.VersionId]);
        Assert.False(store.Recorded[letter.Candidate.VersionId]);
        Assert.False(store.Recorded[photograph.Candidate.VersionId]);
        Assert.False(store.Recorded[generated.Candidate.VersionId]);
        // Only the PDFs a format names and Pegasus did not generate are read,
        // as the system worker.
        Assert.Equal(
            new[] { estimate.Candidate.VersionId, letter.Candidate.VersionId },
            store.Reads.Select(request => request.VersionId!.Value));
        Assert.All(store.Reads, request => Assert.Equal(ActorKind.SystemWorker, request.Actor.Kind));
        Assert.Empty(store.Deferred);
    }

    [Fact]
    public async Task AFailedReadOrAParserFaultRecordsNothingAndTheRestStillRun()
    {
        var unreadable = Filed("Unreadable.pdf", "ESTIMATE");
        var faulting = Filed("Faulting.pdf", "FAULT");
        var estimate = Filed("Audatex report.pdf", "ESTIMATE");
        var store = new Store([unreadable, faulting, estimate], failing: unreadable.Candidate.VersionId);

        var result = await Sweep(store).ExecuteAsync(3);

        Assert.Equal(new RecogniseFiledEstimatesResult(3, 1, 0, 2, nameof(IOException)), result);
        Assert.Equal(new[] { unreadable.Candidate.VersionId, faulting.Candidate.VersionId }, store.Deferred);
        Assert.Equal(new[] { estimate.Candidate.VersionId }, store.Recorded.Keys);
    }

    [Fact]
    public async Task BytesThatAreNotTheRecordedVersionAreNotAnAnswer()
    {
        var estimate = Filed("Audatex report.pdf", "ESTIMATE");
        var store = new Store([estimate with { Candidate = estimate.Candidate with { Sha256 = new string('0', 64) } }]);

        var result = await Sweep(store).ExecuteAsync(1);

        Assert.Equal(1, result.Failures);
        Assert.Empty(store.Recorded);
        Assert.Single(store.Deferred);
    }

    [Fact]
    public async Task ACancelledSweepStops()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var store = new Store([Filed("Audatex report.pdf", "ESTIMATE")]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sweep(store).ExecuteAsync(1, cancellation.Token));
        Assert.Empty(store.Recorded);
    }

    private static RecogniseFiledEstimates Sweep(Store store) => new(store, store, [new PdfParser()]);

    private sealed record FiledVersion(EstimateRecognitionCandidate Candidate, byte[] Content);

    /// <summary>A filed version whose stored bytes are <paramref name="text"/>.</summary>
    private static FiledVersion Filed(
        string fileName, string text, string mediaType = "application/pdf", bool isGenerated = false)
    {
        var content = Encoding.ASCII.GetBytes(text);
        return new(
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fileName, mediaType, content.Length,
                Convert.ToHexStringLower(SHA256.HashData(content)), isGenerated),
            content);
    }

    /// <summary>Names every PDF, as the real PDF container does; reads only "ESTIMATE".</summary>
    private sealed class PdfParser : IEstimateDocumentParser
    {
        public IReadOnlyList<string> FileExtensions { get; } = [".pdf"];

        public bool CanParse(string fileName, string mediaType) =>
            string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

        public ParsedEstimate Parse(ReadOnlyMemory<byte> content) => Encoding.ASCII.GetString(content.Span) switch
        {
            "ESTIMATE" => new("1", [], "Audatex", RepairSpecificationSourceRoute.AudatexPdf),
            "FAULT" => throw new InvalidOperationException("The parser failed."),
            _ => throw new EstimateParseRejectedException("The PDF does not identify exactly one supported estimate provider."),
        };
    }

    /// <summary>The candidate list and the custody read over the same filed versions.</summary>
    private sealed class Store(IReadOnlyList<FiledVersion> filed, Guid? failing = null)
        : IEstimateRecognitionCandidates, IReadLogicalDocumentVersion
    {
        public Dictionary<Guid, bool> Recorded { get; } = [];

        public List<Guid> Deferred { get; } = [];

        public List<ReadLogicalDocumentVersionRequest> Reads { get; } = [];

        public Task<IReadOnlyList<EstimateRecognitionCandidate>> ListAsync(
            int maximumItems,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EstimateRecognitionCandidate>>(
                [.. filed.Take(maximumItems).Select(item => item.Candidate)]);

        public Task RecordAsync(Guid versionId, bool isEstimate, CancellationToken cancellationToken)
        {
            Recorded.Add(versionId, isEstimate);
            return Task.CompletedTask;
        }

        public void Defer(Guid versionId) => Deferred.Add(versionId);

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads.Add(request);
            if (request.VersionId == failing)
            {
                throw new IOException("Box said later.");
            }
            var item = filed.Single(value => value.Candidate.VersionId == request.VersionId);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(item.Content), request.DocumentId, request.VersionId, null, request.ExpectedSha256,
                item.Content.Length, item.Candidate.FileName, item.Candidate.MediaType));
        }
    }
}
