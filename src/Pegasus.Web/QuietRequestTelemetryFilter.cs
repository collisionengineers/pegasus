using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;

namespace Pegasus.Web;

/// <summary>
/// Drops the request rows and the SQL rows that carry no information, so the
/// daily telemetry cap is spent on staff pages: the readiness probes (one a
/// minute, four SQL calls each), the diagnostics version endpoint, static
/// files, the App Service Always On ping (<c>GET /</c> answered with a
/// redirect), and the SQL calls of the keep-warm passes. A failed one
/// (unsuccessful, or a 5xx) is always kept, so a
/// failing probe still shows. Everything else passes through. It runs before
/// adaptive sampling, so what it drops does not count against the sampling
/// budget either.
/// </summary>
public sealed class QuietRequestTelemetryFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    private static readonly PathString[] QuietPrefixes =
    [
        "/health",
        "/css",
        "/js",
        "/fonts",
        "/images",
        "/diagnostics"
    ];

    public void Process(ITelemetry item)
    {
        if (!IsQuiet(item))
        {
            next.Process(item);
        }
    }

    /// <summary>
    /// Whether a request path is one of the quiet ones. The per-request
    /// runtime stamps skip the same paths.
    /// </summary>
    internal static bool IsQuietPath(PathString path)
    {
        foreach (var prefix in QuietPrefixes)
        {
            if (path.StartsWithSegments(prefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQuiet(ITelemetry item) => item switch
    {
        RequestTelemetry request => !IsFailure(request.Success, request.ResponseCode)
            && (IsQuietPath(RequestPath(request)) || IsAlwaysOnPing(request)),
        DependencyTelemetry dependency => !IsFailure(dependency.Success, dependency.ResultCode)
            && (IsQuietPath(PathOf(dependency.Context.Operation.Name)) || InWarmUpPass()),
        _ => false
    };

    /// <summary>
    /// Whether the call was made inside a keep-warm pass. A pass has no request,
    /// so its calls carry no path; it runs under one activity, and a dependency
    /// is tracked on the flow that made the call, where that activity is an
    /// ancestor of the current one. A pass reads what staff pages read, every
    /// few minutes, and its successful calls say nothing a page does not.
    /// </summary>
    private static bool InWarmUpPass()
    {
        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            if (activity.OperationName == Health.StartupWarmup.PassActivityName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFailure(bool? success, string? resultCode) =>
        success == false
        || (int.TryParse(resultCode, out var status) && status >= 500);

    private static bool IsAlwaysOnPing(RequestTelemetry request) =>
        request.Name?.StartsWith("GET ", StringComparison.Ordinal) == true
        && RequestPath(request) == "/"
        && request.ResponseCode == "302";

    private static PathString RequestPath(RequestTelemetry request) =>
        request.Url is { IsAbsoluteUri: true } url ? url.AbsolutePath : PathOf(request.Name);

    /// <summary>The path of an operation name such as <c>GET /health/ready</c>.</summary>
    private static PathString PathOf(string? operationName)
    {
        if (string.IsNullOrEmpty(operationName))
        {
            return default;
        }

        var space = operationName.IndexOf(' ');
        var path = space < 0 ? operationName : operationName[(space + 1)..];
        return path.StartsWith('/') ? new PathString(path) : default;
    }
}
