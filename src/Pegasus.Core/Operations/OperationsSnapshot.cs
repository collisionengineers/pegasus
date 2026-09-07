using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
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
/// What the dashboard shows.
/// </summary>
/// <remarks>
/// The staged-artifact inventory is deliberately absent. It listed raw storage
/// keys, byte counts and reconciliation dispositions on the operator's home
/// screen with no link to the receipt, work item or failure behind any of them
/// — a diagnostic nobody on that screen could act on. Reconciliation itself is
/// unchanged and remains the Worker's job.
/// </remarks>
public sealed record OperationsSnapshot(
    DateTimeOffset AsOfUtc,
    IntakeQueueCounts Intake,
    int TriageCount,
    int UnidentifiedCount,
    IReadOnlyList<CaseDueWork> DueWork,
    CaseStageCounts CaseStages,
    IReadOnlyList<NeedsAttentionItem> NeedsAttention);

public interface IGetOperationsSnapshot
{
    Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The shell notifications menu's own narrow query (C08): the same
/// needs-attention rows <see cref="IGetOperationsSnapshot"/> composes, cut to
/// <see cref="GetOperationsSnapshot.MaximumAttentionRows"/> rather than the
/// full fifty, and without the dashboard counts a notifications menu has no
/// use for. <see cref="RailCountsPageFilter"/> calls it once per authenticated
/// request for every page except Work Centre, which already holds its own
/// full snapshot and slices its own top ten instead of paying for a second
/// call.
/// </summary>
public interface IGetAttentionRows
{
    Task<IReadOnlyList<NeedsAttentionItem>> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public sealed class GetOperationsSnapshot(
    IIntakeReceiptQueries intakeQueries,
    IListTriage listTriage,
    ICaseDueWorkQueries dueWorkQueries,
    IDashboardQueries dashboardQueries,
    ISearchCases searchCases,
    IUnidentifiedStore unidentifiedStore,
    GetRequestOperations requestOperations,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : IGetOperationsSnapshot, IGetAttentionRows
{
    /// <summary>
    /// The needs-attention list's bound, and the bound each of its source
    /// queries is read to. Fifty rows is more than an operator works through
    /// in one sitting; the Cases tabs carry the rest.
    /// </summary>
    public const int MaximumNeedsAttention = 50;

    /// <summary>
    /// The shell notifications menu's bound (C08): more than ten items in a
    /// dropdown is a list the operator cannot scan, so the same ordered rows
    /// are cut here rather than a second wording of the fifty-row bound.
    /// </summary>
    public const int MaximumAttentionRows = 10;

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
    private readonly GetRequestOperations requestOperations =
        requestOperations ?? throw new ArgumentNullException(nameof(requestOperations));
    private readonly IStaffAccountQueries staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        var asOfUtc = timeProvider.GetUtcNow();
        var (_, dayEndUtc, _) =
            LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);

        var intake = await intakeQueries.GetCountsAsync(cancellationToken);
        var inputs = await FetchAttentionInputsAsync(actor, asOfUtc, cancellationToken);
        var caseStages = await dashboardQueries.GetCaseStageCountsAsync(cancellationToken);

        var needsAttention = await ComposeNeedsAttentionAsync(
            asOfUtc,
            dayEndUtc,
            inputs.DueWork,
            inputs.Held,
            inputs.Unidentified,
            inputs.Triage,
            inputs.Requests,
            cancellationToken);

        return new(
            asOfUtc,
            intake,
            inputs.TriageTotalCount,
            inputs.Unidentified.Count,
            inputs.DueWork,
            caseStages,
            needsAttention);
    }

    /// <inheritdoc cref="IGetAttentionRows.ExecuteAsync"/>
    /// <remarks>
    /// The notifications menu's narrow read (C08): the same ordered rows
    /// <see cref="ExecuteAsync(ActionActor, CancellationToken)"/> composes,
    /// without the intake or dashboard counts a menu has no use for.
    /// </remarks>
    async Task<IReadOnlyList<NeedsAttentionItem>> IGetAttentionRows.ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        var asOfUtc = timeProvider.GetUtcNow();
        var (_, dayEndUtc, _) = LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);
        var inputs = await FetchAttentionInputsAsync(actor, asOfUtc, cancellationToken);
        var rows = await ComposeNeedsAttentionAsync(
            asOfUtc,
            dayEndUtc,
            inputs.DueWork,
            inputs.Held,
            inputs.Unidentified,
            inputs.Triage,
            inputs.Requests,
            cancellationToken);

        return rows.Take(MaximumAttentionRows).ToArray();
    }

    /// <summary>
    /// The five needs-attention sources, fetched once and shared by the full
    /// snapshot and the notifications menu's narrower read — one query each,
    /// never a second copy of the fetch behind a second wording.
    /// </summary>
    private async Task<AttentionInputs> FetchAttentionInputsAsync(
        ActionActor actor,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        // The Triage kind is work without a finding, so both no-finding states
        // are queried directly. One unfiltered page would not do: the list is
        // newest-first across every state, so fifty settled records would
        // silently bury an open one off page one.
        var openTriagePage = await listTriage.ExecuteAsync(
            new(actor, TriageState.Open, Page: 1, PageSize: MaximumNeedsAttention),
            cancellationToken);
        var awaitingTriagePage = await listTriage.ExecuteAsync(
            new(actor, TriageState.AwaitingInformation, Page: 1, PageSize: MaximumNeedsAttention),
            cancellationToken);
        var triageWithoutFinding = openTriagePage.Items.Concat(awaitingTriagePage.Items).ToArray();
        var dueWork = await dueWorkQueries.GetDueAsync(
            asOfUtc,
            MaximumNeedsAttention,
            cancellationToken);
        var held = await searchCases.ExecuteAsync(
            new(actor, new(State: CaseLifecycleState.Held), Page: 1, PageSize: MaximumNeedsAttention),
            cancellationToken);
        var unidentified = await unidentifiedStore.ListQueueAsync(null, cancellationToken);
        var requests = await requestOperations.ExecuteAsync(actor, cancellationToken);

        return new AttentionInputs(
            dueWork,
            held.Items,
            unidentified,
            triageWithoutFinding,
            openTriagePage.TotalCount + awaitingTriagePage.TotalCount,
            requests.Items);
    }

    private readonly record struct AttentionInputs(
        IReadOnlyList<CaseDueWork> DueWork,
        IReadOnlyList<CaseSearchItem> Held,
        IReadOnlyList<UnidentifiedQueueRow> Unidentified,
        IReadOnlyList<TriageSummary> Triage,
        int TriageTotalCount,
        IReadOnlyList<RequestOperationProjection> Requests);

    /// <summary>
    /// The five needs-attention kinds, each read from the query that already
    /// backs its Cases tab or Operations table, ordered by priority, then the
    /// earliest due instant (work with no due instant last), then reference,
    /// and cut at <see cref="MaximumNeedsAttention"/>.
    /// </summary>
    private async Task<IReadOnlyList<NeedsAttentionItem>> ComposeNeedsAttentionAsync(
        DateTimeOffset asOfUtc,
        DateTimeOffset dayEndUtc,
        IReadOnlyList<CaseDueWork> dueWork,
        IReadOnlyList<CaseSearchItem> heldCases,
        IReadOnlyList<UnidentifiedQueueRow> unidentified,
        IReadOnlyList<TriageSummary> triage,
        IEnumerable<RequestOperationProjection> requests,
        CancellationToken cancellationToken)
    {
        // Both no-finding states arrive pre-filtered from their own queries.
        var staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            heldCases.Select(item => item.EngineerId ?? Guid.Empty)
                .Concat(triage.Select(record => record.AssigneeId ?? Guid.Empty)),
            cancellationToken);

        var items = new List<NeedsAttentionItem>();
        foreach (var work in dueWork)
        {
            var due = work.NextChaseAtUtc
                ?? (work.DueBy is { } dueBy
                    ? new DateTimeOffset(dueBy.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                    : null);
            // The row-meta already names the reference, so the title is the
            // recorded blocker itself and the notice carries the chase state;
            // repeating either would render one fact twice on the same row.
            items.Add(new(
                NeedsAttentionKind.Case,
                work.CaseId,
                work.Reference,
                work.MissingMaterialReason,
                Detail: null,
                work.State.ToString(),
                DuePriority(due, asOfUtc, dayEndUtc),
                Owner: null,
                due,
                work.MostRecentOutcome,
                Source: null,
                Attempts: null));
        }

        foreach (var held in heldCases)
        {
            items.Add(new(
                NeedsAttentionKind.HeldDecision,
                held.CaseId,
                held.Reference,
                held.Claimant ?? held.Reference,
                held.Principal,
                nameof(CaseLifecycleState.Held),
                DuePriority(held.NextChaseAtUtc, asOfUtc, dayEndUtc),
                OwnerName(held.EngineerId, staffNames),
                held.NextChaseAtUtc,
                LastOutcome: null,
                held.Origin,
                Attempts: null));
        }

        foreach (var row in unidentified)
        {
            items.Add(new(
                NeedsAttentionKind.Mail,
                row.Id,
                row.Reference,
                row.FileName ?? row.EmailSubject ?? row.Reference,
                row.EmailSender,
                row.ReasonCode.ToString(),
                NeedsAttentionPriority.Normal,
                Owner: null,
                Due: null,
                LastOutcome: null,
                row.MediaKind.ToString(),
                Attempts: null));
        }

        foreach (var record in triage)
        {
            items.Add(new(
                NeedsAttentionKind.Triage,
                record.Id,
                record.NormalizedVehicleRegistration,
                record.NormalizedVehicleRegistration,
                Detail: null,
                record.State.ToString(),
                NeedsAttentionPriority.Normal,
                OwnerName(record.AssigneeId, staffNames),
                Due: null,
                LastOutcome: null,
                Source: null,
                Attempts: null));
        }

        foreach (var request in requests)
        {
            if (request.Kind != RequestOperationKind.ExternalWork || !request.CanRetry)
            {
                continue;
            }

            items.Add(new(
                NeedsAttentionKind.ExternalWork,
                request.CaseId,
                request.CaseReference,
                request.ExternalKind ?? request.CaseReference,
                Detail: null,
                request.FailureReason ?? request.FailureCode ?? request.State.ToString(),
                NeedsAttentionPriority.High,
                Owner: null,
                Due: null,
                request.FailureCode,
                request.PrincipalCode,
                request.AttemptCount));
        }

        return items
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Due ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Reference, StringComparer.Ordinal)
            .Take(MaximumNeedsAttention)
            .ToArray();
    }

    private static NeedsAttentionPriority DuePriority(
        DateTimeOffset? due,
        DateTimeOffset asOfUtc,
        DateTimeOffset dayEndUtc) => due switch
    {
        { } instant when instant <= asOfUtc => NeedsAttentionPriority.Overdue,
        { } instant when instant < dayEndUtc => NeedsAttentionPriority.Today,
        _ => NeedsAttentionPriority.Normal
    };

    private static string? OwnerName(Guid? staffId, IReadOnlyDictionary<Guid, string> staffNames) =>
        staffId is { } id
            ? ActorDisplayNames.Resolve(ActorKind.Staff, id.ToString(), staffNames)
            : null;
}
