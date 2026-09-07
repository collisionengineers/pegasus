using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosInstructionExtractionPolicyTests
{
    [Fact]
    public void ADamageAreaLabelAloneDoesNotBecomeAccidentCircumstances()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 7: instruction letter, page 1",
                "Damage Area:\n"
                + "\n"
                + "Offside front wing crushed and the\n"
                + "headlamp assembly is detached.\n"
                + "\n"
                + "TP Vehicle: SCANIA")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "Offside front wing crushed and the headlamp assembly is detached.",
            Field(result, "Damage area").SuggestedValue);
        // And it is NOT the accident circumstances: this letter states no
        // circumstances at all, and damage is not an account of the accident.
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).AccidentCircumstances);
    }

    [Fact]
    public void ALetterWithOnlyADamageAreaStillHasNoCircumstances()
    {
        // The shape of the QDOS audit letters: no circumstances prose at all.
        // ENG-015 appended the labelled damage area to the circumstances
        // field; INTK-060 C03 separated them, because what the vehicle looks
        // like is not an account of how the accident happened and a reviewer
        // reading one concatenated value cannot tell which half the letter
        // actually stated.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 2",
                "Damage Area - Nearside Front: Light\nTP Vehicle: BMW X5")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal("Nearside Front: Light", Field(result, "Damage area").SuggestedValue);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).AccidentCircumstances);
    }

    [Fact]
    public void AWrappedDamageDescriptionDoesNotBecomeAccidentCircumstances()
    {
        // The letters wrap the description mid-sentence across physical rows —
        // the retained QDOS_NX14AXY output carries "...rear wheel arch is\n
        // damaged." — so reading only the label's own row cut the sentence in
        // half. Pre-existing damage is a separate field and stops the block.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 7: instruction letter, page 1",
                "Damage Area - Nearside: Moderate: Nearside rear wheel arch is\n"
                + "damaged. Nearside door is damaged.\n"
                + "\n"
                + "Pre-existing Damage:\n"
                + "\n"
                + "No.\n"
                + "\n"
                + "TP Vehicle: SCANIA")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "Nearside: Moderate: Nearside rear wheel arch is damaged. "
            + "Nearside door is damaged.",
            Field(result, "Damage area").SuggestedValue);
        // The pre-existing damage row that ended the block is its own field.
        Assert.Equal("No.", Field(result, "Pre-existing damage").SuggestedValue);
        Assert.Null(Assert.IsType<InstructionDraft>(result.InstructionDraft).AccidentCircumstances);
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
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(
            "PEUGEOT RCZ GT THP 156",
            Field(result, "Vehicle description").SuggestedValue);
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
    public void TheVehicleDescriptionIsNeverSplitIntoAMakeAndAModel()
    {
        // This once asserted the opposite: the description was split on token
        // position, helped by a five-entry list of two-word makes. Both are
        // guesses the extraction invariants name and forbid, and the
        // independently labelled corpus records the description as ONE value
        // in every original. The whole description survives; a make and a
        // model appear only where the letter labels them.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Our Client's Vehicle: LAND ROVER R ROVER EVOQUE SE LK17 NHT")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "LAND ROVER R ROVER EVOQUE SE LK17 NHT",
            Field(result, "Vehicle description").SuggestedValue);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Contains("Vehicle make", result.MissingFields);
        Assert.Contains("Vehicle model", result.MissingFields);
        // A registration is still recovered from a description that ENDS in a
        // valid one. That is a shape the value either has or has not - not a
        // position, and not a name from a list.
        Assert.Equal("LK17NHT", draft.VehicleRegistration);
        Assert.Equal(
            "LAND ROVER R ROVER EVOQUE SE LK17 NHT",
            Assert.Single(result.Fields, field => field.Name == "Vehicle description").SuggestedValue);
    }

    [Fact]
    public void AnExplicitMakeLabelIsTheOnlySourceOfAMake()
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
        // The description is not a second opinion about the make, and the
        // model the description might have been read as is unavailable.
        Assert.Null(draft.VehicleModel);
        Assert.Equal("PEUGEOT RCZ", Field(result, "Vehicle description").SuggestedValue);
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
        Assert.Equal(
            "MERCEDES-BENZ E 220 D AMG LINE PREMIUM+ AUTO",
            Field(result, "Vehicle description").SuggestedValue);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
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
    public void ClaimantsVehicleLabelKeepsTheWholeDescription()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Claimant\u2019s Vehicle: FORD RANGER WILDTRAK ECOBLUE 4X4 A")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "FORD RANGER WILDTRAK ECOBLUE 4X4 A",
            Field(result, "Vehicle description").SuggestedValue);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(
            "FORD RANGER WILDTRAK ECOBLUE 4X4 A",
            Assert.Single(result.Fields, field => field.Name == "Vehicle description").SuggestedValue);
    }

    [Fact]
    public void AReportsVehicleLinePreservesTheDescriptionWithoutGuessingMakeOrModel()
    {
        // INTK-025: the bodyshop report's own grammar backfills the vehicle
        // description and registration when the letter carries neither. It
        // does not backfill a make or a model - the report states one
        // combined vehicle text too.
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

        Assert.Equal(
            "FORD RANGER WILDTRAK",
            Field(result, "Vehicle description").SuggestedValue);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
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
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        var description = Field(result, "Vehicle description");
        Assert.False(description.HasConflict);
        Assert.Equal("PEUGEOT RCZ GT", description.SuggestedValue);
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
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(
            "FORD RANGER WILDTRAK",
            Field(result, "Vehicle description").SuggestedValue);
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
        Assert.Null(Field(result, "Vehicle description").SuggestedValue);
        // It is read - under the third party's own role, where nothing
        // downstream can mistake it for the claimant's vehicle.
        Assert.Equal(
            "AUDI A4 TECHNIK TDI",
            Field(result, "Third-party vehicle").SuggestedValue);
        Assert.Equal(
            "third-party",
            new QdosInstructionExtractionPolicy().FieldRoles["Third-party vehicle"].PartyRole);
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
            "Your insured failed to stop and collided with the rear of our client's car.",
            draft.AccidentCircumstances);
        // The damage row the paragraph stopped at is a separate field, and the
        // circumstances carry no part of it.
        Assert.Equal("Rear: Moderate", Field(result, "Damage area").SuggestedValue);
        Assert.DoesNotContain("Damage", draft.AccidentCircumstances!, StringComparison.Ordinal);
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
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        // The letter still outranks the report's own vehicle column, so the
        // report's "TOYOTA NOT RECORDED" never becomes the vehicle.
        Assert.Equal("TOYOTA ALPHARD", Field(result, "Vehicle description").SuggestedValue);
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
            "The insured reversed into the claimant's stationary vehicle.",
            draft.AccidentCircumstances);
        Assert.Equal("rear", Field(result, "Damage area").SuggestedValue);
    }

    // The Triage subject template is the only QDOS shape whose registration
    // exists nowhere but the subject, and QDOS writes it in two spacings
    // (INTK-033). Both are real corpus subjects.
    [Theory]
    [InlineData(
        "Engineer Triage - Our Claim Reference 46384/1 , Vehicle Registration YD14VGJ",
        "YD14VGJ")]
    [InlineData(
        "Engineer Triage - Our Claim Reference : 46246/1 - Vehicle Registration : VO75DFJ",
        "VO75DFJ")]
    public void TheSubjectRegistrationLabelIsReadInEitherRecordedSpacing(
        string subject,
        string expected)
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            ReadableWithSubject(
                subject,
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Please see the attached images to determine if the vehicle is repairable.")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(expected, Assert.IsType<InstructionDraft>(result.InstructionDraft).VehicleRegistration);
    }

    [Fact]
    public void TheRegistrationLabelIsNeverReadAsAVehicleDescription()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            ReadableWithSubject(
                "Engineer Triage - Our Claim Reference : 46246/1 - Vehicle Registration : VO75DFJ",
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Please see the attached images.")),
            ProcessedAtUtc,
            QdosContext);

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
    }

    [Fact]
    public void ASubjectVehicleDescriptionIsStillReadWhenItIsNotTheRegistrationLabel()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            ReadableWithSubject(
                "Client Mr Garry Mackenzie, Vehicle NISSAN QASHQAI N-TEC SH61WDY, Our Ref 45858_1",
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Please see the attached instruction.")),
            ProcessedAtUtc,
            QdosContext);

        Assert.Equal(
            "NISSAN QASHQAI N-TEC SH61WDY",
            Field(result, "Vehicle description").SuggestedValue);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal("SH61WDY", draft.VehicleRegistration);
    }

    [Fact]
    public void TheExtractionPolicyNoLongerProducesTriageMatchEvidence()
    {
        // The accepted Triage match is derived from the route's own
        // classification decision now, and there is exactly one owner of it
        // (INTK-033).
        var result = new QdosInstructionExtractionPolicy().Extract(
            ReadableWithSubject(
                "Engineer Triage - Our Claim Reference 46384/1 , Vehicle Registration YD14VGJ",
                new IntakeContentFragment(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Triage Only Request")),
            ProcessedAtUtc,
            QdosContext);

        Assert.DoesNotContain(
            result.Evidence,
            item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);
    }

    /// <summary>
    /// The letter's own party blocks, read from the shape the originals
    /// print: a heading, then rows whose neighbouring column has to be cut
    /// off them, and a client name row the claimant-name field already owns.
    /// </summary>
    [Fact]
    public void ThePartyBlocksAreReadAsTheirOwnRolesAndCutFreeOfTheirNeighbours()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "Our Client:         Ms Angela Feetham\n"
                + "                    AUDIT REPORT NOTIFICATION\n"
                + "CLIENT DETAILS\n"
                + "Ms Angela Feetham                              Vehicle Details\n"
                + "62 Edgefield\n"
                + "West Allotment                                 Client's Vehicle:   NISSAN JUKE\n"
                + "Newcastle upon Tyne\n"
                + "NE27 0BT                                       Accident Date:      02/05/2026\n"
                + "\n"
                + "Home Tel:             07738011335\n"
                + "Mobile:               07738011335\n"
                + "\n"
                + "REPAIRER DETAILS\n"
                + "Gordon Marshall Coachworks\n"
                + "16a West Langland Street\n"
                + "Kilmarnock, KA1 2PY\n"
                + "Tel:                  07923629069\n"
                + "Email:                gwcmarshall@aol.com")),
            ProcessedAtUtc,
            QdosContext);

        // The client's own name row is left to the claimant-name field, the
        // next column is cut off every row, and the bare label the cut leaves
        // behind does not end the block.
        Assert.Equal(
            "62 Edgefield, West Allotment, Newcastle upon Tyne, NE27 0BT",
            Field(result, "Claimant address").SuggestedValue);
        Assert.Equal(
            "Gordon Marshall Coachworks, 16a West Langland Street, Kilmarnock, KA1 2PY",
            Field(result, "Repairer details").SuggestedValue);
        Assert.Equal(
            "AUDIT REPORT NOTIFICATION",
            Field(result, "Requested work").SuggestedValue);
        Assert.Equal("07738011335", Field(result, "Claimant home telephone").SuggestedValue);
        // The repairer's bare "Tel:" row is the repairer's, and the claimant's
        // "Home Tel:" row is the claimant's. Neither takes the other's number.
        Assert.Equal("07923629069", Field(result, "Repairer telephone").SuggestedValue);
        Assert.Equal("gwcmarshall@aol.com", Field(result, "Repairer email").SuggestedValue);

        var roles = new QdosInstructionExtractionPolicy().FieldRoles;
        Assert.Equal("claimant", roles["Claimant address"].PartyRole);
        Assert.Equal("repairer", roles["Repairer details"].PartyRole);
        Assert.Equal("instruction", roles["Requested work"].PartyRole);
        Assert.Equal("principal", roles["Claim number"].ReferenceRole);
    }

    [Fact]
    public void AMislabelledRepairerEmailRowIsWithheldRatherThanRecorded()
    {
        // One recorded original prints a telephone number under the
        // repairer's "Email:" row. A value that is not an address is not this
        // field's value, and returning nothing beats returning that.
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                "attachment 6: instruction letter, page 1",
                "REPAIRER DETAILS\nEmail:                07902 317534")),
            ProcessedAtUtc,
            QdosContext);

        var email = Field(result, "Repairer email");
        Assert.Null(email.SuggestedValue);
        Assert.Empty(email.Candidates);
    }

    private static InstructionReviewField Field(
        InstructionExtractionResult result,
        string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

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
