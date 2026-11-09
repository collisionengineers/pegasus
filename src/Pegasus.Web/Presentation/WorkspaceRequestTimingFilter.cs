using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Times workspace execution before PageModel activation, and result rendering
/// separately. Phase names distinguish full pages from section/refresh requests;
/// no route values or user data are emitted. The Case page's request-to-edit
/// long poll (held up to 25 s) and its lease heartbeat are not page work and
/// get no phase, so the page's figures stay page figures (#1950).
/// </summary>
public sealed class WorkspaceRequestTimingFilter : IAsyncResourceFilter, IAsyncResultFilter
{
    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var prefix = PhasePrefix(context);
        using var timing = prefix is not null
            ? DocumentReadTelemetry.Start(prefix + ".resource")
            : null;
        await next();
    }

    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        var prefix = PhasePrefix(context);
        using var timing = prefix is not null
            ? DocumentReadTelemetry.Start(prefix + ".result")
            : null;
        await next();
    }

    private static string? PhasePrefix(FilterContext context) =>
        (context.ActionDescriptor as PageActionDescriptor)?.ViewEnginePath switch
        {
            "/Cases/Details" when QuietRequestTelemetryFilter.IsQuietHandler(
                context.HttpContext.Request.Path, context.HttpContext.Request.QueryString) => null,
            "/Cases/Details" => HasHandler(context, "Section") ? "web.case.section" : "web.case",
            "/Index" => HasHandler(context, "Refresh") ? "web.workcentre.refresh" : "web.workcentre",
            _ => null
        };

    private static bool HasHandler(FilterContext context, string handler) =>
        string.Equals(context.RouteData.Values["handler"]?.ToString(), handler, StringComparison.OrdinalIgnoreCase)
        || string.Equals(context.HttpContext.Request.Query["handler"].ToString(), handler, StringComparison.OrdinalIgnoreCase);
}
