using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Web;

public sealed class PublicUploadTelemetryInitializer : ITelemetryInitializer
{
    private const string PublicUploadPathPrefix = "/Uploads/";
    private const string RedactedPublicUploadPath = "/Uploads/Request";
    private const string GlassCallbackPathPrefix = "/Integrations/Glass/Callback/";
    private const string RedactedGlassCallbackPath = "/Integrations/Glass/Callback/{correlation}";

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not RequestTelemetry { Url: { IsAbsoluteUri: true } url } request)
        {
            return;
        }

        var redactedPath = url.AbsolutePath.StartsWith(
            PublicUploadPathPrefix,
            StringComparison.OrdinalIgnoreCase)
            ? RedactedPublicUploadPath
            : url.AbsolutePath.StartsWith(
                GlassCallbackPathPrefix,
                StringComparison.OrdinalIgnoreCase)
                ? RedactedGlassCallbackPath
                : null;
        if (redactedPath is null) return;

        request.Url = new UriBuilder(url)
        {
            Path = redactedPath,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }
}
