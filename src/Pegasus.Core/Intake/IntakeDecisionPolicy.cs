using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// Which processing outcomes a case can still be made from.
/// </summary>
/// <remarks>
/// This was two rules that disagreed. The acceptance transaction allowed a
/// definitive instruction or sorted material; the staff screen allowed only
/// <see cref="IntakeDecision.NeedsSorting"/>, so a screen that had just
/// corrected a draft — which rewrites the decision to
/// <see cref="IntakeDecision.CaseCreated"/> — would then refuse the very
/// receipt it had corrected. One rule, in Core, and both callers ask it.
/// </remarks>
public static class IntakeDecisionPolicy
{
    /// <summary>
    /// Whether an item with this decision can still be turned into a case.
    /// </summary>
    /// <remarks>
    /// A definitive instruction is eligible for typed allocation in the
    /// ordinary path, but the decision alone does not prove it succeeded; the
    /// acceptance transaction is the thing that refuses a second reference for
    /// an item that already has a case. A reasoned refusal, an unreadable
    /// source, an unsupported format, a technical failure and a registered
    /// image set are all outcomes that are not pre-case material, so none of
    /// them can become a case here.
    /// </remarks>
    public static bool CanBecomeCase(IntakeDecision decision) => decision switch
    {
        IntakeDecision.CaseCreated or IntakeDecision.NeedsSorting => true,
        IntakeDecision.BlockedIntake
            or IntakeDecision.Unsupported
            or IntakeDecision.OcrRequired
            or IntakeDecision.TechnicalFailure
            or IntakeDecision.ImageIntakeRegistered => false,
        _ => throw new InvalidOperationException(
            $"Unknown intake decision value '{(int)decision}'.")
    };
}
