using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class IndexModel(
    ISearchCases searchCases,
    ILogger<IndexModel> logger) : PageModel
{
    private const int ResultsPerPage = 25;

    [BindProperty(SupportsGet = true, Name = "case")]
    public string? CaseReference { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Registration { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Claimant { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ClaimNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Principal { get; set; }

    [BindProperty(SupportsGet = true)]
    public CaseLifecycleState? State { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? EngineerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ReceivedDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? InstructionDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Origin { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }


    public int PageNumber { get; private set; } = 1;

    public SearchCasesResult? Results { get; private set; }

    public bool QueryFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int? pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        PageNumber = pageNumber ?? 1;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Results = await searchCases.ExecuteAsync(
                new(
                    actor,
                    new(
                        CaseReference,
                        Registration,
                        Claimant,
                        ClaimNumber,
                        Principal,
                        State,
                        EngineerId,
                        ReceivedDate,
                        InstructionDate,
                        FromDate,
                        ToDate,
                        Origin,
                        Query),
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
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return Page();
    }

    public Dictionary<string, string?> RouteValues(int pageNumber)
    {
        var values = new Dictionary<string, string?>
        {
            ["page"] = pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        AddIfPresent(values, "case", CaseReference);
        AddIfPresent(values, "registration", Registration);
        AddIfPresent(values, "claimant", Claimant);
        AddIfPresent(values, "claimNumber", ClaimNumber);
        AddIfPresent(values, "principal", Principal);
        AddIfPresent(values, "state", State?.ToString());
        AddIfPresent(values, "engineerId", EngineerId?.ToString("D"));
        AddIfPresent(
            values,
            "receivedDate",
            ReceivedDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        AddIfPresent(
            values,
            "instructionDate",
            InstructionDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        AddIfPresent(
            values,
            "fromDate",
            FromDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        AddIfPresent(
            values,
            "toDate",
            ToDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        AddIfPresent(values, "origin", Origin);
        AddIfPresent(values, "query", Query);
        return values;
    }

    public string PageUrl(int pageNumber) =>
        QueryHelpers.AddQueryString("/Cases", RouteValues(pageNumber));

    private static void AddIfPresent(
        Dictionary<string, string?> values,
        string key,
        string? value)
    {
        if (value is not null)
        {
            values[key] = value;
        }
    }

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
        Message = "The authorized case search query failed.")]
    private static partial void LogCaseSearchFailed(ILogger logger, Exception exception);
}
