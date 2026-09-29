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
        Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 120, "AssessmentReport.pdf", "application/pdf");

    private static readonly StaffMailAttachment FeeAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('b', 64), 60, "CE_100_fee_note.pdf", "application/pdf");

    private static readonly StaffMailAttachment ImageAttachment = new(
        Guid.NewGuid(), Guid.NewGuid(), new string('c', 64), 90, "CE_100_images.pdf", "application/pdf");

    [Fact]
    public void TheFirstReportIsNamedForTheCase() =>
        Assert.Equal(
            "QDOS26001 PK12TMZ Repairable report",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", 0));

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

    /// <summary>
    /// The template's values come from the delivery's facts: the superseded
    /// report date has a value only when a report of this Case was sent
    /// before, so the built-in body's "supersedes" line is left out on a
    /// first send and names the date on a re-issue.
    /// </summary>
    [Fact]
    public void TheSupersedesLineNamesTheEarlierReportOnlyOnAReIssue()
    {
        var body = EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseReportDelivery);

        var first = EmailTemplates.Render(
            body, Facts(CaseReportSendHistory.None).Values());
        var reissue = EmailTemplates.Render(
            body, Facts(new(1, new DateOnly(2026, 8, 19))).Values());

        Assert.DoesNotContain("supersedes", first, StringComparison.Ordinal);
        Assert.Contains(
            "This report supersedes our report dated 19 August 2026.",
            reissue,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A send recorded without the report date it superseded names no date
    /// nobody holds, so the line is left out.
    /// </summary>
    [Fact]
    public void ASendWithNoRecordedReportDateLeavesTheSupersedesLineOut()
    {
        var values = Facts(new(2, null)).Values();

        Assert.Null(values[EmailTemplates.SupersededReportDate]);
        Assert.DoesNotContain(
            "supersedes",
            EmailTemplates.Render(EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseReportDelivery), values),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheCoveringMessageIsFrozenAsStaffReviewedItWithPlainLineEndings()
    {
        Assert.Equal("One\nTwo", CaseReportDeliveryPolicy.CoveringMessage("One\r\nTwo\r\n  "));

        var longest = new string('a', EmailTemplates.MaximumBodyLength);
        Assert.Equal(longest, CaseReportDeliveryPolicy.CoveringMessage(longest));
    }

    [Fact]
    public void ABlankOrTooLongCoveringMessageIsRefused()
    {
        Assert.Throws<ArgumentException>(() => CaseReportDeliveryPolicy.CoveringMessage(null));
        Assert.Throws<ArgumentException>(() => CaseReportDeliveryPolicy.CoveringMessage(" \r\n "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseReportDeliveryPolicy.CoveringMessage(
            new string('a', EmailTemplates.MaximumBodyLength + 1)));
    }

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

    [Fact]
    public void CompanionSelectionAlwaysIncludesTheAssessmentReport()
    {
        var artifacts = new[]
        {
            Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment),
            Artifact(CaseReportArtifactKind.ImagePack, ImageAttachment),
        };

        Assert.Equal(
            [ReportAttachment, ImageAttachment],
            CaseReportDeliveryPolicy.Attachments(
                GenerationId, artifacts, [CaseReportArtifactKind.ImagePack]));

        Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.Attachments(
            GenerationId,
            [Artifact(CaseReportArtifactKind.ImagePack, ImageAttachment)],
            [CaseReportArtifactKind.ImagePack]));
        Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.Attachments(
            GenerationId,
            [Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment),
             Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment)],
            [CaseReportArtifactKind.ImagePack]));
        Assert.Throws<InvalidOperationException>(() => CaseReportDeliveryPolicy.Attachments(
            GenerationId,
            [Artifact(CaseReportArtifactKind.AssessmentReport, ReportAttachment,
                CaseReportArtifactStatus.Pending),
             Artifact(CaseReportArtifactKind.ImagePack, ImageAttachment)],
            [CaseReportArtifactKind.ImagePack]));
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

    private static CaseReportDeliveryFacts Facts(CaseReportSendHistory history) => new(
        "QDOS26001", "PK12TMZ", "Total loss", "Principal Ltd", history);

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
