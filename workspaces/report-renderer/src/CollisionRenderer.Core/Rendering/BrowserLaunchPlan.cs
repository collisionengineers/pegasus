namespace CollisionRenderer.Core.Rendering;

/// <summary>
/// One browser-launch candidate for the Chromium PDF engine: the bundled Playwright
/// build (<see cref="Channel"/> null — resolved via <c>PLAYWRIGHT_BROWSERS_PATH</c>) or a
/// system browser channel (<c>msedge</c>/<c>chrome</c>) that Playwright locates from its
/// well-known install paths without any downloaded browser.
/// </summary>
public sealed record BrowserLaunchCandidate(string Kind, string? Channel)
{
    public static readonly BrowserLaunchCandidate Bundled = new("bundled", null);
    public static readonly BrowserLaunchCandidate Edge = new("msedge", "msedge");
    public static readonly BrowserLaunchCandidate Chrome = new("chrome", "chrome");
}

/// <summary>
/// How the engine actually resolved (or failed to resolve) a browser. <see cref="Kind"/> is
/// <c>bundled</c>/<c>msedge</c>/<c>chrome</c> on success, <c>missing</c> when every candidate
/// failed; <see cref="Attempts"/> holds one line per failed candidate for diagnostics.
/// </summary>
public sealed record BrowserResolution(string Kind, string? Channel, IReadOnlyList<string> Attempts);

/// <summary>
/// Ordered launch candidates for the PDF engine. The bundled headless shell is always the
/// default; the system Edge then Chrome channels follow so a broken or missing bundled
/// browser degrades to a browser already on the machine (staff Windows boxes always have
/// Edge, usually Chrome) instead of failing the render outright.
/// </summary>
public static class BrowserLaunchPlan
{
    /// <summary>
    /// Build the candidate order. <paramref name="channelPin"/> (normally the
    /// <c>COLLISIONRENDERER_BROWSER_CHANNEL</c> env var / <c>browser_channel</c> user config)
    /// moves a system channel to the front. Unset, <c>bundled</c>, unknown values, and an
    /// unexpanded <c>${user_config…}</c> token (Claude Desktop leaves the literal template
    /// when the optional field was never set) all mean the default order.
    /// </summary>
    public static IReadOnlyList<BrowserLaunchCandidate> Build(string? channelPin)
    {
        var pin = Normalize(channelPin);
        return pin switch
        {
            "msedge" => new[] { BrowserLaunchCandidate.Edge, BrowserLaunchCandidate.Bundled, BrowserLaunchCandidate.Chrome },
            "chrome" => new[] { BrowserLaunchCandidate.Chrome, BrowserLaunchCandidate.Bundled, BrowserLaunchCandidate.Edge },
            _ => new[] { BrowserLaunchCandidate.Bundled, BrowserLaunchCandidate.Edge, BrowserLaunchCandidate.Chrome },
        };
    }

    private static string Normalize(string? channelPin)
    {
        if (string.IsNullOrWhiteSpace(channelPin) || channelPin.Contains("${", StringComparison.Ordinal))
        {
            return "bundled";
        }

        return channelPin.Trim().ToLowerInvariant() switch
        {
            "msedge" or "edge" or "microsoft-edge" => "msedge",
            "chrome" or "google-chrome" => "chrome",
            _ => "bundled",
        };
    }
}
