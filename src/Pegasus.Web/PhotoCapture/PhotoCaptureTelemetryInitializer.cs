using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// A designed refusal on the capture surface is the surface working, not
/// failing (the #1491 lesson): an unavailable link's 404, a conflict, a size
/// or type refusal and a rate limit are recorded as successful requests, so
/// a guesser or a stale link cannot raise the failure rate or an alert. A
/// 5xx, and any 4xx the surface does not design, stays a failure.
/// </summary>
public sealed class PhotoCaptureTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not RequestTelemetry { Url: { IsAbsoluteUri: true } url } request
            || !IsDesignedRefusal(url.AbsolutePath, request.ResponseCode))
        {
            return;
        }

        request.Success = true;
    }

    private static bool IsDesignedRefusal(PathString path, string? responseCode)
    {
        if (path.StartsWithSegments("/capture/api"))
        {
            return responseCode is "404" or "409" or "413" or "415" or "429";
        }

        return (path.StartsWithSegments(PhotoCaptureSurface.LinkPathPrefix)
                || path.StartsWithSegments(PhotoCaptureSurface.StandingPathPrefix))
            && responseCode is "404" or "429";
    }
}
