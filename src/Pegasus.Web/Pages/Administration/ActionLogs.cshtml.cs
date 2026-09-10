using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ActionLogsModel(
    ListActionLogs listActionLogs,
    GetAdministrationHealthMetrics getMetrics,
    TimeProvider timeProvider,
    IGetCaseHeader getCaseHeader,
    ISearchCases searchCases,
    IStaffAccountQueries staffAccounts) : AdministrationPageModel
{
    private const string SecurityArea = "Security";

    private readonly Dictionary<Guid, string> _caseReferences = [];
    private IReadOnlyDictionary<Guid, string> _staffNames = new Dictionary<Guid, string>();
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Area { get; set; }
    [BindProperty(SupportsGet = true)] public string? Actor { get; set; }
    [BindProperty(SupportsGet = true)] public string? ActorType { get; set; }
    [BindProperty(SupportsGet = true, Name = "Result")] public string? ResultFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? Operation { get; set; }
    [BindProperty(SupportsGet = true)] public string? Record { get; set; }
    [BindProperty(SupportsGet = true)] public string? CorrelationId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    public bool OldestFirst { get; private set; }
    public ActionLogPage Result { get; private set; } = new([], false);
    public int CurrentPage { get; private set; } = 1;
    public IReadOnlyList<StaffAccountSummary> People { get; private set; } = [];
    public string? AutomationActorSubjectId { get; private set; }
    public AdministrationHealthMetrics? Metrics { get; private set; }

    public string AutomationActorLabel => OperatorLabels.AutomationActorLabel(
        AutomationActorSubjectId ?? string.Empty,
        AutomationActorSubjectId);

    public string NextPageUrl => PageUrl(CurrentPage + 1);

    public string PreviousPageUrl => PageUrl(CurrentPage - 1);

    public string SortUrl => PageUrl(1, oldestFirst: !OldestFirst);

    private string PageUrl(int page, bool? oldestFirst = null) => "/Administration/ActionLogs?page=" + page
        + "&From=" + Query(From)
        + "&To=" + Query(To)
        + "&Search=" + Query(Search)
        + "&Area=" + Query(Area)
        + "&Actor=" + Query(Actor)
        + "&ActorType=" + Query(ActorType)
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
        await LoadPeopleAsync(cancellationToken);
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
            var record = await ResolveRecordFilterAsync(actor, cancellationToken);
            Result = await listActionLogs.ExecuteAsync(actor,
                new(from, to, Trim(Search), Trim(Area), Trim(Actor), Trim(ResultFilter),
                    Trim(Operation), record, Trim(CorrelationId), OldestFirst,
                    CurrentPage, ActorType: SelectedActorKind()?.ToString()), cancellationToken);
            Metrics = await getMetrics.ExecuteAsync(actor, timeProvider.GetUtcNow(), cancellationToken);
            await ResolveStaffNamesAsync(cancellationToken);
            await ResolveCaseReferencesAsync(actor, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(nameof(page), "Choose a valid page.");
        }
        return Page();
    }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task LoadPeopleAsync(CancellationToken cancellationToken)
    {
        var people = await staffAccounts.ListAsync(0, 100, cancellationToken);
        People = people.Accounts
            .OrderBy(account => account.UserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(account => account.Id)
            .ToArray();
        AutomationActorSubjectId = HttpContext.RequestServices
            .GetService<AutomationMcpOptions>()?
            .ClientId;
    }

    private async Task<string?> ResolveRecordFilterAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var entered = Trim(Record);
        if (entered is null || Guid.TryParse(entered, out _))
        {
            return entered;
        }

        var matches = await searchCases.ExecuteAsync(
            new(actor, new(CaseReference: entered), PageSize: 10),
            cancellationToken);
        var match = matches.Items.SingleOrDefault(item =>
            string.Equals(item.Reference, entered, StringComparison.OrdinalIgnoreCase));
        // Not every recorded reference belongs to a Case (for example, an
        // administration or security record). A recognised Case reference is
        // translated before querying; other plain references retain their
        // established exact-match behaviour.
        return match?.CaseId.ToString("D") ?? entered;
    }

    /// <summary>
    /// The recognised actor kind of one row, parsed case-insensitively: the
    /// column holds whatever a writer stored, and an unrecognised or absent
    /// kind is a row that carries no attribution rather than a parse to retry.
    /// </summary>
    public ActorKind? ActorKindOf(ActionLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Enum.TryParse<ActorKind>(row.ActorKind, ignoreCase: true, out var kind)
            && Enum.IsDefined(kind)
                ? kind
                : null;
    }

    /// <summary>
    /// True when the row's work was done by the Automation client, which the
    /// view marks with its own chip so AI activity is never read as a
    /// colleague's.
    /// </summary>
    public bool IsAiActor(ActionLogRow row) => ActorKindOf(row) == ActorKind.Automation;

    /// <summary>
    /// Who did it. A staff subject resolves to a username (a removed account to
    /// "Former staff"), the Automation client to its registered name, the Worker
    /// to the product's own name, and a legacy security row — written before the
    /// acting principal was recorded — to what the event is, never to a raw
    /// identifier or an invented user.
    /// </summary>
    public string ActorLabel(ActionLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (ActorKindOf(row) is not { } kind)
        {
            return string.Equals(row.Area, SecurityArea, StringComparison.OrdinalIgnoreCase)
                ? OperatorLabels.SecurityEventActorLabel(row.Operation)
                : Guid.TryParse(row.Actor, out _) ? ActorDisplayNames.UnknownStaff : row.Actor;
        }

        return kind switch
        {
            ActorKind.Automation => OperatorLabels.AutomationActorLabel(
                row.Actor,
                HttpContext.RequestServices.GetService<AutomationMcpOptions>()?.ClientId),
            ActorKind.SystemWorker => OperatorLabels.SystemActorLabel,
            _ => ActorDisplayNames.Resolve(kind, row.Actor, _staffNames)
        };
    }

    public string AreaLabel(ActionLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return OperatorLabels.ActionLogArea(row.Area);
    }

    /// <summary>
    /// The posted actor-type filter as a recognised kind. An unrecognised value
    /// is dropped here rather than passed on, so a hand-edited query string
    /// cannot turn the bounded filter into a refused request.
    /// </summary>
    private ActorKind? SelectedActorKind() =>
        Trim(ActorType) is { } value
        && Enum.TryParse<ActorKind>(value, ignoreCase: true, out var kind)
        && Enum.IsDefined(kind)
            ? kind
            : null;

    public string? ReferenceLabel(ActionLogRow row) =>
        IsCaseReference(row)
        && Guid.TryParse(row.Reference, out var caseId)
            ? _caseReferences.GetValueOrDefault(caseId)
            : Guid.TryParse(row.Reference, out _) ? null : row.Reference;

    private static bool IsCaseReference(ActionLogRow row) =>
        string.Equals(row.Area, "Case", StringComparison.OrdinalIgnoreCase)
        || string.Equals(row.Area, "automation_mcp", StringComparison.Ordinal)
        && row.Operation is
            "pegasus_case_get"
            or "pegasus_case_edit_begin"
            or "pegasus_case_edit_renew"
            or "pegasus_case_edit_end"
            or "pegasus_case_update_details"
            or "pegasus_document_add"
            or "pegasus_document_download"
            or "pegasus_document_export"
            or "pegasus_estimate_import"
            or "pegasus_estimate_save"
            or "pegasus_estimate_list"
            or "pegasus_assessment_get"
            or "pegasus_assessment_update";

    // Disabled and deleted accounts are retained rows, so this resolves them
    // too; only a genuinely absent identity falls through to "Former staff".
    private async Task ResolveStaffNamesAsync(CancellationToken cancellationToken) =>
        _staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            Result.Rows
                .Where(row => ActorKindOf(row) == ActorKind.Staff)
                .Select(row => Guid.TryParse(row.Actor, out var staffId) ? staffId : Guid.Empty),
            cancellationToken);

    private async Task ResolveCaseReferencesAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        foreach (var caseId in Result.Rows
                     .Where(IsCaseReference)
                     .Select(row => Guid.TryParse(row.Reference, out var id) ? id : Guid.Empty)
                     .Where(id => id != Guid.Empty)
                     .Distinct())
        {
            var header = await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (header is not null)
            {
                _caseReferences[caseId] = header.Summary.Reference;
            }
        }
    }

    private static string Query(DateTimeOffset? value) =>
        value is { } present ? Uri.EscapeDataString(present.ToString("O")) : string.Empty;

    private static string Query(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
