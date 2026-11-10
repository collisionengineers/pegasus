using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Pegasus.Web;

/// <summary>
/// Keeps a credential that travels in a request path out of request
/// telemetry: Glass's one-use callback correlation and the provider's query,
/// and a photo link's token or a standing link's slug (ADR-0090). The
/// recorded URL is the route template and nothing more, and a request name
/// that carries the raw path is renamed to the template too.
/// </summary>
public sealed class SecretPathTelemetryInitializer : ITelemetryInitializer
{
    private static readonly (string Prefix, string Template)[] SecretPaths =
    [
        ("/Integrations/Glass/Callback/", "/Integrations/Glass/Callback/{correlation}"),
        ("/c/", "/c/{token}"),
        ("/s/", "/s/{slug}")
    ];

    public void Initialize(ITelemetry telemetry)
    {
        // Whatever the item, its operation name may carry the raw path: a row
        // emitted before routing is named by the path, not the route template.
        if (telemetry.Context.Operation.Name is { Length: > 0 } operationName)
        {
            telemetry.Context.Operation.Name = Scrub(operationName);
        }

        if (telemetry is not RequestTelemetry { Url: { IsAbsoluteUri: true } url } request)
        {
            return;
        }

        foreach (var (prefix, template) in SecretPaths)
        {
            if (!url.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Url = new UriBuilder(url)
            {
                Path = template,
                Query = string.Empty,
                Fragment = string.Empty
            }.Uri;
            if (request.Name is { } name && name.Contains(url.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                var space = name.IndexOf(' ', StringComparison.Ordinal);
                request.Name = space < 0 ? template : name[..(space + 1)] + template;
            }

            return;
        }
    }

    /// <summary>An operation name such as <c>GET /c/token</c> with a secret path rewritten to its template.</summary>
    private static string Scrub(string operationName)
    {
        var space = operationName.IndexOf(' ', StringComparison.Ordinal);
        var path = space < 0 ? operationName : operationName[(space + 1)..];
        foreach (var (prefix, template) in SecretPaths)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return space < 0 ? template : operationName[..(space + 1)] + template;
            }
        }

        return operationName;
    }
}
