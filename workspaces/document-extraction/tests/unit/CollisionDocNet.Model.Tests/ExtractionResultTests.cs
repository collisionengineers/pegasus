using System.Text;
using CollisionDocNet.Core;
using CollisionDocNet.Model;

namespace CollisionDocNet.Model.Tests;

[TestClass]
public sealed class ExtractionResultTests
{
    [TestMethod]
    [DataRow(ExtractionOutcome.Complete)]
    [DataRow(ExtractionOutcome.Partial)]
    [DataRow(ExtractionOutcome.Encrypted)]
    [DataRow(ExtractionOutcome.Corrupt)]
    [DataRow(ExtractionOutcome.UnsupportedFormat)]
    [DataRow(ExtractionOutcome.UnsupportedFeature)]
    [DataRow(ExtractionOutcome.ResourceLimitExceeded)]
    [DataRow(ExtractionOutcome.Cancelled)]
    [DataRow(ExtractionOutcome.TimedOut)]
    [DataRow(ExtractionOutcome.TechnicalFailure)]
    public void Constructor_PreservesEveryRequiredOutcome(ExtractionOutcome outcome)
    {
        ExtractionResult result = CreateResult(outcome);

        Assert.AreEqual(outcome, result.Outcome);
        Assert.AreEqual(ExtractionResult.CurrentSchemaVersion, result.SchemaVersion);
    }

    [TestMethod]
    public void Constructor_OrdersEvidenceDeterministically()
    {
        var later = new SourceLocation(SourceLocationKind.ByteRange, "pdf", "body", 20, 1);
        var earlier = new SourceLocation(SourceLocationKind.ByteRange, "pdf", "body", 10, 1);
        ExtractionResult result = CreateResult(
            ExtractionOutcome.Partial,
            content:
            [
                new ContentSegment(2, "text", "second", later),
                new ContentSegment(1, "text", "first", earlier),
            ],
            issues:
            [
                new ExtractionIssue(2, ExtractionIssueSeverity.Warning, "Z", "later", later),
                new ExtractionIssue(1, ExtractionIssueSeverity.Error, "A", "earlier", earlier),
            ]);

        Assert.AreEqual("first", result.Content[0].Text);
        Assert.AreEqual("second", result.Content[1].Text);
        Assert.AreEqual("A", result.Issues[0].Code);
        Assert.AreEqual("Z", result.Issues[1].Code);
    }

    [TestMethod]
    public void Constructor_UsesOrdinalTieBreakersIndependentOfInputOrder()
    {
        ContentSegment alpha = new(1, "text", "alpha", null);
        ContentSegment beta = new(1, "text", "beta", null);

        ExtractionResult first = CreateResult(
            ExtractionOutcome.Complete,
            content: [beta, alpha]);
        ExtractionResult second = CreateResult(
            ExtractionOutcome.Complete,
            content: [alpha, beta]);

        CollectionAssert.AreEqual(first.Content.ToArray(), second.Content.ToArray());
        Assert.AreEqual("alpha", first.Content[0].Text);
    }

    [TestMethod]
    public void SerializeToUtf8Bytes_EquivalentEvidenceIsByteStable()
    {
        ExtractionIssue warning = new(1, ExtractionIssueSeverity.Warning, "DOC001", "Visible issue", null);
        ExtractionIssue error = new(0, ExtractionIssueSeverity.Error, "DOC000", "Visible error", null);
        ExtractionResult first = CreateResult(
            ExtractionOutcome.Partial,
            content: [new ContentSegment(0, "text", "evidence", null)],
            issues: [warning, error]);
        ExtractionResult retry = CreateResult(
            ExtractionOutcome.Partial,
            content: [new ContentSegment(0, "text", "evidence", null)],
            issues: [error, warning]);

        byte[] firstJson = ExtractionResultJson.SerializeToUtf8Bytes(first);
        byte[] retryJson = ExtractionResultJson.SerializeToUtf8Bytes(retry);

        CollectionAssert.AreEqual(firstJson, retryJson);
        Assert.AreNotEqual((byte)0xEF, firstJson[0]);
        Assert.Contains("\"schemaVersion\":\"collisiondocnet-result/1\"", Encoding.UTF8.GetString(firstJson));
        Assert.Contains("\"outcome\":\"Partial\"", Encoding.UTF8.GetString(firstJson));
    }

    [TestMethod]
    public void SerializeToUtf8Bytes_UniqueAssetsWithSameHash_IsIndependentOfInputOrder()
    {
        byte[] content = [1, 2, 3];
        var firstAsset = new ReviewAsset(
            "asset-2",
            "attachment",
            "application/octet-stream",
            "a.bin",
            content,
            new SourceLocation(SourceLocationKind.ContainerEntry, "msg", "a", 0, 3));
        var secondAsset = new ReviewAsset(
            "asset-1",
            "inline-image",
            "application/octet-stream",
            "b.bin",
            content,
            new SourceLocation(SourceLocationKind.ContainerEntry, "msg", "b", 0, 3));

        ExtractionResult first = CreateResult(
            ExtractionOutcome.Complete,
            assets: [secondAsset, firstAsset]);
        ExtractionResult reversed = CreateResult(
            ExtractionOutcome.Complete,
            assets: [firstAsset, secondAsset]);

        CollectionAssert.AreEqual(
            ExtractionResultJson.SerializeToUtf8Bytes(first),
            ExtractionResultJson.SerializeToUtf8Bytes(reversed));
        Assert.AreEqual("asset-1", first.Assets[0].StableId);
        Assert.AreEqual("asset-2", first.Assets[1].StableId);
    }

    [TestMethod]
    public void Constructor_DuplicateAssetStableIdsWithDifferentContent_Throws()
    {
        var firstAsset = new ReviewAsset(
            "asset-1",
            "attachment",
            "application/octet-stream",
            "a.bin",
            new byte[] { 1 },
            null);
        var secondAsset = new ReviewAsset(
            "asset-1",
            "attachment",
            "application/octet-stream",
            "b.bin",
            new byte[] { 2 },
            null);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CreateResult(
                ExtractionOutcome.Complete,
                assets: [secondAsset, firstAsset]));

        Assert.Contains("must be unique", exception.Message);
    }

    [TestMethod]
    public void SerializeToUtf8Bytes_SameHashNestedResults_IsIndependentOfInputOrder()
    {
        ExtractionResult alpha = CreateResult(
            ExtractionOutcome.Partial,
            content: [new ContentSegment(0, "text", "alpha", null)]);
        ExtractionResult beta = CreateResult(
            ExtractionOutcome.Partial,
            content: [new ContentSegment(0, "text", "beta", null)]);

        ExtractionResult first = CreateResult(
            ExtractionOutcome.Partial,
            nestedResults: [beta, alpha]);
        ExtractionResult reversed = CreateResult(
            ExtractionOutcome.Partial,
            nestedResults: [alpha, beta]);

        CollectionAssert.AreEqual(
            ExtractionResultJson.SerializeToUtf8Bytes(first),
            ExtractionResultJson.SerializeToUtf8Bytes(reversed));
    }

    [TestMethod]
    public void SerializeToUtf8Bytes_DifferentElapsedTelemetry_IsCanonicallyByteStable()
    {
        ExtractionResult first = CreateResult(
            ExtractionOutcome.Complete,
            measurements: new ResourceMeasurements(6, 0, 0, 0, 0, 0, 0, 1));
        ExtractionResult retry = CreateResult(
            ExtractionOutcome.Complete,
            measurements: new ResourceMeasurements(6, 0, 0, 0, 0, 0, 0, 999));

        byte[] firstJson = ExtractionResultJson.SerializeToUtf8Bytes(first);
        byte[] retryJson = ExtractionResultJson.SerializeToUtf8Bytes(retry);

        CollectionAssert.AreEqual(firstJson, retryJson);
        Assert.DoesNotContain("elapsedMilliseconds", Encoding.UTF8.GetString(firstJson));
        Assert.AreEqual(999, retry.Measurements.ElapsedMilliseconds);
    }

    [TestMethod]
    public void SerializeToUtf8Bytes_AllEmptyCollectionsAreExplicit()
    {
        string json = Encoding.UTF8.GetString(
            ExtractionResultJson.SerializeToUtf8Bytes(CreateResult(ExtractionOutcome.UnsupportedFormat)));

        Assert.Contains("\"content\":[]", json);
        Assert.Contains("\"assets\":[]", json);
        Assert.Contains("\"issues\":[]", json);
        Assert.Contains("\"nestedResults\":[]", json);
    }

    [TestMethod]
    public void ResourceMeasurements_FromSnapshot_PreservesBoundedCounters()
    {
        var snapshot = new ResourceBudgetSnapshot(1, 2, 3, 4, 5, 6, 7);

        ResourceMeasurements measurements = ResourceMeasurements.FromSnapshot(
            snapshot,
            TimeSpan.FromMilliseconds(8));

        Assert.AreEqual(1, measurements.InputBytes);
        Assert.AreEqual(7, measurements.MaximumNestingDepth);
        Assert.AreEqual(8, measurements.ElapsedMilliseconds);
    }

    [TestMethod]
    public void ReviewAsset_CopiesContentAndDerivesHashAndLength()
    {
        byte[] content = [1, 2, 3];
        var asset = new ReviewAsset("asset-1", "attachment", null, "untrusted.bin", content, null);

        content[0] = 9;

        Assert.AreEqual(3, asset.Length);
        Assert.AreEqual((byte)1, asset.Content[0]);
        Assert.AreEqual(Sha256Digest.Compute(new byte[] { 1, 2, 3 }), asset.ContentHash);
        string json = Encoding.UTF8.GetString(
            ExtractionResultJson.SerializeToUtf8Bytes(
                new ExtractionResult(
                    DetectedContainer.FlatBinary,
                    DetectedFormat.Pdf,
                    ExtractionOutcome.Complete,
                    Sha256Digest.Compute("source"u8),
                    "1.0.0-test",
                    "test-spec/1",
                    "test-policy/1",
                    new ResourceMeasurements(6, 0, 0, 0, 1, 3, 0, 1),
                    assets: [asset])));
        Assert.DoesNotContain("AQID", json);
    }

    [TestMethod]
    [DataRow("con")]
    [DataRow("CON")]
    [DataRow("nul")]
    [DataRow("NUL.bin")]
    [DataRow("con.txt")]
    [DataRow("com1")]
    [DataRow("lpt9")]
    public void ReviewAsset_WindowsReservedDeviceBasename_Throws(string stableId)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ReviewAsset(
                stableId,
                "attachment",
                null,
                null,
                ReadOnlyMemory<byte>.Empty,
                null));
    }

    [TestMethod]
    [DataRow("asset-1")]
    [DataRow("con-file")]
    [DataRow("com10")]
    [DataRow("lpt0")]
    [DataRow("console")]
    public void ReviewAsset_PortableStableId_IsAccepted(string stableId)
    {
        var asset = new ReviewAsset(
            stableId,
            "attachment",
            null,
            null,
            ReadOnlyMemory<byte>.Empty,
            null);

        Assert.AreEqual(stableId, asset.StableId);
    }

    [TestMethod]
    public void ReviewAsset_BlankOptionalFields_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ReviewAsset("asset-1", "attachment", " ", null, ReadOnlyMemory<byte>.Empty, null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ReviewAsset("asset-1", "attachment", null, "", ReadOnlyMemory<byte>.Empty, null));
    }

    [TestMethod]
    public void SourceLocation_NegativeRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SourceLocation(SourceLocationKind.ByteRange, "doc", "WordDocument", -1, 1));
    }

    [TestMethod]
    public void SourceLocation_EndOverflow_Throws()
    {
        Assert.ThrowsExactly<OverflowException>(() =>
            new SourceLocation(SourceLocationKind.ByteRange, "doc", "WordDocument", long.MaxValue, 1));
    }

    [TestMethod]
    public void PublicModels_InvalidEnumsAndBlankSemanticFields_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SourceLocation((SourceLocationKind)999, "doc", "stream", 0, 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ContentSegment(0, " ", "text", null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new MetadataEntry(0, " ", "value", null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new Participant(0, "to", null, null, null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EvidenceRelationship(0, "attachment", " ", "target", null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ExtractionIssue(0, (ExtractionIssueSeverity)999, "CODE", "message", null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateResult((ExtractionOutcome)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ResourceMeasurements(-1, 0, 0, 0, 0, 0, 0, 0));
        var budget = new ResourceBudget(ResourceLimits.CreateCollisionSpikeDefault());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            budget.TryCharge((ResourceKind)999, 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ReviewAsset(
                "../asset",
                "attachment",
                null,
                null,
                ReadOnlyMemory<byte>.Empty,
                null));
    }

    [TestMethod]
    public void ExtractionResult_InvalidDetectedContainerAndFormat_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateResult(
                ExtractionOutcome.Complete,
                detectedContainer: (DetectedContainer)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateResult(
                ExtractionOutcome.Complete,
                detectedFormat: (DetectedFormat)999));
    }

    [TestMethod]
    public void ExtractionResult_BlankRequiredIdentitiesAndNullMeasurements_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateResult(ExtractionOutcome.Complete, extractorVersion: " "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateResult(ExtractionOutcome.Complete, specificationIdentity: ""));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateResult(ExtractionOutcome.Complete, policyIdentity: "\t"));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            CreateResult(ExtractionOutcome.Complete, measurements: null, useDefaultMeasurements: false));
    }

    private static ExtractionResult CreateResult(
        ExtractionOutcome outcome,
        IEnumerable<ContentSegment>? content = null,
        IEnumerable<ExtractionIssue>? issues = null,
        IEnumerable<ReviewAsset>? assets = null,
        IEnumerable<ExtractionResult>? nestedResults = null,
        ResourceMeasurements? measurements = null,
        bool useDefaultMeasurements = true,
        DetectedContainer detectedContainer = DetectedContainer.FlatBinary,
        DetectedFormat detectedFormat = DetectedFormat.Pdf,
        string extractorVersion = "1.0.0-test",
        string specificationIdentity = "test-spec/1",
        string policyIdentity = "test-policy/1") =>
        new(
            detectedContainer,
            detectedFormat,
            outcome,
            Sha256Digest.Compute("source"u8),
            extractorVersion,
            specificationIdentity,
            policyIdentity,
            useDefaultMeasurements
                ? measurements ?? new ResourceMeasurements(6, 0, 0, 0, 0, 0, 0, 1)
                : measurements!,
            content: content,
            assets: assets,
            issues: issues,
            nestedResults: nestedResults);
}
