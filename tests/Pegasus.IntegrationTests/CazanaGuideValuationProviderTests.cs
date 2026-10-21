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
/// Cazana as a guide valuation source through the Companion account
/// (ADR-0079): the guest sign-in, the one metered lookup, the retail and
/// trade (<c>remarket</c>) figures, the Companion Report and VIN carried on
/// the quote, the date a month is valued on, the credentials and JWT kept out
/// of every URL and log, the info answer when Cazana holds no data for a
/// registration, and the one answer — unavailable — every other failure gives
/// the card.
/// </summary>
public sealed class CazanaGuideValuationProviderTests
{
    private const string Email = "companion@collisionengineers.test";
    private const string Password = "synthetic-cazana-password";
    private const string OnboardingToken = "synthetic-onboarding-token";
    private const string Jwt = "synthetic-jwt";
    private const string Uuid = "8e3e5efb-6539-434c-b5f1-c1180d09e7f1";
    private const string Vin = "SB1ZB3AE40E045642";

    private static readonly DateTimeOffset Now = new(2031, 4, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly April = new(2031, 4, 1);

    private const string Branches =
        $$$"""{"id":274424,"token":"{{{OnboardingToken}}}","branches":[{"id":6910,"name":"Collision Engineers","active":1}]}""";

    private const string Confirmed = $$$"""{"jwt":"{{{Jwt}}}"}""";

    private const string Created = $$$"""{"uuid":"{{{Uuid}}}"}""";

    [Fact]
    public async Task TheFiguresVinAndReportComeFromOneCompanionLookupInTheSignedInSession()
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request(" ab12 cde ", April), default);

        Assert.Equal(13657m, quote.RetailValue);
        Assert.Equal(11062m, quote.TradeValue);
        Assert.Equal(April, quote.GuideMonth);
        Assert.Equal(33000, quote.Mileage);
        Assert.Equal(Vin, quote.Vin);
        Assert.Equal("cazana-companion:" + Uuid, quote.Report!.Identity);

        Assert.Equal(
            [
                "POST https://api.cazana.com/guest/onboard/branch",
                "POST https://api.cazana.com/guest/onboard/confirm",
                "POST https://api.cazana.com/app/companion",
                "GET https://api.cazana.com/app/companion/" + Uuid,
            ],
            harness.Requests.Select(request => $"{request.Method} {request.Uri}").ToArray());

        foreach (var guest in harness.Requests.Take(2))
        {
            Assert.Contains("5ab3cc97a8d11a00017332a7a609b760ad0147e0a12295c85fabe703", guest.Headers["X-CZN"]);
            Assert.Null(guest.Authorization);
        }

        foreach (var app in harness.Requests.Skip(2))
        {
            Assert.Equal("Bearer " + Jwt, app.Authorization);
        }

        var lookupBody = harness.Requests[2].Body!;
        Assert.Contains("\"vrm\":\"AB12CDE\"", lookupBody, StringComparison.Ordinal);
        Assert.Contains("\"mileage\":33000", lookupBody, StringComparison.Ordinal);
        Assert.Contains("\"date\":\"2031-04-15\"", lookupBody, StringComparison.Ordinal);
        Assert.DoesNotContain(
            harness.Requests,
            request => request.Uri.AbsoluteUri.Contains(Password, StringComparison.Ordinal)
                || request.Uri.AbsoluteUri.Contains(Jwt, StringComparison.Ordinal));
        Assert.Empty(harness.Logger.Messages);
    }

    /// <summary>
    /// Cazana values on a day: an earlier month on its last day, a later month
    /// today as the current month (operator, 9 October 2026).
    /// </summary>
    [Theory]
    [InlineData("2031-02", "2031-02-28", "2031-02")]
    [InlineData("2028-02", "2028-02-29", "2028-02")]
    [InlineData("2030-12", "2030-12-31", "2030-12")]
    [InlineData("2031-06", "2031-04-15", "2031-04")]
    public async Task AMonthIsValuedOnItsLastDayOrTodayAndNeverAhead(string month, string valuationDate, string answeredMonth)
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", Month(month)), default);

        Assert.Equal(Month(answeredMonth), quote.GuideMonth);
        Assert.Contains($"\"date\":\"{valuationDate}\"", harness.Requests[2].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKilometreReadingIsSentInWholeMiles()
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April, 53108, "km"), default);

        Assert.Contains("\"mileage\":33000", harness.Requests[2].Body!, StringComparison.Ordinal);
        Assert.Equal(53108, quote.Mileage);
    }

    /// <summary>
    /// Cazana holds no data for the registration: nothing is broken, so the
    /// card says so as information (operator, 9 October 2026).
    /// </summary>
    [Fact]
    public async Task ARegistrationCazanaHoldsNoDataForIsNotValuedRatherThanUnavailable()
    {
        var harness = Harness.Create(path => path == "/app/companion"
            ? Json(HttpStatusCode.NotFound, """{"error":{"code":404,"message":"VRM unavailable"}}""")
            : null);

        var refused = await Assert.ThrowsAsync<GuideValuationNotValuedException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.Equal(GuideValuationNotValuedReason.NoVehicleData, refused.Reason);
        Assert.Equal(ValuationSource.Cazana, refused.ValuationSource);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("cazana.valuation.not_found stage=lookup status=404 error=VRM unavailable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/guest/onboard/branch", 401, """{"error":"Bad credentials"}""", "cazana.valuation.forbidden stage=sign_in status=401 error=Bad credentials")]
    [InlineData("/guest/onboard/branch", 404, """{"error":"Not Found"}""", "cazana.valuation.provider_failed stage=sign_in status=404 error=Not Found")]
    [InlineData("/app/companion", 400, """{"error":{"code":400,"message":"Bad Request","data":["mileage must be less than or equal to 500000"]}}""", "cazana.valuation.rejected stage=lookup status=400 error=Bad Request; mileage must be less than or equal to 500000")]
    [InlineData("/app/companion", 429, "", "cazana.valuation.throttled stage=lookup status=429 error=unreadable")]
    [InlineData("/app/companion/" + Uuid, 500, """{"error":{"code":500,"message":"Server Error"}}""", "cazana.valuation.provider_failed stage=read status=500 error=Server Error")]
    public async Task EveryOtherRefusalIsUnavailableAndLoggedByItsCodeAndStage(
        string failingPath, int status, string body, string logged)
    {
        var harness = Harness.Create(path => path == failingPath ? Json((HttpStatusCode)status, body) : null);

        await AssertUnavailableAsync(harness, logged);
    }

    [Fact]
    public async Task AnAccountWithNoActiveBranchIsUnavailableAndNamedAsBranch()
    {
        var harness = Harness.Create(path => path == "/guest/onboard/branch"
            ? Json(HttpStatusCode.OK, """{"id":274424,"token":"t","branches":[{"id":6910,"active":0}]}""")
            : null);

        await AssertUnavailableAsync(harness, "cazana.valuation.branch stage=branch branches=none_active");
    }

    [Theory]
    [InlineData("remarket missing", """{"response":{"valuation":{"retail":13657}}}""")]
    [InlineData("remarket zero", """{"response":{"valuation":{"retail":13657,"remarket":0}}}""")]
    [InlineData("retail text", """{"response":{"valuation":{"retail":"13657","remarket":11062}}}""")]
    public async Task AnAnswerWithoutBothPositiveFiguresIsUnavailable(string row, string body)
    {
        Assert.NotEmpty(row);
        var harness = Harness.Create(path => path == "/app/companion/" + Uuid ? Json(HttpStatusCode.OK, body) : null);

        await AssertUnavailableAsync(harness, "cazana.valuation.unreadable stage=read figures=absent");
    }

    [Theory]
    [InlineData("SB1ZB3AE4********")]
    [InlineData("SHORT")]
    [InlineData("")]
    public async Task AMaskedOrIncompleteVinIsNotCarriedOnTheQuote(string vin)
    {
        var harness = Harness.Create(path => path == "/app/companion/" + Uuid
            ? Json(HttpStatusCode.OK, LookupJson(vin))
            : null);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Null(quote.Vin);
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
        var harness = Harness.Create();

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April, 33000, "furlongs"), default));

        Assert.Empty(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("cazana.valuation.mileage_unit stage=mileage unit=unreadable", StringComparison.Ordinal));
    }

    /// <summary>
    /// An unresolved Key Vault reference fails only the valuation: the card
    /// is unavailable, the log names the key, and Cazana is never asked.
    /// </summary>
    [Theory]
    [InlineData("Cazana:CompanionAccount:Email")]
    [InlineData("Cazana:CompanionAccount:Password")]
    public async Task AnUnresolvedAccountSettingIsUnavailableAndNamesTheKeyNotItsValue(string unresolvedKey)
    {
        var reference = "@Microsoft.KeyVault(SecretUri=https://vault.test/secrets/cazana-companion/1)";
        var harness = Harness.Create(
            account: () => CazanaCompanionAccount.Create(key => key == unresolvedKey ? reference : "configured"));

        await AssertUnavailableAsync(
            harness,
            $"cazana.valuation.configuration {unresolvedKey} is an unresolved Key Vault reference");
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public void TheCompanionAccountRefusesBlankControlAndUnresolvedValuesAndNeverPrintsItsPassword()
    {
        static Func<string, string?> Read(string? email, string? password) =>
            key => key == "Cazana:CompanionAccount:Email" ? email : password;

        Assert.Throws<InvalidOperationException>(() => CazanaCompanionAccount.Create(Read(" ", "secret")));
        Assert.Throws<InvalidOperationException>(() => CazanaCompanionAccount.Create(Read(Email, null)));
        Assert.Throws<InvalidOperationException>(() => CazanaCompanionAccount.Create(Read(Email, "sec\nret")));
        Assert.Throws<InvalidOperationException>(() => CazanaCompanionAccount.Create(
            Read("@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/cazana-companion-email)", "secret")));

        var account = CazanaCompanionAccount.Create(Read($" {Email} ", " spaced secret "));
        Assert.Equal(Email, account.Email);
        Assert.Equal(" spaced secret ", account.Password);
        Assert.DoesNotContain("secret", account.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Composing and resolving the provider reads no account: it is read on
    /// each valuation, so an unresolved Key Vault reference never stops a
    /// Case page.
    /// </summary>
    [Fact]
    public void AddCazanaGuideValuationComposesOneProviderAndReadsTheAccountOnlyWhenValuing()
    {
        var reads = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddCazanaGuideValuation(_ =>
        {
            reads++;
            return CazanaCompanionAccount.Create(static _ => null);
        });
        using var provider = services.BuildServiceProvider();

        var source = Assert.Single(provider.GetServices<IGuideValuationProvider>());

        Assert.IsType<CazanaGuideValuationProvider>(source);
        Assert.Equal(ValuationSource.Cazana, source.Source);
        Assert.Equal(0, reads);
    }

    internal static string LookupJson(string vin = Vin) => $$$"""
        {
          "response": {
            "valuation": {
              "cid": 9946663,
              "ocn": null,
              "date": "2031-04-15",
              "retail": 13657,
              "mileage": { "value": 33000, "isEstimated": false },
              "remarket": 11062,
              "namaGrade": 1,
              "valuation": { "pooled": 13657, "private": 11900, "franchise": 14026 },
              "description": "2024 Toyota Corolla Hybrid HEV Icon",
              "searchMethod": { "method": "vrm", "identifier": "AB12CDE" },
              "vin": "{{{vin}}}"
            },
            "specs": {
              "year": 2024,
              "firstRegistration": "2024-01-04T00:00:00.000000Z",
              "make": "Toyota",
              "model": "Corolla Hybrid HEV",
              "type": "Car",
              "trim": "Icon",
              "bodyStyle": "Estate",
              "transmission": "CVT",
              "gears": 1,
              "doors": 5,
              "seats": 5,
              "fuelType": "Petrol/Hybrid Electric",
              "engineNumber": "2ZR6C86014",
              "engineSizeCc": 1798,
              "numberValves": 16,
              "numberCylinders": 4,
              "powerOutputBhp": 97,
              "powerOutputKw": 72,
              "onTheRoadPrice": 27950,
              "cid": 291493,
              "currentColour": "silver"
            },
            "listing": {},
            "options": null,
            "insights": null
          }
        }
        """;

    private static async Task AssertUnavailableAsync(Harness harness, string logged)
    {
        var refused = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.NotNull(refused.InnerException);
        Assert.Contains(harness.Logger.Messages, message => message.Contains(logged, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message =>
            message.Contains(Password, StringComparison.Ordinal) || message.Contains(Jwt, StringComparison.Ordinal));
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

    internal static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        IReadOnlyDictionary<string, string[]> Headers,
        string? Body);

    private sealed class Harness(
        CazanaGuideValuationProvider provider,
        CapturingLogger logger,
        List<RecordedRequest> requests)
    {
        public CazanaGuideValuationProvider Provider { get; } = provider;

        public CapturingLogger Logger { get; } = logger;

        public List<RecordedRequest> Requests { get; } = requests;

        public static Harness Create(
            Func<string, HttpResponseMessage?>? overridePath = null,
            Func<CazanaCompanionAccount>? account = null)
        {
            var requests = new List<RecordedRequest>();
            var logger = new CapturingLogger();
            return new(
                new CazanaGuideValuationProvider(
                    new ScriptedClientFactory(new Answering(async request =>
                    {
                        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                        requests.Add(new(
                            request.Method,
                            request.RequestUri!,
                            request.Headers.Authorization?.ToString(),
                            request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                            body));
                        var path = request.RequestUri!.AbsolutePath;
                        if (overridePath?.Invoke(path) is { } overridden)
                        {
                            return overridden;
                        }

                        return path switch
                        {
                            "/guest/onboard/branch" => Json(HttpStatusCode.OK, Branches),
                            "/guest/onboard/confirm" => Json(HttpStatusCode.OK, Confirmed),
                            "/app/companion" => Json(HttpStatusCode.OK, Created),
                            "/app/companion/" + Uuid => Json(HttpStatusCode.OK, LookupJson()),
                            _ => Json(HttpStatusCode.NotFound, "{}"),
                        };
                    })),
                    account ?? (() => CazanaCompanionAccount.Create(
                        key => key == "Cazana:CompanionAccount:Email" ? Email : Password)),
                    new FixedClock(Now),
                    logger),
                logger,
                requests);
        }
    }

    internal sealed class Answering(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await answer(request);
        }
    }

    internal sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
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
