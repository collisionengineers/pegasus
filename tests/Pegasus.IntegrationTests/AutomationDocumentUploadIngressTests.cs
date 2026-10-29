using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Pegasus.Core.Documents;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// R203 over real HTTP: a document too large for one argument is staged in
/// parts and added by <c>pegasus_document_add</c>
/// with its <c>uploadId</c>. Custody is written only by the add, with the
/// declared SHA-256; staging writes no history row; a missing, short or
/// damaged part leaves the Case's documents unchanged; a refused add leaves
/// the upload for the retry.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationDocumentUploadIngressTests
{
    [Fact]
    public async Task AnUploadOfEachMeasuredSizeAddsTheDocumentWithTheDeclaredSha256()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var (_, leaseToken) = await BeginEditAsync(client, token, caseId, expectedVersion: 0, rpcId: 1);

        // The two PDFs the issue measured, as deterministic bytes of their sizes:
        // 7 and 30 parts of at most 65,536 base64 characters.
        foreach (var (length, partCount) in new[] { (342_499, 7), (1_459_873, 30) })
        {
            var document = Deterministic(length);
            var sha256 = Sha256Hex(document);
            Assert.Equal(partCount, DocumentUploadLimits.PartCount(length));
            var uploadId = await StageUploadAsync(client, token, document, $"mcp:upload-measured-{length}");
            var arguments = AddArguments(
                caseId,
                uploadId,
                await GetWorkflowVersionAsync(mcpFactory, caseId),
                leaseToken,
                $"mcp:upload-add-{length}");

            var added = await CallAsync(client, token, "pegasus_document_add", arguments);
            Assert.False(added.GetProperty("isReplay").GetBoolean());
            Assert.Equal(sha256, added.GetProperty("sha256").GetString(), ignoreCase: true);
            Assert.Equal(length, added.GetProperty("contentLength").GetInt64());
            var occurrenceId = added.GetProperty("occurrenceId").GetGuid();
            var versionId = added.GetProperty("versionId").GetGuid();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/automation/documents/{occurrenceId:D}/versions/{versionId:D}?caseId={caseId:D}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var original = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, original.StatusCode);
            Assert.Equal(document, await original.Content.ReadAsByteArrayAsync());

            // The staged bytes stay for the upload's life, so a replay of the
            // add returns the original custody record.
            var replay = await CallAsync(client, token, "pegasus_document_add", arguments);
            Assert.True(replay.GetProperty("isReplay").GetBoolean());
            Assert.Equal(versionId, replay.GetProperty("versionId").GetGuid());
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM DocumentVersions v JOIN CaseDocuments d ON d.Id = v.DocumentId
            WHERE d.CaseId = '{caseId:D}'
            """));
        Assert.Equal(4, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'pegasus_document_add' AND Outcome = N'Succeeded'
            """));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind IN (N'pegasus_upload_begin', N'pegasus_upload_part')
            """));
    }

    [Fact]
    public async Task AnIncompleteOrCorruptedUploadIsRefusedAndAddsNothing()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var document = Deterministic(DocumentUploadLimits.PartBytes + 100);
        var first = document[..DocumentUploadLimits.PartBytes];
        var last = document[DocumentUploadLimits.PartBytes..];
        var begun = await CallAsync(
            client,
            token,
            "pegasus_upload_begin",
            new { totalBytes = document.Length, sha256 = Sha256Hex(document), operationKey = "mcp:upload-incomplete" });
        var uploadId = begun.GetProperty("uploadId").GetGuid();
        await CallAsync(client, token, "pegasus_upload_part", PartArguments(uploadId, 1, first, Sha256Hex(first)));

        // A missing part: the add names it and writes nothing.
        Assert.Contains(
            DocumentUploadRefusals.MissingParts([2]),
            await RefusedAsync(client, token, "pegasus_document_add", AddArguments(caseId, uploadId, 0, null, "mcp:upload-incomplete-add")),
            StringComparison.Ordinal);

        // A short part, and a part whose bytes were damaged after hashing,
        // are refused before anything is stored.
        Assert.Contains(
            DocumentUploadRefusals.PartLength(2, last.Length),
            await RefusedAsync(client, token, "pegasus_upload_part", PartArguments(uploadId, 2, last[1..], Sha256Hex(last[1..]))),
            StringComparison.Ordinal);
        var damaged = last.ToArray();
        damaged[0] ^= 0xFF;
        Assert.Contains(
            DocumentUploadRefusals.PartSha256Mismatch,
            await RefusedAsync(client, token, "pegasus_upload_part", PartArguments(uploadId, 2, damaged, Sha256Hex(last))),
            StringComparison.Ordinal);
        Assert.Contains(
            DocumentUploadRefusals.MissingParts([2]),
            await RefusedAsync(client, token, "pegasus_document_add", AddArguments(caseId, uploadId, 0, null, "mcp:upload-incomplete-add")),
            StringComparison.Ordinal);

        // Inline bytes beside an uploadId are refused too.
        var both = new
        {
            caseId,
            fileName = "upload.pdf",
            mediaType = "application/pdf",
            semanticRole = "Other",
            expectedCaseVersion = 0,
            operationKey = "mcp:upload-both",
            contentBase64 = Convert.ToBase64String(first),
            uploadId
        };
        Assert.Contains(
            "Exactly one of contentBase64 and uploadId",
            await RefusedAsync(client, token, "pegasus_document_add", both),
            StringComparison.Ordinal);

        Assert.Equal(0, await CaseDocumentRowsAsync(factory, caseId));
        Assert.Equal(0L, await GetWorkflowVersionAsync(mcpFactory, caseId));
    }

    [Fact]
    public async Task ARefusedAddLeavesTheUploadSoTheRetrySucceeds()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var document = Deterministic(DocumentUploadLimits.PartBytes * 2 + 1);
        var uploadId = await StageUploadAsync(client, token, document, "mcp:upload-retry");

        // A stale Case version refuses the add's lease; nothing is written and
        // the staged parts remain.
        Assert.Contains(
            "changed since it was read",
            await RefusedAsync(client, token, "pegasus_document_add", AddArguments(caseId, uploadId, 99, null, "mcp:upload-retry-stale")),
            StringComparison.Ordinal);
        Assert.Equal(0, await CaseDocumentRowsAsync(factory, caseId));

        var added = await CallAsync(
            client,
            token,
            "pegasus_document_add",
            AddArguments(caseId, uploadId, 0, null, "mcp:upload-retry-add"));
        Assert.Equal(Sha256Hex(document), added.GetProperty("sha256").GetString(), ignoreCase: true);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND Source = N'Automation'"));
    }

    [Fact]
    public async Task ABadBeginIsRefusedAndWritesNothing()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var sha256 = new string('a', 64);

        Assert.Contains(
            DocumentUploadRefusals.TotalBytes,
            await RefusedAsync(client, token, "pegasus_upload_begin", new { totalBytes = 0, sha256, operationKey = "mcp:upload-bad-empty" }),
            StringComparison.Ordinal);
        Assert.Contains(
            DocumentUploadRefusals.TotalBytes,
            await RefusedAsync(
                client,
                token,
                "pegasus_upload_begin",
                new { totalBytes = DocumentUploadLimits.MaximumContentBytes + 1, sha256, operationKey = "mcp:upload-bad-large" }),
            StringComparison.Ordinal);
        Assert.Contains(
            DocumentUploadRefusals.Sha256,
            await RefusedAsync(client, token, "pegasus_upload_begin", new { totalBytes = 10, sha256 = "abc", operationKey = "mcp:upload-bad-hash" }),
            StringComparison.Ordinal);

        // A part for an upload never begun is refused without saying why.
        Assert.Contains(
            DocumentUploadRefusals.Unavailable,
            await RefusedAsync(client, token, "pegasus_upload_part", PartArguments(Guid.NewGuid(), 1, [1], Sha256Hex([1]))),
            StringComparison.Ordinal);

        // Each refusal is history; the begin under a refused key stores nothing,
        // so the same key with good details then begins afresh.
        Assert.Equal(4, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind IN (N'pegasus_upload_begin', N'pegasus_upload_part') AND Outcome = N'Failed'
            """));
        var begun = await CallAsync(
            client,
            token,
            "pegasus_upload_begin",
            new { totalBytes = 10, sha256, operationKey = "mcp:upload-bad-hash" });
        Assert.False(begun.GetProperty("isReplay").GetBoolean());
        Assert.Equal(0, begun.GetProperty("receivedParts").GetArrayLength());
    }

    private static byte[] Deterministic(int length)
    {
        var bytes = new byte[length];
        new Random(length).NextBytes(bytes);
        return bytes;
    }

    private static object AddArguments(
        Guid caseId,
        Guid uploadId,
        long expectedCaseVersion,
        string? editLeaseToken,
        string operationKey) =>
        new
        {
            caseId,
            fileName = "upload.pdf",
            mediaType = "application/pdf",
            semanticRole = "Other",
            expectedCaseVersion,
            operationKey,
            uploadId,
            editLeaseToken
        };

    private static object PartArguments(Guid uploadId, int partNumber, byte[] part, string sha256) =>
        new { uploadId, partNumber, contentBase64 = Convert.ToBase64String(part), sha256 };

    private static async Task<JsonElement> CallAsync(HttpClient client, string token, string tool, object arguments)
    {
        using var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(1, tool, arguments));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadStructuredContentAsync(response);
    }

    private static async Task<string> RefusedAsync(HttpClient client, string token, string tool, object arguments)
    {
        using var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(1, tool, arguments));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadErrorTextAsync(response);
    }

    private static Task<int> CaseDocumentRowsAsync(IntakeWebApplicationFactory factory, Guid caseId) =>
        factory.Database.ScalarAsync<int>(
            $"""
            SELECT (SELECT COUNT(*) FROM CaseDocuments WHERE CaseId = '{caseId:D}')
                 + (SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}')
                 + (SELECT COUNT(*) FROM DocumentVersions v JOIN CaseDocuments d ON d.Id = v.DocumentId
                    WHERE d.CaseId = '{caseId:D}')
            """);
}
