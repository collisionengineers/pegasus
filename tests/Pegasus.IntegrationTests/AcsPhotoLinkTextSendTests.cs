using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.TextMessages;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The photo link text through the Azure Communication Services SMS REST API
/// (R916, R919), against a stand-in transport: the request the adapter makes,
/// and how each answer becomes Sent or a failure code rather than an
/// exception, so the link's history records what happened.
/// </summary>
public sealed class AcsPhotoLinkTextSendTests
{
    private const string From = "+447000000001";
    private const string To = "+447700900123";
    private const string Message = "Hi Sam Taylor, please open https://example.invalid/c/link";

    private static readonly Uri Endpoint = new("https://pegasus-sms.communication.azure.com/");

    [Fact]
    public async Task OneTextIsPostedToTheSmsApiWithTheBearerTokenAndNoDeliveryReport()
    {
        var transport = new RecordingHandler(_ => Response(
            HttpStatusCode.Accepted,
            """{"value":[{"to":"+447700900123","messageId":"Outgoing_20261110_noam","httpStatusCode":202,"successful":true}]}"""));
        var credential = new FixedCredential();

        var outcome = await Sender(transport, credential).SendAsync(To, Message, CancellationToken.None);

        Assert.Equal(PhotoLinkTextOutcome.Sent("Outgoing_20261110_noam"), outcome);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://pegasus-sms.communication.azure.com/sms?api-version=2021-03-07", request.Url);
        Assert.Equal("Bearer token", request.Authorization);
        Assert.Equal("https://communication.azure.com//.default", Assert.Single(Assert.Single(credential.Scopes)));
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(From, root.GetProperty("from").GetString());
        Assert.Equal(To, Assert.Single(root.GetProperty("smsRecipients").EnumerateArray()).GetProperty("to").GetString());
        Assert.Equal(Message, root.GetProperty("message").GetString());
        Assert.False(root.GetProperty("smsSendOptions").GetProperty("enableDeliveryReport").GetBoolean());
    }

    /// <summary>A recipient the provider answers as not successful is a failure named by its status.</summary>
    [Fact]
    public async Task ARecipientTheProviderDidNotAcceptIsAFailure()
    {
        var transport = new RecordingHandler(_ => Response(
            HttpStatusCode.Accepted,
            """{"value":[{"to":"+447700900123","messageId":null,"httpStatusCode":400,"successful":false,"errorMessage":"Invalid recipient"}]}"""));

        var outcome = await Sender(transport).SendAsync(To, Message, CancellationToken.None);

        Assert.Equal(PhotoLinkTextOutcome.Failed("http_400"), outcome);
    }

    [Fact]
    public async Task AnHttpFailureIsAFailureNamedByItsStatus()
    {
        var transport = new RecordingHandler(_ => Response(HttpStatusCode.InternalServerError, "{}"));

        var outcome = await Sender(transport).SendAsync(To, Message, CancellationToken.None);

        Assert.Equal(PhotoLinkTextOutcome.Failed("http_500"), outcome);
    }

    [Fact]
    public async Task AProviderThatCannotBeReachedIsAFailureNotAnException()
    {
        var transport = new RecordingHandler(_ => throw new HttpRequestException("simulated transport failure"));

        var outcome = await Sender(transport).SendAsync(To, Message, CancellationToken.None);

        Assert.Equal(PhotoLinkTextOutcome.Failed("provider_unreachable"), outcome);
    }

    /// <summary>With no number configured nothing is offered and nothing is sent.</summary>
    [Fact]
    public async Task WithoutANumberTextingIsUnavailable()
    {
        var transport = new RecordingHandler(_ => Response(HttpStatusCode.Accepted, "{}"));
        var sender = new AcsPhotoLinkTextSend(
            new HttpClient(transport),
            new FixedCredential(),
            new TextMessageOptions { Endpoint = Endpoint },
            NullLogger<AcsPhotoLinkTextSend>.Instance);

        Assert.False(sender.Available);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(To, Message, CancellationToken.None));
        Assert.Empty(transport.Requests);
    }

    private static AcsPhotoLinkTextSend Sender(RecordingHandler transport, FixedCredential? credential = null) => new(
        new HttpClient(transport),
        credential ?? new FixedCredential(),
        new TextMessageOptions { Endpoint = Endpoint, From = From },
        NullLogger<AcsPhotoLinkTextSend>.Instance);

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record SentRequest(HttpMethod Method, string Url, string? Authorization, string Body);

    /// <summary>A transport that records each request as sent, its body read before the adapter disposes it.</summary>
    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return answer(request);
        }
    }

    private sealed class FixedCredential : TokenCredential
    {
        public List<string[]> Scopes { get; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes.Add(requestContext.Scopes);
            return new("token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
