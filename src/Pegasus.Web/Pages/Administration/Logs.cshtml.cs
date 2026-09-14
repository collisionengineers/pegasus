using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// Administration › Logs (13 September): two tabs on one page — Action logs as
/// before, and the Intake log, every received item with its outcome, what it
/// became and the technical actions (Re-evaluate, Retry allocation, Retry OCR)
/// in a row drawer. Administrators only.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class LogsModel(
    ListActionLogs listActionLogs,
    GetAdministrationHealthMetrics getMetrics,
    TimeProvider timeProvider,
    IGetCaseHeader getCaseHeader,
    ISearchCases searchCases,
    IStaffAccountQueries staffAccounts,
    IAiJobStore aiJobs,
    IListIntakeLog listIntakeLog,
    IReevaluateIntake reevaluateIntake,
    IRetryIntakeOcr retryIntakeOcr,
    IAllocateIntake allocateIntake) : AdministrationPageModel
{
    private const string SecurityArea = "Security";
    private const string AiJobArea = "ai_job";
    public const string IntakeTab = "intake";

    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public IntakeLogOutcome? IntakeOutcome { get; set; }
    [BindProperty(SupportsGet = true)] public IntakeSourceChannel? IntakeSource { get; set; }
    [BindProperty(SupportsGet = true)] public string? IntakePrincipal { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? IntakeFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? IntakeTo { get; set; }
    [BindProperty(SupportsGet = true)] public string? IntakeText { get; set; }
    [BindProperty(SupportsGet = true)] public int? IntakePage { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Receipt { get; set; }

    public bool IsIntakeTab => string.Equals(Tab, IntakeTab, StringComparison.Ordinal);
    public IntakeLogPage IntakeLog { get; private set; } = new([], 1, IntakeLogPolicy.PageSize, 0);
    public IntakeLogCounts? IntakeCounts { get; private set; }
    public IntakeLogDetail? IntakeDetail { get; private set; }

    [TempData] public string? LogsError { get; set; }

    private readonly Dictionary<Guid, string> _caseReferences = [];
    private readonly Dictionary<Guid, AiJobReference> _aiJobReferences = [];
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

    private string PageUrl(int page, bool? oldestFirst = null) => "/Administration/Logs?page=" + page
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
        if (IsIntakeTab)
        {
            return await LoadIntakeLogAsync(actor, cancellationToken);
        }

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
            await ResolveAiJobReferencesAsync(cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(nameof(page), "Choose a valid page.");
        }
        return Page();
    }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<IActionResult> LoadIntakeLogAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        IntakePage = Math.Clamp(IntakePage ?? 1, 1, 10_000);
        var filter = new IntakeLogFilter(
            IntakeOutcome is { } outcome && Enum.IsDefined(outcome) ? outcome : null,
            IntakeSource is { } source && Enum.IsDefined(source) ? source : null,
            Trim(IntakePrincipal),
            IntakeFrom is { } from ? Pegasus.Core.LondonCalendar.StartOfDay(from) : null,
            IntakeTo is { } to ? Pegasus.Core.LondonCalendar.StartOfDay(to.AddDays(1)) : null,
            Trim(IntakeText));
        try
        {
            IntakeCounts = await listIntakeLog.CountsAsync(actor, cancellationToken);
            IntakeLog = await listIntakeLog.ExecuteAsync(actor, filter, IntakePage.Value, cancellationToken);
            if (Receipt is { } receiptId && receiptId != Guid.Empty)
            {
                IntakeDetail = await listIntakeLog.GetAsync(actor, receiptId, cancellationToken);
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return Page();
    }

    public Task<IActionResult> OnPostReevaluateIntakeAsync(
        Guid receiptId,
        long expectedVersion,
        string operationKey,
        string? reason,
        string? returnUrl,
        CancellationToken cancellationToken) =>
        ExecuteIntakeActionAsync(
            receiptId,
            returnUrl,
            reason,
            actor => reevaluateIntake.ExecuteAsync(
                new(receiptId, expectedVersion, actor, operationKey, reason!.Trim()),
                cancellationToken),
            "Re-evaluation with current policy was queued.");

    /// <summary>Retry OCR: Core records the retry and the Worker runs the OCR again.</summary>
    public Task<IActionResult> OnPostRetryIntakeOcrAsync(
        Guid receiptId,
        long expectedVersion,
        string operationKey,
        string? reason,
        string? returnUrl,
        CancellationToken cancellationToken) =>
        ExecuteIntakeActionAsync(
            receiptId,
            returnUrl,
            reason,
            actor => retryIntakeOcr.ExecuteAsync(
                new(receiptId, expectedVersion, actor, operationKey, reason!.Trim()),
                cancellationToken),
            "OCR will be retried.");

    public Task<IActionResult> OnPostRetryIntakeAllocationAsync(
        Guid receiptId,
        long expectedVersion,
        Guid expectedAttemptId,
        string operationKey,
        string? reason,
        string? returnUrl,
        CancellationToken cancellationToken) =>
        ExecuteIntakeActionAsync(
            receiptId,
            returnUrl,
            reason,
            async actor =>
            {
                var result = await allocateIntake.RetryAsync(
                    new(receiptId, expectedVersion, expectedAttemptId, actor, operationKey, reason!.Trim()),
                    cancellationToken);
                if (result.State.Status != IntakeAllocationProjectionStatus.Succeeded)
                {
                    throw new InvalidOperationException(
                        result.State.SafeReason ?? "The case could not be created. No reference was allocated.");
                }
            },
            "Allocation was retried and the case was created.");

    private async Task<IActionResult> ExecuteIntakeActionAsync(
        Guid receiptId,
        string? returnUrl,
        string? reason,
        Func<ActionActor, Task> action,
        string confirmation)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (receiptId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            LogsError = "Give a reason for this action.";
            return ReturnFromIntakeAction(receiptId, returnUrl);
        }

        try
        {
            await action(actor);
            TempData["Confirmation"] = confirmation;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or IntakeVersionConflictException
            or IntakeOperationConflictException
            or IntakeAllocationConcurrencyException
            or IntakeAllocationOperationConflictException
            or KeyNotFoundException)
        {
            LogsError = exception is InvalidOperationException { Message: { Length: > 0 } message }
                && exception.GetType() == typeof(InvalidOperationException)
                ? message
                : "The received item changed. Reload it before trying again.";
        }

        return ReturnFromIntakeAction(receiptId, returnUrl);
    }

    private IActionResult ReturnFromIntakeAction(Guid receiptId, string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage(new { tab = IntakeTab, receipt = receiptId == Guid.Empty ? (Guid?)null : receiptId });

    /// <summary>The query for one intake-log address, carrying the current filters.</summary>
    public string IntakeUrl(int? page = null, Guid? receipt = null, IntakeLogOutcome? outcome = null, bool clearOutcome = false)
    {
        var query = new List<string> { "tab=" + IntakeTab };
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) query.Add(key + "=" + Uri.EscapeDataString(value));
        }
        Add("IntakeOutcome", clearOutcome ? null : (outcome ?? IntakeOutcome)?.ToString());
        Add("IntakeSource", IntakeSource?.ToString());
        Add("IntakePrincipal", Trim(IntakePrincipal));
        Add("IntakeFrom", IntakeFrom?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Add("IntakeTo", IntakeTo?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Add("IntakeText", Trim(IntakeText));
        var targetPage = page ?? IntakePage ?? 1;
        if (targetPage > 1) Add("IntakePage", targetPage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (receipt is { } id) Add("Receipt", id.ToString("D"));
        return "/Administration/Logs?" + string.Join('&', query);
    }

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

    /// <summary>
    /// The record an AI job row points at, or <see langword="null"/> when the
    /// row is not an AI job, its job no longer resolves, or its subject is the
    /// Unidentified queue rather than a record. An AI job's recorded aggregate
    /// is the job, so the log alone shows a bare identifier; resolving it here
    /// is what makes the Action Logs view the readable AI history the
    /// Operations board's live rows link into.
    /// </summary>
    public AiJobReference? AiJobRecordLink(ActionLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return IsAiJobReference(row) && Guid.TryParse(row.Reference, out var jobId)
            ? _aiJobReferences.TryGetValue(jobId, out var link) ? link : null
            : null;
    }

    /// <summary>One AI job row's record: the page it opens and its reference.</summary>
    public sealed record AiJobReference(string Page, Guid SubjectId, string Reference);

    private static bool IsAiJobReference(ActionLogRow row) =>
        string.Equals(row.Area, AiJobArea, StringComparison.Ordinal);

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

    // One read per distinct job on the page, not per transition row: a job
    // writes a history row for every state it passes through.
    private async Task ResolveAiJobReferencesAsync(CancellationToken cancellationToken)
    {
        foreach (var jobId in Result.Rows
                     .Where(IsAiJobReference)
                     .Select(row => Guid.TryParse(row.Reference, out var id) ? id : Guid.Empty)
                     .Where(id => id != Guid.Empty)
                     .Distinct())
        {
            var job = await aiJobs.GetAsync(jobId, cancellationToken);
            if (job?.SubjectId is { } subjectId
                && AiJobActions.RecordPage(job.SubjectKind) is { } page)
            {
                _aiJobReferences[jobId] = new(page, subjectId, job.SubjectReference);
            }
        }
    }

    private static string Query(DateTimeOffset? value) =>
        value is { } present ? Uri.EscapeDataString(present.ToString("O")) : string.Empty;

    private static string Query(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
