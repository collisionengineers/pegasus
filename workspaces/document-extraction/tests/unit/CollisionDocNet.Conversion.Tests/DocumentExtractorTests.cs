using System.IO.Compression;
using System.Text;
using CollisionDocNet.Core;
using CollisionDocNet.Extraction;
using CollisionDocNet.Model;

namespace CollisionDocNet.Extraction.Tests;

[TestClass]
public sealed class DocumentExtractorTests
{
    private readonly TestContext _testContext;

    public DocumentExtractorTests(TestContext testContext) => _testContext = testContext;

    [TestMethod]
    [DataRow("CollisionDocNet.Pdf.PdfParser, CollisionDocNet.Pdf")]
    [DataRow("CollisionDocNet.Writer.WordBinaryExtractor, CollisionDocNet.Writer")]
    [DataRow("CollisionDocNet.Writer.OpenXml.DocxExtractor, CollisionDocNet.Writer.OpenXml")]
    [DataRow("CollisionDocNet.Outlook.MsgReader, CollisionDocNet.Outlook")]
    [DataRow("CollisionDocNet.Email.EmlExtractor, CollisionDocNet.Email")]
    public void FormatHandlerEntryPoint_IsNotPublic(string assemblyQualifiedName)
    {
        Type handler = Type.GetType(assemblyQualifiedName, throwOnError: true)!;

        Assert.IsFalse(handler.IsPublic);
    }

    [TestMethod]
    public async Task ExtractAsync_ByteAndStreamInputs_ProduceEquivalentCanonicalResults()
    {
        byte[] source = Message();
        ExtractionPolicy policy = ExtractionPolicy.CreateDefault();
        var bytesRequest = new ExtractionRequest(ExtractionInput.FromBytes(source), "source-1", "message.eml", "message/rfc822", policy);
        using var stream = new MemoryStream(source, writable: false);
        var streamRequest = new ExtractionRequest(ExtractionInput.FromStream(stream), "source-1", "message.eml", "message/rfc822", policy);

        ExtractionResult fromBytes = await DocumentExtractor.ExtractAsync(bytesRequest);
        ExtractionResult fromStream = await DocumentExtractor.ExtractAsync(streamRequest);

        CollectionAssert.AreEqual(ExtractionResultJson.SerializeToUtf8Bytes(fromBytes), ExtractionResultJson.SerializeToUtf8Bytes(fromStream));
        Assert.AreEqual(ExtractionOutcome.Complete, fromBytes.Outcome);
        Assert.AreEqual(DetectedFormat.InternetMessage, fromBytes.DetectedFormat);
        Assert.AreEqual(ExtractionPolicy.DefaultPolicyId, fromBytes.PolicyIdentity);
    }

    [TestMethod]
    public async Task ExtractAsync_MislabeledInput_UsesBytesAndReportsMismatch()
    {
        ExtractionResult result = await DocumentExtractor.ExtractAsync(Message(), "source-2", "fake.pdf", "application/pdf");

        Assert.AreEqual(DetectedFormat.InternetMessage, result.DetectedFormat);
        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.HasCount(2, result.Issues.Where(static issue => issue.Code.EndsWith("MISMATCH", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExtractAsync_CorruptPdfSignature_PreservesDetectedFormat()
    {
        ExtractionResult result = await DocumentExtractor.ExtractAsync("%PDF-2.0\ncorrupt"u8.ToArray(), "source-pdf", "evidence.pdf");

        Assert.AreEqual(DetectedContainer.FlatBinary, result.DetectedContainer);
        Assert.AreEqual(DetectedFormat.Pdf, result.DetectedFormat);
        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
    }

    [TestMethod]
    public async Task ExtractAsync_CorruptCompoundSignature_PreservesContainer()
    {
        byte[] source = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1, 0, 0, 0, 0];

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "source-cfb", "evidence.doc");

        Assert.AreEqual(DetectedContainer.CompoundFile, result.DetectedContainer);
        Assert.AreEqual(DetectedFormat.Unknown, result.DetectedFormat);
        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
        Assert.Contains("CFB_STRUCTURE_INVALID", result.Issues.Select(static issue => issue.Code));
        Assert.DoesNotContain("FILENAME_HINT_MISMATCH", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_PreCancelledRequest_ReturnsCancelledResult()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ExtractionResult result = await DocumentExtractor.ExtractAsync("%PDF-1.7"u8.ToArray(), "source-3", "a.pdf", cancellationToken: cancellation.Token);

        Assert.AreEqual(ExtractionOutcome.Cancelled, result.Outcome);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public async Task ExtractAsync_BlockedStreamDeadline_ReturnsTimedOut()
    {
        ExtractionPolicy policy = Policy(maxElapsed: TimeSpan.FromMilliseconds(20));
        await using var stream = new BlockingStream();
        var request = new ExtractionRequest(ExtractionInput.FromStream(stream), "source-timeout", "message.eml", null, policy);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(request, _testContext.CancellationToken);

        Assert.AreEqual(ExtractionOutcome.TimedOut, result.Outcome);
        Assert.Contains("EXTRACTION_TIMED_OUT", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_CallerStreamThrowsRecoverableException_ReturnsTechnicalFailure()
    {
        await using var stream = new ThrowingStream();
        var request = new ExtractionRequest(ExtractionInput.FromStream(stream), "throwing-stream", "message.eml", null,
            ExtractionPolicy.CreateDefault());

        ExtractionResult result = await DocumentExtractor.ExtractAsync(request);

        Assert.AreEqual(ExtractionOutcome.TechnicalFailure, result.Outcome);
        Assert.Contains("INPUT_READ_FAILED", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_InputAtLimit_SucceedsAndOneByteOverFails()
    {
        byte[] source = Message();
        ExtractionPolicy exact = Policy(maxInputBytes: source.Length);
        ExtractionPolicy lower = Policy(maxInputBytes: source.Length - 1);

        ExtractionResult accepted = await DocumentExtractor.ExtractAsync(source, "exact", "message.eml", policy: exact);
        ExtractionResult rejected = await DocumentExtractor.ExtractAsync(source, "over", "message.eml", policy: lower);

        Assert.AreEqual(ExtractionOutcome.Complete, accepted.Outcome);
        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, rejected.Outcome);
    }

    [TestMethod]
    public async Task ExtractAsync_TextLimit_ReconcilesHandlerOutputAsResourceLimit()
    {
        ExtractionPolicy policy = Policy(maxTextCharacters: 3);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(Message(), "text-limit", "message.eml", policy: policy);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome,
            string.Join(',', result.Issues.Select(static issue => issue.Code)));
    }

    [TestMethod]
    public async Task ExtractAsync_MinimalDocx_DispatchesOpenXmlHandler()
    {
        byte[] source = MinimalDocx();

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "docx", "evidence.docx");

        Assert.AreEqual(DetectedContainer.ZipPackage, result.DetectedContainer);
        Assert.AreEqual(DetectedFormat.WordprocessingMl, result.DetectedFormat);
        Assert.ContainsSingle(result.Content.Where(static item => item.Text == "evidence"));
    }

    [TestMethod]
    public async Task ExtractAsync_Pdf_DispatchesTextAndEncryptionEvidence()
    {
        ExtractionResult plain = await DocumentExtractor.ExtractAsync(MinimalPdf("BT /F1 12 Tf (evidence) Tj ET"), "pdf", "evidence.pdf");
        ExtractionResult encrypted = await DocumentExtractor.ExtractAsync(MinimalPdf(string.Empty, encrypted: true), "pdf-encrypted", "protected.pdf");

        Assert.AreEqual(DetectedFormat.Pdf, plain.DetectedFormat);
        Assert.AreEqual(ExtractionOutcome.Complete, plain.Outcome);
        Assert.ContainsSingle(plain.Content.Where(static item => item.Text == "evidence"));
        Assert.AreEqual(ExtractionOutcome.Encrypted, encrypted.Outcome);
        Assert.ContainsSingle(encrypted.Metadata.Where(static item => item.Name == "pdf.encryption"));
    }

    [TestMethod]
    public async Task ExtractAsync_SameInputTwice_IsDeterministic()
    {
        byte[] source = Message();

        ExtractionResult first = await DocumentExtractor.ExtractAsync(source, "source-5", "message.eml");
        ExtractionResult second = await DocumentExtractor.ExtractAsync(source, "source-5", "message.eml");

        CollectionAssert.AreEqual(ExtractionResultJson.SerializeToUtf8Bytes(first), ExtractionResultJson.SerializeToUtf8Bytes(second));
    }

    [TestMethod]
    public async Task ExtractAsync_SupportedAttachment_ProducesLinkedNestedResultWithOccurrenceEvidence()
    {
        byte[] pdf = MinimalPdf("BT /F1 12 Tf (nested evidence) Tj ET");
        byte[] source = MultipartMessage(("application/pdf", "evidence.pdf", pdf));

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "nest-root", "message.eml");

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        ExtractionResult child = Assert.ContainsSingle(result.NestedResults);
        Assert.AreEqual(DetectedFormat.Pdf, child.DetectedFormat);
        Assert.IsEmpty(result.Assets);
        string parentAssetId = Assert.ContainsSingle(child.Metadata.Where(static item => item.Name == "parentAssetStableId")).Value;
        Assert.ContainsSingle(child.Metadata.Where(item => item.Name == "embeddedContentHash" && item.Value == child.SourceHash.Hex));
        Assert.ContainsSingle(child.Metadata.Where(static item => item.Name == "nestingPath"));
        Assert.ContainsSingle(result.Relationships.Where(item => item.Kind == "nested-extraction" && item.SourceIdentity == parentAssetId));
        Assert.AreEqual(1, result.Measurements.MaximumNestingDepth);
    }

    [TestMethod]
    public async Task ExtractAsync_DuplicateAttachmentBytes_RemainDistinctStableOccurrences()
    {
        byte[] childMessage = Message();
        byte[] source = MultipartMessage(
            ("application/octet-stream", "first.bin", childMessage),
            ("application/octet-stream", "second.bin", childMessage));

        ExtractionResult first = await DocumentExtractor.ExtractAsync(source, "duplicate-root", "message.eml");
        ExtractionResult second = await DocumentExtractor.ExtractAsync(source, "duplicate-root", "message.eml");

        Assert.HasCount(2, first.NestedResults);
        Assert.AreEqual(first.NestedResults[0].SourceHash, first.NestedResults[1].SourceHash);
        string[] identities = first.NestedResults.Select(static child =>
            Assert.ContainsSingle(child.Metadata.Where(static item => item.Name == "sourceIdentity")).Value).ToArray();
        Assert.AreNotEqual(identities[0], identities[1]);
        Assert.HasCount(2, identities.Distinct(StringComparer.Ordinal));
        CollectionAssert.AreEqual(ExtractionResultJson.SerializeToUtf8Bytes(first), ExtractionResultJson.SerializeToUtf8Bytes(second));
    }

    [TestMethod]
    public async Task ExtractAsync_UnsupportedAttachment_RemainsHashedInformationalEvidence()
    {
        byte[] source = MultipartMessage(("application/octet-stream", "evidence.bin", [1, 2, 3, 4]));

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "unsupported-root", "message.eml");

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.IsEmpty(result.Assets);
        Assert.ContainsSingle(result.Metadata.Where(item => item.Name == "nonPayload.binary" &&
            item.Value.Contains(Sha256Digest.Compute([1, 2, 3, 4]).Hex, StringComparison.Ordinal)));
        Assert.IsEmpty(result.NestedResults);
        ExtractionIssue issue = Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "NESTED_FORMAT_UNSUPPORTED"));
        Assert.AreEqual(ExtractionIssueSeverity.Information, issue.Severity);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "NON_IMAGE_ASSET_NOT_EMITTED"));
    }

    [TestMethod]
    public async Task ExtractAsync_TopLevelInputLimit_DoesNotDoubleChargeSupportedNestedBytes()
    {
        byte[] childMessage = Message();
        byte[] source = MultipartMessage(("application/octet-stream", "child.bin", childMessage));
        ExtractionPolicy policy = Policy(maxInputBytes: source.Length);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "limited-root", "message.eml", policy: policy);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.ContainsSingle(result.NestedResults);
        Assert.DoesNotContain("NESTED_INPUT_LIMIT", result.Issues.Select(static issue => issue.Code));
        Assert.AreEqual(source.Length, result.Measurements.InputBytes);
    }

    [TestMethod]
    public async Task ExtractAsync_CumulativeNestedTextLimit_PropagatesChildTerminalOutcome()
    {
        byte[] source = MultipartMessage(("application/octet-stream", "child.bin", Message()));
        ExtractionPolicy policy = Policy(maxTextCharacters: 12);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "text-limited-root", "message.eml", policy: policy);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        ExtractionResult child = Assert.ContainsSingle(result.NestedResults);
        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, child.Outcome);
        Assert.Contains("NESTED_TERMINAL_OUTCOME", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_MixedFormatNesting_UsesOneBudgetAndPreservesDeterministicPaths()
    {
        byte[] pdf = MinimalPdf("BT /F1 12 Tf (deep evidence) Tj ET");
        byte[] inner = MultipartMessage(("application/pdf", "deep.pdf", pdf));
        byte[] outer = MultipartMessage(("application/octet-stream", "inner.bin", inner));

        ExtractionResult result = await DocumentExtractor.ExtractAsync(outer, "mixed-root", "outer.eml");

        ExtractionResult email = Assert.ContainsSingle(result.NestedResults);
        ExtractionResult nestedPdf = Assert.ContainsSingle(email.NestedResults);
        Assert.AreEqual(DetectedFormat.InternetMessage, email.DetectedFormat);
        Assert.AreEqual(DetectedFormat.Pdf, nestedPdf.DetectedFormat);
        Assert.AreEqual(2, result.Measurements.MaximumNestingDepth);
        string path = Assert.ContainsSingle(nestedPdf.Metadata.Where(static item => item.Name == "nestingPath")).Value;
        Assert.StartsWith("$/", path);
        Assert.AreEqual(2, path.Count(static character => character == '/'));
    }

    [TestMethod]
    public async Task ExtractAsync_EmbeddedSourceBeyondDepthLimit_IsNotEmittedAndPropagatesResourceLimit()
    {
        byte[] source = DocxWithEmbedding(Message());
        ExtractionPolicy policy = Policy(maxNestingDepth: 0);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "depth-root", "outer.docx", policy: policy);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome,
            string.Join(',', result.Issues.Select(static issue => issue.Code)));
        Assert.IsEmpty(result.Assets);
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "nonPayload.binary"));
        Assert.IsEmpty(result.NestedResults);
        Assert.Contains("NESTED_DEPTH_LIMIT", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_ImageAndBinaryAttachments_EmitsOnlyValidatedImageBytes()
    {
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZKXcAAAAASUVORK5CYII=");
        byte[] source = MultipartMessage(
            ("application/octet-stream", "evidence.bin", [1, 2, 3, 4]),
            ("application/octet-stream", "picture.dat", png));

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "image-root", "message.eml");

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        ReviewAsset image = Assert.ContainsSingle(result.Assets);
        Assert.AreEqual("image", image.Kind);
        Assert.AreEqual("image/png", image.MediaType);
        CollectionAssert.AreEqual(png, image.Content.ToArray());
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "nonPayload.binary"));
    }

    [TestMethod]
    public async Task ExtractAsync_ClaimedImageWithoutSupportedSignature_IsPartialAndNotEmitted()
    {
        byte[] source = MultipartMessage(("image/png", "broken.png", [1, 2, 3, 4]));

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "broken-image-root", "message.eml");

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.IsEmpty(result.Assets);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "IMAGE_ASSET_UNSUPPORTED_ENCODING"));
    }

    private static byte[] Message() => Encoding.ASCII.GetBytes(
        "From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: evidence\r\nContent-Type: text/plain\r\n\r\nbody\r\n");

    private static byte[] MultipartMessage(params (string MediaType, string Name, byte[] Content)[] attachments)
    {
        const string boundary = "collisiondocnet-boundary";
        var builder = new StringBuilder();
        builder.Append("From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: nested evidence\r\n")
            .Append("MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n\r\n")
            .Append("--").Append(boundary).Append("\r\nContent-Type: text/plain\r\n\r\nroot body\r\n");
        foreach ((string mediaType, string name, byte[] content) in attachments)
        {
            builder.Append("--").Append(boundary).Append("\r\nContent-Type: ").Append(mediaType)
                .Append("\r\nContent-Disposition: attachment; filename=\"").Append(name)
                .Append("\"\r\nContent-Transfer-Encoding: base64\r\n\r\n")
                .Append(Convert.ToBase64String(content)).Append("\r\n");
        }
        builder.Append("--").Append(boundary).Append("--\r\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static ExtractionPolicy Policy(long? maxInputBytes = null, int? maxTextCharacters = null,
        int? maxNestingDepth = null, TimeSpan? maxElapsed = null)
    {
        ResourceLimits defaults = ResourceLimits.CreateCollisionSpikeDefault();
        return new(ExtractionPolicy.DefaultPolicyId, DeterministicText.PolicyId, StableIdentity.PolicyId,
            new ResourceLimits(ResourceLimits.CollisionSpikeTenMegabytePolicy, maxInputBytes ?? defaults.MaxInputBytes,
                defaults.MaxDecodedBytes, defaults.MaxObjects, maxTextCharacters ?? defaults.MaxTextCharacters,
                defaults.MaxAssets, defaults.MaxAssetBytes, maxNestingDepth ?? defaults.MaxNestingDepth,
                maxElapsed ?? defaults.MaxElapsed));
    }

    private static byte[] MinimalDocx()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Write(archive, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Write(archive, "word/document.xml", "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>evidence</w:t></w:r></w:p></w:body></w:document>");
        }
        return stream.ToArray();
    }

    private static byte[] DocxWithEmbedding(byte[] embedded)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"bin\" ContentType=\"application/octet-stream\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Write(archive, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Write(archive, "word/document.xml", "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>outer</w:t></w:r></w:p></w:body></w:document>");
            Write(archive, "word/_rels/document.xml.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/package\" Target=\"embeddings/child.bin\"/></Relationships>");
            ZipArchiveEntry entry = archive.CreateEntry("word/embeddings/child.bin", CompressionLevel.NoCompression);
            using Stream output = entry.Open();
            output.Write(embedded);
        }
        return stream.ToArray();
    }

    private static byte[] MinimalPdf(string content, bool encrypted = false)
    {
        var bytes = new List<byte>();
        var offsets = new List<int> { 0 };
        AddPdf(bytes, "%PDF-1.7\n");
        AddPdfObject(bytes, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
        AddPdfObject(bytes, offsets, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        AddPdfObject(bytes, offsets, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
        int streamNumber = offsets.Count;
        offsets.Add(bytes.Count);
        AddPdf(bytes, $"{streamNumber} 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream\nendobj\n");
        AddPdfObject(bytes, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        if (encrypted) AddPdfObject(bytes, offsets, "<< /Filter /Standard /V 4 /R 4 >>");
        int xref = bytes.Count;
        AddPdf(bytes, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        for (int index = 1; index < offsets.Count; index++) AddPdf(bytes, $"{offsets[index]:D10} 00000 n \n");
        string encryption = encrypted ? $" /Encrypt {offsets.Count - 1} 0 R" : string.Empty;
        AddPdf(bytes, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R{encryption} >>\nstartxref\n{xref}\n%%EOF\n");
        return bytes.ToArray();
    }

    private static void AddPdfObject(List<byte> bytes, List<int> offsets, string body)
    {
        int number = offsets.Count;
        offsets.Add(bytes.Count);
        AddPdf(bytes, $"{number} 0 obj\n{body}\nendobj\n");
    }

    private static void AddPdf(List<byte> bytes, string text) => bytes.AddRange(Encoding.Latin1.GetBytes(text));

    private static void Write(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using Stream output = entry.Open();
        output.Write(Encoding.UTF8.GetBytes(text));
    }

    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("synthetic caller stream failure");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new InvalidOperationException("synthetic caller stream failure"));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
