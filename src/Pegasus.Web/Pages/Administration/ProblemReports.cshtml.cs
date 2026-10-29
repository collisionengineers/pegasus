using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Support;
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

    public async Task<IActionResult> OnPostRetryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (id == Guid.Empty)
        {
            return RedirectToPage();
        }

        ProblemReport? report;
        try
        {
            report = await retryReport.ExecuteAsync(actor, id, cancellationToken);
        }
        catch (ProblemReportClaimConflictException)
        {
            TempData["ProblemReportError"] = OperatorLabels.ProblemReports.StatusChanged;
            return RedirectToPage();
        }

        // Every outcome in the words the staff Send uses (R424). A removed
        // report, or one another Retry is still sending, says nothing (R423).
        switch (report)
        {
            case { Status: ProblemReportStatus.Sent, IssueNumber: { } number }:
                TempData["Confirmation"] = OperatorLabels.ProblemReports.Reported(number);
                break;
            case { Status: ProblemReportStatus.Unknown }:
                TempData["ProblemReportError"] = OperatorLabels.ProblemReports.Unknown;
                break;
            case { Status: ProblemReportStatus.NotSent, DispatchClaimToken: null }:
                TempData["ProblemReportError"] = OperatorLabels.ProblemReports.Kept;
                break;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfirmIssueAsync(
        Guid id, int issueNumber, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty || issueNumber <= 0)
        {
            TempData["ProblemReportError"] = OperatorLabels.ProblemReports.IssueNumberRequired;
            return RedirectToPage();
        }

        try
        {
            await reconcileReport.ConfirmIssueAsync(actor, id, issueNumber, cancellationToken);
            TempData["Confirmation"] = OperatorLabels.ProblemReports.Reported(issueNumber);
        }
        catch (ProblemReportClaimConflictException)
        {
            TempData["ProblemReportError"] = OperatorLabels.ProblemReports.StatusChanged;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfirmNoIssueAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return RedirectToPage();
        try
        {
            await reconcileReport.ConfirmNoIssueAsync(actor, id, cancellationToken);
            TempData["Confirmation"] = OperatorLabels.ProblemReports.CanBeRetried;
        }
        catch (ProblemReportClaimConflictException)
        {
            TempData["ProblemReportError"] = OperatorLabels.ProblemReports.StatusChanged;
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
