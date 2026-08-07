using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Which outcomes a case can still be made from — one rule, where there used
/// to be two that disagreed.
/// </summary>
/// <remarks>
/// The acceptance transaction allowed a definitive instruction or sorted
/// material; the staff screen allowed only <c>NeedsSorting</c>. Since a draft
/// correction rewrites the decision to <c>CaseCreated</c>, the screen would
/// refuse the very receipt it had just corrected.
/// </remarks>
public sealed class IntakeDecisionPolicyTests
{
    [Theory]
    [InlineData(IntakeDecision.CaseCreated)]
    [InlineData(IntakeDecision.NeedsSorting)]
    public void PreCaseMaterialCanBecomeACase(IntakeDecision decision) =>
        Assert.True(IntakeDecisionPolicy.CanBecomeCase(decision));

    [Theory]
    [InlineData(IntakeDecision.BlockedIntake)]
    [InlineData(IntakeDecision.Unsupported)]
    [InlineData(IntakeDecision.OcrRequired)]
    [InlineData(IntakeDecision.TechnicalFailure)]
    [InlineData(IntakeDecision.ImageIntakeRegistered)]
    public void EverythingElseIsRefused(IntakeDecision decision) =>
        Assert.False(IntakeDecisionPolicy.CanBecomeCase(decision));

    [Fact]
    public void EveryDecisionIsClassifiedDeliberately()
    {
        // A decision added later must be placed on one side or the other by
        // whoever adds it, rather than falling through to a default.
        foreach (var decision in Enum.GetValues<IntakeDecision>())
        {
            _ = IntakeDecisionPolicy.CanBecomeCase(decision);
        }
    }

    [Fact]
    public void AnUndeclaredDecisionFailsClosed() =>
        Assert.Throws<InvalidOperationException>(
            () => IntakeDecisionPolicy.CanBecomeCase((IntakeDecision)99));
}
