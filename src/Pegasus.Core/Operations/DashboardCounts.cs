namespace Pegasus.Core.Operations;

/// <summary>
/// How many cases are sitting in each of the three stages an operator can act
/// on from the dashboard.
/// </summary>
/// <remarks>
/// These three counts did not exist before. The dashboard rendered the literal
/// string "Unavailable" for Not ready and Held, and backed the Review tile with
/// an intake-receipt count — a different entity entirely, and one that was
/// cumulative for all time.
///
/// <see cref="NotReady"/> spans both Not ready case origins (INTK-013): a
/// formal Case in CaseWorkflows (instruction-initiated) and an unmerged Image
/// Intake still awaiting instruction (image-initiated), matching the rows the
/// Queues page's Not ready tab lists for both origins combined.
/// </remarks>
/// <param name="WithEngineer">
/// Cases in <see cref="Pegasus.Core.Workflow.CaseLifecycleState.ReportPreparation"/>
/// or <see cref="Pegasus.Core.Workflow.CaseLifecycleState.PostReport"/>: the
/// operator reads both as "With Engineer" (EPIC-011 D3).
/// </param>
public sealed record CaseStageCounts(int NotReady, int Review, int Held, int WithEngineer);

/// <summary>
/// What moved today and this week.
/// </summary>
/// <remarks>
/// "Sent to Engineer" is counted from the first-handoff proxy, which is the
/// recorded fact that a case reached an Engineer, rather than from a workflow
/// transition that only says the case became eligible. "Reports sent" counts
/// case-linked sent evidence, so a sent message that was never attributed to a
/// case is not claimed as a delivered report.
/// </remarks>
public sealed record CaseActivityCounts(
    int NewCasesToday,
    int SentToEngineerToday,
    int SentToEngineerThisWeek,
    int ReportsSentToday,
    int ReportsSentThisWeek);

/// <summary>
/// What arrived, and what is waiting for a person.
/// </summary>
/// <remarks>
/// <see cref="ReceivedToday"/> counts mailbox-channel intake only (PLAT-012):
/// it backs the Dashboard's E-mail activity tile, so a manual upload — a
/// different intake channel entirely — must not move it.
/// </remarks>
public sealed record MailActivityCounts(int ReceivedToday, int NeedsSorting)
{
    /// <summary>Open Unidentified items; NeedsSorting remains read-only compatibility during rollout.</summary>
    public int Unidentified { get; init; } = NeedsSorting;
}

/// <summary>
/// The dashboard's counts. Every member returns a real number or the tile that
/// would have shown it is not rendered — there is no placeholder value.
/// </summary>
public interface IDashboardQueries
{
    Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken);

    Task<CaseActivityCounts> GetCaseActivityCountsAsync(
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken);

    Task<MailActivityCounts> GetMailActivityCountsAsync(
        DateTimeOffset dayStartUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// The five kinds of work the Work Centre lists as needing attention
/// (FRD-12 § Work Centre). Each is derived from one existing Core query;
/// there is no sixth kind and no placeholder row.
/// </summary>
public enum NeedsAttentionKind
{
    /// <summary>A Case whose missing-material chase is due (its readiness blocker).</summary>
    Case,

    /// <summary>A Case on hold, waiting for a decision.</summary>
    HeldDecision,

    /// <summary>An open Unidentified item.</summary>
    Mail,

    /// <summary>A Triage record with no finding recorded yet.</summary>
    Triage,

    /// <summary>External work that failed and can be retried.</summary>
    ExternalWork
}

/// <summary>
/// The work-item priority chip. Declaration order is the list order: an
/// overdue chase and a retryable failure come first, then work due within
/// the office day, then the rest.
/// </summary>
public enum NeedsAttentionPriority
{
    Overdue,
    High,
    Today,
    Normal
}

/// <summary>
/// One needs-attention row and its detail. Every field is a recorded fact
/// or a Core enum name; the Web layer labels them and owns the route to the
/// record behind <paramref name="Id"/>.
/// </summary>
/// <param name="Id">The record the row opens (Case, Unidentified item, Triage record; the Case for external work).</param>
/// <param name="Reason">Why it needs attention — a chase's missing-material reason, a failure reason, an Unidentified reason code, a Triage state or a Case state.</param>
/// <param name="Source">Where the work came from — a Case origin, media kind, principal or vehicle registration.</param>
public sealed record NeedsAttentionItem(
    NeedsAttentionKind Kind,
    Guid Id,
    string Reference,
    string Title,
    string? Detail,
    string Reason,
    NeedsAttentionPriority Priority,
    string? Owner,
    DateTimeOffset? Due,
    string? LastOutcome,
    string? Source);
