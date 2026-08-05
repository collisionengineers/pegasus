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
