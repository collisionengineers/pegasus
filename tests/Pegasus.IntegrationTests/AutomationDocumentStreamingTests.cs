using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AutomationDocumentStreamingTests
{
    [Fact]
    public async Task MetadataOnlyToolResultDoesNotOpenContent()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        var boundary = new DocumentBoundary();
        using var factory = WithBoundary(WithAutomationMcp(fixture), boundary);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.DocumentsScope);

        using var response = await PostMcpAsync(client, token, ToolCallPayload(
            1, "pegasus_document_download", new
            {
                caseId = DocumentBoundary.CaseId,
                occurrenceId = DocumentBoundary.OccurrenceId,
                versionId = DocumentBoundary.VersionId,
                maxInlineBytes = 1
            }));
        var (content, result) = await ReadToolResultAsync(response);

        Assert.False(result.GetProperty("contentIncluded").GetBoolean());
        Assert.Equal(0, boundary.ContentReads);
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("contentUrl").GetString()));
        // The one text block names the file and where its bytes are; no image or file content travels.
        var block = Assert.Single(content.EnumerateArray());
        Assert.Equal("text", block.GetProperty("type").GetString());
        Assert.Contains("evidence.txt", block.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Contains("contentUrl", block.GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamRefusesCrossCaseAndExactVersionMismatchBeforeReturningBytes()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        var boundary = new DocumentBoundary { RefuseExactVersion = true };
        using var factory = WithBoundary(WithAutomationMcp(fixture), boundary);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.DocumentsScope);

        using var crossCase = Request(
            $"/automation/documents/{DocumentBoundary.OccurrenceId:D}/versions/{DocumentBoundary.VersionId:D}?caseId={Guid.NewGuid():D}",
            token);
        using var crossCaseResponse = await client.SendAsync(crossCase);
        Assert.Equal(HttpStatusCode.NotFound, crossCaseResponse.StatusCode);
        Assert.Equal(0, boundary.ContentReads);

        using var mismatch = Request(
            $"/automation/documents/{DocumentBoundary.OccurrenceId:D}/versions/{DocumentBoundary.VersionId:D}?caseId={DocumentBoundary.CaseId:D}",
            token);
        using var mismatchResponse = await client.SendAsync(mismatch);
        Assert.Equal(HttpStatusCode.InternalServerError, mismatchResponse.StatusCode);
        Assert.Equal(1, boundary.ContentReads);
        Assert.DoesNotContain("stream-boundary", await mismatchResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> WithBoundary(
        WebApplicationFactory<Program> factory,
        DocumentBoundary boundary) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGetCaseDocumentMetadata>();
            services.RemoveAll<IReadLogicalDocumentVersion>();
            services.AddSingleton<IGetCaseDocumentMetadata>(boundary);
            services.AddSingleton<IReadLogicalDocumentVersion>(boundary);
        }));

    private static HttpRequestMessage Request(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed class DocumentBoundary : IGetCaseDocumentMetadata, IReadLogicalDocumentVersion
    {
        public static readonly Guid CaseId = Guid.Parse("d5d5c822-d6f2-4bd4-b243-e27265879c28");
        public static readonly Guid OccurrenceId = Guid.Parse("7b153b14-2dcb-49b1-a64c-aab1a89772a1");
        public static readonly Guid DocumentId = Guid.Parse("df2e8959-ad1d-45a5-9bd1-afd698dbb55e");
        public static readonly Guid VersionId = Guid.Parse("0f324523-ed74-493e-8afc-ddf0ba47d6d3");
        private static readonly byte[] Bytes = "stream-boundary"u8.ToArray();
        private static readonly string Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Bytes));

        public int ContentReads { get; private set; }
        public bool RefuseExactVersion { get; init; }

        public Task<CaseDocumentMetadata?> ExecuteAsync(GetCaseDocumentMetadataQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<CaseDocumentMetadata?>(
                query.CaseId == CaseId && query.OccurrenceId == OccurrenceId && query.VersionId == VersionId
                    ? new(CaseId, OccurrenceId, DocumentId, VersionId, "evidence.txt", "text/plain", Bytes.Length, Hash)
                    : null);

        public Task<LogicalDocumentContent> OpenAsync(ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            ContentReads++;
            if (RefuseExactVersion)
            {
                throw new InvalidDataException("The exact immutable content version does not match.");
            }
            Assert.Equal(CaseId, request.CaseId);
            Assert.Equal(DocumentId, request.DocumentId);
            Assert.Equal(VersionId, request.VersionId);
            Assert.Equal(Hash, request.ExpectedSha256);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(Bytes, writable: false), DocumentId, VersionId, null,
                Hash, Bytes.Length, "evidence.txt", "text/plain"));
        }
    }
}
