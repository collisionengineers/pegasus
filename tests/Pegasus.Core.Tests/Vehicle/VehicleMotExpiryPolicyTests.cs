using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleMotExpiryPolicyTests
{
    [Fact]
    public void TheLatestExpiryAcrossTheTestsIsTheMotExpiry()
    {
        var expiry = VehicleMotExpiryPolicy.Latest(
        [
            Test(new(2024, 9, 20), "PASSED", new(2025, 9, 24)),
            Test(new(2025, 9, 20), "PASSED", new(2026, 9, 24)),
            Test(new(2023, 9, 20), "PASSED", new(2024, 9, 24)),
        ]);

        Assert.Equal(new DateOnly(2026, 9, 24), expiry);
    }

    [Fact]
    public void ALaterFailureKeepsTheEarlierPassExpiry()
    {
        // DVSA records no expiry on a failure, so the last pass's certificate
        // still stands.
        var expiry = VehicleMotExpiryPolicy.Latest(
        [
            Test(new(2025, 9, 20), "PASSED", new(2026, 9, 24)),
            Test(new(2026, 3, 1), "FAILED", null),
        ]);

        Assert.Equal(new DateOnly(2026, 9, 24), expiry);
    }

    [Fact]
    public void TestsWithoutAnExpiryGiveNoMotExpiry() =>
        Assert.Null(VehicleMotExpiryPolicy.Latest(
        [
            Test(new(2025, 9, 20), "FAILED", null),
            Test(new(2025, 9, 27), "FAILED", null),
        ]));

    [Fact]
    public void NoTestsGiveNoMotExpiry() =>
        Assert.Null(VehicleMotExpiryPolicy.Latest([]));

    [Fact]
    public void NullObservationsAreRefused() =>
        Assert.Throws<ArgumentNullException>(() => VehicleMotExpiryPolicy.Latest(null!));

    private static MotTestObservation Test(DateOnly testDate, string status, DateOnly? expiryDate) =>
        new(testDate, status, expiryDate, 50_000, VehicleMileageUnit.Miles);
}
