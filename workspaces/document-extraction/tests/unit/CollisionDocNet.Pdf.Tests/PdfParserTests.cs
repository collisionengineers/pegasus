using System.Text;

namespace CollisionDocNet.Pdf.Tests;

[TestClass]
public sealed class PdfParserTests
{
    [TestMethod]
    public void Parse_ClassicXrefPageAndWinAnsiText_ReturnsPositionedText()
    {
        byte[] pdf = PdfFixture.Create("BT /F1 12 Tf 72 700 Td (Hello) Tj ET", catalogVersion: "2.0");

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual(PdfParseOutcome.Complete, result.Outcome);
        Assert.AreEqual("1.7", result.HeaderVersion);
        Assert.AreEqual("2.0", result.CatalogVersion);
        PdfTextRun run = Assert.ContainsSingle(result.TextRuns);
        Assert.AreEqual("Hello", run.Text);
        Assert.AreEqual(72d, run.X);
        Assert.AreEqual(700d, run.Y);
    }

    [TestMethod]
    public void Parse_ToUnicodeCMap_MapsArbitraryCharacterCode()
    {
        const string cmap = "1 beginbfchar <01> <03A9> endbfchar";
        byte[] pdf = PdfFixture.Create("BT /F1 12 Tf <01> Tj ET", toUnicode: cmap);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual("Ω", Assert.ContainsSingle(result.TextRuns).Text);
        Assert.AreEqual("ToUnicode;position-approximate", result.TextRuns[0].MappingSource);
    }

    [TestMethod]
    public void Parse_ToUnicodeVariableWidthArrayRange_UsesLongestCodeAndArrayMapping()
    {
        const string cmap = "1 begincodespacerange <0000> <FFFF> endcodespacerange 1 beginbfrange <0100> <0101> [<0041> <0042>] endbfrange";
        byte[] pdf = PdfFixture.Create("BT /F1 12 Tf <01000101> Tj ET", toUnicode: cmap);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual("AB", Assert.ContainsSingle(result.TextRuns).Text);
    }

    [TestMethod]
    public void Parse_WinAnsiUndefinedControlRange_MapsDefinedEuro()
    {
        PdfParseResult result = PdfParser.Parse(PdfFixture.Create("BT /F1 12 Tf (\\200) Tj ET"));
        Assert.AreEqual("€", Assert.ContainsSingle(result.TextRuns).Text);
    }

    [TestMethod]
    public void Parse_PathOperatorsAndInlineImage_RetainsPassiveAssetAndContinuesText()
    {
        byte[] pdf = PdfFixture.Create("0 0 10 10 re f n BI /W 1 /H 1 /BPC 8 /CS /G ID X EI BT /F1 12 Tf (After) Tj ET");

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual(PdfParseOutcome.Complete, result.Outcome);
        Assert.AreEqual("After", Assert.ContainsSingle(result.TextRuns).Text);
        Assert.Contains("PDF_INLINE_IMAGE_RETAINED", result.Issues.Select(issue => issue.Code).ToArray());
        PdfPassiveAsset asset = Assert.ContainsSingle(result.Evidence.Assets);
        Assert.AreEqual("inline-image", asset.Kind);
        Assert.AreEqual("1", asset.Properties["Width"]);
        Assert.AreEqual("1", asset.Properties["Height"]);
        Assert.AreEqual("DeviceGray", asset.Properties["ColorSpace"]);
        CollectionAssert.AreEqual(new byte[] { (byte)'X' }, asset.Bytes);
    }

    [TestMethod]
    public void Parse_InlineImageAbbreviatedFilter_ExpandsPropertiesAndKeepsStableIdentity()
    {
        byte[] pdf = PdfFixture.Create("BI /W 1 /H 1 /BPC 8 /CS /RGB /F /DCT ID abc EI");

        PdfParseResult first = PdfParser.Parse(pdf);
        PdfParseResult second = PdfParser.Parse(pdf);

        PdfPassiveAsset asset = Assert.ContainsSingle(first.Evidence.Assets);
        Assert.AreEqual(PdfParseOutcome.Complete, first.Outcome);
        Assert.AreEqual("DCTDecode", asset.Properties["Filter"]);
        Assert.AreEqual("DeviceRGB", asset.Properties["ColorSpace"]);
        Assert.AreEqual("image/jpeg", asset.MediaType);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abc"), asset.Bytes);
        Assert.AreEqual(asset.StableId, Assert.ContainsSingle(second.Evidence.Assets).StableId);
    }

    [TestMethod]
    public void Parse_InlineImageAtByteLimit_IsRetainedButExceededLimitIsResourceOutcome()
    {
        byte[] exact = PdfFixture.Create("BI /W 1 /H 1 ID X EI");
        byte[] exceeded = PdfFixture.Create("BI /W 2 /H 1 ID XY EI");

        PdfParseResult accepted = PdfParser.Parse(exact, new PdfLimits { MaxInlineImageBytes = 1 });
        PdfParseResult rejected = PdfParser.Parse(exceeded, new PdfLimits { MaxInlineImageBytes = 1 });

        Assert.AreEqual(PdfParseOutcome.Complete, accepted.Outcome);
        Assert.HasCount(1, Assert.ContainsSingle(accepted.Evidence.Assets).Bytes);
        Assert.AreEqual(PdfParseOutcome.ResourceLimitExceeded, rejected.Outcome);
        Assert.Contains("PDF_INLINE_IMAGE_LIMIT", rejected.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void Parse_InlineImageWithoutId_ReportsVisiblePartialOutcome()
    {
        PdfParseResult result = PdfParser.Parse(PdfFixture.Create("BI /W 1 /H 1"));

        Assert.AreEqual(PdfParseOutcome.Partial, result.Outcome);
        Assert.Contains("PDF_INLINE_IMAGE_ID_MISSING", result.Issues.Select(issue => issue.Code).ToArray());
        Assert.IsEmpty(result.Evidence.Assets);
    }

    [TestMethod]
    public void Parse_InlineAssetBudgetExceeded_ReturnsResourceOutcomeWithoutAsset()
    {
        PdfParseResult result = PdfParser.Parse(PdfFixture.Create("BI /W 1 /H 1 ID X EI"), new PdfLimits { MaxAssetBytes = 0 });

        Assert.AreEqual(PdfParseOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.Contains("PDF_ASSET_LIMIT", result.Issues.Select(issue => issue.Code).ToArray());
        Assert.IsEmpty(result.Evidence.Assets);
    }

    [TestMethod]
    public void Parse_TjArrayAndTextMovement_OrdersRunsGeometrically()
    {
        byte[] pdf = PdfFixture.Create("BT /F1 10 Tf 10 10 Td [(A) -120 (B)] TJ 0 20 Td (Top) Tj ET");

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.HasCount(3, result.TextRuns);
        Assert.AreEqual("Top", result.TextRuns[0].Text);
        Assert.AreEqual("A", result.TextRuns[1].Text);
        Assert.AreEqual("B", result.TextRuns[2].Text);
    }

    [TestMethod]
    public void Parse_EncryptionDictionary_ReturnsEncryptedWithoutDecrypting()
    {
        byte[] pdf = PdfFixture.Create("", encrypted: true);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual(PdfParseOutcome.Encrypted, result.Outcome);
        Assert.Contains("PDF_ENCRYPTED", result.Issues.Select(i => i.Code).ToArray());
        Assert.IsNotNull(result.Evidence.Encryption);
        Assert.AreEqual("Standard", result.Evidence.Encryption.Handler);
        Assert.AreEqual(4, result.Evidence.Encryption.Revision);
    }

    [TestMethod]
    public void Parse_UnreferencedEncryptLookingObject_DoesNotClassifyAsEncrypted()
    {
        byte[] original = PdfFixture.Create("BT /F1 12 Tf (Plain) Tj ET");
        byte[] suffix = Encoding.ASCII.GetBytes("99 0 obj\n<< /Encrypt 98 0 R >>\nendobj\n");
        byte[] input = new byte[original.Length + suffix.Length];
        original.CopyTo(input, 0); suffix.CopyTo(input, original.Length);

        PdfParseResult result = PdfParser.Parse(input);

        Assert.AreNotEqual(PdfParseOutcome.Encrypted, result.Outcome);
        Assert.IsNull(result.Evidence.Encryption);
        Assert.IsFalse(result.Objects.ContainsKey(new PdfObjectId(99, 0)));
    }

    [TestMethod]
    public void Parse_XrefMarksCatalogFree_ExcludesShadowObjectAndReturnsCorrupt()
    {
        byte[] input = PdfFixture.Create("");
        int state = FindClassicXrefEntryState(input, objectIndex: 1);
        input[state] = (byte)'f';

        PdfParseResult result = PdfParser.Parse(input);

        Assert.AreEqual(PdfParseOutcome.Corrupt, result.Outcome);
        Assert.IsFalse(result.Objects.ContainsKey(new PdfObjectId(1, 0)));
    }

    [TestMethod]
    public void Parse_XrefInUseOffsetDoesNotResolve_ReportsMissingAuthoritativeObject()
    {
        byte[] input = PdfFixture.Create("");
        int line = FindClassicXrefEntryState(input, objectIndex: 1) - 17;
        input[line] = input[line] == (byte)'0' ? (byte)'1' : (byte)'0';

        PdfParseResult result = PdfParser.Parse(input);

        Assert.AreEqual(PdfParseOutcome.Corrupt, result.Outcome);
        Assert.Contains("PDF_XREF_OBJECT_MISSING", result.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void Parse_BadStartXref_StrictIsCorruptAndRecoveryIsPartial()
    {
        byte[] pdf = PdfFixture.Create("BT (evidence) Tj ET");
        int marker = Encoding.ASCII.GetString(pdf).LastIndexOf("startxref", StringComparison.Ordinal);
        int number = marker + "startxref\n".Length;
        pdf[number] = (byte)'9'; pdf[number + 1] = (byte)'9'; pdf[number + 2] = (byte)'9';

        PdfParseResult strict = PdfParser.Parse(pdf);
        PdfParseResult recovered = PdfParser.Parse(pdf, allowRecovery: true);

        Assert.AreEqual(PdfParseOutcome.Corrupt, strict.Outcome);
        Assert.AreEqual(PdfParseOutcome.Partial, recovered.Outcome);
        Assert.IsTrue(recovered.UsedRecovery);
        Assert.Contains("PDF_BOUNDED_RECOVERY", recovered.Issues.Select(i => i.Code).ToArray());
    }

    [TestMethod]
    public void Parse_InputLimit_ReturnsResourceLimitWithoutScanning()
    {
        PdfParseResult result = PdfParser.Parse(new byte[32], new PdfLimits { MaxInputBytes = 8 });
        Assert.AreEqual(PdfParseOutcome.ResourceLimitExceeded, result.Outcome);
    }

    [TestMethod]
    public void Parse_Cancelled_ReturnsStructuredCancelledOutcome()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        PdfParseResult result = PdfParser.Parse(PdfFixture.Create(""), cancellationToken: source.Token);

        Assert.AreEqual(PdfParseOutcome.Cancelled, result.Outcome);
        Assert.Contains("PDF_CANCELLED", result.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void Parse_SameBytes_IsDeterministic()
    {
        byte[] pdf = PdfFixture.Create("BT /F1 12 Tf (Same) Tj ET");
        PdfParseResult first = PdfParser.Parse(pdf); PdfParseResult second = PdfParser.Parse(pdf);
        CollectionAssert.AreEqual(first.TextRuns.ToArray(), second.TextRuns.ToArray());
        CollectionAssert.AreEqual(first.Issues.ToArray(), second.Issues.ToArray());
    }

    private static int FindClassicXrefEntryState(byte[] input, int objectIndex)
    {
        string text = Encoding.ASCII.GetString(input);
        int xref = text.LastIndexOf("\nxref\n", StringComparison.Ordinal) + 1;
        int headerEnd = text.IndexOf('\n', xref + 5) + 1;
        int position = headerEnd;
        for (int i = 0; i <= objectIndex; i++) position = text.IndexOf('\n', position) + 1;
        return position - 3;
    }

    [TestMethod]
    public void Parse_XrefStream_ValidatesAndResolvesCatalog()
    {
        PdfParseResult result = PdfParser.Parse(PdfFixture.CreateXrefStream());

        Assert.AreEqual(PdfParseOutcome.Complete, result.Outcome);
        Assert.AreEqual("1.7", result.HeaderVersion);
        Assert.HasCount(3, result.Objects);
    }

    [TestMethod]
    public void Parse_XrefStreamFreeEntry_ShadowsScannedCatalog()
    {
        PdfParseResult result = PdfParser.Parse(PdfFixture.CreateXrefStream(catalogFree: true));

        Assert.AreEqual(PdfParseOutcome.Corrupt, result.Outcome);
        Assert.IsFalse(result.Objects.ContainsKey(new PdfObjectId(1, 0)));
    }

    [TestMethod]
    public void Parse_ObjectStream_MaterialisesCompressedCatalog()
    {
        PdfParseResult result = PdfParser.Parse(PdfFixture.CreateObjectStream());

        Assert.AreEqual(PdfParseOutcome.Complete, result.Outcome);
        Assert.IsTrue(result.Objects.ContainsKey(new PdfObjectId(4, 0)));
    }

    private static class PdfFixture
    {
        public static byte[] Create(string content, string? catalogVersion = null, string? toUnicode = null, bool encrypted = false)
        {
            var bytes = new List<byte>(); var offsets = new List<int> { 0 };
            Add(bytes, "%PDF-1.7\n%\xE2\xE3\xCF\xD3\n");
            AddObject(bytes, offsets, $"<< /Type /Catalog /Pages 2 0 R{(catalogVersion is null ? string.Empty : $" /Version /{catalogVersion}")} >>");
            AddObject(bytes, offsets, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
            AddObject(bytes, offsets, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
            AddStream(bytes, offsets, $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>", content);
            AddObject(bytes, offsets, toUnicode is null ? "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>" : "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /ToUnicode 6 0 R >>");
            if (toUnicode is not null) AddStream(bytes, offsets, $"<< /Length {Encoding.ASCII.GetByteCount(toUnicode)} >>", toUnicode);
            if (encrypted) AddObject(bytes, offsets, "<< /Filter /Standard /V 4 /R 4 >>");
            int xref = bytes.Count;
            Add(bytes, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
            for (int i = 1; i < offsets.Count; i++) Add(bytes, $"{offsets[i]:D10} 00000 n \n");
            string encryptEntry = encrypted ? $" /Encrypt {offsets.Count - 1} 0 R" : string.Empty;
            Add(bytes, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R{encryptEntry} >>\nstartxref\n{xref}\n%%EOF\n");
            return bytes.ToArray();
        }

        public static byte[] CreateXrefStream(bool catalogFree = false)
        {
            var bytes = new List<byte>(); var offsets = new List<int> { 0 };
            Add(bytes, "%PDF-1.7\n");
            AddObject(bytes, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
            AddObject(bytes, offsets, "<< /Type /Pages /Count 0 /Kids [] >>");
            int xrefOffset = bytes.Count;
            byte[] entries = XrefEntries([0, offsets[1], offsets[2], xrefOffset], [0, catalogFree ? 0 : 1, 1, 1]);
            AddRawStream(bytes, 3, $"<< /Type /XRef /Size 4 /Root 1 0 R /W [1 4 2] /Length {entries.Length} >>", entries);
            Add(bytes, $"startxref\n{xrefOffset}\n%%EOF\n");
            return bytes.ToArray();
        }

        public static byte[] CreateObjectStream()
        {
            var bytes = new List<byte>();
            Add(bytes, "%PDF-1.7\n");
            int objectStreamOffset = bytes.Count;
            const string packed = "4 0 << /Type /Catalog /Pages 2 0 R >>";
            AddRawStream(bytes, 1, $"<< /Type /ObjStm /N 1 /First 4 /Length {Encoding.ASCII.GetByteCount(packed)} >>", Encoding.ASCII.GetBytes(packed));
            int pagesOffset = bytes.Count;
            Add(bytes, "2 0 obj\n<< /Type /Pages /Count 0 /Kids [] >>\nendobj\n");
            int xrefOffset = bytes.Count;
            byte[] entries = XrefEntries([0, objectStreamOffset, pagesOffset, xrefOffset, 1], [0, 1, 1, 1, 2]);
            AddRawStream(bytes, 3, $"<< /Type /XRef /Size 5 /Root 4 0 R /W [1 4 2] /Length {entries.Length} >>", entries);
            Add(bytes, $"startxref\n{xrefOffset}\n%%EOF\n");
            return bytes.ToArray();
        }

        private static void AddObject(List<byte> bytes, List<int> offsets, string body)
        {
            int number = offsets.Count; offsets.Add(bytes.Count); Add(bytes, $"{number} 0 obj\n{body}\nendobj\n");
        }
        private static void AddStream(List<byte> bytes, List<int> offsets, string dictionary, string content)
        {
            int number = offsets.Count; offsets.Add(bytes.Count); Add(bytes, $"{number} 0 obj\n{dictionary}\nstream\n{content}\nendstream\nendobj\n");
        }
        private static void AddRawStream(List<byte> bytes, int number, string dictionary, byte[] content)
        {
            Add(bytes, $"{number} 0 obj\n{dictionary}\nstream\n"); bytes.AddRange(content); Add(bytes, "\nendstream\nendobj\n");
        }
        private static byte[] XrefEntries(int[] fields, int[] types)
        {
            byte[] result = new byte[fields.Length * 7];
            for (int i = 0; i < fields.Length; i++)
            {
                int start = i * 7; result[start] = (byte)types[i];
                result[start + 1] = (byte)(fields[i] >> 24); result[start + 2] = (byte)(fields[i] >> 16); result[start + 3] = (byte)(fields[i] >> 8); result[start + 4] = (byte)fields[i];
                if (types[i] == 0) { result[start + 5] = 0xFF; result[start + 6] = 0xFF; }
            }
            return result;
        }
        private static void Add(List<byte> bytes, string text) => bytes.AddRange(Encoding.Latin1.GetBytes(text));
    }
}
