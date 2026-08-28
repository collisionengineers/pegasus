using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Supplies <c>ViewData["RailCounts"]</c> and <c>ViewData["ShellRenderedAtUtc"]</c>
/// on every authenticated request, so <c>_Layout.cshtml</c>'s rail counts and
/// freshness line never carry a shell-invented figure.
/// </summary>
/// <remarks>
/// The dictionary keys are the rail routes that can carry a count —
/// <c>Inbox</c>, <c>Cases</c>, <c>Operations</c>. Only <c>Cases</c> has a
/// genuinely already-queried figure behind it today
/// (<see cref="IDashboardQueries.GetCaseStageCountsAsync"/>: Not ready + Review
/// + Held, the same aggregate the Work Centre and the Cases page read; wave 3
/// extends it to the full §1.1 sum). Inbox and Operations have no established
/// figure to reuse without inventing one, so they are absent from the
/// dictionary — the layout renders nothing for a missing key, never a stale
/// zero.
///
/// A global <c>IAsyncPageFilter</c> is the direct ASP.NET Core mechanism for
/// shared per-request <c>ViewData</c>. The stage-count query is a single
/// grouped aggregate with no row projection, so running it once more per
/// request from the shell stays cheap.
/// </remarks>
public sealed class RailCountsPageFilter(IDashboardQueries dashboardQueries, TimeProvider timeProvider) : IAsyncPageFilter
{
    private readonly IDashboardQueries dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

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
                ["Cases"] = counts.NotReady + counts.Review + counts.Held
            };
            pageModel.ViewData["ShellRenderedAtUtc"] = timeProvider.GetUtcNow();
        }

        await next();
    }
}
