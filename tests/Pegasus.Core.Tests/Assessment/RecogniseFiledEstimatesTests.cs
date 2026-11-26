using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

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

    /// <summary>
    /// A newly recognised Audatex estimate records what it states on its Case
    /// before its answer: the type its header gives, its VIN and its odometer
    /// (R1140, R1141). One that states nothing records nothing there.
    /// </summary>
    [Fact]
    public async Task AnEstimateRecordsWhatItStatesOnItsCaseBeforeItsAnswer()
    {
        var garage = Filed("Garage estimate.pdf", "GARAGE ESTIMATE");
        var plain = Filed("Audatex report.pdf", "ESTIMATE");
        var store = new Store([garage, plain]);

        var result = await Sweep(store).ExecuteAsync(2);

        Assert.Equal(new RecogniseFiledEstimatesResult(2, 2, 0, 0, null), result);
        var (candidate, reading) = Assert.Single(store.Readings);
        Assert.Equal(garage.Candidate, candidate);
        Assert.Equal(
            new FiledEstimateReading(DocumentSemanticRole.ExternalEstimate, "WVWZZZ1KZAW000001", 24534, "miles"),
            reading);
        Assert.Equal<(bool, string?)>(
            (true, EstimateFormats.AudatexProvider), store.Recorded[garage.Candidate.VersionId]);
    }

    /// <summary>
    /// An estimate recognised before is read again for its format alone,
    /// never for what it states (R1281).
    /// </summary>
    [Fact]
    public async Task AnEstimateReadAgainForItsFormatRecordsNothingOnItsCase()
    {
        var recognised = Filed("Garage estimate.pdf", "GARAGE ESTIMATE", isRecognised: true);
        var store = new Store([recognised]);

        await Sweep(store).ExecuteAsync(1);

        Assert.Empty(store.Readings);
        Assert.Equal(EstimateFormats.AudatexProvider, store.Providers[recognised.Candidate.VersionId]);
    }

    /// <summary>
    /// While staff edit the Case its reading waits: the version is deferred
    /// with no answer, so it is read again later and nothing is lost.
    /// </summary>
    [Fact]
    public async Task ACaseBeingEditedDefersTheEstimateWithNoAnswer()
    {
        var garage = Filed("Garage estimate.pdf", "GARAGE ESTIMATE");
        var store = new Store([garage]) { Editing = true };

        var result = await Sweep(store).ExecuteAsync(1);

        Assert.Equal(
            new RecogniseFiledEstimatesResult(1, 0, 0, 1, nameof(IntakeDependencyUnavailableException)), result);
        Assert.Equal(new[] { garage.Candidate.VersionId }, store.Deferred);
        Assert.Empty(store.Recorded);
    }

    /// <summary>
    /// A header naming Collision Engineers is a CE estimate, any other an
    /// External estimate, and none leaves the file as filed (R1140, R1281).
    /// </summary>
    [Theory]
    [InlineData("TEL: COLLISION ENGINEERS 01515590762 77-79 HOYLAKE ROAD", DocumentSemanticRole.CeEstimate)]
    [InlineData("Collision Engineers Ltd", DocumentSemanticRole.CeEstimate)]
    [InlineData("TEL: WILLOW BODYWORKS FAX: WANDLE WAY", DocumentSemanticRole.ExternalEstimate)]
    [InlineData("CITYGATE GROUP BODYSHOPS TEL: 020", DocumentSemanticRole.ExternalEstimate)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void TheHeaderGivesTheType(string? header, DocumentSemanticRole? expected) =>
        Assert.Equal(expected, FiledEstimateTypePolicy.HeaderType(header));

    /// <summary>
    /// Only a type nobody chose is set from the header: a mailed attachment
    /// as intake filed it (an Audatex estimate is not an Instruction, R1285),
    /// an upload left on Other (R1281) and the import's dropped file. A type
    /// changed after filing, one the Automation Actor or staff chose, and an
    /// original source or Audit report are kept.
    /// </summary>
    [Theory]
    [InlineData("mail-instruction", DocumentSource.Intake, DocumentSemanticRole.Instruction, null, false, true)]
    [InlineData("mail-correspondence", DocumentSource.ExternalCorrespondence, DocumentSemanticRole.Correspondence, null, false, true)]
    [InlineData("mail-changed", DocumentSource.Intake, DocumentSemanticRole.Correspondence, null, true, false)]
    [InlineData("mail-source", DocumentSource.Intake, DocumentSemanticRole.OriginalSource, null, false, false)]
    [InlineData("mail-audit-report", DocumentSource.Intake, DocumentSemanticRole.AuditReport, null, false, false)]
    [InlineData("upload-other", DocumentSource.StaffUpload, DocumentSemanticRole.Other, "upload:1", false, true)]
    [InlineData("upload-other-changed", DocumentSource.StaffUpload, DocumentSemanticRole.Other, "upload:1", true, false)]
    [InlineData("upload-chosen", DocumentSource.StaffUpload, DocumentSemanticRole.Correspondence, "upload:1", false, false)]
    [InlineData("upload-external", DocumentSource.StaffUpload, DocumentSemanticRole.ExternalEstimate, "upload:1", false, false)]
    [InlineData("import-drop", DocumentSource.StaffUpload, DocumentSemanticRole.ExternalEstimate, "estimate-import:key", false, true)]
    [InlineData("automation", DocumentSource.Automation, DocumentSemanticRole.Other, null, false, false)]
    [InlineData("generated", DocumentSource.Generated, DocumentSemanticRole.Other, null, false, false)]
    public void OnlyATypeNobodyChoseIsSetFromTheHeader(
        string name,
        DocumentSource source,
        DocumentSemanticRole role,
        string? identity,
        bool changedAfterFiling,
        bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, FiledEstimateTypePolicy.MayType(source, role, identity, changedAfterFiling));
    }

    /// <summary>
    /// Only an Audatex estimate's reading is recorded, a VIN the vocabulary
    /// cannot hold is dropped, and a reading of nothing is none.
    /// </summary>
    [Fact]
    public void OnlyAnAudatexEstimateStatingSomethingIsAReading()
    {
        ParsedEstimate Parsed(RepairSpecificationSourceRoute route, string? header, string? vin, long? mileage) =>
            new("1", [], "Provider", route, Vin: vin, Header: header, Mileage: mileage, MileageUnit: mileage is null ? null : "miles");

        Assert.Null(FiledEstimateReading.From(
            Parsed(RepairSpecificationSourceRoute.Json, "TEL: COLLISION ENGINEERS", null, 100)));
        Assert.Null(FiledEstimateReading.From(Parsed(RepairSpecificationSourceRoute.AudatexPdf, null, null, null)));
        Assert.Equal(
            new FiledEstimateReading(DocumentSemanticRole.CeEstimate, null, null, null),
            FiledEstimateReading.From(
                Parsed(RepairSpecificationSourceRoute.AudatexPdf, "TEL: COLLISION ENGINEERS", "WING!", null)));
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
            "GARAGE ESTIMATE" => new(
                "1", [], EstimateFormats.AudatexProvider, RepairSpecificationSourceRoute.AudatexPdf,
                Vin: "WVWZZZ1KZAW000001", Header: "TEL: WILLOW BODYWORKS", Mileage: 24534, MileageUnit: "miles"),
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

        /// <summary>What each newly recognised estimate recorded on its Case, in order.</summary>
        public List<(EstimateRecognitionCandidate Candidate, FiledEstimateReading Reading)> Readings { get; } = [];

        /// <summary>Whether a member of staff is editing every Case, so a reading waits.</summary>
        public bool Editing { get; init; }

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

        public Task RecordReadingAsync(
            EstimateRecognitionCandidate candidate,
            FiledEstimateReading reading,
            CancellationToken cancellationToken)
        {
            if (Editing)
            {
                throw new IntakeDependencyUnavailableException("Estimate recognition is waiting for the Case editor.");
            }
            Readings.Add((candidate, reading));
            return Task.CompletedTask;
        }

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
