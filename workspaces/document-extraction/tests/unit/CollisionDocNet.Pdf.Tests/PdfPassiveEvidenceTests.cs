using System.Text;

namespace CollisionDocNet.Pdf.Tests;

[TestClass]
public sealed class PdfPassiveEvidenceTests
{
    [TestMethod]
    public void Parse_InfoAndXmp_ReportsMetadataAndUnvalidatedProfileClaims()
    {
        const string xmp = "<x:xmpmeta xmlns:x='adobe:ns:meta/' xmlns:pdfaid='http://www.aiim.org/pdfa/ns/id/'><pdfaid:part>2</pdfaid:part><pdfaid:conformance>U</pdfaid:conformance></x:xmpmeta>";
        byte[] pdf = EvidencePdf.Create(
            [
                EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>"),
                EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
                EvidencePdf.Object("<< /Title (Evidence) /Author (Analyst) /Producer (Synthetic) >>"),
                EvidencePdf.Stream("<< /Type /Metadata /Subtype /XML", Encoding.UTF8.GetBytes(xmp))
            ], infoObject: 3);

        PdfParseResult result = PdfParser.Parse(pdf);

        PdfEvidenceItem info = Assert.ContainsSingle(result.Evidence.Items.Where(item => item.Kind == "metadata" && item.Subtype == "Info"));
        Assert.AreEqual("Evidence", info.Properties["Title"]);
        PdfEvidenceItem claim = Assert.ContainsSingle(result.Evidence.Items.Where(item => item.Kind == "profile-claim"));
        Assert.AreEqual("2", claim.Properties["part"]);
        Assert.AreEqual("U", claim.Properties["conformance"]);
        Assert.AreEqual("not-performed", claim.Properties["Validation"]);
        PdfPassiveAsset metadata = Assert.ContainsSingle(result.Evidence.Assets);
        Assert.AreEqual("metadata", metadata.Kind);
        Assert.AreEqual("application/rdf+xml", metadata.MediaType);
    }

    [TestMethod]
    public void Parse_TaggingNavigationFormsAndOptionalContent_ProducesPassiveInventory()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R /OCProperties << /OCGs [4 0 R] >> /Outlines 5 0 R /PageLabels 6 0 R /Names 7 0 R /AcroForm 8 0 R /Collection << /View /D >> /AF [10 0 R] >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Object("<< /Type /StructTreeRoot /K [<< /Type /StructElem /S /P /ActualText (Replacement) /Alt (Description) >>] >>"),
            EvidencePdf.Object("<< /Type /OCG /Name (Optional layer) >>"),
            EvidencePdf.Object("<< /Count 0 >>"),
            EvidencePdf.Object("<< /Nums [0 << /S /D /St 1 >>] >>"),
            EvidencePdf.Object("<< /EmbeddedFiles << /Names [(file.bin) 10 0 R] >> >>"),
            EvidencePdf.Object("<< /Fields [9 0 R] /XFA (passive-xfa-value) >>"),
            EvidencePdf.Object("<< /FT /Tx /T (FieldName) /V (FieldValue) >>"),
            EvidencePdf.Object("<< /Type /Filespec /F (file.bin) /UF (file.bin) /AFRelationship /Data /EF << /F 11 0 R >> >>"),
            EvidencePdf.Stream("<< /Type /EmbeddedFile /Subtype /application#2Foctet-stream", [1, 2, 3])
        ]);

        PdfParseResult result = PdfParser.Parse(pdf);
        string[] kinds = result.Evidence.Items.Select(item => item.Kind).ToArray();

        Assert.Contains("tagged-structure", kinds);
        Assert.Contains("optional-content", kinds);
        Assert.Contains("outlines", kinds);
        Assert.Contains("page-labels", kinds);
        Assert.Contains("name-trees", kinds);
        Assert.Contains("acroform", kinds);
        Assert.Contains("portfolio", kinds);
        Assert.Contains("associated-files", kinds);
        Assert.Contains("form-field", kinds);
        Assert.Contains("file-specification", kinds);
        Assert.AreEqual("Replacement", result.Evidence.Items.First(item => item.Kind == "tagged-structure" && item.Properties.ContainsKey("ActualText")).Properties["ActualText"]);
        PdfPassiveAsset attachment = Assert.ContainsSingle(result.Evidence.Assets);
        Assert.AreEqual("file.bin", attachment.Name);
        Assert.AreEqual("Data", attachment.Properties["AFRelationship"]);
    }

    [TestMethod]
    public void Parse_ImagesEmbeddedFilesAndMedia_ReturnsStablePassiveAssets()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Stream("<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /BitsPerComponent 8 /Filter /DCTDecode /SMask 4 0 R", [0xFF, 0xD8, 0xFF, 0xD9]),
            EvidencePdf.Stream("<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ImageMask true", [0x80]),
            EvidencePdf.Stream("<< /Type /EmbeddedFile /Subtype /text#2Fplain", Encoding.ASCII.GetBytes("attachment")),
            EvidencePdf.Stream("<< /Subtype /3D", [7, 8, 9])
        ]);

        PdfParseResult first = PdfParser.Parse(pdf);
        PdfParseResult second = PdfParser.Parse(pdf);

        Assert.HasCount(4, first.Evidence.Assets);
        Assert.HasCount(2, first.Evidence.Assets.Where(asset => asset.Kind == "image"));
        Assert.Contains("embedded-file", first.Evidence.Assets.Select(asset => asset.Kind).ToArray());
        Assert.Contains("media", first.Evidence.Assets.Select(asset => asset.Kind).ToArray());
        CollectionAssert.AreEqual(first.Evidence.Assets.Select(asset => asset.StableId).ToArray(), second.Evidence.Assets.Select(asset => asset.StableId).ToArray());
        Assert.AreEqual("image/jpeg", first.Evidence.Assets.First(asset => asset.ObjectId.Number == 3).MediaType);
    }

    [TestMethod]
    public void Parse_ActionsAndRichMediaAnnotation_InventoryNeverAuthorisesExecutionOrRetrieval()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R /OpenAction 3 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Object("<< /S /JavaScript /JS (app.alert('never')) >>"),
            EvidencePdf.Object("<< /S /URI /URI (https://invalid.example/evidence) >>"),
            EvidencePdf.Object("<< /S /Launch /F (never.exe) >>"),
            EvidencePdf.Object("<< /Type /Annot /Subtype /RichMedia /Rect [0 0 10 10] /Contents (Passive only) >>")
        ]);

        PdfParseResult result = PdfParser.Parse(pdf);
        PdfEvidenceItem[] actions = result.Evidence.Items.Where(item => item.Kind == "action").ToArray();

        Assert.HasCount(3, actions);
        Assert.IsTrue(actions.All(action => action.Properties["Execution"] == "disabled"));
        Assert.IsTrue(actions.All(action => action.Properties["Retrieval"] == "disabled"));
        Assert.AreEqual("RichMedia", Assert.ContainsSingle(result.Evidence.Items.Where(item => item.Kind == "annotation")).Subtype);
    }

    [TestMethod]
    public void Parse_InheritedResourcesAndFormXObject_ExtractsNestedText()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 1 /Kids [3 0 R] /Resources << /Font << /F1 6 0 R >> /XObject << /Fm1 4 0 R >> >> >>"),
            EvidencePdf.Object("<< /Type /Page /Parent 2 0 R /Contents 5 0 R >>"),
            EvidencePdf.Stream("<< /Type /XObject /Subtype /Form /BBox [0 0 100 100]", Encoding.ASCII.GetBytes("BT /F1 12 Tf 10 20 Td (Inside form) Tj ET")),
            EvidencePdf.Stream("<<", Encoding.ASCII.GetBytes("/Fm1 Do")),
            EvidencePdf.Object("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>")
        ]);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual("Inside form", Assert.ContainsSingle(result.TextRuns).Text);
        Assert.DoesNotContain("PDF_XOBJECT_NOT_INTERPRETED", result.Issues.Select(issue => issue.Code).ToArray());
        Assert.Contains("Form", result.Evidence.Items.Where(item => item.Kind == "xobject").Select(item => item.Subtype).ToArray());
    }

    [TestMethod]
    public void Parse_PostScriptAndUnknownXObjects_RetainsPassiveBytesWithoutExecution()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"),
            EvidencePdf.Object("<< /Type /Page /Parent 2 0 R /Resources << /XObject << /Ps 5 0 R /Future 6 0 R >> >> /Contents 4 0 R >>"),
            EvidencePdf.Stream("<<", Encoding.ASCII.GetBytes("/Ps Do /Future Do")),
            EvidencePdf.Stream("<< /Type /XObject /Subtype /PS", Encoding.ASCII.GetBytes("passive-program")),
            EvidencePdf.Stream("<< /Type /XObject /Subtype /FutureType", [1, 2, 3])
        ]);

        PdfParseResult first = PdfParser.Parse(pdf);
        PdfParseResult second = PdfParser.Parse(pdf);

        Assert.AreEqual(PdfParseOutcome.Complete, first.Outcome);
        Assert.HasCount(2, first.Evidence.Assets.Where(asset => asset.Kind == "xobject"));
        Assert.Contains("PS", first.Evidence.Items.Where(item => item.Kind == "xobject").Select(item => item.Subtype).ToArray());
        Assert.Contains("FutureType", first.Evidence.Items.Where(item => item.Kind == "xobject").Select(item => item.Subtype).ToArray());
        Assert.IsTrue(first.Evidence.Assets.Where(asset => asset.Kind == "xobject").All(asset => asset.Properties["Execution"] == "disabled"));
        CollectionAssert.AreEqual(
            first.Evidence.Assets.Where(asset => asset.Kind == "xobject").Select(asset => asset.StableId).ToArray(),
            second.Evidence.Assets.Where(asset => asset.Kind == "xobject").Select(asset => asset.StableId).ToArray());
        Assert.DoesNotContain("PDF_XOBJECT_NOT_INTERPRETED", first.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void Parse_SelfReferentialForm_ReportsCycleAndHonoursCumulativeOperatorBudget()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"),
            EvidencePdf.Object("<< /Type /Page /Parent 2 0 R /Resources << /XObject << /Fm 5 0 R >> >> /Contents 4 0 R >>"),
            EvidencePdf.Stream("<<", Encoding.ASCII.GetBytes("/Fm Do")),
            EvidencePdf.Stream("<< /Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /XObject << /Fm 5 0 R >> >>", Encoding.ASCII.GetBytes("/Fm Do"))
        ]);

        PdfParseResult cycle = PdfParser.Parse(pdf);
        PdfParseResult limited = PdfParser.Parse(pdf, new PdfLimits { MaxOperators = 1 });

        Assert.AreEqual(PdfParseOutcome.Partial, cycle.Outcome);
        Assert.Contains("PDF_FORM_CYCLE", cycle.Issues.Select(issue => issue.Code).ToArray());
        Assert.AreEqual(PdfParseOutcome.ResourceLimitExceeded, limited.Outcome);
        Assert.Contains("PDF_OPERATOR_LIMIT", limited.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void Parse_MarkedContent_InventoriesTagsMcidAndActualText()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"),
            EvidencePdf.Object("<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>"),
            EvidencePdf.Stream("<<", Encoding.ASCII.GetBytes("/P << /MCID 0 /ActualText (Replacement) >> BDC BT (Visible) Tj ET EMC /Artifact BMC EMC"))
        ]);

        PdfParseResult result = PdfParser.Parse(pdf);
        PdfEvidenceItem[] marked = result.Evidence.Items.Where(item => item.Kind == "marked-content").ToArray();

        Assert.HasCount(2, marked);
        PdfEvidenceItem paragraph = marked.First(item => item.Subtype == "P");
        Assert.AreEqual("0", paragraph.Properties["MCID"]);
        Assert.AreEqual("Replacement", paragraph.Properties["ActualText"]);
        Assert.Contains("Artifact", marked.Select(item => item.Subtype).ToArray());
    }

    [TestMethod]
    public void Parse_Signature_ReportsByteRangeStructureWithoutTrustValidation()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Object("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 10 20 30] /Contents <01020304> >>"),
            EvidencePdf.Object("<< /FT /Sig /ByteRange [0 -1] >>")
        ]);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.HasCount(2, result.Evidence.Signatures);
        PdfSignatureEvidence signature = result.Evidence.Signatures[0];
        Assert.IsTrue(signature.ByteRangeStructurallyValid);
        Assert.IsFalse(signature.CoversWholeInput);
        Assert.AreEqual(4, signature.SignatureByteCount);
        Assert.IsFalse(result.Evidence.Signatures[1].ByteRangeStructurallyValid);
        Assert.AreEqual("false", result.Evidence.Items.First(item => item.Kind == "signature").Properties["CryptographicTrustValidated"]);
    }

    [TestMethod]
    public void Parse_SignatureExactContentsGap_ReportsWholeInputCoverageWithoutTrustClaim()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Object("<< /Type /Sig /ByteRange [0000000000 0000000000 0000000000 0000000000] /Contents <01020304> >>")
        ]);
        string text = Encoding.ASCII.GetString(pdf);
        int contentsStart = text.IndexOf("<01020304>", StringComparison.Ordinal);
        int contentsEnd = contentsStart + 10;
        string replacement = $"{0:D10} {contentsStart:D10} {contentsEnd:D10} {pdf.Length - contentsEnd:D10}";
        int rangeStart = text.IndexOf("0000000000 0000000000 0000000000 0000000000", StringComparison.Ordinal);
        Encoding.ASCII.GetBytes(replacement).CopyTo(pdf, rangeStart);

        PdfSignatureEvidence signature = Assert.ContainsSingle(PdfParser.Parse(pdf).Evidence.Signatures);

        Assert.IsTrue(signature.ByteRangeStructurallyValid);
        Assert.IsTrue(signature.CoversWholeInput);
    }

    [TestMethod]
    public void Parse_UnsupportedMetadataFilter_PreservesEncodedFallbackAndStableProvenance()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Stream("<< /Type /Metadata /Subtype /XML /Filter /DCTDecode", [1, 2, 3])
        ]);

        PdfPassiveAsset asset = Assert.ContainsSingle(PdfParser.Parse(pdf).Evidence.Assets);

        Assert.AreEqual("True", asset.Properties["EncodedFallback"]);
        Assert.AreEqual("authoritative-current", asset.Properties["Revision"]);
        Assert.Contains("-r-current-3-0-o0-", asset.StableId);
    }

    [TestMethod]
    public void Parse_PublicKeyEncryption_ClassifiesWithoutDecrypting()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Object("<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s5 /V 4 >>")
        ], encryptObject: 3);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.AreEqual(PdfParseOutcome.Encrypted, result.Outcome);
        Assert.IsNotNull(result.Evidence.Encryption);
        Assert.AreEqual("Adobe.PubSec", result.Evidence.Encryption.Handler);
        Assert.IsTrue(result.Evidence.Encryption.IsPublicKeyHandler);
        Assert.AreEqual(4, result.Evidence.Encryption.Version);
        Assert.IsEmpty(result.Evidence.Items);
        Assert.IsEmpty(result.Evidence.Assets);
    }

    [TestMethod]
    public void Parse_EvidenceLimitExceeded_ReturnsResourceLimitOutcome()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Object("<< /Title (one) >>"),
            EvidencePdf.Object("<< /Author (two) >>")
        ]);

        PdfParseResult result = PdfParser.Parse(pdf, new PdfLimits { MaxEvidenceItems = 1 });

        Assert.AreEqual(PdfParseOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.Contains("PDF_EVIDENCE_LIMIT", result.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void Parse_AssetByteLimitExceeded_ReturnsResourceLimitWithoutPartialAssetSet()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Stream("<< /Type /EmbeddedFile", [1, 2, 3, 4])
        ]);

        PdfParseResult result = PdfParser.Parse(pdf, new PdfLimits { MaxAssetBytes = 3 });

        Assert.AreEqual(PdfParseOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.Contains("PDF_ASSET_LIMIT", result.Issues.Select(issue => issue.Code).ToArray());
        Assert.IsEmpty(result.Evidence.Assets);
    }

    [TestMethod]
    public void Parse_InvalidXmp_ReportsIssueWithoutProfileValidationClaim()
    {
        byte[] pdf = EvidencePdf.Create(
        [
            EvidencePdf.Object("<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R >>"),
            EvidencePdf.Object("<< /Type /Pages /Count 0 /Kids [] >>"),
            EvidencePdf.Stream("<< /Type /Metadata /Subtype /XML", Encoding.UTF8.GetBytes("<xmp><broken></xmp>"))
        ]);

        PdfParseResult result = PdfParser.Parse(pdf);

        Assert.Contains("PDF_XMP_INVALID", result.Issues.Select(issue => issue.Code).ToArray());
        Assert.IsEmpty(result.Evidence.Items.Where(item => item.Kind == "profile-claim"));
    }

    [TestMethod]
    public void Decode_CancelledToken_StopsBeforeDecoderLoop()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var dictionary = new PdfDictionary(new Dictionary<string, PdfValue> { ["Filter"] = new PdfName("RunLengthDecode", new(0, 1)) }, [], new(0, 1));
        var stream = new PdfStream(dictionary, [0, 1, 128], new(0, 3));

        Assert.ThrowsExactly<OperationCanceledException>(() => PdfStreamDecoder.Decode(stream, cancellationToken: source.Token));
    }

    private static class EvidencePdf
    {
        internal readonly record struct Spec(string Dictionary, byte[]? StreamBytes);

        public static Spec Object(string dictionary) => new(dictionary, null);
        public static Spec Stream(string dictionaryWithoutClose, byte[] bytes) => new(dictionaryWithoutClose, bytes);

        public static byte[] Create(IReadOnlyList<Spec> objects, int? infoObject = null, int? encryptObject = null)
        {
            var bytes = new List<byte>();
            var offsets = new List<int> { 0 };
            Add(bytes, "%PDF-1.7\n%\xE2\xE3\xCF\xD3\n");
            for (int index = 0; index < objects.Count; index++)
            {
                Spec spec = objects[index];
                int number = index + 1;
                offsets.Add(bytes.Count);
                Add(bytes, $"{number} 0 obj\n");
                if (spec.StreamBytes is null) Add(bytes, spec.Dictionary);
                else
                {
                    Add(bytes, $"{spec.Dictionary} /Length {spec.StreamBytes.Length} >>\nstream\n");
                    bytes.AddRange(spec.StreamBytes);
                    Add(bytes, "\nendstream");
                }
                Add(bytes, "\nendobj\n");
            }
            int xref = bytes.Count;
            Add(bytes, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
            for (int index = 1; index < offsets.Count; index++) Add(bytes, $"{offsets[index]:D10} 00000 n \n");
            string info = infoObject is null ? string.Empty : $" /Info {infoObject} 0 R";
            string encrypt = encryptObject is null ? string.Empty : $" /Encrypt {encryptObject} 0 R";
            Add(bytes, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R{info}{encrypt} >>\nstartxref\n{xref}\n%%EOF\n");
            return bytes.ToArray();
        }

        private static void Add(List<byte> bytes, string value) => bytes.AddRange(Encoding.Latin1.GetBytes(value));
    }
}
