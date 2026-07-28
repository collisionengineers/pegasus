using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CollisionDocNet.Writer.Tests;

// Independent executable oracle for the MS-DOC 12.5 property-engine contract.
// It intentionally does not call production property parsers, tables or constants.
[TestClass]
public sealed class DocR04ExecutableSpecificationTests
{
    [TestMethod]
    [DynamicData(nameof(SpraCases))]
    public void SprmFraming_AllEightSpraValuesPreserveExactOperand(
        ushort opcode, byte[] encodedOperand, byte[] expectedOperand)
    {
        byte[] record = [.. LittleEndian(opcode), .. encodedOperand];

        IReadOnlyList<OracleSprm> parsed = ParseSprms(record, maximumRecords: 1, maximumBytes: record.Length);

        OracleSprm sprm = Assert.ContainsSingle(parsed);
        Assert.AreEqual(opcode, sprm.Opcode);
        CollectionAssert.AreEqual(expectedOperand, sprm.Operand);
        Assert.AreEqual(0, sprm.RecordOffset);
        Assert.AreEqual(record.Length, sprm.RecordLength);
    }

    public static IEnumerable<(ushort opcode, byte[] encodedOperand, byte[] expectedOperand)> SpraCases =>
    [
        (Opcode(0, 1), [0x11], [0x11]),
        (Opcode(1, 2), [0x12], [0x12]),
        (Opcode(2, 3), [0x21, 0x22], [0x21, 0x22]),
        (Opcode(3, 4), [0x31, 0x32, 0x33, 0x34], [0x31, 0x32, 0x33, 0x34]),
        (Opcode(4, 5), [0x41, 0x42], [0x41, 0x42]),
        (Opcode(5, 6), [0x51, 0x52], [0x51, 0x52]),
        (Opcode(6, 7), [0x02, 0x61, 0x62], [0x02, 0x61, 0x62]),
        (Opcode(7, 8), [0x71, 0x72, 0x73], [0x71, 0x72, 0x73]),
    ];

    [TestMethod]
    public void Catalogue_HasExactGeneratedCardinalityOrdinalsGroupsAndSpraTotals()
    {
        CatalogueRow[] rows = LoadCatalogue();

        Assert.HasCount(322, rows);
        CollectionAssert.AreEqual(Enumerable.Range(0, 322).ToArray(), rows.Select(static row => row.Ordinal).ToArray());
        Assert.AreEqual("Character=84|Paragraph=91|Picture=8|Section=59|Table=80",
            string.Join('|', rows.GroupBy(static row => row.Group).OrderBy(static group => group.Key).Select(static group => $"{group.Key}={group.Count()}")));
        Assert.AreEqual("0=25|1=80|2=59|3=41|4=26|5=9|6=75|7=7",
            string.Join('|', rows.GroupBy(static row => row.Spra).OrderBy(static group => group.Key).Select(static group => $"{group.Key}={group.Count()}")));
        Assert.AreEqual(322, rows.Select(static row => row.Opcode).Distinct().Count());
        Assert.AreEqual(322, rows.Select(static row => row.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    [DynamicData(nameof(CatalogueRows))]
    public void Catalogue_EveryRowOwnsItsLiteralBitsFramingAndPropertyArrays(CatalogueRow row)
    {
        Assert.AreEqual(row.Ispmd, row.Opcode & 0x01FF, row.Name);
        Assert.AreEqual(row.FSpec, (row.Opcode >> 9) & 1, row.Name);
        Assert.AreEqual(GroupCode(row.Group), (row.Opcode >> 10) & 7, row.Name);
        Assert.AreEqual(row.Spra, row.Opcode >> 13, row.Name);
        Assert.AreEqual(ExpectedFraming(row.Opcode, row.Spra), row.OperandFraming, row.Name);
        string[] ownerUniverse = ExpectedOwners(row.Group);
        Assert.IsNotEmpty(row.ValidPropertyArrays, row.Name);
        Assert.AreEqual(ownerUniverse[0], row.ValidPropertyArrays[0], row.Name);
        Assert.IsTrue(row.ValidPropertyArrays.All(ownerUniverse.Contains), row.Name);

        byte[] encodedOperand = FramedOperand(row.OperandFraming);
        OracleSprm parsed = Assert.ContainsSingle(ParseSprms(
            [.. LittleEndian(row.Opcode), .. encodedOperand], 1, encodedOperand.Length + 2));
        CollectionAssert.AreEqual(encodedOperand, parsed.Operand, row.Name);
    }

    public static IEnumerable<CatalogueRow> CatalogueRows => LoadCatalogue();

    [TestMethod]
    public void SprmFraming_TDefTableAndPChgTabsUseTheirExactSpra6Exceptions()
    {
        byte[] tDefTable = [0x08, 0xD6, 0x05, 0x00, 0x01, 0x10, 0x00, 0x20];
        byte[] pChgTabs =
        [
            0x15, 0xC6, 0xFF,
            0x01, 1, 2, 3, 4, 5, 6, 7, 8,
            0x01, 9, 10, 11, 12, 13,
        ];

        IReadOnlyList<OracleSprm> parsed = ParseSprms(
            [.. tDefTable, .. pChgTabs], maximumRecords: 2, maximumBytes: tDefTable.Length + pChgTabs.Length);

        Assert.HasCount(2, parsed);
        CollectionAssert.AreEqual(tDefTable.AsSpan(2).ToArray(), parsed[0].Operand);
        CollectionAssert.AreEqual(pChgTabs.AsSpan(2).ToArray(), parsed[1].Operand);
        Assert.AreEqual(tDefTable.Length, parsed[1].RecordOffset);
    }

    [TestMethod]
    public void SprmFraming_TruncationAndNonExceptionalSentinelAreCorrupt()
    {
        foreach ((ushort opcode, byte[] encoded, _) in SpraCases)
        {
            byte[] complete = [.. LittleEndian(opcode), .. encoded];
            Assert.ThrowsExactly<InvalidDataException>(() =>
                ParseSprms(complete.AsSpan(0, complete.Length - 1), maximumRecords: 1, maximumBytes: complete.Length));
        }

        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParseSprms([0x08, 0xD6, 0x05, 0x00, 1, 2, 3], maximumRecords: 1, maximumBytes: 16));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParseSprms([0x15, 0xC6, 0xFF, 0x01, 1, 2], maximumRecords: 1, maximumBytes: 32));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ParseSprms([.. LittleEndian(Opcode(6, 9)), 0xFF, 0x00], maximumRecords: 1, maximumBytes: 300));
    }

    [TestMethod]
    public void PrmResolution_FiltersByGroupAndPreservesSourceOrder()
    {
        Prm0 simple = new(Group.Paragraph, "align", "center");
        PropertyRecord[] records =
        [
            new([new(Group.Character, "bold", "on"), new(Group.Paragraph, "spacing", "12")]),
            new([new(Group.Table, "cellMargin", "20"), new(Group.Paragraph, "keep", "on")]),
        ];

        IReadOnlyList<PropertyMutation> prm0 = ResolvePrm(simple, records, Group.Paragraph);
        IReadOnlyList<PropertyMutation> prm1 = ResolvePrm(new Prm1(1), records, Group.Paragraph);

        Assert.AreEqual("align=center", Assert.ContainsSingle(prm0).Display);
        Assert.AreEqual("keep=on", string.Join(',', prm1.Select(static value => value.Display)));
        Assert.ThrowsExactly<InvalidDataException>(() => ResolvePrm(new Prm1(2), records, Group.Paragraph));
        Assert.IsEmpty(ResolvePrm(new Prm1(0), records, Group.Section));
    }

    [TestMethod]
    public void PlcAndBte_UseExactFourPlusNMultiplesAndStrictFcOrder()
    {
        byte[] bte = BuildPlc([100u, 200u, 300u], [[2, 0, 0, 0], [4, 0, 0, 0]]);

        Plc parsed = ParsePlc(bte, dataSize: 4, maximumRecords: 2);

        CollectionAssert.AreEqual(new uint[] { 100, 200, 300 }, parsed.Positions);
        Assert.HasCount(2, parsed.Records);
        Assert.ThrowsExactly<InvalidDataException>(() => ParsePlc(bte.AsSpan(0, bte.Length - 1), 4, 2));
        Assert.ThrowsExactly<InvalidDataException>(() => ParsePlc(BuildPlc([100u, 100u], [[2, 0, 0, 0]]), 4, 1));
        Assert.ThrowsExactly<ResourceLimitException>(() => ParsePlc(bte, 4, 1));
    }

    [TestMethod]
    public void ChpxFkp_UsesByteCbAndRejectsHeapOverlap()
    {
        byte[] page = BuildFkp(paragraph: false, propertyOffsets: [200, 220], properties: [[1, 2, 3], [4, 5]]);

        IReadOnlyList<FkpProperty> properties = ParseFkp(page, paragraph: false);

        Assert.HasCount(2, properties);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, properties[0].GrpPrl);
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, properties[1].GrpPrl);
        byte[] overlap = BuildFkp(paragraph: false, propertyOffsets: [200, 202], properties: [[1, 2, 3], [4, 5]]);
        Assert.ThrowsExactly<InvalidDataException>(() => ParseFkp(overlap, paragraph: false));
    }

    [TestMethod]
    public void PapxFkp_UsesCbAndCbPrimeAndRejectsDescriptorHeapOverlap()
    {
        byte[] page = BuildFkp(paragraph: true, propertyOffsets: [200, 230],
            properties: [[0x07, 0x00, 0x35, 0x08, 0x01], [0x08, 0x00, 0x36, 0x08, 0x01, 0x00]], useCbPrimeForSecond: true);

        IReadOnlyList<FkpProperty> parsed = ParseFkp(page, paragraph: true);

        Assert.HasCount(2, parsed);
        Assert.AreEqual((ushort)7, parsed[0].StyleIndex);
        Assert.AreEqual((ushort)8, parsed[1].StyleIndex);
        CollectionAssert.AreEqual(new byte[] { 0x35, 0x08, 0x01 }, parsed[0].GrpPrl);
        CollectionAssert.AreEqual(new byte[] { 0x36, 0x08, 0x01, 0x00 }, parsed[1].GrpPrl);
        byte[] descriptorOverlap = BuildFkp(paragraph: true, propertyOffsets: [20], properties: [[1, 0]]);
        Assert.ThrowsExactly<InvalidDataException>(() => ParseFkp(descriptorOverlap, paragraph: true));
    }

    [TestMethod]
    public void Fkp_EnforcesExactRunCountsFcOrderZeroDefaultsAndBtePageEndpoints()
    {
        byte[] defaults = BuildFkp(paragraph: false, propertyOffsets: [0, 220], properties: [[], [4, 5]]);
        List<FkpProperty> runs = ParseFkp(defaults, paragraph: false);
        Assert.IsTrue(runs[0].IsDefault);
        Assert.AreEqual(100u, runs[0].FcStart);
        Assert.AreEqual(110u, runs[0].FcEnd);
        Assert.IsFalse(runs[1].IsDefault);

        defaults.AsSpan(8, 4).CopyTo(defaults.AsSpan(4, 4));
        Assert.ThrowsExactly<InvalidDataException>(() => ParseFkp(defaults, paragraph: false));

        byte[] badChpxCount = new byte[512];
        badChpxCount[511] = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => ParseFkp(badChpxCount, paragraph: false));
        badChpxCount[511] = 0x66;
        Assert.ThrowsExactly<InvalidDataException>(() => ParseFkp(badChpxCount, paragraph: false));
        byte[] badPapxCount = new byte[512];
        badPapxCount[511] = 0x1E;
        Assert.ThrowsExactly<InvalidDataException>(() => ParseFkp(badPapxCount, paragraph: true));

        byte[] page = BuildFkp(paragraph: false, propertyOffsets: [220], properties: [[1]]);
        byte[] wordDocument = new byte[1536];
        page.CopyTo(wordDocument, 1024);
        byte[] bte = BuildPlc([100u, 110u], [[2, 0, 0, 0]]);
        List<FkpProperty> selected = ResolveBtePage(wordDocument, bte, paragraph: false);
        Assert.AreEqual(100u, Assert.ContainsSingle(selected).FcStart);
        BinaryPrimitives.WriteUInt32LittleEndian(wordDocument.AsSpan(1024), 99);
        Assert.ThrowsExactly<InvalidDataException>(() => ResolveBtePage(wordDocument, bte, paragraph: false));
        BinaryPrimitives.WriteUInt32LittleEndian(bte.AsSpan(8), 3);
        Assert.ThrowsExactly<InvalidDataException>(() => ResolveBtePage(wordDocument, bte, paragraph: false));
    }

    [TestMethod]
    public void Sepx_UsesSectionPlcAndLengthPrefixedWordDocumentGrpPrl()
    {
        byte[] sed = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(sed.AsSpan(2), 40);
        Plc sections = ParsePlc(BuildPlc([0u, 10u], [sed]), dataSize: 12, maximumRecords: 1);
        byte[] wordDocument = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(wordDocument.AsSpan(40), 3);
        new byte[] { 0x35, 0x08, 0x01 }.CopyTo(wordDocument, 42);

        byte[] grpPrl = ReadSepx(wordDocument, sections.Records[0]);

        CollectionAssert.AreEqual(new byte[] { 0x35, 0x08, 0x01 }, grpPrl);
        BinaryPrimitives.WriteUInt16LittleEndian(wordDocument.AsSpan(40), 30);
        Assert.ThrowsExactly<InvalidDataException>(() => ReadSepx(wordDocument, sections.Records[0]));
    }

    [TestMethod]
    public void Sepx_UsesSignedCbDefaultSentinelWholePrlsAndExactMainStorySectionRanges()
    {
        byte[] firstSed = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(firstSed.AsSpan(2), 40);
        byte[] defaultSed = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(defaultSed.AsSpan(2), -1);
        byte[] plcfSed = BuildPlc([0u, 4u, 8u], [firstSed, defaultSed]);
        byte[] mainText = [0x41, 0x42, 0x43, 0x0C, 0x44, 0x45, 0x46, 0x47];
        byte[] wordDocument = new byte[64];
        BinaryPrimitives.WriteInt16LittleEndian(wordDocument.AsSpan(40), 3);
        new byte[] { 0x35, 0x08, 0x01 }.CopyTo(wordDocument, 42);

        List<byte[]> sections = ReadSections(wordDocument, plcfSed, mainText, ccpText: 8);

        Assert.HasCount(2, sections);
        CollectionAssert.AreEqual(new byte[] { 0x35, 0x08, 0x01 }, sections[0]);
        Assert.IsEmpty(sections[1]);
        wordDocument[43] = 0x48;
        Assert.ThrowsExactly<InvalidDataException>(() => ReadSections(wordDocument, plcfSed, mainText, 8));
        wordDocument[43] = 0x08;
        BinaryPrimitives.WriteInt16LittleEndian(wordDocument.AsSpan(40), -2);
        Assert.ThrowsExactly<InvalidDataException>(() => ReadSections(wordDocument, plcfSed, mainText, 8));
        BinaryPrimitives.WriteInt16LittleEndian(wordDocument.AsSpan(40), 3);
        mainText[3] = 0x0B;
        Assert.ThrowsExactly<InvalidDataException>(() => ReadSections(wordDocument, plcfSed, mainText, 8));
        Assert.ThrowsExactly<InvalidDataException>(() => ReadSections(wordDocument, BuildPlc([0u, 4u, 7u], [firstSed, defaultSed]), mainText, 8));
    }

    [TestMethod]
    public void Cascade_AppliesLiteralLayerOrderAndProducesStableSnapshots()
    {
        PropertyLayer[] paragraphLayers =
        [
            Layer("defaults", ("font", "Default"), ("bold", "off")),
            Layer("base-style", ("font", "Base")),
            Layer("current-paragraph-style", ("align", "left")),
            Layer("table-part1-horizontal-band", ("color", "band")),
            Layer("table-part1-vertical-band", ("color", "vertical")),
            Layer("table-part1-first-column", ("color", "first-column")),
            Layer("table-part1-last-column", ("color", "last-column")),
            Layer("table-part1-first-row", ("color", "first-row")),
            Layer("table-part1-last-row", ("color", "last-row")),
            Layer("table-part1-corner", ("color", "corner")),
            Layer("papx", ("align", "right")),
            Layer("paragraph-piece-prm", ("align", "center")),
            Layer("list-derived", ("indent", "720")),
        ];

        CascadeResult first = ApplyCascade(paragraphLayers);
        CascadeResult retry = ApplyCascade(paragraphLayers);

        Assert.AreEqual(
            "defaults:bold=off,font=Default|base-style:bold=off,font=Base|current-paragraph-style:align=left,bold=off,font=Base|table-part1-horizontal-band:align=left,bold=off,color=band,font=Base|table-part1-vertical-band:align=left,bold=off,color=vertical,font=Base|table-part1-first-column:align=left,bold=off,color=first-column,font=Base|table-part1-last-column:align=left,bold=off,color=last-column,font=Base|table-part1-first-row:align=left,bold=off,color=first-row,font=Base|table-part1-last-row:align=left,bold=off,color=last-row,font=Base|table-part1-corner:align=left,bold=off,color=corner,font=Base|papx:align=right,bold=off,color=corner,font=Base|paragraph-piece-prm:align=center,bold=off,color=corner,font=Base|list-derived:align=center,bold=off,color=corner,font=Base,indent=720",
            string.Join('|', first.Snapshots));
        Assert.AreEqual(first.Canonical, retry.Canonical);

        CascadeResult character = ApplyCascade(
        [
            Layer("paragraph-character-defaults", ("font", "paragraph")),
            Layer("base-character-style", ("font", "base")),
            Layer("current-character-style", ("font", "current")),
            Layer("sprmCIstd-transition", ("font", "transition")),
            Layer("chpx", ("font", "direct")),
            Layer("character-piece-prm", ("font", "piece")),
        ]);
        Assert.AreEqual("font=piece", character.Canonical);

        CascadeResult section = ApplyCascade(
            [Layer("section-defaults", ("columns", "1")), Layer("sepx", ("columns", "2"))]);
        Assert.AreEqual("columns=2", section.Canonical);
    }

    [TestMethod]
    public void Styles_ResolveBaseBeforeCurrentAndRejectCycles()
    {
        Dictionary<string, StyleNode> styles = new(StringComparer.Ordinal)
        {
            ["Base"] = new(null, Layer("Base", ("font", "Base"))),
            ["Current"] = new("Base", Layer("Current", ("bold", "on"))),
        };

        IReadOnlyList<PropertyLayer> resolved = ResolveStyle("Current", styles, maximumDepth: 2);

        Assert.AreEqual("Base|Current", string.Join('|', resolved.Select(static layer => layer.Name)));
        styles["Base"] = new("Current", styles["Base"].Properties);
        Assert.ThrowsExactly<InvalidDataException>(() => ResolveStyle("Current", styles, maximumDepth: 4));
        Assert.ThrowsExactly<ResourceLimitException>(() => ResolveStyle("Current", new Dictionary<string, StyleNode>
        {
            ["Base"] = new(null, Layer("Base", ("font", "Base"))),
            ["Current"] = new("Base", Layer("Current", ("bold", "on"))),
        }, maximumDepth: 1));
    }

    [TestMethod]
    public void Styles_ValidateIstdBaseNextLinkCupxTypedUpxPaddingExclusionsAndBounds()
    {
        StyleDefinition[] styles =
        [
            new(StyleKind.Paragraph, 0x0FFF, 0, 0, false, 2,
                [new(UpxKind.Papx, 4, 0, false), new(UpxKind.Chpx, 3, 0, false)]),
            new(StyleKind.Character, 0, 0, 0, false, 1,
                [new(UpxKind.Chpx, 2, 0, false)]),
            new(StyleKind.Table, 0x0FFF, 0, 0, false, 3,
                [new(UpxKind.Tapx, 2, 0, false), new(UpxKind.Papx, 2, 0, false), new(UpxKind.Chpx, 2, 0, false)]),
        ];

        Assert.AreEqual("0>1", ValidateStyle(1, styles, maximumDepth: 2, maximumUpxBytes: 10));
        Assert.AreEqual("2", ValidateStyle(2, styles, maximumDepth: 1, maximumUpxBytes: 6));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(0x0FFE, styles, 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(0, [styles[0] with { Next = 4 }, styles[1], styles[2]], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(0, [styles[0] with { Next = 2 }, styles[1], styles[2] with { IsEmpty = true }], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(1, [styles[0], styles[1] with { Link = 4 }, styles[2]], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(1, [styles[0] with { Base = 1 }, styles[1], styles[2]], 3, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(0, [styles[0] with { Cupx = 1 }, styles[1], styles[2]], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(2, [styles[0], styles[1], styles[2] with { RevisionMarked = true }], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(0, [styles[0] with { Upx = [styles[0].Upx[1], styles[0].Upx[0]] }, styles[1], styles[2]], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(0, [styles[0] with { Upx = [styles[0].Upx[0] with { Padding = 1 }, styles[0].Upx[1]] }, styles[1], styles[2]], 2, 20));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateStyle(2, [styles[0], styles[1], styles[2] with { Upx = [styles[2].Upx[0] with { ContainsExcludedSprm = true }, styles[2].Upx[1], styles[2].Upx[2]] }], 2, 20));
        Assert.ThrowsExactly<ResourceLimitException>(() => ValidateStyle(1, styles, maximumDepth: 1, maximumUpxBytes: 20));
        Assert.ThrowsExactly<ResourceLimitException>(() => ValidateStyle(1, styles, maximumDepth: 2, maximumUpxBytes: 9));

        StyleDefinition revisedParagraph = styles[0] with
        {
            RevisionMarked = true,
            Cupx = 3,
            Upx = [styles[0].Upx[0], styles[0].Upx[1], new(UpxKind.ParagraphRevision, 2, 0, false)],
        };
        Assert.AreEqual("0", ValidateStyle(0, [revisedParagraph, styles[1], styles[2]], 1, 10));
    }

    [TestMethod]
    public void DataIndirection_ResolvesHugePapxAndTablePropsWithCycleDepthAndByteBounds()
    {
        Dictionary<int, DataRecord> records = new()
        {
            [10] = new("HugePapx", [1, 2], 20),
            [20] = new("TableProps", [3, 4, 5], null),
        };

        DataResolution complete = ResolveData(10, records, maximumDepth: 2, maximumBytes: 5);

        Assert.AreEqual(OracleOutcome.Complete, complete.Outcome);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, complete.Bytes);
        Assert.AreEqual("HugePapx@10>TableProps@20", complete.Path);
        records[20] = records[20] with { NextOffset = 10 };
        Assert.AreEqual(OracleOutcome.Corrupt, ResolveData(10, records, 4, 20).Outcome);
        records[20] = records[20] with { NextOffset = null };
        Assert.AreEqual(OracleOutcome.ResourceLimitExceeded, ResolveData(10, records, 1, 20).Outcome);
        Assert.AreEqual(OracleOutcome.ResourceLimitExceeded, ResolveData(10, records, 2, 4).Outcome);
        DataResolution retry = ResolveData(10, records, 2, 5);
        Assert.AreEqual(complete.Outcome, retry.Outcome);
        Assert.AreEqual(complete.Path, retry.Path);
        CollectionAssert.AreEqual(complete.Bytes, retry.Bytes);
    }

    [TestMethod]
    public void PrcData_UsesSignedWholePrlBodiesAndExactHugePapxTablePropsChains()
    {
        byte[] data = new byte[96];
        WritePrcData(data, 10, [0x46, 0x66, 30, 0, 0, 0, 0x35, 0x08, 1, 0x35, 0x08, 1]);
        WritePrcData(data, 30, [0x6B, 0x64, 50, 0, 0, 0, 0x35, 0x08, 1, 0x35, 0x08, 1]);
        WritePrcData(data, 50, [0x35, 0x08, 1, 0x35, 0x08, 1, 0x35, 0x08, 1, 0x35, 0x08, 1]);

        DataResolution complete = ResolvePrcData(data, 10, PrcContext.Papx, maximumDepth: 3, maximumBytes: 36);

        Assert.AreEqual(OracleOutcome.Complete, complete.Outcome);
        Assert.AreEqual("HugePapx@10>TableProps@30>Terminal@50", complete.Path);
        Assert.HasCount(36, complete.Bytes);
        Assert.AreEqual(OracleOutcome.ResourceLimitExceeded, ResolvePrcData(data, 10, PrcContext.Papx, 2, 36).Outcome);
        Assert.AreEqual(OracleOutcome.ResourceLimitExceeded, ResolvePrcData(data, 10, PrcContext.Papx, 3, 35).Outcome);
        WritePrcData(data, 30, [0x6B, 0x64, 10, 0, 0, 0, 0x35, 0x08, 1, 0x35, 0x08, 1]);
        Assert.AreEqual(OracleOutcome.Corrupt, ResolvePrcData(data, 10, PrcContext.Papx, 4, 48).Outcome);

        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), -1);
        Assert.AreEqual(OracleOutcome.Corrupt, ResolvePrcData(data, 10, PrcContext.Papx, 3, 36).Outcome);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), 0x3FA3);
        Assert.AreEqual(OracleOutcome.Corrupt, ResolvePrcData(data, 10, PrcContext.Papx, 3, 36).Outcome);
    }

    [TestMethod]
    public void DataReferences_EnforceHugePapxPlacementGrpPrlAndIstdAndPicLocationContext()
    {
        byte[] data = new byte[80];
        WritePrcData(data, 20, [0x35, 0x08, 1, 0x46, 0x66, 50, 0, 0, 0, 0x35, 0x08, 1]);
        WritePrcData(data, 50, [0x35, 0x08, 1, 0x35, 0x08, 1, 0x35, 0x08, 1, 0x35, 0x08, 1]);
        Assert.AreEqual("Terminal@20", ResolvePrcData(data, 20, PrcContext.Papx, 2, 24).Path);

        WritePrcData(data, 20, [0x46, 0x66, 50, 0, 0, 0, 0x35, 0x08, 1, 0x35, 0x08, 1]);
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateHugePapxContainer([0x46, 0x66, 50, 0, 0, 0], inGrpPrlAndIstd: true, istd: 1));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateHugePapxContainer([0x46, 0x66, 50, 0, 0, 0, 0x35, 0x08, 1], inGrpPrlAndIstd: true, istd: 0));
        ValidateHugePapxContainer([0x46, 0x66, 50, 0, 0, 0], inGrpPrlAndIstd: true, istd: 0);

        Assert.AreEqual(60u, ValidatePicLocation([0x03, 0x6A, 60, 0, 0, 0], Group.Character, data.Length, 1));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidatePicLocation([0x03, 0x6A, 60, 0, 0, 0], Group.Paragraph, data.Length, 1));
        Assert.ThrowsExactly<ResourceLimitException>(() => ValidatePicLocation([0x03, 0x6A, 60, 0, 0, 0], Group.Character, data.Length, 0));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidatePicLocation([0x03, 0x6A, 90, 0, 0, 0], Group.Character, data.Length, 1));
    }

    private static List<OracleSprm> ParseSprms(ReadOnlySpan<byte> bytes, int maximumRecords, int maximumBytes)
    {
        if (bytes.Length > maximumBytes) throw new ResourceLimitException();
        List<OracleSprm> result = [];
        int cursor = 0;
        while (cursor < bytes.Length)
        {
            if (result.Count >= maximumRecords) throw new ResourceLimitException();
            if (cursor > bytes.Length - 2) throw new InvalidDataException();
            int start = cursor;
            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(bytes[cursor..]);
            cursor += 2;
            int operandLength = OperandLength(opcode, bytes, cursor);
            if (cursor > bytes.Length - operandLength) throw new InvalidDataException();
            byte[] operand = bytes.Slice(cursor, operandLength).ToArray();
            cursor += operandLength;
            result.Add(new(opcode, operand, start, cursor - start));
        }
        return result;
    }

    private static int OperandLength(ushort opcode, ReadOnlySpan<byte> bytes, int offset)
    {
        int spra = opcode >> 13;
        if (spra != 6) return spra switch { 0 or 1 => 1, 2 or 4 or 5 => 2, 3 => 4, 7 => 3, _ => throw new InvalidDataException() };
        if (opcode == 0xD608)
        {
            if (offset > bytes.Length - 2) throw new InvalidDataException();
            return checked(BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]) + 1);
        }
        if (offset >= bytes.Length) throw new InvalidDataException();
        byte cb = bytes[offset];
        if (opcode != 0xC615 || cb != 0xFF) return checked(1 + cb);
        if (offset > bytes.Length - 3) throw new InvalidDataException();
        int deleted = bytes[offset + 1];
        int addedOffset = checked(offset + 2 + (deleted * 8));
        if (addedOffset >= bytes.Length) throw new InvalidDataException();
        int added = bytes[addedOffset];
        return checked(3 + (deleted * 8) + (added * 5));
    }

    private static PropertyMutation[] ResolvePrm(object prm, IReadOnlyList<PropertyRecord> records, Group target) => prm switch
    {
        Prm0 simple => simple.Group == target ? [new(simple.Name, simple.Value)] : [],
        Prm1 complex when (uint)complex.Index < (uint)records.Count => records[complex.Index].Mutations.Where(value => value.Group == target).Select(value => new PropertyMutation(value.Name, value.Value)).ToArray(),
        Prm1 => throw new InvalidDataException(),
        _ => throw new InvalidDataException(),
    };

    private static Plc ParsePlc(ReadOnlySpan<byte> bytes, int dataSize, int maximumRecords)
    {
        if (bytes.Length < 4 || (bytes.Length - 4) % (4 + dataSize) != 0) throw new InvalidDataException();
        int count = (bytes.Length - 4) / (4 + dataSize);
        if (count > maximumRecords) throw new ResourceLimitException();
        uint[] positions = new uint[count + 1];
        for (int index = 0; index <= count; index++)
        {
            positions[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(index * 4)..]);
            if (index != 0 && positions[index] <= positions[index - 1]) throw new InvalidDataException();
        }
        int dataOffset = (count + 1) * 4;
        byte[][] records = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            records[index] = bytes.Slice(dataOffset + (index * dataSize), dataSize).ToArray();
        }
        return new(positions, records);
    }

    private static byte[] BuildPlc(uint[] positions, byte[][] records)
    {
        if (positions.Length != records.Length + 1) throw new ArgumentException("A PLC requires n+1 positions.", nameof(positions));
        int dataSize = records.Length == 0 ? 0 : records[0].Length;
        byte[] bytes = new byte[(positions.Length * 4) + (records.Length * dataSize)];
        for (int index = 0; index < positions.Length; index++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), positions[index]);
        int dataOffset = positions.Length * 4;
        for (int index = 0; index < records.Length; index++) records[index].CopyTo(bytes, dataOffset + (index * dataSize));
        return bytes;
    }

    private static byte[] BuildFkp(bool paragraph, int[] propertyOffsets, byte[][] properties, bool useCbPrimeForSecond = false)
    {
        byte[] page = new byte[512];
        int count = propertyOffsets.Length;
        page[511] = checked((byte)count);
        for (int index = 0; index <= count; index++) BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(index * 4), checked((uint)(100 + (index * 10))));
        int descriptorSize = paragraph ? 13 : 1;
        int descriptors = (count + 1) * 4;
        for (int index = 0; index < count; index++)
        {
            page[descriptors + (index * descriptorSize)] = checked((byte)(propertyOffsets[index] / 2));
            byte[] payload = properties[index];
            int offset = propertyOffsets[index];
            if (offset == 0) continue;
            if (!paragraph)
            {
                page[offset] = checked((byte)payload.Length);
                payload.CopyTo(page, offset + 1);
            }
            else if (useCbPrimeForSecond && index == 1)
            {
                page[offset] = 0;
                page[offset + 1] = checked((byte)((payload.Length + 1) / 2));
                payload.CopyTo(page, offset + 2);
            }
            else
            {
                page[offset] = checked((byte)((payload.Length + 1) / 2));
                payload.CopyTo(page, offset + 1);
            }
        }
        return page;
    }

    private static List<FkpProperty> ParseFkp(ReadOnlySpan<byte> page, bool paragraph)
    {
        if (page.Length != 512) throw new InvalidDataException();
        int count = page[511];
        if (count < 1 || count > (paragraph ? 0x1D : 0x65)) throw new InvalidDataException();
        int descriptorSize = paragraph ? 13 : 1;
        int heapFloor = checked(((count + 1) * 4) + (count * descriptorSize));
        uint[] fcs = new uint[count + 1];
        for (int index = 0; index <= count; index++)
        {
            fcs[index] = BinaryPrimitives.ReadUInt32LittleEndian(page[(index * 4)..]);
            if (index != 0 && fcs[index] <= fcs[index - 1]) throw new InvalidDataException();
        }
        List<(int Start, int End)> heaps = [];
        List<FkpProperty> output = [];
        for (int index = 0; index < count; index++)
        {
            int start = page[((count + 1) * 4) + (index * descriptorSize)] * 2;
            if (start == 0)
            {
                output.Add(new(fcs[index], fcs[index + 1], true, null, []));
                continue;
            }
            if (start < heapFloor || start >= 511) throw new InvalidDataException();
            int payloadOffset = start + 1;
            int payloadLength;
            if (!paragraph) payloadLength = page[start];
            else if (page[start] != 0) payloadLength = checked((page[start] * 2) - 1);
            else
            {
                if (payloadOffset >= 511 || page[payloadOffset] == 0) throw new InvalidDataException();
                payloadLength = checked(page[payloadOffset] * 2);
                payloadOffset++;
            }
            int end = checked(payloadOffset + payloadLength);
            if (end > 511 || heaps.Any(range => start < range.End && end > range.Start)) throw new InvalidDataException();
            heaps.Add((start, end));
            byte[] payload = page.Slice(payloadOffset, payloadLength).ToArray();
            ushort? style = paragraph ? BinaryPrimitives.ReadUInt16LittleEndian(payload) : null;
            output.Add(new(fcs[index], fcs[index + 1], false, style, paragraph ? payload.AsSpan(2).ToArray() : payload));
        }
        return output;
    }

    private static List<FkpProperty> ResolveBtePage(ReadOnlySpan<byte> wordDocument, ReadOnlySpan<byte> bte, bool paragraph)
    {
        Plc plc = ParsePlc(bte, 4, maximumRecords: 1);
        uint pageNumber = BinaryPrimitives.ReadUInt32LittleEndian(plc.Records[0]);
        long pageOffset = checked((long)pageNumber * 512);
        if (pageOffset > wordDocument.Length - 512) throw new InvalidDataException();
        List<FkpProperty> runs = ParseFkp(wordDocument.Slice((int)pageOffset, 512), paragraph);
        if (runs[0].FcStart != plc.Positions[0] || runs[^1].FcEnd != plc.Positions[^1]) throw new InvalidDataException();
        return runs;
    }

    private static byte[] ReadSepx(ReadOnlySpan<byte> wordDocument, ReadOnlySpan<byte> sed)
    {
        if (sed.Length != 12) throw new InvalidDataException();
        int offset = BinaryPrimitives.ReadInt32LittleEndian(sed[2..]);
        if (offset == -1) return [];
        if (offset < 0 || offset > wordDocument.Length - 2) throw new InvalidDataException();
        short length = BinaryPrimitives.ReadInt16LittleEndian(wordDocument[offset..]);
        if (length < 0 || offset + 2 + length > wordDocument.Length) throw new InvalidDataException();
        byte[] grpPrl = wordDocument.Slice(offset + 2, length).ToArray();
        _ = ParseSprms(grpPrl, maximumRecords: Math.Max(1, length / 2), maximumBytes: length);
        return grpPrl;
    }

    private static List<byte[]> ReadSections(ReadOnlySpan<byte> wordDocument, ReadOnlySpan<byte> plcfSed, ReadOnlySpan<byte> mainText, int ccpText)
    {
        Plc sections = ParsePlc(plcfSed, 12, maximumRecords: 64);
        if (sections.Positions[0] != 0 || sections.Positions[^1] < ccpText || sections.Positions[^1] > mainText.Length) throw new InvalidDataException();
        List<byte[]> output = [];
        for (int index = 0; index < sections.Records.Length; index++)
        {
            uint end = sections.Positions[index + 1];
            if (index != sections.Records.Length - 1 && (end == 0 || end > mainText.Length || mainText[(int)end - 1] != 0x0C)) throw new InvalidDataException();
            output.Add(ReadSepx(wordDocument, sections.Records[index]));
        }
        return output;
    }

    private static CascadeResult ApplyCascade(IEnumerable<PropertyLayer> layers)
    {
        SortedDictionary<string, string> state = new(StringComparer.Ordinal);
        List<string> snapshots = [];
        foreach (PropertyLayer layer in layers)
        {
            foreach ((string name, string value) in layer.Values) state[name] = value;
            snapshots.Add($"{layer.Name}:{Canonical(state)}");
        }
        return new(Canonical(state), snapshots.ToImmutableArray());
    }

    private static List<PropertyLayer> ResolveStyle(string name, IReadOnlyDictionary<string, StyleNode> styles, int maximumDepth)
    {
        List<PropertyLayer> result = [];
        HashSet<string> visiting = new(StringComparer.Ordinal);
        void Visit(string current, int depth)
        {
            if (depth > maximumDepth) throw new ResourceLimitException();
            if (!visiting.Add(current) || !styles.TryGetValue(current, out StyleNode? style)) throw new InvalidDataException();
            if (style.BaseName is not null) Visit(style.BaseName, depth + 1);
            result.Add(style.Properties);
            visiting.Remove(current);
        }
        Visit(name, 1);
        return result;
    }

    private static DataResolution ResolveData(int offset, IReadOnlyDictionary<int, DataRecord> records, int maximumDepth, int maximumBytes)
    {
        HashSet<int> visited = [];
        List<byte> bytes = [];
        List<string> path = [];
        int? current = offset;
        int depth = 0;
        while (current is int value)
        {
            if (!visited.Add(value) || !records.TryGetValue(value, out DataRecord? record)) return new(OracleOutcome.Corrupt, bytes.ToArray(), string.Join('>', path));
            if (++depth > maximumDepth || record.Bytes.Length > maximumBytes - bytes.Count) return new(OracleOutcome.ResourceLimitExceeded, bytes.ToArray(), string.Join('>', path));
            path.Add($"{record.Kind}@{value}");
            bytes.AddRange(record.Bytes);
            current = record.NextOffset;
        }
        return new(OracleOutcome.Complete, bytes.ToArray(), string.Join('>', path));
    }

    private static void WritePrcData(Span<byte> data, int offset, byte[] grpPrl)
    {
        BinaryPrimitives.WriteInt16LittleEndian(data[offset..], checked((short)grpPrl.Length));
        grpPrl.CopyTo(data[(offset + 2)..]);
    }

    private static DataResolution ResolvePrcData(ReadOnlySpan<byte> data, int offset, PrcContext context, int maximumDepth, int maximumBytes)
    {
        HashSet<int> visited = [];
        List<byte> bytes = [];
        List<string> path = [];
        int current = offset;
        for (int depth = 1; ; depth++)
        {
            if (depth > maximumDepth) return new(OracleOutcome.ResourceLimitExceeded, bytes.ToArray(), string.Join('>', path));
            if (!visited.Add(current) || current < 0 || current > data.Length - 2) return new(OracleOutcome.Corrupt, bytes.ToArray(), string.Join('>', path));
            short cbGrpprl = BinaryPrimitives.ReadInt16LittleEndian(data[current..]);
            if (cbGrpprl < 10 || cbGrpprl > 0x3FA2 || current + 2 + cbGrpprl > data.Length) return new(OracleOutcome.Corrupt, bytes.ToArray(), string.Join('>', path));
            if (cbGrpprl > maximumBytes - bytes.Count) return new(OracleOutcome.ResourceLimitExceeded, bytes.ToArray(), string.Join('>', path));
            byte[] body = data.Slice(current + 2, cbGrpprl).ToArray();
            List<OracleSprm> prls;
            try { prls = ParseSprms(body, Math.Max(1, cbGrpprl / 2), cbGrpprl); }
            catch (InvalidDataException) { return new(OracleOutcome.Corrupt, bytes.ToArray(), string.Join('>', path)); }
            bytes.AddRange(body);

            OracleSprm? reference = prls.FirstOrDefault(static prl => prl.Opcode is 0x6646 or 0x646B);
            if (reference is null || (reference.Opcode == 0x6646 && reference.RecordOffset != 0))
            {
                path.Add($"Terminal@{current}");
                return new(OracleOutcome.Complete, bytes.ToArray(), string.Join('>', path));
            }
            path.Add($"{(reference.Opcode == 0x6646 ? "HugePapx" : "TableProps")}@{current}");
            current = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(reference.Operand));
            _ = context;
        }
    }

    private static void ValidateHugePapxContainer(byte[] grpPrl, bool inGrpPrlAndIstd, ushort istd)
    {
        List<OracleSprm> prls = ParseSprms(grpPrl, 8, grpPrl.Length);
        int hugeIndex = prls.FindIndex(static prl => prl.Opcode == 0x6646);
        if (hugeIndex < 0) throw new InvalidDataException();
        if (inGrpPrlAndIstd && (istd != 0 || prls.Count != 1)) throw new InvalidDataException();
    }

    private static uint ValidatePicLocation(byte[] prl, Group owningGroup, int dataLength, int maximumReferences)
    {
        OracleSprm parsed = Assert.ContainsSingle(ParseSprms(prl, 1, prl.Length));
        if (parsed.Opcode != 0x6A03 || owningGroup != Group.Character) throw new InvalidDataException();
        if (maximumReferences < 1) throw new ResourceLimitException();
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(parsed.Operand);
        if (offset >= dataLength) throw new InvalidDataException();
        return offset;
    }

    private static string ValidateStyle(int istd, IReadOnlyList<StyleDefinition> styles, int maximumDepth, int maximumUpxBytes)
    {
        if ((uint)istd > 0x0FFD || istd >= styles.Count) throw new InvalidDataException();
        HashSet<int> visiting = [];
        List<int> order = [];
        int usedBytes = 0;
        void Visit(int current, int depth)
        {
            if (depth > maximumDepth) throw new ResourceLimitException();
            if ((uint)current > 0x0FFD || current >= styles.Count || !visiting.Add(current)) throw new InvalidDataException();
            StyleDefinition style = styles[current];
            if (style.IsEmpty) throw new InvalidDataException();
            ValidateReference(style.Next, styles, allowZeroSentinel: false);
            ValidateReference(style.Link, styles, allowZeroSentinel: true);
            if (style.Base != 0x0FFF)
            {
                if (style.Base == current) throw new InvalidDataException();
                Visit(style.Base, depth + 1);
            }
            int expectedCupx = style.Kind switch
            {
                StyleKind.Paragraph => style.RevisionMarked ? 3 : 2,
                StyleKind.Character => style.RevisionMarked ? 2 : 1,
                StyleKind.Table when !style.RevisionMarked => 3,
                StyleKind.Numbering when !style.RevisionMarked => 1,
                _ => throw new InvalidDataException(),
            };
            if (style.Cupx != expectedCupx || style.Upx.Count != expectedCupx) throw new InvalidDataException();
            UpxKind[] expectedKinds = style.Kind switch
            {
                StyleKind.Paragraph when style.RevisionMarked => [UpxKind.Papx, UpxKind.Chpx, UpxKind.ParagraphRevision],
                StyleKind.Paragraph => [UpxKind.Papx, UpxKind.Chpx],
                StyleKind.Character when style.RevisionMarked => [UpxKind.Chpx, UpxKind.CharacterRevision],
                StyleKind.Character => [UpxKind.Chpx],
                StyleKind.Table => [UpxKind.Tapx, UpxKind.Papx, UpxKind.Chpx],
                StyleKind.Numbering => [UpxKind.Papx],
                _ => throw new InvalidDataException(),
            };
            for (int index = 0; index < style.Upx.Count; index++)
            {
                Upx upx = style.Upx[index];
                if (upx.Kind != expectedKinds[index] || upx.Padding != 0 || upx.ContainsExcludedSprm) throw new InvalidDataException();
                usedBytes = checked(usedBytes + upx.Length + ((upx.Length & 1) == 0 ? 0 : 1));
                if (usedBytes > maximumUpxBytes) throw new ResourceLimitException();
            }
            order.Add(current);
            visiting.Remove(current);
        }
        Visit(istd, 1);
        return string.Join('>', order);
    }

    private static void ValidateReference(int reference, IReadOnlyList<StyleDefinition> styles, bool allowZeroSentinel)
    {
        if (allowZeroSentinel && reference == 0) return;
        if ((uint)reference > 0x0FFD || reference >= styles.Count || styles[reference].IsEmpty) throw new InvalidDataException();
    }

    private static CatalogueRow[] LoadCatalogue()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root was not found.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory.FullName, "docs", "architecture", "doc-sprm-catalogue.v1.json")));
        return document.RootElement.GetProperty("entries").EnumerateArray().Select(static element => new CatalogueRow(
            element.GetProperty("ordinal").GetInt32(),
            element.GetProperty("name").GetString()!,
            Convert.ToUInt16(element.GetProperty("opcode").GetString()![2..], 16),
            element.GetProperty("ispmd").GetInt32(),
            element.GetProperty("fSpec").GetInt32(),
            element.GetProperty("group").GetString()!,
            element.GetProperty("spra").GetInt32(),
            element.GetProperty("operandFraming").GetString()!,
            element.GetProperty("validPropertyArrays").EnumerateArray().Select(static owner => owner.GetString()!).ToArray())).ToArray();
    }

    private static int GroupCode(string group) => group switch { "Paragraph" => 1, "Character" => 2, "Picture" => 3, "Section" => 4, "Table" => 5, _ => throw new InvalidDataException() };
    private static string ExpectedFraming(ushort opcode, int spra) => opcode switch
    {
        0xD608 => "UInt16Cb_TotalOperandBytesEqualsCbPlus1",
        0xC615 => "ByteCb_Or_FFDeletedAddedTabFormula",
        _ => spra switch { 0 or 1 => "Fixed1", 2 or 4 or 5 => "Fixed2", 3 => "Fixed4", 6 => "ByteCb_ThenCbBytes", 7 => "Fixed3", _ => throw new InvalidDataException() },
    };
    private static byte[] FramedOperand(string framing) => framing switch
    {
        "Fixed1" => [1], "Fixed2" => [1, 2], "Fixed3" => [1, 2, 3], "Fixed4" => [1, 2, 3, 4],
        "ByteCb_ThenCbBytes" or "ByteCb_Or_FFDeletedAddedTabFormula" => [2, 1, 2],
        "UInt16Cb_TotalOperandBytesEqualsCbPlus1" => [3, 0, 1, 2],
        _ => throw new InvalidDataException(),
    };
    private static string[] ExpectedOwners(string group) => group switch
    {
        "Character" => ["CHPX", "UPX-CHPX", "Pcd.Prm-Character"],
        "Paragraph" => ["PAPX", "UPX-PAPX", "Pcd.Prm-Paragraph"],
        "Picture" => ["PICF"], "Section" => ["SEPX"], "Table" => ["TAPX", "UPX-TAPX"],
        _ => throw new InvalidDataException(),
    };

    private static PropertyLayer Layer(string name, params (string Name, string Value)[] values) => new(name, values);
    private static string Canonical(IEnumerable<KeyValuePair<string, string>> values) => string.Join(',', values.Select(static pair => $"{pair.Key}={pair.Value}"));
    private static ushort Opcode(int spra, int id) => checked((ushort)((spra << 13) | id));
    private static byte[] LittleEndian(ushort value) => [(byte)value, (byte)(value >> 8)];

    private enum Group { Paragraph, Character, Table, Section }
    public enum OracleOutcome { Complete, Corrupt, ResourceLimitExceeded }
    private sealed record OracleSprm(ushort Opcode, byte[] Operand, int RecordOffset, int RecordLength);
    private sealed record Prm0(Group Group, string Name, string Value);
    private sealed record Prm1(int Index);
    private sealed record GroupMutation(Group Group, string Name, string Value);
    private sealed record PropertyRecord(IReadOnlyList<GroupMutation> Mutations);
    private sealed record PropertyMutation(string Name, string Value) { public string Display => $"{Name}={Value}"; }
    private sealed record Plc(uint[] Positions, byte[][] Records);
    private sealed record FkpProperty(uint FcStart, uint FcEnd, bool IsDefault, ushort? StyleIndex, byte[] GrpPrl);
    private sealed record PropertyLayer(string Name, IReadOnlyList<(string Name, string Value)> Values);
    private sealed record CascadeResult(string Canonical, ImmutableArray<string> Snapshots);
    private sealed record StyleNode(string? BaseName, PropertyLayer Properties);
    private sealed record DataRecord(string Kind, byte[] Bytes, int? NextOffset);
    private sealed record DataResolution(OracleOutcome Outcome, byte[] Bytes, string Path);
    public sealed record CatalogueRow(int Ordinal, string Name, ushort Opcode, int Ispmd, int FSpec, string Group, int Spra, string OperandFraming, string[] ValidPropertyArrays);
    private enum PrcContext { Papx }
    private enum StyleKind { Paragraph, Character, Table, Numbering }
    private enum UpxKind { Papx, Chpx, Tapx, ParagraphRevision, CharacterRevision }
    private sealed record Upx(UpxKind Kind, int Length, byte Padding, bool ContainsExcludedSprm);
    private sealed record StyleDefinition(StyleKind Kind, int Base, int Next, int Link, bool RevisionMarked, int Cupx, IReadOnlyList<Upx> Upx, bool IsEmpty = false);
    private sealed class ResourceLimitException : Exception { }
}
