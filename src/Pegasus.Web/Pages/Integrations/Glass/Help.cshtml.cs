using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// Where the Pegasus helper extension, in a staff member's browser, hands over
/// the Glass's editor key and asks whether it is still held (R1170, ADR-0091).
/// </summary>
/// <remarks>
/// <para>
/// <b>Key</b> takes the editor's main-frame address (<c>acofr.php</c>), which
/// carries the session key and, inside <c>url0</c>, the session's return
/// address, and the cookies of Glass's load balancer the helper read in that
/// frame (<see cref="GlassHelpFrame.BalancerCookieNames"/>), which name the
/// Glass's server the window's editor session is on. The return token is read
/// from that address, never sent beside
/// it, so a sender cannot pair a key with another session's token. In order,
/// before anything is kept: a JSON post from a browser extension's origin, or
/// 403; a signed-in staff member, or 401; a frame on the configured estimator
/// whose return address is this host's for a well-formed token, or 400; a
/// session launched under that token (404), owned by that staff member (403)
/// and <c>Active</c> (409). The key is then held in this host's memory
/// (<see cref="GlassHelpKeys"/>), replacing any older one. Nothing is asked of
/// Glass's.
/// </para>
/// <para>
/// <b>Poll</b> is sent without the staff cookie, so it neither reads the staff
/// account nor slides the staff session. It names the token, which finds the
/// session by its fingerprint, and answers whether a key is held and when to
/// ask again; 404 and 409 tell the helper to stop. Knowing the token gains a
/// caller nothing here: no key is taken and nothing secret is answered.
/// </para>
/// <para>
/// <b>Why no antiforgery token, and why anonymous.</b> The helper's service
/// worker composes the request, so it carries no form token; it is refused
/// instead on what it does carry, as the Callback is
/// (<see cref="CallbackModel"/>). A page on Glass's origin that tried the
/// same post is cross-site to Pegasus, so the browser withholds the
/// SameSite=Strict staff cookie and Key answers 401. A JSON post from a page
/// needs a preflight, which Pegasus never answers: no answer here carries an
/// <c>Access-Control-Allow-*</c> header. A form-shaped post fails the content
/// type and its <c>Origin</c> is not an extension's. The page is anonymous so
/// that a missing session is a plain 401 the helper can read, not a sign-in
/// redirect, and so that the cookie-less Poll reaches it at all.
/// </para>
/// <para>
/// Nothing here logs the key, the token, the frame address, the return
/// address or a cookie; of the load balancer's cookies the log says how many
/// came. A host that does not reach Glass's answers 404 to everything.
/// </para>
/// </remarks>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class HelpModel(
    IGlassRepairEstimateSessionReader glassSessions,
    GlassRepairEstimateAvailability glassAvailability,
    ILogger<HelpModel> logger) : StaffPageModel
{
    /// <summary>
    /// The protocol the helper speaks, the <c>v</c> of each body. The helper's
    /// own <c>PROTOCOL</c> in <c>src/pegasus-glass-helper/background.js</c>
    /// must equal it (<c>GlassHelperExtensionContractTests</c>). Protocol 2
    /// (19 November 2026) is the hand-off that carries the load balancer's
    /// cookies; a helper that speaks 1 is told to upgrade, because a key
    /// without them reaches the right Glass's server only by chance.
    /// </summary>
    public const int Protocol = 2;

    /// <summary>Where both posts live; the rate limit and the status pages key on it.</summary>
    public const string BasePath = "/Integrations/Glass/Help";

    private const string ExtensionScheme = "chrome-extension://";

    /// <summary>A body is a frame address and a few fields; nothing larger is read.</summary>
    private const int MaximumBodyBytes = 8 * 1024;

    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();

    public async Task<IActionResult> OnPostKeyAsync(CancellationToken cancellationToken)
    {
        if (!glassAvailability.Enabled)
        {
            return NotFound();
        }
        if (!FromTheHelper())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        if (User.Identity?.IsAuthenticated != true)
        {
            return StatusCode(StatusCodes.Status401Unauthorized);
        }
        if (!TryGetActor(out var actor)
            || actor.Kind != ActorKind.Staff
            || !Guid.TryParse(actor.SubjectId, out var staffId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var body = await ReadAsync<KeyBody>(cancellationToken);
        if (body is null)
        {
            return BadRequest();
        }
        if (body.V != Protocol)
        {
            return UnknownProtocol(body.V, body.Version);
        }

        var keys = Keys;
        if (keys.Read(body.Frame, body.Cookies) is not { } frame)
        {
            return BadRequest();
        }

        var read = await glassSessions.FindByCallbackAsync(frame.Token, cancellationToken);
        if (read is null)
        {
            return NotFound();
        }
        var session = read.Session;
        if (staffId != session.PegasusUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        if (session.State != GlassRepairEstimateSessionState.Active)
        {
            keys.Drop(session.Id);
            return Ended();
        }

        keys.Hold(session.Id, frame);
        LogKeyHeld(logger, session.Id, frame.BalancerCookies);
        var help = HelpOn(session);
        return new JsonResult(new { help = HelpWord(help), key = "held", next = NextPoll(help) });
    }

    public async Task<IActionResult> OnPostPollAsync(CancellationToken cancellationToken)
    {
        if (!glassAvailability.Enabled)
        {
            return NotFound();
        }
        if (!FromTheHelper())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var body = await ReadAsync<PollBody>(cancellationToken);
        if (body is null)
        {
            return BadRequest();
        }
        if (body.V != Protocol)
        {
            return UnknownProtocol(body.V, body.Version);
        }
        if (!GlassHelpFrame.IsToken(body.Token))
        {
            return BadRequest();
        }

        var read = await glassSessions.FindByCallbackAsync(body.Token, cancellationToken);
        if (read is null)
        {
            return NotFound();
        }
        var session = read.Session;
        var keys = Keys;
        if (session.State != GlassRepairEstimateSessionState.Active)
        {
            keys.Drop(session.Id);
            return Ended();
        }

        var poll = keys.Poll(session.Id, body.Seen);
        var help = HelpOn(session);
        return new JsonResult(new
        {
            help = HelpWord(help),
            key = poll.Held ? "held" : "needed",
            refresh = poll.Refresh,
            next = NextPoll(help),
        });
    }

    /// <summary>
    /// Whether the staff member has turned help on for the session from the
    /// Case page (R1170, R1183): the session's own flag.
    /// </summary>
    private static bool HelpOn(GlassRepairEstimateSession session) => GlassSessionHelpPolicy.HelpOn(session);

    private static string HelpWord(bool on) => on ? "on" : "off";

    /// <summary>How long the helper waits before its next poll: shorter while help is on.</summary>
    private static int NextPoll(bool help) =>
        (int)(help ? GlassHelpKeys.HelpOnPoll : GlassHelpKeys.HelpOffPoll).TotalMilliseconds;

    /// <summary>Resolved only on a host that reaches Glass's, the one kind that composes it.</summary>
    private GlassHelpKeys Keys => HttpContext.RequestServices.GetRequiredService<GlassHelpKeys>();

    /// <summary>
    /// A JSON post from a browser extension's own origin. A page's post carries
    /// the page's origin, and a form-shaped post is not JSON.
    /// </summary>
    private bool FromTheHelper()
    {
        var origins = Request.Headers.Origin;
        return Request.HasJsonContentType()
            && origins.Count == 1
            && origins[0] is { } origin
            && origin.StartsWith(ExtensionScheme, StringComparison.Ordinal)
            && origin.Length > ExtensionScheme.Length
            && origin[ExtensionScheme.Length..].All(char.IsAsciiLetterLower);
    }

    private async Task<T?> ReadAsync<T>(CancellationToken cancellationToken)
        where T : class
    {
        if (Request.ContentLength is not (> 0 and <= MaximumBodyBytes))
        {
            return null;
        }

        try
        {
            return await Request.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonResult Ended() =>
        new(new { error = "ended" }) { StatusCode = StatusCodes.Status409Conflict };

    private JsonResult UnknownProtocol(int protocol, string? helperVersion)
    {
        LogUnknownProtocol(
            logger,
            protocol,
            helperVersion is not null && HelperVersion().IsMatch(helperVersion) ? helperVersion : "unreadable");
        return new JsonResult(new { error = "protocol", v = Protocol })
        {
            StatusCode = StatusCodes.Status426UpgradeRequired,
        };
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The Glass's helper handed over the editor key for session {SessionId} with {BalancerCookies} load balancer cookies.")]
    private static partial void LogKeyHeld(ILogger logger, Guid sessionId, int balancerCookies);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The Glass's helper spoke protocol {Protocol}, which this host does not know (helper version {HelperVersion}).")]
    private static partial void LogUnknownProtocol(ILogger logger, int protocol, string helperVersion);

    /// <summary>An extension's version as its manifest writes it; only that reaches the log.</summary>
    [GeneratedRegex(@"^\d{1,9}(\.\d{1,9}){0,3}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex HelperVersion();

    /// <summary>
    /// The key hand-off: the protocol, the editor's main-frame address, the
    /// helper's version, and the cookies the helper read in that frame, of
    /// which only the load balancer's are kept.
    /// </summary>
    private sealed record KeyBody(int V, string? Frame, string? Version, Dictionary<string, string>? Cookies);

    /// <summary>
    /// A poll: the protocol, the session's return token, the last ask to show
    /// the selected list again that the window has shown, and the helper's
    /// version.
    /// </summary>
    private sealed record PollBody(int V, string? Token, long Seen, string? Version);
}
