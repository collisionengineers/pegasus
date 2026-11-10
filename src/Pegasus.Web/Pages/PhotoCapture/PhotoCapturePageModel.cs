using Microsoft.AspNetCore.Mvc.Filters;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// The Photo capture pages (FRD-28 Staff pages) answer 404 while
/// <c>Features:PhotoCapture</c> is off, before any handler runs: a capability
/// that is not composed is absent (FRD-28 Feature off).
/// </summary>
public abstract class PhotoCapturePageModel : StaffPageModel
{
    public override Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!ShellRail.PhotoCaptureEnabled(configuration))
        {
            context.Result = NotFound();
            return Task.CompletedTask;
        }

        return next();
    }
}
