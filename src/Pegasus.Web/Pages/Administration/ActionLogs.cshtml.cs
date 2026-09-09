using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ActionLogsModel(
    ListActionLogs listActionLogs,
    TimeProvider timeProvider) : AdministrationPageModel
{
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Area { get; set; }
    [BindProperty(SupportsGet = true)] public string? Actor { get; set; }
    [BindProperty(SupportsGet = true, Name = "Result")] public string? ResultFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? Operation { get; set; }
    [BindProperty(SupportsGet = true)] public string? Record { get; set; }
    [BindProperty(SupportsGet = true)] public string? CorrelationId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    public bool OldestFirst { get; private set; }
    public ActionLogPage Result { get; private set; } = new([], false);
    public int CurrentPage { get; private set; } = 1;

    public string NextPageUrl => PageUrl(CurrentPage + 1);

    public string PreviousPageUrl => PageUrl(CurrentPage - 1);

    public string SortUrl => PageUrl(1, oldestFirst: !OldestFirst);

    private string PageUrl(int page, bool? oldestFirst = null) => "/Administration/ActionLogs?page=" + page
        + "&From=" + Query(From)
        + "&To=" + Query(To)
        + "&Search=" + Query(Search)
        + "&Area=" + Query(Area)
        + "&Actor=" + Query(Actor)
        + "&Result=" + Query(ResultFilter)
        + "&Operation=" + Query(Operation)
        + "&Record=" + Query(Record)
        + "&CorrelationId=" + Query(CorrelationId)
        + "&Sort=" + ((oldestFirst ?? OldestFirst) ? "oldest" : string.Empty);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        var to = To ?? timeProvider.GetUtcNow();
        var from = From ?? to.AddDays(-31);
        From = from;
        To = to;
        CurrentPage = page < 1 ? 1 : page;
        OldestFirst = string.Equals(Sort, "oldest", StringComparison.Ordinal);
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Choose a valid page.");
            return Page();
        }
        if (from >= to || to - from > ListActionLogs.MaximumPeriod)
        {
            ModelState.AddModelError(string.Empty, "Choose a valid UTC period.");
            return Page();
        }
        try
        {
            Result = await listActionLogs.ExecuteAsync(actor,
                new(from, to, Trim(Search), Trim(Area), Trim(Actor), Trim(ResultFilter),
                    Trim(Operation), Trim(Record), Trim(CorrelationId), OldestFirst,
                    CurrentPage), cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(nameof(page), "Choose a valid page.");
        }
        return Page();
    }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Query(DateTimeOffset? value) =>
        value is { } present ? Uri.EscapeDataString(present.ToString("O")) : string.Empty;

    private static string Query(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
