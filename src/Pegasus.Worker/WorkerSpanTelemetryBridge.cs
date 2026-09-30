using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.Worker;

public static class WorkerSpanTelemetryExtensions
{
    /// <summary>
    /// Composes the span bridge over the Application Insights client the Worker
    /// already registers. Program resolves it once at start so the listener is live
    /// before the first function runs.
    /// </summary>
    public static IServiceCollection AddWorkerSpanTelemetry(this IServiceCollection services) =>
        services.AddSingleton<WorkerSpanTelemetryBridge>();
}

/// <summary>
/// Sends the durations of the spans Core already opens on the intake, image intake,
/// custody and Triage paths through the Worker's Application Insights pipeline, one
/// event per stopped span. Without it the Worker exports no span at all, and the
/// time an intake run spends outside any dependency has no name.
/// </summary>
/// <remarks>
/// The listener asks for propagation data only, so no span records tags. The spans
/// carry the intake receipt and the custody work item as tags, and those must never
/// leave the process; this class reads only the span's name, start, duration and
/// trace identity. The events join the pipeline's adaptive sampling, as the Web's
/// document timing does.
/// </remarks>
public sealed class WorkerSpanTelemetryBridge : IDisposable
{
    private const string SpanEventName = "Pegasus.Worker.Span";

    /// <summary>The Core activity sources whose spans are exported.</summary>
    private static readonly HashSet<string> ActivitySourceNames = new(StringComparer.Ordinal)
    {
        "Pegasus.Core.Intake",
        "Pegasus.Core.ImageIntake",
        "Pegasus.Core.Custody",
        "Pegasus.Core.Triage"
    };

    private readonly TelemetryClient telemetryClient;
    private readonly ActivityListener listener;

    public WorkerSpanTelemetryBridge(TelemetryClient telemetryClient)
    {
        this.telemetryClient = telemetryClient;
        listener = new ActivityListener
        {
            ShouldListenTo = static source => ActivitySourceNames.Contains(source.Name),
            // Identity, parent and elapsed time are all the bridge reads. It does not
            // request tags or recorded spans.
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.PropagationData,
            ActivityStopped = TrackDuration
        };
        ActivitySource.AddActivityListener(listener);
    }

    private void TrackDuration(Activity activity)
    {
        if (!telemetryClient.IsEnabled())
        {
            return;
        }

        // EventTelemetry participates in the pipeline's adaptive sampling.
        // MetricTelemetry is always retained by the SDK, so a metric per span would
        // be an unsampled ingestion path.
        var telemetry = new EventTelemetry(SpanEventName)
        {
            Timestamp = activity.StartTimeUtc
        };
        telemetry.Properties["span"] = activity.DisplayName;
        telemetry.Metrics["durationMs"] = activity.Duration.TotalMilliseconds;
        telemetry.Context.Operation.Id = activity.TraceId.ToHexString();
        if (activity.ParentId is { } parentId)
        {
            telemetry.Context.Operation.ParentId = parentId;
        }

        telemetryClient.TrackEvent(telemetry);
    }

    public void Dispose() => listener.Dispose();
}
