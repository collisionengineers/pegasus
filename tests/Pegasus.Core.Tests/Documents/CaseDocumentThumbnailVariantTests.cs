using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// v26 § Crop and tag: a thumbnail shows the occurrence's prepared region.
/// The variant token names that region so a cached rendering is never served
/// for a different crop, and the plain rendering keeps its pre-v26 name.
/// </summary>
public sealed class CaseDocumentThumbnailVariantTests
{
    [Fact]
    public void ThePlainRenderingKeepsItsName()
    {
        Assert.Equal("thumb-480", CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, null));
        Assert.Equal("thumb-480", CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, CaseAssetCrop.Full));
    }

    [Fact]
    public void ARotationOrACropIsItsOwnVariant()
    {
        var crop = new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.25m);

        Assert.Equal("thumb-480-r90-c0-0-1-1", CaseDocumentThumbnails.VariantToken(CaseAssetRotation.Clockwise90, null));
        Assert.Equal("thumb-480-r0-c0.1-0.2-0.5-0.25", CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, crop));
        Assert.NotEqual(
            CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, crop),
            CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.3m)));
    }

    [Fact]
    public void ASevenDecimalCropAtTheLongestRotationUsesTheFullVariantBudget()
    {
        var variant = CaseDocumentThumbnails.VariantToken(
            CaseAssetRotation.Clockwise270,
            new CaseAssetCrop(0.1234567m, 0.1234567m, 0.1234567m, 0.1234567m));

        Assert.Equal("thumb-480-r270-c0.1234567-0.1234567-0.1234567-0.1234567", variant);
        Assert.Equal(55, variant.Length);
        Assert.InRange(variant.Length, 33, 64);
    }

    [Fact]
    public void ARequestIsPreparedOnlyWhenARotationOrACropApplies()
    {
        var plain = new CaseDocumentThumbnailRequest(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 10, "image/jpeg");

        Assert.False(plain.IsPrepared);
        Assert.False((plain with { Crop = CaseAssetCrop.Full }).IsPrepared);
        Assert.True((plain with { Rotation = CaseAssetRotation.Half }).IsPrepared);
        Assert.True((plain with { Crop = new CaseAssetCrop(0m, 0m, 0.5m, 0.5m) }).IsPrepared);
    }
}
