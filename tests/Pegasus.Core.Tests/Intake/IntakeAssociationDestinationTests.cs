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
    [InlineData(IntakeDecision.BlockedIntake)]
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
    public void OrdinaryNonManualReceiptsCannotOfferAStaffDestination(IntakeSourceChannel channel)
    {
        Assert.False(IntakeAssociationDestinationPolicy.CanOffer(
            Receipt(IntakeDecision.NeedsSorting, channel: channel)));
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

    [Theory]
    [InlineData(CaseLifecycleState.Review, false, false, true)]
    [InlineData(CaseLifecycleState.Review, true, false, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, false, false, false)]
    public void StandardMaterialRequiresAnUnarchivedNonterminalDestination(
        CaseLifecycleState state,
        bool archived,
        bool hasReportSentEvidence,
        bool expected)
    {
        Assert.Equal(expected, IntakeAssociationDestinationPolicy.IsViable(
            Receipt(IntakeDecision.NeedsSorting), state, archived, hasReportSentEvidence));
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, false, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, false, true)]
    [InlineData(CaseLifecycleState.PostReport, false, false)]
    [InlineData(CaseLifecycleState.Review, true, false)]
    public void RegisteredImageMaterialRequiresAnEligiblePreReportDestination(
        CaseLifecycleState state,
        bool hasReportSentEvidence,
        bool expected)
    {
        Assert.Equal(expected, IntakeAssociationDestinationPolicy.IsViable(
            Receipt(IntakeDecision.ImageIntakeRegistered, assets: [ImageAsset()]),
            state,
            archived: false,
            hasReportSentEvidence: hasReportSentEvidence));
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
