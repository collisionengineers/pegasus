using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class RepairSpecificationPolicyTests
{
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Theory]
    [InlineData(RepairSpecificationPurpose.OrdinaryAssessment, RepairSpecificationRole.Ordinary)]
    [InlineData(RepairSpecificationPurpose.Audit, RepairSpecificationRole.Conservative)]
    [InlineData(RepairSpecificationPurpose.Audit, RepairSpecificationRole.Maximised)]
    public void OnlyApprovedPurposeRolePairsAreAccepted(
        RepairSpecificationPurpose purpose,
        RepairSpecificationRole role) =>
        RepairSpecificationPolicy.ValidatePurposeAndRole(purpose, role);

    [Theory]
    [InlineData(RepairSpecificationPurpose.OrdinaryAssessment, RepairSpecificationRole.Conservative)]
    [InlineData(RepairSpecificationPurpose.OrdinaryAssessment, RepairSpecificationRole.Maximised)]
    [InlineData(RepairSpecificationPurpose.Audit, RepairSpecificationRole.Ordinary)]
    public void AmbiguousPurposeRolePairsFailClosed(
        RepairSpecificationPurpose purpose,
        RepairSpecificationRole role) =>
        Assert.Throws<InvalidOperationException>(() =>
            RepairSpecificationPolicy.ValidatePurposeAndRole(purpose, role));

    [Fact]
    public void LegacySourceCannotBeAccepted()
    {
        var draft = Draft() with
        {
            Source = new(RepairSpecificationSourceRoute.LegacyUnresolved, null, null, null),
        };
        Assert.Throws<InvalidOperationException>(() =>
            RepairSpecificationPolicy.ValidateAcceptance(draft, Engineer));
    }

    [Fact]
    public void AutomationCannotAcceptEvenConfirmedLines()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RepairSpecificationPolicy.ValidateAcceptance(
                Draft(),
                ActionActor.Automation("automation")));
    }

    [Fact]
    public void UnconfirmedLineBlocksAcceptance()
    {
        var draft = Draft() with
        {
            Lines = [Line("new_part", 1, confirmed: false)],
        };
        Assert.Throws<InvalidOperationException>(() =>
            RepairSpecificationPolicy.ValidateAcceptance(draft, Engineer));
    }

    [Fact]
    public void CalculationBasisMustMatchRawInputsAndRecordedVat()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RepairSpecificationPolicy.ValidateCalculationBasis(
                new(100m, 20m, 10m, 0m, true, 1m, 132m, "calc/v1")));
        var accepted = RepairSpecificationPolicy.ValidateCalculationBasis(
            new(100m, 20m, 10m, 0m, true, 17m, 147m, "calc/v1"));
        Assert.Equal(147m, accepted.Total);
    }

    [Fact]
    public void AcceptedLinesMapOnceToTheThreeOrderedDisplaySections()
    {
        var accepted = Draft() with
        {
            State = RepairSpecificationState.Accepted,
            Lines =
            [
                Line("repair", 2, "Repair door"),
                Line("new_part", 1, "Door skin"),
                Line("paint_repair", 3, "Paint repaired area"),
                Line("specialist_fixed", 4, "Geometry check"),
            ],
        };
        var lists = RepairSpecificationPolicy.ToDisplayLists(accepted);
        Assert.Equal(["Door skin"], lists.NewParts);
        Assert.Equal(["Repair door"], lists.Repairs);
        Assert.Equal(["Paint repaired area", "Geometry check"], lists.AdditionalOperations);
    }

    [Fact]
    public void AcceptanceAllowsTheTwoAuditRolesButNeverAliasesThem()
    {
        var conservative = Draft() with
        {
            Purpose = RepairSpecificationPurpose.Audit,
            Role = RepairSpecificationRole.Conservative,
        };
        var maximised = conservative with
        {
            SpecificationId = Guid.NewGuid(),
            Role = RepairSpecificationRole.Maximised,
        };
        RepairSpecificationPolicy.ValidateAcceptance(conservative, Engineer);
        RepairSpecificationPolicy.ValidateAcceptance(maximised, Engineer);
        Assert.NotEqual(conservative.SpecificationId, maximised.SpecificationId);
        Assert.NotEqual(conservative.Role, maximised.Role);
    }

    private static RepairSpecificationVersion Draft() => new(
        Guid.NewGuid(), Guid.NewGuid(), 1,
        RepairSpecificationPurpose.OrdinaryAssessment,
        RepairSpecificationRole.Ordinary,
        RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, "case://estimate/1", "v1", new string('a', 64)),
        [Line("new_part", 1)],
        new(100m, 20m, 10m, 0m, true, 26m, 156m, "calc/v1"),
        "engineer", DateTimeOffset.UtcNow, null, null, null, null);

    private static CaseEstimateLineRecord Line(
        string type,
        int position,
        string description = "Test line",
        bool confirmed = true) => new(
        Guid.NewGuid(), position, type, null, description, null, null, false,
        null, null, null, null, null, ActorKind.Staff, "engineer", DateTimeOffset.UtcNow,
        confirmed ? "engineer" : null, confirmed ? DateTimeOffset.UtcNow : null);
}
