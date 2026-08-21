using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// The terminal-state vocabulary used to be written out three times — in
/// <see cref="CaseLifecycleRules.IsTerminal"/>, in the EVA hand-off store and
/// in the vehicle-work sweep — so a state added to one was silently
/// non-terminal for the others. These guard the single owner and the one
/// derived view of it (INTK-029).
/// </summary>
public sealed class TerminalCaseStateTests
{
    [Fact]
    public void CancellingOnUnlinkIsATerminalState() =>
        Assert.True(CaseLifecycleRules.IsTerminal(CaseLifecycleState.SourceEmailUnlinked));

    [Fact]
    public void TerminalStateNamesAreExactlyTheStatesIsTerminalAccepts()
    {
        var expected = Enum.GetValues<CaseLifecycleState>()
            .Where(CaseLifecycleRules.IsTerminal)
            .Select(state => state.ToString())
            .ToArray();

        Assert.Equal(expected, CaseLifecycleRules.TerminalStateNames());
    }

    [Fact]
    public void EveryTerminalStateNameFitsThePersistedColumn() =>
        Assert.All(CaseLifecycleRules.TerminalStateNames(), name => Assert.True(name.Length <= 40));

    [Fact]
    public void CancellingOnUnlinkIsRefusedByTheGenericClose()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CaseLifecycleRules.ValidateClose(CloseWith(CaseClosureOutcome.SourceEmailUnlinked)));

        Assert.Equal(
            "Cancelling on unlink requires unlinking the email that created the case.",
            exception.Message);
    }

    [Fact]
    public void AnOrdinaryClosureOutcomeStillPassesValidation() =>
        CaseLifecycleRules.ValidateClose(CloseWith(CaseClosureOutcome.ProviderCancelled));

    private static CloseCaseRequest CloseWith(CaseClosureOutcome outcome) => new(
        Guid.NewGuid(),
        3,
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
        "close:terminal-state-tests",
        "The provider withdrew the instruction.",
        new string('t', 64),
        outcome);
}
