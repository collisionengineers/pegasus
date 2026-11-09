using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// A received photograph stands on its Case under the intake asset it came
/// from, filed by itself or folded in with a Vehicle images record, and the
/// Case page reads the asset back from either to keep a photo link's story
/// (FRD-28, FRD-19).
/// </summary>
public sealed class IntakeImageOccurrenceIdentityTests
{
    [Fact]
    public void AFoldedPhotographsIdentityReadsBackItsAsset()
    {
        var recordId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        var identity = IntakeImageOccurrenceIdentity.Folded($"image-case-custody-merge:{recordId:N}", assetId);

        Assert.Equal($"image-case-custody-merge:{recordId:N}:photograph:{assetId:N}", identity);
        Assert.True(IntakeImageOccurrenceIdentity.TryReadAssetId(identity, out var read));
        Assert.Equal(assetId, read);
    }

    [Fact]
    public void APhotographFiledByItselfReadsBackItsAsset()
    {
        var assetId = Guid.NewGuid();

        Assert.True(IntakeImageOccurrenceIdentity.TryReadAssetId(assetId.ToString("N"), out var read));
        Assert.Equal(assetId, read);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("guide-valuation:report")]
    [InlineData("image-case-custody-merge:11111111111111111111111111111111:document:22222222222222222222222222222222")]
    [InlineData("image-case-custody-merge:11111111111111111111111111111111:photograph:not-an-asset")]
    public void AnyOtherIdentityNamesNoAsset(string? identity)
    {
        Assert.False(IntakeImageOccurrenceIdentity.TryReadAssetId(identity, out var read));
        Assert.Equal(Guid.Empty, read);
    }
}
