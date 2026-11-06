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

    /// <summary>
    /// A one-off Case's reference is its registration, printed once
    /// (operator, 1 November 2026; R580); a repeat's reference differs from
    /// the registration, so both print.
    /// </summary>
    [Theory]
    [InlineData("AB12CDE", "AB12CDE Repairable report")]
    [InlineData("ab12cde", "ab12cde Repairable report")]
    [InlineData("AB12CDE.2", "AB12CDE.2 AB12CDE Repairable report")]
    public void AOneOffNamesItsReportWithTheRegistrationOnce(string reference, string expected) =>
        Assert.Equal(
            expected,
            CaseReportDeliveryNaming.ReportName(reference, "AB12CDE", "Repairable", 0));

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
    /// before, so a template's "supersedes" line is left out on a first send
    /// and names the date on a re-issue.
    /// </summary>
    [Fact]
    public void TheSupersedesLineNamesTheEarlierReportOnlyOnAReIssue()
    {
        const string body = SupersedesBody;

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
            EmailTemplates.Render(SupersedesBody, values),
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
    public void TheReportIsRenamedAndTheCompanionsKeepTheirCustodyNamesUnlessStaffNameThem()
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

    /// <summary>
    /// Operator, 4 November 2026 (v48 item E): staff may name each of the
    /// generation's documents for the send; a kind not named keeps its policy
    /// name, and a filed estimate past the documents keeps its filed name.
    /// </summary>
    [Fact]
    public void StaffNamesReachTheDocumentsTheyNameAndAFiledEstimateKeepsItsName()
    {
        var estimate = new StaffMailAttachment(
            Guid.NewGuid(), Guid.NewGuid(), new string('d', 64), 40, "Audatex 123.pdf", "application/pdf");
        var named = CaseReportDeliveryNaming.Named(
            [ReportAttachment, FeeAttachment, ImageAttachment, estimate],
            "QDOS26001 PK12TMZ Repairable report",
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote, CaseReportArtifactKind.ImagePack],
            new Dictionary<CaseReportArtifactKind, string>
            {
                [CaseReportArtifactKind.AssessmentReport] = " Our report.pdf ",
                [CaseReportArtifactKind.ImagePack] = "Photos.pdf",
                [CaseReportArtifactKind.RepairSpecification] = "Not attached.pdf",
            });

        Assert.Equal(
            ["Our report.pdf", FeeAttachment.FileName, "Photos.pdf", "Audatex 123.pdf"],
            named.Select(attachment => attachment.FileName).ToArray());
        Assert.Equal(ImageAttachment with { FileName = "Photos.pdf" }, named[2]);
        Assert.Equal(estimate, named[3]);
    }

    [Theory]
    [InlineData("report")]
    [InlineData(".pdf")]
    [InlineData("  .pdf")]
    [InlineData("report.PDF")]
    public void AStaffNameMustEndInPdfWithSomethingBeforeIt(string name)
    {
        Assert.Throws<ArgumentException>(() => CaseReportDeliveryNaming.StaffName(name));
        Assert.Throws<ArgumentException>(() => CaseReportDeliveryNaming.Named(
            [ReportAttachment], "QDOS26001 PK12TMZ Repairable report",
            [CaseReportArtifactKind.AssessmentReport],
            new Dictionary<CaseReportArtifactKind, string> { [CaseReportArtifactKind.AssessmentReport] = name }));
    }

    /// <summary>
    /// Operator, 4 November 2026 (v48 item C): the subject staff edited is
    /// sent, trimmed; left blank, the derived one; past 500 characters, refused.
    /// </summary>
    [Fact]
    public void TheSubjectIsStaffsWhenGivenElseTheDerivedOneAndBounded()
    {
        Assert.Equal("Re: Claim", CaseReportDeliveryPolicy.Subject(null, "Re: Claim"));
        Assert.Equal("Re: Claim", CaseReportDeliveryPolicy.Subject("  ", "Re: Claim"));
        Assert.Equal("Our report", CaseReportDeliveryPolicy.Subject(" Our report \r\n", "Re: Claim"));
        Assert.Equal(500, CaseReportDeliveryPolicy.MaximumSubjectLength);
        var longest = new string('s', CaseReportDeliveryPolicy.MaximumSubjectLength);
        Assert.Equal(longest, CaseReportDeliveryPolicy.Subject(longest, "Re: Claim"));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseReportDeliveryPolicy.Subject(longest + "s", "Re: Claim"));
    }

    [Fact]
    public void TheAddressBookOffersTheCasesOwnAddressesAndThePrincipals()
    {
        var book = CaseReportDeliveryPolicy.AddressBook(Suggestions(
            PrincipalReportSendingRules.Default with
            {
                SendTo = ["claims@principal.example"],
                Cc = ["claims@principal.example", "reports@principal.example", "Copy@Insurer.example"]
            },
            originalInstructionSender: "handler@insurer.example",
            instructionCc: ["copy@insurer.example"]));

        Assert.Equal(
            ["handler@insurer.example", "copy@insurer.example", "claims@principal.example", "reports@principal.example"],
            book.Select(candidate => candidate.Address));
        Assert.Equal(
            ["This case", "This case", "Principal", "Principal"],
            book.Select(candidate => candidate.Source));
    }

    /// <summary>
    /// A Case opened from an uploaded e-mail holds no mailbox instruction,
    /// and its original sender is still offered.
    /// </summary>
    [Fact]
    public void TheAddressBookOffersTheUploadedInstructionsSender()
    {
        var book = CaseReportDeliveryPolicy.AddressBook(
            Suggestions(PrincipalReportSendingRules.Default, originalInstructionSender: null)
                with { OriginalSender = "uploaded@insurer.example" });

        var candidate = Assert.Single(book);
        Assert.Equal("uploaded@insurer.example", candidate.Address);
        Assert.Equal("This case", candidate.Source);
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

    private static readonly PrincipalReportNames PchNames = new(
        "{REG} Initial", "{REG} Supplementary", "{REG}_{CASE REF}_AUDIT", null);

    /// <summary>
    /// The Principal's names (R740): the Inspection report's and the Audit
    /// report's, each its First on a first send and its Re-send on every later
    /// one; a Re-send left empty is the standard name, dots included.
    /// </summary>
    [Fact]
    public void APrincipalsNamesNameEachReportForItsSends()
    {
        Assert.Equal(
            "PK12TMZ Initial",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", 0, PchNames.For(auditReport: false, resend: false)));
        // A re-send carries its own name, not the dots of the standard name.
        Assert.Equal(
            "PK12TMZ Supplementary",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", 3, PchNames.For(auditReport: false, resend: true)));
        // The operator's example: a QDOS Audit named {REG}_{CASE REF}_AUDIT.
        Assert.Equal(
            "AB12CDE_a.QDOS26101-p_AUDIT",
            CaseReportDeliveryNaming.ReportName("a.QDOS26101-p", "AB12CDE", "Total loss", 0, PchNames.For(auditReport: true, resend: false)));
        Assert.Equal(
            "a.QDOS26101-p AB12CDE Total loss report.",
            CaseReportDeliveryNaming.ReportName("a.QDOS26101-p", "AB12CDE", "Total loss", 1, PchNames.For(auditReport: true, resend: true)));
    }

    /// <summary>A standalone Audit's report and the Audit work's report take the Audit names; every other report the Inspection names.</summary>
    [Theory]
    [InlineData(CaseType.Inspection, CaseWorkKind.Primary, "PK12TMZ Initial")]
    [InlineData(CaseType.InspectionAndAudit, CaseWorkKind.Primary, "PK12TMZ Initial")]
    [InlineData(CaseType.InspectionAndAudit, CaseWorkKind.Audit, "PK12TMZ_QDOS26001_AUDIT")]
    [InlineData(CaseType.Audit, CaseWorkKind.Primary, "PK12TMZ_QDOS26001_AUDIT")]
    public void EachWorksReportTakesItsOwnName(CaseType caseType, CaseWorkKind workKind, string expected) =>
        Assert.Equal(
            expected,
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", 0, PchNames.For(caseType, workKind, 0)));

    [Fact]
    public void ANameFillsTheReferenceRegistrationAndOutcomeInAnyCaseAndClosesUpAMissingOne()
    {
        const string name = "{case ref} {Reg} {OUTCOME}";

        Assert.Equal(
            "QDOS26001 PK12TMZ Total loss",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Total loss", 0, name));
        Assert.Equal(
            "QDOS26001 Total loss",
            CaseReportDeliveryNaming.ReportName("QDOS26001", " ", "Total loss", 0, name));
        Assert.Equal(
            "QDOS26001 PK12TMZ",
            CaseReportDeliveryNaming.ReportName(" QDOS26001 ", "PK12TMZ", null, 0, name));
    }

    [Fact]
    public void ANameWhoseTokensAreAllEmptyIsTheStandardName() =>
        Assert.Equal(
            "QDOS26001 report",
            CaseReportDeliveryNaming.ReportName("QDOS26001", null, null, 0, "{REG} {OUTCOME}"));

    /// <summary>A character no file name holds becomes _, as custody files it.</summary>
    [Fact]
    public void ANameKeepsOnlyCharactersAFileNameCanHold() =>
        Assert.Equal(
            "PK12TMZ_ QDOS26001_ final_",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", null, 0, "{REG}/ {CASE REF}: final?"));

    [Fact]
    public void WithNoNameTheStandardNamingIsUnchanged() =>
        Assert.Equal(
            "QDOS26001 PK12TMZ Repairable report.",
            CaseReportDeliveryNaming.ReportName("QDOS26001", "PK12TMZ", "Repairable", 1, null));

    [Fact]
    public void TheDeliveryFactsSupplyTheGreeting()
    {
        var values = new CaseReportDeliveryFacts(
            "QDOS26001", "PK12TMZ", "Total loss", "Principal Ltd", CaseReportSendHistory.None,
            "afternoon").Values();

        Assert.Equal("afternoon", values[EmailTemplates.Greeting]);
        Assert.Equal(
            "Good afternoon,\n\nPlease see attached report and fee note.\n\nAny issues let us know.\n\nKind Regards",
            EmailTemplates.Render(EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseReportDelivery), values));
    }

    /// <summary>A template body that names the superseded report; the built-in body does not.</summary>
    private const string SupersedesBody =
        "Our reference: {case reference}\nThis report supersedes our report dated {superseded report date}.";

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

    private static ReportDispatchFacts Suggestions(
        PrincipalReportSendingRules rules,
        string? originalInstructionSender,
        IReadOnlyList<string>? instructionCc = null) => new(
        "QDOS26001",
        "PK12TMZ",
        "Principal Ltd",
        rules,
        originalInstructionSender is null
            ? null
            : new ReportInstructionMessage(
                Guid.NewGuid(), Guid.NewGuid(), "info@collisionengineers.example", "immutable-1", null, null,
                originalInstructionSender, instructionCc ?? [], "Instruction", SenderAddress: null));
}
