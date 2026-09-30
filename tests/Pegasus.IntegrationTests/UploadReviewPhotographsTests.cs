using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The upload review lists a photograph pulled out of a document only once
/// custody can serve it, and asks the page to look again while one is still
/// waiting. Listing it earlier drew a broken image: the asset address answers
/// 409 until custody has confirmed the bytes.
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

    [Fact]
    public void AnImageFileIsAddressedByItsSourceHashAndAnOutcomeImageByItsOwn()
    {
        var image = Receipt("image/png", "A");
        var outcomeReceiptId = Guid.NewGuid();
        var outcome = new UploadOutcomeView(
            UploadOutcomeKind.Working,
            "Processing",
            "The file is being processed.",
            null,
            null,
            ThumbnailReceiptId: outcomeReceiptId,
            ThumbnailContentHash: new string('B', 64));

        Assert.Equal(
            $"/Received/{image.Id:D}/Image?v={new string('A', 64)}",
            UploadReviewFile.ImageUrlOf(null, image));
        Assert.Equal(
            $"/Received/{outcomeReceiptId:D}/Image?v={new string('B', 64)}",
            UploadReviewFile.ImageUrlOf(outcome, image));
        Assert.Null(UploadReviewFile.ImageUrlOf(null, Receipt("application/pdf", "C")));
        Assert.Null(UploadReviewFile.ImageUrlOf(null, null));
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

        Assert.Equal(expected, UploadReviewFile.AwaitsPhotographs(receipt));
    }

    [Fact]
    public void NoReceiptListsNothingAndAwaitsNothing()
    {
        Assert.Empty(UploadReviewFile.PhotographsOf(null));
        Assert.False(UploadReviewFile.AwaitsPhotographs(null));
    }

    private static IntakeAssetRecord Photograph(string hashDigit, IncomingArtifactCustodyState state) =>
        new(
            Guid.NewGuid(),
            "page 1, image",
            $"page-1-image-{hashDigit}.jpg",
            "image/jpeg",
            IntakeAssetKind.EmbeddedImage,
            IntakeAssetDisposition.Embedded,
            InstructionEvidenceImages.EmbeddedPhotographMinimumBytes + 1,
            new string(hashDigit[0], 64),
            "test-storage-key-" + hashDigit,
            1,
            null,
            1200,
            900,
            state);

    private static IntakeReceipt Receipt(params IntakeAssetRecord[] assets) =>
        Receipt("application/pdf", "hash", assets);

    private static IntakeReceipt Receipt(string mediaType, string hashDigit) =>
        Receipt(mediaType, new string(hashDigit[0], 64), []);

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
