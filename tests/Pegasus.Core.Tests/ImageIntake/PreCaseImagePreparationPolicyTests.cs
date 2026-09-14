using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Tests.ImageIntake;

/// <summary>Crop and tag on a pre-Case image: what a request may be, and who may make it.</summary>
public sealed class PreCaseImagePreparationPolicyTests
{
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public void AnUnpreparedImageIsTheWholeFrameWithNoRotationOrTags()
    {
        var original = PreCaseImagePreparation.Original(Guid.NewGuid());
        Assert.False(original.IsPrepared);
        Assert.Empty(original.Tags);
        Assert.Equal(0, original.Version);
        Assert.True((original with { Rotation = CaseAssetRotation.Clockwise90 }).IsPrepared);
        Assert.True((original with { Crop = new CaseAssetCrop(0.1m, 0.1m, 0.5m, 0.5m) }).IsPrepared);
    }

    [Fact]
    public void ACropRequestIsValidatedByTheCaseCropRules()
    {
        var assetId = Guid.NewGuid();
        PreCaseImagePreparationPolicy.Validate(new SavePreCaseImageCropRequest(
            assetId, 0, CaseAssetRotation.Half, new CaseAssetCrop(0.25m, 0.25m, 0.5m, 0.5m), Staff, "crop-1"));

        Assert.ThrowsAny<ArgumentException>(() => PreCaseImagePreparationPolicy.Validate(new SavePreCaseImageCropRequest(
            assetId, 0, CaseAssetRotation.None, new CaseAssetCrop(0.75m, 0m, 0.5m, 1m), Staff, "crop-2")));
        Assert.ThrowsAny<ArgumentException>(() => PreCaseImagePreparationPolicy.Validate(new SavePreCaseImageCropRequest(
            assetId, 0, (CaseAssetRotation)45, CaseAssetCrop.Full, Staff, "crop-3")));
        Assert.ThrowsAny<ArgumentException>(() => PreCaseImagePreparationPolicy.Validate(new SavePreCaseImageCropRequest(
            Guid.Empty, 0, CaseAssetRotation.None, CaseAssetCrop.Full, Staff, "crop-4")));
        Assert.ThrowsAny<ArgumentException>(() => PreCaseImagePreparationPolicy.Validate(new SavePreCaseImageCropRequest(
            assetId, 0, CaseAssetRotation.None, CaseAssetCrop.Full, Staff, new string('k', 101))));
        Assert.Throws<StaffAuthorizationException>(() => PreCaseImagePreparationPolicy.Validate(new SavePreCaseImageCropRequest(
            assetId, 0, CaseAssetRotation.None, CaseAssetCrop.Full, ActionActor.SystemWorker("intake-processing"), "crop-5")));
    }

    [Fact]
    public void ATagRequestNamesATagAndIsStaffCasework()
    {
        PreCaseImagePreparationPolicy.Validate(new TagPreCaseImageRequest(Guid.NewGuid(), ImageTagVocabulary.OverviewId, true, Staff, "tag-1"));
        Assert.ThrowsAny<ArgumentException>(() => PreCaseImagePreparationPolicy.Validate(
            new TagPreCaseImageRequest(Guid.NewGuid(), Guid.Empty, true, Staff, "tag-2")));
        Assert.Throws<StaffAuthorizationException>(() => PreCaseImagePreparationPolicy.Validate(
            new TagPreCaseImageRequest(Guid.NewGuid(), ImageTagVocabulary.OverviewId, false, ActionActor.SystemWorker("intake-processing"), "tag-3")));
    }
}
