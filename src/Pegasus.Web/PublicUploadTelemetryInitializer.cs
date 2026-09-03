using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Web;

public sealed class PublicUploadTelemetryInitializer : ITelemetryInitializer
{
    private const string PublicUploadPathPrefix = "/Uploads/";
    private const string RedactedPublicUploadPath = "/Uploads/Request";

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not RequestTelemetry { Url: { IsAbsoluteUri: true } url } request
            || !url.AbsolutePath.StartsWith(
                PublicUploadPathPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        request.Url = new UriBuilder(url)
        {
            Path = RedactedPublicUploadPath,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }
}
