using Pegasus.Core.Cases;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// The report says where the odometer figure came from. It is derived from the
/// mileage's own provenance, so the sentence and the figure cannot disagree.
/// </summary>
public sealed class CaseVehicleMileageSourcePolicyTests
{
    [Fact]
    public void NoMileageIsAlwaysToBeConfirmed()
    {
        Assert.Equal("tbc", CaseVehicleMileageSourcePolicy.Resolve(null, hasMileage: false));
        Assert.Equal(
            "tbc",
            CaseVehicleMileageSourcePolicy.Resolve(CaseDataSourceKind.VehicleLookup, hasMileage: false));
        Assert.Equal(
            "tbc",
            CaseVehicleMileageSourcePolicy.Resolve(CaseDataSourceKind.StaffCorrection, hasMileage: false));
    }

    [Fact]
    public void ALookupMileageIsOnlineData() =>
        Assert.Equal(
            "online_data",
            CaseVehicleMileageSourcePolicy.Resolve(CaseDataSourceKind.VehicleLookup, hasMileage: true));

    [Fact]
    public void ACustomMileagePrintsAsOnlineData()
    {
        // Staff typed the figure: the Case page reads Custom, and the report
        // prints the online data sentence (operator, 12 October 2026).
        Assert.Equal(
            "online_data",
            CaseVehicleMileageSourcePolicy.Resolve(CaseDataSourceKind.StaffCorrection, hasMileage: true));
        Assert.Equal("online_data", CaseVehicleMileageSourcePolicy.Resolve(null, hasMileage: true));
    }

    [Fact]
    public void AnEstimatedLookupIsRecognisedAndStillResolvesToOnlineData()
    {
        var estimated = new CaseDataSource(
            CaseDataSourceKind.VehicleLookup,
            "vehicle-lookup",
            "vehicle lookup",
            MotMileageEstimationPolicy.MethodKey,
            MotMileageEstimationPolicy.MethodVersion);

        Assert.True(CaseVehicleMileageSourcePolicy.IsEstimated(estimated));
        Assert.Equal("online_data", CaseVehicleMileageSourcePolicy.Resolve(estimated.Kind, hasMileage: true));

        // An exact MOT reading, a staff figure or no source is not an estimate.
        Assert.False(CaseVehicleMileageSourcePolicy.IsEstimated(estimated with
        {
            PolicyKey = VehicleMileagePolicy.MethodKey,
            PolicyVersion = VehicleMileagePolicy.MethodVersion
        }));
        Assert.False(CaseVehicleMileageSourcePolicy.IsEstimated(
            estimated with { Kind = CaseDataSourceKind.StaffCorrection }));
        Assert.False(CaseVehicleMileageSourcePolicy.IsEstimated(null));
    }

    [Theory]
    [InlineData(CaseDataSourceKind.IntakeEvidence)]
    [InlineData(CaseDataSourceKind.MailRoute)]
    [InlineData(CaseDataSourceKind.CaseAcceptance)]
    [InlineData(CaseDataSourceKind.PrincipalApi)]
    [InlineData(CaseDataSourceKind.PrincipalSetting)]
    public void EveryOtherProvenanceCameThroughThePrincipal(CaseDataSourceKind provenance) =>
        Assert.Equal("principal", CaseVehicleMileageSourcePolicy.Resolve(provenance, hasMileage: true));

    [Fact]
    public void EveryResolvedCodeIsOneTheReportRenders()
    {
        string[] rendered = ["online_data", "owner", "repairer", "principal", "average", "tbc"];
        var resolved = Enum.GetValues<CaseDataSourceKind>()
            .SelectMany(provenance => new[]
            {
                CaseVehicleMileageSourcePolicy.Resolve(provenance, hasMileage: true),
                CaseVehicleMileageSourcePolicy.Resolve(provenance, hasMileage: false)
            })
            .Append(CaseVehicleMileageSourcePolicy.Resolve(null, hasMileage: true))
            .Distinct();

        Assert.All(resolved, code => Assert.Contains(code, rendered));
    }
}
