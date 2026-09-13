using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Notifications;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Operations;

/// <summary>
/// The staged-artifact reconciliation inventory.
/// </summary>
/// <remarks>
/// No longer part of the dashboard: it printed storage keys, byte counts and
/// dispositions at an operator who had no link to the receipt or work item
/// behind any of them, and no action to take. Retained because the shape is a
/// genuine reconciliation summary; if it earns a screen again it belongs on a
/// system-health surface.
/// </remarks>
public sealed record StagedArtifactOperationsSnapshot(
    IReadOnlyList<StagedArtifactInventoryItem> Items)
{
    public int Pending => Count(StagedArtifactDisposition.Pending);

    public int Completed => Count(StagedArtifactDisposition.Completed);

    public int Failed => Count(StagedArtifactDisposition.Failed);

    public int Unmatched => Count(StagedArtifactDisposition.Unmatched);

    public int Orphans => Count(StagedArtifactDisposition.Orphan);

    private int Count(StagedArtifactDisposition disposition) =>
        Items.Count(item => item.Disposition == disposition);
}

/// <summary>
/// Office or Mine (Work Centre P2): Office is everything; Mine is the rows whose
/// owner is the signed-in person plus the unowned rows of the kinds they can take.
/// </summary>
public enum NeedsAttentionScope
{
    Office,
    Mine
}

/// <summary>
/// One read of the Needs attention list: the scope, an optional kind filter
/// (P3) and the page (D4, fifty rows). Counts are of the whole filtered list,
/// never of the page.
/// </summary>
public sealed record NeedsAttentionQuery(
    ActionActor Actor,
    NeedsAttentionScope Scope = NeedsAttentionScope.Office,
    int Page = 1,
    IReadOnlyCollection<NeedsAttentionKind>? Kinds = null);

public sealed record NeedsAttentionPage(
    IReadOnlyList<NeedsAttentionItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<NeedsAttentionKind, int> KindCounts,
    int OverdueCount,
    int TodayCount,
    int LaterCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>The metric strip of four (Work Centre D7): Not ready, Review, Held, Unidentified.</summary>
public sealed record WorkCentreMetrics(int NotReady, int Review, int Held, int Unidentified);

/// <summary>
/// What the Work Centre shows.
/// </summary>
/// <remarks>
/// The staged-artifact inventory is deliberately absent. It listed raw storage
/// keys, byte counts and reconciliation dispositions on the operator's home
/// screen with no link to the receipt, work item or failure behind any of them
/// — a diagnostic nobody on that screen could act on. Reconciliation itself is
/// unchanged and remains the Worker's job. Failed external work is absent too
/// (D1): it lives on Operations, whose rail badge counts it.
/// </remarks>
public sealed record OperationsSnapshot(
    DateTimeOffset AsOfUtc,
    IntakeQueueCounts Intake,
    int TriageCount,
    int UnidentifiedCount,
    IReadOnlyList<CaseDueWork> DueWork,
    CaseStageCounts CaseStages,
    IReadOnlyList<NeedsAttentionItem> NeedsAttention)
{
    /// <summary>The page the <see cref="NeedsAttention"/> rows came from, with the whole-list counts.</summary>
    public NeedsAttentionPage Attention { get; init; } = new(NeedsAttention, 1, GetOperationsSnapshot.PageSize, NeedsAttention.Count, new Dictionary<NeedsAttentionKind, int>(), 0, 0, 0);

    public NeedsAttentionScope Scope { get; init; } = NeedsAttentionScope.Office;

    public WorkCentreMetrics Metrics { get; init; } = new(CaseStages.NotReady, CaseStages.Review, CaseStages.Held, UnidentifiedCount);
}

public interface IGetOperationsSnapshot
{
    /// <summary>The Office view, page one.</summary>
    Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<OperationsSnapshot> ExecuteAsync(
        NeedsAttentionQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The first rows of the Office list, cut to
/// <see cref="GetOperationsSnapshot.MaximumAttentionRows"/>. The shell bell no
/// longer mirrors this (Work Centre D10, personal notifications); it remains for
/// the Today pane and any caller that wants the head of the list without the
/// counts.
/// </summary>
public interface IGetAttentionRows
{
    Task<IReadOnlyList<NeedsAttentionItem>> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The Operations rail badge (Work Centre D1): how many failed external
/// work items can be retried. Nothing else counts towards it.
/// </summary>
public interface IGetOperationsBadge
{
    Task<int> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken = default);
}

public sealed class GetOperationsBadge(GetRequestOperations requestOperations) : IGetOperationsBadge
{
    private readonly GetRequestOperations _requestOperations =
        requestOperations ?? throw new ArgumentNullException(nameof(requestOperations));

    public async Task<int> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var requests = await _requestOperations.ExecuteAsync(actor, cancellationToken);
        return requests.Items.Count(item => item.Kind == RequestOperationKind.ExternalWork && item.CanRetry);
    }
}

/// <summary>
/// The Work Centre's rules that need no store: which rows are "mine", what a row
/// is due at, and how the list orders (Work Centre D2, D3, P2).
/// </summary>
public static class NeedsAttentionPolicy
{
    /// <summary>An Engineer may take an unassigned Case or Triage straight from the list (P8).</summary>
    public static bool CanTake(NeedsAttentionKind kind, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Kind == ActorKind.Staff
            && actor.IsInRole(StaffRole.Engineer)
            && kind is NeedsAttentionKind.UnassignedEngineer or NeedsAttentionKind.Triage;
    }

    public static bool IsMine(NeedsAttentionItem item, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(item);
        var me = StaffNotificationPolicy.StaffId(actor);
        return item.OwnerStaffId is { } owner
            ? owner == me
            : CanTake(item.Kind, actor);
    }

    public static NeedsAttentionPriority Priority(
        DateTimeOffset? due,
        DateTimeOffset asOfUtc,
        DateTimeOffset dayEndUtc) => due switch
    {
        { } instant when instant <= asOfUtc => NeedsAttentionPriority.Overdue,
        // Due at the midnight that ends today is due today: a target of 0 lands
        // exactly there.
        { } instant when instant <= dayEndUtc => NeedsAttentionPriority.Today,
        _ => NeedsAttentionPriority.Normal
    };

    /// <summary>A held Case is due on its review date when given, else held + the Held decision target.</summary>
    public static DateTimeOffset? HeldDueAt(DateOnly? reviewOn, DateTimeOffset? heldAtUtc, int heldTargetDays) =>
        reviewOn is { } date
            ? WorkTargets.EndOfDay(date)
            : heldAtUtc is { } held
                ? WorkTargets.DueAt(held, heldTargetDays)
                : null;

    /// <summary>Due instant first, undated last, then received, then reference (D2).</summary>
    public static IOrderedEnumerable<NeedsAttentionItem> Order(IEnumerable<NeedsAttentionItem> items) =>
        items
            .OrderBy(item => item.Due ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Received ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Reference, StringComparer.Ordinal);
}

public sealed class GetOperationsSnapshot(
    IIntakeReceiptQueries intakeQueries,
    IListTriage listTriage,
    ICaseDueWorkQueries dueWorkQueries,
    IDashboardQueries dashboardQueries,
    ISearchCases searchCases,
    IUnidentifiedStore unidentifiedStore,
    IStaffAccountQueries staffAccounts,
    ICaseWorkflowConfiguration workflowConfiguration,
    TimeProvider timeProvider,
    IAiDraftQueries? aiDrafts = null,
    ICaseWorkflowQueries? workflows = null) : IGetOperationsSnapshot, IGetAttentionRows
{
    /// <summary>The list's page size (D4): paged, never cut.</summary>
    public const int PageSize = 50;

    /// <summary>Kept for callers that still name the old bound; the list is paged at <see cref="PageSize"/> now.</summary>
    public const int MaximumNeedsAttention = PageSize;

    /// <summary>
    /// The Today pane's bound: more than ten items in a pane is a list the
    /// operator cannot scan, so the same ordered rows are cut here.
    /// </summary>
    public const int MaximumAttentionRows = 10;

    /// <summary>
    /// How many rows of one kind the composition reads before it stops counting.
    /// Well past any office's open work; a source read to this bound is still
    /// exact for the counts and the paging.
    /// </summary>
    public const int MaximumSourceRows = 500;

    private const int SourcePageSize = 100;

    private readonly IIntakeReceiptQueries intakeQueries =
        intakeQueries ?? throw new ArgumentNullException(nameof(intakeQueries));
    private readonly IListTriage listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly ICaseDueWorkQueries dueWorkQueries =
        dueWorkQueries ?? throw new ArgumentNullException(nameof(dueWorkQueries));
    private readonly IDashboardQueries dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly ISearchCases searchCases =
        searchCases ?? throw new ArgumentNullException(nameof(searchCases));
    private readonly IUnidentifiedStore unidentifiedStore =
        unidentifiedStore ?? throw new ArgumentNullException(nameof(unidentifiedStore));
    private readonly IStaffAccountQueries staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));
    private readonly ICaseWorkflowConfiguration workflowConfiguration =
        workflowConfiguration ?? throw new ArgumentNullException(nameof(workflowConfiguration));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(new NeedsAttentionQuery(actor), cancellationToken);

    public async Task<OperationsSnapshot> ExecuteAsync(
        NeedsAttentionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Actor);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The page must be positive.");
        }

        var asOfUtc = timeProvider.GetUtcNow();
        var intake = await intakeQueries.GetCountsAsync(cancellationToken);
        var inputs = await FetchAttentionInputsAsync(query.Actor, asOfUtc, cancellationToken);
        var caseStages = await dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var all = await ComposeNeedsAttentionAsync(asOfUtc, inputs, cancellationToken);
        var page = Page(all, query, asOfUtc);

        return new(
            asOfUtc,
            intake,
            inputs.TriageTotalCount,
            inputs.Unidentified.Count,
            inputs.DueWork,
            caseStages,
            page.Items)
        {
            Attention = page,
            Scope = query.Scope,
            Metrics = new(caseStages.NotReady, caseStages.Review, caseStages.Held, inputs.Unidentified.Count)
        };
    }

    /// <inheritdoc cref="IGetAttentionRows.ExecuteAsync"/>
    async Task<IReadOnlyList<NeedsAttentionItem>> IGetAttentionRows.ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        var asOfUtc = timeProvider.GetUtcNow();
        var inputs = await FetchAttentionInputsAsync(actor, asOfUtc, cancellationToken);
        var rows = await ComposeNeedsAttentionAsync(asOfUtc, inputs, cancellationToken);
        return rows.Take(MaximumAttentionRows).ToArray();
    }

    private static NeedsAttentionPage Page(
        IReadOnlyList<NeedsAttentionItem> all,
        NeedsAttentionQuery query,
        DateTimeOffset asOfUtc)
    {
        var (_, dayEndUtc, _) = LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);
        IEnumerable<NeedsAttentionItem> filtered = all;
        if (query.Scope == NeedsAttentionScope.Mine)
        {
            filtered = filtered.Where(item => NeedsAttentionPolicy.IsMine(item, query.Actor));
        }

        if (query.Kinds is { Count: > 0 } kinds)
        {
            filtered = filtered.Where(item => kinds.Contains(item.Kind));
        }

        var list = filtered.ToArray();
        var kindCounts = Enum.GetValues<NeedsAttentionKind>()
            .ToDictionary(kind => kind, kind => list.Count(item => item.Kind == kind));
        var overdue = list.Count(item => NeedsAttentionPolicy.Priority(item.Due, asOfUtc, dayEndUtc) == NeedsAttentionPriority.Overdue);
        var today = list.Count(item => NeedsAttentionPolicy.Priority(item.Due, asOfUtc, dayEndUtc) == NeedsAttentionPriority.Today);
        return new(
            list.Skip((query.Page - 1) * PageSize).Take(PageSize).ToArray(),
            query.Page,
            PageSize,
            list.Length,
            kindCounts,
            overdue,
            today,
            list.Length - overdue - today);
    }

    /// <summary>
    /// The needs-attention sources, fetched once. Each is read in full (to
    /// <see cref="MaximumSourceRows"/>) because the counts are of the whole list
    /// and the page is cut afterwards (D4).
    /// </summary>
    private async Task<AttentionInputs> FetchAttentionInputsAsync(
        ActionActor actor,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        // The Triage kind is work without a finding, so both no-finding states
        // are queried directly.
        var (openTriage, openTriageCount) = await ReadTriageAsync(actor, TriageState.Open, cancellationToken);
        var (awaitingTriage, awaitingTriageCount) = await ReadTriageAsync(actor, TriageState.AwaitingInformation, cancellationToken);
        var dueWork = await dueWorkQueries.GetDueAsync(asOfUtc, MaximumSourceRows, cancellationToken);
        var held = await ReadCasesAsync(actor, CaseLifecycleState.Held, cancellationToken);
        var review = await ReadCasesAsync(actor, CaseLifecycleState.Review, cancellationToken);
        var configuration = await workflowConfiguration.GetCurrentAsync(cancellationToken);
        var unidentified = await unidentifiedStore.ListQueueAsync(null, cancellationToken);
        var drafts = aiDrafts is null
            ? []
            : await aiDrafts.ListOpenAsync(cancellationToken);

        var reviewPartitions = review
            .Select(item => new
            {
                Item = item,
                IsReadyForEngineerAssignment = item.EngineerId is null
                    && CaseCompletenessPolicy.Evaluate(
                        new(item.InstructionComplete ?? false, item.ImagesComplete ?? false),
                        configuration).SatisfiesPolicy
            })
            .ToArray();

        return new AttentionInputs(
            configuration,
            dueWork,
            held,
            reviewPartitions.Where(partition => !partition.IsReadyForEngineerAssignment).Select(partition => partition.Item).ToArray(),
            reviewPartitions.Where(partition => partition.IsReadyForEngineerAssignment).Select(partition => partition.Item).ToArray(),
            unidentified,
            [.. openTriage, .. awaitingTriage],
            openTriageCount + awaitingTriageCount,
            drafts);
    }

    private async Task<(IReadOnlyList<TriageSummary> Items, int TotalCount)> ReadTriageAsync(
        ActionActor actor,
        TriageState state,
        CancellationToken cancellationToken)
    {
        List<TriageSummary> items = [];
        var total = 0;
        for (var page = 1; items.Count < MaximumSourceRows; page++)
        {
            var result = await listTriage.ExecuteAsync(new(actor, state, page, SourcePageSize), cancellationToken);
            total = result.TotalCount;
            items.AddRange(result.Items);
            if (result.Items.Count < SourcePageSize || page >= result.TotalPages)
            {
                break;
            }
        }

        return (items, total);
    }

    private async Task<IReadOnlyList<CaseSearchItem>> ReadCasesAsync(
        ActionActor actor,
        CaseLifecycleState state,
        CancellationToken cancellationToken)
    {
        List<CaseSearchItem> items = [];
        for (var page = 1; items.Count < MaximumSourceRows; page++)
        {
            var result = await searchCases.ExecuteAsync(
                new(actor, new(State: state), page, SourcePageSize),
                cancellationToken);
            items.AddRange(result.Items);
            if (!result.HasNextPage)
            {
                break;
            }
        }

        return items;
    }

    private readonly record struct AttentionInputs(
        CaseWorkflowConfiguration Configuration,
        IReadOnlyList<CaseDueWork> DueWork,
        IReadOnlyList<CaseSearchItem> Held,
        IReadOnlyList<CaseSearchItem> Review,
        IReadOnlyList<CaseSearchItem> UnassignedEngineer,
        IReadOnlyList<UnidentifiedQueueRow> Unidentified,
        IReadOnlyList<TriageSummary> Triage,
        int TriageTotalCount,
        IReadOnlyList<AiDraft> Drafts);

    /// <summary>
    /// Every needs-attention row, each read from the query that already backs
    /// its Cases tab, with its due instant from the workflow targets (D3),
    /// ordered by due instant, undated last, then received, then reference (D2).
    /// </summary>
    private async Task<IReadOnlyList<NeedsAttentionItem>> ComposeNeedsAttentionAsync(
        DateTimeOffset asOfUtc,
        AttentionInputs inputs,
        CancellationToken cancellationToken)
    {
        var (_, dayEndUtc, _) = LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);
        var targets = inputs.Configuration;
        var draftOwners = await DraftOwnersAsync(inputs.Drafts, cancellationToken);
        var staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            inputs.Held.Select(item => item.EngineerId ?? Guid.Empty)
                .Concat(inputs.Triage.Select(record => record.AssigneeId ?? Guid.Empty))
                .Concat(draftOwners.Values.Select(id => id ?? Guid.Empty)),
            cancellationToken);

        var items = new List<NeedsAttentionItem>();
        foreach (var work in inputs.DueWork)
        {
            var due = work.NextChaseAtUtc
                ?? (work.DueBy is { } dueBy ? WorkTargets.EndOfDay(dueBy) : null);
            items.Add(new(
                NeedsAttentionKind.CaseChase,
                work.CaseId,
                work.Reference,
                work.MissingMaterialReason,
                Detail: null,
                work.State.ToString(),
                NeedsAttentionPolicy.Priority(due, asOfUtc, dayEndUtc),
                Owner: null,
                due,
                work.MostRecentOutcome,
                Source: null,
                Attempts: null)
            {
                Route = StaffNotificationPolicy.CaseRoute(work.CaseId)
            });
        }

        foreach (var held in inputs.Held)
        {
            var due = NeedsAttentionPolicy.HeldDueAt(held.HoldReviewOn, held.HeldAtUtc ?? held.StateEnteredAtUtc, targets.HeldTargetDays);
            items.Add(new(
                NeedsAttentionKind.HeldDecision,
                held.CaseId,
                held.Reference,
                held.Claimant ?? held.Reference,
                held.Principal,
                nameof(CaseLifecycleState.Held),
                NeedsAttentionPolicy.Priority(due, asOfUtc, dayEndUtc),
                OwnerName(held.EngineerId, staffNames),
                due,
                LastOutcome: null,
                held.Origin,
                Attempts: null,
                Received: held.ReceivedAtUtc)
            {
                OwnerStaffId = held.EngineerId,
                Route = StaffNotificationPolicy.CaseRoute(held.CaseId)
            });
        }

        foreach (var review in inputs.Review)
        {
            var due = WorkTargets.DueAt(review.StateEnteredAtUtc ?? review.CreatedAtUtc, targets.ReviewTargetDays);
            items.Add(new(
                NeedsAttentionKind.ReviewCase,
                review.CaseId,
                review.Reference,
                VehicleLabel(review),
                review.Principal,
                nameof(CaseLifecycleState.Review),
                NeedsAttentionPolicy.Priority(due, asOfUtc, dayEndUtc),
                OwnerName(review.EngineerId, staffNames),
                due,
                LastOutcome: null,
                review.Origin,
                Attempts: null,
                Received: review.ReceivedAtUtc)
            {
                OwnerStaffId = review.EngineerId,
                Route = StaffNotificationPolicy.CaseRoute(review.CaseId, "review")
            });
        }

        foreach (var unassigned in inputs.UnassignedEngineer)
        {
            var due = WorkTargets.DueAt(unassigned.StateEnteredAtUtc ?? unassigned.CreatedAtUtc, targets.ReviewTargetDays);
            items.Add(new(
                NeedsAttentionKind.UnassignedEngineer,
                unassigned.CaseId,
                unassigned.Reference,
                VehicleLabel(unassigned),
                unassigned.Principal,
                "Engineer assignment required",
                NeedsAttentionPolicy.Priority(due, asOfUtc, dayEndUtc),
                Owner: "Unassigned",
                due,
                LastOutcome: null,
                unassigned.Origin,
                Attempts: null,
                Received: unassigned.ReceivedAtUtc)
            {
                Route = StaffNotificationPolicy.CaseRoute(unassigned.CaseId, "assign")
            });
        }

        foreach (var row in inputs.Unidentified)
        {
            var due = WorkTargets.DueAt(row.ReceivedAtUtc, targets.UnidentifiedTargetDays);
            items.Add(new(
                NeedsAttentionKind.Unidentified,
                row.Id,
                row.Reference,
                row.FileName ?? row.EmailSubject ?? row.Reference,
                row.EmailSender,
                row.ReasonCode.ToString(),
                NeedsAttentionPolicy.Priority(due, asOfUtc, dayEndUtc),
                Owner: null,
                due,
                LastOutcome: null,
                row.MediaKind.ToString(),
                Attempts: null,
                Received: row.ReceivedAtUtc)
            {
                Route = StaffNotificationPolicy.UnidentifiedRoute(row.Id)
            });
        }

        foreach (var record in inputs.Triage)
        {
            var due = WorkTargets.DueAt(record.CreatedAtUtc, targets.TriageTargetDays);
            items.Add(new(
                NeedsAttentionKind.Triage,
                record.Id,
                record.Reference ?? record.NormalizedVehicleRegistration,
                record.NormalizedVehicleRegistration,
                Detail: null,
                record.State.ToString(),
                NeedsAttentionPolicy.Priority(due, asOfUtc, dayEndUtc),
                OwnerName(record.AssigneeId, staffNames),
                due,
                LastOutcome: null,
                Source: null,
                Attempts: null,
                Received: record.CreatedAtUtc)
            {
                OwnerStaffId = record.AssigneeId,
                Route = $"/Triage/{record.Id:D}"
            });
        }

        foreach (var draft in inputs.Drafts)
        {
            var owner = draftOwners.GetValueOrDefault(draft.Job.JobId);
            items.Add(new(
                NeedsAttentionKind.AiDraft,
                draft.Job.JobId,
                draft.Job.SubjectReference,
                draft.Job.Kind.ToString(),
                draft.Job.Instruction,
                nameof(AiJobState.DraftReady),
                NeedsAttentionPolicy.Priority(draft.DueAtUtc, asOfUtc, dayEndUtc),
                OwnerName(owner, staffNames),
                draft.DueAtUtc,
                LastOutcome: null,
                Source: draft.Action.ToString(),
                Attempts: null,
                Received: draft.DraftWrittenAtUtc)
            {
                OwnerStaffId = owner,
                Route = draft.Route
            });
        }

        return NeedsAttentionPolicy.Order(
                items.GroupBy(item => (item.Kind, item.Id)).Select(group => group.First()))
            .ToArray();
    }

    /// <summary>The Case's engineer for each draft on a Case, so Mine can find it (P2).</summary>
    private async Task<Dictionary<Guid, Guid?>> DraftOwnersAsync(
        IReadOnlyList<AiDraft> drafts,
        CancellationToken cancellationToken)
    {
        var owners = new Dictionary<Guid, Guid?>();
        if (workflows is null)
        {
            return owners;
        }

        foreach (var draft in drafts)
        {
            if (draft.Job.SubjectKind == AiJobSubjectKind.Case && draft.Job.SubjectId is { } caseId)
            {
                owners[draft.Job.JobId] = (await workflows.GetAsync(caseId, cancellationToken))?.AssignedEngineerId;
            }
        }

        return owners;
    }

    private static string? OwnerName(Guid? staffId, IReadOnlyDictionary<Guid, string> staffNames) =>
        staffId is { } id
            ? ActorDisplayNames.Resolve(ActorKind.Staff, id.ToString(), staffNames)
            : null;

    private static string VehicleLabel(CaseSearchItem item) => string.Join(
        " ",
        new[] { item.VehicleMake, item.VehicleModel, item.Registration }
            .Where(value => !string.IsNullOrWhiteSpace(value))) switch
    {
        { Length: > 0 } vehicle => vehicle,
        _ => item.Claimant ?? item.Reference
    };
}
