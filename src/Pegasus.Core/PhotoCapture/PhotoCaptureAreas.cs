namespace Pegasus.Core.PhotoCapture;

/// <summary>One damaged area as the app works it out: the zones it covers, the zone that keys it and its label.</summary>
public sealed record PhotoCaptureArea(string Key, string Label, IReadOnlyList<string> Zones);

/// <summary>
/// The app's rule for turning the zones a sender marked on the car diagram
/// into damaged areas, ported from the capture app so the two never
/// disagree (FRD-28; handover, 5 November 2026): touching zones, up to
/// three, are one area; a gap starts a new area; a touching run of four or
/// more splits in two; and when more than two areas remain, the closest are
/// merged until two are left. A damage photo is tagged with its area's key,
/// the middle zone of the area's longest run, which is also the zone whose
/// example pictures the app shows.
/// </summary>
public static class PhotoCaptureAreas
{
    public const int MaximumAreas = 2;
    private const int RunSplitLength = 4;

    public static IReadOnlyList<PhotoCaptureArea> Derive(IReadOnlyList<string> markedZones)
    {
        ArgumentNullException.ThrowIfNull(markedZones);
        var ring = PhotoCaptureStory.Zones.Select(zone => zone.Key).ToArray();
        var selected = ring.Select(zone => markedZones.Contains(zone, StringComparer.Ordinal)).ToArray();
        if (!selected.Any(isSelected => isSelected))
        {
            return [];
        }

        var runs = new List<List<string>>();
        if (selected.All(isSelected => isSelected))
        {
            runs.Add([.. ring]);
        }
        else
        {
            // The walk starts at a selected zone whose predecessor is not,
            // so a run that wraps past the front is read as one run.
            var start = Enumerable.Range(0, ring.Length)
                .First(index => selected[index] && !selected[(index + ring.Length - 1) % ring.Length]);
            List<string>? current = null;
            for (var step = 0; step < ring.Length; step++)
            {
                var index = (start + step) % ring.Length;
                if (selected[index])
                {
                    if (current is null)
                    {
                        current = [];
                        runs.Add(current);
                    }

                    current.Add(ring[index]);
                }
                else
                {
                    current = null;
                }
            }
        }

        runs = runs
            .SelectMany(run =>
            {
                if (run.Count < RunSplitLength)
                {
                    return [run];
                }

                var half = (run.Count + 1) / 2;
                return new[] { run.Take(half).ToList(), run.Skip(half).ToList() };
            })
            .OrderBy(run => Position(run, ring))
            .ToList();

        var groups = runs.Select(run => new List<List<string>> { run }).ToList();
        while (groups.Count > MaximumAreas)
        {
            var count = groups.Count;
            var best = 0;
            var bestGap = int.MaxValue;
            for (var index = 0; index < count; index++)
            {
                var gap = Gap(groups[index], groups[(index + 1) % count], ring);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = index;
                }
            }

            if (best < count - 1)
            {
                var merged = groups[best].Concat(groups[best + 1]).ToList();
                groups.RemoveRange(best, 2);
                groups.Insert(best, merged);
            }
            else
            {
                var merged = groups[count - 1].Concat(groups[0]).ToList();
                groups = [merged, .. groups.Skip(1).Take(count - 2)];
            }
        }

        return groups.Select(group =>
        {
            var mainRun = group.Aggregate(group[0], (longest, run) => run.Count > longest.Count ? run : longest);
            var key = mainRun[(mainRun.Count - 1) / 2];
            var parts = group.Select((run, index) => index == 0 ? RunLabel(run) : LowerFirst(RunLabel(run))).ToArray();
            var label = parts.Length > 2
                ? string.Join(", ", parts[..^1]) + " and " + parts[^1]
                : string.Join(" and ", parts);
            return new PhotoCaptureArea(key, label, group.SelectMany(run => run).ToArray());
        }).ToArray();
    }

    /// <summary>The area a tagged damage photo belongs to, by its key, or by the zone it names when the key moved.</summary>
    public static PhotoCaptureArea? Find(IReadOnlyList<PhotoCaptureArea> areas, string? areaKey)
    {
        ArgumentNullException.ThrowIfNull(areas);
        if (areaKey is null)
        {
            return null;
        }

        return areas.FirstOrDefault(area => string.Equals(area.Key, areaKey, StringComparison.Ordinal))
            ?? areas.FirstOrDefault(area => area.Zones.Contains(areaKey, StringComparer.Ordinal));
    }

    private static int Position(List<string> run, string[] ring)
    {
        var first = Array.IndexOf(ring, run[0]);
        return run.Contains("front", StringComparer.Ordinal) && run[0] != "front" && first > 6 ? -1 : first;
    }

    private static int Gap(List<List<string>> first, List<List<string>> second, string[] ring)
    {
        var end = Array.IndexOf(ring, first[^1][^1]);
        var start = Array.IndexOf(ring, second[0][0]);
        return (start - end + ring.Length) % ring.Length;
    }

    private static string RunLabel(List<string> run) => run.Count switch
    {
        1 => PhotoCaptureStory.ZoneLabel(run[0]),
        2 => PhotoCaptureStory.ZoneLabel(run[0]) + " and " + PhotoCaptureStory.ZoneLabel(run[^1]).ToLowerInvariant(),
        _ => PhotoCaptureStory.ZoneLabel(run[0]) + " to " + PhotoCaptureStory.ZoneLabel(run[^1]).ToLowerInvariant()
    };

    private static string LowerFirst(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
