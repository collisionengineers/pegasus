using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Web;

/// <summary>
/// Keeps Glass's one-use callback correlation and the provider's query out of
/// request telemetry: the recorded URL is the route template and nothing more.
/// </summary>
public sealed class GlassCallbackTelemetryInitializer : ITelemetryInitializer
{
    private const string GlassCallbackPathPrefix = "/Integrations/Glass/Callback/";
    private const string RedactedGlassCallbackPath = "/Integrations/Glass/Callback/{correlation}";

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not RequestTelemetry { Url: { IsAbsoluteUri: true } url } request
            || !url.AbsolutePath.StartsWith(GlassCallbackPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        request.Url = new UriBuilder(url)
        {
            Path = RedactedGlassCallbackPath,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }
}
