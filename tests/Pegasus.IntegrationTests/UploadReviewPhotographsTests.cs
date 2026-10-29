using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The upload review draws an uploaded image at once, from the bytes intake
/// holds (operator, 28 October 2026, R463). A photograph pulled out of a
/// document is listed only once custody can serve it, and the page looks again
/// while one is still waiting: the asset address answers 409 until custody has
/// confirmed the bytes.
/// </summary>
public sealed class UploadReviewPhotographsTests
{
    [Fact]
    public void PendingPhotographIsNotListedUntilCustodyConfirmsIt()
    {
        var pending = Photograph("a", IncomingArtifactCustodyState.Pending);
        var confirmed = Photograph("b", IncomingArtifactCustodyState.Confirmed);
        var receipt = Receipt(pending, confirmed);

        var listed = UploadReviewFile.PhotographsOf(receipt);

        var only = Assert.Single(listed);
        Assert.Equal(confirmed.Id, only.AssetId);
        // The address names the photograph's content hash, so the browser keeps
        // it while the page looks again every few seconds.
        Assert.Equal(
            $"/Received/{receipt.Id:D}/Asset/{confirmed.Id:D}?v={confirmed.ContentHash}",
            only.Url);
    }

    /// <summary>
    /// An uploaded image is addressed from the first render by its staged
    /// receipt and content hash, whatever custody says; a document is not drawn.
    /// </summary>
    [Fact]
    public void AnUploadedImageIsAddressedFromTheFirstRenderByItsHash()
    {
        var stagedReceiptId = Guid.NewGuid();
        var hash = new string('A', 64);

        Assert.Equal(
            $"/Upload/Image/{stagedReceiptId:D}?v={hash}",
            UploadReviewFile.ImageUrlOf(stagedReceiptId, "image/png", hash));
        Assert.Equal(
            $"/Upload/Image/{stagedReceiptId:D}",
            UploadReviewFile.ImageUrlOf(stagedReceiptId, "image/jpeg", null));
        Assert.Null(UploadReviewFile.ImageUrlOf(stagedReceiptId, "application/pdf", hash));
        Assert.Null(UploadReviewFile.ImageUrlOf(stagedReceiptId, null, hash));
    }

    [Theory]
    [InlineData(IncomingArtifactCustodyState.Pending, true)]
    [InlineData(IncomingArtifactCustodyState.Confirmed, false)]
    [InlineData(IncomingArtifactCustodyState.Failed, false)]
    [InlineData(IncomingArtifactCustodyState.Unknown, false)]
    public void PageLooksAgainOnlyWhilePhotographCustodyIsPending(
        IncomingArtifactCustodyState state,
        bool expected)
    {
        var receipt = Receipt(Photograph("a", state));

        Assert.Equal(expected, UploadReviewFile.AwaitsCustody(receipt));
    }

    /// <summary>An uploaded image file is drawn from intake bytes, so its custody holds nothing back.</summary>
    [Theory]
    [InlineData(IncomingArtifactCustodyState.Pending)]
    [InlineData(IncomingArtifactCustodyState.Confirmed)]
    [InlineData(IncomingArtifactCustodyState.Failed)]
    [InlineData(IncomingArtifactCustodyState.Unknown)]
    public void ASourceFileNeverHoldsThePageBack(IncomingArtifactCustodyState state)
    {
        Assert.False(UploadReviewFile.AwaitsCustody(Receipt("image/png", "A", state)));
        Assert.False(UploadReviewFile.AwaitsCustody(Receipt("application/pdf", "B", state)));
    }

    [Fact]
    public void NoReceiptListsNothingAndAwaitsNothing()
    {
        Assert.Empty(UploadReviewFile.PhotographsOf(null));
        Assert.False(UploadReviewFile.AwaitsCustody(null));
    }

    private static IntakeAssetRecord Photograph(string hashDigit, IncomingArtifactCustodyState state) =>
        new(
            Guid.NewGuid(),
            "page 1, image",
            $"page-1-image-{hashDigit}.jpg",
            "image/jpeg",
            IntakeAssetKind.EmbeddedImage,
            IntakeAssetDisposition.Embedded,
            50_000,
            new string(hashDigit[0], 64),
            "test-storage-key-" + hashDigit,
            1,
            null,
            1200,
            900,
            state);

    private static IntakeReceipt Receipt(params IntakeAssetRecord[] assets) =>
        Receipt("application/pdf", "hash", assets);

    private static IntakeReceipt Receipt(string mediaType, string hashDigit, IncomingArtifactCustodyState sourceCustody)
    {
        var hash = new string(hashDigit[0], 64);
        return Receipt(mediaType, hash, [
            new IntakeAssetRecord(
                Guid.NewGuid(),
                "source",
                "upload",
                mediaType,
                IntakeAssetKind.Source,
                IntakeAssetDisposition.Source,
                1024,
                hash,
                "test-storage-key-source",
                null,
                null,
                null,
                null,
                sourceCustody)
        ]);
    }

    private static IntakeReceipt Receipt(string mediaType, string sourceHash, IntakeAssetRecord[] assets) =>
        new(
            Guid.NewGuid(),
            "example.pdf",
            mediaType,
            1024,
            sourceHash,
            new(IntakeSourceChannel.ManualUpload, "token"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            IntakeDecision.NeedsSorting,
            "reason",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "reader",
            "1",
            null,
            null,
            Assets: assets);
}
