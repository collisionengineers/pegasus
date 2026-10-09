using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Cazana;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Cazana as a guide valuation source (ADR-0066) against a scripted valuation
/// API: the figures, the date a month is valued on, the key kept out of every
/// URL and log, the info answer for a registration Cazana holds no data for,
/// and the one answer — unavailable — every other failure gives the card.
/// </summary>
public sealed class CazanaGuideValuationProviderTests
{
    private const string Key = "synthetic-cazana-key-0123456789";
    private static readonly DateTimeOffset Now = new(2031, 4, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly April = new(2031, 4, 1);

    private const string Figures =
        """{"cid":1234567,"make":"Ford","model":"Fiesta","mileage":{"value":33000,"estimate":false},"valuation":{"retail":10450,"trade":8725,"independent":10100,"date":"2031-04-15"}}""";

    [Fact]
    public async Task TheFiguresAreCazanasRetailAndTradeValuedTodayForTheCurrentMonth()
    {
        var harness = Harness.Create(_ => Json(HttpStatusCode.OK, Figures));

        var quote = await harness.Provider.GetAsync(Request(" ab12 cde ", April), default);

        Assert.Equal(10450m, quote.RetailValue);
        Assert.Equal(8725m, quote.TradeValue);
        Assert.Equal(April, quote.GuideMonth);
        Assert.Equal(33000, quote.Mileage);
        Assert.Null(quote.Report);
        Assert.Null(quote.Vin);

        var sent = Assert.Single(harness.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://api.cazana.com/valuation/1.0?vrm=AB12CDE&mileage=33000", sent.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", sent.Headers.Authorization!.Scheme);
        Assert.Equal(Key, sent.Headers.Authorization.Parameter);
        Assert.DoesNotContain(Key, sent.RequestUri.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Cazana values on a day: an earlier month on its last day, a later month
    /// today as the current month (operator, 9 October 2026).
    /// </summary>
    [Theory]
    [InlineData("2031-02", "&date=2031-02-28", "2031-02")]
    [InlineData("2028-02", "&date=2028-02-29", "2028-02")]
    [InlineData("2030-12", "&date=2030-12-31", "2030-12")]
    [InlineData("2031-06", "", "2031-04")]
    public async Task AMonthIsValuedOnItsLastDayOrTodayAndNeverAhead(string month, string dateQuery, string answeredMonth)
    {
        var harness = Harness.Create(_ => Json(HttpStatusCode.OK, Figures));

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", Month(month)), default);

        Assert.Equal(Month(answeredMonth), quote.GuideMonth);
        Assert.EndsWith("mileage=33000" + dateQuery, Assert.Single(harness.Requests).RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKilometreReadingIsSentInWholeMiles()
    {
        var harness = Harness.Create(_ => Json(HttpStatusCode.OK, Figures));

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April, 53108, "km"), default);

        Assert.Contains("mileage=33000", Assert.Single(harness.Requests).RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(53108, quote.Mileage);
    }

    /// <summary>
    /// Cazana holds no data for the registration: nothing is broken, so the
    /// card says so as information (operator, 9 October 2026).
    /// </summary>
    [Fact]
    public async Task ARegistrationCazanaHoldsNoDataForIsNotValuedRatherThanUnavailable()
    {
        var harness = Harness.Create(_ => Json(
            HttpStatusCode.NotFound, """{"error":{"code":404,"message":"VRM unavailable"}}"""));

        var refused = await Assert.ThrowsAsync<GuideValuationNotValuedException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.Equal(GuideValuationNotValuedReason.NoVehicleData, refused.Reason);
        Assert.Equal(ValuationSource.Cazana, refused.ValuationSource);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("cazana.valuation.not_found status=404 error=VRM unavailable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(400, """{"error":{"code":400,"message":"Bad Request","data":["mileage must be less than or equal to 500000"]}}""", "cazana.valuation.rejected status=400 error=Bad Request; mileage must be less than or equal to 500000")]
    [InlineData(401, """{"error":"Authorization field missing"}""", "cazana.valuation.forbidden status=401 error=Authorization field missing")]
    [InlineData(403, """{"error":"Access to this API has been disallowed"}""", "cazana.valuation.forbidden status=403 error=Access to this API has been disallowed")]
    [InlineData(429, "", "cazana.valuation.throttled status=429 error=unreadable")]
    [InlineData(500, """{"error":{"code":500,"message":"Server Error"}}""", "cazana.valuation.provider_failed status=500 error=Server Error")]
    [InlineData(503, "<html>down</html>", "cazana.valuation.provider_failed status=503 error=unreadable")]
    public async Task EveryOtherRefusalIsUnavailableAndLoggedByItsCode(int status, string body, string logged)
    {
        var harness = Harness.Create(_ => Json((HttpStatusCode)status, body));

        await AssertUnavailableAsync(harness, logged);
    }

    [Theory]
    [InlineData("trade", """{"valuation":{"retail":10450}}""")]
    [InlineData("null", """{"valuation":{"retail":null,"trade":8725}}""")]
    [InlineData("zero", """{"valuation":{"retail":0,"trade":8725}}""")]
    [InlineData("text", """{"valuation":{"retail":"10450","trade":8725}}""")]
    [InlineData("shape", """[]""")]
    public async Task AnAnswerWithoutBothFiguresIsUnavailable(string row, string body)
    {
        Assert.NotEmpty(row);
        var harness = Harness.Create(_ => Json(HttpStatusCode.OK, body));

        await AssertUnavailableAsync(harness, "cazana.valuation.unreadable figures=absent");
    }

    [Fact]
    public async Task AnAnswerThatIsNotJsonIsUnavailable()
    {
        var harness = Harness.Create(_ => Json(HttpStatusCode.OK, "<html>maintenance</html>"));

        await AssertUnavailableAsync(harness, "cazana.valuation.unreadable json=invalid");
    }

    [Fact]
    public async Task TheNetworkAndATimeoutAreUnavailable()
    {
        await AssertUnavailableAsync(
            Harness.Create(_ => throw new HttpRequestException("connection refused")),
            "cazana.valuation.transport HttpRequestException");
        await AssertUnavailableAsync(
            Harness.Create(_ => throw new TaskCanceledException("timed out")),
            "cazana.valuation.transport TaskCanceledException");
    }

    [Fact]
    public async Task TheCallerGivingUpIsTheCallersOwnOutcome()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var harness = Harness.Create(_ => throw new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), cancelled.Token));
    }

    [Fact]
    public async Task AnUnreadableMileageUnitIsUnavailableAndAsksNothing()
    {
        var harness = Harness.Create(_ => Json(HttpStatusCode.OK, Figures));

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April, 33000, "furlongs"), default));

        Assert.Empty(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("cazana.valuation.mileage_unit unit=unreadable", StringComparison.Ordinal));
    }

    /// <summary>
    /// An unresolved Key Vault reference fails only the valuation: the card
    /// is unavailable, the log names the key, and Cazana is never asked.
    /// </summary>
    [Fact]
    public async Task AnUnresolvedKeyIsUnavailableAndNamesTheKeyNotItsValue()
    {
        var reference = "@Microsoft.KeyVault(SecretUri=https://vault.test/secrets/cazana-api-key/1)";
        var harness = Harness.Create(
            _ => Json(HttpStatusCode.OK, Figures),
            () => CazanaApiKey.Create(_ => reference));

        await AssertUnavailableAsync(
            harness,
            "cazana.valuation.configuration Cazana:ApiKey is an unresolved Key Vault reference");
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public void TheKeyIsOneUnbrokenTokenAndNeverPrinted()
    {
        Assert.Throws<InvalidOperationException>(() => CazanaApiKey.Create(_ => null));
        Assert.Throws<InvalidOperationException>(() => CazanaApiKey.Create(_ => "  "));
        Assert.Throws<InvalidOperationException>(() => CazanaApiKey.Create(_ => "two words"));
        Assert.Throws<InvalidOperationException>(() => CazanaApiKey.Create(_ => "line\nbreak"));

        var key = CazanaApiKey.Create(name => name == "Cazana:ApiKey" ? $" {Key} " : null);

        Assert.Equal(Key, key.Value);
        Assert.DoesNotContain(Key, key.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Composing and resolving the provider reads no key: it is read on each
    /// valuation, so an unresolved Key Vault reference never stops a Case page.
    /// </summary>
    [Fact]
    public void AddCazanaGuideValuationComposesOneProviderAndReadsTheKeyOnlyWhenValuing()
    {
        var reads = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddCazanaGuideValuation(_ =>
        {
            reads++;
            return CazanaApiKey.Create(static _ => null);
        });
        using var provider = services.BuildServiceProvider();

        var source = Assert.Single(provider.GetServices<IGuideValuationProvider>());

        Assert.IsType<CazanaGuideValuationProvider>(source);
        Assert.Equal(ValuationSource.Cazana, source.Source);
        Assert.Equal(0, reads);
    }

    private static async Task AssertUnavailableAsync(Harness harness, string logged)
    {
        var refused = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.NotNull(refused.InnerException);
        Assert.Contains(harness.Logger.Messages, message => message.Contains(logged, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains(Key, StringComparison.Ordinal));
    }

    private static DateOnly Month(string month) =>
        DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static GuideValuationRequest Request(
        string registration, DateOnly month, long mileage = 33000, string? unit = null) => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
        Guid.NewGuid(),
        registration,
        mileage,
        unit,
        month);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Harness
    {
        private Harness(CazanaGuideValuationProvider provider, CapturingLogger logger, List<HttpRequestMessage> requests)
        {
            Provider = provider;
            Logger = logger;
            Requests = requests;
        }

        public CazanaGuideValuationProvider Provider { get; }

        public CapturingLogger Logger { get; }

        public List<HttpRequestMessage> Requests { get; }

        public static Harness Create(
            Func<HttpRequestMessage, HttpResponseMessage> answer,
            Func<CazanaApiKey>? apiKey = null)
        {
            var requests = new List<HttpRequestMessage>();
            var logger = new CapturingLogger();
            return new(
                new CazanaGuideValuationProvider(
                    new ScriptedClientFactory(new Answering(request =>
                    {
                        requests.Add(request);
                        return answer(request);
                    })),
                    apiKey ?? (() => CazanaApiKey.Create(_ => Key)),
                    new FixedClock(Now),
                    logger),
                logger,
                requests);
        }
    }

    private sealed class Answering(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(answer(request));
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingLogger : ILogger<CazanaGuideValuationProvider>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
