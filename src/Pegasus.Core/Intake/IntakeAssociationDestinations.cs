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

    Task<IntakeAssociationDestination?> GetAsync(
        IntakeReceipt receipt,
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>Shared read/write eligibility facts for manual association.</summary>
public static class IntakeAssociationDestinationPolicy
{
    public static bool CanOffer(IntakeReceipt receipt) =>
        receipt.CurrentCaseId is null
        && (receipt.Decision == IntakeDecision.OcrRequired
            || IntakeDecisionPolicy.CanBecomeCase(receipt.Decision)
            || receipt.Decision == IntakeDecision.ImageIntakeRegistered);

    /// <summary>
    /// Staff "Link to case" offers every Case and every Triage Case in any
    /// state (operator, 24 September 2026). Only an archived Case is refused,
    /// as every write to it is. Automatic association keeps its own rules.
    /// </summary>
    public static bool IsViable(bool archived) => !archived;
}
