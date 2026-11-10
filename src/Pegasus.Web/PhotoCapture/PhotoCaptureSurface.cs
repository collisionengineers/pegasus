using System.Net;
using System.Net.Sockets;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// Fixed names for the composition-gated public capture surface (FRD-28,
/// ADR-0090): the pages a link opens, the app's API, the credential header,
/// and the per-IP rate limits FRD-28's limits table names. Off,
/// <c>Features:PhotoCapture</c> composes none of it and every path below
/// answers 404 before routing.
/// </summary>
public static class PhotoCaptureSurface
{
    public const string FeatureFlag = "Features:PhotoCapture";
    public const string PublicOriginKey = "PhotoCapture:PublicOrigin";

    public const string ApiBasePath = "/capture/api/v1";
    public const string LinkPathPrefix = "/c";
    public const string StandingPathPrefix = "/s";
    public const string AssetPathPrefix = "/capture";
    public const string StaffPathPrefix = "/PhotoCapture";
    public const string CredentialHeader = "X-Capture-Link";

    public const string CapturePagePolicy = "CapturePage";
    public const string CaptureReadPolicy = "CaptureRead";
    public const string CaptureUploadPolicy = "CaptureUpload";
    public const string CaptureMintPolicy = "CaptureMint";

    /// <summary>Page requests, per IP, a minute.</summary>
    public const int PageRequestsPerMinute = 30;

    /// <summary>The app's context reads, per IP, a minute.</summary>
    public const int ReadsPerMinute = 60;

    /// <summary>Uploads, per IP, a minute.</summary>
    public const int UploadsPerMinute = 300;

    /// <summary>Captures started, per IP, an hour.</summary>
    public const int MintsPerHour = 10;

    /// <summary>Items, per link, a minute.</summary>
    public const int ItemsPerLinkPerMinute = 120;

    /// <summary>The paths the capture surface owns: its pages, its static app and its API.</summary>
    public static bool IsPublicPath(PathString path) =>
        path.StartsWithSegments(LinkPathPrefix)
        || path.StartsWithSegments(StandingPathPrefix)
        || path.StartsWithSegments(AssetPathPrefix);

    /// <summary>Every path the gate closes when the flag is off, the staff pages included.</summary>
    public static bool IsGatedPath(PathString path) =>
        IsPublicPath(path) || path.StartsWithSegments(StaffPathPrefix);
}

/// <summary>
/// The origin the links print (R779): an absolute HTTPS origin with a
/// trailing slash, read once at startup when the surface is composed.
/// </summary>
public sealed record PhotoCaptureOptions(Uri PublicOrigin)
{
    /// <summary>
    /// The configured origin, or a start-up failure naming the key. Normalised
    /// to a trailing slash as <c>Glass:CallbackBaseUri</c> is, because a
    /// relative link resolves against the base's directory.
    /// </summary>
    public static PhotoCaptureOptions Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var key = PhotoCaptureSurface.PublicOriginKey;
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required when {PhotoCaptureSurface.FeatureFlag} is enabled.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTPS origin with no query or fragment.");
        }

        return new(uri.AbsolutePath.EndsWith('/')
            ? uri
            : new UriBuilder(uri) { Path = uri.AbsolutePath + "/" }.Uri);
    }
}

/// <summary>The addresses a link is written out as; the only place a raw token becomes a URL.</summary>
public static class PhotoLinkUrls
{
    public static string CaptureUrl(Uri origin, CaptureLinkSecret token)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return new Uri(origin, "c/" + token.Value).AbsoluteUri;
    }

    public static string StandingUrl(Uri origin, string slug)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return new Uri(origin, "s/" + slug).AbsoluteUri;
    }
}

/// <summary>
/// The rate-limit partition of a capture caller: the IPv4 address, or the
/// /64 prefix of an IPv6 one, because a phone network hands one subscriber a
/// whole /64 and a per-address limit there bounds nothing.
/// </summary>
public static class ClientPartition
{
    public static string Key(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return new IPAddress(bytes).ToString() + "/64";
    }
}
