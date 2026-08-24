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

    [Fact]
    public void TypographicApostropheLetterYieldsClaimantAndVehicle()
    {
        // The real letters write "Our Client\u2019s Vehicle" with a typographic
        // apostrophe: before normalization the "Our Client" label swallowed the
        // vehicle line as a garbage claimant candidate, and the description
        // label never matched at all.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Our Ref: JF/47862/1\nOur Client: Mr Stuart Mcwalters\n" +
                "Our Client\u2019s Vehicle: MERCEDES-BENZ E 220 D AMG LINE PREMIUM+ AUTO\n" +
                "Registration: V2 MTM\nDate of Accident: 15 August 2026")),
            ProcessedAtUtc,
            QdosContext);

        var claimant = Assert.Single(result.Fields, field => field.Name == "Claimant name");
        Assert.False(claimant.HasConflict);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mr Stuart Mcwalters", draft.ClaimantName);
        Assert.Equal("MERCEDES-BENZ", draft.VehicleMake);
        Assert.Equal("E 220 D AMG LINE PREMIUM+ AUTO", draft.VehicleModel);
        Assert.Equal("V2MTM", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2026, 8, 15), draft.DateOfIncident);
    }

    [Fact]
    public void TwoSpellingsOfOneDateAreNotAConflict()
    {
        // Every letter carries the incident date twice: long form on page one
        // ("15 August 2026") and numeric in the details block ("15/08/2026").
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Date of Accident: 15 August 2026\nAccident Date: 15/08/2026")),
            ProcessedAtUtc,
            QdosContext);

        var field = Assert.Single(result.Fields, item => item.Name == "Date of incident");
        Assert.False(field.HasConflict);
        Assert.Equal(
            new DateOnly(2026, 8, 15),
            Assert.IsType<InstructionDraft>(result.InstructionDraft).DateOfIncident);
    }

    [Fact]
    public void ThirdPartyRowsNeverFeedClaimantFields()
    {
        // Letter page two lists the third party ("TP Vehicle:", "TP
        // Registration:"); those rows must not become claimant candidates.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 2",
                "Registration: V2 MTM\nTP Vehicle: VAUXHALL ASTRA GTC SRI TURBO S/S\n" +
                "TP Registration: KU66XUM")),
            ProcessedAtUtc,
            QdosContext);

        var registration = Assert.Single(result.Fields, field => field.Name == "Vehicle registration");
        Assert.False(registration.HasConflict);
        Assert.DoesNotContain(registration.Candidates, candidate => candidate.Value.Contains("KU66XUM"));
        Assert.Equal(
            "V2MTM",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).VehicleRegistration);
    }

    [Fact]
    public void OrdinalDaySuffixesParseAsDates()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Date of Accident: 27th April 2026")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            new DateOnly(2026, 4, 27),
            Assert.IsType<InstructionDraft>(result.InstructionDraft).DateOfIncident);
    }

    [Fact]
    public void ClaimantsVehicleLabelDerivesTheVehicleFields()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Claimant\u2019s Vehicle: FORD RANGER WILDTRAK ECOBLUE 4X4 A")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("FORD", draft.VehicleMake);
        Assert.Equal("RANGER WILDTRAK ECOBLUE 4X4 A", draft.VehicleModel);
    }

    [Fact]
    public void AReportsVehicleLineFillsTheDetailsTheLetterLacks()
    {
        // INTK-025: the bodyshop report's own grammar backfills make/model
        // when the letter carries no description — and only from a
        // report-named document.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 6: instruction letter, page 1",
                    "Our Ref: JF/47862/1\nOur Client: Mr Stuart Mcwalters"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: Bodyshopreport555017-V1.pdf, page 1",
                    "Vehicle: FORD RANGER WILDTRAK Colour: Black Speedo: Miles\nReg No: MD22DDU")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("FORD", draft.VehicleMake);
        Assert.Equal("RANGER WILDTRAK", draft.VehicleModel);
        Assert.Equal("MD22DDU", draft.VehicleRegistration);
        // "Speedo: Miles" carries no digits and contributes nothing.
        Assert.Null(draft.VehicleMileage);
    }

    [Fact]
    public void TheLetterOutranksTheReportsVehicleLine()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 6: instruction letter, page 1",
                    "Our Client's Vehicle: PEUGEOT RCZ GT"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: Bodyshopreport-V1.pdf, page 1",
                    "Vehicle: FORD RANGER WILDTRAK")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("PEUGEOT", draft.VehicleMake);
        Assert.Equal("RCZ GT", draft.VehicleModel);
    }

    [Fact]
    public void AVehicleLineContributesWhateverDocumentCarriesIt()
    {
        // INTK-028: this once asserted the opposite — a "Vehicle:" line was
        // read only from a document whose file name contained "report".
        // The accompanying report is written by a third-party engineer and
        // named however that firm's system named it, so the file name was
        // never a sound test. The line's own shape is: the QDOS letters
        // write "Our Client's Vehicle:" or "TP Vehicle:", never a bare
        // "Vehicle:" opening a line.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 7: 282770-V1.pdf, page 1",
                "Vehicle: FORD RANGER WILDTRAK")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("FORD", draft.VehicleMake);
        Assert.Equal("RANGER WILDTRAK", draft.VehicleModel);
    }

    [Fact]
    public void TheLettersThirdPartyVehicleLineIsStillNotTheClaimants()
    {
        // The guard the test above used to provide, kept where it belongs:
        // a "TP Vehicle:" row must never reach the claimant's fields, and
        // dropping the file-name gate must not weaken that.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: 42255_1_LtrtoAuditEngin.pdf, page 1",
                "Our Ref: DIK/ND/47603/1\nTP Vehicle: AUDI A4 TECHNIK TDI")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
    }

    [Fact]
    public void TheCircumstancesParagraphLandsAndStopsAtTheDamageBlock()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 2",
                "Please could you check the damage for consistency with the following accident circumstances?\n" +
                "Our client was stationary at traffic lights on Badger Avenue.\n" +
                "Your insured failed to stop and collided with the rear of our client's car.\n" +
                "Damage Area - Rear: Moderate\n" +
                "TP Vehicle: BMW X5")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(
            "Our client was stationary at traffic lights on Badger Avenue. " +
            "Your insured failed to stop and collided with the rear of our client's car." +
            "\n\nDamage Area: Rear: Moderate",
            draft.AccidentCircumstances);
    }

    [Fact]
    public void ALetterWithoutThePromptLeavesCircumstancesEmpty()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Our Client: Mr Stuart Mcwalters")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).AccidentCircumstances);
    }

    [Fact]
    public void QdosTwentySixZeroZeroEightsReportSuppliesItsMileage()
    {
        // INTK-028 regression, verbatim from production: this is exactly
        // what the reader stored for QDOS26008's two documents. The mileage
        // was plainly there and was not read, because the Speedo rule was
        // anchored to the start of a line and the reader lays the report's
        // columns out as one line.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 6: 42255_1_LtrtoAuditEngin.pdf, page 1",
                    "Our Client’s Vehicle: TOYOTA ALPHARD\nTP Vehicle: AUDI A4 TECHNIK TDI"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: Bodyshopreport282770-V1.pdf, page 1",
                    "Vehicle: TOYOTA NOT RECORDED Colour: Black Speedo: 72850 Miles\n"
                        + "Reg No: DP07EFB Registered: Jan 2023 Type: M.P.V. Trans:")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(72_850, draft.VehicleMileage);
        Assert.Equal("DP07EFB", draft.VehicleRegistration);
        // The letter still outranks the report's own vehicle column, so the
        // report's "TOYOTA NOT RECORDED" never becomes the model.
        Assert.Equal("TOYOTA", draft.VehicleMake);
        Assert.Equal("ALPHARD", draft.VehicleModel);
    }

    [Fact]
    public void AMileageColumnIsCutFreeOfItsNeighbours()
    {
        // The value must stop where the next column starts, or it carries
        // "Reg No: …" with it and fails to parse as a mileage at all.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 7: 282770-V1.pdf, page 1",
                "Colour: Black Speedo: 68,240 Miles Reg No: MD22DDU")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(68_240, draft.VehicleMileage);
    }

    [Fact]
    public void AnInstructionLetterKeepsItsCircumstancesEvenWhenItReadsAsAReport()
    {
        // INTK-028 guard rail: broadening report identification must never
        // cost a letter its circumstances paragraph. The circumstances
        // prompt is now its own test rather than being gated on the letter
        // not looking like a report.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Our Client: Mr Stuart Mcwalters\n"
                    + "Colour: Black Speedo: 68,240 Reg No: MD22DDU\n"
                    + "Please can you check the damage for consistency with the following accident circumstances?\n"
                    + "The insured reversed into the claimant's stationary vehicle.\n"
                    + "\n"
                    + "Damage area: rear")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(
            "The insured reversed into the claimant's stationary vehicle."
                + "\n\nDamage Area: rear",
            draft.AccidentCircumstances);
    }

    [Fact]
    public void TheBareDateRowIsTheInstructionDateAndLeavesTheAccidentDateAlone()
    {
        // ENG-015: the letters date themselves with a bare "Date:" row, so
        // without it every QDOS case fell back to its receipt date. The
        // regression this risks is the bare label swallowing the accident
        // row instead — "Date of Accident:" also begins with "Date".
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Our Ref: AKH//47743/1\n"
                    + "Date: 22/08/2026\n"
                    + "Date of Accident: 14/08/2026")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(new DateOnly(2026, 8, 22), draft.InstructionDate);
        Assert.Equal(new DateOnly(2026, 8, 14), draft.DateOfIncident);

        var instruction = Assert.Single(result.Fields, item => item.Name == "Instruction date");
        Assert.False(instruction.HasConflict);
        Assert.False(instruction.IsDefaulted);
    }

    [Fact]
    public void TheBareDateLabelDoesNotClaimAnotherRowsTrailingDate()
    {
        // The other shape of the same risk, and the one the value cannot
        // reject: "Accident Date: 15/08/2026" ends in a perfectly valid date,
        // so only the guarded prefixes keep it out of the instruction date.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Accident Date: 15/08/2026\n"
                    + "Inspection Date: 20/08/2026")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(new DateOnly(2026, 8, 15), draft.DateOfIncident);
        Assert.Equal(new DateOnly(2026, 8, 20), draft.InspectionDate);

        // Neither row is the instruction date, so it defaults as it always did.
        var instruction = Assert.Single(result.Fields, item => item.Name == "Instruction date");
        Assert.True(instruction.IsDefaulted);
    }

    [Fact]
    public void TheAppendedReportsInspectionDateBeatsTheInstructionLetters()
    {
        // ENG-015: the instruction can only propose an inspection date; the
        // appended engineer's report states when the vehicle was actually
        // seen. The later fragment wins for this field, and this field only.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new IntakeContentFragment(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 6: instruction letter, page 1",
                    "Inspection Date: 20/08/2026"),
                new IntakeContentFragment(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: engineer's report, page 1",
                    "Inspection Date: 23/08/2026")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(new DateOnly(2026, 8, 23), draft.InspectionDate);
    }

    [Fact]
    public void TheEarliestFragmentStillWinsForEveryOtherField()
    {
        // The precedence reversal is per field. The claimant name still takes
        // the instruction letter's spelling over the appended report's.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new IntakeContentFragment(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 6: instruction letter, page 1",
                    "Claimant Name: Mrs Caroline Reynolds"),
                new IntakeContentFragment(
                    IntakeEvidenceSource.PdfContent,
                    "attachment 7: engineer's report, page 1",
                    "Claimant Name: C Reynolds")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "Mrs Caroline Reynolds",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).ClaimantName);
    }

    [Fact]
    public void ALetterWithOnlyADamageAreaMakesItTheWholeCircumstances()
    {
        // ENG-015, and the shape of the QDOS audit letters: no circumstances
        // prose at all, so the labelled damage area stands alone with no
        // leading blank line.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 2",
                "Damage Area - Nearside Front: Light\nTP Vehicle: BMW X5")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "Damage Area: Nearside Front: Light",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).AccidentCircumstances);
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
