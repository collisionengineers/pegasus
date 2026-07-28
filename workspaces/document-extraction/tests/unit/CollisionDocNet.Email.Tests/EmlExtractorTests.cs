using System.Text;
using CollisionDocNet.Core;
using CollisionDocNet.Model;

namespace CollisionDocNet.Email.Tests;

[TestClass]
public sealed class EmlExtractorTests
{
    [TestMethod]
    public void Extract_FoldedAndEncodedHeaders_PreservesOrderAndDecodesValues()
    {
        byte[] source = Bytes(
            "From: Sender <sender@example.test>\r\n" +
            "To: First <one@example.test>, Two <two@example.test>\r\n" +
            "Subject: =?UTF-8?B?SGVsbMOt?=\r\n" +
            "X-Trace: first\r\n\tsecond\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\nBody");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.HasCount(3, result.Participants);
        Assert.AreEqual("Hellí", result.Metadata.Single(entry => entry.Name == "header:subject").Value);
        Assert.AreEqual("first second", result.Metadata.Single(entry => entry.Name == "header:x-trace").Value);
        Assert.AreEqual("Body", Assert.ContainsSingle(result.Content).Text);
    }

    [TestMethod]
    public void Extract_MultipartWithQuotedPrintableTextAndBase64Attachment_ProjectsBoth()
    {
        byte[] source = Bytes(
            "MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=outer\r\n\r\n" +
            "preamble\r\n--outer\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nhello=20world\r\n" +
            "--outer\r\nContent-Type: application/octet-stream; name*=utf-8''evidence.bin\r\nContent-Disposition: attachment\r\nContent-Transfer-Encoding: base64\r\nContent-ID: <asset-1>\r\n\r\nAQID\r\n" +
            "--outer--\r\nepilogue");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.AreEqual("hello world", Assert.ContainsSingle(result.Content).Text);
        ReviewAsset asset = Assert.ContainsSingle(result.Assets);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, asset.Content.ToArray());
        Assert.AreEqual("evidence.bin", asset.OriginalName);
        _ = Assert.ContainsSingle(result.Relationships.Where(relationship => relationship.Kind == "content-id"));
    }

    [TestMethod]
    public void Extract_Html_SuppressesScriptAndRecordsPassiveRemoteReference()
    {
        byte[] source = Bytes(
            "Content-Type: text/html; charset=utf-8\r\n\r\n" +
            "<p>Hello &amp; goodbye</p><script>secret()</script><img src=\"https://invalid.test/x\">");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        string text = Assert.ContainsSingle(result.Content).Text;
        Assert.Contains("Hello & goodbye", text);
        Assert.DoesNotContain("secret", text);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_HTML_ACTIVE"));
        _ = Assert.ContainsSingle(result.Relationships.Where(relationship => relationship.Kind == "passive-external-reference"));
    }

    [TestMethod]
    public void Extract_HtmlActiveRemoteAndLocalReferences_ArePassiveEvidenceWithoutDowngrade()
    {
        byte[] source = Bytes(
            "Content-Type: text/html; charset=utf-8\r\n\r\n" +
            "<style>body{background:url(file:///redacted)}</style>" +
            "<iframe src=\"https://invalid.test/redacted\" onload=\"ignored()\"></iframe>" +
            "<p>Visible</p><script>ignored()</script>");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.AreEqual("Visible\n", Assert.ContainsSingle(result.Content).Text);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_HTML_ACTIVE"));
        _ = Assert.ContainsSingle(result.Relationships.Where(relationship => relationship.Kind == "passive-external-reference"));
        _ = Assert.ContainsSingle(result.Relationships.Where(relationship => relationship.Kind == "passive-local-reference"));
    }

    [TestMethod]
    [DataRow("<img src=\"file:///redacted\">")]
    [DataRow("<img src=\"\\\\host\\redacted\">")]
    [DataRow("<img src=\"C:\\redacted\">")]
    public void Extract_HtmlLocalReferenceVariants_AreRedactedPassiveEvidence(string tag)
    {
        ExtractionResult result = EmlExtractor.Extract(
            Bytes($"Content-Type: text/html; charset=utf-8\r\n\r\n{tag}<p>Visible</p>"),
            Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        _ = Assert.ContainsSingle(result.Relationships.Where(relationship => relationship.Kind == "passive-local-reference"));
    }

    [TestMethod]
    public void Extract_HtmlTagNamePrefix_IsNotMisclassifiedAsScript()
    {
        ExtractionResult result = EmlExtractor.Extract(
            Bytes("Content-Type: text/html; charset=utf-8\r\n\r\n<scripture>Retained</scripture>"),
            Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.AreEqual("Retained", Assert.ContainsSingle(result.Content).Text);
        Assert.IsEmpty(result.Issues.Where(issue => issue.Code == "EML_HTML_ACTIVE"));
    }

    [TestMethod]
    public void Extract_NestedMessage_ProducesNestedEvidence()
    {
        byte[] source = Bytes(
            "Content-Type: message/rfc822\r\n\r\n" +
            "From: nested@example.test\r\nContent-Type: text/plain\r\n\r\nNested body");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        ExtractionResult nested = Assert.ContainsSingle(result.NestedResults);
        Assert.AreEqual("Nested body", Assert.ContainsSingle(nested.Content).Text);
        _ = Assert.ContainsSingle(result.Relationships.Where(relationship => relationship.Kind == "nested-message"));
    }

    [TestMethod]
    public void Extract_NestedHtmlPassiveWarning_RemainsCompleteWithoutNestedIncompleteIssue()
    {
        byte[] source = Bytes(
            "Content-Type: message/rfc822\r\n\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n\r\n<p>Nested</p><script>ignored()</script>");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        ExtractionResult nested = Assert.ContainsSingle(result.NestedResults);
        Assert.AreEqual(ExtractionOutcome.Complete, nested.Outcome);
        _ = Assert.ContainsSingle(nested.Issues.Where(issue => issue.Code == "EML_HTML_ACTIVE"));
        Assert.IsEmpty(result.Issues.Where(issue => issue.Code == "EML_NESTED_INCOMPLETE"));
    }

    [TestMethod]
    public void Extract_NestedSemanticLoss_PropagatesPartialAndNestedIncompleteIssue()
    {
        byte[] source = Bytes(
            "Content-Type: message/rfc822\r\n\r\n" +
            "Content-Type: text/plain; format=flowed\r\n\r\nline ");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.AreEqual(ExtractionOutcome.Partial, Assert.ContainsSingle(result.NestedResults).Outcome);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_NESTED_INCOMPLETE"));
    }

    [TestMethod]
    public void Extract_EncryptedPayload_ReturnsEncryptedAndRetainsAsset()
    {
        byte[] source = Bytes("Content-Type: application/pkcs7-mime\r\n\r\nciphertext");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Encrypted, result.Outcome);
        Assert.HasCount(1, result.Assets);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_PROTECTED"));
    }

    [TestMethod]
    public void Extract_MissingHeaderSeparator_ReturnsCorrupt()
    {
        ExtractionResult result = EmlExtractor.Extract(Bytes("Subject: no body separator"), Limits());

        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_MISSING_SEPARATOR"));
    }

    [TestMethod]
    public void Extract_DuplicateSingletonAndMalformedQuotedPrintable_ReturnsPartialWithBothIssues()
    {
        byte[] source = Bytes(
            "Subject: first\r\nSubject: second\r\n" +
            "Content-Type: text/plain; charset=us-ascii\r\n" +
            "Content-Transfer-Encoding: quoted-printable\r\n\r\nbad=ZZ");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_HEADER_DUPLICATE"));
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_TRANSFER_MALFORMED"));
        Assert.AreEqual("bad=ZZ", Assert.ContainsSingle(result.Content).Text);
    }

    [TestMethod]
    public void Extract_MissingMultipartClose_ReturnsPartialWithoutDiscardingText()
    {
        byte[] source = Bytes(
            "Content-Type: multipart/mixed; boundary=x\r\n\r\n" +
            "--x\r\nContent-Type: text/plain\r\n\r\nretained");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.AreEqual("retained", Assert.ContainsSingle(result.Content).Text);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_BOUNDARY_UNCLOSED"));
    }

    [TestMethod]
    public void Extract_InputLimitExceeded_ReturnsExplicitOutcome()
    {
        ResourceLimits limits = Limits(maxInputBytes: 8);

        ExtractionResult result = EmlExtractor.Extract(Bytes("Subject: x\r\n\r\nbody"), limits);

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void Extract_PreCancelledToken_ReturnsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ExtractionResult result = EmlExtractor.Extract(Bytes("Subject: x\r\n\r\nbody"), Limits(), cancellationToken: cancellation.Token);

        Assert.AreEqual(ExtractionOutcome.Cancelled, result.Outcome);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public void Extract_CancellationDuringLargeBase64Scan_ReturnsCancelledWithoutDecodedEvidence()
    {
        byte[] prefix = Bytes("Content-Type: text/plain\r\nContent-Transfer-Encoding: base64\r\n\r\n");
        byte[] source = new byte[prefix.Length + (8 * 1024 * 1024)];
        prefix.CopyTo(source, 0);
        source.AsSpan(prefix.Length).Fill((byte)'A');
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        ExtractionResult result = EmlExtractor.Extract(
            source,
            Limits(maxInputBytes: source.Length, maxDecodedBytes: source.Length),
            cancellationToken: cancellation.Token);

        Assert.AreEqual(ExtractionOutcome.Cancelled, result.Outcome);
        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void Extract_ExpiredDeadline_RemainsTimedOutRatherThanBecomingCorrupt()
    {
        ExtractionResult result = EmlExtractor.Extract(
            Bytes("not-a-valid-header"),
            Limits(maxElapsed: TimeSpan.FromTicks(1)));

        Assert.AreEqual(ExtractionOutcome.TimedOut, result.Outcome);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_TIMED_OUT"));
    }

    [TestMethod]
    public void Extract_SameBytesTwice_ProducesStableProjection()
    {
        byte[] source = Bytes(
            "Content-Type: application/octet-stream\r\nContent-Disposition: attachment; filename=x.bin\r\n\r\nstable");

        ExtractionResult first = EmlExtractor.Extract(source, Limits());
        ExtractionResult second = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(first.SourceHash, second.SourceHash);
        Assert.AreEqual(Assert.ContainsSingle(first.Assets).StableId, Assert.ContainsSingle(second.Assets).StableId);
        Assert.AreEqual(first.Outcome, second.Outcome);
    }

    [TestMethod]
    public void Extract_Base64DecodedLimitExceeded_RemainsResourceLimitedAndDoesNotMaterializeEvidence()
    {
        byte[] source = Bytes("Content-Type: text/plain\r\nContent-Transfer-Encoding: base64\r\n\r\nAQID");

        ExtractionResult result = EmlExtractor.Extract(source, Limits(maxDecodedBytes: 2));

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.IsEmpty(result.Content);
        Assert.IsEmpty(result.Assets);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_LIMIT_DECODED"));
    }

    [TestMethod]
    public void Extract_NestedResourceLimit_PropagatesTerminalOutcomeToParent()
    {
        string nested = "Content-Type: text/plain\r\n\r\nNested";
        byte[] source = Bytes("Content-Type: message/rfc822\r\n\r\n" + nested);

        ExtractionResult result = EmlExtractor.Extract(source, Limits(maxDecodedBytes: Encoding.ASCII.GetByteCount(nested)));

        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.HasCount(1, result.NestedResults);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_NESTED_TERMINAL"));
    }

    [TestMethod]
    public void Extract_MultipartLocations_AreAbsoluteRawEncodedRangesWithExactPartPaths()
    {
        string message =
            "Content-Type: multipart/mixed; boundary=x\r\n\r\n" +
            "--x\r\nContent-Type: text/plain\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nbad=ZZ\r\n" +
            "--x\r\nContent-Type: application/octet-stream\r\nContent-Transfer-Encoding: base64\r\n\r\nAQID\r\n--x--\r\n";
        byte[] source = Bytes(message);

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        ExtractionIssue malformed = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_TRANSFER_MALFORMED"));
        Assert.AreEqual("1.1", malformed.SourceLocation?.Path);
        Assert.AreEqual(message.IndexOf("bad=ZZ", StringComparison.Ordinal), malformed.SourceLocation?.Offset);
        ReviewAsset asset = Assert.ContainsSingle(result.Assets);
        Assert.AreEqual("1.2", asset.SourceLocation?.Path);
        Assert.AreEqual(message.IndexOf("AQID", StringComparison.Ordinal), asset.SourceLocation?.Offset);
        Assert.AreEqual(4L, asset.SourceLocation?.Length);
    }

    [TestMethod]
    [DataRow("message/delivery-status")]
    [DataRow("message/disposition-notification")]
    [DataRow("message/feedback-report")]
    [DataRow("application/ms-tnef")]
    public void Extract_PassiveUnsupportedSubtypes_AreVisibleAndNeverComplete(string mediaType)
    {
        ExtractionResult result = EmlExtractor.Extract(Bytes($"Content-Type: {mediaType}\r\n\r\nretained"), Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.HasCount(1, result.Assets);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_SUBTYPE_UNSUPPORTED"));
    }

    [TestMethod]
    public void Extract_SignedAndFlowedContent_ReportUnverifiedSemantics()
    {
        byte[] source = Bytes(
            "Content-Type: multipart/signed; boundary=s\r\n\r\n" +
            "--s\r\nContent-Type: text/plain; format=flowed\r\n\r\nline \r\n--s--\r\n");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_SIGNED_UNVERIFIED"));
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_FLOWED_UNSUPPORTED"));
    }

    [TestMethod]
    public void Extract_QuotedAndGroupAddresses_DoNotSplitQuotedCommas()
    {
        byte[] source = Bytes(
            "To: Team: \"Surname, Given\" <one@example.test>, Two <two@example.test>;\r\n" +
            "Content-Type: text/plain\r\n\r\nbody");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.HasCount(2, result.Participants);
        Assert.AreEqual("Surname, Given", result.Participants[0].DisplayName);
        Assert.AreEqual("one@example.test", result.Participants[0].Address);
        Assert.AreEqual("two@example.test", result.Participants[1].Address);
    }

    [TestMethod]
    public void Extract_AdjacentEncodedWords_SuppressesSeparatorAndRejectsUnknownMode()
    {
        byte[] source = Bytes(
            "Subject: =?UTF-8?B?SGVsbG8=?=   =?UTF-8?Q?_world?=\r\n" +
            "X-Mode: =?UTF-8?X?opaque?=\r\nContent-Type: text/plain\r\n\r\nbody");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual("Hello world", result.Metadata.Single(entry => entry.Name == "header:subject").Value);
        Assert.AreEqual("=?UTF-8?X?opaque?=", result.Metadata.Single(entry => entry.Name == "header:x-mode").Value);
        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_ENCODED_WORD_MODE"));
    }

    [TestMethod]
    public void Extract_QuotedParametersAndRfc2231Utf8Continuations_AreDecodedWithoutSemicolonSplitting()
    {
        byte[] source = Bytes(
            "Content-Type: application/octet-stream; name=\"semi;colon.bin\"\r\n" +
            "Content-Disposition: attachment; filename*0*=utf-8''caf%C3; filename*1*=%A9.bin\r\n\r\ndata");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual("café.bin", Assert.ContainsSingle(result.Assets).OriginalName);
    }

    [TestMethod]
    public void Extract_LfCompatibilityAndAsciiReplacement_AreExplicitPartialIssues()
    {
        byte[] source = [.. Encoding.ASCII.GetBytes("Content-Type: text/plain; charset=us-ascii\n\nvalue"), 0xFF];

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.IsNotEmpty(result.Issues.Where(issue => issue.Code == "EML_LINE_ENDING_COMPAT"));
        _ = Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == "EML_ASCII_REPLACED"));
    }

    [TestMethod]
    public void Extract_BoundaryPrefixIsNotFramingButTrailingWhitespaceIs()
    {
        byte[] source = Bytes(
            "Content-Type: multipart/mixed; boundary=x\r\n\r\n" +
            "--x-not-a-boundary\r\nignored\r\n" +
            "--x \t\r\nContent-Type: text/plain\r\n\r\nbody\r\n--x--\t\r\n");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual("body", Assert.ContainsSingle(result.Content).Text);
        Assert.IsEmpty(result.Issues.Where(issue => issue.Code is "EML_BOUNDARY_NOT_FOUND" or "EML_BOUNDARY_UNCLOSED"));
    }

    [TestMethod]
    public void Extract_UnknownTransferEncoding_RetainsExactlyOneRawAsset()
    {
        byte[] source = Bytes("Content-Type: application/octet-stream\r\nContent-Transfer-Encoding: x-opaque\r\n\r\nraw");

        ExtractionResult result = EmlExtractor.Extract(source, Limits());

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.HasCount(1, result.Assets);
        CollectionAssert.AreEqual(Bytes("raw"), Assert.ContainsSingle(result.Assets).Content.ToArray());
    }

    [TestMethod]
    public void Extract_ManyFoldedLines_UnfoldsInOrder()
    {
        var source = new StringBuilder("X-Long: start\r\n");
        for (int index = 0; index < 2_000; index++)
        {
            source.Append(" value\r\n");
        }

        source.Append("Content-Type: text/plain\r\n\r\nbody");
        ExtractionResult result = EmlExtractor.Extract(Bytes(source.ToString()), Limits(maxObjects: 5_000));

        string value = result.Metadata.Single(entry => entry.Name == "header:x-long").Value;
        Assert.StartsWith("start value", value);
        Assert.EndsWith("value", value);
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static ResourceLimits Limits(
        long maxInputBytes = 1024 * 1024,
        long maxDecodedBytes = 4 * 1024 * 1024,
        int maxObjects = 10_000,
        TimeSpan? maxElapsed = null) =>
        new(
            "eml-tests/1",
            maxInputBytes,
            maxDecodedBytes,
            maxObjects,
            maxTextCharacters: 1_000_000,
            maxAssets: 100,
            maxAssetBytes: 4 * 1024 * 1024,
            maxNestingDepth: 8,
            maxElapsed: maxElapsed ?? TimeSpan.FromSeconds(10));
}
