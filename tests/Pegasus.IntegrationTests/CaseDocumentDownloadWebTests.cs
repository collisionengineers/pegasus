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

    /// <summary>
    /// The plain thumbnail's validator (v26 § Crop and tag): the source and
    /// the variant it was rendered under, so a prepared region is a different
    /// representation from the gallery rendering.
    /// </summary>
    private static readonly string ThumbnailValidator =
        $"\"{Sha256}-{CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, null)}\"";
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

        using var response = await client.GetAsync(PreviewRoute(thumbnail: true, prep: "0"));
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ThumbnailContent, body);
        Assert.Equal(
            CaseDocumentThumbnails.MediaType,
            response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(ThumbnailValidator, response.Headers.ETag!.Tag);
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

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute(thumbnail: true, prep: "0"));
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{Sha256}\"");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ThumbnailContent, body);
        Assert.Equal(ThumbnailValidator, response.Headers.ETag!.Tag);
        Assert.Equal(1, ports.ThumbnailReads);
    }

    [Fact]
    public async Task AThumbnailTheBrowserAlreadyHoldsIsAnsweredWithNotModified()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute(thumbnail: true, prep: "0"));
        request.Headers.TryAddWithoutValidation("If-None-Match", ThumbnailValidator);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Equal(ThumbnailValidator, response.Headers.ETag!.Tag);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.Equal(TimeSpan.FromDays(7), response.Headers.CacheControl.MaxAge);
        Assert.Equal(1, ports.PreparationReads);
        Assert.Equal(0, ports.ThumbnailReads);
        Assert.Equal(0, ports.LogicalReads);
    }

    [Fact]
    public async Task AThumbnailWithTheCurrentPreparationVersionIsCacheableUnderThePreparedVariant()
    {
        var crop = new CaseAssetCrop(0.1m, 0.2m, 0.7m, 0.6m);
        var ports = new DocumentPorts
        {
            Preparation = PreparationFor(7, CaseAssetRotation.Clockwise90, crop)
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute(thumbnail: true, prep: "7"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ThumbnailValidatorFor(CaseAssetRotation.Clockwise90, crop),
            response.Headers.ETag!.Tag);
        Assert.Equal(TimeSpan.FromDays(7), response.Headers.CacheControl!.MaxAge);
        Assert.Equal(1, ports.PreparationReads);
        var request = Assert.Single(ports.ThumbnailRequests);
        Assert.Equal(CaseAssetRotation.Clockwise90, request.Rotation);
        Assert.Equal(crop, request.Crop);
    }

    [Fact]
    public async Task AStaleThumbnailAddressServesTheCurrentRepresentationWithoutCachingOrNotModified()
    {
        var crop = new CaseAssetCrop(0.1m, 0.2m, 0.7m, 0.6m);
        var ports = new DocumentPorts
        {
            Preparation = PreparationFor(7, CaseAssetRotation.Clockwise90, crop)
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute(thumbnail: true, prep: "6"));
        request.Headers.TryAddWithoutValidation(
            "If-None-Match",
            ThumbnailValidatorFor(CaseAssetRotation.Clockwise90, crop));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.ETag);
        Assert.Equal(1, ports.ThumbnailReads);
        Assert.Equal(0, ports.LogicalReads);
        var thumbnailRequest = Assert.Single(ports.ThumbnailRequests);
        Assert.Equal(CaseAssetRotation.Clockwise90, thumbnailRequest.Rotation);
        Assert.Equal(crop, thumbnailRequest.Crop);
    }

    [Fact]
    public async Task AThumbnailWithoutTheCurrentPreparationVersionIsNotCacheable()
    {
        var ports = new DocumentPorts
        {
            Preparation = PreparationFor(1, CaseAssetRotation.Half, CaseAssetCrop.Full)
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute(thumbnail: true, prep: "0"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.ETag);
        Assert.Equal(1, ports.ThumbnailReads);
    }

    [Fact]
    public async Task AThumbnailWithoutAPreparationAddressIsNeverCacheableOrNotModified()
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, PreviewRoute(thumbnail: true));
        request.Headers.TryAddWithoutValidation("If-None-Match", ThumbnailValidator);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.ETag);
        Assert.Equal(1, ports.PreparationReads);
        Assert.Equal(1, ports.ThumbnailReads);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("not-a-version")]
    [InlineData("")]
    public async Task MalformedOrNegativeThumbnailPreparationValuesAreRejectedWithoutReadingContent(string prep)
    {
        var ports = new DocumentPorts();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = CreateFactory(baseFactory, ports);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(PreviewRoute(thumbnail: true, prep: prep));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.ETag);
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

    private static string PreviewRoute(bool thumbnail = false, string? prep = null) =>
        DownloadRoute()
        + "&inline=true"
        + (thumbnail ? $"&size={CaseDocumentThumbnails.ThumbSizeToken}" : string.Empty)
        + (prep is null ? string.Empty : $"&prep={Uri.EscapeDataString(prep)}");

    private static string ThumbnailValidatorFor(CaseAssetRotation rotation, CaseAssetCrop? crop) =>
        $"\"{Sha256}-{CaseDocumentThumbnails.VariantToken(rotation, crop)}\"";

    private static CaseAssetPreparation PreparationFor(
        long preparationVersion,
        CaseAssetRotation rotation,
        CaseAssetCrop crop) =>
        new(
            CaseId,
            OccurrenceId,
            DocumentId,
            VersionId,
            1,
            Sha256,
            MediaType,
            CaseAssetReportRole.NotUsed,
            null,
            rotation,
            crop,
            preparationVersion,
            "staff",
            new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero));

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
                Substitute<ICaseAssetPreparationQueries>(services, ports);
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
        IDownloadCaseDocument,
        ICaseAssetPreparationQueries
    {
        private int logicalReads;
        private int thumbnailReads;
        private int auditedDownloads;
        private int preparationReads;
        private readonly List<CaseDocumentThumbnailRequest> thumbnailRequests = [];

        public DocumentCustodyStatus CustodyStatus { get; init; } =
            DocumentCustodyStatus.Confirmed;

        public byte[]? Thumbnail { get; init; } = ThumbnailContent;

        public Exception? ReadFailure { get; init; }

        public bool Preview { get; init; } = true;

        public CaseAssetPreparation? Preparation { get; init; }

        public int LogicalReads => Volatile.Read(ref logicalReads);

        public int ThumbnailReads => Volatile.Read(ref thumbnailReads);

        public int AuditedDownloads => Volatile.Read(ref auditedDownloads);

        public int PreparationReads => Volatile.Read(ref preparationReads);

        public IReadOnlyList<CaseDocumentThumbnailRequest> ThumbnailRequests => thumbnailRequests;

        Task<CaseAssetPreparation?> ICaseAssetPreparationQueries.GetForOccurrenceAsync(
            Guid caseId,
            Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref preparationReads);
            Assert.Equal(CaseId, caseId);
            Assert.Equal(OccurrenceId, occurrenceId);
            return Task.FromResult(Preparation);
        }

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationQueries.ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(CaseId, caseId);
            return Task.FromResult<IReadOnlyList<CaseAssetPreparation>>(
                Preparation is null ? [] : [Preparation]);
        }

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
            thumbnailRequests.Add(request);
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
