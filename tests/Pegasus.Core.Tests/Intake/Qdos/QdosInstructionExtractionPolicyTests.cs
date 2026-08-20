using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly EstablishedPrincipalContext QdosContext =
        new("QDOS", QdosMailRoutePolicy.Key, QdosMailRoutePolicy.Version);

    [Fact]
    public void EstablishedQdosPrincipalExtractsFieldsWithoutContentMarker()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Please process the attached instruction."),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Claimant Name: Review Claimant\nClaim Number: Q-423")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("QDOS", draft.SuggestedPrincipalCode);
        Assert.Equal("Review Claimant", draft.ClaimantName);
        Assert.Equal("Q-423", draft.ClaimNumber);
        Assert.DoesNotContain(result.Evidence, item =>
            item.Signal is "qdos-content-marker" or "qdos-transport-marker" or "instruction-structure");
        Assert.Contains(result.Evidence, item => item.Signal == "established-principal");
    }

    [Fact]
    public void EstablishedQdosPrincipalProducesReviewDraftWhenFieldsAreMissing()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction attachment",
                "No recognized instruction labels are present.")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        Assert.Equal("QDOS", Assert.IsType<InstructionDraft>(result.InstructionDraft).SuggestedPrincipalCode);
        Assert.Contains("Claimant name", result.MissingFields);
        Assert.Contains("Claim number", result.MissingFields);
    }

    [Fact]
    public void FlattenedMotTableRowsAreNeverOfferedAsMakeOrModel()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 7: bodyshop report, page 1",
                """
                Brake test results
                Make AUDI NSF : Footbrake : SATISFACTORY
                Model A4 OSR : Handbrake : SATISFACTORY
                """)),
            ProcessedAtUtc,
            QdosContext);

        var make = Assert.Single(result.Fields, field => field.Name == "Vehicle make");
        var model = Assert.Single(result.Fields, field => field.Name == "Vehicle model");
        Assert.Null(make.SuggestedValue);
        Assert.Empty(make.Candidates);
        Assert.Null(model.SuggestedValue);
        Assert.Empty(model.Candidates);
        Assert.Contains("Vehicle make", result.MissingFields);
        Assert.Contains("Vehicle model", result.MissingFields);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
    }

    [Fact]
    public void InstructionFieldsWinOverAppendedMotTableWithoutConflict()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Vehicle Make: Audi\nVehicle Model: A4"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: bodyshop report, page 1",
                    """
                    Brake test results
                    Make AUDI NSF : Footbrake : SATISFACTORY
                    Model A4 OSR : Handbrake : SATISFACTORY
                    """)),
            ProcessedAtUtc,
            QdosContext);

        var make = Assert.Single(result.Fields, field => field.Name == "Vehicle make");
        var model = Assert.Single(result.Fields, field => field.Name == "Vehicle model");
        Assert.False(make.HasConflict);
        Assert.False(model.HasConflict);
        Assert.Equal("Audi", make.SuggestedValue);
        Assert.Equal("A4", model.SuggestedValue);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Audi", draft.VehicleMake);
        Assert.Equal("A4", draft.VehicleModel);
    }

    [Fact]
    public void LabelledValueStopsAtColumnBoundary()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction attachment",
                "Vehicle Make: Audi | Colour Blue\nVehicle Model: A4 Avant  Fuel Diesel")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "Audi",
            Assert.Single(result.Fields, field => field.Name == "Vehicle make").SuggestedValue);
        Assert.Equal(
            "A4 Avant",
            Assert.Single(result.Fields, field => field.Name == "Vehicle model").SuggestedValue);
    }

    [Fact]
    public void MidLineLabelTokenAfterSingleSpaceIsNotALabel()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 3: MOT history, page 2",
                "The vehicle Make recorded at test time was unreadable")),
            ProcessedAtUtc,
            QdosContext);

        var make = Assert.Single(result.Fields, field => field.Name == "Vehicle make");
        Assert.Null(make.SuggestedValue);
        Assert.Empty(make.Candidates);
    }

    [Fact]
    public void RepeatedIdenticalValueAcrossFragmentsIsNotAConflict()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Claim Number: Q-777"),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Claim Number: Q-777")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Claim number");
        Assert.False(field.HasConflict);
        Assert.Equal("Q-777", field.SuggestedValue);
    }

    [Fact]
    public void DifferingValuesAcrossFragmentsPreferTheEarliestFragment()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Claimant Name: First Person"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: bodyshop report, page 1",
                    "Claimant Name: Second Person")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Claimant name");
        Assert.False(field.HasConflict);
        Assert.Equal("First Person", field.SuggestedValue);
        Assert.Equal(2, field.Candidates.Count);
        Assert.Equal(
            "First Person",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).ClaimantName);
    }

    [Fact]
    public void ParsingCandidateBeatsEarlierUnparsableCandidate()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Vehicle Mileage: unknown pending review"),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Vehicle Mileage: 42,000 miles")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Vehicle mileage");
        Assert.False(field.HasConflict);
        Assert.Equal("42,000 miles", field.SuggestedValue);
        Assert.Equal(2, field.Candidates.Count);
        Assert.Equal(
            42000L,
            Assert.IsType<InstructionDraft>(result.InstructionDraft).VehicleMileage);
    }

    [Fact]
    public void SameFragmentDistinctDatesRemainConflicting()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction attachment",
                "Date of Incident: 04/03/2031\nDate of Incident: 05/03/2031")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Date of incident");
        Assert.True(field.HasConflict);
        Assert.Null(field.SuggestedValue);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).DateOfIncident);
    }

    [Fact]
    public void SoleCurrentFormatRegistrationIsSuggestedWithoutALabel()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Please inspect the vehicle AU17 SEO at the address below.\nClaim Number: Q-901"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 2: photos summary, page 1",
                    "Photographs of AU17SEO showing rear damage.")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Vehicle registration");
        Assert.False(field.HasConflict);
        Assert.Equal("AU17 SEO", field.SuggestedValue);
        Assert.DoesNotContain("Vehicle registration", result.MissingFields);
        Assert.Equal(
            "AU17SEO",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).VehicleRegistration);
    }

    [Fact]
    public void MultipleDistinctUnlabelledRegistrationsStayAbsent()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction attachment",
                "Vehicle AU17 SEO collided with third party vehicle BD51 SMR.")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Vehicle registration");
        Assert.Null(field.SuggestedValue);
        Assert.Empty(field.Candidates);
        Assert.Contains("Vehicle registration", result.MissingFields);
    }

    [Theory]
    [InlineData("Registration Number: AB12 CDE")]
    [InlineData("Registration No: AB12 CDE")]
    [InlineData("Reg No: AB12 CDE")]
    [InlineData("Vehicle Reg: AB12 CDE")]
    public void RegistrationLabelSynonymsAreRecognised(string line)
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction attachment",
                line)),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "AB12 CDE",
            Assert.Single(result.Fields, item => item.Name == "Vehicle registration").SuggestedValue);
        Assert.Equal(
            "AB12CDE",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).VehicleRegistration);
    }

    [Fact]
    public void FlattenedLineWithTwoLabelledFieldsSplitsAtTheSecondLabel()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction attachment",
                "Vehicle Make: Audi Vehicle Model: A4 Avant")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "Audi",
            Assert.Single(result.Fields, item => item.Name == "Vehicle make").SuggestedValue);
        Assert.Equal(
            "A4 Avant",
            Assert.Single(result.Fields, item => item.Name == "Vehicle model").SuggestedValue);
    }

    [Fact]
    public void QdosTextDoesNotBecomePrincipalEvidence()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(IntakeEvidenceSource.EmailBody, "message body", "QDOS"),
                new(IntakeEvidenceSource.DocumentContent, "attachment", "QDOS instruction")),
            ProcessedAtUtc,
            QdosContext);

        Assert.DoesNotContain(result.Evidence, item =>
            item.Detail.Contains("identified", StringComparison.OrdinalIgnoreCase)
            || item.Signal.Contains("content-marker", StringComparison.Ordinal));
    }

    [Fact]
    public void DifferentEstablishedPrincipalIsRejected()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new QdosInstructionExtractionPolicy().Extract(
                Readable(),
                ProcessedAtUtc,
                new("OTHER", "other_route", 1)));

        Assert.Contains("not supported", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IncompleteResultCannotCrossPolicyBoundary()
    {
        var readResult = Readable() with { IsIncomplete = true };

        Assert.Throws<ArgumentException>(() =>
            new QdosInstructionExtractionPolicy().Extract(
                readResult,
                ProcessedAtUtc,
                QdosContext));
    }

    [Fact]
    public void PossessiveVehicleLineNeverFeedsTheClaimantLabel()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Our Client:  Mrs Caroline Reynolds\nOur Client's Vehicle: PEUGEOT RCZ GT THP 156\nRegistration:  L100 YDR\nDate of Accident: 3 July 2026")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mrs Caroline Reynolds", draft.ClaimantName);
        Assert.Equal("PEUGEOT", draft.VehicleMake);
        Assert.Equal("RCZ GT THP 156", draft.VehicleModel);
        Assert.Equal("L100YDR", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2026, 7, 3), draft.DateOfIncident);
        var claimant = Assert.Single(result.Fields, field => field.Name == "Claimant name");
        Assert.False(claimant.HasConflict);
    }

    [Fact]
    public void SubjectFactsFillFieldsTheBodyLacks()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            ReadableWithSubject(
                "RTA on 03_07_2026  Mrs Jane Smith (Our Ref SAB_46737_1, Vehicle L100 YDR)",
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Please see the attached instruction.")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mrs Jane Smith", draft.ClaimantName);
        Assert.Equal("SAB_46737_1", draft.ClaimNumber);
        Assert.Equal("L100YDR", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2026, 7, 3), draft.DateOfIncident);
    }

    [Fact]
    public void BodyStatementsBeatSubjectFacts()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            ReadableWithSubject(
                "Client Mr Subject Person (Our Ref SUBJ_1)",
                new IntakeContentFragment(
                    IntakeEvidenceSource.DocumentContent,
                    "instruction attachment",
                    "Claimant Name: Body Person\nClaim Number: BODY-1")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Body Person", draft.ClaimantName);
        Assert.Equal("BODY-1", draft.ClaimNumber);
    }

    [Fact]
    public void TwoWordMakesSplitTheVehicleDescriptionOnTheRightBoundary()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Our Client's Vehicle: LAND ROVER R ROVER EVOQUE SE LK17 NHT")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("LAND ROVER", draft.VehicleMake);
        Assert.Equal("R ROVER EVOQUE SE", draft.VehicleModel);
        Assert.Equal("LK17NHT", draft.VehicleRegistration);
    }

    [Fact]
    public void ExplicitVehicleFieldsBeatTheDescriptionDerivation()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Vehicle Make: AUDI\nOur Client's Vehicle: PEUGEOT RCZ")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("AUDI", draft.VehicleMake);
    }

    private static IntakeSourceReadResult ReadableWithSubject(
        string subject,
        params IntakeContentFragment[] content) =>
        new(
            IntakeSourceReadStatus.Readable,
            content,
            [new(IntakeEvidenceSource.Subject, subject)],
            [],
            false);

    private static IntakeSourceReadResult Readable(params IntakeContentFragment[] content) =>
        new(
            IntakeSourceReadStatus.Readable,
            content,
            [],
            [],
            false);
}
