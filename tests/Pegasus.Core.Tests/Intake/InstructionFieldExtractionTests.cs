using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The provider-neutral extraction engine's structured binding.
///
/// The layouts exercised here are the three shapes the fifteen instruction
/// profiles actually meet — an OAK-shaped header row above aligned value cells,
/// an ALS-shaped label column beside paired party columns, and a DFD-shaped PDF
/// form — reproduced structurally with non-domain values. No genuine
/// correspondence is embedded, and no assertion depends on a real party's data.
/// </summary>
public sealed class InstructionFieldExtractionTests
{
    private static readonly InstructionFieldEngine.FieldDefinition ClaimNumber =
        new("Claim number", ["Claim Number", "Claim No"]);

    private static readonly InstructionFieldEngine.FieldDefinition Registration =
        new(
            "Vehicle registration",
            ["Registration", "Vehicle Registration"],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration);

    private static readonly InstructionFieldEngine.FieldDefinition VehicleModel =
        new("Vehicle model", ["Model"], IsRequired: true);

    private static readonly InstructionFieldEngine.FieldDefinition VatStatus =
        new("VAT status", ["VAT Status", "VAT"]);

    private static readonly InstructionFieldEngine.FieldDefinition InstructionDate =
        new(
            "Date of instruction",
            ["Date of Instruction"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null);

    [Fact]
    public void AHeaderLabelBindsToTheCellAlignedBeneathIt()
    {
        var fields = Extract(
            [ClaimNumber, Registration],
            Cell(1, 1, 1, "Claim Number"),
            Cell(1, 1, 2, "Registration"),
            Cell(1, 2, 1, "CLM-1001"),
            Cell(1, 2, 2, "AB12 CDE"));

        Assert.Equal("CLM-1001", Suggested(fields, "Claim number"));
        Assert.Equal("AB12 CDE", Suggested(fields, "Vehicle registration"));

        // The provenance is the VALUE's cell, not the label's: an operator
        // following the locator lands on what was read.
        var claim = Single(fields, "Claim number");
        Assert.Equal(IntakeLocatorKind.TableCell, claim.Locator!.Kind);
        Assert.Equal("T1R2C1", claim.Locator.Cell);
        Assert.Equal("T1R2C2", Single(fields, "Vehicle registration").Locator!.Cell);
    }

    [Fact]
    public void PairedPartyColumnsKeepTheirOwnPartysValue()
    {
        // A label column beside two party columns whose header row says whose is
        // whose. The definition names the header it is asking about, so the
        // engine reads that column and not its neighbour.
        var claimantRegistration = Registration with { ColumnHeader = "Claimant" };
        var thirdPartyRegistration = new InstructionFieldEngine.FieldDefinition(
            "Third party registration",
            ["Registration", "Vehicle Registration"],
            ColumnHeader: "Third Party",
            IsValidTyped: InstructionFieldEngine.IsUkRegistration);

        IntakeContentFragment[] table =
        [
            Cell(1, 1, 1, "Detail"),
            Cell(1, 1, 2, "Claimant"),
            Cell(1, 1, 3, "Third Party"),
            Cell(1, 2, 1, "Registration"),
            Cell(1, 2, 2, "AB12 CDE"),
            Cell(1, 2, 3, "XY34 ZZZ")
        ];

        var claimant = Extract([claimantRegistration], table);
        Assert.Equal("AB12 CDE", Suggested(claimant, "Vehicle registration"));
        Assert.Equal("T1R2C2", Single(claimant, "Vehicle registration").Locator!.Cell);

        var thirdParty = Extract([thirdPartyRegistration], table);
        Assert.Equal("XY34 ZZZ", Suggested(thirdParty, "Third party registration"));
        Assert.Equal("T1R2C3", Single(thirdParty, "Third party registration").Locator!.Cell);
    }

    [Fact]
    public void PairedColumnsWithoutADeclaredPartyAreAmbiguousWithBothRawValuesAndLocators()
    {
        var fields = Extract(
            [Registration],
            Cell(1, 1, 1, "Detail"),
            Cell(1, 1, 2, "Claimant"),
            Cell(1, 1, 3, "Third Party"),
            Cell(1, 2, 1, "Registration"),
            Cell(1, 2, 2, " AB12  CDE "),
            Cell(1, 2, 3, "XY34 ZZZ"));

        var field = Field(fields, "Vehicle registration");
        Assert.True(field.HasConflict);
        Assert.Null(field.SuggestedValue);
        Assert.Equal(2, field.Candidates.Count);

        // Both readings, each with the text as printed and the cell it sits in.
        Assert.Equal(["AB12 CDE", "XY34 ZZZ"], field.Candidates.Select(candidate => candidate.Value));
        Assert.Equal([" AB12  CDE ", "XY34 ZZZ"], field.Candidates.Select(candidate => candidate.SourceValue));
        Assert.Equal(["T1R2C2", "T1R2C3"], field.Candidates.Select(candidate => candidate.Locator!.Cell));
        Assert.Equal([0, 1], field.Candidates.Select(candidate => candidate.Locator!.Occurrence));
    }

    [Fact]
    public void APdfFormFieldKeepsItsOwnIdentity()
    {
        var claimNumber = ClaimNumber with { FormFields = ["txtClaimRef"] };
        var fields = Extract(
            [claimNumber, Registration with { FormFields = ["txtVrm"] }],
            FormField("txtClaimRef", "CLM-2002"),
            FormField("txtVrm", "AB12 CDE"),
            // A field named after another field's label must not be read as this
            // one: identity is the declared name, never the printed neighbour.
            FormField("Claim Number", "NOT-THE-CLAIM"));

        Assert.Equal("CLM-2002", Suggested(fields, "Claim number"));
        var candidate = Single(fields, "Claim number");
        Assert.Equal(IntakeLocatorKind.FormField, candidate.Locator!.Kind);
        Assert.Equal("txtClaimRef", candidate.Locator.FormField);
        Assert.Equal("AB12 CDE", Suggested(fields, "Vehicle registration"));
    }

    [Fact]
    public void FlattenedNeighbouringTextCannotSwapTwoCellsValues()
    {
        // The same table, flattened the way a text extractor flattens it: each
        // row becomes one line and the two values sit side by side. The cell
        // fragments are present, so the structured binding is what answers and
        // the flattened line is not offered beside it.
        var flattened = new IntakeContentFragment(
            IntakeEvidenceSource.DocumentContent,
            "instruction.docx",
            "Registration Model\nAB12 CDE MODEL-ONE");

        var fields = Extract(
            [Registration, VehicleModel],
            flattened,
            Cell(1, 1, 1, "Registration"),
            Cell(1, 1, 2, "Model"),
            Cell(1, 2, 1, "AB12 CDE"),
            Cell(1, 2, 2, "MODEL-ONE"));

        Assert.Equal("AB12 CDE", Suggested(fields, "Vehicle registration"));
        Assert.Equal("MODEL-ONE", Suggested(fields, "Vehicle model"));
        Assert.False(Field(fields, "Vehicle model").HasConflict);
        Assert.Equal("T1R2C2", Single(fields, "Vehicle model").Locator!.Cell);
    }

    [Fact]
    public void ALabelAloneOnItsRowBindsToTheCellBeneathIt()
    {
        var fields = Extract(
            [ClaimNumber, VehicleModel],
            Cell(1, 1, 1, "Reference block"),
            Cell(1, 2, 1, "Claim Number"),
            Cell(1, 3, 1, "CLM-3003"));

        Assert.Equal("CLM-3003", Suggested(fields, "Claim number"));
        Assert.Equal("T1R3C1", Single(fields, "Claim number").Locator!.Cell);
    }

    [Fact]
    public void AnAbsentModelVatStatusOrDateIsReportedMissingAndNeverGuessed()
    {
        var fields = Extract(
            [Registration, VehicleModel, VatStatus, InstructionDate],
            Cell(1, 1, 1, "Registration"),
            Cell(1, 2, 1, "AB12 CDE"),
            // Text that a positional guess might reach for: a date in a footer,
            // a make with no model beside it, and the word VAT in a sentence.
            new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                "instruction.docx",
                "Printed 01/02/2026. Amounts shown are subject to VAT where applicable."));

        Assert.Equal("AB12 CDE", Suggested(fields, "Vehicle registration"));
        foreach (var name in new[] { "Vehicle model", "VAT status", "Date of instruction" })
        {
            var field = Field(fields, name);
            Assert.Null(field.SuggestedValue);
            Assert.Empty(field.Candidates);
            Assert.False(field.IsDefaulted);
            Assert.False(field.HasConflict);
        }
    }

    [Fact]
    public void MissingRequiredFieldsAreNamedOnceEach()
    {
        var (_, missing, _) = Run(
            [Registration, VehicleModel, VatStatus],
            [Cell(1, 1, 1, "Registration"), Cell(1, 2, 1, "AB12 CDE")]);

        Assert.Equal(["Vehicle model", "VAT status"], missing);
    }

    [Fact]
    public void ADocumentWithNoStructureStillReadsFromItsFlattenedText()
    {
        // The structured path is an addition, not a replacement: a plain e-mail
        // body has neither cells nor form fields and reads exactly as before.
        var fields = Extract(
            [ClaimNumber, Registration],
            new IntakeContentFragment(
                IntakeEvidenceSource.EmailBody,
                "message, email body",
                "Claim Number: CLM-4004\nRegistration: AB12 CDE"));

        Assert.Equal("CLM-4004", Suggested(fields, "Claim number"));
        Assert.Equal("AB12 CDE", Suggested(fields, "Vehicle registration"));
        Assert.Null(Single(fields, "Claim number").Locator);
    }

    [Fact]
    public void TheCurrentBodyIsPreferredOverTheHistoryQuotedBeneathIt()
    {
        // Document order decides, and the reader puts the retained body first.
        // A value the quoted history repeats is the same reading, not a second
        // one, so nothing becomes ambiguous by being quoted back.
        var fields = Extract(
            [ClaimNumber],
            new IntakeContentFragment(
                IntakeEvidenceSource.EmailBody,
                "message, email body",
                "Claim Number: CLM-5005\nFrom: someone\nSent: today\nClaim Number: CLM-5005",
                IntakeSourceLocator.ForMessagePart(IntakeMessagePart.CurrentBody, "chars 0-30")),
            new IntakeContentFragment(
                IntakeEvidenceSource.EmailBody,
                "message, quoted history",
                "From: someone\nSent: today\nClaim Number: CLM-5005",
                IntakeSourceLocator.ForMessagePart(IntakeMessagePart.QuotedHistory, "chars 30-78")));

        var field = Field(fields, "Claim number");
        Assert.False(field.HasConflict);
        Assert.Equal("CLM-5005", field.SuggestedValue);
        Assert.Single(field.Candidates);
        Assert.Equal(IntakeMessagePart.CurrentBody, field.Candidates[0].Locator!.MessagePart);
    }

    private static IntakeContentFragment Cell(int table, int row, int column, string text) =>
        new(
            IntakeEvidenceSource.DocumentContent,
            $"instruction.docx, table {table} row {row} column {column}",
            text,
            IntakeSourceLocator.ForCell(table, row, column));

    private static IntakeContentFragment FormField(string name, string value) =>
        new(
            IntakeEvidenceSource.PdfContent,
            $"instruction.pdf, form field {name}",
            value,
            IntakeSourceLocator.ForFormField(name, page: 1));

    private static IReadOnlyList<InstructionReviewField> Extract(
        InstructionFieldEngine.FieldDefinition[] definitions,
        params IntakeContentFragment[] fragments) =>
        Run(definitions, fragments).Fields;

    private static (IReadOnlyList<InstructionReviewField> Fields, IReadOnlyList<string> Missing, IReadOnlyList<IntakeEvidence> Evidence)
        Run(
            InstructionFieldEngine.FieldDefinition[] definitions,
            IntakeContentFragment[] fragments) =>
        InstructionFieldEngine.ExtractFields(
            fragments,
            definitions,
            new(definitions),
            new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private static InstructionReviewField Field(
        IReadOnlyList<InstructionReviewField> fields,
        string name) =>
        fields.Single(field => field.Name == name);

    private static string? Suggested(IReadOnlyList<InstructionReviewField> fields, string name) =>
        Field(fields, name).SuggestedValue;

    private static InstructionFieldCandidate Single(
        IReadOnlyList<InstructionReviewField> fields,
        string name) =>
        Assert.Single(Field(fields, name).Candidates);
}
