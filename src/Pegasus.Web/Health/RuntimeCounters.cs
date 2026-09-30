using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;

namespace Pegasus.Web.Health;

/// <summary>
/// The runtime and SqlClient event counters Web sends to Application Insights
/// as metrics (the <c>AppMetrics</c> table), and no others. Metrics are not
/// sampled, so the list is the whole of the volume: twelve counters, one row
/// each per collection interval.
/// </summary>
internal static class RuntimeCounters
{
    public const string Runtime = "System.Runtime";
    public const string SqlClient = "Microsoft.Data.SqlClient.EventSource";

    public static IReadOnlyList<(string Source, string Counter)> Requested { get; } =
    [
        (Runtime, "working-set"),
        (Runtime, "gc-heap-size"),
        (Runtime, "gen-2-gc-count"),
        (Runtime, "time-in-gc"),
        (Runtime, "threadpool-thread-count"),
        (Runtime, "threadpool-queue-length"),
        (Runtime, "monitor-lock-contention-count"),
        (SqlClient, "hard-connects"),
        (SqlClient, "soft-connects"),
        (SqlClient, "number-of-active-connections"),
        (SqlClient, "number-of-free-connections"),
        (SqlClient, "number-of-stasis-connections")
    ];

    /// <summary>Replaces the SDK's default counter list with <see cref="Requested"/>.</summary>
    public static void Apply(EventCounterCollectionModule module)
    {
        module.Counters.Clear();
        foreach (var (source, counter) in Requested)
        {
            module.Counters.Add(new EventCounterCollectionRequest(source, counter));
        }
    }
}
