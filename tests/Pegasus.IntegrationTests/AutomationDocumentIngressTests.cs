using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;
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

        var (secondImageOccurrence, secondImageVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 2, "photo-rear.jpg", "image/jpeg", photograph, "Image", "mcp:add-photo-rear");
        var (noteOccurrence, noteVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 3, "note.txt", "text/plain", "batch note text"u8.ToArray(), "Other", "mcp:add-note");

        // At the default limit the walk binds (R192). The first photograph is
        // offered everything after its caption, less base64's growth, so it is
        // the renderer's answer for that bound, not a share; the PDF and then
        // photo-rear.jpg are offered what it leaves, and note.txt what remains
        // after them. The content blocks keep the request order: caption and
        // image, PDF header and page, caption and image, summary and text.
        var renderer = mcpFactory.Services.GetRequiredService<IRenderImageForDelivery>();
        using (var batchDownload = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                5,
                "pegasus_document_download_batch",
                new
                {
                    caseId,
                    documents = new[]
                    {
                        new { occurrenceId = imageOccurrence, versionId = imageVersion },
                        new { occurrenceId = pdfOccurrence, versionId = pdfVersion },
                        new { occurrenceId = secondImageOccurrence, versionId = secondImageVersion },
                        new { occurrenceId = noteOccurrence, versionId = noteVersion }
                    }
                })))
        {
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(batchDownload);
            Assert.Equal(caseId, structured.GetProperty("caseId").GetGuid());
            var docs = structured.GetProperty("documents").EnumerateArray().ToArray();
            string[] requestOrder = ["photo.jpg", "estimate.pdf", "photo-rear.jpg", "note.txt"];
            Assert.Equal(requestOrder, docs.Select(item => item.GetProperty("fileName").GetString()!).ToArray());
            Assert.Equal(
                [true, true, true, true],
                docs.Select(item => item.GetProperty("contentIncluded").GetBoolean()).ToArray());

            var content = blocks.EnumerateArray().ToArray();
            Assert.Equal(
                ["text", "image", "text", "text", "text", "image", "text", "text"],
                content.Select(block => block.GetProperty("type").GetString()!).ToArray());
            Assert.Equal(
                requestOrder,
                content
                    .Where(block => block.GetProperty("type").GetString() == "text")
                    .Select(block => block.GetProperty("text").GetString()!)
                    .Select(text => requestOrder.FirstOrDefault(
                        name => text.StartsWith($"{name} (", StringComparison.Ordinal)))
                    .OfType<string>()
                    .ToArray());
            Assert.Contains("Estimate total 1234.56", content[3].GetProperty("text").GetString(), StringComparison.Ordinal);
            Assert.Equal("batch note text", content[7].GetProperty("text").GetString());

            var first = await renderer.RenderAsync(
                photograph,
                AutomationFileContent.LongestEdge,
                ImageBound(AutomationFileContent.DefaultInlineBytes, content[0]),
                CancellationToken.None);
            Assert.Equal(first.Image!.Content, ImageBytes(content[1]));
            var rear = await renderer.RenderAsync(
                photograph,
                AutomationFileContent.LongestEdge,
                ImageBound(AutomationFileContent.DefaultInlineBytes - InlineContentBytes(content[..4]), content[4]),
                CancellationToken.None);
            Assert.Equal(rear.Image!.Content, ImageBytes(content[5]));
            var inlineBytes = InlineContentBytes(content);
            Assert.True(
                inlineBytes <= AutomationFileContent.DefaultInlineBytes,
                $"Batch content was {inlineBytes} bytes at its encoded size, above the default {AutomationFileContent.DefaultInlineBytes}.");
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_download_batch'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));
    }

    /// <summary>
    /// A PDF whose page text is fewer characters than <c>maxInlineBytes</c> can
    /// still be more UTF-8 bytes, once pound signs and accents are counted.
    /// The batch charges the UTF-8 bytes (R192): at a limit of the text's
    /// characters the PDF does not fit whole, so it is withheld and the
    /// photograph after it is not delivered; at a limit of its UTF-8 bytes the
    /// multi-byte text comes back whole, within that limit.
    /// </summary>
    [Fact]
    public async Task BatchDownloadChargesNonAsciiPdfTextInUtf8Bytes()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var pageText = string.Concat(Enumerable.Repeat("£50 café résumé. ", 40));
        var pdf = IntakeTestEvidence.CreatePdf(pageText);
        var photograph = NoisyJpeg(900, 700);

        var (pdfOccurrence, pdfVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 0, "estimate.pdf", "application/pdf", pdf, "Other", "mcp:add-nonascii-pdf");
        var (imageOccurrence, imageVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 1, "photo.jpg", "image/jpeg", photograph, "Image", "mcp:add-nonascii-photo");

        int characters;
        int utf8Bytes;
        using (var single = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                6,
                "pegasus_document_download",
                new { caseId, occurrenceId = pdfOccurrence, versionId = pdfVersion })))
        {
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(single);
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            var text = string.Concat(blocks.EnumerateArray()
                .Where(block => block.GetProperty("type").GetString() == "text")
                .Select(block => block.GetProperty("text").GetString()));
            // Helvetica extraction keeps the pound sign. The café/résumé accents
            // are in the PDF; the budget proof is the multi-byte text that comes back.
            Assert.Contains("£", text, StringComparison.Ordinal);
            characters = text.Length;
            utf8Bytes = Encoding.UTF8.GetByteCount(text);
            Assert.True(
                utf8Bytes > characters,
                $"The fixture must be more UTF-8 bytes ({utf8Bytes}) than characters ({characters}).");
        }

        // Its characters would fit; its UTF-8 bytes do not, so it is withheld
        // whole and the walk stops before the photograph.
        var (atCharacters, _) = await BatchAsync(characters);
        var withheld = atCharacters.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(
            ["estimate.pdf", "photo.jpg"],
            withheld.Select(item => item.GetProperty("fileName").GetString()!).ToArray());
        Assert.All(withheld, item => Assert.False(item.GetProperty("contentIncluded").GetBoolean()));
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(characters), withheld[0].GetProperty("notice").GetString());
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, withheld[1].GetProperty("notice").GetString());

        // At its UTF-8 bytes it comes back whole and fills the limit exactly;
        // nothing remains for the photograph.
        var (atBytes, blocksAtBytes) = await BatchAsync(utf8Bytes);
        var delivered = atBytes.GetProperty("documents").EnumerateArray().ToArray();
        Assert.True(delivered[0].GetProperty("contentIncluded").GetBoolean());
        Assert.False(delivered[1].GetProperty("contentIncluded").GetBoolean());
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(utf8Bytes), delivered[1].GetProperty("notice").GetString());
        var pdfBlocks = blocksAtBytes.EnumerateArray().SkipLast(1).ToArray();
        Assert.Contains(pdfBlocks, block => block.GetProperty("text").GetString()!.Contains('£', StringComparison.Ordinal));
        Assert.Equal(utf8Bytes, pdfBlocks.Sum(block => Encoding.UTF8.GetByteCount(block.GetProperty("text").GetString()!)));

        async Task<(JsonElement Structured, JsonElement Blocks)> BatchAsync(int maxInlineBytes)
        {
            using var batch = await AutomationMcpTestSupport.PostMcpAsync(
                client,
                token,
                AutomationMcpTestSupport.ToolCallPayload(
                    7,
                    "pegasus_document_download_batch",
                    new
                    {
                        caseId,
                        maxInlineBytes,
                        documents = new[]
                        {
                            new { occurrenceId = pdfOccurrence, versionId = pdfVersion },
                            new { occurrenceId = imageOccurrence, versionId = imageVersion }
                        }
                    }));
            var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(batch);
            return (structured, blocks);
        }
    }

    /// <summary>
    /// The walk at a limit the photograph cannot fit: the PDF comes back, the
    /// undecodable image is withheld with its reason and does not stop the
    /// walk (R211), the photograph for which no quality fits stops it, and
    /// the text file after it is withheld unread. Every withheld item keeps
    /// its whole line, and only the PDF, the one item handed back, has a
    /// <c>document_downloaded</c> row.
    /// </summary>
    [Fact]
    public async Task BatchDownloadRecordsRowsOnlyForDeliveredItems()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var pdf = IntakeTestEvidence.CreatePdf("Estimate total 1234.56 pounds for the nearside door.");
        var (pdfOccurrence, pdfVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 0, "estimate.pdf", "application/pdf", pdf, "Other", "mcp:rows-pdf");
        var (brokenOccurrence, brokenVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 1, "broken.jpg", "image/jpeg", "not an image at all"u8.ToArray(), "Other", "mcp:rows-broken");
        var (photoOccurrence, photoVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 2, "photo.jpg", "image/jpeg", NoisyJpeg(900, 700), "Image", "mcp:rows-photo");
        var (noteOccurrence, noteVersion) = await AddWithoutLeaseAsync(
            client, token, caseId, 3, "note.txt", "text/plain", "after the stop"u8.ToArray(), "Other", "mcp:rows-note");
        const int inlineLimit = 2000;

        using var batch = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                9,
                "pegasus_document_download_batch",
                new
                {
                    caseId,
                    maxInlineBytes = inlineLimit,
                    documents = new[]
                    {
                        new { occurrenceId = pdfOccurrence, versionId = pdfVersion },
                        new { occurrenceId = brokenOccurrence, versionId = brokenVersion },
                        new { occurrenceId = photoOccurrence, versionId = photoVersion },
                        new { occurrenceId = noteOccurrence, versionId = noteVersion }
                    }
                }));
        var (blocks, structured) = await AutomationMcpTestSupport.ReadToolResultAsync(batch);
        var documents = structured.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(
            ["estimate.pdf", "broken.jpg", "photo.jpg", "note.txt"],
            documents.Select(item => item.GetProperty("fileName").GetString()!).ToArray());
        Assert.Equal(
            [true, false, false, false],
            documents.Select(item => item.GetProperty("contentIncluded").GetBoolean()).ToArray());
        Assert.Equal(AutomationFileContent.UndecodableImageNotice, documents[1].GetProperty("notice").GetString());
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(inlineLimit), documents[2].GetProperty("notice").GetString());
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, documents[3].GetProperty("notice").GetString());

        // The PDF's header and page, then one whole line per withheld item,
        // each ending at its own contentUrl, in request order.
        var content = blocks.EnumerateArray().ToArray();
        Assert.All(content, block => Assert.Equal("text", block.GetProperty("type").GetString()));
        Assert.Equal(5, content.Length);
        Assert.Contains("Estimate total 1234.56", content[1].GetProperty("text").GetString(), StringComparison.Ordinal);
        for (var index = 1; index < documents.Length; index++)
        {
            var line = content[index + 1].GetProperty("text").GetString()!;
            Assert.StartsWith($"{documents[index].GetProperty("fileName").GetString()} (", line, StringComparison.Ordinal);
            Assert.EndsWith($"contentUrl: {documents[index].GetProperty("contentUrl").GetString()}", line, StringComparison.Ordinal);
        }
        Assert.True(InlineContentBytes(content) <= inlineLimit);

        var operationKey = structured.GetProperty("operationKey").GetString();
        Assert.Equal(
            $"{operationKey}:0",
            await factory.Database.ScalarAsync<string>(
                $"""
                SELECT STRING_AGG(CorrelationId, N',') FROM ActionHistory
                WHERE EventKind = N'document_downloaded'
                  AND CorrelationId LIKE N'{operationKey}:%'
                """));
    }

    /// <summary>
    /// An image already smaller than the renderer's floor edge is rendered at
    /// its own size, not refused as if no quality fitted, so it cannot stop a
    /// batch it fits (R211). The renderer's floor in bytes is a rendering it
    /// can give: one grey pixel renders at it, and a byte less fits nothing.
    /// </summary>
    [Fact]
    public async Task ASmallImageRendersAtItsOwnSizeAndTheByteFloorIsARendering()
    {
        var renderer = new ImageDeliveryRenderer();

        var small = await renderer.RenderAsync(
            NoisyJpeg(200, 150),
            AutomationFileContent.LongestEdge,
            AutomationFileContent.DefaultInlineBytes,
            CancellationToken.None);
        var image = Assert.IsType<DeliveryImage>(small.Image);
        Assert.Equal((200, 150, false), (image.Width, image.Height, image.Downscaled));

        var pixel = GreyPixelPng();
        var atFloor = await renderer.RenderAsync(
            pixel, AutomationFileContent.LongestEdge, renderer.MinimumBytes, CancellationToken.None);
        Assert.NotNull(atFloor.Image);
        var belowFloor = await renderer.RenderAsync(
            pixel, AutomationFileContent.LongestEdge, renderer.MinimumBytes - 1, CancellationToken.None);
        Assert.Equal(DeliveryImageRefusal.NoQualityFits, belowFloor.Refusal);
    }

    /// <summary>
    /// One item that is not the Case's confirmed current version refuses the
    /// whole batch beside a valid one: nothing is opened and no
    /// <c>document_downloaded</c> row is written (19 October 2026).
    /// </summary>
    [Fact]
    public async Task BatchDownloadFailsClosedWhenOneItemIsUnknownUnconfirmedRemovedOrFromAnotherCase()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDocumentContentStore>();
                services.AddSingleton(provider =>
                    new CountingDocumentContentStore(provider.GetRequiredService<LocalDocumentContentStore>()));
                services.AddSingleton<IDocumentContentStore>(provider =>
                    provider.GetRequiredService<CountingDocumentContentStore>());
            }));
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var valid = await AddWithoutLeaseAsync(
            client, token, caseId, 0, "valid.txt", "text/plain", "valid"u8.ToArray(), "Other", "mcp:closed-valid");
        var unconfirmed = await AddWithoutLeaseAsync(
            client, token, caseId, 1, "pending.txt", "text/plain", "pending"u8.ToArray(), "Other", "mcp:closed-pending");
        var removed = await AddWithoutLeaseAsync(
            client, token, caseId, 2, "removed.txt", "text/plain", "removed"u8.ToArray(), "Other", "mcp:closed-removed");
        var otherCaseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        var foreign = await AddWithoutLeaseAsync(
            client,
            token,
            otherCaseId,
            await AutomationMcpTestSupport.GetWorkflowVersionAsync(mcpFactory, otherCaseId),
            "foreign.txt",
            "text/plain",
            "foreign"u8.ToArray(),
            "Other",
            "mcp:closed-foreign");
        await factory.Database.ExecuteAsync(
            $"UPDATE DocumentVersions SET CustodyStatus = N'Pending' WHERE Id = '{unconfirmed.VersionId:D}'");
        await factory.Database.ExecuteAsync(
            $"UPDATE DocumentVersions SET IsLogicallyRemoved = 1, IsCurrent = 0 WHERE Id = '{removed.VersionId:D}'");
        var store = mcpFactory.Services.GetRequiredService<CountingDocumentContentStore>();
        var opensBefore = store.Opens;

        (Guid OccurrenceId, Guid VersionId)[] refused = [(Guid.NewGuid(), Guid.NewGuid()), unconfirmed, removed, foreign];
        for (var index = 0; index < refused.Length; index++)
        {
            using var response = await AutomationMcpTestSupport.PostMcpAsync(
                client,
                token,
                AutomationMcpTestSupport.ToolCallPayload(
                    20 + index,
                    "pegasus_document_download_batch",
                    new
                    {
                        caseId,
                        documents = new[]
                        {
                            new { occurrenceId = valid.OccurrenceId, versionId = valid.VersionId },
                            new { occurrenceId = refused[index].OccurrenceId, versionId = refused[index].VersionId }
                        }
                    }));
            using var refusal = await AutomationMcpTestSupport.ReadJsonRpcAsync(response);
            Assert.Contains(
                "The document version was not found.",
                refusal.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(opensBefore, store.Opens);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE EventKind = N'document_downloaded'"));
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

        using (var emptyBatch = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                74,
                "pegasus_document_download_batch",
                new { caseId = Guid.NewGuid(), documents = Array.Empty<object>() })))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(emptyBatch)).RootElement.ToString();
            Assert.Contains("Between 1 and 25 documents are required", body, StringComparison.OrdinalIgnoreCase);
        }

        using (var oversizeBatch = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                75,
                "pegasus_document_download_batch",
                new
                {
                    caseId = Guid.NewGuid(),
                    documents = Enumerable.Range(0, 26)
                        .Select(_ => new { occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() })
                        .ToArray()
                })))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(oversizeBatch)).RootElement.ToString();
            Assert.Contains("Between 1 and 25 documents are required", body, StringComparison.OrdinalIgnoreCase);
        }

        var duplicateOccurrence = Guid.NewGuid();
        var duplicateVersion = Guid.NewGuid();
        using (var duplicateBatch = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                76,
                "pegasus_document_download_batch",
                new
                {
                    caseId = Guid.NewGuid(),
                    documents = new[]
                    {
                        new { occurrenceId = duplicateOccurrence, versionId = duplicateVersion },
                        new { occurrenceId = duplicateOccurrence, versionId = duplicateVersion }
                    }
                })))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(duplicateBatch)).RootElement.ToString();
            Assert.Contains("Each document version may be named once", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(6, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind IN (N'pegasus_document_add', N'pegasus_document_download', N'pegasus_document_download_batch')
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
                new { caseId = Guid.NewGuid(), occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() }),
            AutomationMcpTestSupport.ToolCallPayload(
                82,
                "pegasus_document_download_batch",
                new
                {
                    caseId = Guid.NewGuid(),
                    documents = new[] { new { occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() } }
                })
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

    /// <summary>
    /// What the content array costs at its encoded size, as the batch charges
    /// it (R192): text at its UTF-8 bytes, an image block at its base64 length.
    /// </summary>
    private static int InlineContentBytes(IEnumerable<JsonElement> blocks)
    {
        var total = 0;
        foreach (var block in blocks)
        {
            switch (block.GetProperty("type").GetString())
            {
                case "text":
                    total += Encoding.UTF8.GetByteCount(block.GetProperty("text").GetString() ?? string.Empty);
                    break;
                case "image":
                    total += block.GetProperty("data").GetString()!.Length;
                    break;
            }
        }

        return total;
    }

    /// <summary>
    /// The bound the batch offers an image (R192): what remains after its
    /// summary and the longest caption tail an image can carry, less
    /// base64's four bytes for three. The summary is the caption up to its tail.
    /// </summary>
    private static int ImageBound(int remaining, JsonElement caption)
    {
        var text = caption.GetProperty("text").GetString()!;
        var summary = text[..text.IndexOf(" Shown at ", StringComparison.Ordinal)];
        const string LongestTail = " Shown at 1568x1568; the original is unchanged at contentUrl.";
        return (remaining - Encoding.UTF8.GetByteCount(summary) - Encoding.UTF8.GetByteCount(LongestTail)) / 4 * 3;
    }

    private static byte[] ImageBytes(JsonElement image) =>
        Convert.FromBase64String(image.GetProperty("data").GetString()!);

    /// <summary>The local content store, counting every content open.</summary>
    private sealed class CountingDocumentContentStore(IDocumentContentStore inner) : IDocumentContentStore
    {
        private int opens;

        public int Opens => Volatile.Read(ref opens);

        public Task StoreAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            inner.StoreAsync(caseId, caseReference, versionId, content, expectedSha256, cancellationToken);

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            inner.StoreVersionAsync(address, content, expectedSha256, cancellationToken);

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            inner.StoreVersionAsync(address, content, contentLength, expectedSha256, cancellationToken);

        public Task<Stream> OpenReadAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref opens);
            return inner.OpenReadAsync(caseId, caseReference, versionId, expectedSha256, expectedLength, cancellationToken);
        }

        public Task<Stream> OpenReadVersionAsync(
            ManagedDocumentContentAddress address,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref opens);
            return inner.OpenReadVersionAsync(address, expectedSha256, expectedLength, cancellationToken);
        }

        public Task DeleteAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            CancellationToken cancellationToken) =>
            inner.DeleteAsync(caseId, caseReference, versionId, cancellationToken);
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

    /// <summary>One mid-grey pixel, losslessly, so it reaches the renderer unchanged.</summary>
    private static byte[] GreyPixelPng()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Erase(new SKColor(128, 128, 128));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
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
