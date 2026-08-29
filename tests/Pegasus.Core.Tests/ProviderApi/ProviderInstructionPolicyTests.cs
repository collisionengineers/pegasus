using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Core.Tests.ProviderApi;

public sealed class ProviderInstructionPolicyTests
{
    private static readonly DateOnly Received = new(2031, 5, 6);

    private static ProviderInstruction Complete(
        ProviderInstructionKind kind = ProviderInstructionKind.Inspection,
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
        var normalized = ProviderInstructionPolicy.Normalize(Complete());

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
        var error = Assert.Throws<ProviderInstructionValidationException>(
            () => ProviderInstructionPolicy.Normalize(Complete() with { VehicleRegistration = "AB12/CDE" }));

        Assert.Equal("vehicle.registration", error.Field);
    }

    [Fact]
    public void AnOverlongValueIsNamedByItsFieldPath()
    {
        var error = Assert.Throws<ProviderInstructionValidationException>(
            () => ProviderInstructionPolicy.Normalize(
                Complete() with { ClaimNumber = new string('9', ProviderInstructionPolicy.MaximumClaimNumberLength + 1) }));

        Assert.Equal("claimNumber", error.Field);
    }

    [Fact]
    public void TheDraftCarriesEveryDeclaredValueAndDatesItsOwnInstruction()
    {
        var draft = ProviderInstructionPolicy.ToDraft(
            ProviderInstructionPolicy.Normalize(Complete()),
            "qdos",
            Received);

        Assert.Equal("QDOS", draft.SuggestedPrincipalCode);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal("Vehicle is at the repairer.", draft.Notes);
        Assert.Equal(new DateOnly(2031, 5, 20), draft.InspectionDate);
        // No instruction date was stated: an instruction dates from when it was
        // given, and for an API submission that is when it arrived.
        Assert.Equal(Received, draft.InstructionDate);
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
            null, null, null, null, null, null, null, null, null, null, null);
        var required = InstructionDraftCompleteness.MissingFieldNames(empty);
        var declared = ProviderInstructionPolicy
            .ReviewFields(ProviderInstructionPolicy.ToDraft(
                ProviderInstructionPolicy.Normalize(Complete()),
                "QDOS",
                Received))
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(required, label => Assert.Contains(label, declared));
    }

    [Fact]
    public void TheWireVocabularyMapsOntoTheDomainsOwnCaseTypes()
    {
        Assert.Equal(CaseType.Inspection, ProviderInstructionKinds.ToCaseType(ProviderInstructionKind.Inspection));
        Assert.Equal(CaseType.Audit, ProviderInstructionKinds.ToCaseType(ProviderInstructionKind.Audit));
        // The operator's own word for Inspection + Audit.
        Assert.Equal(
            CaseType.InspectionAndAudit,
            ProviderInstructionKinds.ToCaseType(ProviderInstructionKind.AuditReport));
        // Triage is pre-case work and allocates no Case/PO.
        Assert.Null(ProviderInstructionKinds.ToCaseType(ProviderInstructionKind.Triage));

        Assert.Equal(ProviderInstructionKind.AuditReport, ProviderInstructionKinds.Parse("AuditReport"));
        Assert.Throws<ArgumentException>(() => ProviderInstructionKinds.Parse("diminution"));
    }

    [Fact]
    public void OnlyAStandaloneAuditCarriesAnIncomingReport()
    {
        Assert.True(ProviderInstructionKinds.RequiresOriginalReport(ProviderInstructionKind.Audit));
        // Inspection + Audit audits Collision Engineers' own report (FRD-01).
        Assert.False(ProviderInstructionKinds.RequiresOriginalReport(ProviderInstructionKind.AuditReport));
        Assert.False(ProviderInstructionKinds.RequiresOriginalReport(ProviderInstructionKind.Inspection));
    }

    [Fact]
    public void ADeclaredTriageCarriesTheEvidenceTheTriageGateReads()
    {
        var evidence = ProviderInstructionPolicy.TriageEvidence();

        // CreateTriageIfQualifyingAsync reads exactly one Strong
        // AcceptedTriageMatch with a matcher key and a positive version.
        Assert.Equal(IntakeEvidenceFinding.AcceptedTriageMatch, evidence.Finding);
        Assert.Equal(IntakeEvidenceStrength.Strong, evidence.Strength);
        Assert.Equal(IntakeEvidenceSource.ProviderDeclaration, evidence.Source);
        Assert.False(string.IsNullOrWhiteSpace(evidence.MatcherKey));
        Assert.True(evidence.MatcherVersion > 0);
    }
}
