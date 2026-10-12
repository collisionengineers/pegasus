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
        // An estimate is recorded with the format it was read in; no other
        // file has one.
        Assert.Equal<(bool, string?)>(
            (true, EstimateFormats.AudatexProvider), store.Recorded[estimate.Candidate.VersionId]);
        Assert.Equal((false, null), store.Recorded[letter.Candidate.VersionId]);
        Assert.Equal((false, null), store.Recorded[photograph.Candidate.VersionId]);
        Assert.Equal((false, null), store.Recorded[generated.Candidate.VersionId]);
        Assert.Empty(store.Providers);
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

    /// <summary>
    /// An estimate recognised before its format was kept is read once more
    /// for its format alone (operator, 6 October 2026); whether it is an
    /// estimate is not recorded again.
    /// </summary>
    [Fact]
    public async Task ARecognisedEstimateIsReadOnceMoreForItsFormatAlone()
    {
        var recognised = Filed("Audatex report.pdf", "ESTIMATE", isRecognised: true);
        var store = new Store([recognised]);

        var result = await Sweep(store).ExecuteAsync(1);

        Assert.Equal(new RecogniseFiledEstimatesResult(1, 1, 0, 0, null), result);
        Assert.Equal(EstimateFormats.AudatexProvider, store.Providers[recognised.Candidate.VersionId]);
        Assert.Empty(store.Recorded);
        Assert.Empty(store.Deferred);
    }

    /// <summary>
    /// A recognised estimate whose format definitely cannot be determined
    /// (its version is gone, its bytes are not the version recorded, every
    /// format rejects it, or none names it any more) has its format recorded
    /// as empty, so it is never listed again. It is never deferred and never
    /// recorded as no estimate.
    /// </summary>
    [Fact]
    public async Task ARecognisedEstimateWhoseFormatCannotBeDeterminedIsRecordedAsNotDetermined()
    {
        var missing = Filed("Missing.pdf", "ESTIMATE", isRecognised: true);
        var altered = Filed("Altered.pdf", "ESTIMATE", isRecognised: true);
        altered = altered with { Candidate = altered.Candidate with { Sha256 = new string('0', 64) } };
        var unparsed = Filed("Letter.pdf", "Dear Engineer", isRecognised: true);
        var unnamed = Filed("front.jpg", "jpeg", mediaType: "image/jpeg", isRecognised: true);
        var store = new Store([missing, altered, unparsed, unnamed], missing: missing.Candidate.VersionId);

        var result = await Sweep(store).ExecuteAsync(4);

        Assert.Equal(new RecogniseFiledEstimatesResult(4, 0, 0, 4, nameof(FileNotFoundException)), result);
        Assert.All(
            new[] { missing, altered, unparsed, unnamed },
            item => Assert.Equal(string.Empty, store.Providers[item.Candidate.VersionId]));
        Assert.Empty(store.Recorded);
        Assert.Empty(store.Deferred);
    }

    /// <summary>
    /// A re-read that fails in a way that may pass (a failed read or a parser
    /// fault) records nothing: the version is deferred, exactly as a first
    /// read is, and read again on a later run.
    /// </summary>
    [Fact]
    public async Task ARecognisedEstimateWhoseReadMayPassIsDeferredAndRecordsNothing()
    {
        var unreadable = Filed("Unreadable.pdf", "ESTIMATE", isRecognised: true);
        var faulting = Filed("Faulting.pdf", "FAULT", isRecognised: true);
        var store = new Store([unreadable, faulting], failing: unreadable.Candidate.VersionId);

        var result = await Sweep(store).ExecuteAsync(2);

        Assert.Equal(new RecogniseFiledEstimatesResult(2, 0, 0, 2, nameof(IOException)), result);
        Assert.Equal(new[] { unreadable.Candidate.VersionId, faulting.Candidate.VersionId }, store.Deferred);
        Assert.Empty(store.Providers);
        Assert.Empty(store.Recorded);
    }

    private static RecogniseFiledEstimates Sweep(Store store) => new(store, store, [new PdfParser()]);

    private sealed record FiledVersion(EstimateRecognitionCandidate Candidate, byte[] Content);

    /// <summary>A filed version whose stored bytes are <paramref name="text"/>.</summary>
    private static FiledVersion Filed(
        string fileName, string text, string mediaType = "application/pdf", bool isGenerated = false,
        bool isRecognised = false)
    {
        var content = Encoding.ASCII.GetBytes(text);
        return new(
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fileName, mediaType, content.Length,
                Convert.ToHexStringLower(SHA256.HashData(content)), isGenerated, isRecognised),
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
            "ESTIMATE" => new("1", [], EstimateFormats.AudatexProvider, RepairSpecificationSourceRoute.AudatexPdf),
            "FAULT" => throw new InvalidOperationException("The parser failed."),
            _ => throw new EstimateParseRejectedException("The PDF does not identify exactly one supported estimate provider."),
        };
    }

    /// <summary>The candidate list and the custody read over the same filed versions.</summary>
    private sealed class Store(IReadOnlyList<FiledVersion> filed, Guid? failing = null, Guid? missing = null)
        : IEstimateRecognitionCandidates, IReadLogicalDocumentVersion
    {
        public Dictionary<Guid, (bool IsEstimate, string? Provider)> Recorded { get; } = [];

        /// <summary>The formats recorded for versions already recognised.</summary>
        public Dictionary<Guid, string> Providers { get; } = [];

        public List<Guid> Deferred { get; } = [];

        public List<ReadLogicalDocumentVersionRequest> Reads { get; } = [];

        public Task<IReadOnlyList<EstimateRecognitionCandidate>> ListAsync(
            int maximumItems,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EstimateRecognitionCandidate>>(
                [.. filed.Take(maximumItems).Select(item => item.Candidate)]);

        public Task RecordAsync(
            Guid versionId, bool isEstimate, string? provider, CancellationToken cancellationToken)
        {
            Recorded.Add(versionId, (isEstimate, provider));
            return Task.CompletedTask;
        }

        public Task RecordProviderAsync(Guid versionId, string provider, CancellationToken cancellationToken)
        {
            Providers.Add(versionId, provider);
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
            if (request.VersionId == missing)
            {
                throw new FileNotFoundException("The exact managed Box version is unavailable.");
            }
            var item = filed.Single(value => value.Candidate.VersionId == request.VersionId);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(item.Content), request.DocumentId, request.VersionId, null, request.ExpectedSha256,
                item.Content.Length, item.Candidate.FileName, item.Candidate.MediaType));
        }
    }
}
