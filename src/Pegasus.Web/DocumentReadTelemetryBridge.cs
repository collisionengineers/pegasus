using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Pegasus.Core.Documents;

namespace Pegasus.Web;

/// <summary>
/// Sends the bounded document-read phase timings through the application's
/// existing Application Insights pipeline. The pipeline retains its configured
/// initializers and sampling; this bridge supplies no document identifiers or
/// content-derived fields.
/// </summary>
public sealed class DocumentReadTelemetryBridge : IDisposable
{
    private const string DurationEventName = "Pegasus.Document.Read";

    private readonly TelemetryClient telemetryClient;
    private readonly ActivityListener listener;

    public DocumentReadTelemetryBridge(TelemetryClient telemetryClient)
    {
        this.telemetryClient = telemetryClient;
        listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                string.Equals(
                    source.Name,
                    DocumentReadTelemetry.ActivitySourceName,
                    StringComparison.Ordinal),
            // The bridge only needs the activity identity, parent and elapsed
            // time. It deliberately does not request tags or recorded spans.
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.PropagationData,
            ActivityStopped = TrackDuration
        };
        ActivitySource.AddActivityListener(listener);
    }

    private void TrackDuration(Activity activity)
    {
        if (!telemetryClient.IsEnabled()
            || !DocumentReadTelemetry.IsAllowedPhase(activity.DisplayName))
        {
            return;
        }

        // EventTelemetry participates in the application's existing adaptive
        // sampling. MetricTelemetry is always retained by the SDK, so one
        // metric per phase would create an unsampled ingestion path.
        var telemetry = new EventTelemetry(DurationEventName)
        {
            Timestamp = activity.StartTimeUtc
        };
        telemetry.Properties["phase"] = activity.DisplayName;
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
