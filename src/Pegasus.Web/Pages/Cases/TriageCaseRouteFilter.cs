using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case sub-routes below are surfaces of the Case workflow, which a Triage
/// Case does not have, so they are not found for a Triage Case identity. The
/// document download stays open: a Triage Case keeps standard Case files.
/// </summary>
public sealed class TriageCaseRouteFilter(IGetCaseKind getCaseKind) : IAsyncPageFilter
{
    /// <summary>The Case sub-route pages the filter guards, by view-engine path.</summary>
    public static readonly IReadOnlySet<string> GuardedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/Cases/Custody",
        "/Cases/Tasks",
        "/Cases/Vehicle",
        "/Cases/Workflow",
        "/Cases/Closure",
        "/Cases/Assessment/Index",
        "/Cases/Documents/Export",
        "/Cases/Eva/Send"
    };

    private readonly IGetCaseKind _getCaseKind = getCaseKind ?? throw new ArgumentNullException(nameof(getCaseKind));

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (TryGetCaseId(context, out var caseId)
            && await _getCaseKind.ExecuteAsync(caseId, context.HttpContext.RequestAborted) == CaseType.Triage)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }

    private static bool TryGetCaseId(PageHandlerExecutingContext context, out Guid caseId)
    {
        var values = context.RouteData.Values;
        var value = values.TryGetValue("id", out var id)
            ? id
            : values.TryGetValue("caseId", out var alternative) ? alternative : null;
        return Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out caseId);
    }
}
