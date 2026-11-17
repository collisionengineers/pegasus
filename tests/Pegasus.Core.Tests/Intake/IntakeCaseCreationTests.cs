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
            await new IntakeCaseCreation(new FixedEvidence(evidence), new StoredResolutions(null))
                .StandaloneAuditEvidenceIdAsync(receiptId, CancellationToken.None));
        Assert.Null(
            await new IntakeCaseCreation(new FixedEvidence(Evidence(Guid.NewGuid())), new StoredResolutions(null))
                .StandaloneAuditEvidenceIdAsync(receiptId, CancellationToken.None));
        Assert.Null(
            await new IntakeCaseCreation(new FixedEvidence(null), new StoredResolutions(null))
                .StandaloneAuditEvidenceIdAsync(receiptId, CancellationToken.None));
    }

    [Fact]
    public async Task AStoredAddressResolutionIsReadAsItIs()
    {
        var receipt = ReceiptWithAddress();
        var stored = Resolution(null, "2 Yard Road, York") with { IntakeReceiptId = receipt.Id };
        var resolutions = new StoredResolutions(stored);

        var read = await new IntakeCaseCreation(new FixedEvidence(null), resolutions)
            .AddressResolutionAsync(receipt, CancellationToken.None);

        Assert.Same(stored, read);
        Assert.Equal(receipt.Id, Assert.Single(resolutions.Asked));
    }

    [Fact]
    public async Task WithNoStoredResolutionTheItemIsUnresolvedOverItsOwnEvaluationEvenWithASuggestion()
    {
        var receipt = ReceiptWithAddress();

        var read = await new IntakeCaseCreation(new FixedEvidence(null), new StoredResolutions(null))
            .AddressResolutionAsync(receipt, CancellationToken.None);

        Assert.Equal(receipt.Id, read.IntakeReceiptId);
        Assert.Equal(receipt.Version, read.ReceiptVersion);
        Assert.Equal(InspectionAddressResolutionState.Unresolved, read.State);
        Assert.Equal("1 Garage Lane, Leeds", read.Evaluation.Suggestion?.Value);
        Assert.Equal(
            Ext18InspectionAddressPolicy.Evaluate(receipt).Suggestion?.Fingerprint,
            read.Evaluation.Suggestion?.Fingerprint);
        Assert.Null(read.ResolvedValue);
        Assert.Null(read.ResolvedByKind);
        Assert.Null(read.ResolvedBy);
        Assert.Null(read.ResolvedAtUtc);
    }

    private static IntakeReceipt ReceiptWithAddress()
    {
        var id = Guid.NewGuid();
        return new(
            id,
            "instruction.eml",
            "message/rfc822",
            10,
            new string('a', 64),
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, id.ToString("N")),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            IntakeDecision.CaseCreated,
            "Ready.",
            [],
            [new InstructionReviewField(
                "Inspection address",
                "1 Garage Lane, Leeds",
                [new InstructionFieldCandidate("1 Garage Lane, Leeds", IntakeEvidenceSource.EmailBody, "body")],
                false,
                false)],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            "extraction-policy",
            1,
            Version: 4);
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

    private sealed class StoredResolutions(InspectionAddressResolutionSnapshot? stored)
        : IInspectionAddressResolutionStore
    {
        public List<Guid> Asked { get; } = [];

        public Task<InspectionAddressResolutionSnapshot?> GetAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken)
        {
            Asked.Add(intakeReceiptId);
            return Task.FromResult(stored);
        }

        public Task<InspectionAddressResolutionSnapshot> ResolveAsync(
            InspectionAddressResolutionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedEvidence(StandaloneAuditEvidence? evidence) : IStandaloneAuditEvidenceQueries
    {
        public Task<StandaloneAuditEvidence?> GetForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => Task.FromResult(evidence);
    }
}
