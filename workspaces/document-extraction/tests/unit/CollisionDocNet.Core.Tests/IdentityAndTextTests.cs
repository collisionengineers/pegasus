using System.Text;
using CollisionDocNet.Core;

namespace CollisionDocNet.Core.Tests;

[TestClass]
public sealed class IdentityAndTextTests
{
    private static readonly int[] OffsetOne = [1];
    private static readonly int[] OffsetsZeroAndTwo = [0, 2];
    private static readonly int[] OffsetsZeroAndFour = [0, 4];
    private static readonly int[] OffsetsOneAndThree = [1, 3];

    [TestMethod]
    public void Sha256Digest_KnownValue_UsesLowercaseCanonicalHex()
    {
        Sha256Digest digest = Sha256Digest.Compute("abc"u8);

        Assert.AreEqual(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            digest.Hex);
        Assert.IsTrue(Sha256Digest.TryParse(digest.Hex, out Sha256Digest reparsed));
        Assert.AreEqual(digest, reparsed);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    [DataRow("zz7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void Sha256Digest_NonCanonicalValue_IsRejected(string? value)
    {
        Assert.IsFalse(Sha256Digest.TryParse(value, out _));
    }

    [TestMethod]
    public void StableIdentity_SameComponents_IsStableAndOrderSensitive()
    {
        string first = StableIdentity.Create("asset", "source", "part", "1");
        string retry = StableIdentity.Create("asset", "source", "part", "1");
        string reordered = StableIdentity.Create("asset", "part", "source", "1");

        Assert.AreEqual(first, retry);
        Assert.AreNotEqual(first, reordered);
        Assert.StartsWith("asset-", first);
    }

    [TestMethod]
    public void StableIdentity_LengthPrefixPreventsConcatenationAmbiguity()
    {
        Assert.AreNotEqual(
            StableIdentity.Create("asset", "ab", "c"),
            StableIdentity.Create("asset", "a", "bc"));
    }

    [TestMethod]
    [DataRow("../asset")]
    [DataRow("Asset")]
    [DataRow("9asset")]
    [DataRow("asset_name")]
    public void StableIdentity_UnsafeDomain_IsRejected(string domain)
    {
        Assert.ThrowsExactly<ArgumentException>(() => StableIdentity.Create(domain, "part"));
    }

    [TestMethod]
    public void Decode_Utf8InvalidSequence_RejectsOrReplacesByPolicy()
    {
        byte[] bytes = [0x61, 0xC3, 0x28];

        TextDecodeResult rejected = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf8,
            InvalidTextPolicy.Reject);
        TextDecodeResult replaced = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf8,
            InvalidTextPolicy.Replace);

        Assert.IsFalse(rejected.IsSuccess);
        Assert.IsEmpty(rejected.Text);
        Assert.IsTrue(replaced.IsSuccess);
        Assert.Contains("�", replaced.Text);
        CollectionAssert.AreEqual(OffsetOne, replaced.InvalidByteOffsets.ToArray());
    }

    [TestMethod]
    public void Decode_MultipleInvalidUtf8Sequences_ReportsEveryByteOffset()
    {
        byte[] bytes = [0xFF, 0x41, 0xFF];

        TextDecodeResult replaced = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf8,
            InvalidTextPolicy.Replace);
        TextDecodeResult rejected = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf8,
            InvalidTextPolicy.Reject);

        CollectionAssert.AreEqual(OffsetsZeroAndTwo, replaced.InvalidByteOffsets.ToArray());
        CollectionAssert.AreEqual(OffsetsZeroAndTwo, rejected.InvalidByteOffsets.ToArray());
        Assert.AreEqual("�A�", replaced.Text);
        Assert.IsFalse(rejected.IsSuccess);
    }

    [TestMethod]
    public void Decode_InvalidUtf16Sequences_ReportsByteOffsets()
    {
        byte[] bytes = [0x00, 0xD8, 0x41, 0x00, 0x00];

        TextDecodeResult result = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf16LittleEndian,
            InvalidTextPolicy.Replace);

        CollectionAssert.AreEqual(OffsetsZeroAndFour, result.InvalidByteOffsets.ToArray());
        Assert.AreEqual("�A�", result.Text);
    }

    [TestMethod]
    public void Decode_InvalidUtf16BigEndian_ReportsEveryByteOffset()
    {
        byte[] bytes = [0xD8, 0x00, 0x00, 0x41, 0x00];

        TextDecodeResult result = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf16BigEndian,
            InvalidTextPolicy.Reject);

        Assert.IsFalse(result.IsSuccess);
        CollectionAssert.AreEqual(OffsetsZeroAndFour, result.InvalidByteOffsets.ToArray());
    }

    [TestMethod]
    public void Decode_InvalidEnums_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            DocumentTextDecoder.Decode([], (DocumentEncoding)999, InvalidTextPolicy.Reject));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            DocumentTextDecoder.Decode([], DocumentEncoding.Utf8, (InvalidTextPolicy)999));
    }

    [TestMethod]
    public void Decode_Windows1252_MapsDefinedAndReportsUndefinedBytes()
    {
        byte[] bytes = [0x80, 0x81, 0x41];

        TextDecodeResult result = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Windows1252,
            InvalidTextPolicy.Replace);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("€�A", result.Text);
        Assert.HasCount(1, result.InvalidByteOffsets);
        Assert.AreEqual(1, result.InvalidByteOffsets[0]);
    }

    [TestMethod]
    public void Decode_Windows1252Reject_ReportsEveryUndefinedByteOffset()
    {
        byte[] bytes = [0x41, 0x81, 0x42, 0x8D];

        TextDecodeResult result = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Windows1252,
            InvalidTextPolicy.Reject);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(result.Text);
        CollectionAssert.AreEqual(OffsetsOneAndThree, result.InvalidByteOffsets.ToArray());
    }

    [TestMethod]
    public void Decode_Utf16LittleEndian_ReturnsExpectedText()
    {
        byte[] bytes = Encoding.Unicode.GetBytes("AΩ");

        TextDecodeResult result = DocumentTextDecoder.Decode(
            bytes,
            DocumentEncoding.Utf16LittleEndian,
            InvalidTextPolicy.Reject);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("AΩ", result.Text);
    }

    [TestMethod]
    public void DeterministicText_NormalizesUnicodeAndLineEndings()
    {
        string result = DeterministicText.Normalize("e\u0301\r\nnext\rlast");

        Assert.AreEqual("é\nnext\nlast", result);
    }

    [TestMethod]
    public void UtcDocumentTimestamp_ValidFileTime_IsUtcAndInvalidIsRejected()
    {
        long fileTime = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero).ToFileTime();

        Assert.IsTrue(UtcDocumentTimestamp.TryFromFileTime(fileTime, out var timestamp));
        Assert.AreEqual(TimeSpan.Zero, timestamp.Value.Offset);
        Assert.IsFalse(UtcDocumentTimestamp.TryFromFileTime(long.MaxValue, out _));
    }
}
