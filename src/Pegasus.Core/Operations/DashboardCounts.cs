namespace Pegasus.Core.Operations;

/// <summary>
/// How many Cases and image-initiated pre-Cases sit in each Cases rail scope.
/// </summary>
/// <remarks>
/// These three counts did not exist before. The dashboard rendered the literal
/// string "Unavailable" for Not ready and Held, and backed the Review tile with
/// an intake-receipt count — a different entity entirely, and one that was
/// cumulative for all time.
///
/// <see cref="NotReady"/> counts formal Cases only. Image-initiated records
/// still awaiting instruction are counted separately by
/// <see cref="AwaitingInstruction"/>.
/// </remarks>
/// <param name="WithEngineer">
/// Cases in <see cref="Pegasus.Core.Workflow.CaseLifecycleState.ReportPreparation"/>
/// or <see cref="Pegasus.Core.Workflow.CaseLifecycleState.PostReport"/>: the
/// operator reads both as "With Engineer" (EPIC-011 D3).
/// </param>
/// <param name="AwaitingInstruction">
/// Unassociated image-initiated records still awaiting instruction.
/// </param>
/// <param name="Complete">
/// Cases in <see cref="Pegasus.Core.Workflow.CaseLifecycleState.PostReportComplete"/>,
/// the one terminal outcome the Cases rail lists (EPIC-011 D3); the other
/// terminals are excluded from the rail and never counted here.
/// </param>
public sealed record CaseStageCounts(
    int NotReady,
    int Review,
    int Held,
    int WithEngineer,
    int AwaitingInstruction = 0,
    int Complete = 0);

/// <summary>
/// The dashboard's counts. Every member returns a real number or the tile that
/// would have shown it is not rendered — there is no placeholder value.
/// </summary>
public interface IDashboardQueries
{
    Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken);
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
/// <param name="Reason">Why it needs attention — a Core enum name or a recorded failure fact: a chase state, a Case state, an Unidentified reason code, a Triage state or an external failure reason.</param>
/// <param name="Source">Where the work came from — a Case origin, media kind or principal; null when the kind records none.</param>
/// <param name="Attempts">How many times the work has been tried — external work only; null when the kind records none.</param>
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
    string? Source,
    int? Attempts);
