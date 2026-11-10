using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;

namespace Pegasus.Web.PhotoCapture;

/// <summary>What the start, hand-off and details calls send: the standing link's short form.</summary>
public sealed record CaptureFormBody(
    string? Registration,
    DateOnly? IncidentDate,
    string? CustomerName,
    string? Mobile,
    string? Email,
    PhotoCaptureDetails? Details)
{
    public StandingCaptureForm ToForm() =>
        new(Registration, IncidentDate, CustomerName, Mobile, Email, Details ?? PhotoCaptureDetails.Empty);
}

public sealed record CaptureCommitBody(long TotalBytes, string? Sha256, string? MediaType, string? Name);

public sealed record CaptureCompleteBody(string? Note);

public sealed record CaptureStartResponse(string Token, string LinkKey, CaptureContext Context);

/// <summary>The customer's link and message, and the Session token the staff phone sends the form's paperwork under, which never marks the link Opened.</summary>
public sealed record CaptureHandoffResponse(string Url, string Message, string Registration, string SessionToken);

public sealed record CaptureItemResponse(Guid ItemId, string Write, int Count);

public sealed record CapturePartResponse(Guid ItemId, int Part, string Write);

public sealed record CaptureCompleteResponse(IReadOnlyList<MissingCaptureItem> Missing, int Count);

/// <summary>
/// The capture app's API under <c>/capture/api/v1</c> (FRD-28, ADR-0090).
/// Every route is anonymous: <see cref="CaptureLinkAdmission"/> admits it on
/// the <c>X-Capture-Link</c> credential before a handler runs, and each
/// handler reads its own body with a bound counted while reading, never by
/// form or model binding. Nothing here logs a token: the start response
/// carries one, so it, like the hand-off's link, is never compressed.
/// </summary>
public static class CaptureEndpoints
{
    private static readonly string[] AcceptedMediaTypes =
    [
        "image/jpeg",
        "image/png",
        "application/pdf",
        IntakeUploadFilePolicy.Mp4MediaType,
        IntakeUploadFilePolicy.MovMediaType
    ];

    public static void MapPegasusPhotoCapture(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup(PhotoCaptureSurface.ApiBasePath)
            .AllowAnonymous()
            .DisableAntiforgery()
            .AddEndpointFilter<CaptureLinkAdmission>();
        group.MapGet("/context", ContextAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureReadPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Any, CaptureLimits.DetailsBytes));
        group.MapPost("/start", StartAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureMintPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Standing, CaptureLimits.DetailsBytes));
        // A standing link's staff hand a new capture on; a quick link sent
        // from a Case hands its own capture on (R798).
        group.MapPost("/handoff", HandoffAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureMintPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Any, CaptureLimits.DetailsBytes));
        group.MapPost("/details", DetailsAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureUploadPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Capture, CaptureLimits.DetailsBytes));
        group.MapPut("/items/{itemId:guid}", PutItemAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureUploadPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Capture, CaptureLimits.SingleRequestBytes, AddsItem: true));
        group.MapPut("/items/{itemId:guid}/parts/{part:int}", PutPartAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureUploadPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Capture, CaptureLimits.SingleRequestBytes));
        group.MapPost("/items/{itemId:guid}/commit", CommitAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureUploadPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Capture, CaptureLimits.DetailsBytes, AddsItem: true));
        group.MapPost("/complete", CompleteAsync)
            .RequireRateLimiting(PhotoCaptureSurface.CaptureUploadPolicy)
            .WithMetadata(new CaptureRoute(CaptureCredentialKind.Capture, CaptureLimits.DetailsBytes));
    }

    /// <summary>The limiter's refusal on an API path, as a problem the app can read.</summary>
    public static Task WriteRateLimitedAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CaptureProblems.RateLimited().ExecuteAsync(context);
    }

    private static async Task<IResult> ContextAsync(
        HttpContext context,
        [FromServices] CaptureContextFactory contexts,
        CancellationToken cancellationToken)
    {
        var access = CaptureLinkAdmission.Access(context);
        var view = access.Capture is { } capture
            ? await contexts.ForCaptureAsync(capture, access.TokenPurpose, cancellationToken)
            : await contexts.ForStandingAsync(access.StandingLink!, cancellationToken);
        return Results.Json(view, CaptureContextFactory.JsonOptions);
    }

    private static async Task<IResult> StartAsync(
        HttpContext context,
        [FromServices] MintFromStandingLink mint,
        [FromServices] CaptureContextFactory contexts,
        CancellationToken cancellationToken)
    {
        DoNotCompress(context);
        var link = CaptureLinkAdmission.Access(context).StandingLink!;
        var form = await ReadJsonAsync<CaptureFormBody>(context.Request, allowEmpty: false, cancellationToken);
        var minted = await mint.StartAsync(link, form!.ToForm(), cancellationToken);
        var view = await contexts.ForCaptureAsync(minted.Capture, PhotoLinkTokenPurpose.Session, cancellationToken);
        return Results.Json(
            new CaptureStartResponse(minted.Token.Value, view.LinkKey, view),
            CaptureContextFactory.JsonOptions,
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> HandoffAsync(
        HttpContext context,
        [FromServices] MintFromStandingLink mint,
        [FromServices] HandOnCaseLink handOn,
        [FromServices] PhotoCaptureOptions options,
        CancellationToken cancellationToken)
    {
        DoNotCompress(context);
        var access = CaptureLinkAdmission.Access(context);
        var form = await ReadJsonAsync<CaptureFormBody>(context.Request, allowEmpty: false, cancellationToken);
        var minted = access.Capture is { } capture
            ? await handOn.ExecuteAsync(capture, form!.ToForm(), cancellationToken)
            : await mint.HandoffAsync(access.StandingLink!, form!.ToForm(), cancellationToken);
        return Results.Json(
            new CaptureHandoffResponse(
                PhotoLinkUrls.CaptureUrl(options.PublicOrigin, minted.Token),
                minted.Message,
                PhotoCaptureText.DisplayRegistration(minted.Capture.NormalizedRegistration ?? string.Empty),
                minted.SessionToken?.Value ?? throw new InvalidOperationException("A hand-off is minted with a Session token.")),
            CaptureContextFactory.JsonOptions,
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> DetailsAsync(
        HttpContext context,
        [FromServices] RecordPhotoCaptureDetails recordDetails,
        [FromServices] IStandingPhotoLinkStore standingLinks,
        [FromServices] CaptureContextFactory contexts,
        CancellationToken cancellationToken)
    {
        var capture = CaptureLinkAdmission.Access(context).Capture!;
        var form = await ReadJsonAsync<CaptureFormBody>(context.Request, allowEmpty: false, cancellationToken);
        // The standing link scopes the routing to its Principal's own Cases.
        var link = capture.StandingLinkId is { } linkId
            ? await standingLinks.GetAsync(linkId, cancellationToken)
            : null;
        var updated = await recordDetails.ExecuteAsync(capture, link, form!.ToForm(), cancellationToken);
        return Results.Json(
            await contexts.ForCaptureAsync(updated, CaptureLinkAdmission.Access(context).TokenPurpose, cancellationToken),
            CaptureContextFactory.JsonOptions);
    }

    private static async Task<IResult> PutItemAsync(
        Guid itemId,
        HttpContext context,
        [FromServices] ReceivePhotoCaptureItem receive,
        CancellationToken cancellationToken)
    {
        var capture = CaptureLinkAdmission.Access(context).Capture!;
        var query = CaptureItemQuery.Parse(context.Request.Query);
        var mediaType = RequireMediaType(context.Request.ContentType);
        var body = await ReadBodyAsync(context.Request, CaptureLimits.SingleRequestBytes, cancellationToken);
        if (body.Count == 0)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The request has no content.");
        }

        RequireSignature(mediaType, body);
        var received = await receive.ExecuteAsync(
            capture,
            query.ToRequest(
                itemId,
                mediaType,
                body.Count,
                query.Name,
                _ => ValueTask.FromResult<Stream>(new MemoryStream(body.Array!, body.Offset, body.Count, writable: false))),
            cancellationToken);
        return ItemResult(itemId, received);
    }

    private static async Task<IResult> PutPartAsync(
        Guid itemId,
        int part,
        HttpContext context,
        [FromServices] IDocumentUploadStaging staging,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var capture = CaptureLinkAdmission.Access(context).Capture!;
        var query = CaptureItemQuery.Parse(context.Request.Query);
        var uploadId = StagingId(itemId, query.Original);
        var mediaType = RequireMediaType(context.Request.ContentType);
        var totalBytes = CaptureItemQuery.RequiredLong(context.Request.Query, "totalBytes");
        if (totalBytes <= 0)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The total size must be positive.");
        }

        if (totalBytes > CaptureLimits.MaximumBytesFor(query.Kind, query.Original))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.TooLarge, "The file is larger than this link accepts.");
        }

        // The whole file's digest is optional here and required on the commit,
        // which checks the staged bytes against it.
        var declaredSha256 = context.Request.Query["sha256"].ToString();
        var sha256 = declaredSha256.Length == 0
            ? string.Empty
            : NormalizeSha256(declaredSha256)
                ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, DocumentUploadRefusals.Sha256);
        var partCount = CaptureLimits.PartCount(totalBytes);
        if (part < 1 || part > partCount)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, DocumentUploadRefusals.PartNumber(partCount));
        }

        var body = await ReadBodyAsync(context.Request, CaptureLimits.SingleRequestBytes, cancellationToken);
        var expectedLength = CaptureLimits.PartLength(totalBytes, part);
        if (body.Count != expectedLength)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, DocumentUploadRefusals.PartLength(part, expectedLength));
        }

        if (part == 1)
        {
            RequireSignature(mediaType, body);
        }

        // The first part to arrive declares the upload; every later one must
        // declare the same size, and the same digest where both name one,
        // under the same capture.
        var owner = Owner(capture);
        if (!await staging.CreateManifestAsync(
                new DocumentUploadManifest(uploadId, owner, totalBytes, sha256, timeProvider.GetUtcNow()),
                cancellationToken))
        {
            var stored = await staging.ReadManifestAsync(uploadId, cancellationToken);
            if (stored is null
                || !string.Equals(stored.Owner, owner, StringComparison.Ordinal)
                || stored.TotalBytes != totalBytes
                || (stored.Sha256.Length > 0 && sha256.Length > 0
                    && !string.Equals(stored.Sha256, sha256, StringComparison.Ordinal)))
            {
                throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.ItemConflict, DocumentUploadRefusals.OperationKeyConflict);
            }
        }

        var content = new ReadOnlyMemory<byte>(body.Array, body.Offset, body.Count);
        var write = await staging.CreatePartAsync(
            uploadId,
            part,
            content,
            Convert.ToHexString(SHA256.HashData(content.Span)),
            cancellationToken);
        return write switch
        {
            DocumentUploadPartWrite.Created => Results.Json(
                new CapturePartResponse(itemId, part, "created"),
                CaptureContextFactory.JsonOptions,
                statusCode: StatusCodes.Status201Created),
            DocumentUploadPartWrite.AlreadyStored => Results.Json(
                new CapturePartResponse(itemId, part, "replayed"),
                CaptureContextFactory.JsonOptions),
            _ => throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.ItemConflict, DocumentUploadRefusals.PartConflict)
        };
    }

    private static async Task<IResult> CommitAsync(
        Guid itemId,
        HttpContext context,
        [FromServices] IDocumentUploadStaging staging,
        [FromServices] ReceivePhotoCaptureItem receive,
        CancellationToken cancellationToken)
    {
        var capture = CaptureLinkAdmission.Access(context).Capture!;
        var query = CaptureItemQuery.Parse(context.Request.Query);
        var uploadId = StagingId(itemId, query.Original);
        var commit = await ReadJsonAsync<CaptureCommitBody>(context.Request, allowEmpty: false, cancellationToken);
        var sha256 = NormalizeSha256(commit!.Sha256)
            ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, DocumentUploadRefusals.Sha256);
        var manifest = await staging.ReadManifestAsync(uploadId, cancellationToken);
        if (manifest is null || !string.Equals(manifest.Owner, Owner(capture), StringComparison.Ordinal))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.NotReady, DocumentUploadRefusals.Unavailable);
        }

        if (manifest.TotalBytes != commit.TotalBytes
            || (manifest.Sha256.Length > 0 && !string.Equals(manifest.Sha256, sha256, StringComparison.Ordinal)))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.ItemConflict, DocumentUploadRefusals.OperationKeyConflict);
        }

        var received = await staging.ListPartNumbersAsync(uploadId, cancellationToken);
        var missing = Enumerable.Range(1, CaptureLimits.PartCount(manifest.TotalBytes)).Except(received).ToArray();
        if (missing.Length > 0)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.NotReady, DocumentUploadRefusals.MissingParts(missing));
        }

        // The parts were sent under the file's own type; the commit is JSON,
        // so it names the type or the first part's signature does.
        var mediaType = commit.MediaType is { Length: > 0 } declaredType
            ? RequireMediaType(declaredType)
            : await SniffMediaTypeAsync(staging, uploadId, cancellationToken);

        // Core does not compare the staged bytes with the phone's digest, so
        // the parts are read through once here before anything is kept.
        string actual;
        await using (var parts = new ConcatenatedPartsStream(staging, uploadId, manifest.TotalBytes))
        {
            actual = await HashAsync(parts, cancellationToken);
        }

        if (!string.Equals(actual, sha256, StringComparison.Ordinal))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.ItemConflict, DocumentUploadRefusals.Sha256Mismatch);
        }

        var result = await receive.ExecuteAsync(
            capture,
            query.ToRequest(
                itemId,
                mediaType,
                manifest.TotalBytes,
                Bound(commit.Name) ?? query.Name,
                _ => ValueTask.FromResult<Stream>(new ConcatenatedPartsStream(staging, uploadId, manifest.TotalBytes))),
            cancellationToken);
        return ItemResult(itemId, result);
    }

    private static async Task<IResult> CompleteAsync(
        HttpContext context,
        [FromServices] CompletePhotoCapture complete,
        CancellationToken cancellationToken)
    {
        var capture = CaptureLinkAdmission.Access(context).Capture!;
        var body = await ReadJsonAsync<CaptureCompleteBody>(context.Request, allowEmpty: true, cancellationToken);
        var completed = await complete.ExecuteAsync(capture, body?.Note, cancellationToken);
        return Results.Json(
            new CaptureCompleteResponse(completed.Missing, completed.Capture.AcceptedItemCount),
            CaptureContextFactory.JsonOptions);
    }

    /// <summary>The accepted type whose signature the first part carries; the QuickTime brand is told from MP4's.</summary>
    private static async Task<string> SniffMediaTypeAsync(
        IDocumentUploadStaging staging,
        Guid uploadId,
        CancellationToken cancellationToken)
    {
        if (await staging.ReadPartAsync(uploadId, 1, cancellationToken) is { } first)
        {
            foreach (var candidate in AcceptedMediaTypes)
            {
                if (IntakeUploadFilePolicy.HeaderMatches(null, candidate, first.Span) == true)
                {
                    return candidate;
                }
            }
        }

        throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.UnsupportedType, "That kind of file is not accepted here.");
    }

    private static async Task<string> HashAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static IResult ItemResult(Guid itemId, ReceivedPhotoCaptureItem received) =>
        Results.Json(
            new CaptureItemResponse(
                itemId,
                received.Write == PhotoCaptureItemWrite.Replayed ? "replayed" : "created",
                received.ItemCount),
            CaptureContextFactory.JsonOptions,
            statusCode: StatusCodes.Status201Created);

    /// <summary>
    /// Where an item's parts are staged: the phone's item id, or for its
    /// full-size original an id derived from it, because the compressed
    /// photo and its original share the item id and may both be sent in parts.
    /// </summary>
    private static Guid StagingId(Guid itemId, bool original)
    {
        if (!original)
        {
            return itemId;
        }

        var digest = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes("photo-capture-original:" + itemId.ToString("N")));
        return new Guid(digest.AsSpan(0, 16));
    }

    /// <summary>The staging owner of a capture's parts, so one capture can never commit another's.</summary>
    private static string Owner(CaptureRecord capture) => $"photo-capture:{capture.Id:N}";

    private static void DoNotCompress(HttpContext context)
    {
        if (context.Features.Get<IHttpsCompressionFeature>() is { } compression)
        {
            compression.Mode = HttpsCompressionMode.DoNotCompress;
        }
    }

    private static string RequireMediaType(string? contentType)
    {
        if (MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            && parsed.MediaType.Value is { } value)
        {
            foreach (var accepted in AcceptedMediaTypes)
            {
                if (string.Equals(accepted, value, StringComparison.OrdinalIgnoreCase))
                {
                    return accepted;
                }
            }
        }

        throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.UnsupportedType, "That kind of file is not accepted here.");
    }

    /// <summary>The leading bytes must be what the media type says; the file name is not trusted for it.</summary>
    private static void RequireSignature(string mediaType, ArraySegment<byte> content)
    {
        if (IntakeUploadFilePolicy.HeaderMatches(null, mediaType, content.AsSpan()) == false)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.UnsupportedType, "The file is not what its type says.");
        }
    }

    private static string? NormalizeSha256(string? sha256) =>
        sha256 is { Length: 64 } && sha256.All(char.IsAsciiHexDigit) ? sha256.ToUpperInvariant() : null;

    private static string? Bound(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length <= CaptureLimits.NameLength ? trimmed : trimmed[..CaptureLimits.NameLength];
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpRequest request, bool allowEmpty, CancellationToken cancellationToken)
        where T : class
    {
        var body = await ReadBodyAsync(request, CaptureLimits.DetailsBytes, cancellationToken);
        if (body.Count == 0)
        {
            if (allowEmpty)
            {
                return null;
            }

            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The request has no content.");
        }

        if (!request.HasJsonContentType())
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.UnsupportedType, "The request must be application/json.");
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(body.AsSpan(), CaptureContextFactory.JsonOptions);
        }
        catch (JsonException)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The request is not valid JSON of the expected shape.");
        }

        return value ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The request has no content.");
    }

    /// <summary>
    /// The whole body, refused as too large when it runs past the bound. The
    /// bound is enforced while reading rather than trusted from Content-Length,
    /// which a caller controls and a chunked request omits.
    /// </summary>
    private static async Task<ArraySegment<byte>> ReadBodyAsync(
        HttpRequest request,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        // A declared length inside the bound only sizes the buffer.
        var declared = request.ContentLength ?? 0;
        using var buffer = new MemoryStream(declared > 0 && declared <= maximumBytes ? (int)declared : 0);
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.TooLarge, "The request is larger than this link accepts.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}

/// <summary>
/// The item fields an upload carries on its query: <c>kind</c>, <c>area</c>,
/// <c>stage</c>, <c>position</c>, <c>takenAt</c>, <c>live</c>,
/// <c>original</c> and <c>name</c>. A value that is present and unreadable is
/// an invalid request, not a default.
/// </summary>
public sealed record CaptureItemQuery(
    PhotoCaptureItemKind Kind,
    string? Area,
    PhotoCaptureStage? Stage,
    int Position,
    DateTimeOffset? TakenAtUtc,
    bool Live,
    bool Original,
    string? Name)
{
    public static CaptureItemQuery Parse(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var kind = CaptureCodes.ParseKind(query["kind"].ToString()) ?? throw Invalid("kind");
        var area = Optional(query, "area");
        if (area is { Length: > CaptureLimits.AreaLength })
        {
            throw Invalid("area");
        }

        var stageText = Optional(query, "stage");
        PhotoCaptureStage? stage = stageText is null ? null : CaptureCodes.ParseStage(stageText) ?? throw Invalid("stage");
        var positionText = Optional(query, "position");
        var position = 0;
        if (positionText is not null
            && !int.TryParse(positionText, NumberStyles.None, CultureInfo.InvariantCulture, out position))
        {
            throw Invalid("position");
        }

        var takenText = Optional(query, "takenAt");
        DateTimeOffset? takenAt = null;
        if (takenText is not null)
        {
            if (!DateTimeOffset.TryParse(takenText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                throw Invalid("takenAt");
            }

            takenAt = parsed.ToUniversalTime();
        }

        var name = Optional(query, "name");
        return new(
            kind,
            area,
            stage,
            position,
            takenAt,
            Flag(query, "live"),
            Flag(query, "original"),
            name is { Length: > CaptureLimits.NameLength } ? name[..CaptureLimits.NameLength] : name);
    }

    public static long RequiredLong(IQueryCollection query, string key)
    {
        ArgumentNullException.ThrowIfNull(query);
        return long.TryParse(query[key].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Invalid(key);
    }

    public ReceivePhotoCaptureItemRequest ToRequest(
        Guid clientItemId,
        string mediaType,
        long contentLength,
        string? fileName,
        Func<CancellationToken, ValueTask<Stream>> openContentAsync) =>
        new(
            clientItemId,
            Kind,
            Area,
            Stage,
            Position,
            TakenAtUtc,
            Live,
            Original,
            fileName,
            mediaType,
            contentLength,
            openContentAsync);

    private static string? Optional(IQueryCollection query, string key)
    {
        var value = query[key].ToString().Trim();
        return value.Length == 0 ? null : value;
    }

    private static bool Flag(IQueryCollection query, string key) =>
        Optional(query, key) switch
        {
            null or "false" or "0" => false,
            "true" or "1" => true,
            _ => throw Invalid(key)
        };

    private static PhotoCaptureRefusalException Invalid(string key) =>
        new(PhotoCaptureRefusals.InvalidRequest, $"The '{key}' value is missing or not readable.");
}
