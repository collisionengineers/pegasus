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

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Reports = await listReports.ExecuteAsync(actor, cancellationToken);
        _staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            Reports.Select(report => report.StaffId),
            cancellationToken);
    }
}
