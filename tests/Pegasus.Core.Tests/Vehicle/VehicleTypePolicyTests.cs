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
    [InlineData("2-WHEEL", null, "motorcycle")]
    [InlineData("2 AXLE RIGID BODY", 0, "car")]
    [InlineData("2 AXLE RIGID BODY", 1_800, "van")]
    [InlineData("2 AXLE RIGID BODY", 7_500, "other")]
    [InlineData("NON STANDARD", null, null)]
    public void WheelplanAndRigidBodyRevenueWeightProvideTheFallback(
        string wheelplan,
        int? revenueWeightKg,
        string? expected)
    {
        var vehicle = Details(wheelplan: wheelplan, revenueWeightKg: revenueWeightKg);

        Assert.Equal(expected, VehicleTypePolicy.Classify(vehicle));
    }

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
            Details(wheelplan: "2 AXLE RIGID BODY", revenueWeightKg: 7_500)
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
