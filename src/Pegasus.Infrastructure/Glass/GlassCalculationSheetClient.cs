using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Reads the calculation sheet Glass's estimator prints for the estimate a
/// launch address opens: the launch page, <c>aco.php</c>, <c>aco2.php</c>
/// (which carries the session key), the main frame, the report, and the
/// report's PDF. It is the request sequence read by hand on 23 November 2026
/// (issue 1928), and it saves nothing: the estimator is left as a closed
/// window leaves it.
///
/// <para>
/// <b>Why it exists.</b> Glass's publishes no export for an estimate held on
/// an unqualified vehicle, so a placeholder session's lines can only be read
/// from the estimator itself (ADR-0062, R1233).
/// </para>
///
/// <para>
/// <b>One host, its own cookies.</b> Every request goes to the configured
/// estimator and to nothing else, and carries only the cookies the estimator
/// set on this client: the portal's session cookies belong to another origin
/// and are never sent here. The estimator serves the PDF only to the balancer
/// cookies of the session that printed it. No redirect is followed, and no
/// failure carries the session key, a cookie or an address.
/// </para>
/// </summary>
internal sealed partial class GlassCalculationSheetClient(
    HttpClient httpClient,
    GlassRepairEstimateOptions options,
    TimeProvider timeProvider)
{
    private const int MatchTimeoutMilliseconds = 1000;

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36";

    private readonly Dictionary<string, string> cookies = new(StringComparer.Ordinal);

    private DateTimeOffset? last;

    /// <summary>
    /// The sheet as Glass's printed it, proved to begin as a PDF. A page that
    /// cannot be asked for or read is the download's own refusal, with the
    /// page named in the detail; a sheet past the export cap is refused as an
    /// oversize download; an answer that is not a PDF is an unreadable export.
    /// </summary>
    public async Task<byte[]> DownloadAsync(Uri launch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var launchPage = await TextAsync(launch, referer: null, "launch", cancellationToken);
        var opened = GlassMvaClient.Matched(() => LaunchReplace().Match(launchPage), GlassFailure.DownloadRequest);
        if (!opened.Success)
        {
            // Glass's answers its blocked page when the account has too many
            // estimator windows open.
            throw Refused(launchPage.Contains("blocked.html", StringComparison.Ordinal) ? "blocked" : "launch");
        }

        var origin = opened.Groups[1].Value;
        var description = opened.Groups[2].Value;
        var editor = new Uri(launch, "../aco/");
        var stamp = Stamp();
        var first = new Uri(editor, $"aco.php?url0={origin}&XmlPar={description}&WT={stamp}");
        await TextAsync(first, launch, "aco", cancellationToken);
        var second = new Uri(editor, $"aco2.php?url0={Uri.EscapeDataString(origin)}&XmlPar={description}&WT={stamp}");
        var keyPage = await TextAsync(second, first, "aco2", cancellationToken);
        var key = GlassMvaClient.Matched(() => FrameKey().Match(keyPage), GlassFailure.DownloadRequest);
        if (!key.Success)
        {
            throw Refused("key");
        }

        var session = "ksid=" + key.Groups[1].Value;
        var frame = new Uri(
            editor,
            $"acofr.php?{session}&url0={Uri.EscapeDataString(origin)}&noacd=1&thsep=%2C&html5svg=1&ieversion=6");
        await TextAsync(frame, second, "frame", cancellationToken);
        var report = new Uri(editor, $"report.php?{session}&cml=C&calcid=0&dummy={Stamp()}");
        await TextAsync(report, frame, "report", cancellationToken);
        var print = new Uri(editor, $"reppdf.php?{session}&dummy={Stamp()}&calcid=0");
        var printed = await TextAsync(print, report, "reppdf", cancellationToken);
        var named = GlassMvaClient.Matched(() => SheetAddress().Match(printed), GlassFailure.DownloadRequest);
        if (!named.Success)
        {
            throw Refused("sheet");
        }

        var sheet = await BytesAsync(
            new Uri(print, $"{named.Groups[1].Value}?dummy={Stamp()}"),
            print,
            "pdf",
            options.MaximumExportBytes,
            GlassFailure.DownloadOversize,
            cancellationToken);
        return sheet.AsSpan().StartsWith("%PDF-"u8)
            ? sheet
            : throw new GlassMvaStageException(GlassFailure.ExportUnreadable, detail: "sheet=not-a-pdf");
    }

    private string Stamp() =>
        timeProvider.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    private static GlassMvaStageException Refused(string page) =>
        new(GlassFailure.DownloadRequest, detail: $"sheet={page}");

    /// <summary>An estimator page as text. The pages are ISO-8859-1 and only their ASCII is read.</summary>
    private async Task<string> TextAsync(Uri address, Uri? referer, string page, CancellationToken cancellationToken) =>
        Encoding.Latin1.GetString(await BytesAsync(
            address, referer, page, GlassMvaClient.MaximumTextBytes, GlassFailure.DownloadRequest, cancellationToken));

    /// <summary>
    /// One request, after the pause since the last one. Another host is
    /// refused before anything is sent, a redirect is a refusal and is not
    /// followed, and any answer but 200 is a refusal.
    /// </summary>
    private async Task<byte[]> BytesAsync(
        Uri address, Uri? referer, string page, int maximumBytes, string oversizeCode,
        CancellationToken cancellationToken)
    {
        if (!options.IsEstimator(address))
        {
            throw Refused($"{page} host=other");
        }

        if (last is { } previous && previous + options.EditorPause - timeProvider.GetUtcNow() is var wait
            && wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, timeProvider, cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-GB,en;q=0.9");
        if (referer is not null)
        {
            request.Headers.Referrer = referer;
        }
        if (cookies.Count > 0)
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        }

        try
        {
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            Keep(response);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw Refused($"{page} status={(int)response.StatusCode}");
            }

            return await GlassMvaClient.ReadAsync(
                response, maximumBytes, oversizeCode, outcomeUnknown: false, cancellationToken);
        }
        finally
        {
            last = timeProvider.GetUtcNow();
        }
    }

    /// <summary>Keeps each cookie the estimator sets, by name, as a browser's jar does for one host.</summary>
    private void Keep(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var lines))
        {
            return;
        }

        foreach (var line in lines)
        {
            var pair = line.Split(';', 2)[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                cookies[pair[..separator].Trim()] = pair[(separator + 1)..].Trim();
            }
        }
    }

    /// <summary>The launch page's own script: where it sends the window, with what it was opened from and for.</summary>
    [GeneratedRegex(
        @"document\.location\.replace\(\s*'\.\./aco/aco\.php\?url0=([^']{0,4000}?)&XmlPar=([^']{0,4000})'\s*\+\s*Auth\(\)\s*\+\s*WT\(\)\s*\)",
        RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex LaunchReplace();

    /// <summary><c>aco2.php</c>: the session key, in the address of its main frame.</summary>
    [GeneratedRegex(@"acofr\.php\?ksid=([A-Za-z0-9]{1,200})", RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex FrameKey();

    /// <summary><c>reppdf.php</c>: the temporary file its script sends the window to.</summary>
    [GeneratedRegex(
        @"document\.location\.replace\(\s*'(\.\./_tmp/[A-Za-z0-9_/]{1,300}\.pdf)\?dummy='",
        RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex SheetAddress();
}
