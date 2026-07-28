using System.Text;
using CollisionDocNet.Core;
using CollisionDocNet.Extraction;
using CollisionDocNet.Model;

namespace CollisionDocNet.Security.Tests;

[TestClass]
public sealed class ResourceAndXmlSecurityTests
{
    private readonly TestContext _testContext;

    public ResourceAndXmlSecurityTests(TestContext testContext) => _testContext = testContext;

    [TestMethod]
    public async Task ExtractAsync_DocxDocumentTypeAndExternalEntity_IsRejectedWithoutExpansion()
    {
        const string secret = "ENTITY_MUST_NOT_EXPAND_6F62A12A";
        byte[] source = SyntheticDocuments.Docx(
            "<?xml version=\"1.0\"?><!DOCTYPE w:document [<!ENTITY xxe SYSTEM \"file:///does-not-exist\">]>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>&xxe;</w:t></w:r></w:p></w:body></w:document>");

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-dtd", "dtd.docx");

        Assert.AreEqual(DetectedFormat.WordprocessingMl, result.DetectedFormat);
        Assert.DoesNotContain(ExtractionOutcome.Complete, new[] { result.Outcome });
        Assert.IsFalse(result.Content.Any(item => item.Text.Contains(secret, StringComparison.Ordinal)));
        Assert.IsTrue(result.Issues.Any(static issue =>
            issue.Code.Contains("DTD", StringComparison.Ordinal) ||
            issue.Code.Contains("XML", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExtractAsync_ZipTraversalEntry_IsRejectedBeforePartExposure()
    {
        byte[] source = SyntheticDocuments.Docx(
            "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>",
            [("../escape.txt", "must not escape")]);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-traversal", "traversal.docx");

        Assert.AreEqual(DetectedContainer.ZipPackage, result.DetectedContainer);
        Assert.DoesNotContain(ExtractionOutcome.Complete, new[] { result.Outcome });
        Assert.IsEmpty(result.Assets);
    }

    [TestMethod]
    public async Task ExtractAsync_CompressedExpansionOverBudget_ReturnsBoundedOutcome()
    {
        string oversizedText = new('A', 256 * 1024);
        byte[] source = SyntheticDocuments.MinimalDocx(oversizedText);
        ExtractionPolicy policy = Policy(maxDecodedBytes: 8 * 1024, maxTextCharacters: 4 * 1024);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-zip-bomb", "expanded.docx", policy: policy);

        Assert.DoesNotContain(ExtractionOutcome.Complete, new[] { result.Outcome });
        Assert.DoesNotContain(ExtractionOutcome.TechnicalFailure, new[] { result.Outcome });
        Assert.IsTrue(result.Outcome is ExtractionOutcome.ResourceLimitExceeded or ExtractionOutcome.Corrupt);
        Assert.IsInRange(
            0,
            policy.Limits.MaxTextCharacters,
            result.Measurements.TextCharacters);
    }

    [TestMethod]
    public async Task ExtractAsync_DeeplyNestedMime_ReturnsDepthLimitWithoutSilentTruncation()
    {
        string nested = "From: nested@example.test\r\nSubject: leaf\r\nContent-Type: text/plain\r\n\r\nterminal";
        for (int depth = 0; depth < 8; depth++)
        {
            nested = $"From: nested-{depth}@example.test\r\nSubject: level {depth}\r\nContent-Type: message/rfc822\r\n\r\n" + nested;
        }
        ExtractionPolicy policy = Policy(maxNestingDepth: 2);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(
            Encoding.ASCII.GetBytes(nested), "security-nesting", "nested.eml", policy: policy);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.IsTrue(result.Issues.Any(static issue =>
            issue.Code is "EML_LIMIT_DEPTH" or "EML_NESTED_TERMINAL" or "CUMULATIVE_RESOURCE_LIMIT"));
        Assert.IsLessThan(8, result.Measurements.MaximumNestingDepth);
    }

    [TestMethod]
    public async Task ExtractAsync_DocxObjectCountOverBudget_ReturnsResourceLimit()
    {
        var entries = Enumerable.Range(0, 32)
            .Select(static index => ($"word/media/item-{index:D2}.bin", "passive"));
        byte[] source = SyntheticDocuments.Docx(
            "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>",
            entries);
        ExtractionPolicy policy = Policy(maxObjects: 8);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-objects", "objects.docx", policy: policy);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.IsNotEmpty(result.Issues);
    }

    [TestMethod]
    public async Task ExtractAsync_InputOneByteOverLimit_ReturnsResourceLimitWithoutParsing()
    {
        byte[] source = SyntheticDocuments.Eml("bounded");
        ExtractionPolicy policy = Policy(maxInputBytes: source.Length - 1);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-input", "bounded.eml", policy: policy);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.Contains("INPUT_LIMIT", result.Issues.Select(static issue => issue.Code));
        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public async Task ExtractAsync_BlockedCallerStreamDeadline_ReturnsTimedOutAndStopsRead()
    {
        ExtractionPolicy policy = Policy(maxElapsed: TimeSpan.FromMilliseconds(20));
        await using var stream = new BlockingStream();
        var request = new ExtractionRequest(ExtractionInput.FromStream(stream), "security-timeout", "blocked.eml", null, policy);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(request, _testContext.CancellationToken);

        Assert.AreEqual(ExtractionOutcome.TimedOut, result.Outcome);
        Assert.Contains("EXTRACTION_TIMED_OUT", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_CallerStreamIoFailure_ReturnsContentFreeTechnicalFailure()
    {
        await using var stream = new ThrowingStream();
        var request = new ExtractionRequest(
            ExtractionInput.FromStream(stream), "security-stream-failure", "sensitive-input.eml", null,
            ExtractionPolicy.CreateDefault());

        ExtractionResult result = await DocumentExtractor.ExtractAsync(request);

        Assert.AreEqual(ExtractionOutcome.TechnicalFailure, result.Outcome);
        ExtractionIssue issue = Assert.ContainsSingle(result.Issues);
        Assert.AreEqual("INPUT_READ_FAILED", issue.Code);
        Assert.DoesNotContain("sensitive-input.eml", issue.Message);
    }

    [TestMethod]
    [DataRow("pdf", "hostile.pdf")]
    [DataRow("doc", "hostile.doc")]
    [DataRow("docx", "hostile.docx")]
    [DataRow("msg", "hostile.msg")]
    [DataRow("eml", "hostile.eml")]
    public async Task ExtractAsync_PreCancelledFiveFormatCandidate_ReturnsCancelled(string format, string fileName)
    {
        byte[] source = format switch
        {
            "pdf" => SyntheticDocuments.Pdf(string.Empty),
            "doc" => SyntheticDocuments.CompoundSignature("WordDocument"),
            "docx" => SyntheticDocuments.MinimalDocx(),
            "msg" => SyntheticDocuments.CompoundSignature("__properties_version1.0"),
            "eml" => SyntheticDocuments.Eml("body", "text/plain"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-cancelled", fileName, cancellationToken: cancellation.Token);

        Assert.AreEqual(ExtractionOutcome.Cancelled, result.Outcome);
        Assert.Contains("EXTRACTION_CANCELLED", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_HostileContent_IsNotEchoedInDiagnosticIssues()
    {
        const string secret = "SECRET_DIAGNOSTIC_CANARY_52D884";
        byte[] source = Encoding.ASCII.GetBytes($"%PDF-2.0\n{secret}\ninvalid");

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-content-free", "sensitive-name.pdf");

        Assert.IsFalse(result.Issues.Any(issue => issue.Message.Contains(secret, StringComparison.Ordinal)));
        Assert.IsFalse(result.Issues.Any(static issue => issue.Message.Contains("sensitive-name.pdf", StringComparison.Ordinal)));
    }

    private static ExtractionPolicy Policy(
        long? maxInputBytes = null,
        long? maxDecodedBytes = null,
        int? maxObjects = null,
        int? maxTextCharacters = null,
        int? maxNestingDepth = null,
        TimeSpan? maxElapsed = null)
    {
        ResourceLimits defaults = ResourceLimits.CreateCollisionSpikeDefault();
        var limits = new ResourceLimits(
            "security-tests/1",
            maxInputBytes ?? defaults.MaxInputBytes,
            maxDecodedBytes ?? defaults.MaxDecodedBytes,
            maxObjects ?? defaults.MaxObjects,
            maxTextCharacters ?? defaults.MaxTextCharacters,
            defaults.MaxAssets,
            defaults.MaxAssetBytes,
            maxNestingDepth ?? defaults.MaxNestingDepth,
            maxElapsed ?? defaults.MaxElapsed);
        return new("security-tests/1", DeterministicText.PolicyId, StableIdentity.PolicyId, limits);
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
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("synthetic sensitive detail");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("synthetic sensitive detail"));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
