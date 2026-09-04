using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Search;

/// <summary>
/// Search (EPIC-011 §1.7): the advanced filter grid over the case query, a
/// results table whose selectable rows preview the selected Case beside it.
/// </summary>
/// <remarks>
/// The grid's ten fields map 1:1 onto the existing search parameters; the
/// pre-port parameters this design does not draw (<c>case</c>,
/// <c>receivedDate</c>, <c>instructionDate</c>, <c>kind</c>) stay bound and
/// pager-preserved, so the <c>/Cases</c> bookmarks PLAT-029 redirects here
/// keep working with their values intact. The preview pane is built from
/// the row projection plus one batched Engineer-name resolve rather than
/// <c>IGetCase</c>: the wave-1 selection script needs a preview template
/// per row regardless, and this keeps the page at its two queries.
/// Terminal outcomes render their D3 "Closed · outcome" chip here — this
/// is the one work view that lists them.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class IndexModel(
    ISearchCases searchCases,
    IImageIntakeQueries imageIntakeQueries,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider,
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

    /// <summary>The row the Selected Case pane reads; the first row when unset.</summary>
    [BindProperty(SupportsGet = true, Name = "selected")]
    public Guid? SelectedId { get; set; }

    public int PageNumber { get; private set; } = 1;

    public SearchCasesResult? Results { get; private set; }

    public IReadOnlyList<ResultRow> Rows { get; private set; } = [];

    public ResultRow? Selected { get; private set; }

    public IReadOnlyList<ImageIntakeSummary> ImageIntakeResults { get; private set; } = [];

    public bool QueryFailed { get; private set; }

    /// <summary>
    /// When this page's queries last returned. Set only after they succeed,
    /// so a failed load never claims to be fresh.
    /// </summary>
    public DateTimeOffset? LoadedAtUtc { get; private set; }

    /// <summary>
    /// One results-table row and the same row's preview facts, composed
    /// here where the search item is in hand: the vehicle column, the
    /// preview's fact grid and its outstanding requirements all read the
    /// one search projection, so selecting a row needs no second query.
    /// </summary>
    public sealed record ResultRow(
        CaseSearchItem Item,
        string DetailHref,
        string SelectHref,
        string Heading,
        string Muted,
        string Chip,
        string Vehicle,
        string ProviderReference,
        string Engineer,
        string Due,
        string NextAction,
        IReadOnlyList<OperatorLabels.CaseRequirement> Outstanding);

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
                LoadedAtUtc = timeProvider.GetUtcNow();
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
            Rows = await ComposeRowsAsync(cancellationToken);

            var selectedRow = SelectedId is { } selectedId
                ? Rows.FirstOrDefault(row => row.Item.CaseId == selectedId)
                : Rows.Count > 0 ? Rows[0] : null;
            if (SelectedId is not null && selectedRow is null)
            {
                return NotFound();
            }

            if (selectedRow is not null)
            {
                SelectedId = selectedRow.Item.CaseId;
                Selected = selectedRow;
            }

            LoadedAtUtc = timeProvider.GetUtcNow();
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
                // The lifecycle state travels with the row. Omitting it took
                // ImageIntakeSummary's AwaitingInstruction default, so an
                // exact-reference hit on a merged or closed record rendered
                // the wrong chip while the registration search beside it
                // rendered the right one.
                results.Add(new ImageIntakeSummary(
                    byReference.Record.Id,
                    byReference.Record.Origin.ReceiptId,
                    byReference.Record.ImageIntakeReference,
                    byReference.Record.NormalizedVehicleRegistration,
                    byReference.AssociatedCaseId,
                    byReference.AssociatedCaseReference,
                    byReference.RegisteredAtUtc,
                    byReference.Custody,
                    byReference.State,
                    byReference.ClosureReason));
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

    /// <summary>
    /// The display rows: one batched staff-name resolve covers every
    /// Engineer on the page, and the outstanding requirements read the
    /// completeness facts the search already projected (CASE-025's rule:
    /// only a Not ready case has any).
    /// </summary>
    private async Task<IReadOnlyList<ResultRow>> ComposeRowsAsync(CancellationToken cancellationToken)
    {
        var items = Results?.Items ?? [];
        var engineerIds = items
            .Where(item => item.EngineerId is not null)
            .Select(item => item.EngineerId!.Value)
            .Distinct()
            .ToArray();
        var engineerNames = engineerIds.Length == 0
            ? (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>()
            : await ActorDisplayNames.ResolveStaffNamesAsync(
                staffAccounts,
                engineerIds,
                cancellationToken);

        return items.Select(item =>
        {
            var outstanding =
                item is { State: CaseLifecycleState.NotReady, InstructionComplete: { } instructions, ImagesComplete: { } images }
                    ? OperatorLabels.CaseRequirements(!instructions, !images)
                    : [];
            return new ResultRow(
                item,
                $"/Cases/{item.CaseId:D}",
                Href(selected: item.CaseId),
                Join(item.Reference, item.Registration),
                Join(item.Claimant, item.Principal),
                OperatorLabels.CaseStage(item.State),
                string.Join(
                    " ",
                    new[] { item.VehicleMake, item.VehicleModel }
                        .Where(part => !string.IsNullOrWhiteSpace(part))),
                item.ClaimNumber ?? "Not recorded",
                item.EngineerId is { } engineerId
                    ? ActorDisplayNames.Resolve(ActorKind.Staff, engineerId.ToString("D"), engineerNames)
                    : "Unassigned",
                item.NextChaseAtUtc is { } chase ? OperatorLabels.OfficeDate(chase) : "Not recorded",
                outstanding.Count > 0 ? outstanding[0].Resolve : "Not recorded",
                outstanding);
        }).ToArray();
    }

    /// <summary>
    /// This page's address with the given overrides. Every bound filter
    /// rides along, including the ones the grid does not draw, so paging
    /// and row selection never drop a filter an old link carried.
    /// </summary>
    public string Href(int? page = null, Guid? selected = null)
    {
        var values = RouteValues(page ?? PageNumber);
        values["selected"] = selected?.ToString("D");
        return QueryHelpers.AddQueryString(
            "/Search",
            values.Where(item => !string.IsNullOrWhiteSpace(item.Value)));
    }

    /// <summary>
    /// The fields a refresh resubmits: the active filters, page and
    /// selected row, so refreshing reruns the search the operator is
    /// looking at.
    /// </summary>
    public IReadOnlyDictionary<string, string?> RefreshFields()
    {
        var values = RouteValues(PageNumber);
        values["selected"] = SelectedId?.ToString("D");
        return values;
    }

    private Dictionary<string, string?> RouteValues(int pageNumber)
    {
        var values = new Dictionary<string, string?>
        {
            ["page"] = pageNumber.ToString(CultureInfo.InvariantCulture)
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
            ReceivedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfPresent(
            values,
            "instructionDate",
            InstructionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfPresent(
            values,
            "fromDate",
            FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfPresent(
            values,
            "toDate",
            ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfPresent(values, "origin", Origin);
        AddIfPresent(values, "query", Query);
        AddIfPresent(values, "kind", RecordKindFilter);
        return values;
    }

    /// <summary>"first · second", dropping whichever half is absent.</summary>
    public static string Join(string? first, string? second) =>
        string.Join(
            " · ",
            new[] { first, second }.Where(part => !string.IsNullOrWhiteSpace(part)));

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
