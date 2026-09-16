using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class UnidentifiedNextStepPolicyTests
{
    [Fact]
    public void AWaitingAuditOffersItsOriginalReportInsteadOfCaseCreation()
    {
        var next = UnidentifiedNextStepPolicy.Primary(Facts() with
        {
            CanCreateCase = true,
            CanLinkCase = true
        });

        Assert.Equal(UnidentifiedNextStep.AddOriginalReport, next);
    }

    [Fact]
    public void AReportQueuedForReevaluationStaysInProcessingInsteadOfOfferingAnotherUpload()
    {
        var next = UnidentifiedNextStepPolicy.Primary(Facts() with
        {
            SuppliedOriginalReportCustodyState = IncomingArtifactCustodyState.Confirmed,
            IsReevaluationPending = true,
            CanCreateCase = true
        });

        Assert.Equal(UnidentifiedNextStep.AuditReportProcessing, next);
    }

    [Theory]
    [InlineData(IncomingArtifactCustodyState.Pending, UnidentifiedNextStep.AuditReportCustodyPending)]
    [InlineData(IncomingArtifactCustodyState.Unknown, UnidentifiedNextStep.AuditReportCustodyPending)]
    [InlineData(IncomingArtifactCustodyState.Failed, UnidentifiedNextStep.AuditReportCustodyFailed)]
    public void AnUnconfirmedReportCannotOfferCaseCreation(
        IncomingArtifactCustodyState custody,
        UnidentifiedNextStep expected)
    {
        var next = UnidentifiedNextStepPolicy.Primary(Facts() with
        {
            SuppliedOriginalReportCustodyState = custody,
            CanCreateCase = true
        });

        Assert.Equal(expected, next);
    }

    [Fact]
    public void AResolvedAuditHasNoFurtherPrimaryAction()
    {
        var next = UnidentifiedNextStepPolicy.Primary(Facts() with { State = UnidentifiedState.Resolved });

        Assert.Equal(UnidentifiedNextStep.None, next);
    }

    [Fact]
    public void AReviewableNeedsSortingReceiptIsNotAPrimaryCreateCase()
    {
        var receiptId = Guid.NewGuid();
        var item = new UnidentifiedItem(
            Guid.NewGuid(), 1, "U1", UnidentifiedOrigin.Receipt(receiptId),
            UnidentifiedReasonCode.NoUsableIdentification, "Needs staff review.", UnidentifiedState.Open,
            DateTimeOffset.UtcNow, null, ActionActor.SystemWorker("test"), null, null, null, null, null, 0);
        var receipt = new IntakeReceipt(
            receiptId, "instruction.pdf", "application/pdf", 1, new string('A', 64),
            new(IntakeSourceChannel.ManualUpload, "reviewable-needs-sorting"), DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, IntakeDecision.NeedsSorting, "Needs review.", [], [], null, [], null,
            null, false, "test-reader", "1", null, null,
            MailClassificationDecision: MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"), [],
                "Reviewable test classification.", "test", 1, CaseType.Inspection));

        Assert.True(UnidentifiedNextStepPolicy.CanReviewForCaseCreation(item, receipt));
        Assert.False(UnidentifiedNextStepPolicy.CanCreateCase(item, receipt));
        Assert.NotEqual(UnidentifiedNextStep.CreateCase, UnidentifiedNextStepPolicy.Primary(Facts() with
        {
            ReasonCode = UnidentifiedReasonCode.NoUsableIdentification,
            CanCreateCase = false,
            HasSourceMessage = false,
            HasSourceFile = false
        }));
    }

    private static UnidentifiedNextStepFacts Facts() => new(
        UnidentifiedState.Open,
        UnidentifiedReasonCode.AuditOriginalReportMissing,
        CanCreateCase: false,
        CanRegisterImages: false,
        CanOpenTriage: false,
        CanLinkCase: false,
        SuppliedOriginalReportCustodyState: null,
        IsReevaluationPending: false,
        HasManualUploadGroup: false,
        HasSourceMessage: true,
        HasSourceFile: false);
}
