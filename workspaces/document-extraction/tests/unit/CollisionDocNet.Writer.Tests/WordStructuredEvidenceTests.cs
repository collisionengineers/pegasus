using System.Buffers.Binary;
using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Writer.Tests;

[TestClass]
public sealed class WordStructuredEvidenceTests
{
    [TestMethod]
    public void SprmParser_KnownAndUnknownRecords_PreservesOrderedOperands()
    {
        byte[] bytes = [0x35, 0x08, 0x01, 0x34, 0x08, 0x7f];

        var result = WordSprmParser.Parse(bytes, 8, out WordBinaryIssue? issue);

        Assert.IsNull(issue);
        Assert.HasCount(2, result);
        Assert.AreEqual(WordSprmMeaning.Bold, result[0].Meaning);
        Assert.IsTrue(result[0].IsKnown);
        Assert.AreEqual((byte)1, Assert.ContainsSingle(result[0].Operand));
        Assert.AreEqual((ushort)0x0834, result[1].Opcode);
        Assert.AreEqual(WordSprmMeaning.Unknown, result[1].Meaning);
        Assert.IsFalse(result[1].IsKnown);
        Assert.AreEqual((byte)0x7f, Assert.ContainsSingle(result[1].Operand));
    }

    [TestMethod]
    public void SprmParser_TruncatedVariableRecord_ReturnsVisibleIssueAndPriorEvidence()
    {
        byte[] bytes = [0x35, 0x08, 0x01, 0x00, 0xc0, 0x04, 0xaa];

        var result = WordSprmParser.Parse(bytes, 8, out WordBinaryIssue? issue);

        Assert.HasCount(1, result);
        Assert.IsNotNull(issue);
        Assert.AreEqual("doc-sprm-operand-truncated", issue.Code);
    }

    [TestMethod]
    public void Extract_SimplePiecePrm_MapsKnownIsprmWithoutDiscardingOperand()
    {
        const ushort boldPrm = (1 << 8) | (85 << 1);
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "Hi", boldPrm)]);

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        WordPropertyRun run = Assert.ContainsSingle(result.PropertyRuns);
        Assert.AreEqual(WordPropertyRunKind.Piece, run.Kind);
        WordSprm sprm = Assert.ContainsSingle(run.Sprms);
        Assert.AreEqual((ushort)0x0835, sprm.Opcode);
        Assert.AreEqual(WordSprmMeaning.Bold, sprm.Meaning);
        Assert.AreEqual((byte)1, Assert.ContainsSingle(sprm.Operand));
        Assert.AreEqual((uint)0, run.CpStart);
        Assert.AreEqual((uint)2, run.CpEnd);
    }

    [TestMethod]
    public void Extract_CharacterFkp_MapsFcToCpAndPreservesUnknownSprm()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: (word, table) =>
            {
                const int bte = 300;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte), 700);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 4), 702);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 8), 2);
                WordBinaryFixture.SetFibRange(word, 12, bte, 12);

                Span<byte> fkp = word.AsSpan(1024, 512);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp, 700);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp[4..], 702);
                fkp[8] = 100;
                fkp[200] = 14;
                fkp[201] = 0x35;
                fkp[202] = 0x08;
                fkp[203] = 1;
                fkp[204] = 0x30;
                fkp[205] = 0x4a;
                fkp[206] = 2;
                fkp[207] = 0;
                fkp[208] = 0x6d;
                fkp[209] = 0x48;
                fkp[210] = 9;
                fkp[211] = 4;
                fkp[212] = 0x34;
                fkp[213] = 0x08;
                fkp[214] = 0x7f;
                fkp[511] = 1;
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        WordPropertyRun run = Assert.ContainsSingle(result.PropertyRuns);
        Assert.AreEqual(WordPropertyRunKind.Character, run.Kind);
        Assert.AreEqual((uint)0, run.CpStart);
        Assert.AreEqual((uint)2, run.CpEnd);
        Assert.AreEqual((uint)700, run.FcStart);
        Assert.AreEqual((uint)702, run.FcEnd);
        Assert.HasCount(4, run.Sprms);
        Assert.AreEqual(WordSprmMeaning.Bold, run.Sprms[0].Meaning);
        Assert.AreEqual(WordSprmMeaning.Font, run.Sprms[1].Meaning);
        Assert.AreEqual(WordSprmMeaning.Language, run.Sprms[2].Meaning);
        Assert.AreEqual(WordSprmMeaning.Unknown, run.Sprms[3].Meaning);
    }

    [TestMethod]
    public void Extract_ParagraphFkp_ExposesStyleTableAndListSemantics()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 900, false, "Hi")],
            configureStreams: static (word, table) =>
            {
                BinaryPrimitives.WriteUInt16LittleEndian(word.AsSpan(152), 76);
                const int bte = 300;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte), 900);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 4), 902);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 8), 2);
                WordBinaryFixture.SetFibRange(word, 13, bte, 12);

                const int style = 340;
                table[style] = 1;
                table[style + 1] = 2;
                WordBinaryFixture.SetFibRange(word, 1, style, 2);
                const int list = 350;
                table[list] = 3;
                table[list + 1] = 4;
                WordBinaryFixture.SetFibRange(word, 73, list, 2);

                Span<byte> fkp = word.AsSpan(1024, 512);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp, 900);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp[4..], 902);
                fkp[8] = 100;
                fkp[200] = 4;
                BinaryPrimitives.WriteUInt16LittleEndian(fkp[201..], 7);
                fkp[203] = 0x16;
                fkp[204] = 0x24;
                fkp[205] = 1;
                fkp[206] = 0x0a;
                fkp[207] = 0x26;
                fkp[208] = 2;
                fkp[511] = 1;
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        WordPropertyRun run = Assert.ContainsSingle(result.PropertyRuns);
        Assert.AreEqual(WordPropertyRunKind.Paragraph, run.Kind);
        Assert.AreEqual((ushort)7, run.StyleIndex);
        Assert.AreEqual(WordSprmMeaning.InTable, run.Sprms[0].Meaning);
        Assert.AreEqual(WordSprmMeaning.ListLevel, run.Sprms[1].Meaning);
        Assert.Contains(WordStructureKind.StyleSheet, result.Structures.Select(static value => value.Kind));
        Assert.Contains(WordStructureKind.ListDefinition, result.Structures.Select(static value => value.Kind));
        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
    }

    [TestMethod]
    public void Extract_FieldPlc_ReturnsExactAnchoredPassiveStructure()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, table) =>
            {
                const int plc = 340;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(plc), 0);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(plc + 4), 2);
                table[plc + 8] = 0x13;
                table[plc + 9] = 0;
                WordBinaryFixture.SetFibRange(word, 16, plc, 10);
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        WordStructureRecord structure = Assert.ContainsSingle(result.Structures);
        Assert.AreEqual(WordStructureKind.Field, structure.Kind);
        Assert.AreEqual((uint)0, structure.CpStart);
        Assert.AreEqual((uint)2, structure.CpEnd);
        Assert.IsFalse(structure.SemanticallyDecoded);
        Assert.HasCount(2, structure.RecordBytes);
        Assert.Contains("doc-structure-semantic-unimplemented", result.Issues.Select(static issue => issue.Code));
        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
    }

    [TestMethod]
    public void Extract_PassiveStreamsAndSummaryInformation_ReturnStableHashedEvidence()
    {
        Guid objectClass = Guid.Parse("0003000c-0000-0000-c000-000000000046");
        CompoundFileDirectoryEntry[] additional =
        [
            WordBinaryFixture.AdditionalEntry(3, "ObjectPool", CompoundFileObjectType.Storage, 0, [], objectClass),
            WordBinaryFixture.AdditionalEntry(4, "EmbeddedObject", CompoundFileObjectType.Stream, 3, [1, 2, 3]),
            WordBinaryFixture.AdditionalEntry(5, "Data", CompoundFileObjectType.Stream, 0, [4, 5, 6]),
            WordBinaryFixture.AdditionalEntry(6, "\u0005SummaryInformation", CompoundFileObjectType.Stream, 0, BuildSummaryInformation("Synthetic title")),
        ];
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")], hasPictures: true, additionalEntries: additional);

        WordBinaryExtractionResult first = WordBinaryExtractor.Extract(file);
        WordBinaryExtractionResult second = WordBinaryExtractor.Extract(file);
        WordBinaryExtractionResult bounded = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumPassiveAssets = 1 });
        WordBinaryExtractionResult byteBounded = WordBinaryExtractor.Extract(
            file, WordBinaryExtractionLimits.Default with { MaximumPassiveAssetBytes = 3 });

        Assert.HasCount(3, first.PassiveAssets);
        Assert.AreEqual(WordPassiveAssetKind.OleObject, first.PassiveAssets[0].Kind);
        Assert.AreEqual(objectClass, first.PassiveAssets[0].ClassId);
        Assert.AreEqual((uint)3, first.PassiveAssets[0].OwningStorageStreamId);
        Assert.AreEqual("Root Entry/ObjectPool/EmbeddedObject", first.PassiveAssets[0].SourcePath);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, first.PassiveAssets[0].Content.ToArray());
        Assert.AreEqual(WordPassiveAssetKind.PictureData, first.PassiveAssets[1].Kind);
        Assert.AreEqual(WordPassiveAssetKind.PropertySet, first.PassiveAssets[2].Kind);
        WordMetadataProperty title = Assert.ContainsSingle(first.Metadata);
        Assert.AreEqual("Title", title.Name);
        Assert.AreEqual("Synthetic title", title.Value);
        CollectionAssert.AreEqual(first.PassiveAssets.Select(static asset => asset.StableId).ToArray(), second.PassiveAssets.Select(static asset => asset.StableId).ToArray());
        CollectionAssert.AreEqual(first.Metadata.Select(static value => value.StableId).ToArray(), second.Metadata.Select(static value => value.StableId).ToArray());
        Assert.AreEqual(WordBinaryOutcome.Partial, first.Outcome);
        Assert.AreEqual(WordBinaryOutcome.ResourceLimitExceeded, bounded.Outcome);
        Assert.HasCount(1, bounded.PassiveAssets);
        Assert.Contains("doc-passive-asset-limit", bounded.Issues.Select(static issue => issue.Code));
        Assert.HasCount(1, byteBounded.PassiveAssets);
        Assert.AreEqual(WordBinaryOutcome.ResourceLimitExceeded, byteBounded.Outcome);
        Assert.Contains("doc-passive-asset-byte-limit", byteBounded.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Extract_BteExtentDoesNotMatchFkp_RejectsPropertyEvidence(bool mismatchStart)
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: (word, table) =>
            {
                const int bte = 300;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte), mismatchStart ? 701u : 700u);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 4), mismatchStart ? 702u : 703u);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 8), 2);
                WordBinaryFixture.SetFibRange(word, 12, bte, 12);

                Span<byte> fkp = word.AsSpan(1024, 512);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp, 700);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp[4..], 702);
                fkp[511] = 1;
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        Assert.IsEmpty(result.PropertyRuns);
        Assert.Contains("doc-bte-fkp-extent-mismatch", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public void Extract_BteNonIncreasingInterval_RejectsPropertyEvidence()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, table) =>
            {
                const int bte = 300;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte), 700);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 4), 700);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 8), 2);
                WordBinaryFixture.SetFibRange(word, 12, bte, 12);
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.IsEmpty(result.PropertyRuns);
        Assert.Contains("doc-bte-fc-order", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public void Extract_BteAliasesFkpPage_ReportsAliasAndDoesNotDuplicateEvidence()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, table) =>
            {
                const int bte = 300;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte), 700);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 4), 701);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 8), 702);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 12), 2);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 16), 2);
                WordBinaryFixture.SetFibRange(word, 12, bte, 20);

                Span<byte> fkp = word.AsSpan(1024, 512);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp, 700);
                BinaryPrimitives.WriteUInt32LittleEndian(fkp[4..], 702);
                fkp[511] = 1;
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.IsEmpty(result.PropertyRuns);
        Assert.Contains("doc-bte-fkp-alias", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public void Extract_RepeatedRun_IsDeterministicAcrossWholeResult()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            hasPictures: true,
            additionalEntries:
            [
                WordBinaryFixture.AdditionalEntry(3, "Data", CompoundFileObjectType.Stream, 0, [4, 5, 6]),
            ]);

        string first = System.Text.Json.JsonSerializer.Serialize(WordBinaryExtractor.Extract(file));
        string second = System.Text.Json.JsonSerializer.Serialize(WordBinaryExtractor.Extract(file));

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Extract_NonPositiveStructuredLimit_ReturnsResourceLimitWithoutParsing()
    {
        CompoundFile file = WordBinaryFixture.Create([new(0, 2, 700, false, "Hi")]);
        WordBinaryExtractionLimits limits = WordBinaryExtractionLimits.Default with { MaximumSprmsPerRun = 0 };

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file, limits);

        Assert.AreEqual(WordBinaryOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.AreEqual("doc-structured-limit-invalid", Assert.ContainsSingle(result.Issues).Code);
        Assert.IsEmpty(result.Stories);
    }

    [TestMethod]
    public void Extract_MalformedFkpPage_IsVisibleWithoutLosingText()
    {
        CompoundFile file = WordBinaryFixture.Create(
            [new(0, 2, 700, false, "Hi")],
            configureStreams: static (word, table) =>
            {
                const int bte = 300;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte), 700);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 4), 702);
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(bte + 8), uint.MaxValue);
                WordBinaryFixture.SetFibRange(word, 12, bte, 12);
            });

        WordBinaryExtractionResult result = WordBinaryExtractor.Extract(file);

        Assert.AreEqual("Hi", result.Stories[0].Text);
        Assert.AreEqual(WordBinaryOutcome.Partial, result.Outcome);
        Assert.Contains("doc-fkp-page-overflow", result.Issues.Select(static issue => issue.Code));
        Assert.IsEmpty(result.PropertyRuns);
    }

    private static byte[] BuildSummaryInformation(string title)
    {
        byte[] encoded = System.Text.Encoding.Unicode.GetBytes(title + "\0");
        byte[] bytes = new byte[72 + encoded.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xfffe);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 48);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48), checked((uint)(24 + encoded.Length)));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 31);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), checked((uint)(title.Length + 1)));
        encoded.CopyTo(bytes, 72);
        return bytes;
    }
}
