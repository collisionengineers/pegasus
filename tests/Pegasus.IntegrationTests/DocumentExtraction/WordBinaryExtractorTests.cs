using Xunit;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

namespace Pegasus.IntegrationTests.DocumentExtraction;

public sealed class WordBinaryExtractorTests
{
    [Fact]
    public void ExtractRawCompoundFileBytesTraversesStorageAndExtractsText()
    {
        byte[] bytes = WordBinaryFixture.CreateRawCfb([new(0, 2, 700, false, "Hi")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(bytes);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("Hi", result.Stories[0].Text);
        Assert.Equal("0Table", result.SelectedTableStream);
    }

    [Fact]
    public void ExtractRawCompoundFileBytesEnforcesDeclaredInputLimitAtBoundary()
    {
        byte[] bytes = WordBinaryFixture.CreateRawCfb([new(0, 2, 700, false, "Hi")]);

        WordBinaryExtractionResult exact = WordBinaryExtractor.Extract(
            bytes, WordBinaryExtractionLimits.Default with { MaximumInputBytes = bytes.Length });
        WordBinaryExtractionResult exceeded = WordBinaryExtractor.Extract(
            bytes, WordBinaryExtractionLimits.Default with { MaximumInputBytes = bytes.Length - 1 });

        Assert.Equal(WordBinaryOutcome.Complete, exact.Outcome);
        Assert.Equal(WordBinaryOutcome.ResourceLimitExceeded, exceeded.Outcome);
        Assert.Equal("doc-input-limit", Assert.Single(exceeded.Issues).Code);
    }

    [Theory]
    [InlineData((ushort)0x00c1)]
    [InlineData((ushort)0x00d9)]
    [InlineData((ushort)0x0101)]
    [InlineData((ushort)0x010c)]
    [InlineData((ushort)0x0112)]
    public void ExtractDeclaredWord97FamilyVersionsAreAccepted(ushort version)
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "Hi")], version: version);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal(version, result.Fib!.EffectiveVersion);
    }

    [Fact]
    public void ExtractMixedDisorderedPiecesReturnsLogicalTextAndExactProvenance()
    {
        CompoundFile file = WordBinaryFixture.Create(
        [
            new(0, 4, 900, false, "A€B\r"),
            new(4, 6, 600, true, "Ω!"),
        ]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("A€B\nΩ!", result.Stories[0].Text);
        Assert.Equal(2, result.Pieces.Length);
        Assert.Equal((uint)900, result.Pieces[0].FileOffset);
        Assert.Equal((uint)600, result.Pieces[1].FileOffset);
        Assert.Equal((uint)0, result.Stories[0].Segments[0].GlobalCpStart);
        Assert.Equal((uint)900, result.Stories[0].Segments[0].FileOffset);
        Assert.Equal(WordTextSegmentKind.ParagraphMark, result.Stories[0].Segments[1].Kind);
        Assert.Equal((uint)903, result.Stories[0].Segments[1].FileOffset);
        Assert.Equal((uint)600, result.Stories[0].Segments[2].FileOffset);
    }

    [Fact]
    public void ExtractOneTableAndExtendedFibVersionSelectsDeclaredStreamAndVersion()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], useOneTable: true, effectiveVersion: 0x0112);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("1Table", result.SelectedTableStream);
        Assert.NotNull(result.Fib);
        Assert.Equal((ushort)0x0112, result.Fib.EffectiveVersion);
    }

    [Fact]
    public void ExtractNonzeroFib97FileTimeIsNotValidatedAsTableRange()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, _) =>
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(word.AsSpan(152), 93);
                WordBinaryFixture.SetFibRange(word, 87, 0x7F500000, 0x01DCD3DE);
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("Hi", result.Stories[0].Text);
        Assert.False(result.Fib!.RangeCatalogue[87].IsOffsetLengthPair);
    }

    [Fact]
    public void ExtractEncryptedDocumentClassifiesWithoutReadingText()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 4, 700, false, "hide")], encrypted: true, obfuscated: true);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Encrypted, result.Outcome);
        Assert.Empty(result.Stories);
        Assert.Equal("doc-encrypted", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractMissingSelectedTableIsCorruptAndVisible()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], includeSelectedTable: false);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.Equal("doc-table-stream-missing", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractPre97IdentifierIsExplicitUnsupportedFeature()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], identifier: 0xa5dc);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.UnsupportedFeature, result.Outcome);
        Assert.Equal("doc-pre97-unsupported", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractUnsupportedEffectiveVersionIsExplicitUnsupportedFeature()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], effectiveVersion: 0x0200);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.UnsupportedFeature, result.Outcome);
        Assert.Equal("doc-fib-version-unsupported", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractReservedPieceAddressBitIsCorrupt()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], malformedReservedFc: true);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.Equal("doc-piece-fc-reserved", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractPieceExtentDoesNotMatchStoriesIsCorrupt()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], storyLengths: [1, 0, 0, 0, 0, 0, 0, 0]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.Equal("doc-story-piece-mismatch", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractSecondaryFibIsVisiblePartialBecauseItsStoriesAreNeverDecoded()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], nextFibPage: 2);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Partial, result.Outcome);
        Assert.Contains("doc-secondary-fib-unprocessed", result.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ExtractDeclaredPicturesAreRecordedWithoutClaimingTextIsMissing()
    {
        // A picture is not document text and is never opened (ADR-0025), so its
        // presence is recorded and the text read stays complete.
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], hasPictures: true);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Contains("doc-pictures-unprocessed", result.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ExtractStoryCatalogueReturnsOrderedSecondaryStoryAndRecordsItsAnchorIssue()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 7, 700, false, "abc\rde\r")], storyLengths: [4, 2, 0, 0, 0, 0, 0, 0]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        // The footnote story text WAS decoded; only its anchor is unresolved,
        // which loses no text and so does not make the read incomplete.
        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal(8, result.Stories.Length);
        Assert.Equal(WordStoryKind.Main, result.Stories[0].Kind);
        Assert.Equal("abc\n", result.Stories[0].Text);
        Assert.Equal(WordStoryKind.Footnote, result.Stories[1].Kind);
        Assert.Equal("de", result.Stories[1].Text);
        Assert.Equal((uint)4, result.Stories[1].GlobalCpStart);
        Assert.Contains("doc-secondary-story-unanchored", result.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ExtractControlMarkersProjectsSafeTextAndRetainsTokens()
    {
        string controls = "a\tb\vc\fd\u0013x\u0014y\u0015z\u001e\u001f";
        CompoundFile file = WordBinaryFixture.Create([new(0, (uint)controls.Length, 700, true, controls)]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
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

    /// <summary>
    /// Every semantic control marker the extractor classifies. Each is recorded
    /// under its own issue code so it stays visible to a reviewer, and none of
    /// them removes a character of the text the reader hands on, so the read
    /// they appear in is complete.
    /// </summary>
    [Theory]
    [InlineData("\u0001", (int)WordTextSegmentKind.Picture)]
    [InlineData("\u0002", (int)WordTextSegmentKind.FootnoteOrEndnoteReference)]
    [InlineData("a\u0007", (int)WordTextSegmentKind.CellOrRowMark)]
    [InlineData("\u0008", (int)WordTextSegmentKind.EmbeddedObjectMarker)]
    [InlineData("\u000c", (int)WordTextSegmentKind.PageOrSectionBreak)]
    [InlineData("\u0013", (int)WordTextSegmentKind.FieldBegin)]
    [InlineData("\u0014", (int)WordTextSegmentKind.FieldSeparator)]
    [InlineData("\u0015", (int)WordTextSegmentKind.FieldEnd)]
    [InlineData("\u0003", (int)WordTextSegmentKind.UnsupportedControl)]
    public void ExtractSemanticControlMarkersAreRecordedWithoutDegradingTheOutcome(
        string text, int expectedKind)
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, (uint)text.Length, 700, true, text)]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Contains((WordTextSegmentKind)expectedKind, result.Stories[0].Segments.Select(static segment => segment.Kind));
        Assert.Contains(
            $"doc-control-semantic-partial:{(WordTextSegmentKind)expectedKind}",
            result.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ExtractStrayControlByteIsStrippedFromTheProjectedText()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 5, 700, true, "AB\u0003CD")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("ABCD", result.Stories[0].Text);
    }

    [Fact]
    public void ExtractPieceCountAboveConfiguredMaximumReturnsResourceLimitExceeded()
    {
        CompoundFile file = WordBinaryFixture.Create(
        [
            new(0, 1, 700, false, "A"),
            new(1, 2, 800, false, "B"),
        ]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumPieces = 1 });

        Assert.Equal(WordBinaryOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.Equal("doc-piece-count-limit", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractCharacterExtentAboveConfiguredMaximumReturnsResourceLimitExceeded()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "AB")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumCharacters = 1 });

        Assert.Equal(WordBinaryOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.Equal("doc-character-count-limit", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractPieceAndCharacterLimitsAtExactExtentAreAccepted()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "AB")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumPieces = 1, MaximumCharacters = 2 });

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("AB", result.Stories[0].Text);
    }

    /// <summary>
    /// The read every genuine legacy instruction in the corpus actually is: a
    /// clean single save, so the fast-save flag is unset, carrying the style
    /// sheet and document-property ranges Word always writes. Both conditions
    /// are recorded, neither removes text, and the read is complete.
    /// </summary>
    [Fact]
    public void ExtractNormalNonFastSavedFileCarryingAStyleSheetIsComplete()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], addUnprocessedRange: true, complex: false);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("Hi", result.Stories[0].Text);
        string[] codes = result.Issues.Select(static issue => issue.Code).ToArray();
        Assert.Contains("doc-fib-ranges-unprocessed", codes);
        Assert.Contains("doc-complex-flag-unset", codes);
    }

    [Fact]
    public void ExtractReservedCharacterSetByteIsIgnoredAndDecodesWindows1252()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 1, 700, false, "€")], characterSet: 128);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Complete, result.Outcome);
        Assert.Equal("€", result.Stories[0].Text);
    }

    [Fact]
    public void ExtractCompressedByteWithoutWindows1252MappingIsReplacedAndVisible()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 1, 700, false, "\u0081")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Partial, result.Outcome);
        Assert.Equal("�", result.Stories[0].Text);
        Assert.Contains("doc-codepage-unsupported", result.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ExtractPieceBeyondDeclaredCbMacIsCorrupt()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, _) =>
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(word.AsSpan(64), 700));

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Corrupt, result.Outcome);
        Assert.Equal("doc-piece-byte-range", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExtractLoneSurrogateInUnicodePieceIsReplacedAndVisible()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 3, 700, true, "A\ud800B")]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.Equal(WordBinaryOutcome.Partial, result.Outcome);
        Assert.Equal("A�B", result.Stories[0].Text);
        Assert.Contains("doc-lone-surrogate-replaced", result.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ExtractNonCfbFamiliesAreClassifiedWithoutExtensionTrust()
    {
        WordBinaryExtractionResult rtf = WordBinaryExtractor.Extract("{\\rtf1 test}"u8.ToArray());
        WordBinaryExtractionResult pdf = WordBinaryExtractor.Extract("%PDF-2.0"u8.ToArray());
        WordBinaryExtractionResult zip = WordBinaryExtractor.Extract("PK\x03\x04rest"u8.ToArray());

        Assert.Equal(WordBinaryOutcome.UnsupportedFormat, rtf.Outcome);
        Assert.Equal("rtf", rtf.DetectedFamily);
        Assert.Equal("pdf", pdf.DetectedFamily);
        Assert.Equal("zip-or-ooxml", zip.DetectedFamily);
    }

    [Fact]
    public void ExtractCancelledTokenReturnsCancelledOutcome()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "Hi")]);
        using var source = new CancellationTokenSource();
        source.Cancel();

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file, cancellationToken: source.Token);

        Assert.Equal(WordBinaryOutcome.Cancelled, result.Outcome);
    }
}
