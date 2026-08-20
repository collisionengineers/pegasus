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

        // The bounds below are owned once, in Core, and shared with the
        // Administration entry route (AiChannelConnectorRules).
        var configuredBaseUrl = configuration["SendToAi:ChannelBaseUrl"];
        Uri baseUrl;
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            baseUrl = SendToAi.DefaultChannelBaseUrl;
        }
        else if (Pegasus.Core.AiWork.AiChannelConnectorRules.TryParseBaseUrl(
            configuredBaseUrl,
            out var parsed))
        {
            baseUrl = parsed!;
        }
        else
        {
            throw new InvalidOperationException(
                "SendToAi:ChannelBaseUrl must be a loopback http origin without path or query.");
        }

        var channelToken = configuration["SendToAi:ChannelToken"];
        if (!Pegasus.Core.AiWork.AiChannelConnectorRules.IsValidToken(channelToken))
        {
            throw new InvalidOperationException(
                "SendToAi:ChannelToken is required and must be at least 32 characters.");
        }

        var timeoutSeconds = configuration.GetValue<double?>("SendToAi:TimeoutSeconds") ?? 10;
        if (!Pegasus.Core.AiWork.AiChannelConnectorRules.IsValidTimeoutSeconds(timeoutSeconds))
        {
            throw new InvalidOperationException(
                "SendToAi:TimeoutSeconds must be between 1 and 60.");
        }

        return new(baseUrl, channelToken!, TimeSpan.FromSeconds(timeoutSeconds));
    }
}
