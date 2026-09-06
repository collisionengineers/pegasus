using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
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
        var result = await ReadStructuredContentAsync(response);

        Assert.False(result.GetProperty("contentIncluded").GetBoolean());
        Assert.Equal(0, boundary.ContentReads);
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("contentUrl").GetString()));
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

    [Fact]
    public async Task ExportStreamRequiresDocumentScopeAndStreamsSequentialArchive()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        var boundary = new DocumentBoundary();
        using var factory = WithBoundary(WithAutomationMcp(fixture), boundary);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.DocumentsScope);
        var grantId = ClientId;
        var ticket = AutomationDocumentStreaming.ProtectExport(
            factory.Services.GetRequiredService<IDataProtectionProvider>(),
            new(
                DocumentBoundary.CaseId,
                [new(DocumentBoundary.OccurrenceId, DocumentBoundary.VersionId)],
                7,
                "lease-token",
                "mcp:document-export-stream",
                grantId,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        var url = "/automation/document-exports" + QueryString.Create("ticket", ticket);
        var wrongScope = await RequestTokenAsync(client, AutomationMcp.CasesScope);
        using var denied = Request(url, wrongScope);
        using var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        Assert.Equal(0, boundary.ExportReads);

        using var request = Request(url, token);
        request.Headers.Range = new RangeHeaderValue(0, 2);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("export-archive", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, boundary.ExportReads);
        Assert.Equal(7, boundary.LastExportCommand?.ExpectedCaseVersion);
        Assert.Equal("lease-token", boundary.LastExportCommand?.EditLeaseToken);
    }

    [Fact]
    public async Task MaximumExportTicketFitsRequestLineAndTamperingIsNotDisclosed()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        var boundary = new DocumentBoundary();
        using var factory = WithBoundary(WithAutomationMcp(fixture), boundary);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.DocumentsScope);
        var selections = Enumerable.Range(0, AutomationDocumentStreaming.MaximumExportSelections)
            .Select(_ => new DocumentExportSelection(Guid.NewGuid(), Guid.NewGuid()))
            .ToArray();
        var protectedTicket = AutomationDocumentStreaming.ProtectExport(
            factory.Services.GetRequiredService<IDataProtectionProvider>(),
            new(
                DocumentBoundary.CaseId,
                selections,
                7,
                "lease-token",
                "mcp:document-export-maximum",
                ClientId,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        var url = "/automation/document-exports" + QueryString.Create("ticket", protectedTicket);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(url) < 8192);

        using var request = Request(url, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AutomationDocumentStreaming.MaximumExportSelections,
            boundary.LastExportCommand?.Selections.Count);

        var tamperAt = protectedTicket.Length / 2;
        var tamperedTicket = protectedTicket[..tamperAt]
            + (protectedTicket[tamperAt] == 'a' ? 'b' : 'a')
            + protectedTicket[(tamperAt + 1)..];
        var tamperedUrl = "/automation/document-exports"
            + QueryString.Create("ticket", tamperedTicket);
        using var tampered = Request(tamperedUrl, token);
        using var tamperedResponse = await client.SendAsync(tampered);
        Assert.Equal(HttpStatusCode.NotFound, tamperedResponse.StatusCode);
        Assert.Equal(1, boundary.ExportReads);

        var malformedTicket = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Pegasus.Automation.DocumentExports.v1")
            .Protect("{");
        using var malformed = Request(
            "/automation/document-exports" + QueryString.Create("ticket", malformedTicket),
            token);
        using var malformedResponse = await client.SendAsync(malformed);
        Assert.Equal(HttpStatusCode.NotFound, malformedResponse.StatusCode);
        Assert.Equal(1, boundary.ExportReads);
    }

    [Fact]
    public async Task ExportToolRejectsSelectionBeyondStreamTicketBoundBeforeCoreCall()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        var boundary = new DocumentBoundary();
        using var factory = WithBoundary(WithAutomationMcp(fixture), boundary);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.DocumentsScope);
        var selections = Enumerable.Range(0, AutomationDocumentStreaming.MaximumExportSelections + 1)
            .Select(_ => new { occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() })
            .ToArray();

        using var response = await PostMcpAsync(client, token, ToolCallPayload(
            8,
            "pegasus_document_export",
            new
            {
                caseId = DocumentBoundary.CaseId,
                selections,
                expectedCaseVersion = 7,
                editLeaseToken = "lease-token",
                operationKey = "mcp:document-export-too-many",
                maxInlineBytes = 1
            }));
        using var payload = await ReadJsonRpcAsync(response);

        Assert.Contains(
            $"At most {AutomationDocumentStreaming.MaximumExportSelections}",
            payload.RootElement.ToString(),
            StringComparison.Ordinal);
        Assert.Equal(0, boundary.ExportReads);
    }

    private static WebApplicationFactory<Program> WithBoundary(
        WebApplicationFactory<Program> factory,
        DocumentBoundary boundary) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGetCaseDocumentMetadata>();
            services.RemoveAll<IReadLogicalDocumentVersion>();
            services.RemoveAll<IExportCaseDocuments>();
            services.AddSingleton<IGetCaseDocumentMetadata>(boundary);
            services.AddSingleton<IReadLogicalDocumentVersion>(boundary);
            services.AddSingleton<IExportCaseDocuments>(boundary);
        }));

    private static HttpRequestMessage Request(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed class DocumentBoundary : IGetCaseDocumentMetadata, IReadLogicalDocumentVersion,
        IExportCaseDocuments
    {
        public static readonly Guid CaseId = Guid.Parse("d5d5c822-d6f2-4bd4-b243-e27265879c28");
        public static readonly Guid OccurrenceId = Guid.Parse("7b153b14-2dcb-49b1-a64c-aab1a89772a1");
        public static readonly Guid DocumentId = Guid.Parse("df2e8959-ad1d-45a5-9bd1-afd698dbb55e");
        public static readonly Guid VersionId = Guid.Parse("0f324523-ed74-493e-8afc-ddf0ba47d6d3");
        private static readonly byte[] Bytes = "stream-boundary"u8.ToArray();
        private static readonly string Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Bytes));

        public int ContentReads { get; private set; }
        public bool RefuseExactVersion { get; init; }
        public int ExportReads { get; private set; }
        public ExportCaseDocumentsCommand? LastExportCommand { get; private set; }

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

        public Task<DocumentExport> ExecuteAsync(
            ExportCaseDocumentsCommand command,
            CancellationToken cancellationToken = default)
        {
            ExportReads++;
            LastExportCommand = command;
            return Task.FromResult(new DocumentExport(
                new MemoryStream("export-archive"u8.ToArray(), writable: false),
                "case-documents.zip",
                [new("evidence.txt", OccurrenceId, VersionId,
                    DocumentSemanticRole.OriginalSource, Bytes.Length, Hash)]));
        }
    }
}
