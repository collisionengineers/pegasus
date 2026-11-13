using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The steps the staff Create case page and the Automation Actor's accept
/// share when a received item becomes a Case (R1042).
/// </summary>
public sealed class IntakeCaseCreationTests
{
    [Theory]
    [InlineData(IntakeDecision.CaseCreated, true)]
    [InlineData(IntakeDecision.NeedsSorting, true)]
    [InlineData(IntakeDecision.OcrRequired, true)]
    [InlineData(IntakeDecision.Unsupported, false)]
    [InlineData(IntakeDecision.TechnicalFailure, false)]
    [InlineData(IntakeDecision.ImageIntakeRegistered, false)]
    public void APersonCreatesACaseOnAnItemThatCanBecomeOneOrIsHandKeyed(IntakeDecision decision, bool accepted) =>
        Assert.Equal(accepted, IntakeCaseCreation.AcceptsByHand(decision));

    [Fact]
    public void AnImageBasedPrincipalsAddressIsTheSettledOneThenTheSuggestionThenImageBased()
    {
        var suggestion = new InspectionAddressSuggestion(
            "1 Garage Lane, Leeds", InspectionAddressEvidenceKind.PhysicalAddress, [], "fingerprint");

        Assert.Equal(
            "2 Yard Road, York",
            IntakeCaseCreation.ImageBasedInspectionAddress(Resolution(suggestion, "2 Yard Road, York")));
        Assert.Equal(
            "1 Garage Lane, Leeds",
            IntakeCaseCreation.ImageBasedInspectionAddress(Resolution(suggestion, null)));
        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            IntakeCaseCreation.ImageBasedInspectionAddress(Resolution(null, null)));
    }

    [Fact]
    public async Task OnlyAuditEvidenceConfirmedOnTheItemItselfIsCarried()
    {
        var receiptId = Guid.NewGuid();
        var evidence = Evidence(receiptId);

        Assert.Equal(
            evidence.Id,
            await new IntakeCaseCreation(new FixedEvidence(evidence))
                .StandaloneAuditEvidenceIdAsync(receiptId, CancellationToken.None));
        Assert.Null(
            await new IntakeCaseCreation(new FixedEvidence(Evidence(Guid.NewGuid())))
                .StandaloneAuditEvidenceIdAsync(receiptId, CancellationToken.None));
        Assert.Null(
            await new IntakeCaseCreation(new FixedEvidence(null))
                .StandaloneAuditEvidenceIdAsync(receiptId, CancellationToken.None));
    }

    private static InspectionAddressResolutionSnapshot Resolution(
        InspectionAddressSuggestion? suggestion,
        string? resolvedValue) =>
        new(
            Guid.NewGuid(),
            1,
            resolvedValue is null ? InspectionAddressResolutionState.Unresolved : InspectionAddressResolutionState.Accepted,
            new InspectionAddressEvaluation(suggestion, []),
            resolvedValue,
            null,
            null,
            null);

    private static StandaloneAuditEvidence Evidence(Guid receiptId) => new(
        Guid.NewGuid(),
        receiptId,
        Guid.NewGuid(),
        AuditAssessment.Repairable,
        Guid.Empty,
        DateTimeOffset.UnixEpoch,
        "The retained original report states Repairable.",
        0,
        false);

    private sealed class FixedEvidence(StandaloneAuditEvidence? evidence) : IStandaloneAuditEvidenceQueries
    {
        public Task<StandaloneAuditEvidence?> GetForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => Task.FromResult(evidence);
    }
}
