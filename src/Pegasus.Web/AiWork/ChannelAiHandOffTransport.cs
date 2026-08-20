using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pegasus.Core.AiWork;

namespace Pegasus.Web.AiWork;

/// <summary>
/// The one outbound HTTP adapter to the local channel connector. It sends
/// the pointer (never case content) to <c>POST /send</c> and reads the
/// diagnostic delivery record from <c>GET /events</c>. Refusals that mean a
/// configuration problem (401, 403, 413, 415) are terminal; connection
/// failures and 5xx are transient; nothing is retried here — the bounded
/// retry is the operator pressing Send again. It makes no other outbound
/// call, and it never logs the token or the response body.
/// </summary>
internal sealed class ChannelAiHandOffTransport(
    IHttpClientFactory httpClientFactory,
    IAiChannelConnectorStore connectorStore,
    SendToAiOptions options) : IAiHandOffTransport
{
    /// <summary>
    /// One configured client per call: Administration-entered connector
    /// values override the composed configuration, so a change takes effect
    /// on the next hand-off without a restart. An unreadable stored token is
    /// a terminal configuration refusal, not a silent fallback.
    /// </summary>
    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var runtime = await connectorStore.GetRuntimeAsync(cancellationToken);
        var client = httpClientFactory.CreateClient(SendToAi.HttpClientName);
        client.BaseAddress = runtime.ChannelBaseUrl ?? options.ChannelBaseUrl;
        client.Timeout = runtime.Timeout ?? options.Timeout;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", runtime.ChannelToken ?? options.ChannelToken);
        return client;
    }

    public async Task<AiHandOffResult> HandOffAsync(
        AiHandOffPointer handOff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handOff);
        var payload = JsonSerializer.Serialize(new
        {
            schema_version = handOff.SchemaVersion,
            request_id = handOff.RequestId,
            case_reference = handOff.CaseReference,
            instruction = handOff.Instruction
        });
        HttpClient client;
        try
        {
            client = await CreateClientAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return new(AiHandOffOutcomeKind.Refused, exception.Message);
        }

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/send", content, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.RequestEntityTooLarge
                or HttpStatusCode.UnsupportedMediaType
                or HttpStatusCode.BadRequest)
            {
                return new(
                    AiHandOffOutcomeKind.Refused,
                    $"The channel refused the hand-off ({(int)response.StatusCode}).");
            }
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    AiHandOffOutcomeKind.Unreachable,
                    $"The channel returned {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var status = document.RootElement.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;
            return string.Equals(status, "forwarded", StringComparison.Ordinal)
                ? new(AiHandOffOutcomeKind.Accepted, null)
                : new(
                    AiHandOffOutcomeKind.Refused,
                    "The channel accepted the request but did not forward it.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException
                || exception is TaskCanceledException
                    && !cancellationToken.IsCancellationRequested)
        {
            return new(AiHandOffOutcomeKind.Unreachable, "The channel was unreachable.");
        }
        finally
        {
            client.Dispose();
        }
    }

    public async Task<AiChannelReply?> TryReadReplyAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        try
        {
            using var client = await CreateClientAsync(cancellationToken);
            using var response = await client.GetAsync(
                $"/events?request_id={Uri.EscapeDataString(requestId)}&limit=1",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("events", out var events)
                || events.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var record in events.EnumerateArray())
            {
                if (!record.TryGetProperty("request_id", out var recordId)
                    || !string.Equals(recordId.GetString(), requestId, StringComparison.Ordinal)
                    || !record.TryGetProperty("reply", out var reply)
                    || reply.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var status = reply.TryGetProperty("status", out var replyStatus)
                    ? replyStatus.GetString()
                    : null;
                if (status is null)
                {
                    continue;
                }

                var message = reply.TryGetProperty("message", out var replyMessage)
                    ? replyMessage.GetString()
                    : null;
                DateTimeOffset? repliedAt = reply.TryGetProperty("replied_at", out var repliedAtValue)
                    && repliedAtValue.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(repliedAtValue.GetString(), out var parsed)
                        ? parsed
                        : null;
                return new(status, message, repliedAt);
            }

            return null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or HttpRequestException
                or JsonException
                || exception is TaskCanceledException
                    && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}

/// <summary>
/// Composition for the gated Send to AI hand-off. Nothing registers unless
/// <c>Features:SendToAi</c> enabled it at startup; without it there is no
/// transport, no send behaviour, and the assessment panel renders the
/// unavailable state.
/// </summary>
public static class SendToAiExtensions
{
    public static IServiceCollection AddPegasusSendToAi(
        this IServiceCollection services,
        SendToAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        // Base address, timeout, and bearer token are applied per call by
        // the transport so Administration-entered connector values override
        // the composed configuration without a restart.
        services.AddHttpClient(SendToAi.HttpClientName)
            // Redirects are not followed: a 3xx from the configured loopback
            // connector would otherwise resend the pointer — and the bearer
            // token — to whatever host it named, defeating the loopback
            // restriction and this adapter's contract that it makes no other
            // outbound call. A 3xx falls through as a transport failure.
            .ConfigurePrimaryHttpMessageHandler(
                () => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<Pegasus.Core.AiWork.IAiHandOffTransport, ChannelAiHandOffTransport>();
        services.AddScoped<
            Pegasus.Core.AiWork.IAiChannelConnectorStore,
            Pegasus.Infrastructure.Persistence.EfAiChannelConnectorStore>();
        services.AddScoped<Pegasus.Core.AiWork.ISendCaseToAi, Pegasus.Core.AiWork.SendCaseToAi>();
        services.AddScoped<
            Pegasus.Core.AiWork.IReconcileAiWorkRequest,
            Pegasus.Core.AiWork.ReconcileAiWorkRequest>();
        services.AddScoped<
            Pegasus.Core.AiWork.ICancelAiWorkRequest,
            Pegasus.Core.AiWork.CancelAiWorkRequest>();
        return services;
    }
}
