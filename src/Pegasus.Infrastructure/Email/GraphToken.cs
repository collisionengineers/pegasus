using Azure.Core;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// The one token request every Graph caller makes: the application's
/// <c>.default</c> Graph scope. The credential caches the token per scope, so
/// only the first request after a start reaches the managed-identity
/// endpoint. On 4 and 5 November 2026 that first fetch took 2 to 14 s.
/// Web's start-up warm-up makes it, so no staff send pays it (issue 1948).
/// </summary>
public static class GraphToken
{
    public static readonly TokenRequestContext Context = new(["https://graph.microsoft.com/.default"]);
}
