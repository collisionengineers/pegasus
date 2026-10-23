namespace Pegasus.Web.Health;

/// <summary>
/// Tells the keep-warm loop that a signed-in page was just served, so its next
/// tick can skip a pass that would read what that request has warmed. It is
/// registered after authentication, whether or not Application Insights is, and
/// counts only a signed-in request the telemetry filter does not call quiet, so
/// the Always On ping, scanners and sign-in pages never skip a pass. The common
/// case, a flag already set, is one volatile read.
/// </summary>
internal static class PageRequestSignal
{
    public static Task InvokeAsync(HttpContext context, RequestDelegate next, StartupWarmupState state)
    {
        if (!state.PageRequestSeen
            && context.User.Identity?.IsAuthenticated == true
            && !QuietRequestTelemetryFilter.IsQuietPath(context.Request.Path))
        {
            state.NotePageRequest();
        }

        return next(context);
    }
}
