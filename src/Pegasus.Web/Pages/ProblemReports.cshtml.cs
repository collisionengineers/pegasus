using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Core.Support;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// Report a problem (FRD-12): the shell dialog and the Error page post here.
/// The report is stored, then raised as an issue; the person returns to the
/// page they were on with the outcome as a confirmation, or the reason it was
/// kept as Not sent.
/// </summary>
[Authorize]
public sealed class ProblemReportsModel(
    ReportProblem reportProblem,
    ApplicationBuild build,
    IMemoryCache cache) : StaffPageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostReportAsync(
        string? description,
        string? route,
        string? traceId,
        string? caseReference,
        string? viewport,
        bool editing,
        string? errors,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var back = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        ProblemReport report;
        try
        {
            report = await reportProblem.ExecuteAsync(
                ProblemReportRequests.Build(
                    HttpContext,
                    actor,
                    build,
                    cache,
                    new ProblemReportRequests.PostedFacts(description, route, traceId, caseReference, viewport, editing, errors)),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            TempData["ProblemReportError"] = exception.Message;
            return LocalRedirect(back);
        }

        if (report.Status == ProblemReportStatus.Sent && report.IssueNumber is { } number)
        {
            TempData["Confirmation"] = OperatorLabels.ProblemReports.Reported(number);
        }
        else
        {
            TempData["ProblemReportError"] = OperatorLabels.ProblemReports.Kept;
        }

        return LocalRedirect(back);
    }
}
