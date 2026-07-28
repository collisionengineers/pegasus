using CollisionDocNet.Storage.CompoundFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CollisionDocNet.Writer.Tests;

[TestClass]
public sealed class WordBinaryExtractorTests
{
    [TestMethod]
    public void Extract_RawCompoundFileBytes_TraversesStorageAndExtractsText()
    {
        byte[] bytes = WordBinaryFixture.CreateRawCfb([new(0, 2, 700, false, "Hi")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(bytes);

        Assert.AreEqual(WordBinaryOutcome.Complete, result.Outcome);
        Assert.AreEqual("Hi", result.Stories[0].Text);
        Assert.AreEqual("0Table", result.SelectedTableStream);
    }

    [TestMethod]
    public void Extract_RawCompoundFileBytes_EnforcesDeclaredInputLimitAtBoundary()
    {
        byte[] bytes = WordBinaryFixture.CreateRawCfb([new(0, 2, 700, false, "Hi")]);

        WordBinaryExtractionResult exact = WordBinaryExtractor.Extract(
            bytes, WordBinaryExtractionLimits.Default with { MaximumInputBytes = bytes.Length });
        WordBinaryExtractionResult exceeded = WordBinaryExtractor.Extract(
            bytes, WordBinaryExtractionLimits.Default with { MaximumInputBytes = bytes.Length - 1 });

        Assert.AreEqual(WordBinaryOutcome.Complete, exact.Outcome);
        Assert.AreEqual(WordBinaryOutcome.ResourceLimitExceeded, exceeded.Outcome);
        Assert.AreEqual("doc-input-limit", Assert.ContainsSingle(exceeded.Issues).Code);
    }

    [TestMethod]
    [DataRow((ushort)0x00c1)]
    [DataRow((ushort)0x00d9)]
    [DataRow((ushort)0x0101)]
    [DataRow((ushort)0x010c)]
    [DataRow((ushort)0x0112)]
    public void Extract_DeclaredWord97FamilyVersions_AreAccepted(ushort version)
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "Hi")], version: version);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Complete, result.Outcome);
        Assert.AreEqual(version, result.Fib!.EffectiveVersion);
    }

    [TestMethod]
    public void Extract_MixedDisorderedPieces_ReturnsLogicalTextAndExactProvenance()
    {
        CompoundFile file = WordBinaryFixture.Create(
        [
            new(0, 4, 900, false, "A€B\r"),
            new(4, 6, 600, true, "Ω!"),
        ]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Complete, result.Outcome);
        Assert.AreEqual("A€B\nΩ!", result.Stories[0].Text);
        Assert.HasCount(2, result.Pieces);
        Assert.AreEqual((uint)900, result.Pieces[0].FileOffset);
        Assert.AreEqual((uint)600, result.Pieces[1].FileOffset);
        Assert.AreEqual((uint)0, result.Stories[0].Segments[0].GlobalCpStart);
        Assert.AreEqual((uint)900, result.Stories[0].Segments[0].FileOffset);
        Assert.AreEqual(WordTextSegmentKind.ParagraphMark, result.Stories[0].Segments[1].Kind);
        Assert.AreEqual((uint)903, result.Stories[0].Segments[1].FileOffset);
        Assert.AreEqual((uint)600, result.Stories[0].Segments[2].FileOffset);
    }

    [TestMethod]
    public void Extract_OneTableAndExtendedFibVersion_SelectsDeclaredStreamAndVersion()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], useOneTable: true, effectiveVersion: 0x0112);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Complete, result.Outcome);
        Assert.AreEqual("1Table", result.SelectedTableStream);
        Assert.IsNotNull(result.Fib);
        Assert.AreEqual((ushort)0x0112, result.Fib.EffectiveVersion);
    }

    [TestMethod]
    public void Extract_NonzeroFib97FileTime_IsNotValidatedAsTableRange()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, _) =>
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(word.AsSpan(152), 93);
                WordBinaryFixture.SetFibRange(word, 87, 0x7F500000, 0x01DCD3DE);
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Complete, result.Outcome,
            string.Join(',', result.Issues.Select(static issue => issue.Code)));
        Assert.AreEqual("Hi", result.Stories[0].Text);
        Assert.IsFalse(result.Fib!.RangeCatalogue[87].IsOffsetLengthPair);
    }

    [TestMethod]
    public void Extract_EncryptedDocument_ClassifiesWithoutReadingText()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 4, 700, false, "hide")], encrypted: true, obfuscated: true);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Encrypted, result.Outcome);
        Assert.IsEmpty(result.Stories);
        Assert.AreEqual("doc-encrypted", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_MissingSelectedTable_IsCorruptAndVisible()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], includeSelectedTable: false);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.AreEqual("doc-table-stream-missing", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_Pre97Identifier_IsExplicitUnsupportedFeature()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], identifier: 0xa5dc);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.UnsupportedFeature, result.Outcome);
        Assert.AreEqual("doc-pre97-unsupported", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_UnsupportedEffectiveVersion_IsExplicitUnsupportedFeature()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], effectiveVersion: 0x0200);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.UnsupportedFeature, result.Outcome);
        Assert.AreEqual("doc-fib-version-unsupported", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_ReservedPieceAddressBit_IsCorrupt()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], malformedReservedFc: true);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.AreEqual("doc-piece-fc-reserved", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_PieceExtentDoesNotMatchStories_IsCorrupt()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], storyLengths: [1, 0, 0, 0, 0, 0, 0, 0]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.AreEqual("doc-story-piece-mismatch", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_PicturesAndSecondaryFib_ForceVisiblePartialOutcome()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], hasPictures: true, nextFibPage: 2);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        string[] codes = result.Issues.Select(static issue => issue.Code).ToArray();
        Assert.Contains("doc-pictures-unprocessed", codes);
        Assert.Contains("doc-secondary-fib-unprocessed", codes);
    }

    [TestMethod]
    public void Extract_StoryCatalogue_ReturnsOrderedSecondaryStoryAndPartialAnchorIssue()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 6, 700, false, "abc\rde")], storyLengths: [3, 2, 0, 0, 0, 0, 0, 0]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        Assert.HasCount(8, result.Stories);
        Assert.AreEqual(WordStoryKind.Main, result.Stories[0].Kind);
        Assert.AreEqual("abc", result.Stories[0].Text);
        Assert.AreEqual(WordStoryKind.Footnote, result.Stories[1].Kind);
        Assert.AreEqual("de", result.Stories[1].Text);
        Assert.AreEqual((uint)4, result.Stories[1].GlobalCpStart);
        Assert.Contains("doc-secondary-story-unanchored", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public void Extract_ControlMarkers_ProjectsSafeTextAndRetainsTokens()
    {
        string controls = "a\tb\vc\fd\u0013x\u0014y\u0015z\u001e\u001f";
        CompoundFile file = WordBinaryFixture.Create([new(0, (uint)controls.Length, 700, true, controls)]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        WordStory story = result.Stories[0];
        WordTextSegmentKind[] kinds = story.Segments.Select(static segment => segment.Kind).ToArray();
        Assert.Contains(WordTextSegmentKind.Tab, kinds);
        Assert.Contains(WordTextSegmentKind.LineBreak, kinds);
        Assert.Contains(WordTextSegmentKind.PageOrSectionBreak, kinds);
        Assert.Contains(WordTextSegmentKind.FieldBegin, kinds);
        Assert.Contains(WordTextSegmentKind.FieldSeparator, kinds);
        Assert.Contains(WordTextSegmentKind.FieldEnd, kinds);
        Assert.Contains(WordTextSegmentKind.NonBreakingHyphen, kinds);
        Assert.Contains(WordTextSegmentKind.OptionalHyphen, kinds);
    }

    [TestMethod]
    [DataRow("\u0013", WordTextSegmentKind.FieldBegin)]
    [DataRow("\u0014", WordTextSegmentKind.FieldSeparator)]
    [DataRow("\u0015", WordTextSegmentKind.FieldEnd)]
    [DataRow("\u0002", WordTextSegmentKind.FootnoteOrEndnoteReference)]
    public void Extract_UnpairedSemanticControls_ForcePartialOutcome(string text, WordTextSegmentKind expectedKind)
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, (uint)text.Length, 700, true, text)]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        Assert.Contains(expectedKind, result.Stories[0].Segments.Select(static segment => segment.Kind));
        Assert.Contains("doc-control-semantic-partial", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public void Extract_PieceCountAboveConfiguredMaximum_ReturnsResourceLimitExceeded()
    {
        CompoundFile file = WordBinaryFixture.Create(
        [
            new(0, 1, 700, false, "A"),
            new(1, 2, 800, false, "B"),
        ]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumPieces = 1 });

        Assert.AreEqual(WordBinaryOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.AreEqual("doc-piece-count-limit", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_CharacterExtentAboveConfiguredMaximum_ReturnsResourceLimitExceeded()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "AB")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumCharacters = 1 });

        Assert.AreEqual(WordBinaryOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.AreEqual("doc-character-count-limit", Assert.ContainsSingle(result.Issues).Code);
    }

    [TestMethod]
    public void Extract_PieceAndCharacterLimitsAtExactExtent_AreAccepted()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "AB")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumPieces = 1, MaximumCharacters = 2 });

        Assert.AreEqual(WordBinaryOutcome.Complete, result.Outcome);
        Assert.AreEqual("AB", result.Stories[0].Text);
    }

    [TestMethod]
    public void Extract_PropertyBranches_ForceVisiblePartialOutcome()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi", Prm: 1)], addUnprocessedRange: true);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        string[] codes = result.Issues.Select(static issue => issue.Code).ToArray();
        Assert.Contains("doc-prm-reference-invalid", codes);
        Assert.Contains("doc-fib-ranges-unprocessed", codes);
    }

    [TestMethod]
    public void Extract_UnsupportedCompressedCodePage_DoesNotSilentlyDecode()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 1, 700, false, "€")], characterSet: 128);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        Assert.AreEqual("�", result.Stories[0].Text);
        Assert.Contains("doc-codepage-unsupported", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public void Extract_NonCfbFamilies_AreClassifiedWithoutExtensionTrust()
    {
        WordBinaryExtractionResult rtf = WordBinaryExtractor.Extract("{\\rtf1 test}"u8.ToArray());
        WordBinaryExtractionResult pdf = WordBinaryExtractor.Extract("%PDF-2.0"u8.ToArray());
        WordBinaryExtractionResult zip = WordBinaryExtractor.Extract("PK\x03\x04rest"u8.ToArray());

        Assert.AreEqual(WordBinaryOutcome.UnsupportedFormat, rtf.Outcome);
        Assert.AreEqual("rtf", rtf.DetectedFamily);
        Assert.AreEqual("pdf", pdf.DetectedFamily);
        Assert.AreEqual("zip-or-ooxml", zip.DetectedFamily);
    }

    [TestMethod]
    public void Extract_CancelledToken_ReturnsCancelledOutcome()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "Hi")]);
        using var source = new CancellationTokenSource();
        source.Cancel();

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file, cancellationToken: source.Token);

        Assert.AreEqual(WordBinaryOutcome.Cancelled, result.Outcome);
    }
}
