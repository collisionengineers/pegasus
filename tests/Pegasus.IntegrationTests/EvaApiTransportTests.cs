using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Eva;
using Pegasus.Infrastructure.Eva;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-04. The fixtures here are the shapes EVA's test environment actually
/// returned, taken from the reference connector's recorded traffic — not the
/// shapes its documentation describes. Every one of them differs from the
/// documented contract in some way, which is the reason this file exists.
/// </summary>
public sealed class EvaApiTransportTests
{
    private static readonly Uri BaseUri = new("https://sentry.evasoftware.co.uk/api/");

    /// <summary>
    /// A real success, verbatim: camelCase members where the documentation
    /// promises PascalCase, and the File Reference buried in the message text
    /// rather than given a field.
    /// </summary>
    private const string RecordedSuccess =
        """{"statusCode":200,"message":"Inspection Request has been processed. File Reference: 61239","id":"600005"}""";

    [Fact]
    public async Task ARecordedSuccessYieldsBothIdentifiers()
    {
        var result = await SubmitAsync(Responder(HttpStatusCode.OK, RecordedSuccess));

        Assert.Equal(EvaSubmissionOutcome.Succeeded, result.Outcome);
        Assert.Equal("600005", result.EvaId);
        Assert.Equal("61239", result.FileReference);
        Assert.Null(result.FailureCode);
    }

    /// <summary>
    /// The behaviour that makes reading the HTTP status alone unsafe: EVA
    /// answers 200 OK with a refusal in the body. Recorded when RequestFrom
    /// carried a code EVA had not issued.
    /// </summary>
    [Fact]
    public async Task ARejectionInsideAnHttpSuccessIsARejection()
    {
        var result = await SubmitAsync(Responder(
            HttpStatusCode.OK,
            """{"statusCode":400,"message":"Please check the value in the 'RequestFrom' field is correct and try again.","id":""}"""));

        Assert.Equal(EvaSubmissionOutcome.Rejected, result.Outcome);
        Assert.Null(result.EvaId);
        Assert.Contains("RequestFrom", result.FailureDetail!, StringComparison.Ordinal);
        Assert.Equal(0, result.ImagesSent);
    }

    /// <summary>
    /// Recorded when the Agent code had not yet been provisioned server-side.
    /// An empty id is not an id.
    /// </summary>
    [Fact]
    public async Task AnUnboundFieldRejectionCarriesEvasOwnWords()
    {
        var result = await SubmitAsync(Responder(
            HttpStatusCode.OK,
            """{"statusCode":400,"message":"'Agent' field value couldn't be bound.","id":""}"""));

        Assert.Equal(EvaSubmissionOutcome.Rejected, result.Outcome);
        Assert.Null(result.EvaId);
        Assert.Contains("Agent", result.FailureDetail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// EVA's 500 is text/plain from a JSON endpoint. There is no envelope to
    /// read, so delivery is unknown — and unknown is the only outcome that may
    /// be retried.
    /// </summary>
    [Fact]
    public async Task APlainTextServerErrorIsUnknownAndRetryable()
    {
        var result = await SubmitAsync(Responder(
            HttpStatusCode.InternalServerError,
            "An error occurred. If the issue persists please contact Minotaur Software with a copy of the data model you're sending and the endpoint this applied to.",
            "text/plain"));

        Assert.Equal(EvaSubmissionOutcome.Unknown, result.Outcome);
        Assert.True(EvaSubmissionPolicy.IsRetryable(result.Outcome));
        Assert.Contains("Minotaur", result.FailureDetail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATransportFailureLeavesDeliveryUnknown()
    {
        var result = await SubmitAsync((_, _) =>
            throw new HttpRequestException("The remote host refused the connection."));

        Assert.Equal(EvaSubmissionOutcome.Unknown, result.Outcome);
        Assert.Equal("eva_unreachable", result.FailureCode);
    }

    /// <summary>
    /// The token endpoint is not OAuth2: form-urlencoded, PascalCase with an
    /// underscore, and no grant_type.
    /// </summary>
    [Fact]
    public async Task TheTokenRequestUsesEvasOwnFieldNames()
    {
        string? tokenBody = null;
        string? tokenContentType = null;

        // Its own handler rather than the shared helper, which answers the
        // token endpoint itself and would hide the request under test.
        using var handler = new DelegateHandler((request, body) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal))
            {
                tokenBody = body;
                tokenContentType = request.Content!.Headers.ContentType!.MediaType;
                return Ok("""{"access_token":"tok","expires_in":5}""");
            }

            return Ok(RecordedSuccess);
        });
        using var client = new HttpClient(handler);
        await Transport(client).SubmitInstructionAsync(Payload());

        Assert.Equal("application/x-www-form-urlencoded", tokenContentType);
        Assert.NotNull(tokenBody);
        Assert.Contains("Client_Id=client", tokenBody, StringComparison.Ordinal);
        Assert.Contains("Client_Secret=secret", tokenBody, StringComparison.Ordinal);
        Assert.DoesNotContain("grant_type", tokenBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// EVA documents expires_in in MINUTES and defaults it to 5. Read as
    /// seconds, a token would be treated as expiring before the submission it
    /// was minted for finished uploading, and every submission would mint its
    /// own.
    /// </summary>
    [Fact]
    public async Task TheTokenIsCachedAcrossSubmissionsBecauseExpiryIsInMinutes()
    {
        var tokenRequests = 0;
        var time = new MovableTimeProvider(
            DateTimeOffset.Parse("2026-08-27T09:00:00Z", CultureInfo.InvariantCulture));
        using var handler = new DelegateHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal))
            {
                tokenRequests++;
                return Ok("""{"access_token":"tok","expires_in":5}""");
            }

            return Ok(RecordedSuccess);
        });
        using var client = new HttpClient(handler);
        var transport = Transport(client, time);

        await transport.SubmitInstructionAsync(Payload());
        time.Advance(TimeSpan.FromMinutes(3));
        await transport.SubmitInstructionAsync(Payload());

        Assert.Equal(1, tokenRequests);

        // Past five minutes less the renewal margin, it must mint again.
        time.Advance(TimeSpan.FromMinutes(3));
        await transport.SubmitInstructionAsync(Payload());
        Assert.Equal(2, tokenRequests);
    }

    /// <summary>
    /// A token can expire between the margin check and the request landing, so
    /// a 401 is re-minted once. It is not a general retry: EVA has no
    /// idempotency and a blind resend can duplicate the claim.
    /// </summary>
    [Fact]
    public async Task AnUnauthorizedSubmissionIsRetriedExactlyOnce()
    {
        var submissions = 0;
        using var handler = new DelegateHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal))
            {
                return Ok("""{"access_token":"tok","expires_in":5}""");
            }

            submissions++;
            return submissions == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(string.Empty)
                }
                : Ok(RecordedSuccess);
        });
        using var client = new HttpClient(handler);

        var result = await Transport(client).SubmitInstructionAsync(Payload());

        Assert.Equal(2, submissions);
        Assert.Equal(EvaSubmissionOutcome.Succeeded, result.Outcome);
    }

    /// <summary>
    /// Refused credentials are terminal. Retrying them would burn the attempt
    /// budget on an answer that will not change.
    /// </summary>
    [Fact]
    public async Task RefusedCredentialsAreARejectionAndNotRetried()
    {
        using var handler = new DelegateHandler((request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(
                        """{"error":"unauthorized_client","error_description":"Invalid Client ID or Secret"}""",
                        Encoding.UTF8,
                        "application/json")
                }
                : Ok(RecordedSuccess));
        using var client = new HttpClient(handler);

        var result = await Transport(client).SubmitInstructionAsync(Payload());

        Assert.Equal(EvaSubmissionOutcome.Rejected, result.Outcome);
        Assert.False(EvaSubmissionPolicy.IsRetryable(result.Outcome));
        Assert.Equal("eva_auth_401", result.FailureCode);
    }

    /// <summary>
    /// Images travel as base64 inside the instruction body, in EVA's own file
    /// model, in the order they were given.
    /// </summary>
    [Fact]
    public async Task ImagesTravelAsBase64InsideTheInstruction()
    {
        string? submitted = null;
        await SubmitAsync(
            (request, body) =>
            {
                if (!request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal))
                {
                    submitted = body;
                }

                return Ok(RecordedSuccess);
            },
            Payload(
                new EvaInstructionFile("001 front", ".jpg", new byte[] { 1, 2, 3 }),
                new EvaInstructionFile("002 rear", ".png", new byte[] { 4, 5, 6 })));

        using var document = JsonDocument.Parse(submitted!);
        var files = document.RootElement.GetProperty("Files").EnumerateArray().ToArray();

        Assert.Equal(2, files.Length);
        Assert.Equal("001 front", files[0].GetProperty("Name").GetString());
        Assert.Equal(".jpg", files[0].GetProperty("Extension").GetString());
        Assert.Equal(
            Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            files[0].GetProperty("Data").GetString());
        Assert.Equal("002 rear", files[1].GetProperty("Name").GetString());
    }

    /// <summary>
    /// The payload reaches EVA under EVA's field names, not ours.
    /// </summary>
    [Fact]
    public async Task TheInstructionUsesEvasFieldNames()
    {
        string? submitted = null;
        await SubmitAsync((request, body) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal))
            {
                submitted = body;
            }

            return Ok(RecordedSuccess);
        });

        using var document = JsonDocument.Parse(submitted!);
        var root = document.RootElement;

        Assert.Equal("COLLENGAPI", root.GetProperty("RequestFrom").GetString());
        Assert.Equal("QDOS26031", root.GetProperty("ExternalRef").GetString());
        Assert.Equal("MT15OYK", root.GetProperty("VehReg").GetString());
        Assert.Equal("A Smith", root.GetProperty("InsName").GetString());
        Assert.Equal("2026-01-31T00:00:00Z", root.GetProperty("DtIncident").GetString());
    }

    private static async Task<EvaSubmissionResult> SubmitAsync(
        Func<HttpRequestMessage, string?, HttpResponseMessage> responder,
        EvaInstructionPayload? payload = null)
    {
        using var handler = new DelegateHandler((request, body) =>
            request.RequestUri!.AbsolutePath.EndsWith("Connect/token", StringComparison.Ordinal)
                ? Ok("""{"access_token":"tok","expires_in":5}""")
                : responder(request, body));
        using var client = new HttpClient(handler);
        return await Transport(client).SubmitInstructionAsync(payload ?? Payload());
    }

    private static Func<HttpRequestMessage, string?, HttpResponseMessage> Responder(
        HttpStatusCode status,
        string body,
        string mediaType = "application/json") =>
        (_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static EvaApiTransport Transport(HttpClient client, TimeProvider? time = null) =>
        new EvaApiTransport(Options(), client, time ?? TimeProvider.System);

    private static EvaApiOptions Options() => EvaApiOptions.Create(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Eva:BaseUri"] = BaseUri.ToString(),
            ["Eva:ClientId"] = "client",
            ["Eva:ClientSecret"] = "secret",
            ["Eva:RequestFrom"] = "COLLENGAPI",
            ["Eva:InspectionType"] = "Vehicle Damage Inspection",
            ["Eva:InstructionEmail"] = "digital@collisionengineers.co.uk"
        });

    private static EvaInstructionPayload Payload(params EvaInstructionFile[] files) =>
        CaseEvaApiMapping.Map(
            new EvaReplayFields(
                "Connexus",
                "MT15OYK",
                "Land Rover Defender 110",
                "A Smith",
                "AKH/47743/1",
                "31/01/2026",
                "05/02/2026",
                "10/02/2026",
                "Image Based Assessment",
                "Rear-end collision.",
                "20%",
                "43850",
                "Miles"),
            "QDOS26031",
            Options().Instruction,
            files);

    /// <summary>
    /// A clock the test moves. The repository has no time-provider testing
    /// package and adding one for a single assertion is not worth a new
    /// dependency; the existing tests use small local fakes the same way.
    /// </summary>
    private sealed class MovableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset now = utcNow;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan delta) => now = now.Add(delta);
    }

    /// <summary>
    /// The house HTTP fake, extended to capture the request body — which is
    /// most of what these tests assert on.
    /// </summary>
    private sealed class DelegateHandler(
        Func<HttpRequestMessage, string?, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return handler(request, body);
        }
    }
}
