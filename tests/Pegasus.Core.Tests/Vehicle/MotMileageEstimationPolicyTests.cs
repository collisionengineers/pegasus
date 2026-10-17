using System.Text.Json;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

/// <summary>
/// FRD-06's conservative MOT mileage estimation over invented chronological
/// histories built here, counted in days from one invented date.
/// </summary>
public sealed class MotMileageEstimationPolicyTests
{
    private static readonly DateOnly Origin = new(2030, 1, 15);

    // Four 365-day intervals at 25, 30, 27 and 29 miles a day.
    private static readonly MotTestObservation[] Steady =
    [
        Passed(0, 20_000),
        Passed(365, 29_125),
        Passed(730, 40_075),
        Passed(1_095, 49_930),
        Passed(1_460, 60_515)
    ];

    // The same odometer, then a replacement the next reading confirms.
    private static readonly MotTestObservation[] Replaced =
    [
        Passed(0, 50_000),
        Passed(365, 60_000),
        Passed(730, 5_000),
        Passed(1_095, 15_000)
    ];

    [Fact]
    public void ResultIsIndependentOfInputOrder()
    {
        MotTestObservation[] history =
        [
            .. Steady,
            Test(365, "FAILED", 29_125),
            Test(1_100, "PASSED", null),
            Test(1_460, "passed", 60_515),
            new(On(1_460), "PASSED", null, 97_389, VehicleMileageUnit.Kilometres)
        ];
        MotTestObservation[][] orders =
        [
            [.. Enumerable.Reverse(history)],
            [.. history.Skip(4), .. history.Take(4)],
            [.. history.Where((_, index) => index % 2 == 1), .. history.Where((_, index) => index % 2 == 0)]
        ];
        int[] days = [-10, 1_095, 1_195, 1_825, 1_826, 2_000];

        foreach (var day in days)
        {
            var expected = Json(MotMileageEstimationPolicy.Estimate(history, On(day)));

            Assert.All(orders, order =>
                Assert.Equal(expected, Json(MotMileageEstimationPolicy.Estimate(order, On(day)))));
            // Integers and decimals only, so a stored copy reads back unchanged.
            Assert.Equal(expected, Json(JsonSerializer.Deserialize<MotMileageEstimate>(expected)!));
        }
    }

    [Fact]
    public void UnrecognisedUnitsAreSetAsideNotDeleted()
    {
        MotTestObservation[] history =
        [
            Passed(0, 20_000),
            new(On(100), "PASSED", null, null, VehicleMileageUnit.Miles),
            new(On(200), "PASSED", null, 25_000, null),
            new(On(300), "PASSED", null, 27_000, (VehicleMileageUnit)99),
            new(On(400), "PASSED", null, -5, VehicleMileageUnit.Miles),
            new(On(500), "PASSED", null, 48_280, VehicleMileageUnit.Kilometres)
        ];

        var evidence = MotMileageEstimationPolicy.Estimate(history, On(600)).Evidence;

        Assert.Equal(history, evidence.Observations);
        MotMileageSetAsideObservation[] setAside =
        [
            new(history[1], MotReadingUnusableReason.NoMileage),
            new(history[2], MotReadingUnusableReason.NoUnit),
            new(history[3], MotReadingUnusableReason.UndefinedUnit),
            new(history[4], MotReadingUnusableReason.NegativeMileage)
        ];
        Assert.Equal(setAside, evidence.SetAsideObservations);
        MotMileageReading[] readings =
        [
            new(On(0), On(0), 20_000, null),
            new(On(500), On(500), VehicleMileagePolicy.ToMiles(48_280, VehicleMileageUnit.Kilometres), null)
        ];
        Assert.Equal(readings, evidence.Readings);
        Assert.Equal(30_000L, evidence.Readings[1].Miles);
    }

    [Fact]
    public void FailAndRetestIsOneEpisode()
    {
        MotTestObservation[] history =
        [
            Passed(0, 20_000),
            Test(365, "FAILED", 30_000),
            Test(375, "passed", 30_010),
            Test(730, "failed", 40_000),
            Test(745, "FAILED", 40_005),
            // 30 days after the second failure: still its retest.
            Test(775, "PASSED", 40_020),
            Test(1_095, "FAILED", 50_000),
            // 31 days after: a test of its own.
            Test(1_126, "PASSED", 50_100)
        ];

        var evidence = MotMileageEstimationPolicy.Estimate(history, On(1_200)).Evidence;

        MotMileageReading[] readings =
        [
            new(On(0), On(0), 20_000, null),
            new(On(365), On(375), 30_010, null),
            new(On(730), On(775), 40_020, null),
            new(On(1_095), On(1_095), 50_000, null),
            new(On(1_126), On(1_126), 50_100, null)
        ];
        Assert.Equal(readings, evidence.Readings);
        int[] spans = [375, 400, 320, 31];
        Assert.Equal(spans, evidence.Intervals.Select(interval => interval.Days));

        // Disagreeing readings on the retest's day leave the episode unread,
        // though the failed test before them read the odometer.
        var conflicting = MotMileageEstimationPolicy.Estimate(
            [Passed(0, 20_000), Test(365, "FAILED", 30_000), Test(370, "PASSED", 30_050), Test(370, "PASSED", 30_500)],
            On(400)).Evidence;
        Assert.Equal(
            new MotMileageReading(On(365), On(370), null, MotMileageSetAsideReason.ConflictingReadings),
            conflicting.Readings[1]);
    }

    [Fact]
    public void ImplausibleAndShortIntervalsAreSetAsideAndKept()
    {
        MotTestObservation[] history =
        [
            Passed(0, 10_000),
            Passed(365, 20_000),
            Passed(425, 21_000),
            Passed(790, 31_000),
            Passed(1_155, 177_000)
        ];

        var evidence = MotMileageEstimationPolicy.Estimate(history, On(1_200)).Evidence;

        MotMileageSetAsideReason?[] reasons =
        [
            null,
            MotMileageSetAsideReason.ShortSpan,
            null,
            MotMileageSetAsideReason.ImplausibleRate
        ];
        Assert.Equal(reasons, evidence.Intervals.Select(interval => interval.SetAsideReason));
        Assert.Equal(60, evidence.Intervals[1].Days);
        Assert.Equal(400m, evidence.Intervals[3].MilesPerDay);
        // Kept with no weight in the rate; every reading stays in the segment.
        Assert.Null(evidence.Intervals[1].Weight);
        Assert.Null(evidence.Intervals[3].Weight);
        Assert.NotNull(evidence.Intervals[0].Weight);
        Assert.NotNull(evidence.Intervals[2].Weight);
        Assert.Equal(
            history.Select(test => new MotMileagePoint(test.TestDate, test.Mileage!.Value)),
            Assert.Single(evidence.Segments).Readings);
    }

    [Fact]
    public void ACorroboratedDropStartsASegmentALoneDropDoesNot()
    {
        var replaced = MotMileageEstimationPolicy.Estimate(Replaced, On(1_200)).Evidence;

        Assert.Equal(2, replaced.Segments.Count);
        MotMileagePoint[] newOdometer = [new(On(730), 5_000), new(On(1_095), 15_000)];
        Assert.Equal(newOdometer, replaced.Segments[1].Readings);
        Assert.Equal(MotMileageSetAsideReason.NegativeDelta, replaced.Intervals[1].SetAsideReason);
        Assert.All(replaced.Readings, reading => Assert.Null(reading.SetAsideReason));

        // 6,000 then 70,000: the next reading is at or above the post-drop
        // reading, so the drop is corroborated however far the next one climbs.
        var climbing = MotMileageEstimationPolicy.Estimate(
            [Passed(0, 50_000), Passed(365, 60_000), Passed(730, 6_000), Passed(1_095, 70_000)],
            On(1_200)).Evidence;

        Assert.Equal(2, climbing.Segments.Count);
        MotMileagePoint[] fromTheDrop = [new(On(730), 6_000), new(On(1_095), 70_000)];
        Assert.Equal(fromTheDrop, climbing.Segments[1].Readings);
        Assert.All(climbing.Readings, reading => Assert.Null(reading.SetAsideReason));

        // 6,000 then 5,000, which nothing follows: neither drop is
        // corroborated, so both are set aside and the segment continues from
        // 60,000.
        var lone = MotMileageEstimationPolicy.Estimate(
            [Passed(0, 50_000), Passed(365, 60_000), Passed(730, 6_000), Passed(1_095, 5_000)],
            On(1_200)).Evidence;

        MotMileagePoint[] kept = [new(On(0), 50_000), new(On(365), 60_000)];
        Assert.Equal(kept, Assert.Single(lone.Segments).Readings);
        MotMileageSetAsideReason?[] readingReasons =
        [
            null,
            null,
            MotMileageSetAsideReason.UncorroboratedDrop,
            MotMileageSetAsideReason.UncorroboratedDrop
        ];
        Assert.Equal(readingReasons, lone.Readings.Select(reading => reading.SetAsideReason));
        MotMileageSetAsideReason?[] intervalReasons =
        [
            null,
            MotMileageSetAsideReason.NegativeDelta,
            MotMileageSetAsideReason.NegativeDelta
        ];
        Assert.Equal(intervalReasons, lone.Intervals.Select(interval => interval.SetAsideReason));
        Assert.Equal(60_000L, lone.Intervals[2].StartMiles);
        Assert.Equal(730, lone.Intervals[2].Days);
    }

    [Fact]
    public void RateIsAWeightedMedianNotAMean()
    {
        // Rates 25, 30, 27 and 29 a day: the mean is 27.75, and the newest
        // intervals weigh most.
        var steady = MotMileageEstimationPolicy.Estimate(Steady, On(1_500)).Evidence;

        Assert.Equal(29m, Assert.Single(steady.Segments).MilesPerDay);
        decimal?[] weights = [146m, 182.5m, 266_450m / 1_095, 365m];
        Assert.Equal(weights, steady.Intervals.Select(interval => interval.Weight));

        // Rates 20, 22, 290 and 21 a day: one plausible outlier moves the mean
        // to 88.25 and leaves the median where the others are.
        var outlier = MotMileageEstimationPolicy.Estimate(
            [Passed(0, 10_000), Passed(365, 17_300), Passed(730, 25_330), Passed(1_095, 131_180), Passed(1_460, 138_845)],
            On(1_500)).Evidence;

        Assert.Equal(21m, Assert.Single(outlier.Segments).MilesPerDay);

        // Spans of 500 and 400 days both count as QualitySpanCapDays.
        var capped = MotMileageEstimationPolicy.Estimate(
            [Passed(0, 10_000), Passed(500, 20_000), Passed(900, 30_000)],
            On(1_000)).Evidence;

        decimal?[] cappedWeights = [365m * 730 / 1_130, 365m];
        Assert.Equal(cappedWeights, capped.Intervals.Select(interval => interval.Weight));
    }

    [Fact]
    public void ExactDateDelegatesToTheLatestObservationPolicy()
    {
        var exact = MotMileageEstimationPolicy.Estimate(Steady, On(1_095));

        Assert.Equal(MotMileageEstimateKind.Exact, exact.Kind);
        Assert.Equal(VehicleMileagePolicy.Calculate([Steady[3]])!.Value, exact.Miles);
        Assert.Equal("latest-mot-observation", exact.MethodKey);
        Assert.Equal(2, exact.MethodVersion);
        Assert.Null(exact.Low);
        Assert.Null(exact.High);

        // A failed test's own date reads its own odometer, in miles, though
        // its episode reads as the retest.
        var failed = MotMileageEstimationPolicy.Estimate(
            [Passed(0, 20_000), new(On(365), "FAILED", null, 48_280, VehicleMileageUnit.Kilometres), Passed(380, 30_020)],
            On(365));

        Assert.Equal(MotMileageEstimateKind.Exact, failed.Kind);
        Assert.Equal(VehicleMileagePolicy.ToMiles(48_280, VehicleMileageUnit.Kilometres), failed.Miles);

        // Off an MOT date the figure is this method's own.
        var between = MotMileageEstimationPolicy.Estimate(Steady, On(1_195));
        Assert.Equal(MotMileageEstimationPolicy.MethodKey, between.MethodKey);
        Assert.Equal(MotMileageEstimationPolicy.MethodVersion, between.MethodVersion);
    }

    [Fact]
    public void InterpolatesOnlyInsideASegment()
    {
        // 100 of the 365 days from 49,930 to 60,515, within the widest
        // holdout miss of 3 a day.
        var inside = MotMileageEstimationPolicy.Estimate(Steady, On(1_195));

        Assert.Equal(MotMileageEstimateKind.Estimate, inside.Kind);
        Assert.Equal(52_830L, inside.Miles);
        Assert.Equal(52_530L, inside.Low);
        Assert.Equal(53_130L, inside.High);

        // Between the old odometer's 60,000 and the new one's 5,000 either
        // could be fitted: a range from zero, not a blend of the two.
        var across = MotMileageEstimationPolicy.Estimate(Replaced, On(500));

        Assert.Equal(MotMileageEstimateKind.QualifiedRange, across.Kind);
        Assert.Null(across.Miles);
        Assert.Equal(0L, across.Low);
        Assert.Equal(60_000L + (135 * MotMileageEstimationPolicy.MaxPlausibleMilesPerDay), across.High);

        // Inside the new odometer's segment, with no holdout yet.
        var second = MotMileageEstimationPolicy.Estimate(Replaced, On(900));

        Assert.Equal(MotMileageEstimateKind.QualifiedRange, second.Kind);
        Assert.Equal(5_000L, second.Low);
        Assert.Equal(15_000L, second.High);
    }

    [Fact]
    public void ForecastAtTheHorizonIsAnEstimatePastItIsARange()
    {
        var atHorizon = MotMileageEstimationPolicy.Estimate(Steady, On(1_825));

        Assert.Equal(365, Assert.Single(atHorizon.Evidence.Segments).HorizonDays);
        Assert.Equal(MotMileageEstimateKind.Estimate, atHorizon.Kind);
        Assert.False(atHorizon.NonProbabilistic);
        // 60,515 + 29 a day for 365 days, give or take 3 a day.
        Assert.Equal(71_100L, atHorizon.Miles);
        Assert.Equal(70_005L, atHorizon.Low);
        Assert.Equal(72_195L, atHorizon.High);

        var past = MotMileageEstimationPolicy.Estimate(Steady, On(1_826));

        Assert.Equal(MotMileageEstimateKind.QualifiedRange, past.Kind);
        Assert.True(past.NonProbabilistic);
        Assert.Null(past.Miles);
        Assert.Equal(60_515L, past.Low);
        Assert.Equal(60_515L + (366 * MotMileageEstimationPolicy.MaxPlausibleMilesPerDay), past.High);
    }

    [Fact]
    public void IntervalsComeOnlyFromChronologicalHoldouts()
    {
        var segment = Assert.Single(MotMileageEstimationPolicy.Estimate(Steady, On(1_825)).Evidence.Segments);

        // The first two readings have too few clean intervals before them;
        // 49,930 is predicted at 30 a day from the first three readings and
        // 60,515 at 27 a day from the first four.
        MotMileageHoldout[] holdouts =
        [
            new(On(1_095), 49_930, 365, 51_025, -1_095),
            new(On(1_460), 60_515, 365, 59_785, 730)
        ];
        Assert.Equal(holdouts, segment.Holdouts);

        // A later reading never changes an earlier prediction.
        var revised = MotMileageEstimationPolicy.Estimate([.. Steady[..4], Passed(1_460, 70_000)], On(1_825));

        Assert.Equal(holdouts[0], Assert.Single(revised.Evidence.Segments).Holdouts[0]);
    }

    [Fact]
    public void SparseHistoryGivesARangeNeverAPointOrAPrior()
    {
        var single = MotMileageEstimationPolicy.Estimate([Passed(0, 30_000)], On(200));

        Assert.Equal(MotMileageEstimateKind.QualifiedRange, single.Kind);
        Assert.Null(single.Miles);
        Assert.Equal(30_000L, single.Low);
        Assert.Equal(30_000L + (200 * MotMileageEstimationPolicy.MaxPlausibleMilesPerDay), single.High);

        // Two clean intervals give a rate, but nothing has tested it yet.
        var three = MotMileageEstimationPolicy.Estimate(Steady[..3], On(830));
        var segment = Assert.Single(three.Evidence.Segments);

        Assert.Equal(30m, segment.MilesPerDay);
        Assert.Empty(segment.Holdouts);
        Assert.Equal(MotMileageEstimateKind.QualifiedRange, three.Kind);
        Assert.Null(three.Miles);
        Assert.Equal(40_075L, three.Low);
        Assert.Equal(40_075L + (100 * MotMileageEstimationPolicy.MaxPlausibleMilesPerDay), three.High);

        // Before the first reading the floor is zero.
        var before = MotMileageEstimationPolicy.Estimate(Steady, On(-100));

        Assert.Equal(MotMileageEstimateKind.QualifiedRange, before.Kind);
        Assert.Equal(0L, before.Low);
        Assert.Equal(20_000L, before.High);
    }

    [Fact]
    public void NoHistoryAndConflictsAbstain()
    {
        var none = MotMileageEstimationPolicy.Estimate([], On(0));

        Assert.Equal(MotMileageEstimateKind.Abstention, none.Kind);
        Assert.Equal(MotMileageAbstentionReason.NoMotHistory, none.AbstentionReason);
        Assert.Null(none.Miles);
        Assert.Null(none.Low);
        Assert.Null(none.High);

        var unusable = MotMileageEstimationPolicy.Estimate(
            [new(On(0), "PASSED", null, null, null), new(On(365), "PASSED", null, -1, VehicleMileageUnit.Miles)],
            On(400));

        Assert.Equal(MotMileageAbstentionReason.NoUsableReadings, unusable.AbstentionReason);

        MotTestObservation[] disagreeing =
        [
            Passed(0, 30_000),
            new(On(0), "PASSED", null, 48_280, VehicleMileageUnit.Kilometres),
            Passed(365, 40_000),
            Passed(365, 40_500)
        ];

        // The same reading in two units agrees; two different readings do not.
        Assert.Equal(MotMileageEstimateKind.Exact, MotMileageEstimationPolicy.Estimate(disagreeing, On(0)).Kind);
        var onConflict = MotMileageEstimationPolicy.Estimate(disagreeing, On(365));
        Assert.Equal(MotMileageEstimateKind.Abstention, onConflict.Kind);
        Assert.Equal(MotMileageAbstentionReason.ConflictingReadings, onConflict.AbstentionReason);

        var everyDayConflicts = MotMileageEstimationPolicy.Estimate([Passed(0, 30_000), Passed(0, 30_500)], On(100));
        Assert.Equal(MotMileageAbstentionReason.ConflictingReadings, everyDayConflicts.AbstentionReason);
    }

    [Theory]
    [InlineData("exact-agrees", true, 1_095, 49_930L, false)]
    [InlineData("exact-differs", true, 1_095, 49_931L, true)]
    [InlineData("estimate-low-edge", true, 1_825, 70_005L, false)]
    [InlineData("estimate-high-edge", true, 1_825, 72_195L, false)]
    [InlineData("estimate-below", true, 1_825, 70_004L, true)]
    [InlineData("estimate-above", true, 1_825, 72_196L, true)]
    [InlineData("range-inside", true, 1_826, 100_000L, false)]
    [InlineData("range-below", true, 1_826, 60_514L, true)]
    [InlineData("range-above", true, 1_826, 170_316L, true)]
    [InlineData("abstention", false, 0, 123_456L, false)]
    public void ContradictsFollowsTheKind(string row, bool withHistory, int day, long caseMiles, bool contradicts)
    {
        _ = row;
        var estimate = MotMileageEstimationPolicy.Estimate(withHistory ? Steady : [], On(day));

        Assert.Equal(contradicts, estimate.Contradicts(caseMiles));
    }

    private static DateOnly On(int day) => Origin.AddDays(day);

    private static MotTestObservation Passed(int day, long miles) => Test(day, "PASSED", miles);

    private static MotTestObservation Test(int day, string status, long? miles) =>
        new(On(day), status, null, miles, VehicleMileageUnit.Miles);

    private static string Json(MotMileageEstimate estimate) => JsonSerializer.Serialize(estimate);
}
