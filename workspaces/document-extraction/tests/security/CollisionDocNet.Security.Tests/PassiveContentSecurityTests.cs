using CollisionDocNet.Extraction;
using CollisionDocNet.Model;

namespace CollisionDocNet.Security.Tests;

/// <summary>
/// EXT-SEC-001 runtime regressions for passive handling. Inputs are synthetic and
/// exercise ISO 32000-2:2020 actions, ECMA-376 OPC relationships, RFC 5322/MIME,
/// and [MS-CFB] container rejection through the one public extraction boundary.
/// </summary>
[TestClass]
public sealed class PassiveContentSecurityTests
{
    [TestMethod]
    public async Task ExtractAsync_EmlActiveMarkupAndPathAttachment_DoesNotCreateInputSelectedPath()
    {
        string canary = Path.Combine(Path.GetTempPath(), $"collisiondocnet-security-{Environment.ProcessId}.canary");
        Assert.IsFalse(File.Exists(canary));
        string body = $"<script>WScript.Shell.Run('calc.exe')</script><img src=\"file:///{canary}\"><a href=\"http://127.0.0.1:1/secret\">x</a>";
        byte[] source = SyntheticDocuments.MultipartEml("secure-boundary", $"../../{Path.GetFileName(canary)}", body);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-eml", "hostile.eml", "message/rfc822");

        Assert.IsFalse(File.Exists(canary));
        Assert.AreEqual(DetectedFormat.InternetMessage, result.DetectedFormat);
        Assert.DoesNotContain(ExtractionOutcome.TechnicalFailure, new[] { result.Outcome });
        Assert.IsEmpty(result.Assets);
        MetadataEntry descriptor = Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "nonPayload.binary"));
        Assert.DoesNotContain("../", descriptor.Value);
        Assert.DoesNotContain("\\", descriptor.Value);
        Assert.Contains("NON_IMAGE_ASSET_NOT_EMITTED", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_DocxExternalRelationshipsAndMacroPart_RemainPassive()
    {
        const string relationships =
            "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rIdExternal\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"http://127.0.0.1:1/private\" TargetMode=\"External\"/>" +
            "<Relationship Id=\"rIdMacro\" Type=\"http://schemas.microsoft.com/office/2006/relationships/vbaProject\" Target=\"vbaProject.bin\"/>" +
            "</Relationships>";
        byte[] source = SyntheticDocuments.Docx(
            "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>passive</w:t></w:r></w:p></w:body></w:document>",
            [("word/vbaProject.bin", "CreateObject WScript.Shell calc.exe")], relationships);

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-docx", "hostile.docx");

        Assert.AreEqual(DetectedFormat.WordprocessingMl, result.DetectedFormat);
        Assert.DoesNotContain(ExtractionOutcome.TechnicalFailure, new[] { result.Outcome });
        Assert.Contains("external", result.Relationships.Select(static item => item.Kind));
        Assert.Contains("DOCX_EXTERNAL_RELATIONSHIP", result.Issues.Select(static issue => issue.Code));
        Assert.Contains("DOCX_UNKNOWN_RELATIONSHIP", result.Issues.Select(static issue => issue.Code));
        Assert.Contains("DOCX_ORPHAN_PART", result.Issues.Select(static issue => issue.Code));
    }

    [TestMethod]
    public async Task ExtractAsync_PdfJavaScriptLaunchAndUriActions_AreReportedWithoutExecution()
    {
        byte[] source = SyntheticDocuments.Pdf(
            "BT /F1 12 Tf (review) Tj ET",
            catalogueExtras: "/OpenAction 6 0 R",
            additionalObject: "<< /Type /Action /S /JavaScript /JS (app.launchURL('http://127.0.0.1:1/private')) /Next << /S /Launch /F (calc.exe) >> >>");

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-pdf", "active.pdf");

        Assert.AreEqual(DetectedFormat.Pdf, result.DetectedFormat);
        Assert.DoesNotContain(ExtractionOutcome.TechnicalFailure, new[] { result.Outcome });
        Assert.IsTrue(result.Metadata.Any(static item =>
            item.Name.Contains("action", StringComparison.OrdinalIgnoreCase) ||
            item.Value.Contains("JavaScript", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("hostile.doc", "application/msword")]
    [DataRow("hostile.msg", "application/vnd.ms-outlook")]
    public async Task ExtractAsync_CfbEmbeddedProgramMarkers_AreRejectedWithoutExecution(string fileName, string mediaType)
    {
        string canary = Path.Combine(Path.GetTempPath(), $"collisiondocnet-cfb-{Environment.ProcessId}.canary");
        byte[] source = SyntheticDocuments.CompoundSignature($"powershell.exe New-Item '{canary}' http://127.0.0.1:1 \\\\server\\share");

        ExtractionResult result = await DocumentExtractor.ExtractAsync(source, "security-cfb", fileName, mediaType);

        Assert.IsFalse(File.Exists(canary));
        Assert.AreEqual(DetectedContainer.CompoundFile, result.DetectedContainer);
        Assert.DoesNotContain(ExtractionOutcome.Complete, new[] { result.Outcome });
        Assert.DoesNotContain(ExtractionOutcome.TechnicalFailure, new[] { result.Outcome });
    }
}
