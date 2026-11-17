using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;
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

    /// <summary>
    /// A content route is a machine surface: a caller without the bearer gets
    /// the bare challenge, not the HTML status page, and the refusal is a
    /// transport-denial security event as it is on <c>/mcp</c>.
    /// </summary>
    [Fact]
    public async Task AnUnauthenticatedStreamIsChallengedWithoutTheStatusPage()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        using var factory = WithAutomationMcp(fixture);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/automation/documents/{DocumentBoundary.OccurrenceId:D}/versions/{DocumentBoundary.VersionId:D}?caseId={DocumentBoundary.CaseId:D}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(
            "<html",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await (await fixture.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_access_denied' AND Outcome = N'Denied'
            """));
    }

    /// <summary>
    /// Stop refuses a token issued before it: on a content route the refusal is
    /// a 403 with the resolver's security event, and no bytes are read. That
    /// event is the only one the refusal writes (R1097).
    /// </summary>
    [Fact]
    public async Task AStoppedClientsTokenIsForbiddenTheStream()
    {
        using var fixture = new IntakeWebApplicationFactory(TimeProvider.System);
        var boundary = new DocumentBoundary();
        using var factory = WithBoundary(WithAutomationMcp(fixture), boundary);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(
            client, $"{AutomationMcp.DocumentsScope} {AutomationMcp.IntakeScope}");
        using (var scope = factory.Services.CreateScope())
        {
            var stopped = await scope.ServiceProvider
                .GetRequiredService<AutomationClientRegistry>()
                .SetEnabledAsync(
                    enabled: false,
                    ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                    "Integration-test stop",
                    Guid.NewGuid().ToString("N"),
                    CancellationToken.None);
            Assert.False(stopped.IsEnabled);
        }

        using var request = Request(
            $"/automation/documents/{DocumentBoundary.OccurrenceId:D}/versions/{DocumentBoundary.VersionId:D}?caseId={DocumentBoundary.CaseId:D}",
            token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, boundary.ContentReads);
        Assert.Equal(1, await (await fixture.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_client_disabled'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
        Assert.Equal(0, await (await fixture.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_access_denied'
            """));

        // The intake-source route refuses the stopped client the same way.
        using var sourceRequest = Request($"/automation/intake-sources/{Guid.NewGuid():D}", token);
        using var sourceResponse = await client.SendAsync(sourceRequest);

        Assert.Equal(HttpStatusCode.Forbidden, sourceResponse.StatusCode);
        Assert.Equal(2, await (await fixture.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_client_disabled'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
        Assert.Equal(0, await (await fixture.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_access_denied'
            """));
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
