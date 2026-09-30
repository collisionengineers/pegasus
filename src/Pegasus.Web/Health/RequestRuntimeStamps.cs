using System.Globalization;
using Microsoft.ApplicationInsights.DataContracts;

namespace Pegasus.Web.Health;

/// <summary>
/// Stamps each page request's telemetry row with what the runtime was doing
/// around it, so a slow request says whether it waited for a thread, sat
/// through garbage collection or paged memory in. The values are properties
/// of the row the request already produces, so they add no telemetry items.
/// Health probes, static files and the version endpoint are skipped, as the
/// filter drops their rows.
/// </summary>
/// <remarks>
/// <c>tp.threads</c> and <c>tp.pending</c> are read when the request starts.
/// <c>gc.gen2.delta</c>, <c>gc.pause.ms.delta</c> and <c>majflt.delta</c> are the
/// change over the request. <c>majflt.delta</c> is absent where
/// <c>/proc/self/stat</c> is (Windows). Concurrent requests share the process
/// counters, so a delta says what the process did during the request, not what
/// the request did.
/// </remarks>
internal static class RequestRuntimeStamps
{
    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var telemetry = context.Features.Get<RequestTelemetry>();
        if (telemetry is null || QuietRequestTelemetryFilter.IsQuietPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var threads = ThreadPool.ThreadCount;
        var pending = ThreadPool.PendingWorkItemCount;
        var gen2 = GC.CollectionCount(2);
        var pause = GC.GetTotalPauseDuration();
        var faults = ProcFs.ReadMajorFaults();
        try
        {
            await next(context);
        }
        finally
        {
            var properties = telemetry.Properties;
            properties["tp.threads"] = threads.ToString(CultureInfo.InvariantCulture);
            properties["tp.pending"] = pending.ToString(CultureInfo.InvariantCulture);
            properties["gc.gen2.delta"] = (GC.CollectionCount(2) - gen2).ToString(CultureInfo.InvariantCulture);
            properties["gc.pause.ms.delta"] = (GC.GetTotalPauseDuration() - pause).TotalMilliseconds
                .ToString("0.###", CultureInfo.InvariantCulture);
            if (faults is { } before && ProcFs.ReadMajorFaults() is { } after)
            {
                properties["majflt.delta"] = (after - before).ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
