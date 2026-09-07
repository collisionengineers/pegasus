using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Pch;

/// <summary>
/// The PCH instruction profile against the shapes its five recorded originals
/// carry. The fixtures are transcribed from those originals' derived text: a
/// tab-separated label/value form with an indented address block, a footer
/// that names two firms, and rows for a driver, an insurer and a hire company
/// that are three parties and not the claimant.
/// </summary>
public sealed class PchInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly EstablishedPrincipalContext PchContext =
        new("PCH", PchInstructionExtractionPolicy.DocumentProfileKeyValue, 1);

    /// <summary>
    /// The audit-request form, in the shape the originals print it. Tabs
    /// between label and value, and the address as indented continuation rows.
    /// </summary>
    private const string AuditInstruction =
        "To:\tCollision Engineers Ltd \n"
        + "Date:\t6th May 2026 \n"
        + "From:\tHannah Hammill\n"
        + "\n"
        + "URGENT NEW INSTRUCTION (Connexus Audit Report)\n"
        + "\n"
        + "Claim Number:\t573942 \n"
        + "Incident date:\t31/03/2026\n"
        + "\n"
        + "Driver:\tMiss Carolann Hughes\n"
        + "\n"
        + "Policyholder Name:\tMrs Adam Bielecka \n"
        + "Policyholder VAT Status:\tisn't VAT registered\n"
        + "Address:\t5 Mornington Road\n"
        + "\tAshford\n"
        + "\tMiddlesex\n"
        + "\tTW15 1NP\n"
        + "\n"
        + "Tel Home:\t07860882699\n"
        + "Tel Mobile:\t07415527323\n"
        + "\n"
        + "Vehicle Make:\tVOLVO Xc90 r-design t8 phev awd\n"
        + "Engine size:\t1,969.00\n"
        + "Registration No:\tVN20XFC\n"
        + "Mileage:\t\n"
        + "\n"
        + "Vehicle Driveable:\tNo\n"
        + "Area of damage:\tRear\n"
        + "Description of damage:\tRear bumper smashed partially knocked off at bottom.\n"
        + "Nature of Damage:\tNon-Fault\n"
        + "Pre Existing Damage:\t None that we are aware of \n"
        + "Incident Circumstances:\tOur client has been stationary in traffic.\n"
        + "Agreed Value:\t0.00\n"
        + "Current Vehicle Location:\t\n"
        + "\n"
        + "Insurer Policy No:\tMRPC0103479703-LS\n"
        + "Insurer Claim No:\t \n"
        + "\n"
        + "Hire Supplied:\tYes\n"
        + "Hire Company:\tConnexus Vehicle Solutions\n"
        + "Hire Out Date:\t01/04/2026\n"
        + "\n"
        + "IMPORTANT NOTES\n"
        + "\n"
        + "Please confirm by return receipt of instruction. Arrange inspection within "
        + "24 hours and provide full report within 48 hours\n"
        + "\n"
        + "Performance Car Hire, 1210 Centre Park Square, Centre Park, Warrington, WA1 1RU\n"
        + "VAT Reg No. GB706924625";

    [Fact]
    public void TheAuditInstructionYieldsTheClaimantsIdentityAndItsOwnReference()
    {
        var result = Extract(AuditInstruction);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("PCH", draft.SuggestedPrincipalCode);
        Assert.Equal("Mrs Adam Bielecka", draft.ClaimantName);
        Assert.Equal("573942", draft.ClaimNumber);
        Assert.Equal("VN20XFC", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2026, 3, 31), draft.DateOfIncident);
        Assert.Equal(new DateOnly(2026, 5, 6), draft.InstructionDate);
        Assert.Equal("Our client has been stationary in traffic.", draft.AccidentCircumstances);
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
    }

    [Fact]
    public void TheDriverIsItsOwnRoleAndNeverTheClaimant()
    {
        // One recorded original names a driver who is a different person from
        // the policyholder, and three name the same person. Either way the
        // rows are two roles, and the driver is not a second claimant.
        var result = Extract(AuditInstruction);

        Assert.Equal("Mrs Adam Bielecka", Field(result, "Claimant name").SuggestedValue);
        Assert.Equal("Miss Carolann Hughes", Field(result, "Driver name").SuggestedValue);

        var roles = new PchInstructionExtractionPolicy().FieldRoles;
        Assert.Equal("claimant", roles["Claimant name"].PartyRole);
        Assert.Equal("driver", roles["Driver name"].PartyRole);
    }

    [Fact]
    public void ThePrincipalsClaimNumberIsNotTheInsurersPolicyOrClaimNumber()
    {
        var result = Extract(AuditInstruction);

        Assert.Equal("573942", Field(result, "Claim number").SuggestedValue);
        Assert.Equal(
            "MRPC0103479703-LS",
            Field(result, "Insurer policy number").SuggestedValue);
        // The insurer's claim row is blank in this original: an explicit
        // absence, not a value borrowed from the row above it.
        Assert.Null(Field(result, "Insurer claim number").SuggestedValue);

        var roles = new PchInstructionExtractionPolicy().FieldRoles;
        Assert.Equal("principal", roles["Claim number"].ReferenceRole);
        Assert.Equal("insurer-policy", roles["Insurer policy number"].ReferenceRole);
        Assert.Equal("insurer", roles["Insurer policy number"].PartyRole);
    }

    [Fact]
    public void TheHireCompanyAndHireOutDateAreTheHireProvidersAndNotTheIncidents()
    {
        var result = Extract(AuditInstruction);

        Assert.Equal(
            "Connexus Vehicle Solutions",
            Field(result, "Hire company").SuggestedValue);
        Assert.Equal("01/04/2026", Field(result, "Hire out date").SuggestedValue);
        // The incident and instruction dates are the document's own, and the
        // hire-out date has displaced neither.
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(new DateOnly(2026, 3, 31), draft.DateOfIncident);
        Assert.Equal(new DateOnly(2026, 5, 6), draft.InstructionDate);
        Assert.Equal(
            "hire-provider",
            new PchInstructionExtractionPolicy().FieldRoles["Hire company"].PartyRole);
    }

    [Fact]
    public void TheCombinedVehicleTextIsKeptWholeAndNoModelIsInvented()
    {
        // The template labels one combined string "Vehicle Make:" and prints
        // no Model row at all. Splitting it would be the guess the invariants
        // forbid; inventing a model would be worse.
        var result = Extract(AuditInstruction);

        Assert.Equal(
            "VOLVO Xc90 r-design t8 phev awd",
            Field(result, "Vehicle make").SuggestedValue);
        Assert.Null(Field(result, "Vehicle model").SuggestedValue);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).VehicleModel);
    }

    [Fact]
    public void TheIndentedAddressBlockIsReadWholeAndIsNotTheSupplierFooter()
    {
        var result = Extract(AuditInstruction);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(
            "5 Mornington Road, Ashford, Middlesex, TW15 1NP",
            draft.ClaimantAddress);
        // The footer is a real address, and it is the supplier's.
        Assert.DoesNotContain("Centre Park", draft.ClaimantAddress!, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExplicitVatStatusIsReadAndNothingIsDefaulted()
    {
        Assert.Equal(
            "isn't VAT registered",
            Field(Extract(AuditInstruction), "VAT status").SuggestedValue);
        Assert.Equal(
            "is VAT registered",
            Field(
                Extract("Vehicle Make:\tToyota Proace\nRegistration No:\tBD22GZW\n"
                    + "Policyholder VAT Status:\tis VAT registered"),
                "VAT status").SuggestedValue);
        // No row, no value. The legacy hard-coded "No" is not imported.
        Assert.Null(
            Field(
                Extract("Vehicle Make:\tToyota Proace\nRegistration No:\tBD22GZW"),
                "VAT status").SuggestedValue);
    }

    [Fact]
    public void AnAbsentInstructionDateIsNotTodaysDate()
    {
        var result = Extract("Policyholder Name:\tMr Junior Cover\nRegistration No:\tJR07CVR");

        var field = Field(result, "Instruction date");
        Assert.Null(field.SuggestedValue);
        Assert.False(field.IsDefaulted);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).InstructionDate);
    }

    [Fact]
    public void AVehicleLocationStatusIsNotAnInspectionAddress()
    {
        // One recorded original prints "in use" in the location row. It says
        // whether the car is being driven, not where it is, and no inspection
        // address is better than that one.
        var status = Extract(
            "Registration No:\tBD69NJY\nCurrent Vehicle Location:\tin use");
        Assert.Null(Field(status, "Inspection address").SuggestedValue);
        Assert.Null(Assert.IsType<InstructionDraft>(status.InstructionDraft).InspectionAddress);

        var address = Extract(
            "Registration No:\tBD69NJY\n"
            + "Current Vehicle Location:\tUnit 4 Kestrel Way, Sheffield, S9 1TH");
        Assert.Equal(
            "Unit 4 Kestrel Way, Sheffield, S9 1TH",
            Field(address, "Inspection address").SuggestedValue);
    }

    [Fact]
    public void TheReportDeadlineIsADeadlineAndNeverAnInspectionDate()
    {
        var result = Extract(AuditInstruction);

        Assert.Contains(
            "provide full report within 48 hours",
            Field(result, "Report deadline").SuggestedValue);
        Assert.Equal(
            "deadline",
            new PchInstructionExtractionPolicy().FieldRoles["Report deadline"].PartyRole);
        // Nothing in this template says an inspection was appointed or
        // completed, so there is no inspection date to record.
        Assert.Null(Field(result, "Inspection date").SuggestedValue);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).InspectionDate);
    }

    [Fact]
    public void TheInstructionHeadingIsRequestedWorkAndNotAnOutcome()
    {
        Assert.Equal(
            "URGENT NEW INSTRUCTION (Connexus Audit Report)",
            Field(Extract(AuditInstruction), "Requested work").SuggestedValue);
    }

    [Fact]
    public void DamageDriveabilityAndPreExistingDamageStayOutOfTheCircumstances()
    {
        var result = Extract(AuditInstruction);

        Assert.Equal("Rear", Field(result, "Damage area").SuggestedValue);
        Assert.Equal(
            "Rear bumper smashed partially knocked off at bottom.",
            Field(result, "Damage description").SuggestedValue);
        Assert.Equal("Non-Fault", Field(result, "Damage nature").SuggestedValue);
        Assert.Equal(
            "None that we are aware of",
            Field(result, "Pre-existing damage").SuggestedValue);
        Assert.Equal("No", Field(result, "Vehicle status").SuggestedValue);
        Assert.Equal(
            "Our client has been stationary in traffic.",
            Field(result, "Accident circumstances").SuggestedValue);
    }

    [Fact]
    public void NoRepairerOrStorageBlockMeansNoRepairerAndNotTheFooter()
    {
        // No recorded original carries either block. The fields stay
        // unavailable; the supplier footer is not pressed into service as one.
        var result = Extract(AuditInstruction);

        Assert.Null(Field(result, "Repairer name").SuggestedValue);
        Assert.Null(Field(result, "Storage location").SuggestedValue);
    }

    [Fact]
    public void AnIncompleteOrUnreadableSourceCannotCrossThePolicyBoundary()
    {
        var incomplete = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, "PCH 01.DOC", AuditInstruction)],
            [],
            [],
            RequiresOcr: false,
            IsIncomplete: true);

        Assert.Throws<ArgumentException>(() =>
            new PchInstructionExtractionPolicy().Extract(incomplete, ProcessedAtUtc, PchContext));
    }

    [Fact]
    public void AnotherPrincipalsEstablishedContextIsRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            new PchInstructionExtractionPolicy().Extract(
                Readable(AuditInstruction),
                ProcessedAtUtc,
                new("QDOS", "qdos_mail_route", 1)));
    }

    [Fact]
    public void TheProfileDeclaresTheTwoRecordedVariantsAndNoOthers()
    {
        var policy = new PchInstructionExtractionPolicy();

        Assert.Equal(
            [
                PchInstructionExtractionPolicy.PerformanceVariantKey,
                PchInstructionExtractionPolicy.LawshieldVariantKey
            ],
            policy.Variants.Select(variant => variant.Key));
        Assert.Equal(
            InstructionDocumentSignature.InstructionRole,
            policy.Signature.DocumentRole);
        Assert.All(policy.Variants, variant =>
            InstructionDocumentSignature.Validate(variant.Signature));
    }

    private static InstructionExtractionResult Extract(string text) =>
        new PchInstructionExtractionPolicy().Extract(Readable(text), ProcessedAtUtc, PchContext);

    private static IntakeSourceReadResult Readable(string text) =>
        new(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, "PCH 01.DOC", text)],
            [],
            [],
            RequiresOcr: false);

    private static InstructionReviewField Field(
        InstructionExtractionResult result,
        string name) =>
        Assert.Single(result.Fields, field => field.Name == name);
}
