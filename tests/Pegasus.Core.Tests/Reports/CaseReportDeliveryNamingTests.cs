using Pegasus.Core.Cases;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// v28 P21, P22 and P23: the addresses the delivery form offers, the
/// documents a delivery attaches, and how the attached report is named and
/// covered when the Case has sent a report before.
/// </summary>
public sealed class CaseReportDeliveryNamingTests
{
    private static readonly Guid GenerationId = Guid.NewGuid();

    private static readonly StaffMailAttachment ReportAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 120, "CE_100_assessment.pdf", "application/pdf");

    private static readonly StaffMailAttachment FeeAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('b', 64), 60, "CE_100_fee_note.pdf", "application/pdf");

    private static readonly StaffMailAttachment ImageAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('c', 64), 90, "CE_100_images.pdf", "application/pdf");

    [Fact]
    public void TheFirstReportIsNamedForTheCaseAndCarriesThePlainCoveringLine()
    {
        Assert.Equal(
            "QDOS26001 PK12TMZ Repairable report",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", 0));
        Assert.Equal(
            CaseReportDeliveryNaming.FirstMessage,
            CaseReportDeliveryNaming.Message(CaseReportSendHistory.None));
    }

    /// <summary>A re-issue adds one dot for each report of this Case already sent.</summary>
    [Theory]
    [InlineData(1, "QDOS26001 PK12TMZ Total loss report.")]
    [InlineData(2, "QDOS26001 PK12TMZ Total loss report..")]
    public void EachReportAlreadySentAddsADot(int sentCount, string expected) =>
        Assert.Equal(
            expected,
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Total loss", sentCount));

    [Fact]
    public void ACaseWithNoRegistrationOrOutcomeStillNamesItsReport() =>
        Assert.Equal(
            "QDOS26001 report",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "  ", null, 0));

    [Fact]
    public void ANegativeSentCountIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", -1));

    [Fact]
    public void ALaterReportSaysWhichReportItSupersedes() =>
        Assert.Equal(
            "Please find attached our updated report, which supersedes our report dated 19 August 2026.",
            CaseReportDeliveryNaming.Message(new(1, new DateOnly(2026, 8, 19))));

    /// <summary>
    /// A send recorded without the report date it superseded still covers the
    /// delivery, rather than naming a date nobody holds.
    /// </summary>
    [Fact]
    public void ASendWithNoRecordedReportDateKeepsThePlainCoveringLine() =>
        Assert.Equal(
            CaseReportDeliveryNaming.FirstMessage,
            CaseReportDeliveryNaming.Message(new(2, null)));

    [Fact]
    public void OnlyTheReportIsRenamedAndTheCompanionsKeepTheirCustodyNames()
    {
        var named = CaseReportDeliveryNaming.Named(
            [ReportAttachment, FeeAttachment, ImageAttachment],
            "QDOS26001 PK12TMZ Repairable report");

        Assert.Equal("QDOS26001 PK12TMZ Repairable report.pdf", named[0].FileName);
        Assert.Equal(FeeAttachment, named[1]);
        Assert.Equal(ImageAttachment, named[2]);
        // The bytes a rename travels with are the same bytes.
        Assert.Equal(ReportAttachment.Sha256, named[0].Sha256);
        Assert.Equal(ReportAttachment.VersionId, named[0].VersionId);
    }

    [Fact]
    public void TheAddressBookOffersThePrincipalsAddressesAndTheCasesOwnSender()
    {
        var book = CaseReportDeliveryPolicy.AddressBook(Suggestions(
            ["claims@principal.example", "claims@principal.example", "reports@principal.example"],
            includeOriginalInstructionSender: true,
            originalInstructionSender: "handler@principal.example"));

        Assert.Equal(
            ["handler@principal.example", "claims@principal.example", "reports@principal.example"],
            book.Select(candidate => candidate.Address));
        Assert.Equal("This case", book[0].Source);
        Assert.Equal("Principal", book[1].Source);
    }

    [Fact]
    public void TheAddressBookLeavesOutTheSenderThePrincipalDoesNotWant()
    {
        var book = CaseReportDeliveryPolicy.AddressBook(Suggestions(
            ["claims@principal.example"],
            includeOriginalInstructionSender: false,
            originalInstructionSender: "handler@principal.example"));

        Assert.Equal(["claims@principal.example"], book.Select(candidate => candidate.Address));
    }

    [Fact]
    public void ADeliveryAttachesTheDocumentsItChose()
    {
        var attachments = CaseReportDeliveryPolicy.Attachments(
            GenerationId,
            [
                Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment),
                Artifact(CaseReportArtifactKind.FeeNote, FeeAttachment),
                Artifact(CaseReportArtifactKind.ImagePack, ImageAttachment),
            ],
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack]);

        Assert.Equal([ReportAttachment, ImageAttachment], attachments);
    }

    /// <summary>
    /// A companion still being filed to Box never blocks a delivery that did
    /// not ask for it, and never silently drops out of one that did.
    /// </summary>
    [Fact]
    public void AnUnconfirmedCompanionOnlyBlocksTheDeliveryThatChoseIt()
    {
        IReadOnlyList<CaseReportArtifactRecord> artifacts =
        [
            Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment),
            Artifact(CaseReportArtifactKind.ImagePack, ImageAttachment, CaseReportArtifactStatus.Pending),
        ];

        Assert.Equal(
            [ReportAttachment],
            CaseReportDeliveryPolicy.Attachments(
                GenerationId, artifacts, [CaseReportArtifactKind.AssessmentReport]));
        Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.Attachments(
            GenerationId, artifacts, [CaseReportArtifactKind.ImagePack]));
    }

    [Fact]
    public void ADeliveryCannotAttachADocumentTheGenerationDoesNotHold() =>
        Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.Attachments(
            GenerationId,
            [Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment)],
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.RepairSpecification]));

    private static CaseReportArtifactRecord Artifact(
        CaseReportArtifactKind kind,
        StaffMailAttachment attachment,
        CaseReportArtifactStatus status = CaseReportArtifactStatus.Confirmed) => new(
        Guid.NewGuid(), GenerationId, kind, status, "artifact-1",
        attachment.DocumentId, attachment.VersionId, attachment.Sha256,
        attachment.ContentLength, attachment.FileName, attachment.MediaType,
        "box-file", "box-version", null, null);

    private static ReportRecipientSuggestions Suggestions(
        IReadOnlyList<string> additionalAddresses,
        bool includeOriginalInstructionSender = false,
        string? originalInstructionSender = null) => new(
        "QDOS26001",
        PrincipalReportRecipientSettings.Normalize(
            includeOriginalInstructionSender, additionalAddresses),
        originalInstructionSender);
}
