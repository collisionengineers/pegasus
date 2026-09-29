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
        Assert.Equal($"/Received/{receipt.Id:D}/Asset/{confirmed.Id:D}", only.Url);
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
        new(
            Guid.NewGuid(),
            "example.pdf",
            "application/pdf",
            1024,
            "hash",
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
