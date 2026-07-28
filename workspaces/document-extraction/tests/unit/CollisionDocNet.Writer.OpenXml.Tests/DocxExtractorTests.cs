using CollisionDocNet.Core;
using CollisionDocNet.Model;

namespace CollisionDocNet.Writer.OpenXml.Tests;

[TestClass]
public sealed class DocxExtractorTests
{
    private const string MainType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    [TestMethod]
    public void Extract_TransitionalParagraphTokens_ReturnsCompleteOrderedTextAndProperties()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>Hello</w:t><w:tab/><w:t>world</w:t><w:br/><w:t>end</w:t></w:r></w:p>");

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.Contains("Hello\tworld\nend", result.Content[0].Text);
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "property.title"));
        Assert.AreEqual("transitional", result.Metadata.Single(static item => item.Name == "story.main.namespace").Value);
        Assert.IsEmpty(result.Issues);
    }

    [TestMethod]
    public void Extract_StrictMainAndHeader_DiscoversStoriesThroughRelationships()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>Main text</w:t></w:r></w:p>", strict: true, includeHeader: true);

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.ContainsSingle(result.Content.Where(static item => item.Kind == "docx.main.paragraph" && item.Text == "Main text"));
        Assert.ContainsSingle(result.Content.Where(static item => item.Kind == "docx.header.paragraph" && item.Text == "Header text"));
        Assert.AreEqual("strict", result.Metadata.Single(static item => item.Name == "story.main.namespace").Value);
    }

    [TestMethod]
    public void Extract_FieldsBookmarksHyperlinksAndDeletedText_PreservesInspectableEvidence()
    {
        const string body = "<w:p><w:bookmarkStart w:id=\"1\" w:name=\"mark\"/><w:hyperlink r:id=\"rIdExternal\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:r><w:t>link</w:t></w:r></w:hyperlink><w:fldSimple w:instr=\"DATE\"><w:r><w:instrText>DATE</w:instrText><w:t>value</w:t></w:r></w:fldSimple><w:del><w:r><w:delText>removed</w:delText></w:r></w:del></w:p>";
        byte[] source = DocxFixture.Create(body, externalLink: true);

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "bookmark" && item.Value == "mark"));
        Assert.ContainsSingle(result.Content.Where(static item => item.Kind == "docx.main.field-instruction" && item.Text == "DATE"));
        Assert.ContainsSingle(result.Content.Where(static item => item.Kind == "docx.main.deleted" && item.Text == "removed"));
        ExtractionIssue issue = Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_EXTERNAL_RELATIONSHIP"));
        Assert.AreEqual(ExtractionIssueSeverity.Information, issue.Severity);
    }

    [TestMethod]
    public void Extract_DependencyParts_AreInventoriedWithoutLosingTextCompleteness()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>", dependencyParts: true);

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "dependency.styles.elements"));
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "dependency.numbering.elements"));
        Assert.HasCount(2, result.Issues.Where(static issue => issue.Code == "DOCX_DEPENDENCY_INVENTORY_ONLY"));
        Assert.IsTrue(result.Issues.Where(static issue => issue.Code == "DOCX_DEPENDENCY_INVENTORY_ONLY")
            .All(static issue => issue.Severity == ExtractionIssueSeverity.Information));
    }

    [TestMethod]
    public void Extract_WebSettingsRelationship_UsesExactTypeAndKeepsPartReachable()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>main</w:t></w:r></w:p>"),
            entries =>
            {
                entries["word/webSettings.xml"] = $"<w:webSettings xmlns:w=\"{DocxFixture.TransitionalWord}\"><w:optimizeForBrowser/></w:webSettings>";
                entries["[Content_Types].xml"] = entries["[Content_Types].xml"].Replace("</Types>", "<Override PartName=\"/word/webSettings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.webSettings+xml\"/></Types>", StringComparison.Ordinal);
                entries["word/_rels/document.xml.rels"] = $"<Relationships xmlns=\"{DocxFixture.PackageRelationships}\"><Relationship Id=\"rIdWeb\" Type=\"{DocxFixture.OfficeRelationships}/webSettings\" Target=\"webSettings.xml\"/></Relationships>";
            });

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "dependency.webSettings.elements"));
        Assert.IsEmpty(result.Issues.Where(static issue => issue.Code is "DOCX_ORPHAN_PART" or "DOCX_ORPHAN_RELATIONSHIP_SOURCE"));
    }

    [TestMethod]
    public void Extract_StandardDrawingTextDescriptionAndImage_AreRetainedWithoutFalsePartial()
    {
        const string drawing = "<w:p><w:r><w:drawing><wp:inline xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\"><wp:docPr id=\"1\" name=\"Picture 1\" descr=\"Review image\"/><a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:graphicData><pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:nvPicPr><pic:cNvPr id=\"2\" name=\"Evidence\"/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rIdImage\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"/></pic:blipFill></pic:pic><a:t>Drawing label</a:t></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>";
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create(drawing),
            entries =>
            {
                entries["word/media/image1.png"] = "synthetic-image";
                entries["word/_rels/document.xml.rels"] = $"<Relationships xmlns=\"{DocxFixture.PackageRelationships}\"><Relationship Id=\"rIdImage\" Type=\"{DocxFixture.OfficeRelationships}/image\" Target=\"media/image1.png\"/></Relationships>";
            });

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.ContainsSingle(result.Content.Where(static item => item.Kind == "docx.main.drawing-text" && item.Text == "Drawing label"));
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "drawing.descr" && item.Value == "Review image"));
        Assert.ContainsSingle(result.Assets.Where(static item => item.Kind == "image"));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_DRAWING_PASSIVE" && issue.Severity == ExtractionIssueSeverity.Information));
        Assert.IsEmpty(result.Issues.Where(static issue => issue.Code == "DOCX_UNKNOWN_MARKUP"));
    }

    [TestMethod]
    public void Extract_DependencyWithSpoofedContentType_RemainsVisiblePartial()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>main</w:t></w:r></w:p>", dependencyParts: true),
            entries => entries["[Content_Types].xml"] = entries["[Content_Types].xml"].Replace(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml",
                "application/x-spoof+styles+xml", StringComparison.Ordinal));

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_DEPENDENCY_CONTENT_TYPE_INVALID"));
        Assert.IsEmpty(result.Metadata.Where(static item => item.Name == "dependency.styles.elements"));
    }

    [TestMethod]
    public void Extract_StandardSectionGeometryMarkup_DoesNotCreateFalseUnknownMarkup()
    {
        const string body = "<w:p><w:r><w:t>main</w:t></w:r></w:p><w:sectPr><w:pgSz w:w=\"12240\" w:h=\"15840\"/><w:pgMar w:top=\"1440\"/><w:cols w:space=\"720\"/><w:docGrid w:linePitch=\"360\"/></w:sectPr>";

        ExtractionResult result = DocxExtractor.Extract(DocxFixture.Create(body));

        Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
        Assert.IsEmpty(result.Issues.Where(static issue => issue.Code == "DOCX_UNKNOWN_MARKUP"));
        Assert.AreEqual("1", result.Metadata.Single(static item => item.Name == "story.main.sections").Value);
    }

    [TestMethod]
    public void Extract_MceAlternateContent_IsVisiblePartialNotSilentSuccess()
    {
        const string body = "<mc:AlternateContent><mc:Choice Requires=\"w14\"><w:p><w:r><w:t>choice</w:t></w:r></w:p></mc:Choice><mc:Fallback><w:p><w:r><w:t>fallback</w:t></w:r></w:p></mc:Fallback></mc:AlternateContent>";
        byte[] source = DocxFixture.Create(body, mce: true);

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.IsNotEmpty(result.Issues.Where(static issue => issue.Code == "DOCX_MCE_PARTIAL"));
    }

    [TestMethod]
    public void Extract_PassiveAssetsAndMacro_AreStableAndNeverExecuted()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>", passiveAssets: true, macroEnabled: true);

        ExtractionResult first = DocxExtractor.Extract(source);
        ExtractionResult retry = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, first.Outcome);
        Assert.HasCount(4, first.Assets);
        CollectionAssert.AreEqual(first.Assets.Select(static asset => asset.StableId).ToArray(), retry.Assets.Select(static asset => asset.StableId).ToArray());
        Assert.ContainsSingle(first.Issues.Where(static issue => issue.Code == "DOCX_ACTIVE_CONTENT_PASSIVE"));
        Assert.IsNotEmpty(first.Issues.Where(static issue => issue.Code == "DOCX_EMBEDDED_CONTENT_PASSIVE"));
    }

    [TestMethod]
    public void Extract_CorruptZip_ReturnsCorruptStructuredResult()
    {
        ExtractionResult result = DocxExtractor.Extract("not a package"u8.ToArray());

        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_OPC_INVALID"));
    }

    [TestMethod]
    public void Extract_EncryptedCompoundWrapper_ReturnsEncryptedWithoutDecrypting()
    {
        byte[] source = DocxFixture.EncryptedCompoundWrapper();
        CollisionDocNet.Storage.Detection.FormatDetectionResult detection =
            CollisionDocNet.Storage.Detection.FileFormatDetector.Detect(source);
        CollisionDocNet.Storage.CompoundFile.CompoundFileReadResult compound =
            CollisionDocNet.Storage.CompoundFile.CompoundFileReader.Read(source);
        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Encrypted, result.Outcome,
            $"Detector candidates={string.Join(',', detection.Candidates.Select(static candidate => candidate.Format))}; diagnostic={detection.DiagnosticCode}; cfb={compound.Error}");
        Assert.AreEqual(DetectedContainer.CompoundFile, result.DetectedContainer);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_ENCRYPTED"));
        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void Extract_InputAboveConfiguredLimit_ReturnsResourceLimitExceeded()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>");
        var limits = new ResourceLimits("tiny", 64, 1024, 100, 100, 10, 1024, 1, TimeSpan.FromSeconds(1));

        ExtractionResult result = DocxExtractor.Extract(source, new() { ResourceLimits = limits });

        AssertResourceLimit(result, "DOCX_INPUT_LIMIT");
    }

    [TestMethod]
    public void Extract_PreCancelled_ReturnsCancelledStructuredResult()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ExtractionResult result = DocxExtractor.Extract(
            DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>"), cancellationToken: cancellation.Token);

        Assert.AreEqual(ExtractionOutcome.Cancelled, result.Outcome);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_CANCELLED"));
    }

    [TestMethod]
    public void Extract_DtdInStory_ReturnsPartialWithoutEntityResolution()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>safe</w:t></w:r></w:p>");
        using var input = new MemoryStream(source);
        using var sourceArchive = new System.IO.Compression.ZipArchive(input, System.IO.Compression.ZipArchiveMode.Read);
        var entries = sourceArchive.Entries.ToDictionary(
            static entry => entry.FullName,
            static entry =>
            {
                using Stream stream = entry.Open();
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }, StringComparer.Ordinal);
        entries["word/document.xml"] = $"<!DOCTYPE x [<!ENTITY ex SYSTEM 'file:///forbidden'>]><w:document xmlns:w=\"{DocxFixture.TransitionalWord}\"><w:body><w:p><w:r><w:t>&ex;</w:t></w:r></w:p></w:body></w:document>";

        ExtractionResult result = DocxExtractor.Extract(DocxFixture.Zip(entries));

        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_STORY_XML_INVALID"));
        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void Extract_SuffixedRelationshipUri_IsNotMistakenForOfficeDocument()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>hidden</w:t></w:r></w:p>"),
            entries => entries["_rels/.rels"] = entries["_rels/.rels"].Replace(
                DocxFixture.OfficeRelationships + "/officeDocument",
                "https://attacker.invalid/officeDocument",
                StringComparison.Ordinal));

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
        Assert.IsEmpty(result.Content);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_MAIN_RELATIONSHIP_MISSING"));
    }

    [TestMethod]
    public void Extract_SpoofedMainContentTypeSuffix_IsRejected()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>hidden</w:t></w:r></w:p>"),
            entries => entries["[Content_Types].xml"] = entries["[Content_Types].xml"].Replace(
                MainType, "application/x-spoof+" + MainType, StringComparison.Ordinal));

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Corrupt, result.Outcome);
        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void Extract_SpoofedAssetRelationship_IsNotFollowedAndBecomesOrphanEvidence()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>", passiveAssets: true),
            entries => entries["word/_rels/document.xml.rels"] = entries["word/_rels/document.xml.rels"].Replace(
                DocxFixture.OfficeRelationships + "/image",
                "https://attacker.invalid/image",
                StringComparison.Ordinal));

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.HasCount(2, result.Assets);
        Assert.IsNotEmpty(result.Issues.Where(static issue => issue.Code == "DOCX_ORPHAN_PART"));
    }

    [TestMethod]
    public void Extract_CurrentFieldDeletedAndCurrentText_PreservesSourceOrder()
    {
        const string body = "<w:p><w:r><w:t>before</w:t></w:r><w:fldSimple w:instr=\" DATE \"/><w:r><w:t>result</w:t></w:r><w:del><w:r><w:delText>removed</w:delText></w:r></w:del><w:r><w:t>after</w:t></w:r></w:p>";

        ExtractionResult result = DocxExtractor.Extract(DocxFixture.Create(body));

        string[] expectedText = ["before", " DATE ", "result", "removed", "after"];
        string[] expectedKinds = ["docx.main.text", "docx.main.field-instruction", "docx.main.text", "docx.main.deleted", "docx.main.paragraph"];
        CollectionAssert.AreEqual(
            expectedText,
            result.Content.Select(static segment => segment.Text).ToArray());
        CollectionAssert.AreEqual(
            expectedKinds,
            result.Content.Select(static segment => segment.Kind).ToArray());
    }

    [TestMethod]
    public void Extract_FormattingWhitespace_IsIgnoredButForeignMarkupIsVisibleAndOmitted()
    {
        const string body = "\n  <w:p>\n    <w:r><w:t>kept</w:t></w:r>\n    <x:payload xmlns:x=\"urn:foreign\"><w:t>not-owned</w:t></x:payload>\n  </w:p>\n";

        ExtractionResult result = DocxExtractor.Extract(DocxFixture.Create(body));

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.AreEqual("kept", string.Concat(result.Content.Select(static segment => segment.Text)));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_UNKNOWN_MARKUP"));
    }

    [TestMethod]
    public void Extract_UnknownWordMarkup_IsVisibleAndItsSubtreeIsNotProjected()
    {
        const string body = "<w:p><w:r><w:t>kept</w:t></w:r><w:future><w:t>not-owned</w:t></w:future></w:p>";

        ExtractionResult result = DocxExtractor.Extract(DocxFixture.Create(body));

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.AreEqual("kept", string.Concat(result.Content.Select(static segment => segment.Text)));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_UNKNOWN_MARKUP"));
    }

    [TestMethod]
    public void Extract_MceAlternative_ProjectsFallbackOnly()
    {
        const string body = "<mc:AlternateContent><mc:Choice Requires=\"w14\"><w:p><w:r><w:t>choice</w:t></w:r></w:p></mc:Choice><mc:Fallback><w:p><w:r><w:t>fallback</w:t></w:r></w:p></mc:Fallback></mc:AlternateContent>";

        ExtractionResult result = DocxExtractor.Extract(DocxFixture.Create(body, mce: true));

        Assert.AreEqual("fallback", string.Concat(result.Content.Select(static segment => segment.Text)));
        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
    }

    [TestMethod]
    public void Extract_ReachableStoryGraph_AlwaysProjectsMainBeforeHeader()
    {
        ExtractionResult result = DocxExtractor.Extract(
            DocxFixture.Create("<w:p><w:r><w:t>main</w:t></w:r></w:p>", includeHeader: true));

        Assert.AreEqual("docx.main.paragraph", result.Content[0].Kind);
        Assert.AreEqual("docx.header.paragraph", result.Content[1].Kind);
    }

    [TestMethod]
    public void Extract_ReachableHeaderWithSpoofedContentType_IsNotParsed()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>main</w:t></w:r></w:p>", includeHeader: true),
            entries => entries["[Content_Types].xml"] = entries["[Content_Types].xml"].Replace(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml",
                "application/x-spoof+application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml",
                StringComparison.Ordinal));

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.IsEmpty(result.Content.Where(static segment => segment.Kind.StartsWith("docx.header", StringComparison.Ordinal)));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_STORY_CONTENT_TYPE_INVALID"));
    }

    [TestMethod]
    public void Extract_ReachableSettingsProtection_IsReportedPassively()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>main</w:t></w:r></w:p>"),
            entries =>
            {
                entries["word/settings.xml"] = $"<w:settings xmlns:w=\"{DocxFixture.TransitionalWord}\"><w:documentProtection w:edit=\"readOnly\"/></w:settings>";
                entries["[Content_Types].xml"] = entries["[Content_Types].xml"].Replace("</Types>", "<Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/></Types>", StringComparison.Ordinal);
                entries["word/_rels/document.xml.rels"] = $"<Relationships xmlns=\"{DocxFixture.PackageRelationships}\"><Relationship Id=\"rIdSettings\" Type=\"{DocxFixture.OfficeRelationships}/settings\" Target=\"settings.xml\"/></Relationships>";
            });

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.ContainsSingle(result.Metadata.Where(static item => item.Name == "document.protection" && item.Value == "present-passive"));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_DOCUMENT_PROTECTION_PASSIVE"));
    }

    [TestMethod]
    public void Extract_OrphanPartAndForeignRelationshipSource_AreFlaggedAndNotProjected()
    {
        byte[] source = DocxFixture.Rewrite(
            DocxFixture.Create("<w:p><w:r><w:t>main</w:t></w:r></w:p>"),
            entries =>
            {
                entries["word/orphan.xml"] = $"<w:document xmlns:w=\"{DocxFixture.TransitionalWord}\"><w:body><w:p><w:r><w:t>orphan</w:t></w:r></w:p></w:body></w:document>";
                entries["word/_rels/orphan.xml.rels"] = $"<Relationships xmlns=\"{DocxFixture.PackageRelationships}\"><Relationship Id=\"rId1\" Type=\"{DocxFixture.OfficeRelationships}/hyperlink\" Target=\"https://example.invalid\" TargetMode=\"External\"/></Relationships>";
            });

        ExtractionResult result = DocxExtractor.Extract(source);

        Assert.AreEqual(ExtractionOutcome.Partial, result.Outcome);
        Assert.DoesNotContain("orphan", string.Concat(result.Content.Select(static segment => segment.Text)));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_ORPHAN_RELATIONSHIP_SOURCE"));
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_ORPHAN_PART"));
    }

    [TestMethod]
    public void Extract_CumulativeObjectLimit_ReturnsResourceLimitBeforeCorrupt()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>");
        var limits = new ResourceLimits("objects", 1_000_000, 1_000_000, 4, 1_000_000, 10, 1_000_000, 1, TimeSpan.FromSeconds(30));

        ExtractionResult result = DocxExtractor.Extract(source, new() { ResourceLimits = limits });

        AssertResourceLimit(result, "DOCX_XML_LIMIT");
    }

    [TestMethod]
    public void Extract_OpcRelationshipLimit_ReturnsResourceLimitNotCorrupt()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>");
        var opc = CollisionDocNet.Storage.Opc.OpcLimits.Default with { MaximumRelationships = 1 };

        ExtractionResult result = DocxExtractor.Extract(source, new() { OpcLimits = opc });

        AssertResourceLimit(result, "DOCX_RELATIONSHIP_LIMIT");
    }

    [TestMethod]
    public void Extract_StoryXmlDepthLimit_ReturnsResourceLimitNotCorrupt()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>");
        var opc = CollisionDocNet.Storage.Opc.OpcLimits.Default with
        {
            Xml = CollisionDocNet.Storage.Xml.BoundedXmlLimits.Default with { MaximumDepth = 3 },
        };

        ExtractionResult result = DocxExtractor.Extract(source, new() { OpcLimits = opc });

        AssertResourceLimit(result, "DOCX_XML_LIMIT");
    }

    [TestMethod]
    public void Extract_CumulativeTextAcrossStories_ReturnsResourceLimit()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>main-text</w:t></w:r></w:p>", includeHeader: true);
        var limits = new ResourceLimits("text", 1_000_000, 1_000_000, 10_000, 20, 10, 1_000_000, 1, TimeSpan.FromSeconds(30));

        ExtractionResult result = DocxExtractor.Extract(source, new() { ResourceLimits = limits });

        AssertResourceLimit(result, "DOCX_XML_TEXT_LIMIT");
    }

    [TestMethod]
    public void Extract_TotalDecodedPackageLimit_ReturnsResourceLimit()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>");
        var limits = new ResourceLimits("decoded", 1_000_000, 64, 10_000, 10_000, 10, 1_000_000, 1, TimeSpan.FromSeconds(30));

        ExtractionResult result = DocxExtractor.Extract(source, new() { ResourceLimits = limits });

        AssertResourceLimit(result, "DOCX_ZIP_LIMIT");
    }

    [TestMethod]
    public void Extract_ReachableAssetCountLimit_ReturnsResourceLimit()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>", passiveAssets: true);
        var limits = new ResourceLimits("assets", 1_000_000, 1_000_000, 10_000, 10_000, 1, 1_000_000, 1, TimeSpan.FromSeconds(30));

        ExtractionResult result = DocxExtractor.Extract(source, new() { ResourceLimits = limits });

        AssertResourceLimit(result, "DOCX_ASSET_COUNT_LIMIT");
    }

    [TestMethod]
    public void Extract_ReachableAssetByteLimit_ReturnsResourceLimit()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>", passiveAssets: true);
        var limits = new ResourceLimits("asset-bytes", 1_000_000, 1_000_000, 10_000, 10_000, 10, 1, 1, TimeSpan.FromSeconds(30));

        ExtractionResult result = DocxExtractor.Extract(source, new() { ResourceLimits = limits });

        AssertResourceLimit(result, "DOCX_ASSET_BYTES_LIMIT");
    }

    [TestMethod]
    public void Extract_ElapsedDeadline_ReturnsTimedOutNotCancelled()
    {
        var limits = new ResourceLimits("deadline", 1_000_000, 1_000_000, 10_000, 10_000, 10, 1_000_000, 1, TimeSpan.FromTicks(1));

        ExtractionResult result = DocxExtractor.Extract(
            DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>"),
            new() { ResourceLimits = limits, TimeProvider = new AdvancingTimeProvider() });

        Assert.AreEqual(ExtractionOutcome.TimedOut, result.Outcome);
        Assert.ContainsSingle(result.Issues.Where(static issue => issue.Code == "DOCX_TIMED_OUT"));
    }

    [TestMethod]
    public void Extract_AssetLocationAndIdentity_AreDeterministicAndContainerBased()
    {
        byte[] source = DocxFixture.Create("<w:p><w:r><w:t>text</w:t></w:r></w:p>", passiveAssets: true);

        ExtractionResult first = DocxExtractor.Extract(source);
        ExtractionResult second = DocxExtractor.Extract(source);

        CollectionAssert.AreEqual(first.Assets.Select(static asset => asset.StableId).ToArray(), second.Assets.Select(static asset => asset.StableId).ToArray());
        Assert.IsTrue(first.Assets.All(static asset => asset.SourceLocation is { Kind: SourceLocationKind.ContainerEntry, Domain: "docx-part" }));
        Assert.IsTrue(first.Content.All(static segment => segment.SourceLocation is { Kind: SourceLocationKind.LogicalRange, Domain: "xml-line-column", Offset: > 0, Length: > 0 }));
    }

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Add(ref _timestamp, 2);
    }

    private static void AssertResourceLimit(ExtractionResult result, string expectedCode)
    {
        Assert.AreEqual(ExtractionOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.ContainsSingle(result.Issues.Where(issue => issue.Code == expectedCode));
        Assert.IsEmpty(result.Issues.Where(static issue => issue.Code == "DOCX_OPC_INVALID"));
    }
}
