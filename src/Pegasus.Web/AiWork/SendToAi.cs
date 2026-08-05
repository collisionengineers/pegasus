using Microsoft.Extensions.Configuration;

namespace Pegasus.Web.AiWork;

/// <summary>
/// Fixed names for the composition-gated Send to AI hand-off (AI-09).
/// Vendor-neutral by ADR-0011: the domain action is Send to AI; the one
/// sanctioned provider label lives in the UI copy only.
/// </summary>
public static class SendToAi
{
    public const string FeatureFlag = "Features:SendToAi";
    public const string HttpClientName = "SendToAiChannel";
    public static readonly Uri DefaultChannelBaseUrl = new("http://127.0.0.1:8629");
}

/// <summary>
/// Composition-time options for the Send to AI hand-off. The whole surface
/// stays absent unless <c>Features:SendToAi</c> is enabled, and enabling it
/// outside the DevelopmentOffline runtime profile fails closed — the
/// channels transport is a research preview and carries local evidence runs
/// only. The channel token comes from user-secrets and is never tracked,
/// displayed, or logged; the browser never sees it.
/// </summary>
public sealed record SendToAiOptions(
    Uri ChannelBaseUrl,
    string ChannelToken,
    TimeSpan Timeout)
{
    public static SendToAiOptions? TryCreate(
        IConfiguration configuration,
        bool developmentOfflineProfile)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>(SendToAi.FeatureFlag))
        {
            return null;
        }
        if (!developmentOfflineProfile)
        {
            throw new InvalidOperationException(
                $"{SendToAi.FeatureFlag} requires the DevelopmentOffline runtime profile.");
        }

        var configuredBaseUrl = configuration["SendToAi:ChannelBaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? SendToAi.DefaultChannelBaseUrl
            : Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var parsed)
                ? parsed
                : throw new InvalidOperationException(
                    "SendToAi:ChannelBaseUrl must be an absolute URL.");
        if (baseUrl.Scheme != Uri.UriSchemeHttp
            || !baseUrl.IsLoopback
            || !string.IsNullOrEmpty(baseUrl.Query)
            || baseUrl.AbsolutePath != "/")
        {
            throw new InvalidOperationException(
                "SendToAi:ChannelBaseUrl must be a loopback http origin without path or query.");
        }

        var channelToken = configuration["SendToAi:ChannelToken"];
        if (string.IsNullOrWhiteSpace(channelToken) || channelToken.Length < 32)
        {
            throw new InvalidOperationException(
                "SendToAi:ChannelToken is required and must be at least 32 characters.");
        }

        var timeoutSeconds = configuration.GetValue<double?>("SendToAi:TimeoutSeconds") ?? 10;
        if (timeoutSeconds is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "SendToAi:TimeoutSeconds must be between 1 and 60.");
        }

        return new(baseUrl, channelToken, TimeSpan.FromSeconds(timeoutSeconds));
    }
}
