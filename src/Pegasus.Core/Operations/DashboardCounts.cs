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
/// operator reads both as "With Engineer" (operator decision D3).
/// </param>
/// <param name="AwaitingInstruction">
/// Unassociated image-initiated records still awaiting instruction.
/// </param>
/// <param name="Query">
/// Cases in <see cref="Pegasus.Core.Workflow.CaseLifecycleState.Query"/>,
/// a separate reversible workflow queue.
/// </param>
public sealed record CaseStageCounts(
    int NotReady,
    int Review,
    int Held,
    int WithEngineer,
    int AwaitingInstruction = 0,
    int Query = 0);

/// <summary>
/// A pre-report Case its early vehicle images were paired into, which no
/// member of staff has changed since (FRD-19, INT-32).
/// </summary>
/// <param name="PairedAtUtc">When the Image-initiated Case merged into the Case.</param>
/// <param name="ImagesRegisteredAtUtc">When the images registered as their Image-initiated Case.</param>
public sealed record PairedVehicleImagesCase(
    Guid CaseId,
    string Reference,
    string ImageReference,
    string? Principal,
    Guid? EngineerId,
    DateTimeOffset PairedAtUtc,
    DateTimeOffset ImagesRegisteredAtUtc);

/// <summary>
/// One open Case task with the Case it is on and its creation (FRD-15, Open
/// tasks). The Work Centre makes one row of each Case's open tasks.
/// </summary>
/// <param name="CreatedAtUtc">When the task was created: its creation event's instant.</param>
/// <param name="CreatedByStaffId">The staff member the creation event records; null when its actor was not staff.</param>
public sealed record OpenCaseTask(
    Guid CaseId,
    string Reference,
    string? Principal,
    string? Registration,
    string? Claimant,
    string? VehicleMake,
    string? VehicleModel,
    Guid TaskId,
    string Description,
    Guid? AssigneeId,
    DateTimeOffset CreatedAtUtc,
    Guid? CreatedByStaffId);

/// <summary>
/// The Cases rail counts and the Work Centre's own reads. Every count is a
/// real number or the tile that would have shown it is not rendered — there is
/// no placeholder value.
/// </summary>
public interface IDashboardQueries
{
    Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Pre-report, unarchived Cases with a merged Image-initiated Case and no
    /// version-advancing staff change since that merge, one row per Case (its
    /// latest merge).
    /// </summary>
    Task<IReadOnlyList<PairedVehicleImagesCase>> ListPairedVehicleImagesAwaitingStaffAsync(
        CancellationToken cancellationToken);

    /// <summary>Every open Case task with its creation, one row per task.</summary>
    Task<IReadOnlyList<OpenCaseTask>> ListOpenCaseTasksAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The Work Centre's actionable kinds (Work Centre D1–D3, D9). Each is derived
/// from one existing Core query; every kind but <see cref="NeedsAttentionKind.OpenTasks"/> carries
/// a due instant from the workflow targets. There is no placeholder row. Failed
/// external work is not a kind (D1): a custody failure is retried in its Case's
/// Custody page.
/// </summary>
public enum NeedsAttentionKind
{
    /// <summary>A Case whose missing-material chase is due (its readiness blocker); due at the next chase time.</summary>
    CaseChase,

    /// <summary>An open Unidentified item; due received + Unidentified target.</summary>
    Unidentified,

    /// <summary>A Triage record with no finding recorded yet; due opened + Triage target.</summary>
    Triage,

    /// <summary>A Case on hold, waiting for a decision; due on its review date, else held + Held decision target.</summary>
    HeldDecision,

    /// <summary>A Case that is ready for the required Review decision; due entered Review + Review target.</summary>
    ReviewCase,

    /// <summary>A ready Case that has no Engineer assigned; due entered Review + Review target.</summary>
    UnassignedEngineer,

    /// <summary>An AI job in Draft ready waiting for a person; due draft written + AI draft target.</summary>
    AiDraft,

    /// <summary>
    /// A pre-report Case its early vehicle images were paired into, not
    /// changed by staff since; due at the pairing.
    /// </summary>
    VehicleImagesPaired,

    /// <summary>
    /// A Case with at least one open task, named by its oldest open task; no
    /// due instant, so always Normal.
    /// </summary>
    OpenTasks
}

/// <summary>
/// Where a row sits against its due instant (Work Centre D2): Overdue is due at
/// or before now, Today is due before the next midnight Europe/London, Normal is
/// later or undated. Declaration order is the list order.
/// </summary>
public enum NeedsAttentionPriority
{
    Overdue,
    Today,
    Normal
}

/// <summary>
/// One needs-attention row and its detail. Every field is a recorded fact
/// or a Core enum name; the Web layer labels them and owns the route to the
/// record behind <paramref name="Id"/>.
/// </summary>
/// <param name="Id">The record the row opens (Case, Unidentified item, Triage record; the Case for external work).</param>
/// <param name="Reason">Why it needs attention — a Core enum name or a recorded fact: a chase state, a Case state, an Unidentified reason code, a Triage state, an external failure reason or the first open task's description.</param>
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
    string Owner,
    DateTimeOffset? Due,
    string? LastOutcome,
    string? Source,
    int? Attempts,
    DateTimeOffset? Received = null)
{
    /// <summary>The due instant the row ages against (Work Centre D3); null only when the kind has none yet.</summary>
    public DateTimeOffset? DueAtUtc => Due;

    public DateTimeOffset? ReceivedAtUtc => Received;

    /// <summary>The staff member the row belongs to — the Case's engineer, the Triage assignee, or the first open task's assignee else its creator; null when unowned.</summary>
    public Guid? OwnerStaffId { get; init; }

    /// <summary>How many more open tasks the Case has beyond the one an Open tasks row names; 0 for every other kind.</summary>
    public int MoreCount { get; init; }

    /// <summary>The relative application path the row's action opens (Work Centre P4).</summary>
    public string Route { get; init; } = string.Empty;

    /// <summary>When the row's current occurrence began; a dismissal of <see cref="Id"/> at or after it hides the row.</summary>
    public DateTimeOffset QualifiedAtUtc { get; init; }
}
