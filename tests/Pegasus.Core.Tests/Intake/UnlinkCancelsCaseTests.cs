using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Whether an unlink cancels the case is decided once, on the receipt, so the
/// Mail surface never works it out again from raw fields — the warning it shows
/// and the cancellation the store performs read the same rule (INTK-029).
/// </summary>
public sealed class UnlinkCancelsCaseTests
{
    private static readonly Guid OwnCase = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherCase = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public void TheEmailWhoseAcceptanceCreatedTheCaseCancelsItWhenUnlinked() =>
        Assert.True(Receipt(acceptedCaseId: OwnCase).UnlinkCancelsCase);

    [Fact]
    public void AReceiptRelinkedToAnotherCaseDoesNotCancelThatCase() =>
        Assert.False(
            Receipt(acceptedCaseId: OwnCase, manualCaseId: OtherCase, manualVersion: 1)
                .UnlinkCancelsCase);

    [Fact]
    public void AReceiptMerelyAssociatedWithACaseDoesNotCancelIt() =>
        Assert.False(
            Receipt(acceptedCaseId: null, manualCaseId: OtherCase, manualVersion: 0)
                .UnlinkCancelsCase);

    [Fact]
    public void AnAlreadyUnlinkedOriginNoLongerOffersToCancelAnything() =>
        Assert.False(
            Receipt(acceptedCaseId: OwnCase, manualCaseId: null, manualVersion: 1)
                .UnlinkCancelsCase);

    private static IntakeReceipt Receipt(
        Guid? acceptedCaseId,
        Guid? manualCaseId = null,
        long? manualVersion = null) =>
        new(
            Guid.NewGuid(),
            "instruction.pdf",
            "application/pdf",
            1024,
            new string('a', 64),
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "message-token"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            IntakeDecision.CaseCreated,
            "The instruction names the principal and one claim.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            null,
            null,
            null,
            AcceptedCaseId: acceptedCaseId,
            ManualLinkedCaseId: manualCaseId,
            ManualAssociationVersion: manualVersion);
}
