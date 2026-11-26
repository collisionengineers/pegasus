using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// R1027: a new Case takes the Repairer its instruction names when that name
/// is exactly one active Repairer's, ignoring capitals; otherwise (R1264) the
/// one linked Repairer its text names; otherwise none. Synthetic names only.
/// </summary>
public sealed class EstablishedIntakeRepairerTests
{
    private const string RepairerNameField = "Repairer name";

    private static readonly ContactChoice Named = Repairer("Harbour Body Repairs");
    private static readonly ContactChoice Other = Repairer("Harbour Body Repairs North");
    private static readonly ContactChoice ProPaint = Repairer("Pro Paint Bodyshop", "AX");
    private static readonly ContactChoice Quayside = Repairer("Quayside Motors", "AX");
    private static readonly ContactChoice Elsewhere = Repairer("Moorland Coachworks", "QDOS");

    [Theory]
    [InlineData("Harbour Body Repairs")]
    [InlineData("HARBOUR BODY REPAIRS")]
    [InlineData("  harbour body repairs ")]
    public void TheInstructionsNamedRepairerIsTaken(string printed)
    {
        var field = Field(RepairerNameField, printed);

        var established = EstablishedIntakeRepairer.For([field], [Other, Named]);

        Assert.NotNull(established);
        Assert.Same(Named, established.Repairer);
        Assert.Same(field, established.Field);
    }

    [Theory]
    [InlineData("A garage the directory does not hold")]
    [InlineData("HarbourBodyRepairs")]
    [InlineData("Harbour Body Repairs Ltd")]
    [InlineData("Harbour Body")]
    public void ANameThatIsNoRepairersEstablishesNothing(string printed)
    {
        var fields = new[] { Field(RepairerNameField, printed) };

        Assert.NotNull(EstablishedIntakeRepairer.NamedIn(fields));
        Assert.Null(EstablishedIntakeRepairer.For(fields, [Named, Other]));
    }

    [Fact]
    public void AConflictedFieldNamesNothing()
    {
        var conflicted = new InstructionReviewField(
            RepairerNameField, "Harbour Body Repairs", [], false, HasConflict: true);

        Assert.Null(EstablishedIntakeRepairer.NamedIn([conflicted]));
        Assert.Null(EstablishedIntakeRepairer.For([conflicted], [Named]));
    }

    [Fact]
    public void AnotherFieldIsNotTheRepairer()
    {
        var fields = new[]
        {
            Field("Repairer address", "Harbour Body Repairs"),
            Field("Claim source", "Harbour Body Repairs")
        };

        Assert.Null(EstablishedIntakeRepairer.NamedIn(fields));
        Assert.Null(EstablishedIntakeRepairer.For(fields, [Named]));
    }

    [Fact]
    public void AnInstructionThatNamesNoRepairerEstablishesNothing()
    {
        Assert.Null(EstablishedIntakeRepairer.NamedIn([]));
        Assert.Null(EstablishedIntakeRepairer.For([Field(RepairerNameField, "  ")], [Named]));
    }

    [Fact]
    public void TwoRepairersOfTheNameEstablishNothing()
    {
        var twin = Repairer("harbour body repairs");

        Assert.Null(EstablishedIntakeRepairer.For([Field(RepairerNameField, "Harbour Body Repairs")], [Named, twin]));
    }

    /// <summary>
    /// R1264: otherwise the one active Repairer linked to the Case's Principal
    /// that the instruction's text names, capitals and spaces ignored.
    /// </summary>
    [Theory]
    [InlineData("exact", "The vehicle is at Pro Paint Bodyshop, Leeds.")]
    [InlineData("unspaced", "The vehicle is at ProPaint Bodyshop, Leeds.")]
    [InlineData("capitals", "THE VEHICLE IS AT PRO PAINT BODYSHOP")]
    public void TheOneLinkedRepairerTheTextNamesIsTaken(string row, string text)
    {
        Assert.NotEmpty(row);

        var established = EstablishedIntakeRepairer.For([], [Named, ProPaint, Quayside, Elsewhere], "AX", text);

        Assert.NotNull(established);
        Assert.Same(ProPaint, established.Repairer);
        Assert.Null(established.Field);
    }

    [Fact]
    public void TwoLinkedRepairersTheTextNamesGiveNone()
    {
        Assert.Null(EstablishedIntakeRepairer.For(
            [], [ProPaint, Quayside], "AX", "Towed from Quayside Motors to Pro Paint Bodyshop."));
    }

    [Fact]
    public void ARepairerTheTextNamesThatIsNotLinkedToThePrincipalGivesNone()
    {
        Assert.Null(EstablishedIntakeRepairer.For([], [ProPaint, Elsewhere], "AX", "At Moorland Coachworks."));
        Assert.Null(EstablishedIntakeRepairer.For([], [Named, ProPaint], "AX", "At Harbour Body Repairs."));
    }

    [Fact]
    public void TheInstructionsNamedRepairerComesBeforeTheText()
    {
        var field = Field(RepairerNameField, "Harbour Body Repairs");

        var established = EstablishedIntakeRepairer.For(
            [field], [Named, ProPaint], "AX", "The vehicle is at Pro Paint Bodyshop.");

        Assert.Same(Named, established?.Repairer);
        Assert.Same(field, established?.Field);
    }

    [Fact]
    public void NoTextGivesNoRepairer()
    {
        Assert.Null(EstablishedIntakeRepairer.For([], [ProPaint], "AX", null));
        Assert.Null(EstablishedIntakeRepairer.For([], [ProPaint], "AX", "  "));
        Assert.Null(EstablishedIntakeRepairer.For([], [ProPaint], null, "At Pro Paint Bodyshop."));
    }

    private static InstructionReviewField Field(string name, string value) =>
        new(
            name,
            value,
            [new(value, IntakeEvidenceSource.PdfContent, "instruction.pdf")],
            IsDefaulted: false,
            HasConflict: false);

    private static ContactChoice Repairer(string name, params string[] linkedPrincipalCodes) =>
        new(
            Guid.NewGuid(),
            name,
            ContactRole.Repairer,
            Version: 1,
            Email: null,
            Telephone: null,
            Address: null,
            Postcode: null,
            NotesOnEveryCase: null,
            LinkedPrincipalCodes: linkedPrincipalCodes);
}
