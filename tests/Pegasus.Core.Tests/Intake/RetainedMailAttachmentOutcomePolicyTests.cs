using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// One attachment's own outcome (message planning, 13 September): decided from
/// the receipt that processed it, its image record and its Unidentified item,
/// so the attachments of one message can read differently.
/// </summary>
public sealed class RetainedMailAttachmentOutcomePolicyTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2031, 5, 6, 9, 31, 0, TimeSpan.Zero);

    [Fact]
    public void AnUnprocessedAttachmentReadsNotYetProcessed()
    {
        var (kind, record, reason) = RetainedMailAttachmentOutcomePolicy.Decide(new(null, null, null, false));
        Assert.Equal(AttachmentOutcomeKind.NotYetProcessed, kind);
        Assert.Null(record);
        Assert.Null(reason);
    }

    [Fact]
    public void AMemberThatCouldNotBeReadSaysSoWithItsReasonAndItsUnidentifiedItemEvenInsideACase()
    {
        var caseId = Guid.NewGuid();
        var receipt = Receipt(IntakeDecision.ImageIntakeRegistered, failureReason: "The image could not be decoded.") with { AcceptedCaseId = caseId };
        var item = Item("U00042");

        var (kind, record, reason) = RetainedMailAttachmentOutcomePolicy.Decide(new(receipt, null, item, MemberCouldNotBeRead: true));

        Assert.Equal(AttachmentOutcomeKind.CouldNotBeRead, kind);
        Assert.Equal(new AttachmentOutcomeRecord(AttachmentOutcomeRecordKind.Unidentified, item.Id, "U00042"), record);
        Assert.Equal("The image could not be decoded.", reason);
    }

    [Theory]
    [InlineData(IntakeDecision.Unsupported)]
    [InlineData(IntakeDecision.OcrRequired)]
    public void AnUnreadableDocumentReadsCouldNotBeRead(IntakeDecision decision)
    {
        var (kind, _, reason) = RetainedMailAttachmentOutcomePolicy.Decide(
            new(Receipt(decision, decisionReason: "The PDF text layer was empty."), null, null, false));
        Assert.Equal(AttachmentOutcomeKind.CouldNotBeRead, kind);
        Assert.Equal("The PDF text layer was empty.", reason);
    }

    [Fact]
    public void TheCaseTheReceiptReachedIsTheOutcome()
    {
        var caseId = Guid.NewGuid();
        var (kind, record, _) = RetainedMailAttachmentOutcomePolicy.Decide(
            new(Receipt(IntakeDecision.CaseCreated) with { AcceptedCaseId = caseId }, null, null, false));
        Assert.Equal(AttachmentOutcomeKind.CaseCreated, kind);
        Assert.Equal(AttachmentOutcomeRecordKind.Case, record!.Kind);
        Assert.Equal(caseId, record.Id);
    }

    [Fact]
    public void ProcessingFailedCarriesTheFailureAndItsItem()
    {
        var item = Item("U00043");
        var (kind, record, reason) = RetainedMailAttachmentOutcomePolicy.Decide(
            new(Receipt(IntakeDecision.TechnicalFailure, failureReason: "Processing could not complete."), null, item, false));
        Assert.Equal(AttachmentOutcomeKind.ProcessingFailed, kind);
        Assert.Equal(item.Id, record!.Id);
        Assert.Equal("Processing could not complete.", reason);
    }

    [Fact]
    public void AnImageRecordReadsVehicleImagesAndOtherwiseTheUnidentifiedItem()
    {
        var image = new AttachmentOutcomeRecord(AttachmentOutcomeRecordKind.ImageIntake, Guid.NewGuid(), "AB12CDE-01");
        Assert.Equal(
            (AttachmentOutcomeKind.VehicleImages, image, (string?)null),
            RetainedMailAttachmentOutcomePolicy.Decide(new(Receipt(IntakeDecision.ImageIntakeRegistered), image, null, false)));

        var item = Item("U00044");
        var (kind, record, _) = RetainedMailAttachmentOutcomePolicy.Decide(new(Receipt(IntakeDecision.NeedsSorting), null, item, false));
        Assert.Equal(AttachmentOutcomeKind.Unidentified, kind);
        Assert.Equal(item.Id, record!.Id);

        Assert.Equal(
            AttachmentOutcomeKind.NoDestination,
            RetainedMailAttachmentOutcomePolicy.Decide(new(Receipt(IntakeDecision.NeedsSorting), null, null, false)).Kind);
    }

    private static IntakeReceipt Receipt(
        IntakeDecision decision,
        string? failureReason = null,
        string decisionReason = "Processed.") => new(
        Guid.NewGuid(),
        "estimate.pdf",
        "application/pdf",
        102_400,
        new string('A', 64),
        new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "desk-token"),
        ReceivedAtUtc,
        ReceivedAtUtc.AddMinutes(1),
        decision,
        decisionReason,
        [],
        [],
        null,
        [],
        failureReason is null ? null : "failure",
        failureReason,
        false,
        "pdf",
        "1",
        null,
        null,
        Version: 1);

    private static UnidentifiedItem Item(string reference) => new(
        Guid.NewGuid(),
        42,
        reference,
        UnidentifiedOrigin.Receipt(Guid.NewGuid()),
        UnidentifiedReasonCode.CouldNotBeRead,
        "The file could not be read.",
        UnidentifiedState.Open,
        ReceivedAtUtc,
        null,
        ActionActor.SystemWorker("intake-processing"),
        null,
        null,
        null,
        null,
        null,
        1);
}
