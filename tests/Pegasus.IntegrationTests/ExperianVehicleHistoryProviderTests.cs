using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Experian;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Experian as the vehicle history check (ADR-0089): the password-grant
/// sign-in and its cached token, the condition-only Custom Search (R741), the
/// write-off records without the removed ones (R743), Experian's own "not
/// found" (R748), and the one answer — unavailable — every other failure
/// gives (R747), with the credentials, token and registration kept out of
/// every URL and log.
/// </summary>
public sealed class ExperianVehicleHistoryProviderTests
{
    private const string BaseUri = "https://sandbox-uk-api.experian.com/ukci/automotive";
    private const string TokenUri = "https://sandbox-uk-api.experian.com/oauth2/v1/token";
    private const string ClientId = "synthetic-client-id";
    private const string ClientSecret = "synthetic-client-secret";
    private const string Username = "synthetic-user";
    private const string Password = "synthetic-experian-password";
    private const string AccessToken = "synthetic-access-token";
    private const string Registration = "AB12CDE";

    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private const string Token = $$$"""{"access_token":"{{{AccessToken}}}","expires_in":"1800","token_type":"Bearer"}""";

    private const string CleanVehicle = """{"vehicle":{"summary":{"vrm":"AB12CDE"},"provenance":{"condition":[]}}}""";

    [Fact]
    public async Task ACleanVehicleSignsInWithThePasswordGrantAndAsksForConditionOnly()
    {
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, CleanVehicle));

        var result = await harness.Provider.CheckAsync(Registration, default);

        Assert.True(result.VehicleFound);
        Assert.Empty(result.WriteOffs);
        Assert.Equal(2, harness.Requests.Count);

        var token = harness.Requests[0];
        Assert.Equal(HttpMethod.Post, token.Method);
        Assert.Equal(new Uri(TokenUri), token.Uri);
        Assert.Equal("password", Assert.Single(token.Headers["Grant_type"]));
        using (var body = JsonDocument.Parse(token.Body!))
        {
            Assert.Equal(Username, body.RootElement.GetProperty("username").GetString());
            Assert.Equal(Password, body.RootElement.GetProperty("password").GetString());
            Assert.Equal(ClientId, body.RootElement.GetProperty("client_id").GetString());
            Assert.Equal(ClientSecret, body.RootElement.GetProperty("client_secret").GetString());
        }

        var vehicle = harness.Requests[1];
        Assert.Equal(HttpMethod.Post, vehicle.Method);
        Assert.Equal(new Uri(BaseUri + "/v2/vehicle/"), vehicle.Uri);
        Assert.Equal("Bearer " + AccessToken, vehicle.Authorization);
        Assert.Equal("""{"vrm":"AB12CDE","data":{"condition":true}}""", vehicle.Body);
        Assert.DoesNotContain(Registration, vehicle.Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteOffsAreCarriedWithoutTheRemovedOnes()
    {
        const string answer = """
            {"vehicle":{"provenance":{"condition":[
              {"losstype":"Category S","status":"Written off","causeofdamage":"A","lossdate":"2021-03-12T00:00:00","insurername":"Aviva","claimnumber":"123456"},
              {"losstype":"Category N","causeofdamage":"F","lossdate":"2019-01-02T00:00:00","insurername":"Direct Line","dateremoved":"2019-06-01T00:00:00"},
              {"status":"Category C","causeofdamage":"Z"}
            ]}}}
            """;
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, answer));

        var result = await harness.Provider.CheckAsync(Registration, default);

        Assert.True(result.VehicleFound);
        Assert.Collection(
            result.WriteOffs,
            first => Assert.Equal(new VehicleWriteOffRecord("Category S", "Accident", new DateOnly(2021, 3, 12), "Aviva", "123456"), first),
            second => Assert.Equal(new VehicleWriteOffRecord("Category C", "Z", null, null, null), second));
    }

    [Fact]
    public async Task TheTokenIsReusedWhileItHasAMinuteLeftAndRenewedAfter()
    {
        var clock = new MovingClock(Now);
        var harness = Harness.Create(
            vehicle: _ => Json(HttpStatusCode.OK, CleanVehicle),
            token: () => Json(HttpStatusCode.OK, """{"access_token":"t","expires_in":120}"""),
            clock: clock);

        await harness.Provider.CheckAsync(Registration, default);
        clock.UtcNow = Now.AddSeconds(30);
        await harness.Provider.CheckAsync(Registration, default);
        Assert.Equal(1, harness.TokenRequests);

        clock.UtcNow = Now.AddSeconds(61);
        await harness.Provider.CheckAsync(Registration, default);
        Assert.Equal(2, harness.TokenRequests);
    }

    [Fact]
    public async Task ATokenExperianRefusesIsRenewedOnce()
    {
        var calls = 0;
        var harness = Harness.Create(vehicle: _ => ++calls == 1
            ? Json(HttpStatusCode.Unauthorized, "{}")
            : Json(HttpStatusCode.OK, CleanVehicle));

        var result = await harness.Provider.CheckAsync(Registration, default);

        Assert.True(result.VehicleFound);
        Assert.Equal(2, harness.TokenRequests);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExperiansOwnNotFoundIsItsAnswer()
    {
        var harness = Harness.Create(vehicle: _ => Json(
            HttpStatusCode.NotFound,
            """{"detail":"No vehicle found","errors":{"error":"Vehicle not found"},"status":404,"title":"Not Found"}"""));

        var result = await harness.Provider.CheckAsync(Registration, default);

        Assert.False(result.VehicleFound);
        Assert.Contains(harness.Logger.Messages, message => message.Contains("experian.history.not_found", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "experian.history.provider_failed")]
    [InlineData(HttpStatusCode.BadRequest, "experian.history.rejected")]
    [InlineData(HttpStatusCode.Forbidden, "experian.history.forbidden")]
    [InlineData(HttpStatusCode.TooManyRequests, "experian.history.throttled")]
    [InlineData(HttpStatusCode.InternalServerError, "experian.history.provider_failed")]
    public async Task EveryOtherRefusalIsUnavailableAndLoggedByCode(HttpStatusCode status, string code)
    {
        // A 404 without Experian's error body is a gateway's, not Experian's answer.
        var harness = Harness.Create(vehicle: _ => Json(status, "<html>refused</html>"));

        await Assert.ThrowsAsync<VehicleHistoryCheckUnavailableException>(() =>
            harness.Provider.CheckAsync(Registration, default));

        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains(code, StringComparison.Ordinal) && message.Contains("stage=vehicle", StringComparison.Ordinal));
        harness.AssertNothingSecretLogged();
    }

    [Fact]
    public async Task ARefusedSignInIsUnavailable()
    {
        var harness = Harness.Create(
            vehicle: _ => Json(HttpStatusCode.OK, CleanVehicle),
            token: () => Json(HttpStatusCode.Unauthorized, """{"error":"invalid_grant"}"""));

        await Assert.ThrowsAsync<VehicleHistoryCheckUnavailableException>(() =>
            harness.Provider.CheckAsync(Registration, default));

        Assert.Single(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("experian.history.forbidden", StringComparison.Ordinal) && message.Contains("stage=token", StringComparison.Ordinal));
        harness.AssertNothingSecretLogged();
    }

    [Fact]
    public async Task AnUnreadableAnswerIsUnavailable()
    {
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, "not json"));

        await Assert.ThrowsAsync<VehicleHistoryCheckUnavailableException>(() =>
            harness.Provider.CheckAsync(Registration, default));

        Assert.Contains(harness.Logger.Messages, message => message.Contains("experian.history.unreadable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATransportFailureIsUnavailable()
    {
        var harness = Harness.Create(vehicle: _ => throw new HttpRequestException("connection reset"));

        await Assert.ThrowsAsync<VehicleHistoryCheckUnavailableException>(() =>
            harness.Provider.CheckAsync(Registration, default));

        Assert.Contains(harness.Logger.Messages, message => message.Contains("experian.history.transport", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnresolvedSecretIsUnavailableBeforeAnyCall()
    {
        var harness = Harness.Create(
            vehicle: _ => Json(HttpStatusCode.OK, CleanVehicle),
            account: () => ExperianAccount.Create(key => key == "Experian:Account:Password"
                ? "@Microsoft.KeyVault(SecretUri=https://vault.vault.azure.net/secrets/experian-password/1)"
                : Setting(key)));

        await Assert.ThrowsAsync<VehicleHistoryCheckUnavailableException>(() =>
            harness.Provider.CheckAsync(Registration, default));

        Assert.Empty(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("experian.history.configuration", StringComparison.Ordinal)
            && message.Contains("Experian:Account:Password", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://sandbox-uk-api.experian.com/ukci/automotive")]
    [InlineData("https://experian.example.test/ukci/automotive")]
    [InlineData("https://user@sandbox-uk-api.experian.com/ukci/automotive")]
    public void TheAccountRefusesAnAddressThatIsNotHttpsExperian(string baseUri)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExperianAccount.Create(key => key == "Experian:BaseUri" ? baseUri : Setting(key)));

        Assert.Equal("Experian:BaseUri must be an HTTPS Experian address.", exception.Message);
        Assert.Equal(nameof(ExperianAccount), ExperianAccount.Create(Setting).ToString());
    }

    private static string? Setting(string key) => key switch
    {
        "Experian:BaseUri" => BaseUri,
        "Experian:TokenUri" => TokenUri,
        "Experian:Account:ClientId" => ClientId,
        "Experian:Account:ClientSecret" => ClientSecret,
        "Experian:Account:Username" => Username,
        "Experian:Account:Password" => Password,
        _ => null,
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        CazanaGuideValuationProviderTests.Json(status, body);

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        IReadOnlyDictionary<string, string[]> Headers,
        string? Body);

    private sealed class Harness(
        ExperianVehicleHistoryProvider provider,
        CapturingLogger logger,
        List<RecordedRequest> requests)
    {
        public ExperianVehicleHistoryProvider Provider { get; } = provider;

        public CapturingLogger Logger { get; } = logger;

        public List<RecordedRequest> Requests { get; } = requests;

        public int TokenRequests => Requests.Count(request => request.Uri == new Uri(TokenUri));

        public static Harness Create(
            Func<HttpRequestMessage, HttpResponseMessage> vehicle,
            Func<HttpResponseMessage>? token = null,
            Func<ExperianAccount>? account = null,
            TimeProvider? clock = null)
        {
            var requests = new List<RecordedRequest>();
            var logger = new CapturingLogger();
            return new(
                new ExperianVehicleHistoryProvider(
                    new ScriptedClientFactory(new CazanaGuideValuationProviderTests.Answering(async request =>
                    {
                        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                        requests.Add(new(
                            request.Method,
                            request.RequestUri!,
                            request.Headers.Authorization?.ToString(),
                            request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                            body));
                        return request.RequestUri == new Uri(TokenUri)
                            ? token?.Invoke() ?? Json(HttpStatusCode.OK, Token)
                            : vehicle(request);
                    })),
                    account ?? (() => ExperianAccount.Create(Setting)),
                    clock ?? new MovingClock(Now),
                    logger),
                logger,
                requests);
        }

        public void AssertNothingSecretLogged()
        {
            foreach (var message in Logger.Messages)
            {
                Assert.DoesNotContain(Password, message, StringComparison.Ordinal);
                Assert.DoesNotContain(ClientSecret, message, StringComparison.Ordinal);
                Assert.DoesNotContain(AccessToken, message, StringComparison.Ordinal);
                Assert.DoesNotContain(Registration, message, StringComparison.Ordinal);
            }
        }
    }

    private sealed class MovingClock(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class CapturingLogger : ILogger<ExperianVehicleHistoryProvider>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
