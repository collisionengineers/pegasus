using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Pegasus.Web.Mcp;
using SkiaSharp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MCP-04 caller evidence: document add and download through the gated /mcp
/// host, with the download handed back as native MCP content (a text block,
/// an image block, PDF page text) rather than base64. Ingress gate, token and
/// inventory stay in <see cref="AutomationMcpIngressTests"/>.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationDocumentIngressTests
{
    [Fact]
    public async Task AddAndDownloadOverHttpReplayAndAttributeHistory()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var (caseVersion, leaseToken) = await AutomationMcpTestSupport.BeginEditAsync(
            client,
            token,
            caseId,
            expectedVersion: 0,
            rpcId: 41);
        var content = "mcp-04 document fixture"u8.ToArray();
        const string operationKey = "mcp:document-add-success";

        Guid occurrenceId;
        Guid versionId;
        using (var response = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(41, caseId, content, "Other", caseVersion, leaseToken, operationKey)))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(response);
            Assert.False(structured.GetProperty("isReplay").GetBoolean());
            occurrenceId = structured.GetProperty("occurrenceId").GetGuid();
            versionId = structured.GetProperty("versionId").GetGuid();
            Assert.Equal("instruction.txt", structured.GetProperty("fileName").GetString());
            Assert.False(string.IsNullOrWhiteSpace(structured.GetProperty("sha256").GetString()));
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_add'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));

        using (var replay = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(42, caseId, content, "Other", caseVersion, leaseToken, operationKey)))
        {
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(replay);
            Assert.True(structured.GetProperty("isReplay").GetBoolean());
            Assert.Equal(occurrenceId, structured.GetProperty("occurrenceId").GetGuid());
            Assert.Equal(versionId, structured.GetProperty("versionId").GetGuid());
        }

        // A small text file comes back as its text, in a block the client shows
        // its model; the structured half carries identity, size, hash and URL.
        using (var download = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                43,
                "pegasus_document_download",
                new { caseId, occurrenceId, versionId })))
        {
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(download);
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.Equal("text/plain", structured.GetProperty("mediaType").GetString());
            Assert.Equal(content.Length, structured.GetProperty("contentLength").GetInt64());
            Assert.True(
                !structured.TryGetProperty("contentBase64", out _),
                "Content never travels as base64 in the structured result.");
            Assert.Contains(
                blocks.EnumerateArray(),
                block => block.GetProperty("type").GetString() == "text"
                    && block.GetProperty("text").GetString() == "mcp-04 document fixture");
        }

        using (var oversize = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                44,
                "pegasus_document_download",
                new { caseId, occurrenceId, versionId, maxInlineBytes = 1 })))
        {
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(oversize);
            Assert.False(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.DoesNotContain(
                blocks.EnumerateArray(),
                block => block.GetProperty("type").GetString() != "text");
            Assert.Contains(
                "exceeds the inline limit",
                structured.GetProperty("notice").GetString(),
                StringComparison.Ordinal);
            var contentUrl = structured.GetProperty("contentUrl").GetString();
            Assert.False(string.IsNullOrWhiteSpace(contentUrl));
            using var streamRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            streamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            streamRequest.Headers.Range = new RangeHeaderValue(0, 2);
            using var streamed = await client.SendAsync(streamRequest);
            Assert.Equal(HttpStatusCode.PartialContent, streamed.StatusCode);
            Assert.Equal(content[..3], await streamed.Content.ReadAsByteArrayAsync());

            using var unauthenticated = await client.GetAsync(contentUrl);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            var wrongScopeToken = await AutomationMcpTestSupport.RequestTokenAsync(
                client, AutomationMcp.CasesScope);
            using var wrongScopeRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            wrongScopeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wrongScopeToken);
            using var wrongScope = await client.SendAsync(wrongScopeRequest);
            Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_download'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));
    }

    /// <summary>
    /// What Claude Desktop asked for: a photograph arrives as an image block
    /// it can show, re-encoded to the byte budget, and a PDF arrives as its
    /// page text; the originals stay in custody at the content URL. The adds
    /// present no lease token, so each holds the lease for its one command and
    /// leaves the Case free afterwards.
    /// </summary>
    [Fact]
    public async Task ImageDownloadsAsAnImageBlockAndPdfDownloadsAsPageText()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var photograph = NoisyJpeg(1600, 1200);
        Assert.True(photograph.Length > AutomationFileContent.DefaultInlineBytes, "The fixture must exceed the budget.");
        var pdf = IntakeTestEvidence.CreatePdf("Estimate total 1234.56 pounds for the nearside door.");

        var (imageOccurrence, imageVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 0, "photo.jpg", "image/jpeg", photograph, "Image", "mcp:add-photo");
        var (pdfOccurrence, pdfVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 1, "estimate.pdf", "application/pdf", pdf, "Other", "mcp:add-pdf");

        // The implicit lease is held for the command only: nothing is left on the Case.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{caseId:D}' AND EditLeaseHolderKind IS NULL"));
        Assert.Equal(2, await AutomationMcpTestSupport.GetWorkflowVersionAsync(mcpFactory, caseId));

        using (var download = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                3,
                "pegasus_document_download",
                new { caseId, occurrenceId = imageOccurrence, versionId = imageVersion })))
        {
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(download);
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.Equal(photograph.Length, structured.GetProperty("contentLength").GetInt64());
            var image = Assert.Single(
                blocks.EnumerateArray(),
                block => block.GetProperty("type").GetString() == "image");
            Assert.Equal("image/jpeg", image.GetProperty("mimeType").GetString());
            var bytes = Convert.FromBase64String(image.GetProperty("data").GetString()!);
            Assert.True(bytes.Length <= AutomationFileContent.DefaultInlineBytes, $"{bytes.Length} bytes exceed the budget.");
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xD8, bytes[1]);
            using var decoded = SKBitmap.Decode(bytes);
            Assert.NotNull(decoded);
            Assert.True(decoded.Width <= AutomationFileContent.LongestEdge);
            Assert.Contains(
                blocks.EnumerateArray(),
                block => block.GetProperty("type").GetString() == "text"
                    && block.GetProperty("text").GetString()!.Contains("Shown at", StringComparison.Ordinal));
        }

        using (var download = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                4,
                "pegasus_document_download",
                new { caseId, occurrenceId = pdfOccurrence, versionId = pdfVersion })))
        {
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(download);
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.All(blocks.EnumerateArray(), block => Assert.Equal("text", block.GetProperty("type").GetString()));
            Assert.Contains(
                blocks.EnumerateArray(),
                block => block.GetProperty("text").GetString()!.StartsWith("Page 1 of 1", StringComparison.Ordinal)
                    && block.GetProperty("text").GetString()!.Contains("Estimate total 1234.56", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DocumentToolsRefuseValidationFailuresWithoutLeakingTheLeaseToken()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(client, "automation.documents");
        var leaked = new string('d', 64);

        using (var badRole = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(70, Guid.NewGuid(), "x"u8.ToArray(), "NotARole", 0, leaked, "mcp:document-bad-role")))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(badRole)).RootElement.ToString();
            Assert.Contains("semantic role", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(leaked, body, StringComparison.Ordinal);
        }

        // No lease token means the tool claims the lease itself; on a case that
        // does not exist the claim is refused and nothing is written.
        using (var unknownCase = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(71, Guid.NewGuid(), "x"u8.ToArray(), "Other", 0, "", "mcp:document-unknown-case")))
        {
            var body = await AutomationMcpTestSupport.ReadErrorTextAsync(unknownCase);
            Assert.DoesNotContain("edit lease token is required", body, StringComparison.OrdinalIgnoreCase);
        }

        using (var emptyVersion = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                73,
                "pegasus_document_download",
                new { caseId = Guid.NewGuid(), occurrenceId = Guid.NewGuid(), versionId = Guid.Empty })))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(emptyVersion)).RootElement.ToString();
            Assert.Contains("version identifier", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(3, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind IN (N'pegasus_document_add', N'pegasus_document_download')
              AND Outcome = N'Failed'
            """));
    }

    [Fact]
    public async Task DocumentToolsEnforceTheDocumentsScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnlyToken = await AutomationMcpTestSupport.RequestTokenAsync(client, "automation.cases");

        var payloads = new[]
        {
            AddPayload(
                80,
                Guid.NewGuid(),
                "x"u8.ToArray(),
                "Other",
                0,
                new string('e', 64),
                "mcp:document-scope-denied"),
            AutomationMcpTestSupport.ToolCallPayload(
                81,
                "pegasus_document_download",
                new { caseId = Guid.NewGuid(), occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() })
        };

        foreach (var payload in payloads)
        {
            using var response = await AutomationMcpTestSupport.PostMcpAsync(client, casesOnlyToken, payload);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await AutomationMcpTestSupport.ReadJsonRpcAsync(response);
            Assert.Contains(
                "automation.documents",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(payloads.Length, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    private static async Task<(Guid OccurrenceId, Guid VersionId)> AddWithoutLeaseAsync(
        HttpClient client,
        string token,
        Guid caseId,
        long expectedCaseVersion,
        string fileName,
        string mediaType,
        byte[] content,
        string semanticRole,
        string operationKey)
    {
        using var response = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                1,
                "pegasus_document_add",
                new
                {
                    caseId,
                    fileName,
                    mediaType,
                    contentBase64 = Convert.ToBase64String(content),
                    semanticRole,
                    expectedCaseVersion,
                    operationKey
                }));
        var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(response);
        return (structured.GetProperty("occurrenceId").GetGuid(), structured.GetProperty("versionId").GetGuid());
    }

    /// <summary>A photograph-sized JPEG that does not compress: random pixels.</summary>
    private static byte[] NoisyJpeg(int width, int height)
    {
        var random = new Random(20261001);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = bitmap.GetPixelSpan();
        random.NextBytes(pixels);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 92);
        return encoded.ToArray();
    }

    private static string AddPayload(
        int id,
        Guid caseId,
        byte[] content,
        string semanticRole,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey) =>
        AutomationMcpTestSupport.ToolCallPayload(
            id,
            "pegasus_document_add",
            new
            {
                caseId,
                fileName = "instruction.txt",
                mediaType = "text/plain",
                contentBase64 = Convert.ToBase64String(content),
                semanticRole,
                expectedCaseVersion,
                editLeaseToken,
                operationKey
            });
}
