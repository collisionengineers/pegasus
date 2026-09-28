using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Core.Tests.PrincipalApi;

public sealed class PrincipalInstructionPolicyTests
{
    private static PrincipalInstruction Complete(
        PrincipalInstructionKind kind = PrincipalInstructionKind.Inspection,
        AuditAssessment? verdict = null) =>
        new(
            kind,
            verdict,
            ClaimNumber: "12345/1",
            ClaimantName: "Alex  Mercer",
            ClaimantContactNumber: "07700 900000",
            ClaimantAddress: " 1 High Street \n  Leeds ",
            FileHandlerName: "Sam Handler",
            FileHandlerEmailAddress: "sam@example.test",
            FileHandlerPhoneNumber: "0113 496 0000",
            VehicleRegistration: "ab12 cde",
            VehicleMake: "Peugeot",
            VehicleModel: "RCZ GT THP 156",
            VehicleMileage: 48210,
            VehicleMileageUnit: "miles",
            DateOfIncident: new(2031, 4, 1),
            AccidentCircumstances: "Rear-ended at a junction.",
            InspectionDateRequested: new(2031, 5, 20),
            InspectionAddress: "Repairer Ltd\nLeeds",
            VatStatus: "Yes",
            Notes: "Vehicle is at the repairer.");

    [Fact]
    public void NormalisationMatchesTheCaseStoreRatherThanInventingItsOwn()
    {
        var normalized = PrincipalInstructionPolicy.Normalize(Complete());

        // Whitespace collapsed, registration compacted and uppercased, address
        // line breaks kept — the same shapes CaseDataPolicy applies, so a value
        // is not reshaped again on its way onto the case.
        Assert.Equal("Alex Mercer", normalized.ClaimantName);
        Assert.Equal("AB12CDE", normalized.VehicleRegistration);
        Assert.Equal("1 High Street\nLeeds", normalized.ClaimantAddress);
    }

    [Fact]
    public void AnInvalidRegistrationIsNamedByItsFieldPath()
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => PrincipalInstructionPolicy.Normalize(Complete() with { VehicleRegistration = "AB12/CDE" }));

        Assert.Equal("vehicle.registration", error.Field);
    }

    [Fact]
    public void AnOverlongValueIsNamedByItsFieldPath()
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => PrincipalInstructionPolicy.Normalize(
                Complete() with { ClaimNumber = new string('9', PrincipalInstructionPolicy.MaximumClaimNumberLength + 1) }));

        Assert.Equal("claimNumber", error.Field);
    }

    [Fact]
    public void TheDraftCarriesEveryDeclaredValue()
    {
        var draft = PrincipalInstructionPolicy.ToDraft(
            PrincipalInstructionPolicy.Normalize(Complete()),
            "qdos");

        Assert.Equal("QDOS", draft.SuggestedPrincipalCode);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal("Vehicle is at the repairer.", draft.Notes);
        Assert.Equal(new DateOnly(2031, 5, 20), draft.InspectionDate);
        Assert.Empty(InstructionDraftCompleteness.MissingFieldNames(draft));
    }

    /// <summary>
    /// The intake field labels are the operator's words and already have three
    /// users — the extraction policy that produces them, the completeness rule
    /// that reports them, and the case snapshot that looks values up by them.
    /// A declaration is a fourth, and it is the one that can silently write a
    /// draft value the snapshot then refuses for want of provenance. This pins
    /// the overlap so a renamed label fails here rather than in allocation.
    /// </summary>
    [Fact]
    public void EveryRequiredFieldLabelHasAMatchingDeclaredReviewField()
    {
        var empty = new InstructionDraft(
            null, null, null, null, null, null, null, null, null, null);
        var required = InstructionDraftCompleteness.MissingFieldNames(empty);
        var declared = PrincipalInstructionPolicy
            .ReviewFields(PrincipalInstructionPolicy.ToDraft(
                PrincipalInstructionPolicy.Normalize(Complete()),
                "QDOS"))
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(required, label => Assert.Contains(label, declared));
    }

    [Theory]
    [InlineData("""{"caseType":"inspection","claimNumber":"12345/1","files":[]}""")]
    [InlineData("""{"caseType":"audit","originalReportVerdict":"repairable","claimNumber":"12345/1"}""")]
    public void AnInstructionParsesWithNoFiles(string body)
    {
        // Files are optional (operator, 2026-09-28): an empty or absent list is
        // a complete declaration, read the same way by the endpoint and intake.
        var (instruction, files) = PrincipalInstructionJson.Parse(System.Text.Encoding.UTF8.GetBytes(body));
        Assert.Empty(files);
        Assert.Equal("12345/1", PrincipalInstructionPolicy.Normalize(instruction).ClaimNumber);
    }

    [Fact]
    public void AnInstructionDateInTheBodyIsNotAContractMemberAndIsIgnored()
    {
        var (instruction, files) = PrincipalInstructionJson.Parse(System.Text.Encoding.UTF8.GetBytes(
            """{"caseType":"inspection","claimNumber":"12345/1","instructionDate":"2031-05-01","files":[{"fileName":"instruction.pdf","mediaType":"application/pdf","contentBase64":"JVBERi0="}]}"""));
        Assert.Single(files);
        Assert.DoesNotContain(
            PrincipalInstructionPolicy.ReviewFields(PrincipalInstructionPolicy.ToDraft(PrincipalInstructionPolicy.Normalize(instruction), "QDOS")),
            field => field.Name == "Instruction date");
        // A declaration stored before the member was retired still reads.
        Assert.NotNull(PrincipalInstructionJson.Deserialize("""{"kind":"Inspection","instructionDate":"2031-05-01"}"""));
    }

    [Fact]
    public void TheWireVocabularyMapsOntoTheDomainsOwnCaseTypes()
    {
        Assert.Equal(CaseType.Inspection, PrincipalInstructionKinds.ToCaseType(PrincipalInstructionKind.Inspection));
        Assert.Equal(CaseType.Audit, PrincipalInstructionKinds.ToCaseType(PrincipalInstructionKind.Audit));
        // The operator's own word for Inspection + Audit.
        Assert.Equal(
            CaseType.InspectionAndAudit,
            PrincipalInstructionKinds.ToCaseType(PrincipalInstructionKind.AuditReport));
        // Triage is pre-case work and allocates no Case/PO.
        Assert.Null(PrincipalInstructionKinds.ToCaseType(PrincipalInstructionKind.Triage));

        Assert.Equal(PrincipalInstructionKind.AuditReport, PrincipalInstructionKinds.Parse("AuditReport"));
        Assert.Throws<ArgumentException>(() => PrincipalInstructionKinds.Parse("diminution"));
    }

    [Fact]
    public void OnlyAStandaloneAuditCarriesAnIncomingReport()
    {
        Assert.True(PrincipalInstructionKinds.RequiresOriginalReport(PrincipalInstructionKind.Audit));
        // Inspection + Audit audits Collision Engineers' own report (FRD-01).
        Assert.False(PrincipalInstructionKinds.RequiresOriginalReport(PrincipalInstructionKind.AuditReport));
        Assert.False(PrincipalInstructionKinds.RequiresOriginalReport(PrincipalInstructionKind.Inspection));
    }

    [Fact]
    public void ADeclaredTriageCarriesTheEvidenceTheTriageGateReads()
    {
        var evidence = PrincipalInstructionPolicy.TriageEvidence();

        // CreateTriageIfQualifyingAsync reads exactly one Strong
        // AcceptedTriageMatch with a matcher key and a positive version.
        Assert.Equal(IntakeEvidenceFinding.AcceptedTriageMatch, evidence.Finding);
        Assert.Equal(IntakeEvidenceStrength.Strong, evidence.Strength);
        Assert.Equal(IntakeEvidenceSource.PrincipalDeclaration, evidence.Source);
        Assert.False(string.IsNullOrWhiteSpace(evidence.MatcherKey));
        Assert.True(evidence.MatcherVersion > 0);
    }
}
