using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using System.Text;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReportsModel(
    GetV1ActivityReport report,
    GetEngineerActivityReport engineerReport,
    TimeProvider timeProvider) : AdministrationPageModel
{
    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }

    public PrincipalReportActivityReport Result { get; private set; } = new(default, default, []);
    public EngineerActivityReport EngineerResult { get; private set; } = new(default, default, []);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ? Page() : Forbid();

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        return File(
            Encoding.UTF8.GetBytes(PrincipalReportActivityCsv.ToCsv(Result.Rows)),
            "text/csv; charset=utf-8",
            "principal-report-activity.csv");
    }

    public async Task<IActionResult> OnGetEngineerCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        return File(
            Encoding.UTF8.GetBytes(EngineerActivityReportCsv.ToCsv(EngineerResult.Rows)),
            "text/csv; charset=utf-8",
            "engineer-activity.csv");
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return false;
        var to = To is { } localTo ? LondonCalendar.ToUtc(localTo) : timeProvider.GetUtcNow();
        var from = From is { } localFrom ? LondonCalendar.ToUtc(localFrom) : to.AddDays(-31);
        try
        {
            Result = await report.ExecuteAsync(actor, from, to, cancellationToken);
            EngineerResult = await engineerReport.ExecuteAsync(actor, from, to, null, cancellationToken);
            From ??= LondonCalendar.TimeAt(Result.FromUtc);
            To ??= LondonCalendar.TimeAt(Result.ToUtc);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, "Choose a valid London period.");
        }

        return true;
    }
}
