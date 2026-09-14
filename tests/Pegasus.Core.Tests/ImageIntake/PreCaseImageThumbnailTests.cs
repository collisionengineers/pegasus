using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.ImageIntake;

/// <summary>
/// A pre-Case image's tile draws its recorded crop and rotation the way a Case
/// image's tile does; with nothing recorded the original is served and nothing
/// is read or rendered.
/// </summary>
public sealed class PreCaseImageThumbnailTests
{
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task APreparedImageRendersItsRecordedRotationAndCropFromTheAuthorisedRead()
    {
        var receiptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var crop = new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.6m);
        var fixture = new Fixture(new PreCaseImagePreparation(assetId, CaseAssetRotation.Clockwise90, crop, [], 3));

        await using var thumbnail = await fixture.Sut.OpenAsync(new PreCaseImageThumbnailQuery(receiptId, assetId, Staff));

        Assert.NotNull(thumbnail);
        Assert.Equal(CaseDocumentThumbnails.MediaType, thumbnail.MediaType);
        Assert.Equal("source-sha", thumbnail.SourceSha256);
        Assert.Equal((receiptId, assetId), fixture.Download.Read);
        Assert.Equal((CaseAssetRotation.Clockwise90, crop), fixture.Renderer.Rendered);
    }

    [Fact]
    public async Task AnUnpreparedImageIsServedWholeWithoutAReadOrARender()
    {
        var assetId = Guid.NewGuid();
        var none = new Fixture(null);
        var cleared = new Fixture(PreCaseImagePreparation.Original(assetId) with { Version = 2 });

        Assert.Null(await none.Sut.OpenAsync(new PreCaseImageThumbnailQuery(Guid.NewGuid(), assetId, Staff)));
        Assert.Null(await cleared.Sut.OpenAsync(new PreCaseImageThumbnailQuery(Guid.NewGuid(), assetId, Staff)));
        Assert.Null(none.Download.Read);
        Assert.Null(cleared.Renderer.Rendered);
    }

    [Fact]
    public async Task ANonImageOrAFailedRenderFallsBackToTheOriginal()
    {
        var assetId = Guid.NewGuid();
        var prepared = new PreCaseImagePreparation(assetId, CaseAssetRotation.Clockwise270, CaseAssetCrop.Full, [], 1);
        var svg = new Fixture(prepared, contentType: "image/svg+xml");
        var unrenderable = new Fixture(prepared, renders: false);

        Assert.Null(await svg.Sut.OpenAsync(new PreCaseImageThumbnailQuery(Guid.NewGuid(), assetId, Staff)));
        Assert.Null(svg.Renderer.Rendered);
        Assert.Null(await unrenderable.Sut.OpenAsync(new PreCaseImageThumbnailQuery(Guid.NewGuid(), assetId, Staff)));
    }

    [Fact]
    public async Task TheTileNeedsTheCaseworkRight()
    {
        var assetId = Guid.NewGuid();
        var fixture = new Fixture(new PreCaseImagePreparation(assetId, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full, [], 1));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => fixture.Sut.OpenAsync(
            new PreCaseImageThumbnailQuery(Guid.NewGuid(), assetId, ActionActor.SystemWorker("intake-processing"))));
        Assert.Null(fixture.Download.Read);
    }

    private sealed class Fixture
    {
        public Fixture(PreCaseImagePreparation? preparation, string contentType = "image/png", bool renders = true)
        {
            Download = new RecordingDownload(contentType);
            Renderer = new RecordingRenderer(renders);
            Sut = new ReadPreCaseImageThumbnail(Download, new Store(preparation), Renderer);
        }

        public RecordingDownload Download { get; }

        public RecordingRenderer Renderer { get; }

        public ReadPreCaseImageThumbnail Sut { get; }
    }

    private sealed class RecordingDownload(string contentType) : IDownloadIntakeAsset
    {
        public (Guid ReceiptId, Guid AssetId)? Read { get; private set; }

        public Task<IntakeSourceDownload?> ExecuteAsync(DownloadIntakeAssetQuery query, CancellationToken cancellationToken = default)
        {
            StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
            Read = (query.ReceiptId, query.AssetId);
            return Task.FromResult<IntakeSourceDownload?>(new IntakeSourceDownload(new byte[] { 1, 2, 3 }, "photo.png", contentType, 3, "source-sha"));
        }
    }

    private sealed class RecordingRenderer(bool renders) : IRenderImageThumbnail
    {
        public (CaseAssetRotation Rotation, CaseAssetCrop Crop)? Rendered { get; private set; }

        public Task<byte[]?> RenderAsync(ReadOnlyMemory<byte> content, CaseAssetRotation rotation, CaseAssetCrop crop, CancellationToken cancellationToken)
        {
            Rendered = (rotation, crop);
            return Task.FromResult(renders ? new byte[] { 0xFF, 0xD8 } : null);
        }
    }

    private sealed class Store(PreCaseImagePreparation? preparation) : IPreCaseImagePreparationStore
    {
        public Task<IReadOnlyDictionary<Guid, PreCaseImagePreparation>> ListAsync(IReadOnlyCollection<Guid> intakeAssetIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, PreCaseImagePreparation>>(
                preparation is null
                    ? new Dictionary<Guid, PreCaseImagePreparation>()
                    : new Dictionary<Guid, PreCaseImagePreparation> { [preparation.IntakeAssetId] = preparation });

        public Task<PreCaseImagePreparation> SaveCropAsync(SavePreCaseImageCropRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PreCaseImagePreparation> SetTagAsync(TagPreCaseImageRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
