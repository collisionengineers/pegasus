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
    IReadOnlyList<CaseDueWork> DueWork,
    CaseStageCounts CaseStages,
    CaseActivityCounts CaseActivity,
    MailActivityCounts MailActivity,
    IReadOnlyList<NeedsAttentionItem> NeedsAttention);

public interface IGetOperationsSnapshot
{
    Task<OperationsSnapshot> ExecuteAsync(
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
    TimeProvider timeProvider) : IGetOperationsSnapshot
{
    /// <summary>
    /// The needs-attention list's bound, and the bound each of its source
    /// queries is read to. Fifty rows is more than an operator works through
    /// in one sitting; the Cases tabs carry the rest.
    /// </summary>
    public const int MaximumNeedsAttention = 50;

    /// <summary>
    /// The zone the day and week boundaries are taken against.
    /// </summary>
    /// <remarks>
    /// "Today" on this screen means the office's today. Counting from a UTC
    /// midnight would move the boundary by an hour for half the year and
    /// silently reassign work between days.
    /// </remarks>
    private const string OfficeTimeZoneId = "Europe/London";

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
        var (dayStartUtc, weekStartUtc) = OfficeBoundaries(asOfUtc);

        var intake = await intakeQueries.GetCountsAsync(cancellationToken);
        var triage = await listTriage.ExecuteAsync(
            new(actor, State: null, Page: 1, PageSize: MaximumNeedsAttention),
            cancellationToken);
        var dueWork = await dueWorkQueries.GetDueAsync(
            asOfUtc,
            MaximumNeedsAttention,
            cancellationToken);
        var caseStages = await dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var caseActivity = await dashboardQueries.GetCaseActivityCountsAsync(
            dayStartUtc,
            weekStartUtc,
            cancellationToken);
        var mailActivity = await dashboardQueries.GetMailActivityCountsAsync(
            dayStartUtc,
            cancellationToken);

        var held = await searchCases.ExecuteAsync(
            new(actor, new(State: CaseLifecycleState.Held), Page: 1, PageSize: MaximumNeedsAttention),
            cancellationToken);
        var unidentified = await unidentifiedStore.ListQueueAsync(null, cancellationToken);
        var requests = await requestOperations.ExecuteAsync(actor, cancellationToken);
        var needsAttention = await ComposeNeedsAttentionAsync(
            asOfUtc,
            dayStartUtc,
            dueWork,
            held.Items,
            unidentified,
            triage.Items,
            requests.Items,
            cancellationToken);

        return new(
            asOfUtc,
            intake,
            triage.TotalCount,
            dueWork,
            caseStages,
            caseActivity,
            mailActivity,
            needsAttention);
    }

    /// <summary>
    /// The five needs-attention kinds, each read from the query that already
    /// backs its Cases tab or Operations table, ordered by priority, then the
    /// earliest due instant (work with no due instant last), then reference,
    /// and cut at <see cref="MaximumNeedsAttention"/>.
    /// </summary>
    private async Task<IReadOnlyList<NeedsAttentionItem>> ComposeNeedsAttentionAsync(
        DateTimeOffset asOfUtc,
        DateTimeOffset dayStartUtc,
        IReadOnlyList<CaseDueWork> dueWork,
        IReadOnlyList<CaseSearchItem> heldCases,
        IReadOnlyList<UnidentifiedQueueRow> unidentified,
        IReadOnlyList<TriageSummary> triage,
        IEnumerable<RequestOperationProjection> requests,
        CancellationToken cancellationToken)
    {
        var dayEndUtc = dayStartUtc.AddDays(1);
        var openTriage = triage
            .Where(record => record.State is TriageState.Open or TriageState.AwaitingInformation)
            .ToArray();
        var staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            heldCases.Select(item => item.EngineerId ?? Guid.Empty)
                .Concat(openTriage.Select(record => record.AssigneeId ?? Guid.Empty)),
            cancellationToken);

        var items = new List<NeedsAttentionItem>();
        foreach (var work in dueWork)
        {
            var due = work.NextChaseAtUtc
                ?? (work.DueBy is { } dueBy
                    ? new DateTimeOffset(dueBy.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                    : null);
            items.Add(new(
                NeedsAttentionKind.Case,
                work.CaseId,
                work.Reference,
                work.Reference,
                work.MissingMaterialReason,
                work.MissingMaterialReason,
                DuePriority(due, asOfUtc, dayEndUtc),
                Owner: null,
                due,
                work.MostRecentOutcome,
                work.MostRecentChannel));
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
                held.Origin));
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
                row.MediaKind.ToString()));
        }

        foreach (var record in openTriage)
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
                Source: null));
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
                request.AttemptCount is { } attempts ? $"{attempts} attempts" : null,
                request.FailureReason ?? request.FailureCode ?? request.State.ToString(),
                NeedsAttentionPriority.High,
                Owner: null,
                Due: null,
                request.FailureCode,
                request.PrincipalCode));
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

    /// <summary>
    /// The start of the office's today and of its week, expressed in UTC.
    /// </summary>
    /// <remarks>
    /// The week starts on Monday, which is the week the office works to. Where
    /// the platform carries no IANA database the zone cannot be resolved and
    /// the boundaries fall back to UTC — an hour out for part of the year, and
    /// the only alternative to failing the whole dashboard over a clock.
    /// </remarks>
    private static (DateTimeOffset DayStartUtc, DateTimeOffset WeekStartUtc) OfficeBoundaries(
        DateTimeOffset asOfUtc)
    {
        TimeZoneInfo office;
        try
        {
            office = TimeZoneInfo.FindSystemTimeZoneById(OfficeTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            office = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            office = TimeZoneInfo.Utc;
        }

        var local = TimeZoneInfo.ConvertTime(asOfUtc, office);
        var dayStartLocal = new DateTimeOffset(local.Date, local.Offset);
        var daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
        var weekStartLocal = dayStartLocal.AddDays(-daysSinceMonday);
        return (dayStartLocal.ToUniversalTime(), weekStartLocal.ToUniversalTime());
    }
}
