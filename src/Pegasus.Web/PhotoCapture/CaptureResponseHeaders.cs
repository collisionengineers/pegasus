namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// The headers every capture page and API response carries (FRD-28, ADR-0090),
/// in every environment. The capture surface owns them: the staff shell's
/// Content Security Policy skips these paths, so the two never meet on one
/// response.
/// </summary>
public static class CaptureResponseHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; "
        + "media-src 'self' blob:; connect-src 'self'; worker-src 'none'; object-src 'none'; "
        + "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public const string PermissionsPolicy = "camera=(self), microphone=(), geolocation=()";

    public static void Apply(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var headers = response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Robots-Tag"] = "noindex, nofollow";
        headers.CacheControl = "no-store";
        headers.XContentTypeOptions = "nosniff";
    }
}
