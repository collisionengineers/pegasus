using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047 B04: the Glass's repair estimate as an operator drives it — the
/// Estimate section's Launch and Resume, and the provider's own return at
/// <c>/Integrations/Glass/Callback/{correlation}</c> — through the real routes
/// and the host's own composition.
/// </summary>
/// <remarks>
/// <para>
/// Only the provider is replaced, and only by the one scripted Market Value
/// Assessor both Glass's suites share
/// (<see cref="GlassProviderFixture"/>), attached to the named client the
/// gateway resolves. Everything else is production wiring: the real session
/// store on LocalDB, the real Case edit lease, the real per-user credential
/// store, the host's own document custody, and the canonical import.
/// </para>
/// <para>
/// Nothing here is a real account, secret, provider address or vehicle: the
/// origins are reserved <c>.test</c> names, the credential is an obviously
/// synthetic fixture pair that never appears in an assertion message, and the
/// vehicle is the documented estate registration AB12CDE.
/// </para>
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed class GlassRepairEstimateCallbackWebTests
{
    private const string PegasusOrigin = "https://localhost:7139/";
    private const string CallbackRoute = "/Integrations/Glass/Callback/";
    private const string FixtureAccount = "glass-fixture-account";
    private const string FixtureSecret = "glass-fixture-value-not-a-secret";

    /// <summary>A Glass's account key shaped like the one Stream A's store mints.</summary>
    private const string OtherAccountKey =
        "9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f1e0d9c8b7a6f5e4d3c2b1a0f9e8d";

    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>The export and its embedded calculation sheet, in a stable order.</summary>
    private static readonly string[] BothDocuments = ["application/pdf", "application/xml"];

    // ----------------------------------------------------------------- launch

    /// <summary>
    /// The session exists before the provider is touched, and the operator
    /// leaves for the estimator on the address the launch produced — which
    /// names Pegasus's own callback, not the provider's.
    /// </summary>
    [Fact]
    public async Task ALaunchSendsTheEngineerToTheEstimatorOnPegasusOwnCallback()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();

        using var launched = await workspace.LaunchAsync();

        Assert.Equal(HttpStatusCode.Found, launched.StatusCode);
        var estimator = new Uri(launched.Headers.Location!.ToString(), UriKind.Absolute);
        Assert.Equal(GlassProviderFixture.EstimatorBase.Host, estimator.Host);
        var caller = new Uri(Query(estimator.Query)["caller"], UriKind.Absolute);
        Assert.Equal(new Uri(PegasusOrigin).Host, caller.Host);
        Assert.StartsWith(CallbackRoute, caller.AbsolutePath, StringComparison.Ordinal);

        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Equal(workspace.CaseId, session.CaseId);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, session.PegasusUserId);
        Assert.Null(session.CallbackConsumedAtUtc);
        // The provider session never reaches the browser.
        Assert.DoesNotContain(
            GlassProviderFixture.EreSession, estimator.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// One operator action is one estimate: the second post of the same form
    /// gets the session the first created and starts nothing at the provider.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnLaunchGetsTheSessionTheFirstClickCreated()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var form = await workspace.LaunchFormAsync();

        using var first = await workspace.PostAsync("LaunchGlass", form);
        using var second = await workspace.PostAsync("LaunchGlass", form);

        Assert.Equal(HttpStatusCode.Found, second.StatusCode);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(1, workspace.Mva.Count("POST /ere/start-ere"));
    }

    /// <summary>
    /// A Glass's account holds one live calculation, so a second launch is
    /// refused where every other Estimate refusal is reported and no second
    /// session is recorded.
    /// </summary>
    [Fact]
    public async Task ASecondLaunchWhileOneIsLiveIsRefusedAndRecordsNoSecondSession()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        using (var first = await workspace.LaunchAsync())
        {
            Assert.Equal(HttpStatusCode.Found, first.StatusCode);
        }

        using var second = await workspace.PostAsync("LaunchGlass", await workspace.LaunchFormAsync());

        Assert.Equal(HttpStatusCode.Found, second.StatusCode);
        // Back to the Estimate section, not out to the provider.
        Assert.StartsWith(
            $"/Cases/{workspace.CaseId:D}",
            second.Headers.Location!.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "already holds a live session",
            await workspace.CaseHtmlAsync(),
            StringComparison.Ordinal);
        Assert.Single(await workspace.SessionsAsync());
    }

    // --------------------------------------------------------------- the page

    /// <summary>
    /// The control belongs to the operator's own Glass's account: without one,
    /// or without the Engineer role, the Estimate section offers nothing —
    /// absent, not disabled.
    /// </summary>
    [Theory]
    [InlineData(false, "Engineer")]
    [InlineData(true, "User")]
    public async Task TheGlassControlIsAbsentWithoutAnEnabledAccountAndForANonEngineer(
        bool credentialed, string role)
    {
        await using var workspace = await Workspace.CreateAsync(credentialed, role);

        var html = await workspace.CaseHtmlAsync();

        Assert.DoesNotContain("handler=LaunchGlass", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ResumeGlass", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- the return

    /// <summary>
    /// The whole operator journey: Save &amp; Exit lands the calculation as a
    /// Draft on the Case, keeps both of the provider's documents, completes the
    /// session, and puts the operator back on the Estimate section.
    /// </summary>
    [Fact]
    public async Task TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();

        using var returned = await workspace.ReturnAsync(correlation);

        Assert.Equal(HttpStatusCode.Found, returned.StatusCode);
        Assert.Equal(
            $"/Cases/{workspace.CaseId:D}?section=estimate",
            returned.Headers.Location!.ToString());
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Null(session.FailureCode);
        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.NotNull(session.CallbackConsumedAtUtc);

        var estimate = Assert.Single(await workspace.EstimatesAsync());
        Assert.Equal(RepairSpecificationSourceRoute.Glasses, estimate.Source.Route);
        Assert.Equal(RepairSpecificationState.Draft, estimate.State);
        Assert.NotEmpty(estimate.Lines);
        Assert.Equal(BothDocuments, await workspace.RetainedMediaTypesAsync());
    }

    /// <summary>
    /// A repeated return reads back what the first one produced: the same
    /// session, and no second Draft.
    /// </summary>
    [Fact]
    public async Task TheSameReturnDeliveredTwiceRecordsNothingASecondTime()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        using (var first = await workspace.ReturnAsync(correlation))
        {
            Assert.Equal(HttpStatusCode.Found, first.StatusCode);
        }
        var consumedAtUtc = Assert.Single(await workspace.SessionsAsync()).CallbackConsumedAtUtc;

        using var again = await workspace.ReturnAsync(correlation);

        Assert.Equal(HttpStatusCode.Found, again.StatusCode);
        Assert.Equal(
            $"/Cases/{workspace.CaseId:D}?section=estimate",
            again.Headers.Location!.ToString());
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(consumedAtUtc, session.CallbackConsumedAtUtc);
        Assert.Single(await workspace.EstimatesAsync());
        Assert.Equal(2, (await workspace.RetainedMediaTypesAsync()).Count);
    }

    /// <summary>
    /// A signed-in stranger cannot spend a session they did not launch, and the
    /// refusal leaves the return address usable by the Engineer who owns it.
    /// </summary>
    [Fact]
    public async Task AnotherEngineersReturnIsRefusedWithoutSpendingTheCallback()
    {
        await using var workspace = await Workspace.CreateAsync();
        var correlation = await workspace.SeedAnotherEngineersSessionAsync();

        using var refused = await workspace.ReturnAsync(correlation);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Null(session.CallbackConsumedAtUtc);
        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
    }

    /// <summary>
    /// Only the gateway's own refusal of a return is reported as one. Any
    /// other failure inside it is a fault: it surfaces as a server error, and
    /// the session it was about is exactly as it was, with its return still
    /// the owner's to deliver.
    /// </summary>
    [Fact]
    public async Task AnUnrelatedGatewayFailureOnTheReturnSurfacesAndSpendsNothing()
    {
        var fault = new GatewayFault();
        await using var workspace = await Workspace.CreateAsync(fault: fault);
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        fault.OnComplete = true;

        using var failed = await workspace.ReturnAsync(correlation);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Null(session.CallbackConsumedAtUtc);
        Assert.Equal(0, workspace.Mva.Count("GET /ere/ere-callback/"));

        fault.OnComplete = false;
        using var delivered = await workspace.ReturnAsync(correlation);
        Assert.Equal(HttpStatusCode.Found, delivered.StatusCode);
        Assert.Equal(
            $"/Cases/{workspace.CaseId:D}?section=estimate",
            delivered.Headers.Location!.ToString());
        Assert.NotNull(Assert.Single(await workspace.SessionsAsync()).CallbackConsumedAtUtc);
    }

    /// <summary>A return that names no session Pegasus is waiting for.</summary>
    [Fact]
    public async Task AReturnThatNamesNoSessionIsNotFound()
    {
        await using var workspace = await Workspace.CreateAsync();

        using var missing = await workspace.ReturnAsync(NewCorrelation());

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    /// <summary>
    /// The provider composes the request, so it carries no staff antiforgery
    /// token. A posted return is read exactly like a navigated one: refused on
    /// the token in its own path, never on a form token it cannot have.
    /// </summary>
    [Fact]
    public async Task APostedReturnIsReadTheSameWayAndNeverRefusedForAMissingFormToken()
    {
        await using var workspace = await Workspace.CreateAsync();

        using var posted = await workspace.Client.PostAsync(
            CallbackRoute + NewCorrelation() + GlassProviderFixture.SavedQuery,
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
    }

    /// <summary>
    /// The provider composes the request, so it carries no staff session: the
    /// operator signs in and comes back to the same address, whole.
    /// </summary>
    [Fact]
    public async Task AReturnThatArrivesSignedOutIsSentToSignInWithItsOwnAddress()
    {
        await using var workspace = await Workspace.CreateAsync();
        var correlation = NewCorrelation();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, CallbackRoute + correlation + GlassProviderFixture.SavedQuery);
        request.Headers.Add("X-Test-Anonymous", "true");

        using var challenged = await workspace.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        var location = challenged.Headers.Location!.ToString();
        Assert.StartsWith("/Account/SignIn?ReturnUrl=", location, StringComparison.Ordinal);
        var returnUrl = Uri.UnescapeDataString(location["/Account/SignIn?ReturnUrl=".Length..]);
        Assert.Equal(CallbackRoute + correlation + GlassProviderFixture.SavedQuery, returnUrl);
    }

    /// <summary>
    /// The return route is anonymous-reachable by construction, so it is rate
    /// limited per client. The policy is the host's; this proves it is attached
    /// to this route.
    /// </summary>
    [Fact]
    public async Task TheReturnRouteIsRateLimitedPerClient()
    {
        await using var workspace = await Workspace.CreateAsync();
        const int permitted = 30;
        for (var attempt = 0; attempt < permitted; attempt++)
        {
            using var allowed = await workspace.ReturnAsync(NewCorrelation());
            Assert.Equal(HttpStatusCode.NotFound, allowed.StatusCode);
        }

        using var limited = await workspace.ReturnAsync(NewCorrelation());

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    // --------------------------------------------------------------- the wait

    /// <summary>
    /// The Case moved on while the operator was inside Glass's: everything the
    /// provider produced is kept, nothing is imported, and the estimate lands
    /// when the Engineer takes the Case back.
    /// </summary>
    [Fact]
    public async Task AReturnThatLostTheCasesEditAuthorityWaitsUntilTheResumeImportsIt()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        await workspace.FinishEditingAsync();

        using (var returned = await workspace.ReturnAsync(correlation))
        {
            Assert.Equal(HttpStatusCode.Found, returned.StatusCode);
        }

        var waiting = Assert.Single(await workspace.SessionsAsync());
        Assert.Null(waiting.FailureCode);
        Assert.Equal(GlassRepairEstimateSessionState.AwaitingImport, waiting.State);
        Assert.Empty(await workspace.EstimatesAsync());
        // The provider's documents were kept, so the resume offers them again
        // rather than asking Glass's for a second copy.
        Assert.Equal(2, (await workspace.RetainedMediaTypesAsync()).Count);

        await workspace.ClaimLeaseAsync();
        using var resumed = await workspace.PostAsync("ResumeGlass", await workspace.ResumeFormAsync());

        Assert.Equal(HttpStatusCode.Found, resumed.StatusCode);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        var estimate = Assert.Single(await workspace.EstimatesAsync());
        Assert.Equal(RepairSpecificationSourceRoute.Glasses, estimate.Source.Route);
        Assert.Equal(2, (await workspace.RetainedMediaTypesAsync()).Count);
    }

    // -------------------------------------------------------------- the shape

    private static string NewCorrelation() =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static Dictionary<string, string> Query(string query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            parsed[Uri.UnescapeDataString(part[..separator])] =
                Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return parsed;
    }

    /// <summary>
    /// One rendered form's own hidden fields, including the antiforgery token
    /// the tag helper writes. Reading the form the operator would submit is
    /// what makes these tests drive the page rather than the handler.
    /// </summary>
    private static Dictionary<string, string> FormFor(string html, string handler)
    {
        var form = Regex.Match(
            html,
            "<form[^>]*action=\"[^\"]*handler=" + Regex.Escape(handler) + "[^\"&]*\"[^>]*>(?<body>.*?)</form>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(form.Success, $"The page must render the {handler} form.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in Regex.Matches(
            form.Groups["body"].Value, "<input[^>]*>", RegexOptions.IgnoreCase).Cast<Match>())
        {
            var name = Regex.Match(input.Value, "name=\"(?<name>[^\"]*)\"", RegexOptions.IgnoreCase);
            if (!name.Success)
            {
                continue;
            }

            var value = Regex.Match(input.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
            fields[name.Groups["name"].Value] = WebUtility.HtmlDecode(
                value.Success ? value.Groups["value"].Value : string.Empty);
        }

        return fields;
    }

    private sealed class GatewayFault
    {
        public bool OnComplete { get; set; }
    }

    /// <summary>The real gateway with one fault a test can switch on: an unrelated failure inside a return.</summary>
    private sealed class FaultingGateway(IGlassRepairEstimateGateway inner, GatewayFault fault) : IGlassRepairEstimateGateway
    {
        public Task<GlassRepairEstimateSession> LaunchAsync(
            GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken) =>
            inner.LaunchAsync(request, cancellationToken);

        public Task<GlassRepairEstimateSession> ResumeAsync(
            GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken) =>
            inner.ResumeAsync(request, cancellationToken);

        public Task<GlassRepairEstimateSession> CompleteAsync(
            GlassRepairEstimateCallback callback, CancellationToken cancellationToken) =>
            fault.OnComplete
                ? throw new InvalidOperationException("An unrelated failure inside the gateway.")
                : inner.CompleteAsync(callback, cancellationToken);

        public Task<Uri?> GetEstimatorUrlAsync(
            ActionActor actor, Guid sessionId, CancellationToken cancellationToken) =>
            inner.GetEstimatorUrlAsync(actor, sessionId, cancellationToken);
    }

    /// <summary>
    /// One Case, one Engineer with a Glass's account, and the scripted provider
    /// on the named client the gateway resolves.
    /// </summary>
    private sealed class Workspace : IAsyncDisposable
    {
        private readonly IntakeWebApplicationFactory baseFactory;
        private readonly WebApplicationFactory<Program> factory;

        private Workspace(
            IntakeWebApplicationFactory baseFactory,
            WebApplicationFactory<Program> factory,
            HttpClient client,
            ScriptedGlass mva,
            Guid caseId)
        {
            this.baseFactory = baseFactory;
            this.factory = factory;
            Client = client;
            Mva = mva;
            CaseId = caseId;
        }

        public HttpClient Client { get; }

        public ScriptedGlass Mva { get; }

        public Guid CaseId { get; }

        public static async Task<Workspace> CreateAsync(
            bool credentialed = true, string role = StaffRoleNames.Engineer, GatewayFault? fault = null)
        {
            var mva = new ScriptedGlass();
            GlassProviderFixture.Script(mva);
            var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
            var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Glass:MarketValueAssessorBaseUri"] = GlassProviderFixture.MvaBase.AbsoluteUri,
                        ["Glass:EstimatorBaseUri"] = GlassProviderFixture.EstimatorBase.AbsoluteUri,
                        ["Glass:CallbackBaseUri"] = PegasusOrigin,
                        ["Glass:RepairProfileId"] = GlassProviderFixture.ProfileId,
                        ["Glass:ExportPollSeconds"] = "1",
                        ["Glass:ExportTimeoutSeconds"] = "5",
                    }));
                builder.ConfigureTestServices(services =>
                {
                    services.Configure<HttpClientFactoryOptions>(
                        GlassRepairEstimateOptions.HttpClientName,
                        options => options.HttpMessageHandlerBuilderActions.Add(
                            handlerBuilder => handlerBuilder.PrimaryHandler = mva));
                    if (fault is not null)
                    {
                        // The host's own gateway, behind a switch the test flips:
                        // everything else it does is real.
                        services.AddScoped<GlassRepairEstimateGateway>();
                        services.AddScoped<IGlassRepairEstimateGateway>(provider =>
                            new FaultingGateway(provider.GetRequiredService<GlassRepairEstimateGateway>(), fault));
                    }
                });
            });

            var caseId = await SeedCaseAsync(factory.Services);
            if (credentialed)
            {
                await SeedCredentialAsync(factory.Services);
            }

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri(PegasusOrigin),
            });
            client.DefaultRequestHeaders.Add("X-Test-Roles", role);
            return new(baseFactory, factory, client, mva, caseId);
        }

        public async Task<string> CaseHtmlAsync()
        {
            using var response = await Client.GetAsync($"/Cases/{CaseId:D}?section=estimate");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task ClaimLeaseAsync()
        {
            using var claimed = await PostAsync("ClaimLease", FormFor(await CaseHtmlAsync(), "ClaimLease"));
            Assert.Equal(HttpStatusCode.Found, claimed.StatusCode);
            Assert.Contains(
                "name=\"editLeaseToken\"", await CaseHtmlAsync(), StringComparison.Ordinal);
        }

        public async Task FinishEditingAsync()
        {
            using var released = await PostAsync("ReleaseLease", FormFor(await CaseHtmlAsync(), "ReleaseLease"));
            Assert.Equal(HttpStatusCode.Found, released.StatusCode);
        }

        public async Task<Dictionary<string, string>> LaunchFormAsync() =>
            FormFor(await CaseHtmlAsync(), "LaunchGlass");

        public async Task<Dictionary<string, string>> ResumeFormAsync() =>
            FormFor(await CaseHtmlAsync(), "ResumeGlass");

        public async Task<HttpResponseMessage> LaunchAsync() =>
            await PostAsync("LaunchGlass", await LaunchFormAsync());

        /// <summary>
        /// The one-use token the provider will return with, read where the
        /// operator's browser reads it: out of the address they were sent to.
        /// </summary>
        public async Task<string> LaunchAndReadCorrelationAsync()
        {
            using var launched = await LaunchAsync();
            Assert.Equal(HttpStatusCode.Found, launched.StatusCode);
            var estimator = new Uri(launched.Headers.Location!.ToString(), UriKind.Absolute);
            return new Uri(Query(estimator.Query)["caller"], UriKind.Absolute).Segments[^1];
        }

        public Task<HttpResponseMessage> ReturnAsync(string correlation) =>
            Client.GetAsync(CallbackRoute + correlation + GlassProviderFixture.SavedQuery);

        public Task<HttpResponseMessage> PostAsync(string handler, Dictionary<string, string> fields) =>
            Client.PostAsync(
                $"/Cases/{CaseId:D}?section=estimate&handler={handler}",
                new FormUrlEncodedContent(fields));

        public async Task<IReadOnlyList<GlassRepairEstimateSession>> SessionsAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var reader = scope.ServiceProvider.GetRequiredService<IGlassRepairEstimateSessionReader>();
            var ids = await context.Set<GlassRepairEstimateSessionEntity>()
                .AsNoTracking()
                .OrderBy(entity => entity.CreatedAtUtc)
                .Select(entity => new { entity.CaseId, entity.UserId })
                .ToArrayAsync();
            var sessions = new List<GlassRepairEstimateSession>();
            foreach (var id in ids)
            {
                sessions.Add(await reader.GetForCaseAsync(id.CaseId, id.UserId, CancellationToken.None)
                    ?? throw new InvalidOperationException("The recorded session could not be read back."));
            }

            return sessions;
        }

        public async Task<IReadOnlyList<RepairSpecificationVersion>> EstimatesAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider
                .GetRequiredService<IListCaseEstimates>()
                .ExecuteAsync(CaseId, CancellationToken.None);
        }

        /// <summary>Every confirmed document the Case now holds, in a stable order.</summary>
        public async Task<IReadOnlyList<string>> RetainedMediaTypesAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var caseId = CaseId;
            return await context.Set<DocumentOccurrenceEntity>()
                .AsNoTracking()
                .Where(occurrence => occurrence.CaseId == caseId)
                .Join(
                    context.Set<DocumentVersionEntity>().AsNoTracking(),
                    occurrence => occurrence.VersionId,
                    version => version.Id,
                    (_, version) => version)
                .Where(version => version.CustodyStatus == DocumentCustodyStatus.Confirmed)
                .Select(version => version.MediaType)
                .OrderBy(mediaType => mediaType)
                .ToArrayAsync();
        }

        /// <summary>
        /// A live session for this Case owned by a different Engineer, written
        /// through the real store so the callback resolves it exactly as it
        /// resolves one this browser launched.
        /// </summary>
        public async Task<string> SeedAnotherEngineersSessionAsync()
        {
            var correlation = NewCorrelation();
            await using var scope = factory.Services.CreateAsyncScope();
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var otherUserId = Guid.NewGuid();
            context.Users.Add(new PegasusIdentityUser
            {
                Id = otherUserId,
                UserName = "b.engineer",
                NormalizedUserName = "B.ENGINEER",
                IsEnabled = true,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            });
            await context.SaveChangesAsync();

            await scope.ServiceProvider.GetRequiredService<IGlassRepairEstimateSessionStore>().CreateAsync(
                new GlassRepairEstimateSessionMaterial(
                    new GlassRepairEstimateSession(
                        Guid.NewGuid(),
                        CaseId,
                        otherUserId,
                        CredentialGeneration: 1,
                        OtherAccountKey,
                        GlassRepairEstimateSessionState.Active,
                        Version: 0,
                        OperationKey: Guid.NewGuid().ToString("N"),
                        FixedUtcNow,
                        FixedUtcNow.AddHours(8),
                        ProviderVehicleId: null,
                        ProviderEstimateId: null,
                        FailureCode: null),
                    // Opaque to every layer that stores it; nothing in this test
                    // asks anything of its contents.
                    protectedProviderState: "protected:fixture:not-a-secret",
                    GlassRepairEstimateGateway.CallbackDigestOf(correlation)),
                CancellationToken.None);
            return correlation;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await factory.DisposeAsync();
            baseFactory.Dispose();
        }

        private static async Task SeedCredentialAsync(IServiceProvider services)
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPerUserExternalCredentialAdministration>()
                .ReplaceAsync(
                    ActionActor.Staff(
                        DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                    DevelopmentOfflineIdentity.AdministratorId,
                    ExternalCredentialProvider.GlassRepairEstimate,
                    expectedVersion: 0,
                    FixtureAccount,
                    FixtureSecret,
                    enabled: true,
                    CancellationToken.None);
        }

        /// <summary>
        /// A Case an Engineer may open and edit: past its first hand-off, not
        /// yet Complete, with the vehicle facts a Glass's launch is made of and
        /// a custody root so a retained document confirms rather than waits.
        /// </summary>
        private static async Task<Guid> SeedCaseAsync(IServiceProvider services)
        {
            await using var scope = services.CreateAsyncScope();
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            const string reference = "GLAS31001";
            var actor = DevelopmentOfflineIdentity.AdministratorId.ToString("D");

            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = "Glass's web test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = reference,
                    IsActive = true,
                    Version = 0,
                },
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "glass-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"glass:{receiptId:N}",
                    ReceivedAtUtc = FixedUtcNow,
                    ProcessedAtUtc = FixedUtcNow,
                    SourceReaderKey = "glass-web-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Glass's web test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]",
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principalId,
                    SequenceLineageId = lineageId,
                    Year = 2031,
                    Sequence = 1,
                    Reference = reference,
                    Type = "Inspection",
                    InitialState = "not_ready",
                    CustodyState = "confirmed",
                    CustodyRootRemoteId = reference,
                    OriginIntakeReceiptId = receiptId,
                    InstructionComplete = true,
                    ImagesComplete = true,
                    InstructionConfirmedByStaff = true,
                    ImagesConfirmedByStaff = true,
                    CreatedAtUtc = FixedUtcNow,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid(),
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "ReportPreparation",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid(),
                },
                new CaseDataSnapshotEntity
                {
                    CaseId = caseId,
                    OriginIntakeReceiptId = receiptId,
                    OriginSourceChannel = "manual_upload",
                    OriginExternalReceiptToken = $"glass:{receiptId:N}",
                    OriginSourceHash = new string('0', 64),
                    OriginReceivedAtUtc = FixedUtcNow,
                    SourceReaderKey = "glass-web-test",
                    SourceReaderVersion = "1",
                    ExtractionPolicyKey = "glass-web-test",
                    ExtractionPolicyVersion = 1,
                    CompletenessPolicyKey = "glass-web-test",
                    CompletenessPolicyVersion = 1,
                    CompletenessPolicySatisfied = true,
                    AcceptedAtUtc = FixedUtcNow,
                },
                // The Assessment gate opens on the first hand-off, which this
                // Case is past; no export is performed by these tests.
                new EvaFirstHandoffProxyEntity
                {
                    CaseId = caseId,
                    AdapterKey = "glass-web-test",
                    AdapterVersion = "1",
                    RecordedAtUtc = FixedUtcNow,
                    LatestExportedWorkflowVersion = 1,
                    ActorSubjectId = actor,
                    ClaimsExternalDelivery = false,
                    ClaimsEngineerAssignment = false,
                });
            await context.SaveChangesAsync();

            foreach (var (name, type, value) in new[]
            {
                (CaseDataFieldNames.VehicleRegistration, CaseDataCodes.Text, GlassProviderFixture.Registration),
                (CaseDataFieldNames.VehicleMileage, CaseDataCodes.Integer,
                    GlassProviderFixture.MileageMiles.ToString(CultureInfo.InvariantCulture)),
                (CaseDataFieldNames.VehicleMileageUnit, CaseDataCodes.Text, "miles"),
            })
            {
                context.Add(new CaseDataFieldEntity
                {
                    CaseId = caseId,
                    FieldName = name,
                    ValueKind = CaseDataCodes.Confirmed,
                    ValueType = type,
                    Value = value,
                    SourceKind = CaseDataCodes.StaffCorrection,
                    SourceIdentity = actor,
                    SourceLabel = "CASE-047 Glass's web fixture",
                    PolicyKey = CaseDataPolicy.EditPolicyKey,
                    PolicyVersion = CaseDataPolicy.EditPolicyVersion,
                    ConfirmedByActor = actor,
                    ConfirmedAtUtc = FixedUtcNow,
                });
            }

            await context.SaveChangesAsync();
            return caseId;
        }
    }
}
