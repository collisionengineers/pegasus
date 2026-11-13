using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// R1027: a new Case takes the Repairer its instruction names when that name
/// is exactly one active Repairer's, ignoring capitals; otherwise none.
/// Synthetic names only.
/// </summary>
public sealed class EstablishedIntakeRepairerTests
{
    private const string RepairerNameField = "Repairer name";

    private static readonly ContactChoice Named = Repairer("Harbour Body Repairs");
    private static readonly ContactChoice Other = Repairer("Harbour Body Repairs North");

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

    private static InstructionReviewField Field(string name, string value) =>
        new(
            name,
            value,
            [new(value, IntakeEvidenceSource.PdfContent, "instruction.pdf")],
            IsDefaulted: false,
            HasConflict: false);

    private static ContactChoice Repairer(string name) =>
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
            LinkedPrincipalCodes: []);
}
