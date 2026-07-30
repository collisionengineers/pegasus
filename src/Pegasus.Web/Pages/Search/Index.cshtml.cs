using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Search;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class IndexModel(
    ISearchCases searchCases,
    ILogger<IndexModel> logger) : PageModel
{
    private const int ResultsPerPage = 25;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    public int PageNumber { get; private set; } = 1;

    public SearchCasesResult? Results { get; private set; }

    public bool QueryFailed { get; private set; }

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int? pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        PageNumber = pageNumber ?? 1;
        if (!ModelState.IsValid || !HasQuery)
        {
            return Page();
        }

        try
        {
            Results = await searchCases.ExecuteAsync(
                new(
                    actor,
                    new CaseSearchFilters(Query: Query),
                    PageNumber,
                    ResultsPerPage),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseSearchFailed(logger, exception);
            QueryFailed = true;
        }

        return Page();
    }

    public Dictionary<string, string?> RouteValues(int pageNumber)
    {
        var values = new Dictionary<string, string?>
        {
            ["page"] = pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (Query is not null)
        {
            values["q"] = Query;
        }

        return values;
    }

    public string PageUrl(int pageNumber) =>
        QueryHelpers.AddQueryString("/Search", RouteValues(pageNumber));

    private bool TryGetActor(out ActionActor actor)
    {
        if (StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var resolved))
        {
            actor = resolved;
            return true;
        }

        actor = null!;
        return false;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The authorized global case search query failed.")]
    private static partial void LogCaseSearchFailed(ILogger logger, Exception exception);
}
