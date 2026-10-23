using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Health;

/// <summary>
/// Tells the keep-warm loop that a signed-in page was just served, so its next
/// tick can skip a pass that would read what that request has warmed. It is
/// registered after routing and authentication, whether or not Application
/// Insights is, and counts only a signed-in GET whose endpoint is a Razor page
/// and that names no handler. A POST, a handler fetch (the status poll, a
/// dialog fragment), a lease or edit heartbeat, a static file, the Always On
/// ping, a scanner and a sign-in page never skip a pass, because none of them
/// reads what the pass warms. The common case, a flag already set, is one
/// volatile read.
/// </summary>
internal static class PageRequestSignal
{
    public static Task InvokeAsync(HttpContext context, RequestDelegate next, StartupWarmupState state)
    {
        if (!state.PageRequestSeen
            && HttpMethods.IsGet(context.Request.Method)
            && context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>() is not null
            && !NamesHandler(context.Request.QueryString)
            && context.User.Identity?.IsAuthenticated == true
            && !QuietRequestTelemetryFilter.IsQuietPath(context.Request.Path))
        {
            state.NotePageRequest();
        }

        return next(context);
    }

    // A handler is asked for by name in the query, so the page-model's own
    // fetches (status, dialog, preview) carry it and the page's own GET never does.
    private static bool NamesHandler(QueryString query) =>
        query.HasValue && query.Value!.Contains("handler=", StringComparison.OrdinalIgnoreCase);
}
