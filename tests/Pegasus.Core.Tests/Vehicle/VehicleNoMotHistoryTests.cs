using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

/// <summary>
/// 9 October 2026 (a.QDOS26092): a lookup that answered for the Case's
/// registration with no MOT test has no mileage to give, and says so.
/// </summary>
public sealed class VehicleNoMotHistoryTests
{
    private const string Registration = "OE74EYP";

    [Theory]
    [InlineData("current", VehicleLookupOutcome.Current, null)]
    [InlineData("stale", VehicleLookupOutcome.Stale, null)]
    [InlineData("dvsa-404", VehicleLookupOutcome.Partial, VehicleLookupFailure.DvsaNotFound)]
    public void AnAnsweredLookupWithNoMotTestHasNoMotHistory(string row, VehicleLookupOutcome outcome, string? failureCode)
    {
        _ = row;
        Assert.True(VehicleMileagePolicy.HasNoMotHistory(Observation(outcome, failureCode, []), Registration));
    }

    [Fact]
    public void TheCaseRegistrationIsComparedInItsLookupForm() =>
        Assert.True(VehicleMileagePolicy.HasNoMotHistory(
            Observation(VehicleLookupOutcome.Current, null, []), "oe74 eyp"));

    [Theory]
    [InlineData("not-found", VehicleLookupOutcome.NotFound, null)]
    [InlineData("failed", VehicleLookupOutcome.Failed, "dvsa_failed_500")]
    [InlineData("unavailable", VehicleLookupOutcome.Unavailable, "dvsa_unavailable")]
    [InlineData("throttled", VehicleLookupOutcome.Throttled, "dvsa_throttled")]
    [InlineData("partial-other", VehicleLookupOutcome.Partial, "dvsa_unreadable_tests")]
    [InlineData("partial-dvla", VehicleLookupOutcome.Partial, VehicleLookupFailure.DvlaNotFound)]
    public void ALookupThatDidNotReadTheMotHistoryClaimsNothing(string row, VehicleLookupOutcome outcome, string? failureCode)
    {
        _ = row;
        Assert.False(VehicleMileagePolicy.HasNoMotHistory(Observation(outcome, failureCode, []), Registration));
    }

    [Fact]
    public void AVehicleWithAnMotTestHasHistory() =>
        Assert.False(VehicleMileagePolicy.HasNoMotHistory(
            Observation(VehicleLookupOutcome.Current, null, [new(new(2025, 9, 20), "PASSED", new(2026, 9, 24), 30_000, VehicleMileageUnit.Miles)]),
            Registration));

    [Fact]
    public void AnAnswerForAnotherRegistrationClaimsNothing() =>
        Assert.False(VehicleMileagePolicy.HasNoMotHistory(
            Observation(VehicleLookupOutcome.Current, null, []), "AB12CDE"));

    [Fact]
    public void NoLookupOrNoRegistrationClaimsNothing()
    {
        Assert.False(VehicleMileagePolicy.HasNoMotHistory(null, Registration));
        Assert.False(VehicleMileagePolicy.HasNoMotHistory(Observation(VehicleLookupOutcome.Current, null, []), null));
    }

    private static VehicleLookupObservation Observation(
        VehicleLookupOutcome outcome,
        string? failureCode,
        IReadOnlyList<MotTestObservation> tests)
    {
        var at = new DateTimeOffset(2026, 10, 9, 7, 17, 1, TimeSpan.Zero);
        return new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            outcome,
            Registration,
            new("dvla-ves+dvsa-mot-history", "ves-1.2+mot-history-v1", "identity", at, at, at),
            null,
            tests,
            null,
            failureCode is null ? null : new(failureCode, Retryable: false),
            at);
    }
}
