using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The per-browser layout preferences the server renders in its first paint,
/// so a collapsed rail, a remembered Tabs layout or a folded panel never
/// flashes open before script runs (CSP forbids an inline script). site.js
/// writes the cookies when a preference changes; this is their one reader,
/// and every value is allow-listed: anything else reads as the default.
/// </summary>
public static partial class ShellPreferences
{
    /// <summary><c>collapsed</c> or <c>expanded</c>; one-year cookie.</summary>
    public const string RailCookie = "pegasus-rail";

    /// <summary><c>scroll</c> or <c>tabs</c>; a session cookie (v26 remembers a manual layout for the session).</summary>
    public const string CaseLayoutCookie = "pegasus-case-layout";

    /// <summary>The collapsed panel keys joined by <c>|</c>; one-year cookie.</summary>
    public const string CollapsedPanelsCookie = "pegasus-collapsed";

    public const int MaximumCollapsedPanels = 40;

    public static bool RailCollapsed(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.Equals(request.Cookies[RailCookie], "collapsed", StringComparison.Ordinal);
    }

    /// <summary>The Case record's initial layout: Tabs only when the cookie says so, else Scroll.</summary>
    public static string CaseLayout(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.Equals(request.Cookies[CaseLayoutCookie], "tabs", StringComparison.Ordinal) ? "tabs" : "scroll";
    }

    public static bool PanelCollapsed(HttpRequest request, string key)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CollapsedPanels(request.Cookies[CollapsedPanelsCookie]).Contains(key);
    }

    /// <summary>
    /// The allow-listed keys a cookie value names: at most
    /// <see cref="MaximumCollapsedPanels"/>, each 1–40 of <c>[a-z0-9.-]</c>;
    /// a malformed key is ignored rather than trusted.
    /// </summary>
    public static IReadOnlySet<string> CollapsedPanels(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumCollapsedPanels * 41)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return value.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Where(key => PanelKey().IsMatch(key))
            .Take(MaximumCollapsedPanels)
            .ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex("^[a-z0-9.-]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex PanelKey();
}
