namespace Pegasus.Web.Health;

/// <summary>
/// Tells the keep-warm loop that a signed-in page was just served, so its next
/// tick can skip a pass that would read what that request has warmed. The
/// pipeline calls it after routing and authentication, whether or not
/// Application Insights is registered, for a request whose endpoint is a Razor
/// page (the caller has already found that endpoint metadata), and it counts
/// only a signed-in GET that names no handler. A POST, a handler fetch (the status poll, a
/// dialog fragment), a lease or edit heartbeat, a static file, the Always On
/// ping, a scanner and a sign-in page never skip a pass, because none of them
/// reads what the pass warms. The common case, a flag already set, is one
/// volatile read.
/// </summary>
internal static class PageRequestSignal
{
    public static void Note(HttpContext context, StartupWarmupState state)
    {
        if (!state.PageRequestSeen
            && HttpMethods.IsGet(context.Request.Method)
            && !NamesHandler(context.Request.QueryString)
            && context.User.Identity?.IsAuthenticated == true
            && !QuietRequestTelemetryFilter.IsQuietPath(context.Request.Path))
        {
            state.NotePageRequest();
        }
    }

    // A handler is asked for by name in the query, so the page-model's own
    // fetches (status, dialog, preview) carry it and the page's own GET never does.
    private static bool NamesHandler(QueryString query) =>
        query.HasValue && query.Value!.Contains("handler=", StringComparison.OrdinalIgnoreCase);
}
