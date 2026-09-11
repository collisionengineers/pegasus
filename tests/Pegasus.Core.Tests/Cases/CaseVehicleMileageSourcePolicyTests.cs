using Pegasus.Core.Cases;

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
        Assert.Equal("tbc", CaseVehicleMileageSourcePolicy.Resolve(null, hasMileage: false, null));
        Assert.Equal(
            "tbc",
            CaseVehicleMileageSourcePolicy.Resolve(
                CaseDataSourceKind.VehicleLookup,
                hasMileage: false,
                "owner"));
        Assert.Equal(
            "tbc",
            CaseVehicleMileageSourcePolicy.Resolve(
                CaseDataSourceKind.StaffCorrection,
                hasMileage: false,
                "repairer"));
    }

    [Fact]
    public void ALookupMileageIsOnlineData() =>
        Assert.Equal(
            "online_data",
            CaseVehicleMileageSourcePolicy.Resolve(
                CaseDataSourceKind.VehicleLookup,
                hasMileage: true,
                // The staff pick is ignored: staff did not supply this figure.
                "repairer"));

    [Theory]
    [InlineData("owner")]
    [InlineData("repairer")]
    [InlineData("principal")]
    public void AStaffMileageKeepsTheStaffChoice(string choice) =>
        Assert.Equal(
            choice,
            CaseVehicleMileageSourcePolicy.Resolve(
                CaseDataSourceKind.StaffCorrection,
                hasMileage: true,
                choice));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("online_data")]
    [InlineData("average")]
    [InlineData("tbc")]
    [InlineData("Owner")]
    public void AnUnusableStaffChoiceReadsAsTheOwner(string? choice)
    {
        // online_data, average and tbc are not staff's to pick, and the codes
        // are matched exactly; anything else falls back to who staff most
        // often ask.
        Assert.Equal(
            "owner",
            CaseVehicleMileageSourcePolicy.Resolve(
                CaseDataSourceKind.StaffCorrection,
                hasMileage: true,
                choice));
        Assert.Equal(
            "owner",
            CaseVehicleMileageSourcePolicy.Resolve(null, hasMileage: true, choice));
    }

    [Theory]
    [InlineData(CaseDataSourceKind.IntakeEvidence)]
    [InlineData(CaseDataSourceKind.MailRoute)]
    [InlineData(CaseDataSourceKind.CaseAcceptance)]
    [InlineData(CaseDataSourceKind.ProviderApi)]
    [InlineData(CaseDataSourceKind.ProviderSetting)]
    public void EveryOtherProvenanceCameThroughThePrincipal(CaseDataSourceKind provenance) =>
        Assert.Equal(
            "principal",
            CaseVehicleMileageSourcePolicy.Resolve(provenance, hasMileage: true, "owner"));

    [Fact]
    public void EveryResolvedCodeIsOneTheReportRenders()
    {
        string[] rendered = ["online_data", "owner", "repairer", "principal", "average", "tbc"];
        var resolved = Enum.GetValues<CaseDataSourceKind>()
            .SelectMany(provenance => new[]
            {
                CaseVehicleMileageSourcePolicy.Resolve(provenance, hasMileage: true, null),
                CaseVehicleMileageSourcePolicy.Resolve(provenance, hasMileage: false, null)
            })
            .Append(CaseVehicleMileageSourcePolicy.Resolve(null, hasMileage: true, "repairer"))
            .Distinct();

        Assert.All(resolved, code => Assert.Contains(code, rendered));
        Assert.Equal(["owner", "repairer", "principal"], CaseVehicleMileageSourcePolicy.StaffChoices);
    }
}
