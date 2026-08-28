using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Search;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class IndexModel(
    ISearchCases searchCases,
    IImageIntakeQueries imageIntakeQueries,
    ILogger<IndexModel> logger) : StaffPageModel
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

    [BindProperty(SupportsGet = true, Name = "kind")]
    public string? RecordKindFilter { get; set; }


    public int PageNumber { get; private set; } = 1;

    public SearchCasesResult? Results { get; private set; }

    public IReadOnlyList<ImageIntakeSummary> ImageIntakeResults { get; private set; } = [];

    public bool QueryFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int? pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (RecordKindFilter is not (null or "" or "instructions" or "images"))
        {
            return NotFound();
        }
        PageNumber = pageNumber ?? 1;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            if (RecordKindFilter != "instructions")
            {
                await LoadImageIntakeResultsAsync(cancellationToken);
            }

            if (RecordKindFilter == "images")
            {
                return Page();
            }

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

    /// <summary>
    /// The additive Image-intake lookup for UI-07: an exact Image Intake
    /// Reference or a registration search surfaces Image-intake rows beside
    /// case results. Case-search schema is unchanged — an Image Intake
    /// Reference is not a Case reference. With the `Images` filter and no
    /// search input, every Image intake is listed.
    /// </summary>
    private async Task LoadImageIntakeResultsAsync(CancellationToken cancellationToken)
    {
        var results = new List<ImageIntakeSummary>();
        var seen = new HashSet<Guid>();
        foreach (var raw in new[] { CaseReference, Query })
        {
            var candidate = raw?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            var byReference = await imageIntakeQueries.GetByReferenceAsync(
                candidate,
                cancellationToken);
            if (byReference is not null && seen.Add(byReference.Record.Id))
            {
                results.Add(new ImageIntakeSummary(
                    byReference.Record.Id,
                    byReference.Record.Origin.ReceiptId,
                    byReference.Record.ImageIntakeReference,
                    byReference.Record.NormalizedVehicleRegistration,
                    byReference.AssociatedCaseId,
                    byReference.AssociatedCaseReference,
                    byReference.RegisteredAtUtc));
            }
        }

        foreach (var raw in new[] { Registration, Query })
        {
            var compact = new string((raw ?? string.Empty)
                .ToUpperInvariant()
                .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
                .ToArray());
            if (compact.Length == 0)
            {
                continue;
            }

            foreach (var summary in await imageIntakeQueries.SearchByRegistrationAsync(
                compact,
                cancellationToken))
            {
                if (seen.Add(summary.Id))
                {
                    results.Add(summary);
                }
            }
        }

        if (results.Count == 0
            && RecordKindFilter == "images"
            && string.IsNullOrWhiteSpace(CaseReference)
            && string.IsNullOrWhiteSpace(Registration)
            && string.IsNullOrWhiteSpace(Query))
        {
            results.AddRange(await imageIntakeQueries.ListAsync(null, cancellationToken));
        }

        ImageIntakeResults = results;
    }

    public static string ImageIntakeOutcomeLabel(ImageIntakeSummary summary) =>
        summary.AssociatedCaseId is null ? "Image intake registered" : "Associated with Case";

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
        AddIfPresent(values, "kind", RecordKindFilter);
        return values;
    }

    public string PageUrl(int pageNumber) =>
        QueryHelpers.AddQueryString("/Search", RouteValues(pageNumber));

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

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The authorized case search query failed.")]
    private static partial void LogCaseSearchFailed(ILogger logger, Exception exception);
}
