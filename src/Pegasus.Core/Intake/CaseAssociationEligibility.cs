using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// The one owner of which Cases a pre-instruction record may combine with
/// automatically: an Image-initiated Case merging into a Case or Triage Case,
/// and a Triage Case linking to a formal Case
/// ([FRD-22](../../../docs/frd/frd-22-pre-case-gates-matching-and-association.md)).
/// Both use the same pre-report rule (operator, 26 October 2026). A member of
/// staff's link reaches every Case in any state and every Triage Case.
/// </summary>
public static class CaseAssociationEligibility
{
    /// <summary>
    /// A formal Case is eligible for automatic association only before report
    /// delivery: an editable pre-report workflow state and no retained
    /// report-sent evidence. Terminal and post-report states are never
    /// eligible.
    /// </summary>
    public static bool IsFormalCaseEligible(
        CaseLifecycleState state,
        bool hasReportSentEvidence) =>
        !hasReportSentEvidence
        && state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Held
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation;

    /// <summary>
    /// A Triage Case takes vehicle images automatically while it is open and
    /// not yet linked to its instruction Case; linking closes it.
    /// </summary>
    public static bool IsTriageCaseEligible(TriageState state, bool linkedToInstructionCase) =>
        !linkedToInstructionCase && TriageLifecycleRules.ActiveStates.Contains(state);

    /// <summary>
    /// A formal Case takes precedence over a Triage Case for the same vehicle:
    /// Triage candidates count only when no formal Case remains.
    /// </summary>
    public static IReadOnlyList<ImageIntakeCaseCandidate> PreferFormalCases(
        IReadOnlyList<ImageIntakeCaseCandidate> candidates)
    {
        var formal = FormalCases(candidates);
        return formal.Count > 0 ? formal : candidates;
    }

    /// <summary>
    /// Images matched before they register are filed on a formal Case
    /// directly; a Triage Case takes them through the merge once they have
    /// registered (FRD-19).
    /// </summary>
    public static IReadOnlyList<ImageIntakeCaseCandidate> FormalCases(
        IReadOnlyList<ImageIntakeCaseCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.Any(candidate => candidate.IsTriage)
            ? candidates.Where(candidate => !candidate.IsTriage).ToArray()
            : candidates;
    }
}
