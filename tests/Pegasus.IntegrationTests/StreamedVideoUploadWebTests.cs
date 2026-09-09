using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

public sealed partial class MultiFormatIntakeWebTests
{
    [Theory]
    [InlineData("evidence.mp4", "video/mp4", "isom")]
    [InlineData("evidence.mov", "video/quicktime", "qt  ")]
    public async Task LargerVideoUploadsRetainOriginalBytesWithoutDocumentExtraction(
        string fileName, string mediaType, string brand)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        // Synthetic container header exercises admission and retention, not playback.
        var bytes = new byte[(2 * 1024 * 1024) + 17];
        bytes[3] = 24;
        "ftyp"u8.CopyTo(bytes.AsSpan(4));
        System.Text.Encoding.ASCII.GetBytes(brand).CopyTo(bytes, 8);

        var result = await UploadAsync(factory, client, fileName, mediaType, bytes);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Null(receipt.FailureCode);
        Assert.Null(receipt.InstructionDraft);
        Assert.Empty(receipt.Fields);
        Assert.Equal(bytes.LongLength, receipt.SourceLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), receipt.SourceHash);
        Assert.Contains(receipt.Evidence, item => item.Signal == "video-review-required");
        var asset = Assert.Single(receipt.AssetRecords, item => item.Kind == IntakeAssetKind.Source);
        await using var scope = factory.Services.CreateAsyncScope();
        var retained = await scope.ServiceProvider.GetRequiredService<IIntakeArtifactStore>()
            .ReadAsync(asset.StorageKey, CancellationToken.None);
        Assert.NotNull(retained);
        Assert.Equal(bytes, retained.Value.ToArray());
    }
}
