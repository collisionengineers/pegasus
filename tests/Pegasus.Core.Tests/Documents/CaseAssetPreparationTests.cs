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
        bool inReport = false,
        IReadOnlyList<Guid>? tags = null,
        int? order = null,
        CaseAssetRotation rotation = CaseAssetRotation.None,
        CaseAssetCrop? crop = null,
        string sha256 = "recorded-sha",
        string contentType = "image/jpeg",
        int sourceVersion = 1,
        long preparationVersion = 0,
        bool fullPage = false) =>
        new(
            caseId ?? CaseId,
            occurrenceId ?? Guid.NewGuid(),
            documentId ?? Guid.NewGuid(),
            versionId ?? Guid.NewGuid(),
            sourceVersion,
            sha256,
            contentType,
            inReport,
            order,
            rotation,
            crop ?? CaseAssetCrop.Full,
            preparationVersion,
            null,
            null,
            fullPage)
        {
            TagIds = tags ?? []
        };
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

    /// <summary>
    /// More than one image may carry the Overview or Close-up tag (operator,
    /// 26 September 2026): the save accepts it, and the report takes the first.
    /// </summary>
    [Fact]
    public void TwoImagesTaggedOverviewSaveAndTheFirstInOrderPrintsAsTheOverview()
    {
        var first = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId], order: 1);
        var second = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId], order: 2);

        var saved = CaseAssetPreparationPolicy.ValidateSet(CaseId, [second, first], NoConfirmedSources);
        var report = CaseAssetPreparationPolicy.ForReport(saved);

        Assert.Equal(2, saved.Count);
        Assert.Equal(
            [(first.OccurrenceId, CaseAssetReportRole.Overview), (second.OccurrenceId, CaseAssetReportRole.Supporting)],
            report.Select(image => (image.OccurrenceId, image.Role)).ToArray());
    }

    [Fact]
    public void AReportWithoutTaggedImagesIsNotASaveBlock()
    {
        var untagged = new[] { Item(inReport: true, order: 1) };

        var result = CaseAssetPreparationPolicy.ValidateSet(CaseId, untagged, NoConfirmedSources);

        Assert.True(Assert.Single(result).InReport);
        Assert.All(
            CaseAssetPreparationPolicy.ForReport(result),
            image => Assert.Equal(CaseAssetReportRole.Supporting, image.Role));
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
        var item = Item(inReport: true, rotation: rotation);

        var result = CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources);

        Assert.Equal(rotation, Assert.Single(result).Rotation);
    }

    [Fact]
    public void AnUndefinedRotationFailsClosed()
    {
        var item = Item(inReport: true, rotation: (CaseAssetRotation)45);
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
        var rotated = Item(inReport: true, rotation: CaseAssetRotation.Clockwise90, crop: crop);

        var validated = Assert.Single(
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [rotated], NoConfirmedSources));

        Assert.Equal(crop, validated.Crop);
        Assert.Equal(CaseAssetRotation.Clockwise90, validated.Rotation);
    }

    [Fact]
    public void ReorderingTheImagesInTheReportRenormalizesToAContiguousSequence()
    {
        var a = Item(inReport: true, order: 5);
        var b = Item(inReport: true, tags: [ImageTagVocabulary.CloseUpId], order: 2);
        var c = Item(inReport: true, order: 9);
        var unordered = Item(inReport: true);

        var result = CaseAssetPreparationPolicy.ValidateSet(CaseId, [a, unordered, b, c], NoConfirmedSources);

        Assert.Equal(
            [b.OccurrenceId, a.OccurrenceId, c.OccurrenceId, unordered.OccurrenceId],
            result.OrderBy(item => item.Order).Select(item => item.OccurrenceId).ToArray());
        Assert.Equal([1, 2, 3, 4], result.OrderBy(item => item.Order).Select(item => item.Order).ToArray());
    }

    [Fact]
    public void ReplayingAnAlreadyNormalizedSetIsIdempotent()
    {
        var a = Item(inReport: true, order: 5);
        var b = Item(inReport: true, order: 2);

        var first = CaseAssetPreparationPolicy.ValidateSet(CaseId, [a, b], NoConfirmedSources);
        var second = CaseAssetPreparationPolicy.ValidateSet(CaseId, first, NoConfirmedSources);

        Assert.Equal(
            first.OrderBy(item => item.OccurrenceId).Select(item => (item.OccurrenceId, item.Order)),
            second.OrderBy(item => item.OccurrenceId).Select(item => (item.OccurrenceId, item.Order)));
    }

    [Fact]
    public void AnImageOutOfTheReportClaimsNoPageOfItsOwn()
    {
        var item = Item(inReport: false, rotation: CaseAssetRotation.Half, fullPage: true);

        var result = Assert.Single(CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources));

        Assert.False(result.InReport);
        Assert.Null(result.Order);
        Assert.False(result.FullPage);
        Assert.Equal(CaseAssetRotation.Half, result.Rotation);
    }

    [Fact]
    public void AnImageOutOfTheReportCannotCarryAnOrder()
    {
        var item = Item(inReport: false, order: 1);
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [item], NoConfirmedSources));
    }

    [Fact]
    public void UnsupportedMediaFailsClosedOnlyInTheReport()
    {
        var inReport = Item(inReport: true, contentType: "application/pdf");
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [inReport], NoConfirmedSources));

        var outOfReport = Item(inReport: false, contentType: "application/pdf");
        Assert.Single(CaseAssetPreparationPolicy.ValidateSet(CaseId, [outOfReport], NoConfirmedSources));
    }
    [Fact]
    public void HashMismatchAgainstTheCurrentConfirmedSourceFailsClosed()
    {
        var item = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId], sha256: "recorded-hash");
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
        var item = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId]);
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
        var item = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId]);
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
        var item = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId]);
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
        var otherCase = Item(caseId: Guid.NewGuid(), inReport: true, order: 1);
        Assert.Throws<InvalidOperationException>(() =>
            CaseAssetPreparationPolicy.ValidateSet(CaseId, [otherCase], NoConfirmedSources));
    }

    /// <summary>
    /// The tag decides how an image in the report prints (operator, 26
    /// September 2026): the first image tagged Close-up, then the first other
    /// one tagged Overview, then the rest in order as Supporting. An image out
    /// of the report never prints, whatever it is tagged.
    /// </summary>
    [Fact]
    public void ForReportPrintsTheTaggedCloseUpThenOverviewThenTheRestInOrder()
    {
        var closeUp = Item(inReport: true, tags: [ImageTagVocabulary.CloseUpId], order: 3, fullPage: true);
        var overview = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId], order: 4);
        var supportingTwo = Item(inReport: true, order: 2);
        var supportingOne = Item(inReport: true, order: 1);
        var outOfReport = Item(inReport: false, tags: [ImageTagVocabulary.CloseUpId]);

        var report = CaseAssetPreparationPolicy.ForReport(
            [supportingTwo, closeUp, outOfReport, overview, supportingOne]);

        Assert.Equal(
            [
                (closeUp.OccurrenceId, CaseAssetReportRole.CloseUp, (int?)null),
                (overview.OccurrenceId, CaseAssetReportRole.Overview, (int?)null),
                (supportingOne.OccurrenceId, CaseAssetReportRole.Supporting, (int?)1),
                (supportingTwo.OccurrenceId, CaseAssetReportRole.Supporting, (int?)2),
            ],
            report.Select(item => (item.OccurrenceId, item.Role, item.Order)).ToArray());
        Assert.True(report[0].FullPage);
    }

    /// <summary>One image tagged both Close-up and Overview prints once, as the Close-up.</summary>
    [Fact]
    public void AnImageTaggedCloseUpAndOverviewPrintsOnceAsTheCloseUp()
    {
        var both = Item(inReport: true, tags: [ImageTagVocabulary.OverviewId, ImageTagVocabulary.CloseUpId], order: 1);

        var image = Assert.Single(CaseAssetPreparationPolicy.ForReport([both]));

        Assert.Equal(CaseAssetReportRole.CloseUp, image.Role);
    }

    [Theory]
    [InlineData("00000000-0000-4000-8000-0000000017a3", true)]
    [InlineData("00000000-0000-4000-8000-0000000017a4", true)]
    [InlineData("00000000-0000-4000-8000-0000000017a1", false)]
    [InlineData("00000000-0000-4000-8000-0000000017a2", false)]
    public void ThirdPartyAndReflectionTakeAnImageOutOfTheReport(string tagId, bool takesOut)
    {
        Assert.Equal(takesOut, ImageTagVocabulary.TakesImageOutOfReport(Guid.Parse(tagId)));
    }
}