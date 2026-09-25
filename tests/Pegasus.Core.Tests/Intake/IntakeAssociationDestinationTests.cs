using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake;

public sealed class IntakeAssociationDestinationTests
{
    [Theory]
    [InlineData(IntakeDecision.OcrRequired)]
    [InlineData(IntakeDecision.NeedsSorting)]
    [InlineData(IntakeDecision.CaseCreated)]
    [InlineData(IntakeDecision.ImageIntakeRegistered)]
    public void ManualPendingReceiptsCanOfferAStaffDestination(IntakeDecision decision)
    {
        Assert.True(IntakeAssociationDestinationPolicy.CanOffer(Receipt(decision)));
    }

    [Theory]
    [InlineData(IntakeDecision.Unsupported)]
    [InlineData(IntakeDecision.TechnicalFailure)]
    public void UnsafeManualSourceOutcomesCannotOfferAStaffDestination(IntakeDecision decision)
    {
        Assert.False(IntakeAssociationDestinationPolicy.CanOffer(Receipt(decision)));
    }

    [Fact]
    public void AReceiptAlreadyAssociatedWithACaseCannotOfferAnotherDestination()
    {
        Assert.False(IntakeAssociationDestinationPolicy.CanOffer(
            Receipt(IntakeDecision.NeedsSorting, currentCaseId: Guid.NewGuid())));
    }

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox)]
    [InlineData(IntakeSourceChannel.Automation)]
    [InlineData(IntakeSourceChannel.ProviderApi)]
    public void SafeNonManualReceiptsCanOfferAReasonedStaffDestination(IntakeSourceChannel channel)
    {
        foreach (var decision in new[] { IntakeDecision.NeedsSorting, IntakeDecision.OcrRequired, IntakeDecision.CaseCreated })
        {
            Assert.True(IntakeAssociationDestinationPolicy.CanOffer(Receipt(decision, channel: channel)));
        }
        foreach (var decision in new[] { IntakeDecision.Unsupported, IntakeDecision.TechnicalFailure })
        {
            Assert.False(IntakeAssociationDestinationPolicy.CanOffer(Receipt(decision, channel: channel)));
        }
    }

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox)]
    [InlineData(IntakeSourceChannel.Automation)]
    [InlineData(IntakeSourceChannel.ProviderApi)]
    public void RegisteredImagesCanOfferAStaffDestinationAcrossSources(IntakeSourceChannel channel)
    {
        Assert.True(IntakeAssociationDestinationPolicy.CanOffer(
            Receipt(IntakeDecision.ImageIntakeRegistered, channel: channel, assets: [ImageAsset()])));
    }

    /// <summary>
    /// Staff "Link to case" reaches every Case in any lifecycle state (operator,
    /// 24 September 2026); only an archived Case is refused.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AStaffDestinationIsViableInAnyStateUnlessArchived(bool archived, bool expected)
    {
        Assert.Equal(expected, IntakeAssociationDestinationPolicy.IsViable(archived));
    }

    [Fact]
    public void ATriageCaseDestinationCarriesItsTriageStateInsteadOfACaseStage()
    {
        var destination = new IntakeAssociationDestination(
            Guid.NewGuid(), "t.QDOS26001", "AB12CDE", null, null, 3)
        {
            TriageState = Pegasus.Core.Triage.TriageState.Completed
        };

        Assert.True(destination.IsTriageCase);
        Assert.Null(destination.State);
    }

    private static IntakeReceipt Receipt(
        IntakeDecision decision,
        IntakeSourceChannel channel = IntakeSourceChannel.ManualUpload,
        Guid? currentCaseId = null,
        IReadOnlyList<IntakeAssetRecord>? assets = null) =>
        new(
            Guid.NewGuid(),
            "retained-source",
            assets is { Count: > 0 } ? "image/jpeg" : "application/pdf",
            1,
            new string('a', 64),
            new IntakeSourceIdentity(channel, "source-token"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            decision,
            "Test fixture.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "reader",
            "1",
            null,
            null,
            assets,
            ManualLinkedCaseId: currentCaseId,
            ManualAssociationVersion: currentCaseId is null ? null : 1,
            ManualAssociationActorKind: currentCaseId is null ? null : ActorKind.Staff);

    private static IntakeAssetRecord ImageAsset() => new(
        Guid.NewGuid(),
        "retained-source",
        "vehicle.jpg",
        "image/jpeg",
        IntakeAssetKind.Source,
        IntakeAssetDisposition.Source,
        1,
        new string('a', 64),
        "storage/vehicle.jpg",
        null,
        null,
        null,
        null);
}
