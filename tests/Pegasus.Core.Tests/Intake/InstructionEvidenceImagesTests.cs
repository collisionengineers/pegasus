using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class InstructionEvidenceImagesTests
{
    [Fact]
    public void SelectsAttachedImagesAndLargeEmbeddedImagesOnly()
    {
        var attachedPhoto = Asset(IntakeAssetKind.Attachment, "image/jpeg", 90_000, "AA");
        var attachedPdf = Asset(IntakeAssetKind.Attachment, "application/pdf", 90_000, "BB");
        var embeddedPhoto = Asset(IntakeAssetKind.EmbeddedImage, "image/jpeg", 60_000, "CC");
        var letterheadArt = Asset(IntakeAssetKind.EmbeddedImage, "image/png", 4_039, "DD");
        var inlineSignature = Asset(IntakeAssetKind.InlineImage, "image/png", 90_000, "EE");
        var source = Asset(IntakeAssetKind.Source, "message/rfc822", 90_000, "FF");

        var selected = InstructionEvidenceImages.Select(
            [source, inlineSignature, letterheadArt, embeddedPhoto, attachedPdf, attachedPhoto]);

        Assert.Equal([attachedPhoto.Id, embeddedPhoto.Id], selected.Select(item => item.Id));
    }

    [Fact]
    public void TheThresholdIsABoundaryNotAGuess()
    {
        var atFloor = Asset(
            IntakeAssetKind.EmbeddedImage,
            "image/jpeg",
            InstructionEvidenceImages.EmbeddedPhotographMinimumBytes,
            "AA");
        var underFloor = Asset(
            IntakeAssetKind.EmbeddedImage,
            "image/jpeg",
            InstructionEvidenceImages.EmbeddedPhotographMinimumBytes - 1,
            "BB");

        var selected = InstructionEvidenceImages.Select([atFloor, underFloor]);

        Assert.Equal([atFloor.Id], selected.Select(item => item.Id));
    }

    [Fact]
    public void OnePhotographCarriedTwiceAppearsOncePreferringTheAttachedCopy()
    {
        var attached = Asset(IntakeAssetKind.Attachment, "image/jpeg", 90_000, "AA", "damage.jpg");
        var embeddedCopy = Asset(IntakeAssetKind.EmbeddedImage, "image/jpeg", 90_000, "aa", "page-1-image-1.jpg");
        var repeatedAcrossPages = Asset(IntakeAssetKind.EmbeddedImage, "image/jpeg", 90_000, "aa", "page-2-image-1.jpg");

        var selected = InstructionEvidenceImages.Select(
            [embeddedCopy, repeatedAcrossPages, attached]);

        var only = Assert.Single(selected);
        Assert.Equal(attached.Id, only.Id);
    }

    [Fact]
    public void QdosTwentySixZeroZeroEightsLetterheadBannersAreNotEvidence()
    {
        // INTK-030, measured from production. The operator reported the
        // first two images as signatures/logos. These are those two, at
        // their real sizes and dimensions, beside one of the nine genuine
        // photographs from the same receipt. Note the byte floor admits
        // both banners and one of them is a JPEG, so neither size nor
        // format could have told them apart — only the shape does.
        var pngBanner = Asset(
            IntakeAssetKind.EmbeddedImage, "image/png", 110_783, "b1",
            "page-1-image-1.png", width: 1990, height: 437);
        var jpegBanner = Asset(
            IntakeAssetKind.EmbeddedImage, "image/jpeg", 77_972, "b2",
            "page-1-image-2.jpg", width: 2214, height: 248);
        var photograph = Asset(
            IntakeAssetKind.EmbeddedImage, "image/jpeg", 156_740, "p1",
            "page-2-image-3.jpg", width: 709, height: 646);

        var selected = InstructionEvidenceImages.Select(
            [pngBanner, jpegBanner, photograph]);

        var only = Assert.Single(selected);
        Assert.Equal(photograph.Id, only.Id);
    }

    [Fact]
    public void AnImageWithNoRecordedDimensionsIsStillAdmitted()
    {
        // Failing open is deliberate: refusing to show a genuine
        // photograph is the worse of the two errors.
        var unmeasured = Asset(
            IntakeAssetKind.EmbeddedImage, "image/jpeg", 90_000, "u1");

        Assert.Single(InstructionEvidenceImages.Select([unmeasured]));
    }

    // ---- A05: exact metadata and authorization at the stream boundary ----

    /// <summary>
    /// A connector is told exactly what it is about to fetch — identity, size,
    /// hash, and which receipt version the answer belongs to — and is told none
    /// of it without the right to read the bytes themselves.
    /// </summary>
    [Fact]
    public async Task SourceMetadataNamesTheExactFileAndCarriesNoStorageKey()
    {
        var harness = new DownloadHarness();

        var metadata = await new GetIntakeSourceMetadata(harness.Receipts).ExecuteAsync(
            new(harness.Receipt.Id, StaffActor));

        Assert.NotNull(metadata);
        Assert.Equal(harness.Receipt.Id, metadata!.ReceiptId);
        Assert.Equal(harness.Receipt.Version, metadata.ReceiptVersion);
        Assert.Equal(harness.SourceAsset.Id, metadata.AssetId);
        Assert.Equal("instruction.pdf", metadata.FileName);
        Assert.Equal("application/pdf", metadata.MediaType);
        Assert.Equal(DownloadHarness.SourceBytes.Length, metadata.ContentLength);
        Assert.Equal(DownloadHarness.SourceHash, metadata.Sha256);
        Assert.Equal(0, metadata.Occurrence);

        // Nothing on the record names a storage location: a connector holding
        // one could read the artifact store outside every check on this
        // boundary, and keep reading it after the receipt moved on.
        Assert.DoesNotContain(
            "storage",
            System.Text.Json.JsonSerializer.Serialize(metadata),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssetMetadataNumbersEachRetainedFileWithinItsReceipt()
    {
        var harness = new DownloadHarness();

        var attachment = await new GetIntakeAssetMetadata(harness.Receipts).ExecuteAsync(
            new(harness.Receipt.Id, harness.AttachmentAsset.Id, StaffActor));

        Assert.NotNull(attachment);
        Assert.Equal(1, attachment!.Occurrence);
        Assert.Equal(harness.AttachmentAsset.ContentHash, attachment.Sha256);
        Assert.Equal(harness.Receipt.Version, attachment.ReceiptVersion);
    }

    [Fact]
    public async Task MetadataAndBytesRefuseTheSameActorsAtTheSameBoundary()
    {
        var harness = new DownloadHarness();
        ActionActor[] forbidden =
        [
            ActionActor.SystemWorker("intake-processing"),
            ActionActor.RequestLink(Guid.NewGuid()),
            ActionActor.Provider(Guid.NewGuid())
        ];

        foreach (var actor in forbidden)
        {
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                new GetIntakeSourceMetadata(harness.Receipts).ExecuteAsync(
                    new(harness.Receipt.Id, actor)));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                new GetIntakeAssetMetadata(harness.Receipts).ExecuteAsync(
                    new(harness.Receipt.Id, harness.SourceAsset.Id, actor)));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                harness.Download().ExecuteAsync(
                    new(harness.Receipt.Id, harness.SourceAsset.Id, actor)));
        }
    }

    /// <summary>
    /// The Automation Actor is the connector's identity. ADR-0011 grants it the
    /// ordinary operational casework surface, which is exactly this boundary.
    /// </summary>
    [Fact]
    public async Task TheAutomationActorMayReadMetadataAndBytes()
    {
        var harness = new DownloadHarness();
        var connector = ActionActor.Automation("intake-connector");

        Assert.NotNull(await new GetIntakeSourceMetadata(harness.Receipts).ExecuteAsync(
            new(harness.Receipt.Id, connector)));
        var download = await harness.Download().ExecuteAsync(
            new(harness.Receipt.Id, harness.SourceAsset.Id, connector));
        Assert.Equal(DownloadHarness.SourceHash, download!.Sha256);
    }

    [Fact]
    public async Task BytesAreServedThroughTheLogicalReaderWhenItIsComposed()
    {
        var harness = new DownloadHarness();
        var reader = new RecordingLogicalReader();

        var download = await harness.Download(reader).ExecuteAsync(
            new(harness.Receipt.Id, harness.SourceAsset.Id, StaffActor));

        Assert.Equal(DownloadHarness.SourceHash, download!.Sha256);
        Assert.Equal(0, harness.Artifacts.Reads);
        var request = Assert.Single(reader.Requests);
        Assert.Equal(harness.SourceAsset.Id, request.IntakeAssetId);
        Assert.Equal(harness.Receipt.Id, request.IntakeReceiptId);
        Assert.Equal(DownloadHarness.SourceHash, request.ExpectedSha256);
        Assert.Equal(DownloadHarness.SourceBytes.Length, request.ExpectedContentLength);
    }

    [Fact]
    public async Task ContentThatDoesNotMatchTheRecordedHashIsRefused()
    {
        var harness = new DownloadHarness();
        harness.Artifacts.Corrupt = true;

        await Assert.ThrowsAsync<IntakeArtifactIntegrityException>(() =>
            harness.Download().ExecuteAsync(
                new(harness.Receipt.Id, harness.SourceAsset.Id, StaffActor)));
    }

    [Fact]
    public async Task AnAssetOfAnotherReceiptIsNeverServedUnderThisOne()
    {
        var harness = new DownloadHarness();

        Assert.Null(await harness.Download().ExecuteAsync(
            new(harness.Receipt.Id, Guid.NewGuid(), StaffActor)));
        Assert.Null(await new GetIntakeAssetMetadata(harness.Receipts).ExecuteAsync(
            new(harness.Receipt.Id, Guid.NewGuid(), StaffActor)));
    }

    private static readonly ActionActor StaffActor =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class DownloadHarness
    {
        public static readonly byte[] SourceBytes = Encoding.UTF8.GetBytes("QDOS instruction");
        public static readonly string SourceHash =
            Convert.ToHexString(SHA256.HashData(SourceBytes));
        private static readonly byte[] AttachmentBytes = Encoding.UTF8.GetBytes("photo");
        private static readonly string AttachmentHash =
            Convert.ToHexString(SHA256.HashData(AttachmentBytes));

        public DownloadHarness()
        {
            SourceAsset = new(
                Guid.NewGuid(),
                "uploaded instruction.pdf",
                "instruction.pdf",
                "application/pdf",
                IntakeAssetKind.Source,
                IntakeAssetDisposition.Source,
                SourceBytes.Length,
                SourceHash,
                "storage/source",
                null, null, null, null);
            AttachmentAsset = new(
                Guid.NewGuid(),
                "uploaded instruction.pdf, attachment 1: photo.jpg",
                "photo.jpg",
                "image/jpeg",
                IntakeAssetKind.Attachment,
                IntakeAssetDisposition.Attachment,
                AttachmentBytes.Length,
                AttachmentHash,
                "storage/photo",
                null, null, null, null);
            Receipt = BuildReceipt([SourceAsset, AttachmentAsset]);
            Receipts = new FakeReceiptQueries(Receipt);
            Artifacts = new FakeArtifactStore(new()
            {
                ["storage/source"] = SourceBytes,
                ["storage/photo"] = AttachmentBytes
            });
        }

        public IntakeAssetRecord SourceAsset { get; }

        public IntakeAssetRecord AttachmentAsset { get; }

        public IntakeReceipt Receipt { get; }

        public FakeReceiptQueries Receipts { get; }

        public FakeArtifactStore Artifacts { get; }

        public DownloadIntakeAsset Download(IReadLogicalDocumentVersion? reader = null) =>
            new(Receipts, Artifacts, reader);

        private static IntakeReceipt BuildReceipt(IntakeAssetRecord[] assets) =>
            new(
                Guid.NewGuid(),
                "instruction.pdf",
                "application/pdf",
                SourceBytes.Length,
                SourceHash,
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    Guid.NewGuid().ToString("N")),
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                IntakeDecision.NeedsSorting,
                "Recorded by the pipeline.",
                [], [], null, [], null, null, false,
                "intake_source_reader", "1", null, null,
                assets,
                Version: 7);
    }

    private sealed class RecordingLogicalReader : IReadLogicalDocumentVersion
    {
        public List<ReadLogicalDocumentVersionRequest> Requests { get; } = [];

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(DownloadHarness.SourceBytes, writable: false),
                null,
                null,
                request.IntakeAssetId,
                DownloadHarness.SourceHash,
                DownloadHarness.SourceBytes.Length,
                "instruction.pdf",
                "application/pdf"));
        }
    }

    private sealed class FakeArtifactStore(Dictionary<string, byte[]> content) : IIntakeArtifactStore
    {
        public bool Corrupt { get; set; }

        public int Reads { get; private set; }

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken)
        {
            Reads++;
            if (!content.TryGetValue(storageKey, out var bytes))
            {
                return Task.FromResult<ReadOnlyMemory<byte>?>(null);
            }

            return Task.FromResult<ReadOnlyMemory<byte>?>(
                Corrupt ? Encoding.UTF8.GetBytes("tampered") : bytes);
        }
    }

    private sealed class FakeReceiptQueries(IntakeReceipt receipt) : IIntakeReceiptQueries
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
            Task.FromResult(id == receipt.Id ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }

    private static IntakeAssetRecord Asset(
        IntakeAssetKind kind,
        string mediaType,
        long contentLength,
        string hash,
        string fileName = "asset.bin",
        int? width = null,
        int? height = null) => new(
        Guid.NewGuid(),
        "test",
        fileName,
        mediaType,
        kind,
        kind switch
        {
            IntakeAssetKind.Source => IntakeAssetDisposition.Source,
            IntakeAssetKind.Attachment => IntakeAssetDisposition.Attachment,
            IntakeAssetKind.InlineImage => IntakeAssetDisposition.Inline,
            _ => IntakeAssetDisposition.Embedded
        },
        contentLength,
        hash,
        $"storage/{hash}",
        null,
        null,
        width,
        height);
}
