using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;

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
    MailActivityCounts MailActivity);

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
    TimeProvider timeProvider) : IGetOperationsSnapshot
{
    private const int MaximumDueWork = 20;

    private readonly IIntakeReceiptQueries intakeQueries =
        intakeQueries ?? throw new ArgumentNullException(nameof(intakeQueries));
    private readonly IListTriage listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly ICaseDueWorkQueries dueWorkQueries =
        dueWorkQueries ?? throw new ArgumentNullException(nameof(dueWorkQueries));
    private readonly IDashboardQueries dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        var asOfUtc = timeProvider.GetUtcNow();
        var (dayStartUtc, weekStartUtc) =
            LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);

        var intake = await intakeQueries.GetCountsAsync(cancellationToken);
        var triage = await listTriage.ExecuteAsync(
            new(actor, State: null, Page: 1, PageSize: 1),
            cancellationToken);
        var dueWork = await dueWorkQueries.GetDueAsync(
            asOfUtc,
            MaximumDueWork,
            cancellationToken);
        var caseStages = await dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var caseActivity = await dashboardQueries.GetCaseActivityCountsAsync(
            dayStartUtc,
            weekStartUtc,
            cancellationToken);
        var mailActivity = await dashboardQueries.GetMailActivityCountsAsync(
            dayStartUtc,
            cancellationToken);

        return new(
            asOfUtc,
            intake,
            triage.TotalCount,
            dueWork,
            caseStages,
            caseActivity,
            mailActivity);
    }
}
