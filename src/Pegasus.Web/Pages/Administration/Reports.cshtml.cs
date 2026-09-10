using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;
using System.Text;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReportsModel(
    GetEngineerActivityReport engineerReport,
    GetV1ActivityReport principalActivityReport,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : AdministrationPageModel
{
    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "engineerId")] public Guid? EngineerId { get; set; }

    public EngineerActivityReport EngineerResult { get; private set; } = new(default, default, []);
    public IReadOnlyList<StaffAccountSummary> People { get; private set; } = [];

    /// <summary>
    /// The MI-02/MI-03 per-Principal read for the same period. <see langword="null"/>
    /// means the query failed or returned invalid data; the page renders that
    /// as an unavailable state rather than a false zero.
    /// </summary>
    public PrincipalReportActivityReport? PrincipalActivity { get; private set; }

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

    public async Task<IActionResult> OnGetPrincipalCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        return File(
            Encoding.UTF8.GetBytes(ReportsByPrincipalCsv(PrincipalActivity)),
            "text/csv; charset=utf-8",
            "reports-by-principal.csv");
    }

    public async Task<IActionResult> OnGetTurnaroundCsvAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Forbid();
        return File(
            Encoding.UTF8.GetBytes(TurnaroundCsv(PrincipalActivity)),
            "text/csv; charset=utf-8",
            "turnaround.csv");
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

        try
        {
            PrincipalActivity = await principalActivityReport.ExecuteAsync(actor, from, to, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            // The invalid-period case is already reported above; the two
            // reports share one period filter.
        }
        catch (InvalidDataException)
        {
            PrincipalActivity = null;
        }

        return true;
    }

    /// <summary>
    /// MI-02: per-Principal report counts by type for the period. Mirrors
    /// exactly the columns <c>Reports.cshtml</c> renders for this section.
    /// </summary>
    private static string ReportsByPrincipalCsv(PrincipalReportActivityReport? report)
    {
        var builder = new StringBuilder("Principal,Reports produced,Reports sent,Report types").Append("\r\n");
        if (report is null) return builder.ToString();
        foreach (var row in report.Rows.Where(row => row.GeneratedArtifacts > 0 || row.Sent > 0))
        {
            builder.Append(EngineerActivityReportCsv.EscapeField(row.PrincipalCode)).Append(',')
                .Append(row.GeneratedArtifacts).Append(',')
                .Append(row.Sent).Append(',')
                .Append(EngineerActivityReportCsv.EscapeField(string.Join(
                    "; ",
                    row.ArtifactTypes
                        .Where(type => type.Generated > 0)
                        .Select(type => $"{OperatorLabels.ReportKind(type.Kind)} {type.Generated}"))))
                .Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// MI-03: holding age and instruction-to-produced/ready/sent turnaround
    /// per Principal for the period. Mirrors exactly the columns
    /// <c>Reports.cshtml</c> renders for this section.
    /// </summary>
    private static string TurnaroundCsv(PrincipalReportActivityReport? report)
    {
        var builder = new StringBuilder(
            "Principal,Currently held,Oldest held since,Time to produce,Time to ready,Time to send").Append("\r\n");
        if (report is null) return builder.ToString();
        foreach (var row in report.Rows.Where(row =>
            row.CurrentHeldCases > 0
            || row.AverageReceivedToGeneration.HasValue
            || row.AverageReceivedToReady.HasValue
            || row.AverageReceivedToSent.HasValue))
        {
            builder.Append(EngineerActivityReportCsv.EscapeField(row.PrincipalCode)).Append(',')
                .Append(row.CurrentHeldCases).Append(',')
                .Append(OperatorLabels.OfficeTime(row.OldestHeldAtUtc, string.Empty)).Append(',')
                .Append(OperatorLabels.ReportTurnaround(row.AverageReceivedToGeneration, string.Empty)).Append(',')
                .Append(OperatorLabels.ReportTurnaround(row.AverageReceivedToReady, string.Empty)).Append(',')
                .Append(OperatorLabels.ReportTurnaround(row.AverageReceivedToSent, string.Empty))
                .Append("\r\n");
        }

        return builder.ToString();
    }
}
