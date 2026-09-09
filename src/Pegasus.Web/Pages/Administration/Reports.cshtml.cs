using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using System.Text;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReportsModel(
    GetEngineerActivityReport engineerReport,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : AdministrationPageModel
{
    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "engineerId")] public Guid? EngineerId { get; set; }

    public EngineerActivityReport EngineerResult { get; private set; } = new(default, default, []);
    public IReadOnlyList<StaffAccountSummary> People { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ? Page() : Forbid();

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken cancellationToken)
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
            var people = await staffAccounts.ListAsync(0, 100, cancellationToken);
            People = people.Accounts
                .Where(account => account.IsEnabled)
                .OrderBy(account => account.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(account => account.Id)
                .ToArray();
            EngineerResult = await engineerReport.ExecuteAsync(actor, from, to, EngineerId, cancellationToken);
            From ??= LondonCalendar.TimeAt(EngineerResult.FromUtc);
            To ??= LondonCalendar.TimeAt(EngineerResult.ToUtc);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, "Choose a valid London period.");
        }

        return true;
    }
}
