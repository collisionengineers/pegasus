using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

public enum CaseFilingTargetDisposition
{
    /// <summary>No filing is due: the Case's own origin receipt, an archived Case, an automatic association the Case's state no longer accepts, or a Triage Case the pipeline associated.</summary>
    NotApplicable,

    /// <summary>The Case is being edited; filing waits for the editor.</summary>
    Deferred,

    /// <summary>The Case may receive the receipt's evidence now.</summary>
    Current
}

/// <param name="IsStaffAssociation">Whether the active association was a member of staff's decision rather than the pipeline's.</param>
/// <param name="IsOriginReceipt">Whether the receipt created the Case; acceptance custody filed its source.</param>
/// <param name="Workflow">The Case's workflow, or null for a Triage Case.</param>
/// <param name="HasTriage">Whether a Case with no workflow has its Triage record.</param>
public sealed record CaseFilingTargetFacts(
    bool IsStaffAssociation,
    bool IsOriginReceipt,
    CaseFilingWorkflowFacts? Workflow,
    bool HasTriage);

/// <param name="State">The workflow state, or null when the stored state is not one Pegasus knows.</param>
public sealed record CaseFilingWorkflowFacts(
    bool IsArchived,
    CaseLifecycleState? State,
    bool HasReportSentEvidence,
    DateTimeOffset? EditLeaseExpiresAtUtc);

/// <summary>
/// The one rule for whether a receipt's retained evidence may be filed on the
/// Case it is actively associated with. A member of staff's association — a
/// staff link, or a destination declared on upload — files on any Case in any
/// state (FRD-22), including a Triage Case, which has no workflow. The
/// pipeline's own association files only while the Case is in a state
/// automatic association accepts and no report has been sent
/// (<see cref="CaseAssociationEligibility.IsFormalCaseEligible"/>), and
/// never on a Triage Case. An archived Case never files. A live Case edit
/// lease defers filing rather than refusing it.
/// </summary>
/// <remarks>
/// The plan, every custody step and the Case-filing replay listing all answer
/// through this rule; a store that filters on it in SQL mirrors it from
/// <see cref="AutomaticFilingStates"/>.
/// </remarks>
public static class CaseFilingTargetRule
{
    /// <summary>The Case states the pipeline's own association files into while no report has been sent.</summary>
    public static IReadOnlyList<CaseLifecycleState> AutomaticFilingStates { get; } =
        Enum.GetValues<CaseLifecycleState>()
            .Where(state => CaseAssociationEligibility.IsFormalCaseEligible(state, false))
            .ToArray();

    public static CaseFilingTargetDisposition Decide(
        CaseFilingTargetFacts facts,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.IsOriginReceipt)
        {
            return CaseFilingTargetDisposition.NotApplicable;
        }

        if (facts.Workflow is { } workflow)
        {
            if (workflow.IsArchived
                || (!facts.IsStaffAssociation
                    && !(workflow.State is { } state
                        && CaseAssociationEligibility.IsFormalCaseEligible(
                            state, workflow.HasReportSentEvidence))))
            {
                return CaseFilingTargetDisposition.NotApplicable;
            }

            return CaseEditAuthority.IsHeld(workflow.EditLeaseExpiresAtUtc, nowUtc)
                ? CaseFilingTargetDisposition.Deferred
                : CaseFilingTargetDisposition.Current;
        }

        return facts.IsStaffAssociation && facts.HasTriage
            ? CaseFilingTargetDisposition.Current
            : CaseFilingTargetDisposition.NotApplicable;
    }
}
