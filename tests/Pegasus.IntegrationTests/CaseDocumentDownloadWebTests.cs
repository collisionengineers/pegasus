using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The one authorised document route, in its two capacities (DOCS-015).
///
/// A preview is a page element: it is cacheable by the content hash it names,
/// answers a held copy with 304, offers a derived rendering at
/// <c>size=thumb</c>, and never reaches <see cref="IDownloadCaseDocument"/> —
/// which is where the custody <c>ActionHistory</c> row is written, so not
/// reaching it is what "no history row for a preview" means here. A save is a
/// custody act: it goes through that port, is never cached, and arrives as an
/// attachment.
///
/// A read Box refused for now is a 503 with <c>Retry-After</c> rather than the
/// 404 the browser drew as a broken image.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseDocumentDownloadWebTests
{
    private static readonly Guid CaseId = Guid.Parse("6a3c1f52-9a20-4c11-9a2f-4a21c9d18c01");
    private static readonly Guid OccurrenceId = Guid.Parse("6a3c1f52-9a20-4c11-9a2f-4a21c9d18c02");
    private static readonly Guid DocumentId = Guid.Parse("6a3c1f52-9a20-4c11-9a2f-4a21c9d18c03");
    private static readonly Guid VersionId = Guid.Parse("6a3c1f52-9a20-4c11-9a2f-4a21c9d18c04");
    private const string Sha256 = "1b2c3d4e5f60718293a4b5c6d7e8f9001122334455667788990011223344556f";
    private const string FileName = "evidence.jpg";
    private const string MediaType = "image/jpeg";

    private static readonly byte[] FullContent = Encoding.UTF8.GetBytes("the full photograph");
    private static readonly byte[] ThumbnailContent = Encoding.UTF8.GetBytes("the rendering");

    [Fact]
    public async Task AnInlinePreviewIsCacheableByContentHashAndIsNotAnAuditedDownload()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute());
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FullContent, body);
        Assert.Equal(MediaType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition!.DispositionType);
        var caching = response.Headers.CacheControl!;
        Assert.True(caching.Private);
        Assert.Equal(TimeSpan.FromDays(7), caching.MaxAge);
        Assert.Contains(caching.Extensions, directive => directive.Name == "immutable");
        Assert.Equal($"\"{Sha256}\"", response.Headers.ETag!.Tag);
        Assert.False(response.Headers.ETag.IsWeak);
        Assert.Equal(1, ports.LogicalReads);
        Assert.Equal(0, ports.AuditedDownloads);
    }

    [Fact]
    public async Task APreviewTheBrowserAlreadyHoldsIsAnsweredWithNotModified()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute());
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{Sha256}\"");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Equal($"\"{Sha256}\"", response.Headers.ETag!.Tag);
        Assert.True(response.Headers.CacheControl!.Private);
        // Nothing was read: the point of the validator is that Box is not asked.
        Assert.Equal(0, ports.LogicalReads);
        Assert.Equal(0, ports.AuditedDownloads);
    }

    [Fact]
    public async Task TheThumbnailVariantServesTheDerivedRenderingUnderItsOwnValidator()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute(thumbnail: true));
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ThumbnailContent, body);
        Assert.Equal(
            CaseDocumentThumbnails.MediaType,
            response.Content.Headers.ContentType!.MediaType);
        Assert.Equal($"\"{Sha256}-thumb\"", response.Headers.ETag!.Tag);
        Assert.Equal(TimeSpan.FromDays(7), response.Headers.CacheControl!.MaxAge);
        Assert.Equal(1, ports.ThumbnailReads);
        // The tile never asked for the full photograph, which is the whole
        // point of the variant.
        Assert.Equal(0, ports.LogicalReads);
        Assert.Equal(0, ports.AuditedDownloads);
    }

    /// <summary>
    /// The fallback answers the request and nothing more. A rendering failure
    /// can be transient, so the full image it sends instead is not cached and
    /// carries no validator: the next visit asks for the rendering again rather
    /// than a browser holding the full photograph on the tile's URL for a week.
    /// </summary>
    [Fact]
    public async Task AThumbnailThatCannotBeRenderedFallsBackToTheFullImageWithoutCachingIt()
    {
        var ports = new DocumentPorts { Thumbnail = null };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute(thumbnail: true));
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FullContent, body);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.CacheControl.MaxAge);
        Assert.Null(response.Headers.ETag);
        Assert.Equal(1, ports.LogicalReads);
    }

    /// <summary>
    /// Each URL keeps its own representation. The full image's validator, which
    /// the viewer and the download hold, does not satisfy the tile's thumbnail
    /// request — that is what let one fallback pin the full image on the tile.
    /// </summary>
    [Fact]
    public async Task AThumbnailRequestIsNotSatisfiedByTheFullImageValidator()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute(thumbnail: true));
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{Sha256}\"");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ThumbnailContent, body);
        Assert.Equal($"\"{Sha256}-thumb\"", response.Headers.ETag!.Tag);
        Assert.Equal(1, ports.ThumbnailReads);
    }

    [Fact]
    public async Task AThumbnailTheBrowserAlreadyHoldsIsAnsweredWithNotModified()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute(thumbnail: true));
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{Sha256}-thumb\"");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Equal($"\"{Sha256}-thumb\"", response.Headers.ETag!.Tag);
        Assert.Equal(0, ports.ThumbnailReads);
        Assert.Equal(0, ports.LogicalReads);
    }

    [Fact]
    public async Task AThrottledContentReadIsRetriableRatherThanMissing()
    {
        var ports = new DocumentPorts
        {
            ReadFailure = new HttpRequestException(
                "Box version download returned 429.", null, HttpStatusCode.TooManyRequests)
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter!.Delta);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task AVersionStillReachingCustodyIsRetriableRatherThanMissing()
    {
        var ports = new DocumentPorts { CustodyStatus = DocumentCustodyStatus.Pending };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter!.Delta);
        Assert.Equal(0, ports.LogicalReads);
    }

    [Fact]
    public async Task AnUnknownOccurrenceIsStillMissing()
    {
        var ports = new DocumentPorts { Preview = false };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SavingADocumentStaysTheAuditedReadAndIsNeverCached()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(DownloadRoute());
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FullContent, body);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(FileName, response.Content.Headers.ContentDisposition.FileName);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.ETag);
        // One audited read, which is the ActionHistory row.
        Assert.Equal(1, ports.AuditedDownloads);
        Assert.Equal(0, ports.LogicalReads);
    }

    [Fact]
    public async Task AThrottledSaveIsRetriableRatherThanMissing()
    {
        var ports = new DocumentPorts
        {
            ReadFailure = new HttpRequestException(
                "Box version download returned 503.", null, HttpStatusCode.ServiceUnavailable)
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(DownloadRoute());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter!.Delta);
    }

    private static string DownloadRoute() =>
        $"/Cases/{CaseId:D}/Documents/{OccurrenceId:D}/Download?versionId={VersionId:D}";

    private static string PreviewRoute(bool thumbnail = false) =>
        DownloadRoute()
        + "&inline=true"
        + (thumbnail ? $"&size={CaseDocumentThumbnails.ThumbSizeToken}" : string.Empty);

    private static WebApplicationFactory<Program> CreateFactory(
        IntakeWebApplicationFactory baseFactory,
        DocumentPorts ports) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IReadCaseDocumentPreview>(services, ports);
                Substitute<IReadLogicalDocumentVersion>(services, ports);
                Substitute<IReadCaseDocumentThumbnail>(services, ports);
                Substitute<IDownloadCaseDocument>(services, ports);
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static void Substitute<T>(IServiceCollection services, T instance)
        where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(instance);
    }

    /// <summary>
    /// Every port the route reaches, in one recorder: which read was made
    /// matters as much as what came back.
    /// </summary>
    private sealed class DocumentPorts :
        IReadCaseDocumentPreview,
        IReadLogicalDocumentVersion,
        IReadCaseDocumentThumbnail,
        IDownloadCaseDocument
    {
        private int logicalReads;
        private int thumbnailReads;
        private int auditedDownloads;

        public DocumentCustodyStatus CustodyStatus { get; init; } =
            DocumentCustodyStatus.Confirmed;

        public byte[]? Thumbnail { get; init; } = ThumbnailContent;

        public Exception? ReadFailure { get; init; }

        public bool Preview { get; init; } = true;

        public int LogicalReads => Volatile.Read(ref logicalReads);

        public int ThumbnailReads => Volatile.Read(ref thumbnailReads);

        public int AuditedDownloads => Volatile.Read(ref auditedDownloads);

        Task<CaseDocumentPreview?> IReadCaseDocumentPreview.ExecuteAsync(
            CaseDocumentPreviewQuery query,
            CancellationToken cancellationToken)
        {
            Assert.Equal(CaseId, query.CaseId);
            Assert.Equal(OccurrenceId, query.OccurrenceId);
            Assert.Equal(VersionId, query.VersionId);
            return Task.FromResult(Preview
                ? new CaseDocumentPreview(
                    CaseId,
                    OccurrenceId,
                    DocumentId,
                    VersionId,
                    FileName,
                    MediaType,
                    FullContent.LongLength,
                    Sha256,
                    CustodyStatus)
                : null);
        }

        Task<LogicalDocumentContent> IReadLogicalDocumentVersion.OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref logicalReads);
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }
            Assert.Equal(DocumentId, request.DocumentId);
            Assert.Equal(VersionId, request.VersionId);
            Assert.Equal(Sha256, request.ExpectedSha256);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(FullContent, writable: false),
                DocumentId,
                VersionId,
                null,
                Sha256,
                FullContent.LongLength,
                FileName,
                MediaType));
        }

        Task<CaseDocumentThumbnail?> IReadCaseDocumentThumbnail.OpenAsync(
            CaseDocumentThumbnailRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref thumbnailReads);
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }
            Assert.Equal(Sha256, request.Sha256);
            return Task.FromResult(Thumbnail is null
                ? null
                : new CaseDocumentThumbnail(
                    new MemoryStream(Thumbnail, writable: false),
                    CaseDocumentThumbnails.MediaType,
                    Thumbnail.LongLength,
                    Sha256));
        }

        Task<DocumentDownload?> IDownloadCaseDocument.ExecuteAsync(
            DownloadCaseDocumentQuery query,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref auditedDownloads);
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }
            Assert.Equal(CaseId, query.CaseId);
            Assert.False(string.IsNullOrWhiteSpace(query.OperationKey));
            return Task.FromResult<DocumentDownload?>(new(
                new MemoryStream(FullContent, writable: false),
                FileName,
                MediaType,
                FullContent.LongLength,
                Sha256));
        }
    }
}
