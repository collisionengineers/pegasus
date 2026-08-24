using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// An automatically created case used to be born with every completeness flag
/// false and a policy that demanded staff confirmation nobody would ever give,
/// so it could never leave Not ready however complete it was. QDOS26009 held
/// claimant, claim number, incident date, make, model and registration and
/// still read "details incomplete" (CASE-013).
/// </summary>
public sealed class AutomaticCaseReadinessTests
{
    private static readonly CaseWorkflowConfiguration RequiresEverything = new(
        RequireCompleteInstructionsBeforeEngineerAssignment: true,
        RequireCompleteImagesBeforeEngineerAssignment: true,
        RequireStaffInstructionReviewBeforeEngineerAssignment: true,
        RequireStaffImageReviewBeforeEngineerAssignment: true,
        PolicyKey: "case-workflow",
        PolicyVersion: 1);

    [Fact]
    public void AnAutomaticallyDefinitiveIntakeIsReadyWithoutStaffConfirmation() =>
        Assert.True(
            CaseCompletenessPolicy
                .Evaluate(Complete(staffConfirmed: false), RequiresEverything, automaticallyDefinitive: true)
                .SatisfiesPolicy);

    [Fact]
    public void StaffAcceptanceIsNotExemptFromTheStaffReviewRequirement() =>
        Assert.False(
            CaseCompletenessPolicy
                .Evaluate(Complete(staffConfirmed: false), RequiresEverything, automaticallyDefinitive: false)
                .SatisfiesPolicy);

    [Fact]
    public void TheWaiverCoversStaffReviewOnlyAndNotMissingEvidence() =>
        Assert.False(
            CaseCompletenessPolicy
                .Evaluate(
                    new CaseCompleteness(false, false, false, false),
                    RequiresEverything,
                    automaticallyDefinitive: true)
                .SatisfiesPolicy);

    [Fact]
    public void TheReadinessRuleAndTheAcceptancePolicyAgreeOnTheWaiver()
    {
        var completeness = Complete(staffConfirmed: false);

        Assert.Equal(
            completeness.IsReadyForReview(automaticallyDefinitive: true),
            CaseCompletenessPolicy
                .Evaluate(completeness, RequiresEverything, automaticallyDefinitive: true)
                .SatisfiesPolicy);
        Assert.Equal(
            completeness.IsReadyForReview(automaticallyDefinitive: false),
            CaseCompletenessPolicy
                .Evaluate(completeness, RequiresEverything, automaticallyDefinitive: false)
                .SatisfiesPolicy);
    }

    // CASE-021. The four tests above pin the policy; these pin the value fed
    // into it. The constant `true` meant an audit with an instruction, a report
    // and no photographs was born Review-ready while the EVA export refused the
    // same case for having no images.
    [Fact]
    public void AReceiptWithNoPhotographsIsNotImageComplete()
    {
        var completeness = AllocateIntake.AutomaticCompleteness(
            ReceiptWith(
                Asset("49378_1_LtrtoAuditEngin.pdf", "application/pdf", IntakeAssetKind.Attachment, 82_000),
                Asset("Bodyshopreport119508-V1.pdf", "application/pdf", IntakeAssetKind.Attachment, 240_000)));

        Assert.True(completeness.InstructionComplete);
        Assert.False(completeness.ImagesComplete);
        Assert.False(
            CaseCompletenessPolicy
                .Evaluate(completeness, RequiresEverything, automaticallyDefinitive: true)
                .SatisfiesPolicy);
    }

    [Fact]
    public void AReceiptCarryingAGenuinePhotographIsImageComplete()
    {
        var completeness = AllocateIntake.AutomaticCompleteness(
            ReceiptWith(
                Asset("damage-1.jpg", "image/jpeg", IntakeAssetKind.Attachment, 1_400_000, 4032, 3024)));

        Assert.True(completeness.ImagesComplete);
        Assert.True(
            CaseCompletenessPolicy
                .Evaluate(completeness, RequiresEverything, automaticallyDefinitive: true)
                .SatisfiesPolicy);
    }

    [Fact]
    public void ALetterheadBannerIsNotAPhotograph()
    {
        // The corpus shape from INTK-030: 1990x437, comfortably over any byte
        // floor, and a JPEG sibling at 2214x248. Only the side ratio catches
        // them, and this test exists so the readiness gate keeps agreeing with
        // the gallery and custody about what an image is.
        var completeness = AllocateIntake.AutomaticCompleteness(
            ReceiptWith(
                Asset("letterhead.png", "image/png", IntakeAssetKind.EmbeddedImage, 110_783, 1990, 437)));

        Assert.False(completeness.ImagesComplete);
    }

    private static IntakeAssetRecord Asset(
        string fileName,
        string mediaType,
        IntakeAssetKind kind,
        long contentLength,
        int? width = null,
        int? height = null) =>
        new(
            Guid.NewGuid(),
            $"outer message, attachment {fileName}",
            fileName,
            mediaType,
            kind,
            kind == IntakeAssetKind.Attachment
                ? IntakeAssetDisposition.Attachment
                : IntakeAssetDisposition.Embedded,
            contentLength,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fileName))),
            $"storage/{fileName}",
            null,
            null,
            width,
            height);

    private static IntakeReceipt ReceiptWith(params IntakeAssetRecord[] assets) =>
        new(
            Guid.NewGuid(),
            "instruction.eml",
            "message/rfc822",
            1024,
            Convert.ToHexString(SHA256.HashData([1, 2, 3])),
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "readiness-fixture"),
            Fixed,
            Fixed,
            IntakeDecision.CaseCreated,
            "A definitive instruction was identified and is eligible for case allocation.",
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
            assets);

    private static readonly DateTimeOffset Fixed =
        new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);

    private static CaseCompleteness Complete(bool staffConfirmed) =>
        new(InstructionComplete: true,
            ImagesComplete: true,
            InstructionConfirmedByStaff: staffConfirmed,
            ImagesConfirmedByStaff: staffConfirmed);
}
