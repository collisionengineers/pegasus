using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// The read boundary for a retained source's possible staff-selected Case destinations. It is
/// deliberately separate from the general Case search: a Case that can be
/// found is not necessarily a destination that may receive this source.
/// </summary>
/// <remarks>
/// A Triage Case is a destination too. It has no Case lifecycle state, so its
/// <see cref="State"/> is null and <see cref="TriageState"/> carries its Triage
/// state; <see cref="Version"/> is then its Triage version, the authority a
/// staff link to it is checked against.
/// </remarks>
public sealed record IntakeAssociationDestination(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    CaseLifecycleState? State,
    long Version)
{
    public Pegasus.Core.Triage.TriageState? TriageState { get; init; }

    /// <summary>The Principal the Case belongs to, when the source query carries it.</summary>
    public string? Principal { get; init; }

    public bool IsTriageCase => TriageState is not null;
}

public interface IIntakeAssociationDestinationQueries
{
    /// <summary>
    /// The retained match candidates the intake evaluation already recorded,
    /// filtered through the same current destination policy as a typed search.
    /// </summary>
    Task<IReadOnlyList<IntakeAssociationDestination>> GetSuggestedAsync(
        IntakeReceipt receipt,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntakeAssociationDestination>> SearchAsync(
        IntakeReceipt receipt,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The typed search for Change linked Case: for a message that is already
    /// linked, the Cases it may be linked to instead, never the one it is linked
    /// to. <see cref="SearchAsync"/> answers only a message with no Case.
    /// </summary>
    Task<IReadOnlyList<IntakeAssociationDestination>> SearchToChangeAsync(
        IntakeReceipt receipt,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<IntakeAssociationDestination?> GetAsync(
        IntakeReceipt receipt,
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A Case or Triage Case as a destination before anything has been
    /// uploaded to it: what Add evidence on a Case page declares (FRD-18).
    /// Null when the Case is unknown or archived.
    /// </summary>
    Task<IntakeAssociationDestination?> GetAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IntakeAssociationDestination?>(null);

    /// <summary>
    /// The same Case or Triage Case as <see cref="GetAsync(Guid, ActionActor, CancellationToken)"/>
    /// answers, from its authority row alone: its reference, state and the
    /// version a link is checked against, without the registration, claimant
    /// and Principal that read adds for a destination card. For a caller that
    /// claims the destination and shows nothing of it.
    /// </summary>
    Task<IntakeAssociationDestination?> GetAuthorityAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IntakeAssociationDestination?>(null);
}

/// <summary>Shared read/write eligibility facts for manual association.</summary>
public static class IntakeAssociationDestinationPolicy
{
    public static bool CanOffer(IntakeReceipt receipt) =>
        receipt.CurrentCaseId is null && CanBeLinked(receipt);

    /// <summary>
    /// Change linked Case offers a linked message the same destinations. It
    /// unlinks first, so the link itself is still decided by
    /// <see cref="CanOffer"/> against the receipt the unlink leaves.
    /// </summary>
    public static bool CanOfferToChange(IntakeReceipt receipt) =>
        receipt.CurrentCaseId is not null && CanBeLinked(receipt);

    private static bool CanBeLinked(IntakeReceipt receipt) =>
        receipt.Decision == IntakeDecision.OcrRequired
        || IntakeDecisionPolicy.CanBecomeCase(receipt.Decision)
        || receipt.Decision == IntakeDecision.ImageIntakeRegistered;

    /// <summary>
    /// Staff "Link to case" offers every Case and every Triage Case in any
    /// state (operator, 24 September 2026). Only an archived Case is refused,
    /// as every write to it is. Automatic association keeps its own rules.
    /// </summary>
    public static bool IsViable(bool archived) => !archived;
}
