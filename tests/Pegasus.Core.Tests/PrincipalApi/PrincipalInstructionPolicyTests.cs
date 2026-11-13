using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Core.Tests.PrincipalApi;

public sealed class PrincipalInstructionPolicyTests
{
    private const string Complete =
        """
        {
          "caseType": "inspection",
          "claimNumber": "12345/1",
          "claimant": { "name": "Alex  Mercer", "contactNumber": "07700 900000", "address": " 1 High Street \n  Leeds " },
          "vehicle": { "registration": "ab12 cde", "make": "Peugeot", "model": "RCZ GT THP 156", "mileage": 48210, "mileageUnit": "miles" },
          "incident": { "dateOfIncident": "2031-04-01", "circumstances": "Rear-ended at a junction." },
          "inspection": { "dateRequested": "2031-05-20", "location": "Repairer Ltd\nLeeds" },
          "repairer": { "name": "Example  Garage", "address": " 2 Example Road \n Leeds " },
          "claimSource": "Example Introducer",
          "vatStatus": "Yes",
          "notes": "Vehicle is at the repairer."
        }
        """;

    private static (PrincipalInstruction Instruction, IReadOnlyList<PrincipalSubmissionFile> Files) Parse(string body) =>
        PrincipalInstructionJson.Parse(Encoding.UTF8.GetBytes(body));

    [Fact]
    public void NormalisationIsTheCaseStoresOwn()
    {
        var draft = Parse(Complete).Instruction.Draft;

        // Whitespace collapsed, registration compacted and uppercased, the
        // claimant address keeping its lines and the inspection address on
        // one — the shapes CaseDataPolicy applies, so a value is not reshaped
        // again on its way onto the case.
        Assert.Equal("Alex Mercer", draft.ClaimantName);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal("1 High Street\nLeeds", draft.ClaimantAddress);
        Assert.Equal("Repairer Ltd Leeds", draft.InspectionAddress);
    }

    [Fact]
    public void AnInvalidRegistrationIsNamedByItsFieldPath()
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => Parse("""{"caseType":"inspection","vehicle":{"registration":"AB12/CDE"}}"""));

        Assert.Equal("vehicle.registration", error.Field);
    }

    [Fact]
    public void AnOverlongValueIsNamedByItsFieldPath()
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => Parse($$"""{"caseType":"inspection","claimNumber":"{{new string('9', CaseDataLimits.ClaimNumber + 1)}}"}"""));

        Assert.Equal("claimNumber", error.Field);
    }

    [Fact]
    public void TheDraftCarriesEveryDeclaredValueAndNoPrincipal()
    {
        var draft = Parse(Complete).Instruction.Draft;

        // The credential decides the Principal; processing stamps it.
        Assert.Null(draft.SuggestedPrincipalCode);
        Assert.Equal("Vehicle is at the repairer.", draft.Notes);
        Assert.Equal(new DateOnly(2031, 5, 20), draft.InspectionDate);
        Assert.Empty(InstructionDraftCompleteness.MissingFieldNames(draft));
    }

    /// <summary>
    /// The stored declaration is what a retry with the same key is compared
    /// against, so it must read back as exactly what was parsed.
    /// </summary>
    [Fact]
    public void AStoredDeclarationReadsBackEqual()
    {
        var instruction = Parse(Complete).Instruction;

        Assert.Equal(instruction, PrincipalInstructionJson.Deserialize(PrincipalInstructionJson.Serialize(instruction)));
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
        var instruction = Parse(Complete).Instruction;
        var declared = PrincipalInstructionPolicy
            .ReviewFields(instruction.Draft, instruction)
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
        var parsed = Parse(body);
        Assert.Empty(parsed.Files);
        Assert.Equal("12345/1", parsed.Instruction.Draft.ClaimNumber);
    }

    [Fact]
    public void MembersTheContractDoesNotNameAreIgnored()
    {
        // The retired instructionDate and principal, and the retired per-file
        // ordinal, mediaType and role, are ignored rather than refused.
        var (instruction, files) = Parse(
            """{"caseType":"inspection","principal":"OTHER","claimNumber":"12345/1","instructionDate":"2031-05-01","files":[{"ordinal":7,"fileName":"instruction.pdf","mediaType":"image/png","role":"originalreport","contentBase64":"JVBERi0="}]}""");

        var file = Assert.Single(files);
        Assert.Equal("application/pdf", file.MediaType);
        Assert.Equal(PrincipalInstructionPolicy.AssetSourceLabel(0), file.SourceLabel);
        Assert.DoesNotContain(
            PrincipalInstructionPolicy.ReviewFields(instruction.Draft, instruction),
            field => field.Name == "Instruction date");
    }

    [Fact]
    public void FilesAreTypedByExtensionAndTheOriginalReportComesLast()
    {
        var (_, files) = Parse(
            """{"caseType":"audit","originalReportVerdict":"total-loss","originalReport":{"fileName":"report.pdf","contentBase64":"JVBERi0="},"files":[{"fileName":"front.JPG","contentBase64":"/9j/"}]}""");

        Assert.Collection(
            files,
            image =>
            {
                Assert.Equal("files[0]", image.Field);
                Assert.Equal("image/jpeg", image.MediaType);
            },
            report =>
            {
                Assert.Equal("originalReport", report.Field);
                Assert.Equal(PrincipalInstructionPolicy.OriginalReportSourceLabel, report.SourceLabel);
                Assert.Equal("application/pdf", report.MediaType);
            });
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("folder/report.pdf")]
    public void AFileNameMustBeASupportedLeafName(string fileName)
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => Parse($$"""{"caseType":"inspection","files":[{"fileName":"{{fileName}}","contentBase64":"JVBERi0="}]}"""));

        Assert.Equal("files[0].fileName", error.Field);
    }

    [Fact]
    public void TheWireVocabularyMapsOntoTheDomainsOwnCaseTypes()
    {
        // The operator's own word for Inspection + Audit, matched in any case.
        Assert.Equal(CaseType.InspectionAndAudit, Parse("""{"caseType":"AuditReport"}""").Instruction.CaseType);
        Assert.Equal(CaseType.Triage, Parse("""{"caseType":"triage"}""").Instruction.CaseType);
        Assert.Equal(
            "caseType",
            Assert.Throws<PrincipalInstructionValidationException>(() => Parse("""{"caseType":"diminution"}""")).Field);
    }

    [Theory]
    [InlineData("""{"caseType":"audit"}""")]
    [InlineData("""{"caseType":"inspection","originalReportVerdict":"repairable"}""")]
    [InlineData("""{"caseType":"auditreport","originalReportVerdict":"repairable"}""")]
    public void OnlyAStandaloneAuditStatesAVerdict(string body)
    {
        // Inspection + Audit audits Collision Engineers' own report (FRD-01).
        var error = Assert.Throws<PrincipalInstructionValidationException>(() => Parse(body));

        Assert.Equal("originalReportVerdict", error.Field);
    }

    /// <summary>
    /// R1053: the repairer and claim source a Principal names travel as the
    /// review fields e-mail intake prints for them, each naming the
    /// declaration, so the one owner of each decides what the Case takes.
    /// </summary>
    [Fact]
    public void TheRepairerAndClaimSourceAreDeclaredReviewFields()
    {
        var instruction = Parse(Complete).Instruction;

        Assert.Equal("Example Garage", instruction.RepairerName);
        Assert.Equal("2 Example Road\nLeeds", instruction.RepairerAddress);
        Assert.Equal("Example Introducer", instruction.ClaimSource);
        var fields = PrincipalInstructionPolicy.ReviewFields(instruction.Draft, instruction);
        var repairer = EstablishedIntakeRepairer.NamedIn(fields);
        Assert.NotNull(repairer);
        Assert.Equal("Example Garage", repairer.SuggestedValue);
        Assert.Equal(IntakeEvidenceSource.PrincipalDeclaration, Assert.Single(repairer.Candidates).Source);
        var claimSource = EstablishedIntakeClaimSource.NamedIn(fields);
        Assert.NotNull(claimSource);
        Assert.Equal("Example Introducer", claimSource.SuggestedValue);
        Assert.Contains(
            fields,
            field => field.ToCaseDataFieldName() == CaseDataFieldNames.RepairerAddress
                && field.SuggestedValue == "2 Example Road\nLeeds");
    }

    [Theory]
    [InlineData("""{"caseType":"inspection","repairer":{"name":"   "},"claimSource":""}""")]
    [InlineData("""{"caseType":"inspection"}""")]
    public void AnUnnamedRepairerOrClaimSourceAddsNoReviewField(string body)
    {
        var instruction = Parse(body).Instruction;
        var fields = PrincipalInstructionPolicy.ReviewFields(instruction.Draft, instruction);

        Assert.DoesNotContain(fields, field => field.Name is PrincipalInstructionPolicy.RepairerNameField
            or PrincipalInstructionPolicy.RepairerAddressField
            or EstablishedIntakeClaimSource.ClaimSourceField);
    }

    [Fact]
    public void AnOverlongRepairerOrClaimSourceIsNamedByItsFieldPath()
    {
        var name = new string('x', ContactDirectoryPolicy.MaximumNameLength + 1);

        Assert.Equal(
            "repairer.name",
            Assert.Throws<PrincipalInstructionValidationException>(
                () => Parse($$"""{"caseType":"inspection","repairer":{"name":"{{name}}"}}""")).Field);
        Assert.Equal(
            "claimSource",
            Assert.Throws<PrincipalInstructionValidationException>(
                () => Parse($$"""{"caseType":"inspection","claimSource":"{{name}}"}""")).Field);
    }

    /// <summary>
    /// The odometer's two units, in the spellings its one owner reads, stored
    /// canonically; any other unit is refused rather than read as miles.
    /// </summary>
    [Theory]
    [InlineData("Miles", "miles")]
    [InlineData("km", "kilometres")]
    [InlineData("Kilometres", "kilometres")]
    public void AMileageUnitIsStoredInItsCanonicalSpelling(string sent, string stored)
    {
        var draft = Parse($$"""{"caseType":"inspection","vehicle":{"mileage":1000,"mileageUnit":"{{sent}}"}}""")
            .Instruction.Draft;

        Assert.Equal(stored, draft.VehicleMileageUnit);
    }

    [Fact]
    public void AnUnknownMileageUnitIsRefused()
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => Parse("""{"caseType":"inspection","vehicle":{"mileage":1000,"mileageUnit":"furlongs"}}"""));

        Assert.Equal("vehicle.mileageUnit", error.Field);
    }

    [Theory]
    [InlineData("inspection")]
    [InlineData("auditreport")]
    [InlineData("triage")]
    public void OnlyAStandaloneAuditCarriesAnOriginalReport(string caseType)
    {
        var error = Assert.Throws<PrincipalInstructionValidationException>(
            () => Parse($$"""{"caseType":"{{caseType}}","originalReport":{"fileName":"report.pdf","contentBase64":"JVBERi0="}}"""));

        Assert.Equal("originalReport", error.Field);
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
