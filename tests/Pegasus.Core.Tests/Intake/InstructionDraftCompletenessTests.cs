using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// What a draft still needs, said in the operator's words before anything is
/// written.
/// </summary>
/// <remarks>
/// This rule used to live as a private predicate inside the EF mutation store,
/// where it decided the decision a correction produced and no screen could ask
/// it first. The consequence was concrete: a correction with a blank field
/// landed, the same rule then refused it, and the item was left blocked with
/// no warning. These tests are the rule's own proof, at the level it belongs.
/// </remarks>
public sealed class InstructionDraftCompletenessTests
{
    [Fact]
    public void FullyKeyedDraftWithNoExtractionBehindItIsComplete()
    {
        // The hand-keyed case. Nothing here came out of a document; a person
        // typed all of it, and that is a complete instruction.
        Assert.True(InstructionDraftCompleteness.IsComplete(Complete()));
        Assert.Empty(InstructionDraftCompleteness.MissingFieldNames(Complete()));
    }

    [Theory]
    [InlineData("Claimant name")]
    [InlineData("Claim number")]
    [InlineData("Vehicle registration")]
    [InlineData("Vehicle make")]
    [InlineData("Vehicle model")]
    [InlineData("Vehicle mileage")]
    [InlineData("Accident circumstances")]
    [InlineData("Date of incident")]
    [InlineData("Instruction date")]
    [InlineData("Inspection address")]
    public void EachRequiredFieldMissingInTurnNamesExactlyThatField(string fieldName)
    {
        var draft = Without(fieldName);

        var missing = InstructionDraftCompleteness.MissingFieldNames(draft);

        Assert.Equal([fieldName], missing);
        Assert.False(InstructionDraftCompleteness.IsComplete(draft));
    }

    [Fact]
    public void WhitespaceIsNotAnAnswer()
    {
        var draft = Complete() with { InspectionAddress = "   " };

        Assert.Equal(["Inspection address"], InstructionDraftCompleteness.MissingFieldNames(draft));
    }

    [Fact]
    public void EveryMissingFieldIsNamed()
    {
        var missing = InstructionDraftCompleteness.MissingFieldNames(
            new(null, null, null, null, null, null, null, null, null, null, null));

        Assert.Equal(10, missing.Count);
    }

    [Fact]
    public void ASuggestedPrincipalAndAnInspectionDateAreNotRequiredHere()
    {
        // The principal is confirmed on the create screen against the register,
        // not taken from the draft, and an inspection date is a deadline rather
        // than instruction detail. Neither blocks completeness.
        var draft = Complete() with { SuggestedPrincipalCode = null, InspectionDate = null };

        Assert.True(InstructionDraftCompleteness.IsComplete(draft));
    }

    private static InstructionDraft Complete() => new(
        "QDOS",
        "Controlled Claimant",
        "PROTOCOL-2031-001",
        "AB12CDE",
        "Example Make",
        "Example Model",
        12345L,
        "Controlled protocol circumstances",
        new DateOnly(2031, 3, 4),
        new DateOnly(2031, 3, 5),
        "1 Example Street, Exampleton",
        new DateOnly(2031, 3, 20));

    private static InstructionDraft Without(string fieldName) => fieldName switch
    {
        "Claimant name" => Complete() with { ClaimantName = null },
        "Claim number" => Complete() with { ClaimNumber = null },
        "Vehicle registration" => Complete() with { VehicleRegistration = null },
        "Vehicle make" => Complete() with { VehicleMake = null },
        "Vehicle model" => Complete() with { VehicleModel = null },
        "Vehicle mileage" => Complete() with { VehicleMileage = null },
        "Accident circumstances" => Complete() with { AccidentCircumstances = null },
        "Date of incident" => Complete() with { DateOfIncident = null },
        "Instruction date" => Complete() with { InstructionDate = null },
        "Inspection address" => Complete() with { InspectionAddress = null },
        _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown field.")
    };
}
