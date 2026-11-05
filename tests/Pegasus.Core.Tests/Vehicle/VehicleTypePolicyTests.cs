using Pegasus.Core.Assessment;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleTypePolicyTests
{
    [Theory]
    [InlineData("M1", "car")]
    [InlineData("N1", "van")]
    [InlineData("L1e", "scooter")]
    [InlineData("L3e", "motorcycle")]
    [InlineData("N3", "other")]
    [InlineData("O2", "trailer")]
    [InlineData("T1", "other")]
    public void RecognisedTypeApprovalDeterminesTheVehicleType(
        string typeApproval,
        string expected)
    {
        var vehicle = Details(typeApproval: typeApproval);

        Assert.Equal(expected, VehicleTypePolicy.Classify(vehicle));
    }

    [Theory]
    [InlineData("2-WHEEL", "motorcycle")]
    [InlineData("2 WHEEL", "motorcycle")]
    [InlineData("3 WHEEL", "motorcycle")]
    [InlineData("ARTICULATED", "other")]
    [InlineData("NON STANDARD", null)]
    public void WheelplanProvidesTheFallback(string wheelplan, string? expected) =>
        Assert.Equal(expected, VehicleTypePolicy.Classify(Details(wheelplan: wheelplan)));

    // QCL26005-p: a 2017 Toyota Prius that VES answered with no type approval,
    // a rigid body and a revenue weight was classified a van. The weight
    // cannot tell a car from a van, so the type is left for staff
    // (operator, 5 November 2026).
    [Theory]
    [InlineData(null)]
    [InlineData(1_635)]
    [InlineData(7_500)]
    public void ARigidBodyWithNoTypeApprovalIsLeftForStaff(int? revenueWeightKg) =>
        Assert.Null(VehicleTypePolicy.Classify(
            Details(wheelplan: "2 AXLE RIGID BODY", revenueWeightKg: revenueWeightKg)));

    [Fact]
    public void ClassificationNormalisesCasingAndWhitespace()
    {
        var vehicle = Details(typeApproval: "  l 1 e  ");

        Assert.Equal("scooter", VehicleTypePolicy.Classify(vehicle));
    }

    [Fact]
    public void ASubmissionWithNoVehicleHasNoClassification() =>
        Assert.Null(VehicleTypePolicy.Classify(null));

    [Fact]
    public void EveryClassificationIsAVehicleTypeVocabularyCode()
    {
        var vehicles = new[]
        {
            Details(typeApproval: "M1"),
            Details(typeApproval: "N1"),
            Details(typeApproval: "L1e"),
            Details(typeApproval: "L3e"),
            Details(typeApproval: "O2"),
            Details(typeApproval: "T1"),
            Details(wheelplan: "2-WHEEL"),
            Details(wheelplan: "ARTICULATED")
        };
        var codes = AssessmentVocabulary.Definitions[AssessmentVocabulary.VehicleType].Codes!;

        Assert.All(
            vehicles.Select(VehicleTypePolicy.Classify).Where(code => code is not null),
            code => Assert.Contains(code!, codes));
    }

    private static VehicleDetails Details(
        string? typeApproval = null,
        string? wheelplan = null,
        int? revenueWeightKg = null) =>
        new(null, null, null, null, null, typeApproval, wheelplan, revenueWeightKg);
}
