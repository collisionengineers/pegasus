using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Experian;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CAP as a guide valuation source through Experian (ADR-0089, R1201): the
/// CAP-valuation Custom Search at the Case's mileage in whole miles, CAP's
/// retail and clean as the card's retail and trade (R1202), the current month
/// whatever month was asked (R1203), Experian's own "not found" as no data
/// and every other failure as unavailable (R1204), nothing but figures on the
/// quote (R1205), and the one sign-in it shares with the vehicle history
/// check.
/// </summary>
public sealed class ExperianCapValuationProviderTests
{
    private const string BaseUri = "https://sandbox-uk-api.experian.com/ukci/automotive";
    private const string TokenUri = "https://sandbox-uk-api.experian.com/oauth2/v1/token";
    private const string ClientSecret = "synthetic-client-secret";
    private const string Password = "synthetic-experian-password";
    private const string AccessToken = "synthetic-access-token";
    private const string Registration = "AB12CDE";

    // 19 November 2026, London.
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly CurrentMonth = new(2026, 11, 1);

    private const string Token = $$$"""{"access_token":"{{{AccessToken}}}","expires_in":"1800","token_type":"Bearer"}""";

    private const string Valued = """
        {"vehicle":{"summary":{"vrm":"AB12CDE"},"valuations":{"cap":{"valuation":
          {"retail":9450,"clean":7800,"average":7300,"below":6500,"valuationmileage":41000}}}}}
        """;

    [Fact]
    public async Task ACapValuationAsksAtTheCasesMileageAndAnswersRetailAndCleanForTheCurrentMonth()
    {
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, Valued));

        // An earlier month is asked; CAP is valued only as it stands today.
        var quote = await harness.Provider.GetAsync(Request(new DateOnly(2026, 8, 1)), default);

        Assert.Equal(ValuationSource.Cap, harness.Provider.Source);
        Assert.Equal(9_450m, quote.RetailValue);
        Assert.Equal(7_800m, quote.TradeValue);
        Assert.Equal(CurrentMonth, quote.GuideMonth);
        Assert.Equal(41_000L, quote.Mileage);
        Assert.Null(quote.Report);
        Assert.Null(quote.Vin);

        Assert.Equal(2, harness.Requests.Count);
        var vehicle = harness.Requests[1];
        Assert.Equal(HttpMethod.Post, vehicle.Method);
        Assert.Equal(new Uri(BaseUri + "/v2/vehicle/"), vehicle.Uri);
        Assert.Equal("Bearer " + AccessToken, vehicle.Authorization);
        Assert.Equal(
            """{"vrm":"AB12CDE","currentmileage":41000,"data":{"cap":{"valuation":true}}}""",
            vehicle.Body);
        Assert.DoesNotContain(Registration, vehicle.Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Empty(harness.Logger.Messages);
    }

    [Fact]
    public async Task AKilometreReadingIsAskedInWholeMiles()
    {
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, Valued));

        var quote = await harness.Provider.GetAsync(Request(CurrentMonth, mileage: 100_000, unit: "km"), default);

        Assert.Contains("\"currentmileage\":62137,", harness.Requests[1].Body, StringComparison.Ordinal);
        // The quote stands for the Case's own reading.
        Assert.Equal(100_000L, quote.Mileage);
    }

    [Fact]
    public async Task AnUnreadableMileageUnitIsUnavailableAndAsksNothing()
    {
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, Valued));

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(CurrentMonth, unit: "furlongs"), default));

        Assert.Empty(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("experian.cap.mileage_unit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExperiansOwnNotFoundIsNoDataForTheRegistration()
    {
        var harness = Harness.Create(vehicle: _ => Json(
            HttpStatusCode.NotFound,
            """{"detail":"Vehicle not found","errors":{"error":"Vehicle not found on database"},"status":404,"title":"Not Found"}"""));

        var notValued = await Assert.ThrowsAsync<GuideValuationNotValuedException>(() =>
            harness.Provider.GetAsync(Request(CurrentMonth), default));

        Assert.Equal(ValuationSource.Cap, notValued.ValuationSource);
        Assert.Equal(GuideValuationNotValuedReason.NoVehicleData, notValued.Reason);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("experian.cap.not_found", StringComparison.Ordinal));
        harness.AssertNothingSecretLogged();
    }

    /// <summary>
    /// An answer that names the vehicle but not CAP's two figures has not
    /// valued it: unavailable, never a card of blanks or zeroes, with
    /// Experian's message ids in the log.
    /// </summary>
    [Theory]
    [InlineData("""{"vehicle":{"summary":{"vrm":"AB12CDE"}},"messages":[{"id":"100","title":"Permission"}]}""", "valuation=absent messages=100")]
    [InlineData("""{"vehicle":{"valuations":{"cap":{"messages":["No valuation"]}}}}""", "valuation=absent")]
    [InlineData("""{"vehicle":{"valuations":{"cap":{"valuation":{"retail":9450}}}}}""", "figures=absent")]
    [InlineData("""{"vehicle":{"valuations":{"cap":{"valuation":{"retail":0,"clean":0}}}}}""", "figures=absent")]
    [InlineData("""{"messages":[]}""", "vehicle=absent")]
    public async Task AnAnswerWithoutCapsFiguresIsUnavailable(string answer, string detail)
    {
        var harness = Harness.Create(vehicle: _ => Json(HttpStatusCode.OK, answer));

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(CurrentMonth), default));

        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("experian.cap.unreadable", StringComparison.Ordinal)
            && message.Contains(detail, StringComparison.Ordinal));
        harness.AssertNothingSecretLogged();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "experian.cap.provider_failed")]
    [InlineData(HttpStatusCode.BadRequest, "experian.cap.rejected")]
    [InlineData(HttpStatusCode.Forbidden, "experian.cap.forbidden")]
    [InlineData(HttpStatusCode.TooManyRequests, "experian.cap.throttled")]
    [InlineData(HttpStatusCode.InternalServerError, "experian.cap.provider_failed")]
    public async Task EveryOtherRefusalIsUnavailableAndLoggedByCode(HttpStatusCode status, string code)
    {
        // A 404 without Experian's error body is a gateway's, not Experian's answer.
        var harness = Harness.Create(vehicle: _ => Json(status, "<html>refused</html>"));

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(CurrentMonth), default));

        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains(code, StringComparison.Ordinal) && message.Contains("stage=vehicle", StringComparison.Ordinal));
        harness.AssertNothingSecretLogged();
    }

    [Fact]
    public async Task ATokenExperianRefusesIsRenewedOnce()
    {
        var calls = 0;
        var harness = Harness.Create(vehicle: _ => ++calls == 1
            ? Json(HttpStatusCode.Unauthorized, "{}")
            : Json(HttpStatusCode.OK, Valued));

        var quote = await harness.Provider.GetAsync(Request(CurrentMonth), default);

        Assert.Equal(9_450m, quote.RetailValue);
        Assert.Equal(2, harness.TokenRequests);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AnUnresolvedSecretIsUnavailableBeforeAnyCall()
    {
        var harness = Harness.Create(
            vehicle: _ => Json(HttpStatusCode.OK, Valued),
            account: () => ExperianAccount.Create(key => key == "Experian:Account:Password"
                ? "@Microsoft.KeyVault(SecretUri=https://vault.vault.azure.net/secrets/experian-password/1)"
                : Setting(key)));

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(CurrentMonth), default));

        Assert.Empty(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("experian.cap.configuration", StringComparison.Ordinal)
            && message.Contains("Experian:Account:Password", StringComparison.Ordinal));
    }

    /// <summary>
    /// The valuation and the vehicle history check are one account's two
    /// acts: they share the client, so the second act signs in with the token
    /// the first one fetched.
    /// </summary>
    [Fact]
    public async Task TheValuationAndTheHistoryCheckShareOneSignIn()
    {
        var harness = Harness.Create(vehicle: _ => Json(
            HttpStatusCode.OK,
            """{"vehicle":{"provenance":{"condition":[]},"valuations":{"cap":{"valuation":{"retail":9450,"clean":7800}}}}}"""));
        var history = new ExperianVehicleHistoryProvider(
            harness.Client, new CapturingLogger<ExperianVehicleHistoryProvider>());

        await harness.Provider.GetAsync(Request(CurrentMonth), default);
        var check = await history.CheckAsync(Registration, default);

        Assert.True(check.VehicleFound);
        Assert.Equal(1, harness.TokenRequests);
        Assert.Equal(3, harness.Requests.Count);
    }

    /// <summary>
    /// A host that composes both acts has one Experian client and one CAP
    /// provider, and building them reads no account: the account is read on
    /// each call, so an unresolved Key Vault reference never stops a Case page.
    /// </summary>
    [Fact]
    public void BothActsComposeOneClientAndReadTheAccountOnlyWhenCalling()
    {
        var reads = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddExperianVehicleHistoryCheck(_ =>
        {
            reads++;
            return ExperianAccount.Create(static _ => null);
        });
        services.AddExperianCapValuation(_ =>
        {
            reads++;
            return ExperianAccount.Create(static _ => null);
        });
        using var provider = services.BuildServiceProvider();

        var source = Assert.Single(provider.GetServices<IGuideValuationProvider>());
        Assert.IsType<ExperianCapValuationProvider>(source);
        Assert.Equal(ValuationSource.Cap, source.Source);
        Assert.IsType<ExperianVehicleHistoryProvider>(
            Assert.Single(provider.GetServices<IVehicleHistoryCheckProvider>()));
        Assert.Single(provider.GetServices<ExperianClient>());
        Assert.Equal(0, reads);
    }

    private static GuideValuationRequest Request(DateOnly month, long mileage = 41_000, string? unit = null) => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
        Guid.NewGuid(),
        Registration,
        mileage,
        unit,
        month);

    private static string? Setting(string key) => key switch
    {
        "Experian:BaseUri" => BaseUri,
        "Experian:TokenUri" => TokenUri,
        "Experian:Account:ClientId" => "synthetic-client-id",
        "Experian:Account:ClientSecret" => ClientSecret,
        "Experian:Account:Username" => "synthetic-user",
        "Experian:Account:Password" => Password,
        _ => null,
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        CazanaGuideValuationProviderTests.Json(status, body);

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body);

    private sealed class Harness(
        ExperianClient client,
        ExperianCapValuationProvider provider,
        CapturingLogger<ExperianCapValuationProvider> logger,
        List<RecordedRequest> requests)
    {
        public ExperianClient Client { get; } = client;

        public ExperianCapValuationProvider Provider { get; } = provider;

        public CapturingLogger<ExperianCapValuationProvider> Logger { get; } = logger;

        public List<RecordedRequest> Requests { get; } = requests;

        public int TokenRequests => Requests.Count(request => request.Uri == new Uri(TokenUri));

        public static Harness Create(
            Func<HttpRequestMessage, HttpResponseMessage> vehicle,
            Func<ExperianAccount>? account = null)
        {
            var requests = new List<RecordedRequest>();
            var logger = new CapturingLogger<ExperianCapValuationProvider>();
            var clock = new CazanaGuideValuationProviderTests.FixedClock(Now);
            var client = new ExperianClient(
                new ScriptedClientFactory(new CazanaGuideValuationProviderTests.Answering(async request =>
                {
                    var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                    requests.Add(new(
                        request.Method,
                        request.RequestUri!,
                        request.Headers.Authorization?.ToString(),
                        body));
                    return request.RequestUri == new Uri(TokenUri)
                        ? Json(HttpStatusCode.OK, Token)
                        : vehicle(request);
                })),
                account ?? (() => ExperianAccount.Create(Setting)),
                clock);
            return new(client, new ExperianCapValuationProvider(client, clock, logger), logger, requests);
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
