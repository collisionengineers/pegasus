using System.Buffers.Binary;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CollisionDocNet.Writer.Tests;

// Test-only oracle derived independently from MS-DOC 12.5. It deliberately does
// not call WordFibParser, WordPieceTableParser, or WordBinaryExtractor.
[TestClass]
public sealed class DocR03ExecutableSpecificationTests
{
    private static readonly Dictionary<byte, char> CompressedOverrides = new()
    {
        [0x82] = '\u201A',
        [0x83] = '\u0192',
        [0x84] = '\u201E',
        [0x85] = '\u2026',
        [0x86] = '\u2020',
        [0x87] = '\u2021',
        [0x88] = '\u02C6',
        [0x89] = '\u2030',
        [0x8A] = '\u0160',
        [0x8B] = '\u2039',
        [0x8C] = '\u0152',
        [0x91] = '\u2018',
        [0x92] = '\u2019',
        [0x93] = '\u201C',
        [0x94] = '\u201D',
        [0x95] = '\u2022',
        [0x96] = '\u2013',
        [0x97] = '\u2014',
        [0x98] = '\u02DC',
        [0x99] = '\u2122',
        [0x9A] = '\u0161',
        [0x9B] = '\u203A',
        [0x9C] = '\u0153',
        [0x9F] = '\u0178',
    };

    [TestMethod]
    [DataRow((ushort)0x00C1, (byte)0, (byte)0)]
    [DataRow((ushort)0x00C1, (byte)15, (byte)0)]
    [DataRow((ushort)0x00D9, (byte)15, (byte)0)]
    [DataRow((ushort)0x0101, (byte)15, (byte)5)]
    [DataRow((ushort)0x010C, (byte)15, (byte)10)]
    [DataRow((ushort)0x0112, (byte)15, (byte)15)]
    public void QuickSaveRules_AcceptEverySupportedVersionBoundary(
        ushort nFib, byte baseCount, byte extendedCount)
    {
        Assert.IsTrue(IsValidQuickSave(nFib, baseCount, extendedCount));
    }

    [TestMethod]
    [DataRow((ushort)0x00C1, (byte)16, (byte)0)]
    [DataRow((ushort)0x00D9, (byte)14, (byte)0)]
    [DataRow((ushort)0x0101, (byte)0, (byte)0)]
    [DataRow((ushort)0x010C, (byte)15, (byte)16)]
    [DataRow((ushort)0x0112, (byte)15, (byte)255)]
    public void QuickSaveRules_RejectVersionInvalidValues(
        ushort nFib, byte baseCount, byte extendedCount)
    {
        Assert.IsFalse(IsValidQuickSave(nFib, baseCount, extendedCount));
    }

    [TestMethod]
    public void PlcPcd_MapsCompressedAndUnicodePiecesInLogicalCpOrder()
    {
        byte[] plc = BuildPlcPcd(
            cps: [0u, 2u, 4u],
            pieces:
            [
                new PieceDescriptor(700, Compressed: true, NoParagraphLast: false),
                new PieceDescriptor(100, Compressed: false, NoParagraphLast: true),
            ]);

        IReadOnlyList<Piece> pieces = ParsePlcPcd(plc);

        Assert.HasCount(2, pieces);
        Assert.AreEqual(new Piece(0, 2, 700, 2, true, false, 0), pieces[0]);
        Assert.AreEqual(new Piece(2, 4, 100, 4, false, true, 0), pieces[1]);
        Assert.IsGreaterThan(pieces[1].FileOffset, pieces[0].FileOffset, "Physical order must not replace CP order.");
    }

    [TestMethod]
    public void PlcPcd_RejectsEveryCpAndRecordBoundaryViolation()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ParsePlcPcd([]));
        Assert.ThrowsExactly<InvalidDataException>(() => ParsePlcPcd(new byte[4]));
        Assert.ThrowsExactly<InvalidDataException>(() => ParsePlcPcd(new byte[15]));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParsePlcPcd(BuildPlcPcd([1u, 2u], [new(0, true, false)])));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParsePlcPcd(BuildPlcPcd([0u, 0u], [new(0, true, false)])));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParsePlcPcd(BuildPlcPcd([0u, 0x7FFFFFFFu], [new(0, true, false)])));
    }

    [TestMethod]
    public void CompressedDecoder_ExercisesAll256ByteValues()
    {
        byte[] bytes = Enumerable.Range(0, 256).Select(static value => (byte)value).ToArray();
        string decoded = DecodeCompressed(bytes);

        Assert.AreEqual(256, decoded.Length);
        for (int value = 0; value < 256; value++)
        {
            char expected = CompressedOverrides.TryGetValue((byte)value, out char mapped)
                ? mapped
                : (char)value;
            Assert.AreEqual(expected, decoded[value], $"Unexpected mapping for 0x{value:X2}.");
        }
        Assert.AreEqual('\u0080', decoded[0x80]);
        Assert.AreEqual('\u008E', decoded[0x8E]);
        Assert.AreEqual('\u009E', decoded[0x9E]);
    }

    [TestMethod]
    public void UnicodeDecoder_HandlesPairsAcrossPiecesAndRejectsIsolatedUnits()
    {
        Assert.AreEqual("\U0001F642", DecodeUnicodeUnits([0xD83D, 0xDE42]));
        Assert.AreEqual("\uFFFDx\uFFFD", DecodeUnicodeUnits([0xD83D, (ushort)'x', 0xDE42]));
    }

    [TestMethod]
    public void StoryRanges_CoverEveryPartAndPlaceOneGuardAfterTheLastSpecializedPart()
    {
        string[] kinds = ["Main", "Footnote", "Header", "Comment", "Endnote", "MainTextbox", "HeaderTextbox"];
        for (int specialized = 1; specialized < kinds.Length; specialized++)
        {
            uint[] counts = [2, 0, 0, 0, 0, 0, 0];
            counts[specialized] = 3;
            StoryLayout layout = BuildStoryLayout(counts, reserved3: 0);

            Assert.HasCount(7, layout.Ranges);
            Assert.AreEqual(kinds[specialized], layout.Ranges[specialized].Kind);
            Assert.AreEqual(2u, layout.Ranges[specialized].StartCp);
            Assert.AreEqual(5u, layout.Ranges[specialized].EndCp);
            Assert.AreEqual(5u, layout.OutsideGuardCp);
            Assert.AreEqual(6u, layout.FinalCp);
        }
    }

    [TestMethod]
    public void StoryRanges_UseContiguousCumulativeRangesAndNoGuardForMainOnly()
    {
        StoryLayout all = BuildStoryLayout([2, 1, 2, 3, 4, 5, 6], reserved3: 0);
        Assert.AreEqual(0u, all.Ranges[0].StartCp);
        Assert.AreEqual(2u, all.Ranges[1].StartCp);
        Assert.AreEqual(3u, all.Ranges[2].StartCp);
        Assert.AreEqual(23u, all.OutsideGuardCp);
        Assert.AreEqual(24u, all.FinalCp);

        StoryLayout mainOnly = BuildStoryLayout([2, 0, 0, 0, 0, 0, 0], reserved3: 0);
        Assert.IsNull(mainOnly.OutsideGuardCp);
        Assert.AreEqual(2u, mainOnly.FinalCp);
        Assert.ThrowsExactly<InvalidDataException>(() => BuildStoryLayout([2, 0, 0, 0, 0, 0, 0], reserved3: 1));
    }

    [TestMethod]
    public void NoParagraphLast_RejectsParagraphMarkOnlyWhenBitIsSet()
    {
        Assert.IsTrue(IsValidNoParagraphLast(false, "a\rb"));
        Assert.IsTrue(IsValidNoParagraphLast(true, "ab"));
        Assert.IsFalse(IsValidNoParagraphLast(true, "a\rb"));
    }

    [TestMethod]
    [DataRow((ushort)0x00C1, 93, 0, 900, false)]
    [DataRow((ushort)0x00C1, 93, 0, 900, true)]
    [DataRow((ushort)0x00D9, 108, 2, 1024, false)]
    [DataRow((ushort)0x00D9, 108, 2, 1024, true)]
    [DataRow((ushort)0x0101, 136, 2, 1248, false)]
    [DataRow((ushort)0x0101, 136, 2, 1248, true)]
    [DataRow((ushort)0x010C, 164, 2, 1472, false)]
    [DataRow((ushort)0x010C, 164, 2, 1472, true)]
    [DataRow((ushort)0x0112, 183, 5, 1630, false)]
    [DataRow((ushort)0x0112, 183, 5, 1630, true)]
    public void FibLayouts_UseExactIndependentVersionShape(
        ushort nFib, int rangeCount, int newWordCount, int totalLength, bool isComplex)
    {
        byte[] bytes = BuildFibLayout(nFib, rangeCount, newWordCount, totalLength, isComplex, cbMac: 2048);

        FibLayout layout = ReadFibLayout(bytes);

        Assert.AreEqual(nFib, layout.BaseVersion);
        Assert.AreEqual(nFib, layout.EffectiveVersion);
        Assert.AreEqual(14, layout.ShortWordCount);
        Assert.AreEqual(22, layout.LongWordCount);
        Assert.AreEqual(rangeCount, layout.RangeCount);
        Assert.AreEqual(newWordCount, layout.NewWordCount);
        Assert.AreEqual(totalLength, layout.ConsumedBytes);
        Assert.AreEqual(isComplex, layout.IsComplex);
        Assert.AreEqual(2048u, layout.CbMac);
        Assert.AreEqual(1u, layout.PartCounts[0]);
        Assert.AreEqual(0u, layout.ReservedPartCount);
    }

    [TestMethod]
    public void FibLayouts_RejectCrossVersionCountsAndTrailingShortLayouts()
    {
        byte[] d9WithC1Ranges = BuildFibLayout(0x00D9, 93, 0, 900, isComplex: false, cbMac: 2048);
        byte[] truncated112 = BuildFibLayout(0x0112, 183, 5, 1630, isComplex: true, cbMac: 2048)[..^1];

        Assert.ThrowsExactly<InvalidDataException>(() => ReadFibLayout(d9WithC1Ranges));
        Assert.ThrowsExactly<InvalidDataException>(() => ReadFibLayout(truncated112));
    }

    [TestMethod]
    public void Clx_AcceptsZeroOneAndMultiplePrcsBeforeOneFinalPcdt()
    {
        byte[] plc = BuildPlcPcd([0u, 1u], [new(40, true, false)]);
        byte[] fixedFourByteOperand = [0x00, 0x60, 0x01, 0x02, 0x03, 0x04];
        byte[] variableOperand = [0x00, 0xC0, 0x02, 0xAA, 0xBB];
        byte[] tDefTableOperand = [0x08, 0xD6, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00];
        byte[] pChgTabsExtendedOperand = [0x15, 0xC6, 0xFF, 0x00, 0x00];

        ClxResult zero = ParseClx(BuildClx([], plc));
        ClxResult one = ParseClx(BuildClx([[0x35, 0x08, 0x01]], plc));
        ClxResult multiple = ParseClx(BuildClx(
            [[0x35, 0x08, 0x01],
             [.. fixedFourByteOperand, .. variableOperand, .. tDefTableOperand, .. pChgTabsExtendedOperand]], plc));

        Assert.IsEmpty(zero.PropertyRecords);
        Assert.HasCount(1, one.PropertyRecords);
        Assert.HasCount(2, multiple.PropertyRecords);
        byte[] expectedVariableRecords =
            [.. fixedFourByteOperand, .. variableOperand, .. tDefTableOperand, .. pChgTabsExtendedOperand];
        CollectionAssert.AreEqual(expectedVariableRecords, multiple.PropertyRecords[1]);
        Assert.HasCount(1, zero.Pieces);
        Assert.HasCount(1, one.Pieces);
        Assert.HasCount(1, multiple.Pieces);
    }

    [TestMethod]
    public void Clx_RejectsMissingDuplicatedNonFinalAndMalformedRecords()
    {
        byte[] plc = BuildPlcPcd([0u, 1u], [new(40, true, false)]);
        byte[] pcdt = BuildClx([], plc);
        byte[] duplicated = [.. pcdt, .. pcdt];
        byte[] trailing = [.. pcdt, 0x00];
        byte[] pcdtThenPrc = [.. pcdt, 0x01, 0x03, 0x00, 0x35, 0x08, 0x01];
        byte[] negativePrc = [0x01, 0xFF, 0xFF, .. pcdt];
        byte[] oversizedPrc = [0x01, 0xA3, 0x3F, .. new byte[0x3FA3], .. pcdt];
        byte[] incompletePrl = [0x01, 0x02, 0x00, 0x35, 0x08, .. pcdt];

        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx([]));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx([0x01, 0x00, 0x00]));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx(duplicated));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx(trailing));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx(pcdtThenPrc));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx(negativePrc));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx(oversizedPrc));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseClx(incompletePrl));
    }

    [TestMethod]
    public void PiecePrm_ResolvesPrm0AndPrecedingPrcIndexes()
    {
        const ushort prm0 = (ushort)((0x5A << 8) | (85 << 1));
        const ushort prm1First = (0 << 1) | 1;
        const ushort prm1Second = (1 << 1) | 1;
        byte[] prc0 = [0x35, 0x08, 0x01];
        byte[] prc1 = [0x36, 0x08, 0x00];
        byte[] plc = BuildPlcPcd(
            [0u, 1u, 2u, 3u],
            [new(40, true, false, prm0), new(42, true, false, prm1First), new(44, true, false, prm1Second)]);

        ClxResult result = ParseClx(BuildClx([prc0, prc1], plc));

        PrmResolution simple = ResolvePrm(result.Pieces[0].PropertyModifier, result.PropertyRecords);
        PrmResolution first = ResolvePrm(result.Pieces[1].PropertyModifier, result.PropertyRecords);
        PrmResolution second = ResolvePrm(result.Pieces[2].PropertyModifier, result.PropertyRecords);
        Assert.AreEqual("Prm0", simple.Kind);
        Assert.AreEqual(85, simple.IndexOrIsprm);
        Assert.AreEqual(0x5A, simple.Value);
        Assert.AreEqual("Prm1", first.Kind);
        CollectionAssert.AreEqual(prc0, first.PropertyRecord);
        Assert.AreEqual("Prm1", second.Kind);
        CollectionAssert.AreEqual(prc1, second.PropertyRecord);
    }

    [TestMethod]
    public void PiecePrm_RejectsPrm1WithoutAnExistingPrecedingPrc()
    {
        const ushort firstPrc = 1;
        const ushort secondPrc = 3;

        Assert.ThrowsExactly<InvalidDataException>(() => ResolvePrm(firstPrc, []));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ResolvePrm(secondPrc, [[0x35, 0x08, 0x01]]));
    }

    [TestMethod]
    public void PieceBounds_AcceptExactCbMacAndRejectOneByteOverForBothEncodings()
    {
        Piece compressed = new(0, 2, 98, 2, true, false, 0);
        Piece unicode = new(0, 2, 96, 4, false, false, 0);

        Assert.AreEqual(100, ValidatePieceBounds(compressed, wordDocumentLength: 120, cbMac: 100));
        Assert.AreEqual(100, ValidatePieceBounds(unicode, wordDocumentLength: 120, cbMac: 100));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ValidatePieceBounds(compressed with { FileOffset = 99 }, wordDocumentLength: 120, cbMac: 100));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ValidatePieceBounds(unicode with { FileOffset = 97 }, wordDocumentLength: 120, cbMac: 100));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ValidatePieceBounds(compressed, wordDocumentLength: 99, cbMac: 100));
    }

    [TestMethod]
    public void HeaderStories_UseSixPrefixKindsThenSixKindsPerSectionAndExcludeGuards()
    {
        List<HeaderStory> stories = BuildHeaderStories(sectionCount: 2);

        string[] prefix =
        [
            "FootnoteSeparator", "FootnoteContinuationSeparator", "FootnoteContinuationNotice",
            "EndnoteSeparator", "EndnoteContinuationSeparator", "EndnoteContinuationNotice",
        ];
        string[] sectionKinds = ["EvenHeader", "OddHeader", "EvenFooter", "OddFooter", "FirstHeader", "FirstFooter"];
        Assert.HasCount(18, stories);
        CollectionAssert.AreEqual(prefix, stories.Take(6).Select(static story => story.Kind).ToArray());
        CollectionAssert.AreEqual(sectionKinds, stories.Skip(6).Take(6).Select(static story => story.Kind).ToArray());
        CollectionAssert.AreEqual(sectionKinds, stories.Skip(12).Take(6).Select(static story => story.Kind).ToArray());
        Assert.IsTrue(stories.Take(6).All(static story => story.SectionIndex is null));
        Assert.IsTrue(stories.Skip(6).Take(6).All(static story => story.SectionIndex == 0));
        Assert.IsTrue(stories.Skip(12).All(static story => story.SectionIndex == 1));
        uint expectedCp = 0;
        for (int index = 0; index < stories.Count; index++)
        {
            bool separatorStory = stories[index].SectionIndex is null;
            string literal = ((char)('A' + index)).ToString();
            Assert.AreEqual(expectedCp, stories[index].GlobalCpStart);
            Assert.AreEqual(checked(expectedCp + (separatorStory ? 1u : 2u)), stories[index].GlobalCpEnd);
            Assert.AreEqual(separatorStory ? literal : literal + "\n", stories[index].ReviewText);
            Assert.AreEqual(stories[index].GlobalCpEnd, stories[index].ExcludedGuardCp);
            Assert.AreEqual(SpecOutcome.Complete, stories[index].Outcome);
            expectedCp = checked(stories[index].ExcludedGuardCp + 1);
        }
    }

    [TestMethod]
    public void HeaderStories_RejectMissingGuardAndDoNotProjectTheGuard()
    {
        HeaderStory valid = ParseHeaderStory("OddHeader", sectionIndex: 3, "text\r\r", globalCpStart: 50);

        Assert.AreEqual("text\n", valid.ReviewText);
        Assert.AreEqual(50u, valid.GlobalCpStart);
        Assert.AreEqual(55u, valid.GlobalCpEnd);
        Assert.AreEqual(55u, valid.ExcludedGuardCp);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParseHeaderStory("OddHeader", sectionIndex: 3, "text\r", globalCpStart: 50));
    }

    [TestMethod]
    public void SecondaryAutoText_AppendsNamedRangesAfterPrimaryEvidenceWithOwnProvenance()
    {
        ProjectionResult result = ProjectAutoText(
            [new("Main", "body", new("WordDocument", 0, 4, 700, 8))],
            "firstsecond",
            ["First", "Second"],
            [0u, 5u, 11u, 999u],
            secondaryByteOffset: 1200);

        Assert.AreEqual(SpecOutcome.Complete, result.Outcome);
        Assert.HasCount(3, result.Segments);
        Assert.AreEqual("Main", result.Segments[0].Kind);
        Assert.AreEqual("AutoText:First", result.Segments[1].Kind);
        Assert.AreEqual("first", result.Segments[1].Text);
        Assert.AreEqual(new TextProvenance("SecondaryWordDocument", 0, 5, 1200, 10), result.Segments[1].Provenance);
        Assert.AreEqual("AutoText:Second", result.Segments[2].Kind);
        Assert.AreEqual("second", result.Segments[2].Text);
        Assert.AreEqual(new TextProvenance("SecondaryWordDocument", 5, 11, 1210, 12), result.Segments[2].Provenance);
    }

    [TestMethod]
    public void SecondaryAutoText_MissingNameOrRangeRetainsDecodedTextAsPartialEvidence()
    {
        ProjectionResult result = ProjectAutoText(
            [new("Main", "body", new("WordDocument", 0, 4, 700, 8))],
            "orphan",
            [],
            [0u, 7u],
            secondaryByteOffset: 1200);

        Assert.AreEqual(SpecOutcome.Partial, result.Outcome);
        Assert.HasCount(2, result.Segments);
        Assert.AreEqual("AutoText:Unanchored", result.Segments[1].Kind);
        Assert.AreEqual("orphan", result.Segments[1].Text);
    }

    [TestMethod]
    [DynamicData(nameof(ControlCases))]
    public void ContractControls_WithAndWithoutRequiredSpecialProperty_HaveExactProjectionAndProvenance(
        int codePoint,
        string expectedKind,
        bool owningStructureDeclaresMarker,
        bool hasSpecialProperty,
        string expectedProjection,
        SpecOutcome expectedOutcome)
    {
        TextProvenance source = new("WordDocument", 40, 41, 700, codePoint > char.MaxValue ? 4 : 2);

        ControlProjection result = ProjectControl(codePoint, owningStructureDeclaresMarker, hasSpecialProperty, source);

        Assert.AreEqual(expectedKind, result.Kind);
        Assert.AreEqual(expectedProjection, result.ReviewText);
        Assert.AreEqual(expectedOutcome, result.Outcome);
        Assert.AreEqual(source, result.Provenance);
    }

    public static IEnumerable<(int codePoint, string kind, bool owningStructureDeclaresMarker, bool hasSpecialProperty, string projection, SpecOutcome outcome)> ControlCases
    {
        get
        {
            foreach (ControlContract control in Controls)
            {
                yield return (control.CodePoint, control.Kind, true, true, ExpectedControlProjection(control), SpecOutcome.Complete);
                yield return (control.CodePoint, control.Kind, true, false,
                    control.RequiresSpecial ? string.Empty : ExpectedControlProjection(control),
                    control.RequiresSpecial ? SpecOutcome.Corrupt : SpecOutcome.Complete);
                bool unownedC0Special = control.RequiresSpecial && control.CodePoint < 0x20;
                yield return (control.CodePoint,
                    unownedC0Special ? "UnsupportedControl" : control.RequiresSpecial ? "Text" : control.Kind,
                    false,
                    false,
                    unownedC0Special ? string.Empty : control.RequiresSpecial
                        ? char.ConvertFromUtf32(control.CodePoint)
                        : ExpectedControlProjection(control),
                    unownedC0Special ? SpecOutcome.Partial : SpecOutcome.Complete);
            }
        }
    }

    private static readonly ControlContract[] Controls =
    [
        new(0x0001, "PictureAnchor", true, ""),
        new(0x0002, "FootnoteOrEndnoteReference", true, ""),
        new(0x0005, "CommentReference", true, ""),
        new(0x0007, "CellOrRowBoundary", false, "\t"),
        new(0x0008, "DrawingAnchor", true, ""),
        new(0x0009, "Tab", false, "\t"),
        new(0x000B, "LineBreakOrStyleSeparator", false, "\n"),
        new(0x000C, "PageOrSectionBreak", false, "\n"),
        new(0x000D, "ParagraphMark", false, "\n"),
        new(0x000E, "ColumnBreak", false, "\n"),
        new(0x0013, "FieldBegin", true, ""),
        new(0x0014, "FieldSeparator", true, ""),
        new(0x0015, "FieldEnd", true, ""),
        new(0x0028, "Symbol", true, "\u2605"),
        new(0x003C, "SdtStartMarker", true, ""),
        new(0x003E, "SdtEndMarker", true, ""),
        new(0x2002, "EnSpace", true, "\u2002"),
        new(0x2003, "EmSpace", true, "\u2003"),
    ];

    private static byte[] BuildFibLayout(
        ushort nFib,
        int rangeCount,
        int newWordCount,
        int totalLength,
        bool isComplex,
        uint cbMac)
    {
        byte[] bytes = new byte[totalLength];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xA5EC);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), nFib);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0x0409);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), isComplex ? (ushort)0x0004 : (ushort)0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 22);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), cbMac);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64 + (3 * 4)), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(152), checked((ushort)rangeCount));
        int newCountOffset = checked(154 + (rangeCount * 8));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(newCountOffset), checked((ushort)newWordCount));
        if (newWordCount != 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(newCountOffset + 2), nFib);
        }
        Assert.AreEqual(totalLength, checked(newCountOffset + 2 + (newWordCount * 2)), "Independent fixture size is internally inconsistent.");
        return bytes;
    }

    private static FibLayout ReadFibLayout(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 154 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0xA5EC)
        {
            throw new InvalidDataException();
        }

        ushort baseVersion = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(2));
        (int expectedRanges, int expectedNewWords, int expectedSize) = baseVersion switch
        {
            0x00C1 => (93, 0, 900),
            0x00D9 => (108, 2, 1024),
            0x0101 => (136, 2, 1248),
            0x010C => (164, 2, 1472),
            0x0112 => (183, 5, 1630),
            _ => throw new InvalidDataException(),
        };
        ushort csw = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(32));
        ushort cslw = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(62));
        ushort cbRgFcLcb = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(152));
        if (csw != 14 || cslw != 22 || cbRgFcLcb != expectedRanges)
        {
            throw new InvalidDataException();
        }

        int newCountOffset = checked(154 + (cbRgFcLcb * 8));
        if (newCountOffset > bytes.Length - 2)
        {
            throw new InvalidDataException();
        }
        ushort cswNew = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(newCountOffset));
        int consumed = checked(newCountOffset + 2 + (cswNew * 2));
        if (cswNew != expectedNewWords || consumed != expectedSize || consumed != bytes.Length)
        {
            throw new InvalidDataException();
        }

        ushort effective = cswNew == 0
            ? baseVersion
            : BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(newCountOffset + 2));
        uint[] parts =
        [
            ReadLw(bytes, 3), ReadLw(bytes, 4), ReadLw(bytes, 5), ReadLw(bytes, 7),
            ReadLw(bytes, 8), ReadLw(bytes, 9), ReadLw(bytes, 10),
        ];
        return new(baseVersion, effective, csw, cslw, cbRgFcLcb, cswNew, consumed,
            (BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(10)) & 0x0004) != 0,
            ReadLw(bytes, 0), parts, ReadLw(bytes, 6));
    }

    private static uint ReadLw(ReadOnlySpan<byte> bytes, int index) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(64 + (index * 4)));

    private static byte[] BuildClx(IReadOnlyList<byte[]> prcs, byte[] plcPcd)
    {
        using var stream = new MemoryStream();
        byte[] encodedPrcLength = new byte[2];
        foreach (byte[] prc in prcs)
        {
            stream.WriteByte(0x01);
            BinaryPrimitives.WriteInt16LittleEndian(encodedPrcLength, checked((short)prc.Length));
            stream.Write(encodedPrcLength);
            stream.Write(prc);
        }
        stream.WriteByte(0x02);
        Span<byte> plcLength = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(plcLength, checked((uint)plcPcd.Length));
        stream.Write(plcLength);
        stream.Write(plcPcd);
        return stream.ToArray();
    }

    private static ClxResult ParseClx(ReadOnlySpan<byte> clx)
    {
        List<byte[]> prcs = [];
        int cursor = 0;
        while (cursor < clx.Length && clx[cursor] == 0x01)
        {
            if (cursor > clx.Length - 3) throw new InvalidDataException();
            short prcLength = BinaryPrimitives.ReadInt16LittleEndian(clx.Slice(cursor + 1));
            if (prcLength < 0 || prcLength > 0x3FA2 || cursor + 3 > clx.Length - prcLength)
            {
                throw new InvalidDataException();
            }
            byte[] grpPrl = clx.Slice(cursor + 3, prcLength).ToArray();
            ValidateGrpPrl(grpPrl);
            prcs.Add(grpPrl);
            cursor = checked(cursor + 3 + prcLength);
        }
        if (cursor > clx.Length - 5 || clx[cursor] != 0x02) throw new InvalidDataException();
        uint length32 = BinaryPrimitives.ReadUInt32LittleEndian(clx.Slice(cursor + 1));
        if (length32 > int.MaxValue) throw new InvalidDataException();
        int pcdLength = (int)length32;
        cursor += 5;
        if (cursor != clx.Length - pcdLength) throw new InvalidDataException();
        return new(prcs, ParsePlcPcd(clx.Slice(cursor, pcdLength)));
    }

    private static void ValidateGrpPrl(ReadOnlySpan<byte> grpPrl)
    {
        int cursor = 0;
        while (cursor < grpPrl.Length)
        {
            if (cursor > grpPrl.Length - 2) throw new InvalidDataException();
            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(grpPrl.Slice(cursor));
            cursor += 2;
            int spra = (opcode >> 13) & 0x7;
            int operandLength = spra switch
            {
                0 or 1 => 1,
                2 or 4 or 5 => 2,
                3 => 4,
                7 => 3,
                6 => VariableOperandLength(opcode, grpPrl, cursor),
                _ => throw new InvalidDataException(),
            };
            if (cursor > grpPrl.Length - operandLength) throw new InvalidDataException();
            cursor += operandLength;
        }
    }

    private static int VariableOperandLength(ushort opcode, ReadOnlySpan<byte> grpPrl, int operandOffset)
    {
        if (opcode == 0xD608)
        {
            if (operandOffset > grpPrl.Length - 2) throw new InvalidDataException();
            ushort cb = BinaryPrimitives.ReadUInt16LittleEndian(grpPrl.Slice(operandOffset));
            return checked(cb + 1);
        }

        if (operandOffset >= grpPrl.Length) throw new InvalidDataException();
        byte cb8 = grpPrl[operandOffset];
        if (opcode != 0xC615 || cb8 != 0xFF)
        {
            return checked(1 + cb8);
        }

        if (operandOffset > grpPrl.Length - 3) throw new InvalidDataException();
        int deletedTabCount = grpPrl[operandOffset + 1];
        int addedCountOffset = checked(operandOffset + 2 + (deletedTabCount * 8));
        if (addedCountOffset >= grpPrl.Length) throw new InvalidDataException();
        int addedTabCount = grpPrl[addedCountOffset];
        return checked(3 + (deletedTabCount * 8) + (addedTabCount * 5));
    }

    private static PrmResolution ResolvePrm(ushort prm, IReadOnlyList<byte[]> precedingPrcs)
    {
        if ((prm & 1) == 0)
        {
            return new("Prm0", (prm >> 1) & 0x7F, prm >> 8, []);
        }
        int index = prm >> 1;
        if ((uint)index >= (uint)precedingPrcs.Count) throw new InvalidDataException();
        return new("Prm1", index, 0, precedingPrcs[index]);
    }

    private static int ValidatePieceBounds(Piece piece, int wordDocumentLength, uint cbMac)
    {
        int end = checked(piece.FileOffset + piece.ByteLength);
        if (piece.FileOffset < 0 || end > wordDocumentLength || (uint)end > cbMac)
        {
            throw new InvalidDataException();
        }
        return end;
    }

    private static List<HeaderStory> BuildHeaderStories(int sectionCount)
    {
        string[] prefix =
        [
            "FootnoteSeparator", "FootnoteContinuationSeparator", "FootnoteContinuationNotice",
            "EndnoteSeparator", "EndnoteContinuationSeparator", "EndnoteContinuationNotice",
        ];
        string[] perSection = ["EvenHeader", "OddHeader", "EvenFooter", "OddFooter", "FirstHeader", "FirstFooter"];
        List<HeaderStory> result = [];
        uint cp = 0;
        foreach (string kind in prefix)
        {
            result.Add(ParseHeaderStory(kind, null, $"{(char)('A' + result.Count)}\r", cp));
            cp += 2;
        }
        for (int section = 0; section < sectionCount; section++)
        {
            foreach (string kind in perSection)
            {
                result.Add(ParseHeaderStory(kind, section, $"{(char)('A' + result.Count)}\r\r", cp));
                cp += 3;
            }
        }
        return result;
    }

    private static HeaderStory ParseHeaderStory(string kind, int? sectionIndex, string rawText, uint globalCpStart)
    {
        bool separatorStory = sectionIndex is null;
        if (rawText.Length == 0 || rawText[^1] != '\r' ||
            (!separatorStory && (rawText.Length < 2 || rawText[^2] != '\r')))
        {
            throw new InvalidDataException();
        }
        uint contentLength = checked((uint)rawText.Length - 1);
        string reviewText = rawText[..^1].Replace("\r", "\n", StringComparison.Ordinal);
        return new(kind, sectionIndex, reviewText, globalCpStart,
            checked(globalCpStart + contentLength), checked(globalCpStart + contentLength), SpecOutcome.Complete);
    }

    private static ProjectionResult ProjectAutoText(
        IReadOnlyList<ProjectedSegment> primary,
        string secondaryText,
        IReadOnlyList<string> names,
        IReadOnlyList<uint> cps,
        int secondaryByteOffset)
    {
        List<ProjectedSegment> output = [.. primary];
        if (names.Count + 2 != cps.Count || cps.Count < 2 || cps[0] != 0 ||
            (names.Count == 0 && secondaryText.Length != 0))
        {
            string retained = secondaryText;
            output.Add(new("AutoText:Unanchored", retained,
                new("SecondaryWordDocument", 0, checked((uint)retained.Length), secondaryByteOffset, checked(retained.Length * 2))));
            return new(SpecOutcome.Partial, output);
        }
        for (int index = 0; index < names.Count; index++)
        {
            uint start = cps[index];
            uint end = cps[index + 1];
            if (end < start || end > secondaryText.Length) throw new InvalidDataException();
            output.Add(new($"AutoText:{names[index]}", secondaryText[(int)start..(int)end],
                new("SecondaryWordDocument", start, end, checked(secondaryByteOffset + ((int)start * 2)), checked((int)(end - start) * 2))));
        }
        return new(SpecOutcome.Complete, output);
    }

    private static ControlProjection ProjectControl(
        int codePoint,
        bool owningStructureDeclaresMarker,
        bool hasSpecialProperty,
        TextProvenance provenance)
    {
        ControlContract? contract = Controls.SingleOrDefault(candidate => candidate.CodePoint == codePoint);
        if (contract is null) throw new InvalidDataException();
        if (contract.RequiresSpecial && !owningStructureDeclaresMarker)
        {
            return codePoint < 0x20
                ? new("UnsupportedControl", string.Empty, SpecOutcome.Partial, provenance)
                : new("Text", char.ConvertFromUtf32(codePoint), SpecOutcome.Complete, provenance);
        }
        if (contract.RequiresSpecial && !hasSpecialProperty)
        {
            return new(contract.Kind, string.Empty, SpecOutcome.Corrupt, provenance);
        }
        return new(contract.Kind, ExpectedControlProjection(contract), SpecOutcome.Complete, provenance);
    }

    private static string ExpectedControlProjection(ControlContract control) => control.ReviewProjection;

    private static bool IsValidQuickSave(ushort nFib, byte baseCount, byte extendedCount) =>
        nFib == 0x00C1
            ? baseCount <= 15 && extendedCount == 0
            : nFib is 0x00D9 or 0x0101 or 0x010C or 0x0112
                && baseCount == 15
                && extendedCount <= 15;

    private static string DecodeCompressed(IEnumerable<byte> bytes) =>
        new(bytes.Select(value => CompressedOverrides.GetValueOrDefault(value, (char)value)).ToArray());

    private static string DecodeUnicodeUnits(IEnumerable<ushort> units)
    {
        StringBuilder output = new();
        ushort[] values = units.ToArray();
        for (int index = 0; index < values.Length; index++)
        {
            char current = (char)values[index];
            if (char.IsHighSurrogate(current) && index + 1 < values.Length && char.IsLowSurrogate((char)values[index + 1]))
            {
                output.Append(current).Append((char)values[++index]);
            }
            else if (char.IsSurrogate(current))
            {
                output.Append('\uFFFD');
            }
            else
            {
                output.Append(current);
            }
        }
        return output.ToString();
    }

    private static bool IsValidNoParagraphLast(bool noParagraphLast, string text) =>
        !noParagraphLast || !text.Contains('\r', StringComparison.Ordinal);

    private static byte[] BuildPlcPcd(uint[] cps, PieceDescriptor[] pieces)
    {
        byte[] bytes = new byte[(cps.Length * 4) + (pieces.Length * 8)];
        for (int index = 0; index < cps.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), cps[index]);
        }
        int pcdOffset = cps.Length * 4;
        for (int index = 0; index < pieces.Length; index++)
        {
            uint encodedFc = checked((uint)(pieces[index].FileOffset * (pieces[index].Compressed ? 2 : 1)));
            if (pieces[index].Compressed) encodedFc |= 0x40000000u;
            if (pieces[index].NoParagraphLast) bytes[pcdOffset + (index * 8)] = 0x01;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(pcdOffset + (index * 8) + 2), encodedFc);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(pcdOffset + (index * 8) + 6), pieces[index].PropertyModifier);
        }
        return bytes;
    }

    private static List<Piece> ParsePlcPcd(ReadOnlySpan<byte> plc)
    {
        if (plc.Length < 16 || (plc.Length - 4) % 12 != 0) throw new InvalidDataException();
        int pieceCount = (plc.Length - 4) / 12;
        int pcdOffset = (pieceCount + 1) * 4;
        List<Piece> pieces = [];
        uint previous = BinaryPrimitives.ReadUInt32LittleEndian(plc);
        if (previous != 0) throw new InvalidDataException();
        for (int index = 0; index < pieceCount; index++)
        {
            uint next = BinaryPrimitives.ReadUInt32LittleEndian(plc.Slice((index + 1) * 4));
            if (next <= previous || next >= 0x7FFFFFFF) throw new InvalidDataException();
            byte flags = plc[pcdOffset + (index * 8)];
            uint encodedFc = BinaryPrimitives.ReadUInt32LittleEndian(plc.Slice(pcdOffset + (index * 8) + 2));
            if ((encodedFc & 0x80000000u) != 0) throw new InvalidDataException();
            bool compressed = (encodedFc & 0x40000000u) != 0;
            int fileOffset = checked((int)((encodedFc & 0x3FFFFFFFu) / (compressed ? 2u : 1u)));
            int byteLength = checked((int)(next - previous) * (compressed ? 1 : 2));
            ushort prm = BinaryPrimitives.ReadUInt16LittleEndian(plc.Slice(pcdOffset + (index * 8) + 6));
            pieces.Add(new(previous, next, fileOffset, byteLength, compressed, (flags & 1) != 0, prm));
            previous = next;
        }
        return pieces;
    }

    private static StoryLayout BuildStoryLayout(uint[] counts, uint reserved3)
    {
        if (counts.Length != 7 || counts[0] == 0 || reserved3 != 0) throw new InvalidDataException();
        string[] kinds = ["Main", "Footnote", "Header", "Comment", "Endnote", "MainTextbox", "HeaderTextbox"];
        List<StoryRange> ranges = [];
        uint cp = 0;
        for (int index = 0; index < counts.Length; index++)
        {
            uint end = checked(cp + counts[index]);
            ranges.Add(new(kinds[index], cp, end));
            cp = end;
        }
        bool hasSpecialized = counts.Skip(1).Any(static count => count != 0);
        return new(ranges, hasSpecialized ? cp : null, checked(cp + (hasSpecialized ? 1u : 0u)));
    }

    private readonly record struct PieceDescriptor(int FileOffset, bool Compressed, bool NoParagraphLast, ushort PropertyModifier = 0);
    private readonly record struct Piece(uint StartCp, uint EndCp, int FileOffset, int ByteLength, bool Compressed, bool NoParagraphLast, ushort PropertyModifier);
    private readonly record struct StoryRange(string Kind, uint StartCp, uint EndCp);
    private sealed record StoryLayout(IReadOnlyList<StoryRange> Ranges, uint? OutsideGuardCp, uint FinalCp);
    private sealed record FibLayout(
        ushort BaseVersion,
        ushort EffectiveVersion,
        int ShortWordCount,
        int LongWordCount,
        int RangeCount,
        int NewWordCount,
        int ConsumedBytes,
        bool IsComplex,
        uint CbMac,
        IReadOnlyList<uint> PartCounts,
        uint ReservedPartCount);
    private sealed record ClxResult(IReadOnlyList<byte[]> PropertyRecords, IReadOnlyList<Piece> Pieces);
    private sealed record PrmResolution(string Kind, int IndexOrIsprm, int Value, byte[] PropertyRecord);
    public enum SpecOutcome { Complete, Partial, Corrupt }
    private sealed record HeaderStory(
        string Kind,
        int? SectionIndex,
        string ReviewText,
        uint GlobalCpStart,
        uint GlobalCpEnd,
        uint ExcludedGuardCp,
        SpecOutcome Outcome);
    private sealed record TextProvenance(
        string Stream,
        uint GlobalCpStart,
        uint GlobalCpEnd,
        int ByteOffset,
        int ByteLength);
    private sealed record ProjectedSegment(string Kind, string Text, TextProvenance Provenance);
    private sealed record ProjectionResult(SpecOutcome Outcome, IReadOnlyList<ProjectedSegment> Segments);
    private sealed record ControlContract(int CodePoint, string Kind, bool RequiresSpecial, string ReviewProjection);
    private sealed record ControlProjection(string Kind, string ReviewText, SpecOutcome Outcome, TextProvenance Provenance);
}
