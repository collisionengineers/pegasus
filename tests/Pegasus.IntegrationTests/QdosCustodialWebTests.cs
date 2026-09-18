using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class QdosCustodialWebTests
{
    [Fact]
    public async Task CanonicalCaseWorkspaceUsesTheAuthenticatedOfflineStaffSession()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Search");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Filter cases", body, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CanonicalDownloadOwnerCallsCoreAndReturnsVerifiedSafeMetadata()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var handlers = new RecordingDocumentHandlers();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDownloadCaseDocument>();
                services.AddSingleton<IDownloadCaseDocument>(handlers);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(
            $"/Cases/{handlers.CaseId:D}/Documents/{handlers.OccurrenceId:D}/Download?versionId={handlers.VersionId:D}");
        var content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(handlers.Payload, content);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("engineer-report.pdf", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        // The evidence viewer added an inline disposition to this route. Until then no test
        // pinned the disposition *type*, so a default flipped to inline would
        // have shipped green. This is the assertion that makes the additive
        // claim checkable.
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(response.Headers.TryGetValues("X-Content-SHA256", out var hashes));
        Assert.Equal(handlers.Sha256, Assert.Single(hashes));
        var query = Assert.Single(handlers.Downloads);
        Assert.Equal(handlers.CaseId, query.CaseId);
        Assert.Equal(handlers.OccurrenceId, query.OccurrenceId);
        Assert.Equal(handlers.VersionId, query.VersionId);
        Assert.Equal(ActorKind.Staff, query.Actor.Kind);
        Assert.Equal(StaffRole.Administrator, Assert.Single(query.Actor.Roles));
        Assert.True(StaffAuthorization.IsAuthorized(query.Actor, StaffAccessRight.PerformCasework));
        const string downloadOperationPrefix = "web-download:";
        Assert.StartsWith(downloadOperationPrefix, query.OperationKey, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(
            query.OperationKey[downloadOperationPrefix.Length..],
            "N",
            out _));

        var wrongCaseId = Guid.NewGuid();
        using var denied = await client.GetAsync(
            $"/Cases/{wrongCaseId:D}/Documents/{handlers.OccurrenceId:D}/Download?versionId={handlers.VersionId:D}");
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain(handlers.OccurrenceId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(handlers.VersionId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InlineFlagPreviewsOnlyContentABrowserWillNotExecute()
    {
        // The preview and the download are the same authorised read
        // through the same use case; only the disposition differs. A case
        // document is arbitrary operator-supplied content, so retained HTML
        // served inline from this origin would execute as same-origin script --
        // the flag therefore allowlists what a browser renders inertly. One
        // route observed under four media types, so one host and one restored
        // database serve them all.
        using var baseFactory = new IntakeWebApplicationFactory();
        var handlers = new RecordingDocumentHandlers();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDownloadCaseDocument>();
                services.AddSingleton<IDownloadCaseDocument>(handlers);
                services.RemoveAll<IReadCaseDocumentPreview>();
                services.AddSingleton<IReadCaseDocumentPreview>(handlers);
                services.RemoveAll<IReadLogicalDocumentVersion>();
                services.AddSingleton<IReadLogicalDocumentVersion>(handlers);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var route = $"/Cases/{handlers.CaseId:D}/Documents/{handlers.OccurrenceId:D}/Download"
            + $"?versionId={handlers.VersionId:D}";

        using (var preview = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Equal("inline", preview.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Contains(
                "engineer-report.pdf",
                preview.Content.Headers.ContentDisposition?.ToString(),
                StringComparison.Ordinal);
            Assert.Equal("application/pdf", preview.Content.Headers.ContentType?.MediaType);
            Assert.Equal(handlers.Payload, await preview.Content.ReadAsByteArrayAsync());
            Assert.Equal("nosniff", Assert.Single(preview.Headers.GetValues("X-Content-Type-Options")));
        }

        handlers.MediaType = "image/jpeg";
        handlers.FileName = "damage.jpg";
        using (var preview = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal("inline", preview.Content.Headers.ContentDisposition?.DispositionType);
        }

        // Not on the allowlist: the flag is ignored and the save disposition
        // stands, so retained markup can never render from this origin.
        handlers.MediaType = "text/html";
        handlers.FileName = "statement.html";
        using (var refused = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Equal("attachment", refused.Content.Headers.ContentDisposition?.DispositionType);
        }

        // SVG is image/*, but it executes script when navigated to -- and the
        // document link this route serves is navigable with no script or on a
        // middle-click. It is excluded from the allowlist for that reason, so
        // an operator-supplied SVG can never render from this origin.
        handlers.MediaType = "image/svg+xml";
        handlers.FileName = "diagram.svg";
        using (var refused = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Equal("attachment", refused.Content.Headers.ContentDisposition?.DispositionType);
        }

        // And the flag is opt-in: absent, a previewable type still downloads.
        handlers.MediaType = "application/pdf";
        handlers.FileName = "engineer-report.pdf";
        using (var plain = await client.GetAsync(route))
        {
            Assert.Equal("attachment", plain.Content.Headers.ContentDisposition?.DispositionType);
        }
    }

    private sealed class RecordingDocumentHandlers :
        IDownloadCaseDocument,
        IExportCaseDocuments,
        IReadCaseDocumentPreview,
        IReadLogicalDocumentVersion
    {
        public string MediaType { get; set; } = "application/pdf";

        public string FileName { get; set; } = "engineer-report.pdf";

        public Guid CaseId { get; } = Guid.NewGuid();

        public Guid OccurrenceId { get; } = Guid.NewGuid();

        public Guid DocumentId { get; } = Guid.NewGuid();

        public Guid VersionId { get; } = Guid.NewGuid();

        public byte[] Payload { get; } = Encoding.UTF8.GetBytes("verified report content");

        public byte[] ExportPayload { get; } = Encoding.UTF8.GetBytes("deterministic archive bytes");

        public string Sha256 { get; } = new('a', 64);

        public List<DownloadCaseDocumentQuery> Downloads { get; } = [];

        public List<ExportCaseDocumentsCommand> Exports { get; } = [];

        public Task<DocumentDownload?> ExecuteAsync(
            DownloadCaseDocumentQuery query,
            CancellationToken cancellationToken = default)
        {
            Downloads.Add(query);
            if (query.CaseId != CaseId
                || query.OccurrenceId != OccurrenceId
                || query.VersionId != VersionId)
            {
                return Task.FromResult<DocumentDownload?>(null);
            }

            return Task.FromResult<DocumentDownload?>(
                new(
                    new MemoryStream(Payload, writable: false),
                    FileName,
                    MediaType,
                    Payload.Length,
                    Sha256));
        }

        public Task<DocumentExport> ExecuteAsync(
            ExportCaseDocumentsCommand command,
            CancellationToken cancellationToken = default)
        {
            Exports.Add(command);
            var selection = Assert.Single(command.Selections);
            return Task.FromResult(
                new DocumentExport(
                    new MemoryStream(ExportPayload, writable: false),
                    "case-export.zip",
                    [
                        new(
                            "engineer-report.pdf",
                            selection.OccurrenceId,
                            selection.VersionId,
                            DocumentSemanticRole.EngineerReport,
                            Payload.Length,
                            Sha256)
                    ]));
        }

        public Task<CaseDocumentPreview?> ExecuteAsync(
            CaseDocumentPreviewQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query.CaseId != CaseId
                || query.OccurrenceId != OccurrenceId
                || query.VersionId != VersionId)
            {
                return Task.FromResult<CaseDocumentPreview?>(null);
            }

            return Task.FromResult<CaseDocumentPreview?>(
                new(
                    CaseId,
                    OccurrenceId,
                    DocumentId,
                    VersionId,
                    FileName,
                    MediaType,
                    Payload.Length,
                    Sha256,
                    DocumentCustodyStatus.Confirmed));
        }

        Task<LogicalDocumentContent> IReadLogicalDocumentVersion.OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(Payload, writable: false),
                DocumentId,
                VersionId,
                null,
                Sha256,
                Payload.Length,
                FileName,
                MediaType));
    }

}
