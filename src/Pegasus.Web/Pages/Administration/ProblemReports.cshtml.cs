using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Support;
using Pegasus.Infrastructure.Support;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// Problem reports (FRD-17): every report kept, newest first, with where it
/// went. Retry raises a Not sent report again; nothing here edits a report.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ProblemReportsModel(
    ListProblemReports listReports,
    RetryProblemReport retryReport,
    ReconcileProblemReport reconcileReport,
    IStaffAccountQueries staffAccounts) : AdministrationPageModel
{
    public IReadOnlyList<ProblemReport> Reports { get; private set; } = [];
    private IReadOnlyDictionary<Guid, string> _staffNames = new Dictionary<Guid, string>();

    public string ReportedBy(ProblemReport report) =>
        ActorDisplayNames.Resolve(ActorKind.Staff, report.StaffId.ToString("D"), _staffNames);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRetryAsync(Guid id, string operationKey, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IsOperationKeyValid(operationKey) || id == Guid.Empty)
        {
            return RedirectToPage();
        }

        var report = await retryReport.ExecuteAsync(actor, id, cancellationToken);
        if (report?.Status == ProblemReportStatus.Sent && report.IssueNumber is { } number)
        {
            TempData["Confirmation"] = OperatorLabels.ProblemReports.Reported(number);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfirmIssueAsync(
        Guid id, int issueNumber, string operationKey, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!IsOperationKeyValid(operationKey) || id == Guid.Empty || issueNumber <= 0)
        {
            TempData["ProblemReportError"] = "A valid issue number is required.";
            return RedirectToPage();
        }

        try
        {
            var issueUrl = $"https://github.com/{GitHubProblemReportOptions.ApprovedRepository}/issues/{issueNumber}";
            await reconcileReport.ConfirmIssueAsync(
                actor, id, new ProblemReportDelivery(issueNumber, issueUrl), cancellationToken);
            TempData["Confirmation"] = OperatorLabels.ProblemReports.Reported(issueNumber);
        }
        catch (ProblemReportClaimConflictException)
        {
            TempData["ProblemReportError"] = "The report status changed. Reload and check it again.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfirmNoIssueAsync(
        Guid id, string operationKey, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!IsOperationKeyValid(operationKey) || id == Guid.Empty) return RedirectToPage();
        try
        {
            await reconcileReport.ConfirmNoIssueAsync(actor, id, cancellationToken);
            TempData["Confirmation"] = "The report can now be retried.";
        }
        catch (ProblemReportClaimConflictException)
        {
            TempData["ProblemReportError"] = "The report status changed. Reload and check it again.";
        }
        return RedirectToPage();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Reports = await listReports.ExecuteAsync(actor, cancellationToken);
        _staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            Reports.Select(report => report.StaffId),
            cancellationToken);
    }
}
