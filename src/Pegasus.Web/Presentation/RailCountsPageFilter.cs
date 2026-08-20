using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Supplies <c>ViewData["RailCounts"]</c> on every authenticated request, so
/// <c>_Layout.cshtml</c>'s rail badges never carry a shell-invented number.
/// </summary>
/// <remarks>
/// PLAT-003: the rail (PLAT-001) shipped with the badge mechanism but no
/// page ever populated it. Only the Queues badge gets a real figure here —
/// it is the one rail route with a genuinely already-queried number behind
/// it (<see cref="IDashboardQueries.GetCaseStageCountsAsync"/>, the same
/// query UI-02 already deployed on the Dashboard and the Queues page's own
/// tab badges). Inbox and Cases have no established figure to reuse without
/// inventing one, so they are left absent from the dictionary — the layout
/// already renders nothing for a missing key, never a stale zero.
///
/// A global <c>IAsyncPageFilter</c> is the direct ASP.NET Core mechanism for
/// shared per-request <c>ViewData</c>; nothing narrower already exists in
/// this codebase to reuse instead. <see cref="GetCaseStageCountsAsync"/> is
/// a single grouped aggregate query with no row projection (documented on
/// <c>EfDashboardQueries</c> itself), so running it once more per request
/// from the shell stays cheap.
/// </remarks>
public sealed class RailCountsPageFilter(IDashboardQueries dashboardQueries) : IAsyncPageFilter
{
    private readonly IDashboardQueries dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true
            && context.HandlerInstance is PageModel pageModel)
        {
            var counts = await dashboardQueries.GetCaseStageCountsAsync(context.HttpContext.RequestAborted);
            pageModel.ViewData["RailCounts"] = new Dictionary<string, int>
            {
                ["Queues"] = counts.NotReady + counts.Review + counts.Held
            };
        }

        await next();
    }
}
