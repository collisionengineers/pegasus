namespace Pegasus.Web.Pages.Uploads;

/// <summary>
/// Fixed names and bounds for the anonymous upload link (INT-31), the
/// product's only external-facing screen.
/// </summary>
/// <remarks>
/// The per-token limiter in <see cref="RequestUploadAttemptLimiter"/> bounds a
/// caller who holds a valid token. It cannot bound a caller who holds none:
/// <see cref="RequestModel.OnPostAsync"/> answers <c>NotFound</c> for an
/// unknown token before the limiter is consulted, and the limiter partitions
/// on the token digest, so an anonymous caller has nothing to spend.
///
/// That gap only became reachable when the composition gate opened. While
/// <c>DocumentRequests:AcceptedLimitsVersion</c> was absent the middleware
/// short-circuited every <c>/Uploads</c> request to 404 before a body was
/// read; with the surface composed, Razor Pages' antiforgery filter reads and
/// buffers the whole multipart body before the page can reject it.
///
/// So the transport-level bound is partitioned by calling address, exactly as
/// it already is for staff sign-in, the MCP ingress and the Provider API. The
/// limiter runs at <c>UseRateLimiter</c>, after routing and before endpoint
/// execution, so a rejected caller is answered 429 without the body ever being
/// read.
/// </remarks>
public static class PublicUploadLink
{
    public const string RateLimitPolicy = "PublicUploadLink";

    /// <summary>
    /// Per calling address, per minute. A genuine sender uploads a handful of
    /// files once; the configured per-token allowance (
    /// <c>DocumentRequests:RateLimit</c>) is what bounds a legitimate holder.
    /// This is the outer bound on an address that holds no token at all.
    /// </summary>
    public const int RequestsPerClientPerMinute = 30;
}
