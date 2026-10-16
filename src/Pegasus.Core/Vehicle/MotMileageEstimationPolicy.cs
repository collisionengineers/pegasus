namespace Pegasus.Core.Vehicle;

/// <summary>What the MOT history can say about the mileage on one date.</summary>
public enum MotMileageEstimateKind
{
    /// <summary>An MOT test on that date read the odometer.</summary>
    Exact,

    /// <summary>A figure derived from the history, with an interval from its chronological holdouts.</summary>
    Estimate,

    /// <summary>Bounds only, explicitly non-probabilistic; never written to the Case.</summary>
    QualifiedRange,

    /// <summary>The history gives no answer.</summary>
    Abstention
}

public enum MotMileageAbstentionReason
{
    /// <summary>DVSA holds no MOT test, as <see cref="VehicleMileagePolicy.HasNoMotHistory"/>.</summary>
    NoMotHistory,

    /// <summary>Every MOT test was set aside by the unit filter.</summary>
    NoUsableReadings,

    /// <summary>The readings that would answer disagree on the same day.</summary>
    ConflictingReadings
}

/// <summary>Why an observation, reading or interval takes no part in the rate.</summary>
public enum MotMileageSetAsideReason
{
    NoMileage,
    NoUnit,
    UndefinedUnit,
    NegativeMileage,

    /// <summary>The episode's last same-day readings disagree after conversion.</summary>
    ConflictingReadings,

    /// <summary>A drop the next reading does not confirm: the post-drop reading.</summary>
    UncorroboratedDrop,

    NegativeDelta,
    ShortSpan,
    ImplausibleRate
}

/// <summary>An MOT test the unit filter set aside; it is kept, never deleted.</summary>
public sealed record MotMileageSetAsideObservation(
    MotTestObservation Observation,
    MotMileageSetAsideReason Reason);

/// <summary>
/// One episode (a test, or a failed test and its retests) read as its last
/// usable reading in miles. <paramref name="Miles"/> is null only when that
/// day's readings disagree.
/// </summary>
public sealed record MotMileageReading(
    DateOnly EpisodeStartedOn,
    DateOnly ObservedOn,
    long? Miles,
    MotMileageSetAsideReason? SetAsideReason);

public sealed record MotMileagePoint(DateOnly ObservedOn, long Miles);

/// <summary>
/// Two consecutive readings. <paramref name="Weight"/> is the interval's
/// weight in its segment's rate, present only for a clean interval.
/// </summary>
public sealed record MotMileageInterval(
    DateOnly StartedOn,
    long StartMiles,
    DateOnly EndedOn,
    long EndMiles,
    int Days,
    decimal MilesPerDay,
    decimal? Weight,
    MotMileageSetAsideReason? SetAsideReason);

/// <summary>A reading predicted from the segment's earlier readings only.</summary>
public sealed record MotMileageHoldout(
    DateOnly ObservedOn,
    long Miles,
    int Days,
    long PredictedMiles,
    long ErrorMiles);

/// <summary>
/// Readings on one odometer. <paramref name="HorizonDays"/> is the longest
/// span any of its holdouts predicted across.
/// </summary>
public sealed record MotMileageSegment(
    IReadOnlyList<MotMileagePoint> Readings,
    decimal? MilesPerDay,
    IReadOnlyList<MotMileageHoldout> Holdouts,
    int? HorizonDays);

/// <summary>
/// Everything the answer rests on, each kept distinct: the raw tests, the
/// normalised readings, what was set aside and why, the segments with their
/// rate and holdouts, and the method that assembled them.
/// </summary>
public sealed record MotMileageEvidence(
    string MethodKey,
    int MethodVersion,
    IReadOnlyList<MotTestObservation> Observations,
    IReadOnlyList<MotMileageSetAsideObservation> SetAsideObservations,
    IReadOnlyList<MotMileageReading> Readings,
    IReadOnlyList<MotMileageInterval> Intervals,
    IReadOnlyList<MotMileageSegment> Segments);

/// <summary>
/// The answer for <paramref name="AsOf"/>, always in miles. Only an Exact or
/// an Estimate carries <paramref name="Miles"/>; an Estimate's interval and a
/// QualifiedRange's bounds are <paramref name="Low"/> and
/// <paramref name="High"/>. <paramref name="MethodKey"/> and
/// <paramref name="MethodVersion"/> are those of the figure: an Exact is
/// <see cref="VehicleMileagePolicy"/>'s reading, anything else this method's.
/// </summary>
public sealed record MotMileageEstimate(
    MotMileageEstimateKind Kind,
    DateOnly AsOf,
    long? Miles,
    long? Low,
    long? High,
    MotMileageAbstentionReason? AbstentionReason,
    string MethodKey,
    int MethodVersion,
    MotMileageEvidence Evidence)
{
    /// <summary>A qualified range states bounds, not a likelihood.</summary>
    public bool NonProbabilistic => Kind == MotMileageEstimateKind.QualifiedRange;

    /// <summary>
    /// Whether a Case mileage, already in miles (<see cref="VehicleMileagePolicy.ToMiles"/>),
    /// disagrees with this answer. An abstention contradicts nothing.
    /// </summary>
    public bool Contradicts(long caseMiles) => Kind switch
    {
        MotMileageEstimateKind.Exact => caseMiles != Miles,
        MotMileageEstimateKind.Estimate or MotMileageEstimateKind.QualifiedRange =>
            caseMiles < Low || caseMiles > High,
        _ => false
    };
}

/// <summary>
/// The conservative MOT mileage estimation FRD-06 describes. It keeps every
/// raw test, reads fail-and-retest episodes as one, sets implausible or
/// low-information intervals aside without deleting them, starts a new
/// segment only at a corroborated odometer drop, and derives a rate from a
/// recency- and quality-weighted median of one segment's clean intervals. No
/// cohort prior is read: a sparse or unsafe history gives a qualified range or
/// an abstention, never an invented figure.
/// </summary>
public static class MotMileageEstimationPolicy
{
    public const string MethodKey = "mot-mileage-estimate";

    /// <summary>Changing any tuning constant below bumps this version.</summary>
    public const int MethodVersion = 1;

    /// <summary>Tests within this many days after a failed test are its retests.</summary>
    public const int RetestWindowDays = 30;

    /// <summary>An interval shorter than this carries too little to give a rate.</summary>
    public const int MinIntervalDays = 90;

    /// <summary>A rate above this is implausible; it is also the qualified range's ceiling.</summary>
    public const int MaxPlausibleMilesPerDay = 300;

    /// <summary>An interval ending this many days before the latest reading weighs half as much.</summary>
    public const int RecencyHalfLifeDays = 730;

    /// <summary>An interval's span counts towards its weight up to this many days.</summary>
    public const int QualitySpanCapDays = 365;

    /// <summary>Fewer clean intervals than this give no rate and no eligible holdout.</summary>
    public const int MinCleanIntervals = 2;

    private const string FailedTestStatus = "FAILED";

    /// <summary>The mileage on <paramref name="asOf"/>; the same answer for any input order.</summary>
    public static MotMileageEstimate Estimate(IReadOnlyList<MotTestObservation> observations, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var ordered = observations
            .OrderBy(item => item.TestDate)
            .ThenBy(item => item.TestStatus, StringComparer.Ordinal)
            .ThenBy(item => item.ExpiryDate)
            .ThenBy(item => item.Mileage)
            .ThenBy(item => item.MileageUnit)
            .ToArray();
        var setAside = new List<MotMileageSetAsideObservation>();
        foreach (var observation in ordered)
        {
            if (SetAsideReason(observation) is { } reason)
            {
                setAside.Add(new(observation, reason));
            }
        }

        var readings = EpisodeReadings(ordered);
        var (intervals, segments) = Segment(readings);
        var evidence = new MotMileageEvidence(
            MethodKey, MethodVersion, ordered, setAside, readings, intervals, segments);

        if (ordered.Length == 0)
        {
            return Abstain(asOf, MotMileageAbstentionReason.NoMotHistory, evidence);
        }

        if (readings.Count == 0)
        {
            return Abstain(asOf, MotMileageAbstentionReason.NoUsableReadings, evidence);
        }

        var onDate = Array.FindAll(ordered, item => item.TestDate == asOf);
        if (VehicleMileagePolicy.Calculate(onDate) is { } exact)
        {
            return new(
                MotMileageEstimateKind.Exact,
                asOf,
                exact.Value,
                null,
                null,
                null,
                exact.MethodKey,
                exact.MethodVersion,
                evidence);
        }

        // A usable test on the date that still gives no exact reading
        // disagrees with itself; so does a history whose every episode does.
        return Array.Exists(onDate, IsUsable) || segments.Count == 0
            ? Abstain(asOf, MotMileageAbstentionReason.ConflictingReadings, evidence)
            : FromSegments(segments, asOf, evidence);
    }

    private static MotMileageEstimate FromSegments(
        List<MotMileageSegment> segments,
        DateOnly asOf,
        MotMileageEvidence evidence)
    {
        var first = segments[0].Readings[0];
        if (asOf < first.ObservedOn)
        {
            return Range(asOf, 0, first.Miles, evidence);
        }

        var index = segments.FindLastIndex(segment => segment.Readings[0].ObservedOn <= asOf);
        var current = segments[index];
        var last = current.Readings[^1];
        if (asOf < last.ObservedOn)
        {
            var before = current.Readings.Last(point => point.ObservedOn < asOf);
            var after = current.Readings.First(point => point.ObservedOn > asOf);
            if (current.Holdouts.Count == 0)
            {
                return Range(asOf, before.Miles, after.Miles, evidence);
            }

            // Linear between the two readings. A value between two whole
            // readings rounds to one between them, so it stays clamped to them.
            var elapsed = asOf.DayNumber - before.ObservedOn.DayNumber;
            var span = after.ObservedOn.DayNumber - before.ObservedOn.DayNumber;
            var interpolated = before.Miles + Round((decimal)(after.Miles - before.Miles) * elapsed / span);
            var spread = Round(ErrorRate(current) * elapsed);
            return Estimated(
                asOf,
                interpolated,
                Math.Max(before.Miles, interpolated - spread),
                Math.Min(after.Miles, interpolated + spread),
                evidence);
        }

        var days = asOf.DayNumber - last.ObservedOn.DayNumber;
        var ceiling = last.Miles + ((long)MaxPlausibleMilesPerDay * days);
        if (index + 1 < segments.Count)
        {
            // Between two segments either odometer may be the one fitted, so
            // the range spans both: from zero, as before a first reading, to
            // the higher of the next reading and this segment's ceiling.
            return Range(asOf, 0, Math.Max(segments[index + 1].Readings[0].Miles, ceiling), evidence);
        }

        if (current.MilesPerDay is { } rate && days <= current.HorizonDays)
        {
            var forecast = last.Miles + Round(rate * days);
            var spread = Round(ErrorRate(current) * days);
            return Estimated(asOf, forecast, Math.Max(last.Miles, forecast - spread), forecast + spread, evidence);
        }

        return Range(asOf, last.Miles, ceiling, evidence);
    }

    private static List<MotMileageReading> EpisodeReadings(MotTestObservation[] ordered)
    {
        var readings = new List<MotMileageReading>();
        var start = 0;
        while (start < ordered.Length)
        {
            var end = start + 1;
            var retestUntil = RetestUntil(ordered[start], null);
            while (end < ordered.Length
                && (ordered[end].TestDate == ordered[end - 1].TestDate || ordered[end].TestDate <= retestUntil))
            {
                retestUntil = RetestUntil(ordered[end], retestUntil);
                end++;
            }

            var episode = ordered[start..end];
            var usable = Array.FindAll(episode, IsUsable);
            if (usable.Length > 0)
            {
                // The latest exact reading, and none when that day disagrees.
                var reading = VehicleMileagePolicy.Calculate(episode);
                readings.Add(new(
                    episode[0].TestDate,
                    usable[^1].TestDate,
                    reading?.Value,
                    reading is null ? MotMileageSetAsideReason.ConflictingReadings : null));
            }

            start = end;
        }

        return readings;
    }

    private static (List<MotMileageInterval> Intervals, List<MotMileageSegment> Segments) Segment(
        List<MotMileageReading> readings)
    {
        var intervals = new List<MotMileageInterval>();
        var groups = new List<(List<MotMileagePoint> Points, List<int> Intervals)>();
        var read = Enumerable.Range(0, readings.Count).Where(index => readings[index].Miles is not null).ToArray();
        MotMileagePoint? previous = null;
        for (var position = 0; position < read.Length; position++)
        {
            var reading = readings[read[position]];
            var point = new MotMileagePoint(reading.ObservedOn, reading.Miles!.Value);
            if (previous is null)
            {
                groups.Add((new List<MotMileagePoint> { point }, new List<int>()));
                previous = point;
                continue;
            }

            var interval = Between(previous, point);
            if (point.Miles < previous.Miles)
            {
                intervals.Add(interval with { SetAsideReason = MotMileageSetAsideReason.NegativeDelta });

                // Corroborated only when the next reading stays on the new,
                // lower odometer: at or above the post-drop reading and below
                // the pre-drop one.
                var following = position + 1 < read.Length ? readings[read[position + 1]].Miles : null;
                if (following >= point.Miles && following < previous.Miles)
                {
                    groups.Add((new List<MotMileagePoint> { point }, new List<int>()));
                    previous = point;
                }
                else
                {
                    readings[read[position]] = reading with
                    {
                        SetAsideReason = MotMileageSetAsideReason.UncorroboratedDrop
                    };
                }

                continue;
            }

            groups[^1].Intervals.Add(intervals.Count);
            intervals.Add(interval with
            {
                SetAsideReason = interval.Days < MinIntervalDays ? MotMileageSetAsideReason.ShortSpan
                    : interval.MilesPerDay > MaxPlausibleMilesPerDay ? MotMileageSetAsideReason.ImplausibleRate
                    : null
            });
            groups[^1].Points.Add(point);
            previous = point;
        }

        var segments = new List<MotMileageSegment>();
        foreach (var (points, indices) in groups)
        {
            var latest = points[^1].ObservedOn;
            foreach (var index in indices)
            {
                if (intervals[index].SetAsideReason is null)
                {
                    intervals[index] = intervals[index] with { Weight = Weight(intervals[index], latest) };
                }
            }

            var within = indices.Select(index => intervals[index]).ToArray();
            var holdouts = new List<MotMileageHoldout>();
            for (var target = 1; target < points.Count; target++)
            {
                // Each reading predicted from the readings before it only.
                var (start, end) = (points[target - 1], points[target]);
                if (Rate(within[..(target - 1)], start.ObservedOn) is not { } rate)
                {
                    continue;
                }

                var days = end.ObservedOn.DayNumber - start.ObservedOn.DayNumber;
                var predicted = start.Miles + Round(rate * days);
                holdouts.Add(new(end.ObservedOn, end.Miles, days, predicted, end.Miles - predicted));
            }

            segments.Add(new(points, Rate(within, latest), holdouts, holdouts.Max(holdout => (int?)holdout.Days)));
        }

        return (intervals, segments);
    }

    /// <summary>
    /// The weighted median of the clean intervals' rates, or none below
    /// <see cref="MinCleanIntervals"/>.
    /// </summary>
    private static decimal? Rate(MotMileageInterval[] intervals, DateOnly latest)
    {
        var clean = intervals
            .Where(interval => interval.SetAsideReason is null)
            .Select(interval => (interval.MilesPerDay, Weight: Weight(interval, latest), interval.StartedOn))
            .OrderBy(item => item.MilesPerDay)
            .ThenBy(item => item.StartedOn)
            .ToArray();
        if (clean.Length < MinCleanIntervals)
        {
            return null;
        }

        var half = clean.Sum(item => item.Weight) / 2;
        var index = 0;
        var cumulative = clean[0].Weight;
        while (cumulative < half)
        {
            cumulative += clean[++index].Weight;
        }

        return clean[index].MilesPerDay;
    }

    /// <summary>
    /// Recency times quality: a newer interval never weighs less than an
    /// older one and a longer span never less than a shorter one, each capped.
    /// </summary>
    private static decimal Weight(MotMileageInterval interval, DateOnly latest)
    {
        decimal quality = Math.Min(interval.Days, QualitySpanCapDays);
        var age = latest.DayNumber - interval.EndedOn.DayNumber;
        return quality * RecencyHalfLifeDays / (RecencyHalfLifeDays + age);
    }

    /// <summary>The widest miss per day among the segment's holdouts.</summary>
    private static decimal ErrorRate(MotMileageSegment segment) =>
        segment.Holdouts.Max(holdout => Math.Abs(holdout.ErrorMiles) / (decimal)holdout.Days);

    private static MotMileageInterval Between(MotMileagePoint start, MotMileagePoint end)
    {
        var days = end.ObservedOn.DayNumber - start.ObservedOn.DayNumber;
        return new(
            start.ObservedOn,
            start.Miles,
            end.ObservedOn,
            end.Miles,
            days,
            (decimal)(end.Miles - start.Miles) / days,
            null,
            null);
    }

    private static DateOnly? RetestUntil(MotTestObservation test, DateOnly? current) =>
        string.Equals(test.TestStatus, FailedTestStatus, StringComparison.OrdinalIgnoreCase)
            ? test.TestDate.AddDays(RetestWindowDays)
            : current;

    private static bool IsUsable(MotTestObservation observation) => SetAsideReason(observation) is null;

    private static MotMileageSetAsideReason? SetAsideReason(MotTestObservation observation) =>
        observation.Mileage is not { } value ? MotMileageSetAsideReason.NoMileage
        : observation.MileageUnit is not { } unit ? MotMileageSetAsideReason.NoUnit
        : !Enum.IsDefined(unit) ? MotMileageSetAsideReason.UndefinedUnit
        : value < 0 ? MotMileageSetAsideReason.NegativeMileage
        : null;

    /// <summary>Rounds as <see cref="VehicleMileagePolicy.ToMiles"/> does.</summary>
    private static long Round(decimal miles) => (long)Math.Round(miles, MidpointRounding.AwayFromZero);

    private static MotMileageEstimate Estimated(
        DateOnly asOf,
        long miles,
        long low,
        long high,
        MotMileageEvidence evidence) =>
        new(MotMileageEstimateKind.Estimate, asOf, miles, low, high, null, MethodKey, MethodVersion, evidence);

    private static MotMileageEstimate Range(DateOnly asOf, long low, long high, MotMileageEvidence evidence) =>
        new(MotMileageEstimateKind.QualifiedRange, asOf, null, low, high, null, MethodKey, MethodVersion, evidence);

    private static MotMileageEstimate Abstain(
        DateOnly asOf,
        MotMileageAbstentionReason reason,
        MotMileageEvidence evidence) =>
        new(MotMileageEstimateKind.Abstention, asOf, null, null, null, reason, MethodKey, MethodVersion, evidence);
}
