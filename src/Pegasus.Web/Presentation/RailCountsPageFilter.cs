using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Pegasus.Core.Actors;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Supplies <c>ViewData["RailCounts"]</c> and <c>ViewData["ShellRenderedAtUtc"]</c>
/// on every authenticated request, so <c>_Layout.cshtml</c>'s rail counts and
/// freshness line never carry a shell-invented figure.
/// </summary>
/// <remarks>
/// The dictionary keys are the rail routes that can carry a count —
/// <c>Inbox</c>, <c>Cases</c>, <c>Operations</c>. <c>Cases</c> is the EPIC-011
/// §1.1 contract sum, not_ready + review + with_engineer + held + triage +
/// unidentified, read from the same queries the Cases page itself runs:
/// <see cref="IDashboardQueries.GetCaseStageCountsAsync"/> (one grouped
/// aggregate), <see cref="IListTriage"/> (the open-Triage total; the rows are
/// not projected beyond page one) and
/// <see cref="IUnidentifiedStore.ListQueueAsync"/>. Inbox and Operations have
/// no established figure to reuse without inventing one, so they are absent
/// from the dictionary — the layout renders nothing for a missing key, never
/// a stale zero.
///
/// A global <c>IAsyncPageFilter</c> is the direct ASP.NET Core mechanism for
/// shared per-request <c>ViewData</c>.
/// </remarks>
public sealed class RailCountsPageFilter(
    IDashboardQueries dashboardQueries,
    IListTriage listTriage,
    IUnidentifiedStore unidentifiedStore,
    IGetAttentionRows getAttentionRows,
    TimeProvider timeProvider) : IAsyncPageFilter
{
    private readonly IDashboardQueries dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly IListTriage listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly IUnidentifiedStore unidentifiedStore =
        unidentifiedStore ?? throw new ArgumentNullException(nameof(unidentifiedStore));
    private readonly IGetAttentionRows getAttentionRows =
        getAttentionRows ?? throw new ArgumentNullException(nameof(getAttentionRows));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true
            && context.HandlerInstance is PageModel pageModel
            && StaffActorFactory.TryCreate(
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                user.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            var cancellationToken = context.HttpContext.RequestAborted;
            var stagesTask = dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
            var triageTask = listTriage.ExecuteAsync(new(actor, State: null, Page: 1, PageSize: 1), cancellationToken);
            var unidentifiedTask = unidentifiedStore.ListQueueAsync(null, cancellationToken);
            // Work Centre already holds its own full snapshot and slices its
            // own top ten (Pages/Index.cshtml.cs) — calling the narrow query
            // again here would be a second read of the same rows.
            var isWorkCentre = pageModel is Pegasus.Web.Pages.IndexModel;
            var attentionRowsTask = isWorkCentre
                ? null
                : getAttentionRows.ExecuteAsync(actor, cancellationToken);

            await (attentionRowsTask is null
                ? Task.WhenAll(stagesTask, triageTask, unidentifiedTask)
                : Task.WhenAll(stagesTask, triageTask, unidentifiedTask, attentionRowsTask));

            var stages = stagesTask.Result;
            pageModel.ViewData["RailCounts"] = new Dictionary<string, int>
            {
                ["Cases"] = stages.NotReady
                    + stages.Review
                    + stages.WithEngineer
                    + stages.Held
                    + stages.AwaitingInstruction
                    + triageTask.Result.TotalCount
                    + unidentifiedTask.Result.Count
            };
            pageModel.ViewData["ShellRenderedAtUtc"] = timeProvider.GetUtcNow();

            if (attentionRowsTask is not null)
            {
                pageModel.ViewData["AttentionRows"] = attentionRowsTask.Result;
            }
        }

        await next();
    }
}
