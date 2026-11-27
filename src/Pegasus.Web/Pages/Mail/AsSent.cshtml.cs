using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// One kept message as it was sent (v54 D5 to D8; R1277, R1280): the
/// sanitised document Core renders from its kept original, for the frame the
/// e-mail dialogue draws it in. A message with no formatting, or whose
/// original cannot be read, answers the empty <see cref="NoneDocument"/>, and
/// the dialogue keeps its plain text.
/// </summary>
/// <remarks>
/// Every answer carries this page's own Content Security Policy in place of
/// the site's (the site's middleware sets its header before the page runs):
/// no script, no form, no plugin, only pictures and inline styles, framed only
/// by Pegasus and sandboxed so its links open a new tab outside the sandbox.
/// A rendering is named by its version: the browser keeps it for a week when
/// the address carries <c>v</c>, and otherwise asks again each time.
/// </remarks>
public sealed class AsSentModel(GetMailAsSent getMailAsSent) : StaffPageModel, IShellFreeGet
{
    public const string ContentSecurityPolicy =
        "default-src 'none'; img-src data: https: http:; style-src 'unsafe-inline'; font-src data:; "
        + "form-action 'none'; base-uri 'none'; frame-ancestors 'self'; "
        + "sandbox allow-same-origin allow-popups allow-popups-to-escape-sandbox";

    /// <summary>The answer for a message the dialogue shows as plain text only.</summary>
    public const string NoneDocument =
        "<!DOCTYPE html><html data-mail-as-sent=\"none\"><head><meta charset=\"utf-8\"></head><body></body></html>";

    private const string HtmlContentType = "text/html; charset=utf-8";

    /// <summary>A rendering asked for without a version is checked with the server each time.</summary>
    private const string RevalidatedCacheControl = "private, no-cache";

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery] string? v = null,
        CancellationToken cancellationToken = default)
    {
        var headers = Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Robots-Tag"] = "noindex";
        headers.CacheControl = IntakeImageAddress.UncachedCacheControl;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        MailAsSent? asSent;
        try
        {
            asSent = await getMailAsSent.ExecuteAsync(actor, id, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is IntakeCustodyUnavailableException
            or IntakeArtifactIntegrityException
            or FileNotFoundException)
        {
            // An original that cannot be read is plain text (v54 D6).
            return None();
        }

        if (asSent is null)
        {
            return NotFound();
        }
        if (asSent is not { Status: MailAsSentStatus.Available, Html: { } html, Version: { Length: > 0 } version })
        {
            return None();
        }

        var etag = $"\"{version}\"";
        headers.ETag = etag;
        headers.CacheControl = string.IsNullOrEmpty(v) ? RevalidatedCacheControl : IntakeImageAddress.KeptCacheControl;
        if (Pegasus.Web.Pages.Cases.Documents.DownloadModel.HoldsRepresentation(Request, etag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }
        return Content(html, HtmlContentType);
    }

    private ContentResult None()
    {
        Response.Headers.CacheControl = IntakeImageAddress.UncachedCacheControl;
        return Content(NoneDocument, HtmlContentType);
    }
}
