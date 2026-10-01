using System.Net;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Glass's as a guide valuation source (ADR-0059) against the scripted Market
/// Value Assessor: the figures, the month and mileage it is asked for, the
/// stock save and the report the portal itself makes, and the one answer —
/// unavailable — every failure gives the card.
/// </summary>
public sealed class GlassGuideValuationProviderTests
{
    private static readonly DateOnly April = new(2031, 4, 1);

    [Fact]
    public async Task TheFiguresAreRetailTransactedAndGlassTradeInTheChosenMonth()
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request(), default);

        Assert.Equal(17717m, quote.RetailValue);
        Assert.Equal(15600m, quote.TradeValue);
        Assert.Equal(April, quote.GuideMonth);
        Assert.Equal(MileageMiles, quote.Mileage);
        Assert.Equal("glass-stock:" + VehicleId, quote.Report!.Identity);

        // The portal's own order: sign in, look up, value, refresh, then stock.
        var mva = harness.Mva;
        Assert.Contains("login_name=valuation-test", mva.Requests[1].Body, StringComparison.Ordinal);
        Assert.True(mva.IndexOf("/index/search-vrm/") < mva.IndexOf("/three-phase-vehicle/get-vehicles/"));
        Assert.True(mva.IndexOf("/three-phase-vehicle/get-vehicles/") < mva.IndexOf("/three-phase-vehicle/get-values/"));
        Assert.True(mva.IndexOf("/three-phase-vehicle/get-values/") < mva.IndexOf("/three-phase-vehicle/refresh-vrm-count"));
        Assert.True(mva.IndexOf("/three-phase-vehicle/refresh-vrm-count") < mva.IndexOf("/index/create-new-vehicle/"));
        Assert.Contains(mva.Requests, request => request.Path == "/three-phase-vehicle/get-vehicles/source/vrm/valdate/203104");
        Assert.Contains(mva.Requests, request => request.Path == "/three-phase-vehicle/get-values/vehicle/1/source/vrm/valdate/203104");
        Assert.Contains(mva.Requests, request =>
            request.Path == "/index/create-new-vehicle/value/0/valuate/1/mileage/33000/valdate/203104/condition/false");

        // The report is the stocked vehicle's, and it is read only when filed.
        Assert.Equal(0, mva.Count("GET /pdf-print/"));
    }

    [Fact]
    public async Task TheReportIsTheValuesOnlyPrintOfTheStockedVehicleOverTheSameSession()
    {
        var harness = Harness.Create();
        var quote = await harness.Provider.GetAsync(Request(), default);

        var pdf = await quote.Report!.FetchPdfAsync(default);

        Assert.Equal(ReportPdf, Encoding.UTF8.GetString(pdf));
        var print = Assert.Single(harness.Mva.Requests, request => request.Path.StartsWith("/pdf-print/", StringComparison.Ordinal));
        Assert.Equal("/pdf-print/storess/template/0/printaction/vehicle-valuation/vehicles/" + VehicleId, print.Path);
        var download = Assert.Single(harness.Mva.Requests, request => request.Path == ReportPath);
        Assert.Contains("NDP=session-cookie", download.Cookie, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://elsewhere.test/ndp_download/18390/report.pdf", "glass.report.off_origin")]
    [InlineData(null, "glass.report.none")]
    public async Task AReportLinkThatIsNotOneOnGlassOwnOriginIsRefused(string? link, string code)
    {
        var harness = Harness.Create();
        harness.Mva.Set("GET /pdf-print/", new(
            HttpStatusCode.OK, link is null ? "<h4>No report</h4>" : ReportLink(link)));
        var quote = await harness.Provider.GetAsync(Request(), default);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => quote.Report!.FetchPdfAsync(default));

        Assert.Equal(code, refused.FailureCode);
        Assert.Equal(0, harness.Mva.Count("GET /ndp_download/"));
    }

    [Fact]
    public async Task AKilometreReadingIsAskedInWholeMiles()
    {
        var harness = Harness.Create();

        // 53,108 km is 32,999.78 miles: Glass's is asked for 33,000.
        await harness.Provider.GetAsync(Request() with { Mileage = 53108, MileageUnit = "km" }, default);

        Assert.Contains(harness.Mva.Requests, request =>
            request.Path == "/index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000");
        Assert.Contains(harness.Mva.Requests, request =>
            request.Path.StartsWith("/index/create-new-vehicle/value/0/valuate/1/mileage/33000/", StringComparison.Ordinal));
    }

    [Fact]
    public void ACommentedOutBoxIsNeverAFigure()
    {
        var figures = GlassMvaClient.GuideFigures(Values());
        Assert.Equal(new GlassGuideFigures(17717m, 15600m), figures);

        // Only the Retail Transacted box is left outside a comment.
        var hidden = Values().Replace(
            "<div id=\"three_phase_glass_trade\"", "<!--<div id=\"three_phase_glass_trade\"", StringComparison.Ordinal)
            .Replace("<div id=\"three_phase_transacted\"", "--><div id=\"three_phase_transacted\"", StringComparison.Ordinal);
        var refused = Assert.Throws<GlassMvaStageException>(() => GlassMvaClient.GuideFigures(hidden));
        Assert.Equal("glass.valuation.unreadable", refused.FailureCode);
        Assert.Equal("box=three_phase_glass_trade count=0", refused.Detail);
    }

    /// <summary>
    /// Whatever stopped it, the card says the same approved sentence and
    /// nothing is saved to the stock list; the log names the stage.
    /// </summary>
    [Theory]
    [InlineData("POST /login/index", 200, "<form name=\"Form_Login\"></form>", "glass.login.redirect")]
    [InlineData("GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1", 200,
        "{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":-1}", "glass.lookup.notfound")]
    [InlineData("GET /three-phase-vehicle/get-vehicles", 200,
        "{\"success\":true,\"html\":\"<div class=\\\"three_phase_car_info car1\\\">N\\/C: 999<\\/div>\"}", "glass.candidates.none")]
    [InlineData("GET /three-phase-vehicle/get-values", 200, ValuationNotPossible, "glass.valuation.not_possible")]
    [InlineData("GET /three-phase-vehicle/get-values", 200, "<div id=\"three_phase_glass_trade\" class=\"three_phase_value_box\"><div class=\"three_phase_value_text\">-</div></div>", "glass.valuation.unreadable")]
    [InlineData("GET /three-phase-vehicle/get-values", 500, "", "glass.valuation.request")]
    public async Task EveryProviderFailureIsUnavailableAndSavesNothingToTheStockList(
        string route, int status, string body, string code)
    {
        var harness = Harness.Create();
        harness.Mva.Set(route, new((HttpStatusCode)status, body));

        var unavailable = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(), default));

        Assert.IsType<GlassMvaStageException>(unavailable.InnerException);
        Assert.Equal(0, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(0, harness.Mva.Count("GET /three-phase-vehicle/refresh-vrm-count"));
        Assert.Contains(harness.Logger.Messages, message => message.Contains(code, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains(Registration, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATransportFailureOrTimeoutIsUnavailable()
    {
        foreach (var failure in new Exception[]
        {
            new HttpRequestException("No such host is known."),
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException()),
        })
        {
            var harness = Harness.Create(transport: _ => new Failing(failure));

            var unavailable = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
                harness.Provider.GetAsync(Request(), default));

            // HttpClient restates a handler's timeout as its own; either way
            // the provider's failure is what the card's notice stands for.
            Assert.True(unavailable.InnerException is HttpRequestException or OperationCanceledException);
            Assert.Contains(harness.Logger.Messages, message => message.Contains("glass.transport.failed", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task AnUnresolvedAccountOrUnreadableUnitIsUnavailableBeforeAnyRequest()
    {
        var unresolved = Harness.Create(account: () => GlassValuationAccount.Create(key =>
            key.EndsWith("Password", StringComparison.Ordinal)
                ? "@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/glass-valuation-password)"
                : "valuation-test"));
        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            unresolved.Provider.GetAsync(Request(), default));
        Assert.Empty(unresolved.Mva.Requests);
        Assert.Contains(unresolved.Logger.Messages, message =>
            message.Contains("Glass:ValuationAccount:Password is an unresolved Key Vault reference", StringComparison.Ordinal));

        var unreadable = Harness.Create();
        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            unreadable.Provider.GetAsync(Request() with { MileageUnit = "furlongs" }, default));
        Assert.Empty(unreadable.Mva.Requests);
    }

    [Fact]
    public async Task AFailedStockSaveStillAnswersTheFiguresWithoutAReport()
    {
        var harness = Harness.Create();
        harness.Mva.Set("GET /index/create-new-vehicle", new(HttpStatusCode.InternalServerError, string.Empty));

        var quote = await harness.Provider.GetAsync(Request(), default);

        Assert.Equal(17717m, quote.RetailValue);
        Assert.Equal(15600m, quote.TradeValue);
        Assert.Null(quote.Report);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("not saved to the stock list", StringComparison.Ordinal)
            && message.Contains("glass.vehicle.request", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheCallerGivingUpIsNotUnavailable()
    {
        var harness = Harness.Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Provider.GetAsync(Request(), cancelled.Token));
        Assert.Empty(harness.Logger.Messages);
    }

    [Fact]
    public void TheValuationAccountRefusesBlankControlAndUnresolvedValuesAndNeverPrintsItsPassword()
    {
        static Func<string, string?> Read(string? username, string? password) =>
            key => key == "Glass:ValuationAccount:Username" ? username : password;

        Assert.Throws<InvalidOperationException>(() => GlassValuationAccount.Create(Read(" ", "secret")));
        Assert.Throws<InvalidOperationException>(() => GlassValuationAccount.Create(Read("valuation-test", null)));
        Assert.Throws<InvalidOperationException>(() => GlassValuationAccount.Create(Read("valuation-test", "sec\nret")));
        Assert.Throws<InvalidOperationException>(() => GlassValuationAccount.Create(
            Read("@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/glass-valuation-username)", "secret")));

        var account = GlassValuationAccount.Create(Read(" valuation-test ", " spaced secret "));
        Assert.Equal("valuation-test", account.Username);
        Assert.Equal(" spaced secret ", account.Password);
        Assert.DoesNotContain("secret", account.ToString(), StringComparison.Ordinal);
    }

    private static GuideValuationRequest Request() => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
        Guid.NewGuid(),
        Registration,
        MileageMiles,
        MileageUnit: null,
        April);

    private sealed class Harness
    {
        private Harness(ScriptedGlass mva, GlassGuideValuationProvider provider, CapturingLogger logger)
        {
            Mva = mva;
            Provider = provider;
            Logger = logger;
        }

        public ScriptedGlass Mva { get; }

        public GlassGuideValuationProvider Provider { get; }

        public CapturingLogger Logger { get; }

        public static Harness Create(
            Func<HttpMessageHandler, HttpMessageHandler>? transport = null,
            Func<GlassValuationAccount>? account = null)
        {
            var mva = new ScriptedGlass();
            Script(mva);
            var options = new GlassRepairEstimateOptions(
                MvaBase,
                EstimatorBase,
                CallbackBase,
                ProfileId,
                SessionLifetime: TimeSpan.FromHours(8),
                ExportPollInterval: TimeSpan.FromMilliseconds(5),
                ExportTimeout: TimeSpan.FromMilliseconds(50),
                MaximumExportBytes: 16 * 1024 * 1024);
            var logger = new CapturingLogger();
            return new(
                mva,
                new GlassGuideValuationProvider(
                    new ScriptedClientFactory(transport?.Invoke(mva) ?? mva),
                    () => options,
                    account ?? (() => GlassValuationAccount.Create(key =>
                        key.EndsWith("Username", StringComparison.Ordinal) ? "valuation-test" : "synthetic-password")),
                    TimeProvider.System,
                    logger),
                logger);
        }
    }

    private sealed class Failing(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(failure);
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<GlassGuideValuationProvider>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
