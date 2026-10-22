namespace Pegasus.Web.Health;

/// <summary>
/// Tells the keep-warm loop that a signed-in page was just served, so its next
/// tick can skip a pass that would read what that request has warmed. It is
/// registered whether or not Application Insights is, and counts the requests
/// the telemetry filter does not call quiet. The common case, a flag already
/// set, is one volatile read.
/// </summary>
internal static class PageRequestSignal
{
    public static Task InvokeAsync(HttpContext context, RequestDelegate next, StartupWarmupState state)
    {
        if (!state.PageRequestSeen && !QuietRequestTelemetryFilter.IsQuietPath(context.Request.Path))
        {
            state.NotePageRequest();
        }

        return next(context);
    }
}
