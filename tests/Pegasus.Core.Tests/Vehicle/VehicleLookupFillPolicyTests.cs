using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleLookupFillPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void ALookupFillsOnlyWhatTheCaseDoesNotAlreadyHold(
        bool hasFact,
        bool hasConfirmed,
        bool expected) =>
        Assert.Equal(expected, VehicleLookupFillPolicy.Fills(hasFact, hasConfirmed));

    [Fact]
    public void TheModelComesFromDvsaBesideTheDvlaDescription()
    {
        // VES answers no model at all, so before the DVSA vehicle object was
        // read every production lookup held a null model.
        var merged = VehicleLookupFillPolicy.Merge(
            new("TOYOTA", null, 2007, 2362, "PETROL"),
            new("TOYOTA", "ALPHARD", 2007, null, null));

        Assert.Equal(new VehicleDetails("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"), merged);
    }

    [Fact]
    public void DvsaAloneDescribesTheVehicle()
    {
        var merged = VehicleLookupFillPolicy.Merge(null, new("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"));

        Assert.Equal(new VehicleDetails("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"), merged);
    }

    [Fact]
    public void DvlaAloneDescribesTheVehicle()
    {
        var merged = VehicleLookupFillPolicy.Merge(new("FORD", null, 2020, 999, "PETROL"), null);

        Assert.Equal(new VehicleDetails("FORD", null, 2020, 999, "PETROL"), merged);
    }

    [Fact]
    public void NeitherProviderDescribingTheVehicleIsNoVehicle() =>
        Assert.Null(VehicleLookupFillPolicy.Merge(null, null));

    [Fact]
    public void ARecordThatDescribesNothingIsNoVehicle()
    {
        // An all-null merge is not evidence, and a blank member would fail
        // VehicleLookupResult.EnsureValidFor rather than read as absent.
        Assert.Null(VehicleLookupFillPolicy.Merge(
            new(null, null, null, null, null),
            new("  ", "", null, null, "\t")));
    }

    [Fact]
    public void BlankProviderMembersAreNormalisedToAbsent()
    {
        var merged = VehicleLookupFillPolicy.Merge(
            new(" ", null, 0, 0, ""),
            new("TOYOTA", " ALPHARD ", 2007, 2362, "PETROL"));

        Assert.Equal(new VehicleDetails("TOYOTA", "ALPHARD", 2007, 2362, "PETROL"), merged);
    }

    [Fact]
    public void DvlaAnswersFirstWhereTheProvidersDisagree()
    {
        var merged = VehicleLookupFillPolicy.Merge(
            new("FORD", null, 2020, 999, "PETROL"),
            new("TOYOTA", "ALPHARD", 2007, 2362, "DIESEL"));

        Assert.Equal(new VehicleDetails("FORD", "ALPHARD", 2020, 999, "PETROL"), merged);
    }
}
