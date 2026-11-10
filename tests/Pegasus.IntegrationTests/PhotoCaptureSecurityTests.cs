using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The public capture surface's refusals through the composed Web host, with
/// no database (FRD-28 "Public surface", ADR-0090): the one unavailable card
/// and its headers, one answer for every dead link, a dead link's upload
/// refused before its size is weighed, the per-IP page limit, and the whole
/// surface absent when the feature is off. The captures and standing links a
/// test names are listed in memory; every other store call is outside these
/// paths and refuses.
/// </summary>
public sealed class PhotoCaptureSecurityTests
{
    private const string UnavailableHeading = "This link is no longer active.";
    private const string UnavailableLine = "Ask whoever sent it to you for a new one.";

    private static readonly DateTimeOffset Now = IntakeWebApplicationFactory.FixedUtcNow;

    private static readonly Dictionary<string, string?> FeatureOff = new()
    {
        [PhotoCaptureSurface.FeatureFlag] = "false"
    };

    /// <summary>
    /// A random token names no capture: the page answers 404 with the card
    /// FRD-28 words, carries the five headers FRD-28 lists and sets no cookie.
    /// </summary>
    [Fact]
    public async Task AnUnknownLinkGetsTheOneUnavailableCardWithTheCaptureHeadersAndNoCookie()
    {
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = WithListedLinks(baseFactory, new ListedCaptures(), new ListedStandingLinks());
        using var client = PhoneClient(factory);

        using var response = await client.GetAsync($"/c/{PhotoLinkToken.Create().Value}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(UnavailableHeading, html, StringComparison.Ordinal);
        Assert.Contains(UnavailableLine, html, StringComparison.Ordinal);
        Assert.Equal(
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; "
            + "connect-src 'self'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; "
            + "frame-ancestors 'none'",
            Header(response, "Content-Security-Policy"));
        Assert.Equal("camera=(self), microphone=(), geolocation=()", Header(response, "Permissions-Policy"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("noindex, nofollow", Header(response, "X-Robots-Tag"));
        Assert.Equal("no-store", Header(response, "Cache-Control"));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    /// <summary>
    /// Unknown, expired, revoked, paused and a Case archived are never told
    /// apart (R789, R793): a cancelled capture, a revoked token on a live
    /// capture, an expired capture, a live capture whose Case is archived and
    /// a paused standing link each answer the unknown token's bytes.
    /// </summary>
    [Fact]
    public async Task UnknownExpiredRevokedPausedAndArchivedLinksAnswerTheSameBytes()
    {
        var expired = PhotoLinkToken.Create();
        var cancelled = PhotoLinkToken.Create();
        var revokedToken = PhotoLinkToken.Create();
        var archived = PhotoLinkToken.Create();
        var captures = new ListedCaptures();
        captures.Add(expired, Capture(expiresAtUtc: Now.AddDays(-1), revokedAtUtc: null), tokenRevoked: false);
        captures.Add(cancelled, Capture(expiresAtUtc: Now.AddDays(6), revokedAtUtc: Now.AddHours(-1)), tokenRevoked: false);
        captures.Add(revokedToken, Capture(expiresAtUtc: Now.AddDays(6), revokedAtUtc: null), tokenRevoked: true);
        captures.Add(archived, Capture(expiresAtUtc: Now.AddDays(6), revokedAtUtc: null), tokenRevoked: false, caseArchived: true);
        var pausedSlug = PhotoLinkToken.CreateSlug();
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = WithListedLinks(baseFactory, captures, new ListedStandingLinks(PausedLink(pausedSlug)));
        using var client = PhoneClient(factory);

        var unknown = await UnavailableBytesAsync(client, $"/c/{PhotoLinkToken.Create().Value}");

        Assert.Equal(unknown, await UnavailableBytesAsync(client, $"/c/{expired.Value}"));
        Assert.Equal(unknown, await UnavailableBytesAsync(client, $"/c/{cancelled.Value}"));
        Assert.Equal(unknown, await UnavailableBytesAsync(client, $"/c/{revokedToken.Value}"));
        Assert.Equal(unknown, await UnavailableBytesAsync(client, $"/c/{archived.Value}"));
        Assert.Equal(unknown, await UnavailableBytesAsync(client, $"/s/{pausedSlug}"));
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    /// <summary>
    /// The admission refuses a dead link before it weighs the body, so an
    /// upload past the request cap on an unknown token is the same 404 as any
    /// other call on it, not a 413 that would tell a guesser the route took it.
    /// </summary>
    [Fact]
    public async Task AnOversizedUploadOnADeadLinkIsUnavailableNotTooLarge()
    {
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = WithListedLinks(baseFactory, new ListedCaptures(), new ListedStandingLinks());
        using var client = PhoneClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{PhotoCaptureSurface.ApiBasePath}/items/{Guid.NewGuid():D}?kind=overview&position=1&live=false&original=false")
        {
            Content = new ByteArrayContent(new byte[CaptureLimits.SingleRequestBytes + 1])
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        request.Headers.Add(
            PhotoCaptureSurface.CredentialHeader,
            PhotoCaptureAccess.CapturePrefix + PhotoLinkToken.Create().Value);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(PhotoCaptureRefusals.Unavailable, problem.RootElement.GetProperty("code").GetString());
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    /// <summary>
    /// Page requests are limited to 30 a minute per IP (FRD-28 limits): the
    /// 31st in the minute is refused with 429 and recorded as a capture rate
    /// limit. Its own host, so no other test spends this limiter's window.
    /// </summary>
    [Fact]
    public async Task TheThirtyFirstPageRequestInAMinuteIsRateLimited()
    {
        const int pageRequestsPerMinute = 30;
        var events = new RecordedSecurityEvents();
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IPhotoCaptureStore>(services, new ListedCaptures());
            Substitute<ISecurityEventWriter>(services, events);
        }));
        using var client = PhoneClient(factory);
        var path = $"/c/{PhotoLinkToken.Create().Value}";

        for (var page = 0; page < pageRequestsPerMinute; page++)
        {
            using var admitted = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, admitted.StatusCode);
        }

        using var limited = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        var refusal = Assert.Single(events.Appended);
        Assert.Equal(SecurityEventType.RateLimited, refusal.Type);
        Assert.Equal("capture_rate_limited", refusal.ReasonCode);
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    /// <summary>
    /// Off, <c>Features:PhotoCapture</c> composes none of the surface and its
    /// pages, app, API and staff pages answer 404 before routing.
    /// </summary>
    [Fact]
    public async Task WithTheFeatureOffEveryCapturePathAnswersNotFound()
    {
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(PhotoCaptureSurface.FeatureFlag, "false");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(FeatureOff));
        });
        using var client = PhoneClient(factory);

        // Nothing of the surface was composed: its origin is registered only when the flag is on.
        Assert.Null(factory.Services.GetService<PhotoCaptureOptions>());
        string[] paths =
        [
            $"/c/{PhotoLinkToken.Create().Value}",
            $"/s/{PhotoLinkToken.CreateSlug()}",
            $"{PhotoCaptureSurface.ApiBasePath}/context",
            "/capture/capture.js",
            PhotoCaptureSurface.StaffPathPrefix
        ];
        var answered = new List<string>();
        foreach (var path in paths)
        {
            using var response = await client.GetAsync(path);
            answered.Add($"{path} {(int)response.StatusCode}");
        }

        Assert.Equal(paths.Select(path => $"{path} 404"), answered);
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    /// <summary>
    /// A standing link whose contact is deactivated starts no captures and
    /// answers like an unknown one (R796): its page is an unknown slug's
    /// bytes, and its start call is the API's 404.
    /// </summary>
    [Fact]
    public async Task AnInactiveContactsSlugAnswersLikeAnUnknownOne()
    {
        var inactiveSlug = PhotoLinkToken.CreateSlug();
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = WithListedLinks(
            baseFactory,
            new ListedCaptures(),
            new ListedStandingLinks(InactiveContactsLink(inactiveSlug)));
        using var client = PhoneClient(factory);

        var unknown = await UnavailableBytesAsync(client, $"/s/{PhotoLinkToken.CreateSlug()}");

        Assert.Equal(unknown, await UnavailableBytesAsync(client, $"/s/{inactiveSlug}"));
        using var start = new HttpRequestMessage(HttpMethod.Post, $"{PhotoCaptureSurface.ApiBasePath}/start")
        {
            Content = new StringContent("{\"registration\":\"AB12CDE\"}", System.Text.Encoding.UTF8, "application/json")
        };
        start.Headers.Add(PhotoCaptureSurface.CredentialHeader, PhotoCaptureAccess.StandingPrefix + inactiveSlug);
        using var started = await client.SendAsync(start);
        Assert.Equal(HttpStatusCode.NotFound, started.StatusCode);
        using var problem = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        Assert.Equal(PhotoCaptureRefusals.Unavailable, problem.RootElement.GetProperty("code").GetString());
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    private static WebApplicationFactory<Program> WithListedLinks(
        IntakeWebApplicationFactory baseFactory,
        ListedCaptures captures,
        ListedStandingLinks standingLinks) =>
        baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IPhotoCaptureStore>(services, captures);
            Substitute<IStandingPhotoLinkStore>(services, standingLinks);
        }));

    /// <summary>A phone: no member of staff is signed in, and every cookie it is sent stays on the response.</summary>
    private static HttpClient PhoneClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "1");
        return client;
    }

    private static async Task<byte[]> UnavailableBytesAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? string.Join(", ", values)
            : response.Content.Headers.TryGetValues(name, out var contentValues)
                ? string.Join(", ", contentValues)
                : string.Empty;

    private static CaptureRecord Capture(DateTimeOffset expiresAtUtc, DateTimeOffset? revokedAtUtc) => new(
        Id: Guid.NewGuid(),
        Origin: PhotoCaptureOrigin.CaseLink,
        StandingLinkId: null,
        OrganizationId: null,
        CaseId: Guid.NewGuid(),
        CaseReference: "a.QDOS31009",
        SubmissionGroupId: null,
        Destination: PhotoCaptureDestination.Case,
        RoutingReason: null,
        NormalizedRegistration: "AB12CDE",
        IncidentDate: null,
        CustomerName: null,
        Mobile: null,
        Email: null,
        Details: PhotoCaptureDetails.Empty,
        Note: null,
        Mode: PhotoCaptureMode.Guided,
        LiveCameraOnly: false,
        OriginalsInBackground: false,
        IssuedByStaffId: null,
        CreatedAtUtc: expiresAtUtc.AddDays(-CaptureLimits.DefaultLifetimeDays),
        ExpiresAtUtc: expiresAtUtc,
        RevokedAtUtc: revokedAtUtc,
        OpenedAtUtc: null,
        FirstItemAtUtc: null,
        LastItemAtUtc: null,
        SentAtUtc: null,
        SettledAtUtc: null,
        SettledBatches: 0,
        AcceptedItemCount: 0,
        AcceptedBytes: 0,
        ReminderCount: 0,
        LastRemindedAtUtc: null,
        CallLoggedAtUtc: null,
        Version: 1);

    private static StandingPhotoLink PausedLink(string slug) => new(
        Id: Guid.NewGuid(),
        Kind: StandingPhotoLinkKind.Contact,
        OrganizationId: Guid.NewGuid(),
        OrganizationName: "Example Bodyshop",
        PrincipalId: null,
        IsRepairer: true,
        ContactActive: true,
        Slug: slug,
        ForwardedMode: PhotoCaptureMode.Guided,
        LiveCameraOnly: false,
        LinkLifetimeDays: CaptureLimits.DefaultLifetimeDays,
        OriginalsInBackground: false,
        PausedAtUtc: Now.AddDays(-1),
        RetiredAtUtc: null,
        CreatedAtUtc: Now.AddDays(-30),
        Version: 1);

    /// <summary>A contact's link that is neither paused nor retired, whose contact is deactivated.</summary>
    private static StandingPhotoLink InactiveContactsLink(string slug) =>
        PausedLink(slug) with { ContactActive = false, PausedAtUtc = null };

    private static NotSupportedException Outside() =>
        new("Only a presented token's capture or slug's standing link is read on the refused paths.");

    /// <summary>The captures a test lists by their token; a token it did not list names nothing.</summary>
    private sealed class ListedCaptures : IPhotoCaptureStore
    {
        private readonly ConcurrentDictionary<string, (CaptureRecord Capture, bool TokenRevoked, bool CaseArchived, PhotoLinkTokenPurpose Purpose)> byDigest =
            new(StringComparer.Ordinal);

        public void Add(
            CaptureLinkSecret token,
            CaptureRecord capture,
            bool tokenRevoked,
            bool caseArchived = false,
            PhotoLinkTokenPurpose purpose = PhotoLinkTokenPurpose.Email) =>
            byDigest[PhotoLinkToken.Digest(token)] = (capture, tokenRevoked, caseArchived, purpose);

        public Task<(CaptureRecord Capture, bool TokenRevoked, bool CaseArchived, PhotoLinkTokenPurpose Purpose)?> FindByTokenDigestAsync(
            string tokenDigest,
            CancellationToken cancellationToken)
        {
            (CaptureRecord Capture, bool TokenRevoked, bool CaseArchived, PhotoLinkTokenPurpose Purpose)? found = byDigest.TryGetValue(tokenDigest, out var listed)
                ? listed
                : null;
            return Task.FromResult(found);
        }

        public Task<CaptureRecord> CreateAsync(
            NewPhotoCapture capture,
            string tokenDigest,
            PhotoLinkTokenPurpose tokenPurpose,
            string? sessionTokenDigest,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord?> GetAsync(Guid captureId, CancellationToken cancellationToken) => throw Outside();

        public Task AddTokenAsync(
            Guid captureId,
            string tokenDigest,
            PhotoLinkTokenPurpose purpose,
            Guid? issuedByStaffId,
            DateTimeOffset issuedAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordOpenedAsync(Guid captureId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken) =>
            throw Outside();

        public Task<CaptureRecord> RecordDetailsAsync(
            Guid captureId,
            long expectedVersion,
            string? normalizedRegistration,
            DateOnly? incidentDate,
            string? customerName,
            string? mobile,
            string? email,
            PhotoCaptureDetails details,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordRoutingAsync(
            Guid captureId,
            long expectedVersion,
            PhotoCaptureDestination destination,
            Guid? caseId,
            string reason,
            CancellationToken cancellationToken) => throw Outside();

        public Task<PhotoCaptureItemWriteResult> AddItemAsync(NewPhotoCaptureItem item, CancellationToken cancellationToken) =>
            throw Outside();

        public Task RecordItemReceiptAsync(Guid itemId, Guid stagedReceiptId, CancellationToken cancellationToken) =>
            throw Outside();

        public Task RecordItemProcessedAsync(
            Guid itemId,
            Guid intakeReceiptId,
            Guid? intakeAssetId,
            CancellationToken cancellationToken) => throw Outside();

        public Task<bool> ReserveOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken) =>
            throw Outside();

        public Task ReleaseOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken) =>
            throw Outside();

        public Task RecordItemOriginalAsync(Guid itemId, CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordSentAsync(
            Guid captureId,
            string? note,
            DateTimeOffset sentAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordSettledAsync(
            Guid captureId,
            long expectedVersion,
            Guid? submissionGroupId,
            DateTimeOffset settledAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RevokeAsync(
            Guid captureId,
            ActionActor actor,
            string operationKey,
            DateTimeOffset revokedAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> ExtendAsync(
            Guid captureId,
            ActionActor actor,
            string operationKey,
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordReminderAsync(
            Guid captureId,
            ActionActor actor,
            string operationKey,
            DateTimeOffset remindedAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordCallAsync(
            Guid captureId,
            ActionActor actor,
            string operationKey,
            DateTimeOffset calledAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<IReadOnlyList<PhotoCaptureWithItems>> ListAwaitingSettleAsync(
            DateTimeOffset nowUtc,
            int maximumItems,
            CancellationToken cancellationToken) => throw Outside();

        public Task<IReadOnlyList<PhotoCaptureItem>> ListItemsAsync(Guid captureId, CancellationToken cancellationToken) =>
            throw Outside();

        public Task<PhotoCaptureItem?> FindItemAsync(Guid captureId, Guid clientItemId, CancellationToken cancellationToken) =>
            throw Outside();

        public Task<IReadOnlyList<(PhotoCaptureWithItems Capture, Guid CaseId, string CaseReference)>> ListSettledReachingCaseAsync(
            int maximumItems,
            CancellationToken cancellationToken) => throw Outside();

        public Task<CaptureRecord> RecordCaseAsync(
            Guid captureId,
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken) => throw Outside();

        public Task<bool> AllItemsProcessedAsync(Guid captureId, int batch, CancellationToken cancellationToken) =>
            throw Outside();
    }

    /// <summary>The standing links a test lists by their slug; a slug it did not list names nothing.</summary>
    private sealed class ListedStandingLinks(params StandingPhotoLink[] links) : IStandingPhotoLinkStore
    {
        public Task<StandingPhotoLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(links.FirstOrDefault(link => string.Equals(link.Slug, slug, StringComparison.Ordinal)));

        public Task<StandingPhotoLink?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(links.FirstOrDefault(link => link.Id == id));

        public Task<StandingPhotoLink?> FindForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw Outside();

        public Task<StandingPhotoLink?> FindWebsiteAsync(CancellationToken cancellationToken) => throw Outside();

        public Task<StandingPhotoLink> CreateAsync(
            StandingPhotoLinkKind kind,
            Guid? organizationId,
            string slug,
            PhotoCaptureMode forwardedMode,
            bool liveCameraOnly,
            int linkLifetimeDays,
            bool originalsInBackground,
            ActionActor actor,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<StandingPhotoLink> UpdateDefaultsAsync(
            Guid id,
            long expectedVersion,
            PhotoCaptureMode forwardedMode,
            bool liveCameraOnly,
            int linkLifetimeDays,
            bool originalsInBackground,
            CancellationToken cancellationToken) => throw Outside();

        public Task<StandingPhotoLink> SetPausedAsync(
            Guid id,
            long expectedVersion,
            DateTimeOffset? pausedAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<StandingPhotoLink> SetRetiredAsync(
            Guid id,
            long expectedVersion,
            DateTimeOffset retiredAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<StandingPhotoLink> ReplaceSlugAsync(
            Guid id,
            long expectedVersion,
            string newSlug,
            DateTimeOffset replacedAtUtc,
            CancellationToken cancellationToken) => throw Outside();

        public Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(Guid standingLinkId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StandingPhotoLinkAddress>>([]);

        public Task<int> CountMintedSinceAsync(Guid standingLinkId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            throw Outside();
    }

    /// <summary>The security events the host appended, in order.</summary>
    private sealed class RecordedSecurityEvents : ISecurityEventWriter
    {
        private readonly ConcurrentQueue<SecurityEvent> appended = new();

        public IReadOnlyList<SecurityEvent> Appended => [.. appended];

        public Task AppendAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
        {
            appended.Enqueue(securityEvent);
            return Task.CompletedTask;
        }
    }
}
