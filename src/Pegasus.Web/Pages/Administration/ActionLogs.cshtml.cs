using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ActionLogsModel(ListActionLogs listActionLogs) : AdministrationPageModel
{
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Actor { get; set; }
    [BindProperty(SupportsGet = true)] public string? EventKind { get; set; }
    [BindProperty(SupportsGet = true)] public string? AggregateType { get; set; }
    [BindProperty(SupportsGet = true)] public string? Outcome { get; set; }
    [BindProperty(SupportsGet = true)] public string? CorrelationId { get; set; }
    public ActionLogPage Result { get; private set; } = new([], false);
    public int CurrentPage { get; private set; } = 1;

    public string NextPageUrl => "/Administration/ActionLogs?page=" + (CurrentPage + 1)
        + "&From=" + Query(From)
        + "&To=" + Query(To)
        + "&Actor=" + Query(Actor)
        + "&EventKind=" + Query(EventKind)
        + "&AggregateType=" + Query(AggregateType)
        + "&Outcome=" + Query(Outcome)
        + "&CorrelationId=" + Query(CorrelationId);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var to = To ?? DateTimeOffset.UtcNow;
        var from = From ?? to.AddDays(-31);
        CurrentPage = page;
        try
        {
            Result = await listActionLogs.ExecuteAsync(actor,
                new(from, to, Trim(Actor), Trim(EventKind), Trim(AggregateType), Trim(Outcome), Trim(CorrelationId), CurrentPage), cancellationToken);
        }
        catch (ArgumentOutOfRangeException) { ModelState.AddModelError(string.Empty, "Choose a valid UTC period."); }
        return Page();
    }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Query(DateTimeOffset? value) =>
        value is { } present ? Uri.EscapeDataString(present.ToString("O")) : string.Empty;

    private static string Query(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
