using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Intake;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Glass's as a guide valuation source (ADR-0060) against the scripted Market
/// Value Assessor: the figures, the month and mileage it is asked for, the
/// stock save and the report the portal itself makes, and the one answer —
/// unavailable — every failure gives the card, but for a vehicle too old to value.
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

    /// <summary>
    /// Glass's looks the VIN up from the plate when the vehicle is stocked;
    /// the stocked vehicle's details page names it (operator, 7 October 2026).
    /// </summary>
    [Fact]
    public async Task TheQuoteCarriesTheVinTheStockedVehicleNames()
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request(), default);

        Assert.Equal(Vin, quote.Vin);
        var mva = harness.Mva;
        Assert.True(mva.IndexOf("/index/create-new-vehicle/") < mva.IndexOf("/index/vehicle-details-value/"));
        Assert.Contains(mva.Requests, request => request.Path == "/index/vehicle-details-value/id/" + VehicleId);
    }

    [Fact]
    public async Task AStockedVehicleWithoutAVinAnswersNone()
    {
        var harness = Harness.Create();
        harness.Mva.Set("GET /index/vehicle-details-value/", new(HttpStatusCode.OK, VehicleDetail(vin: string.Empty)));

        var quote = await harness.Provider.GetAsync(Request(), default);

        Assert.Null(quote.Vin);
        Assert.NotNull(quote.Report);
    }

    [Fact]
    public async Task AnUnreadVinStillAnswersTheFiguresAndTheReport()
    {
        var harness = Harness.Create();
        harness.Mva.Set("GET /index/vehicle-details-value/", new(HttpStatusCode.InternalServerError, string.Empty));

        var quote = await harness.Provider.GetAsync(Request(), default);

        Assert.Equal(17717m, quote.RetailValue);
        Assert.Equal(15600m, quote.TradeValue);
        Assert.Equal("glass-stock:" + VehicleId, quote.Report!.Identity);
        Assert.Null(quote.Vin);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("its VIN was not read", StringComparison.Ordinal)
            && message.Contains("glass.details.request", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheReportIsTheValuesOnlyPrintOfTheStockedVehicleOverTheSameSession()
    {
        var harness = Harness.Create();
        var quote = await harness.Provider.GetAsync(Request(), default);

        var pdf = await quote.Report!.FetchPdfAsync(default);

        Assert.Equal(ReportPdf, pdf);
        var print = Assert.Single(harness.Mva.Requests, request => request.Path.StartsWith("/pdf-print/", StringComparison.Ordinal));
        Assert.Equal("/pdf-print/storess/template/0/printaction/vehicle-valuation/vehicles/" + VehicleId, print.Path);
        var download = Assert.Single(harness.Mva.Requests, request => request.Path == ReportPath);
        Assert.Contains("NDP=session-cookie", download.Cookie, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shared account can hand back another vehicle's print (issue 1032):
    /// the filing reads the report's text and keeps only the Case's own.
    /// </summary>
    [Fact]
    public async Task TheReportIsFiledWhenItNamesTheCaseRegistration()
    {
        var harness = Harness.Create();
        var quote = await harness.Provider.GetAsync(Request(), default);
        var custody = new RecordingCustody();

        await new FileGuideValuationReport(custody, new PdfPigPageTextExtractor()).ExecuteAsync(
            new(Request().Actor, Guid.NewGuid(), ValuationSource.Glasses, Registration, April, quote.Report!),
            default);

        Assert.Single(custody.Requests);
    }

    [Fact]
    public async Task AReportThatNamesAnotherRegistrationIsNeverFiled()
    {
        var harness = Harness.Create();
        harness.Mva.Set("GET /ndp_download/18390/", new(
            HttpStatusCode.OK, string.Empty, ContentType: "application/pdf", Bytes: ValuationReport("MP23KTV")));
        var quote = await harness.Provider.GetAsync(Request(), default);
        var custody = new RecordingCustody();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FileGuideValuationReport(custody, new PdfPigPageTextExtractor()).ExecuteAsync(
                new(Request().Actor, Guid.NewGuid(), ValuationSource.Glasses, Registration, April, quote.Report!),
                default));

        Assert.Contains("registration=different", refused.Message, StringComparison.Ordinal);
        Assert.Empty(custody.Requests);
    }

    [Theory]
    [InlineData("https://elsewhere.test/ndp_download/18390/report.pdf", "glass.report.off_origin")]
    [InlineData(null, "glass.report.none")]
    public async Task AReportLinkThatIsNotOneOnGlassOwnOriginIsRefused(string? link, string code)
    {
        var harness = Harness.Create();
        // The scripted print route is replaced at its full length: the longest
        // matching prefix answers.
        harness.Mva.Set("GET /pdf-print/storess/template/0/printaction/vehicle-valuation/vehicles/", new(
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
        // A valuation never stands in a placeholder for a plate it cannot find.
        Assert.Equal(0, harness.Mva.Count("POST /index/unqualified-vehicle-insert"));
        Assert.Equal(0, harness.Mva.Count("GET /three-phase-vehicle/refresh-vrm-count"));
        Assert.Contains(harness.Logger.Messages, message => message.Contains(code, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains(Registration, StringComparison.Ordinal));
    }

    /// <summary>
    /// A rejected credential is the portal's own redirect to its "Login failed"
    /// page (issue 1030). The card says the same approved sentence, the log
    /// names the code, and nothing signs in again or asks for anything else.
    /// </summary>
    [Fact]
    public async Task ARejectedPasswordIsUnavailableAndLoggedByItsOwnCodeAfterOneSignIn()
    {
        var harness = Harness.Create();
        harness.Mva.Set("POST /login/index", new(
            HttpStatusCode.Found, string.Empty, Location: "https://mva.test/login/login-failed"));

        var unavailable = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(), default));

        var stage = Assert.IsType<GlassMvaStageException>(unavailable.InnerException);
        Assert.Equal(GlassFailure.LoginRejected, stage.FailureCode);
        Assert.Equal(1, harness.Mva.Count("POST /login/index"));
        Assert.Equal(0, harness.Mva.Count("GET /index"));
        Assert.Contains(harness.Logger.Messages, message => message.Contains("glass.login.rejected", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains("valuation-test", StringComparison.Ordinal));
    }

    /// <summary>
    /// A vehicle older than Glass's values is not a failure to report: the
    /// card says so (operator, 2 October 2026). The log names it by its code
    /// and never holds the provider's message.
    /// </summary>
    [Fact]
    public async Task AVehicleTooOldToValueIsItsOwnAnswerAndSavesNothingToTheStockList()
    {
        var harness = Harness.Create();
        harness.Mva.Set("GET /three-phase-vehicle/get-values", new(HttpStatusCode.OK, ValuationVehicleAge));

        var refused = await Assert.ThrowsAsync<GuideValuationNotValuedException>(() =>
            harness.Provider.GetAsync(Request(), default));

        Assert.Equal(ValuationSource.Glasses, refused.ValuationSource);
        Assert.Equal(GuideValuationNotValuedReason.VehicleAge, refused.Reason);
        Assert.Equal(0, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("glass.valuation.vehicle_age success=False errormsg=present", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains("rolling", StringComparison.Ordinal));
    }

    /// <summary>
    /// Issue 996: the portal answers a plate its VRM supplier does not know
    /// with a type number that is the JSON <c>false</c>. That is "not found"
    /// after the one search, not three empty candidate reads, and the card
    /// says the same approved sentence as for any other failure.
    /// </summary>
    [Fact]
    public async Task APlateTheProviderDoesNotKnowIsUnavailableAfterOneRequest()
    {
        var harness = Harness.Create();
        const string search = "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000";
        harness.Mva.Set(search, new(HttpStatusCode.OK, UnknownPlate));

        var unavailable = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(), default));

        var stage = Assert.IsType<GlassMvaStageException>(unavailable.InnerException);
        Assert.Equal("glass.lookup.notfound", stage.FailureCode);
        Assert.Equal(1, harness.Mva.Count(search));
        Assert.Equal(0, harness.Mva.Count("GET /three-phase-vehicle/get-vehicles"));
        Assert.Equal(0, harness.Mva.Count("POST /index/unqualified-vehicle-insert"));
        Assert.Equal(0, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("glass.lookup.notfound", StringComparison.Ordinal)
            && message.Contains("natcode=absent", StringComparison.Ordinal));
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

    /// <summary>
    /// Issue 1021: the sign-in page's first read after a restart ran out of
    /// the adapter's match budget and was logged as a transport failure. A
    /// read budget that runs out is the stage's own refusal, named by the
    /// stage, and says whether the provider may already have acted.
    /// </summary>
    [Fact]
    public void AReadBudgetThatRunsOutIsTheStagesOwnRefusalNeverTransport()
    {
        var timedOut = new RegexMatchTimeoutException("input", "pattern", TimeSpan.FromMilliseconds(1));

        var refused = Assert.Throws<GlassMvaStageException>(() =>
            GlassMvaClient.Matched<Match>(() => throw timedOut, "glass.login.csrf"));
        Assert.Equal("glass.login.csrf", refused.FailureCode);
        Assert.Equal("regex=timeout", refused.Detail);
        Assert.False(refused.OutcomeUnknown);

        var uncertain = Assert.Throws<GlassMvaStageException>(() =>
            GlassMvaClient.Matched<Match>(() => throw timedOut, "glass.start.caller", outcomeUnknown: true));
        Assert.True(uncertain.OutcomeUnknown);

        Assert.Equal(1, GlassMvaClient.Matched(() => 1, "glass.login.csrf"));
    }

    /// <summary>
    /// A read budget that somehow escapes its stage is a page the adapter
    /// could not read, never the network.
    /// </summary>
    [Fact]
    public async Task AReadBudgetThatEscapesItsStageIsUnreadableNotTransport()
    {
        var harness = Harness.Create(transport: _ => new Failing(
            new RegexMatchTimeoutException("input", "pattern", TimeSpan.FromMilliseconds(100))));

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request(), default));

        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("glass.valuation.unreadable RegexMatchTimeoutException", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message =>
            message.Contains("glass.transport.failed", StringComparison.Ordinal));
    }

    /// <summary>Every pattern the adapter reads a page with has a budget a stalled host can meet.</summary>
    [Fact]
    public void EveryPatternHasAtLeastOneSecondToMatch()
    {
        var patterns = typeof(GlassMvaClient)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(Regex) && method.GetParameters().Length == 0)
            .ToArray();

        Assert.True(patterns.Length >= 12, $"Expected the adapter's twelve patterns, found {patterns.Length}.");
        Assert.All(patterns, method =>
        {
            var budget = ((Regex)method.Invoke(null, null)!).MatchTimeout;
            Assert.True(budget >= TimeSpan.FromSeconds(1), $"{method.Name} matches within {budget}.");
        });
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

    private sealed class RecordingCustody : ICaseArtifactCustody
    {
        public List<CaseArtifactCustodyRequest> Requests { get; } = [];

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult<CaseArtifactCustodyResult>(new(
                CaseArtifactCustodyDisposition.Confirmed, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "box-file", "box-version", request.Sha256, request.ContentLength, request.MediaType, null, null));
        }
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
