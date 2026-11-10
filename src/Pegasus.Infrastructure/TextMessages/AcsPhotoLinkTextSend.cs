using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.TextMessages;

/// <summary>
/// Where texts are sent from (R918): the Azure Communication Services
/// endpoint and the SMS-enabled UK mobile number bought on it, in E.164.
/// Texting is unavailable while either is empty.
/// </summary>
public sealed class TextMessageOptions
{
    public const string SectionName = "TextMessages";

    public Uri? Endpoint { get; set; }

    public string? From { get; set; }

    public bool Configured => Endpoint is not null && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Where no number is configured: no page offers Send by text.</summary>
public sealed class UnavailablePhotoLinkTextSend : IPhotoLinkTextSend
{
    public bool Available => false;

    public Task<PhotoLinkTextOutcome> SendAsync(string recipient, string message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No text provider is configured.");
}

/// <summary>
/// One photo link text through the Azure Communication Services SMS REST API
/// (<c>POST {endpoint}/sms</c>), authorised with the Web's managed identity
/// (Microsoft Entra ID; Contributor on the resource). Delivery reports are not
/// asked for (R921): accepted is what the provider says on the request. A
/// refusal, an HTTP failure or no answer becomes a failure code, never an
/// exception, so the link's history records what happened.
/// </summary>
public sealed partial class AcsPhotoLinkTextSend(
    HttpClient httpClient,
    TokenCredential credential,
    TextMessageOptions options,
    ILogger<AcsPhotoLinkTextSend> logger) : IPhotoLinkTextSend
{
    public const string HttpClientName = nameof(AcsPhotoLinkTextSend);
    private const string ApiVersion = "2021-03-07";
    private static readonly TokenRequestContext Scope = new(["https://communication.azure.com//.default"]);

    public bool Available => options.Configured;

    public async Task<PhotoLinkTextOutcome> SendAsync(string recipient, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!options.Configured)
        {
            throw new InvalidOperationException("No text provider is configured.");
        }

        try
        {
            var token = await credential.GetTokenAsync(Scope, cancellationToken);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(options.Endpoint!, $"sms?api-version={ApiVersion}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = JsonContent.Create(new SendRequest(
                options.From!.Trim(),
                [new Recipient(recipient)],
                message,
                new SendOptions(EnableDeliveryReport: false)));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log.Refused(logger, (int)response.StatusCode);
                return PhotoLinkTextOutcome.Failed($"http_{(int)response.StatusCode}");
            }

            var body = await response.Content.ReadFromJsonAsync<SendResponse>(cancellationToken);
            var result = body?.Value is { Count: > 0 } results ? results[0] : null;
            if (result is { Successful: true })
            {
                return PhotoLinkTextOutcome.Sent(result.MessageId);
            }

            Log.Refused(logger, result?.HttpStatusCode ?? 0);
            return PhotoLinkTextOutcome.Failed(result is null ? "no_result" : $"http_{result.HttpStatusCode}");
        }
        catch (HttpRequestException exception)
        {
            Log.Unreachable(logger, exception);
            return PhotoLinkTextOutcome.Failed("provider_unreachable");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Unreachable(logger, exception);
            return PhotoLinkTextOutcome.Failed("provider_timeout");
        }
        catch (Azure.Identity.AuthenticationFailedException exception)
        {
            Log.Unreachable(logger, exception);
            return PhotoLinkTextOutcome.Failed("not_authorised");
        }
    }

    private sealed record SendRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("smsRecipients")] IReadOnlyList<Recipient> SmsRecipients,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("smsSendOptions")] SendOptions SmsSendOptions);

    private sealed record Recipient([property: JsonPropertyName("to")] string To);

    private sealed record SendOptions([property: JsonPropertyName("enableDeliveryReport")] bool EnableDeliveryReport);

    private sealed record SendResponse([property: JsonPropertyName("value")] IReadOnlyList<SendResult>? Value);

    private sealed record SendResult(
        [property: JsonPropertyName("messageId")] string? MessageId,
        [property: JsonPropertyName("httpStatusCode")] int HttpStatusCode,
        [property: JsonPropertyName("successful")] bool Successful);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "The text provider refused a photo link text with status {StatusCode}.")]
        public static partial void Refused(ILogger logger, int statusCode);

        [LoggerMessage(Level = LogLevel.Warning, Message = "The text provider could not be reached for a photo link text.")]
        public static partial void Unreachable(ILogger logger, Exception exception);
    }
}
