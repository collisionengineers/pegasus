using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Worker;

/// <summary>
/// MAIL-020: drops successful SQL dependency telemetry that took under
/// <see cref="SlowSqlThreshold"/>. A slow successful statement (a lock wait, say) is
/// kept, because it is the one that explains a slow run. Failed SQL calls, HTTP
/// dependencies, requests, exceptions and traces pass through.
/// </summary>
public sealed class SqlDependencyTelemetryFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public static readonly TimeSpan SlowSqlThreshold = TimeSpan.FromMilliseconds(250);

    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry { Type: "SQL", Success: true } dependency
            && dependency.Duration < SlowSqlThreshold)
        {
            return;
        }

        next.Process(item);
    }
}
