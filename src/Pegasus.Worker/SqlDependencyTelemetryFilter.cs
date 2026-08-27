using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Worker;

/// <summary>
/// MAIL-020: drops successful SQL dependency telemetry. Failed SQL calls,
/// HTTP dependencies, requests, exceptions and traces pass through.
/// </summary>
public sealed class SqlDependencyTelemetryFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry { Type: "SQL", Success: true })
        {
            return;
        }

        next.Process(item);
    }
}
