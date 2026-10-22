using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Worker;

/// <summary>
/// MAIL-020: drops successful SQL dependency telemetry that took under
/// <see cref="SlowSqlThreshold"/>. A slow successful statement (a lock wait, say) is
/// kept, because it is the one that explains a slow run. It also drops a failed blob
/// dependency whose error is <c>BlobAlreadyExists</c>: every conditional blob write
/// sends <c>If-None-Match: *</c> and verifies the blob already there on that 409, so the
/// row records the by-design path. Every other failed storage call, failed SQL calls,
/// HTTP dependencies, requests, exceptions and traces pass through.
/// </summary>
public sealed class SqlDependencyTelemetryFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public static readonly TimeSpan SlowSqlThreshold = TimeSpan.FromMilliseconds(250);

    private const string StorageDependencyType = "InProc | Microsoft.Storage";

    private const string BlobAlreadyExists = "ErrorCode: BlobAlreadyExists";

    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry dependency && IsExpected(dependency))
        {
            return;
        }

        next.Process(item);
    }

    private static bool IsExpected(DependencyTelemetry dependency) =>
        dependency switch
        {
            { Type: "SQL", Success: true } => dependency.Duration < SlowSqlThreshold,
            { Type: StorageDependencyType, Success: false } =>
                dependency.Properties.TryGetValue("Error", out var error)
                && error.Contains(BlobAlreadyExists, StringComparison.Ordinal),
            _ => false
        };
}
