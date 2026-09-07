using Pegasus.Core.Documents;

namespace Pegasus.Core.Tests.Documents;

public sealed class CaseAssetPreparationTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<Guid, DocumentVersion> NoConfirmedSources =
        new Dictionary<Guid, DocumentVersion>();

    private static CaseAssetPreparation Item(
        Guid? occurrenceId = null,
        Guid? documentId = null,
        Guid? versionId = null,
        Guid? caseId = null,
        CaseAssetReportRole role = CaseAssetReportRole.NotUsed,
        int? order = null,
        CaseAssetRotation rotation = CaseAssetRotation.None,
        CaseAssetCrop? crop = null,
        string sha256 = "recorded-sha",
        string contentType = "image/jpeg",
        int sourceVersion = 1,
        long preparationVersion = 0) =>
        new(
            caseId ?? CaseId,
            occurrenceId ?? Guid.NewGuid(),
            documentId ?? Guid.NewGuid(),
            versionId ?? Guid.NewGuid(),
            sourceVersion,
            sha256,
            contentType,
            role,
            order,
            rotation,
            crop ?? CaseAssetCrop.Full,
            preparationVersion,
            null,
            null);

    private static DocumentVersion Confirmed(
        CaseAssetPreparation item,
        string? sha256 = null,
        string? mediaType = null,
        bool isCurrent = true,
        bool isRemoved = false,
        DocumentCustodyStatus status = DocumentCustodyStatus.Confirmed) =>
        new(
            item.VersionId,
            item.DocumentId,
            item.SourceVersion,
            "file.jpg",
            mediaType ?? item.SourceContentType,
            1,
            sha256 ?? item.SourceSha256,
            status,
            Now,
            "Staff:test",
            isCurrent,
            isRemoved,
            null);

    [Fact]
    public void DuplicateCloseUpIsRejected()
    {
        var first = Item(role: CaseAssetReportRole.CloseUp);
        var second = Item(role: CaseAssetReportRole.CloseUp);
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [first, second], NoConfirmedSources));
    }

    [Fact]
    public void DuplicateOverviewIsRejected()
    {
        var first = Item(role: CaseAssetReportRole.Overview);
        var second = Item(role: CaseAssetReportRole.Overview);
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [first, second], NoConfirmedSources));
    }

    [Fact]
    public void MissingReadinessRolesIsNotASaveBlock()
    {
        var supportingOnly = new[] { Item(role: CaseAssetReportRole.Supporting, order: 1) };

        var result = CaseAssetPreparationPolicy.ValidateSet(CaseId, supportingOnly, NoConfirmedSources);

        Assert.Single(result);
        Assert.DoesNotContain(result, item => item.Role == CaseAssetReportRole.CloseUp);
        Assert.DoesNotContain(result, item => item.Role == CaseAssetReportRole.Overview);
    }

    [Theory]
    [InlineData(-0.1, 0, 0.5, 0.5)]
    [InlineData(0, -0.1, 0.5, 0.5)]
    [InlineData(0, 0, 0, 0.5)]
    [InlineData(0, 0, 0.5, 0)]
    [InlineData(0.6, 0, 0.6, 0.5)]
    [InlineData(0, 0.6, 0.5, 0.6)]
    [InlineData(1.1, 0, 0.5, 0.5)]
    public void InvalidOrOutOfRangeCropFailsClosed(double left, double top, double width, double height)
    {
        var crop = new CaseAssetCrop((decimal)left, (decimal)top, (decimal)width, (decimal)height);
        Assert.Throws<ArgumentOutOfRangeException>(crop.Validate);
    }

    [Fact]
    public void EmptyZeroAreaCropFailsClosed()
    {
        var crop = new CaseAssetCrop(0.2m, 0.2m, 0m, 0m);
        Assert.Throws<ArgumentOutOfRangeException>(crop.Validate);
    }

    [Fact]
    public void CropWithMoreThanSevenDecimalPlacesFailsClosed()
    {
        var crop = new CaseAssetCrop(0.123456789m, 0m, 0.5m, 0.5m);
        Assert.Throws<ArgumentOutOfRangeException>(crop.Validate);
    }

    [Fact]
    public void FullCropValidatesCleanlyAndIsRecognizedAsFull()
    {
        CaseAssetCrop.Full.Validate();
        Assert.True(CaseAssetCrop.Full.IsFull);
        Assert.False(new CaseAssetCrop(0.1m, 0.1m, 0.5m, 0.5m).IsFull);
    }

    [Theory]
    [InlineData(CaseAssetRotation.None)]
    [InlineData(CaseAssetRotation.Clockwise90)]
    [InlineData(CaseAssetRotation.Half)]
    [InlineData(CaseAssetRotation.Clockwise270)]
    public void EachDefinedRotationIsAccepted(CaseAssetRotation rotation)
    {
        var item = Item(role: CaseAssetReportRole.CloseUp, rotation: rotation);

        var result = CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources);

        Assert.Equal(rotation, Assert.Single(result).Rotation);
    }

    [Fact]
    public void AnUndefinedRotationFailsClosed()
    {
        var item = Item(role: CaseAssetReportRole.CloseUp, rotation: (CaseAssetRotation)45);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources));
    }

    [Fact]
    public void CropFractionsAreCarriedThroughUnchangedWhenRotationChanges()
    {
        // The crop convention is fractions of the already-rotated source: the
        // policy never re-derives the crop for a different rotation, so
        // saving the same crop values under two different rotations keeps
        // them byte-identical.
        var crop = new CaseAssetCrop(0.1m, 0.1m, 0.5m, 0.5m);
        var rotated = Item(role: CaseAssetReportRole.CloseUp, rotation: CaseAssetRotation.Clockwise90, crop: crop);

        var validated = Assert.Single(
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [rotated], NoConfirmedSources));

        Assert.Equal(crop, validated.Crop);
        Assert.Equal(CaseAssetRotation.Clockwise90, validated.Rotation);
    }

    [Fact]
    public void ReorderingSupportingImagesRenormalizesToAContiguousSequence()
    {
        var a = Item(role: CaseAssetReportRole.Supporting, order: 5);
        var b = Item(role: CaseAssetReportRole.Supporting, order: 2);
        var c = Item(role: CaseAssetReportRole.Supporting, order: 9);

        var result = CaseAssetPreparationPolicy.ValidateSet(CaseId, [a, b, c], NoConfirmedSources);

        Assert.Equal(
            [b.OccurrenceId, a.OccurrenceId, c.OccurrenceId],
            result.OrderBy(item => item.Order).Select(item => item.OccurrenceId).ToArray());
        Assert.Equal([1, 2, 3], result.OrderBy(item => item.Order).Select(item => item.Order).ToArray());
    }

    [Fact]
    public void ReplayingAnAlreadyNormalizedSetIsIdempotent()
    {
        var a = Item(role: CaseAssetReportRole.Supporting, order: 5);
        var b = Item(role: CaseAssetReportRole.Supporting, order: 2);

        var first = CaseAssetPreparationPolicy.ValidateSet(CaseId, [a, b], NoConfirmedSources);
        var second = CaseAssetPreparationPolicy.ValidateSet(CaseId, first, NoConfirmedSources);

        Assert.Equal(
            first.OrderBy(item => item.OccurrenceId).Select(item => (item.OccurrenceId, item.Order)),
            second.OrderBy(item => item.OccurrenceId).Select(item => (item.OccurrenceId, item.Order)));
    }

    [Fact]
    public void ResetRestoresNotUsedNullOrderNoneRotationAndFullCrop()
    {
        var prepared = Item(role: CaseAssetReportRole.Supporting, order: 3, rotation: CaseAssetRotation.Half);
        var reset = prepared with
        {
            Role = CaseAssetReportRole.NotUsed,
            Order = null,
            Rotation = CaseAssetRotation.None,
            Crop = CaseAssetCrop.Full
        };

        var result = Assert.Single(CaseAssetPreparationPolicy.ValidateSet(CaseId, [reset], NoConfirmedSources));

        Assert.Equal(CaseAssetReportRole.NotUsed, result.Role);
        Assert.Null(result.Order);
        Assert.Equal(CaseAssetRotation.None, result.Rotation);
        Assert.True(result.Crop.IsFull);
    }

    [Fact]
    public void AnUnusedAssetCannotCarryASupportingOrder()
    {
        var item = Item(role: CaseAssetReportRole.NotUsed, order: 1);
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources));
    }

    [Fact]
    public void UnsupportedMediaFailsClosed()
    {
        var item = Item(role: CaseAssetReportRole.CloseUp, contentType: "application/pdf");
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources));
    }

    [Fact]
    public void HashMismatchAgainstTheCurrentConfirmedSourceFailsClosed()
    {
        var item = Item(role: CaseAssetReportRole.Overview, sha256: "recorded-hash");
        var confirmed = Confirmed(item, sha256: "different-hash");

        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(
                CaseId,
                [item],
                new Dictionary<Guid, DocumentVersion> { [item.OccurrenceId] = confirmed }));
    }

    [Fact]
    public void ASupersededNoLongerCurrentSourceFailsClosed()
    {
        var item = Item(role: CaseAssetReportRole.Overview);
        var confirmed = Confirmed(item, isCurrent: false);

        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(
                CaseId,
                [item],
                new Dictionary<Guid, DocumentVersion> { [item.OccurrenceId] = confirmed }));
    }

    [Fact]
    public void ALogicallyRemovedSourceFailsClosed()
    {
        var item = Item(role: CaseAssetReportRole.Overview);
        var confirmed = Confirmed(item, isRemoved: true);

        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(
                CaseId,
                [item],
                new Dictionary<Guid, DocumentVersion> { [item.OccurrenceId] = confirmed }));
    }

    [Fact]
    public void ANotYetConfirmedSourceFailsClosed()
    {
        var item = Item(role: CaseAssetReportRole.Overview);
        var confirmed = Confirmed(item, status: DocumentCustodyStatus.Pending);

        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(
                CaseId,
                [item],
                new Dictionary<Guid, DocumentVersion> { [item.OccurrenceId] = confirmed }));
    }

    [Fact]
    public void ACrossCaseAssetIsRejected()
    {
        var otherCase = Item(caseId: Guid.NewGuid(), role: CaseAssetReportRole.Supporting, order: 1);
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [otherCase], NoConfirmedSources));
    }

    [Fact]
    public void ForReportOrdersCloseUpThenOverviewThenSupportingByOrderAndExcludesNotUsed()
    {
        var closeUp = Item(role: CaseAssetReportRole.CloseUp);
        var overview = Item(role: CaseAssetReportRole.Overview);
        var supportingTwo = Item(role: CaseAssetReportRole.Supporting, order: 2);
        var supportingOne = Item(role: CaseAssetReportRole.Supporting, order: 1);
        var notUsed = Item(role: CaseAssetReportRole.NotUsed);

        var report = CaseAssetPreparationPolicy.ForReport(
            [supportingTwo, closeUp, notUsed, overview, supportingOne]);

        Assert.Equal(
            [closeUp.OccurrenceId, overview.OccurrenceId, supportingOne.OccurrenceId, supportingTwo.OccurrenceId],
            report.Select(item => item.OccurrenceId).ToArray());
        Assert.DoesNotContain(report, item => item.OccurrenceId == notUsed.OccurrenceId);
    }
}
