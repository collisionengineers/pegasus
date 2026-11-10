using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Web.PhotoCapture;

/// <summary>Which credential a capture API route admits.</summary>
public enum CaptureCredentialKind
{
    /// <summary>A photo link's token or a standing link's slug: the context read, and handing a capture on.</summary>
    Any,

    /// <summary>A photo link's token only: everything that adds to a capture.</summary>
    Capture,

    /// <summary>A standing link's slug only: starting a capture.</summary>
    Standing
}

/// <summary>
/// A capture API route's admission: the credential it takes, the largest body
/// it reads, and whether the call adds an item and so counts against the
/// link's own item rate.
/// </summary>
public sealed record CaptureRoute(CaptureCredentialKind Credential, long BodyLimit, bool AddsItem = false);

/// <summary>Items a link may add a minute (FRD-28), partitioned by the capture, which a caller cannot forge.</summary>
public sealed class CaptureItemRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<Guid> limiter = PartitionedRateLimiter.Create<Guid, Guid>(
        captureId => RateLimitPartition.GetFixedWindowLimiter(
            captureId,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = PhotoCaptureSurface.ItemsPerLinkPerMinute,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    public bool TryAcquire(Guid captureId)
    {
        using var lease = limiter.AttemptAcquire(captureId);
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}

/// <summary>
/// The admission of every capture API call (ADR-0090). It sets the capture
/// headers, reads <c>X-Capture-Link</c>, refuses a value without a token's or
/// slug's shape before any store is read, resolves the rest, and answers the
/// one 404 for anything unavailable without reading the body. An admitted
/// call has its body bounded to the route's cap and its first call records
/// the capture as opened (never a page fetch: link previews fetch pages).
/// A designed refusal is a problem answer, not an exception; anything else
/// is a 500 problem carrying the trace id, so the developer exception page
/// never reaches a phone.
/// </summary>
public sealed partial class CaptureLinkAdmission(
    PhotoCaptureAccess access,
    IPhotoCaptureStore captures,
    CaptureItemRateLimiter itemLimiter,
    TimeProvider timeProvider,
    ILogger<CaptureLinkAdmission> logger) : IEndpointFilter
{
    private const string AccessKey = "Pegasus.PhotoCapture.Access";

    /// <summary>The credential this call was admitted on.</summary>
    public static CaptureAccess Access(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[AccessKey] as CaptureAccess
            ?? throw new InvalidOperationException("The capture call was not admitted.");
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        CaptureResponseHeaders.Apply(http.Response);
        try
        {
            var route = http.GetEndpoint()?.Metadata.GetMetadata<CaptureRoute>()
                ?? throw new InvalidOperationException("A capture API route names its admission.");
            var credential = http.Request.Headers[PhotoCaptureSurface.CredentialHeader].ToString();
            if (!HasShape(credential, route.Credential))
            {
                return Unavailable();
            }

            var resolved = await access.ResolveAsync(credential, http.RequestAborted);
            var admitted = route.Credential switch
            {
                CaptureCredentialKind.Capture => resolved.Capture is not null,
                CaptureCredentialKind.Standing => resolved.StandingLink is not null,
                _ => resolved.IsAvailable
            };
            if (!admitted)
            {
                return Unavailable();
            }

            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
            {
                bodySize.MaxRequestBodySize = route.BodyLimit;
            }

            if (http.Request.ContentLength > route.BodyLimit)
            {
                return CaptureProblems.For(PhotoCaptureRefusals.TooLarge, "The request is larger than this link accepts.");
            }

            if (resolved.Capture is { } capture)
            {
                if (route.AddsItem && !itemLimiter.TryAcquire(capture.Id))
                {
                    http.Response.Headers.RetryAfter = "60";
                    return CaptureProblems.RateLimited();
                }

                // The phone that made the capture holds a Session token, which never opens the link.
                if (capture.OpenedAtUtc is null && PhotoCaptureAccess.MarksOpened(resolved.TokenPurpose))
                {
                    resolved = resolved with
                    {
                        Capture = await captures.RecordOpenedAsync(capture.Id, timeProvider.GetUtcNow(), http.RequestAborted)
                    };
                }
            }

            http.Items[AccessKey] = resolved;
            return await next(context);
        }
        catch (PhotoCaptureRefusalException refusal)
        {
            return CaptureProblems.For(refusal.Code, refusal.Message);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return CaptureProblems.For(PhotoCaptureRefusals.TooLarge, "The request is larger than this link accepts.");
        }
        catch (Exception exception) when (!http.RequestAborted.IsCancellationRequested)
        {
            LogUnexpected(logger, exception, http.TraceIdentifier);
            return CaptureProblems.Unexpected(http.TraceIdentifier);
        }
    }

    /// <summary>The shape check that costs no lookup: <c>c.</c> and a token, or <c>s.</c> and a slug, as the route admits.</summary>
    private static bool HasShape(string credential, CaptureCredentialKind kind)
    {
        if (credential.StartsWith(PhotoCaptureAccess.CapturePrefix, StringComparison.Ordinal))
        {
            return kind != CaptureCredentialKind.Standing
                && PhotoLinkToken.HasTokenShape(credential[PhotoCaptureAccess.CapturePrefix.Length..]);
        }

        if (credential.StartsWith(PhotoCaptureAccess.StandingPrefix, StringComparison.Ordinal))
        {
            return kind != CaptureCredentialKind.Capture
                && PhotoLinkToken.NormalizeSlug(credential[PhotoCaptureAccess.StandingPrefix.Length..]) is not null;
        }

        return false;
    }

    private static IResult Unavailable() =>
        CaptureProblems.For(PhotoCaptureRefusals.Unavailable, "This link is no longer active.");

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "A photo capture API call failed unexpectedly (trace {TraceId}).")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string traceId);
}

/// <summary>The capture API's problem answers: one status per refusal code, the code in <c>code</c>.</summary>
public static class CaptureProblems
{
    public const string RateLimitedCode = "rate_limited";
    public const string ServerErrorCode = "server_error";

    public static IResult For(string code, string title) =>
        Results.Problem(
            statusCode: StatusFor(code),
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult RateLimited() =>
        Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests.",
            extensions: new Dictionary<string, object?> { ["code"] = RateLimitedCode });

    public static IResult Unexpected(string traceId) =>
        Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "The request could not be completed.",
            extensions: new Dictionary<string, object?> { ["code"] = ServerErrorCode, ["traceId"] = traceId });

    /// <summary>A cap is a refusal of what was sent, not rate limiting: 409, never 429.</summary>
    public static int StatusFor(string code) => code switch
    {
        PhotoCaptureRefusals.Unavailable => StatusCodes.Status404NotFound,
        PhotoCaptureRefusals.DetailsRequired
            or PhotoCaptureRefusals.ItemConflict
            or PhotoCaptureRefusals.NotReady
            or PhotoCaptureRefusals.RegistrationLocked
            or PhotoCaptureRefusals.CapReached
            or PhotoCaptureRefusals.MintCapReached
            or PhotoCaptureRefusals.CaseUnavailable => StatusCodes.Status409Conflict,
        PhotoCaptureRefusals.UnsupportedType => StatusCodes.Status415UnsupportedMediaType,
        PhotoCaptureRefusals.TooLarge => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status400BadRequest
    };
}
