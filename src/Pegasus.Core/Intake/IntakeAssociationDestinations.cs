using Pegasus.Core.Cases;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// The read boundary for a retained source's possible staff-selected Case destinations. It is
/// deliberately separate from the general Case search: a Case that can be
/// found is not necessarily a destination that may receive this source.
/// </summary>
public sealed record IntakeAssociationDestination(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    CaseLifecycleState State,
    long Version);

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

    public static bool IsViable(
        IntakeReceipt receipt,
        CaseLifecycleState state,
        bool archived,
        bool hasReportSentEvidence) =>
        !archived
        && !CaseLifecycleRules.IsTerminal(state)
        && (!ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt)
            || ImageIntakeLifecycleRules.IsCaseEligibleForAssociation(state, hasReportSentEvidence));
}
