using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages.Capture;

/// <summary>What the capture app's document embeds: its context and its image manifest, both JSON.</summary>
public sealed record CaptureDocumentView(string ContextJson, string AssetsJson)
{
    public static CaptureDocumentView Empty { get; } = new("null", "{}");
}

/// <summary>
/// The capture surface's one card: a heading and a line, in the app's own
/// styling and with nothing that leads into the staff application.
/// </summary>
public sealed record CaptureCardView(string Heading, string Line)
{
    /// <summary>The one answer for an unknown, expired, revoked or paused link (FRD-28).</summary>
    public static CaptureCardView Unavailable { get; } =
        new("This link is no longer active.", "Ask whoever sent it to you for a new one.");

    public const string PartialName = "/Pages/Capture/_CaptureUnavailable.cshtml";

    /// <summary>The unavailable card as the page's answer, with the same 404 the API gives.</summary>
    public static PartialViewResult UnavailableResult(PageModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var result = page.Partial(PartialName, Unavailable);
        result.StatusCode = StatusCodes.Status404NotFound;
        return result;
    }
}
