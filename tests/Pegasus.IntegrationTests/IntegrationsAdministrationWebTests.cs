using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Administration;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Integrations administration page (operator, 26 November 2026; R1289):
/// the automatic requests panel with its five switches and one Save, and one
/// panel per integration login whose integration the host composes, showing
/// the account and when it was replaced and never a typed value.
/// </summary>
/// <remarks>
/// The test host composes no valuation or Experian provider, so the tests that
/// need a login's panel compose a provider for every integration that is never
/// asked (<see cref="WithEveryIntegration"/>), as the valuation section's web
/// tests compose theirs.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed partial class IntegrationsAdministrationWebTests
{
    private const string Route = "/Administration/Integrations";
    private const string TypedSecret = "Typed-secret-never-shown-41";
    private const string TypedName = "typed-account-name";

    private static readonly ActionActor Administrator = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    private static readonly ValuationSource[] GuideSources =
        [ValuationSource.Glasses, ValuationSource.Brego, ValuationSource.Cazana, ValuationSource.Cap];

    [Fact]
    public async Task ANonAdministratorCannotOpenOrPostToIntegrations()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using (var response = await client.GetAsync(Route))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var posted = await client.PostAsync(
            $"{Route}?handler=SaveAutomatic",
            new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Forbidden, posted.StatusCode);
    }

    /// <summary>
    /// On a fresh database the Glass's switch is on and the four added on 26
    /// November 2026 are off (R1287). The page is listed after Unroadworthy
    /// reasons in the rail and the hub, and where the host composes no
    /// integration it draws no login panel.
    /// </summary>
    [Fact]
    public async Task OnAFreshDatabaseOnlyTheGlassesSwitchIsOnAndNoUncomposedLoginIsDrawn()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, Route);

        var labels = new[]
        {
            OperatorLabels.Integrations.AutomaticGlassValuation,
            OperatorLabels.Integrations.AutomaticBregoValuation,
            OperatorLabels.Integrations.AutomaticCazanaValuation,
            OperatorLabels.Integrations.AutomaticCapValuation,
            OperatorLabels.Integrations.AutomaticExperianCheck,
        };
        var positions = labels
            .Select(label => html.IndexOf($"<span>{HtmlEncoder.Default.Encode(label)}</span>", StringComparison.Ordinal))
            .ToList();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.Order().ToList(), positions);
        Assert.True(CheckboxIsChecked(html, "GlassesValuationEnabled"));
        Assert.False(CheckboxIsChecked(html, "BregoValuationEnabled"));
        Assert.False(CheckboxIsChecked(html, "CazanaValuationEnabled"));
        Assert.False(CheckboxIsChecked(html, "CapValuationEnabled"));
        Assert.False(CheckboxIsChecked(html, "VehicleHistoryCheckEnabled"));
        Assert.Contains($">{OperatorLabels.Integrations.SaveAutomatic}</span>", html, StringComparison.Ordinal);

        // A capability this host does not carry is absent, never an inert panel.
        Assert.DoesNotContain("handler=SaveLogin", html, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Integrations.Save, html, StringComparison.Ordinal);

        var nav = html[html.IndexOf("class=\"admin-nav panel\"", StringComparison.Ordinal)..];
        var reasonsEntry = nav.IndexOf("href=\"/Administration/UnroadworthyReasons\"", StringComparison.Ordinal);
        var integrationsEntry = nav.IndexOf($"href=\"{Route}\"", StringComparison.Ordinal);
        Assert.True(reasonsEntry >= 0 && integrationsEntry > reasonsEntry, "The nav entry must follow Unroadworthy reasons.");
        Assert.Contains("#icon-link", nav[integrationsEntry..], StringComparison.Ordinal);
        Assert.Matches($"<a(?=[^>]*href=\"{Route}\")(?=[^>]*aria-current=\"page\")", html);

        var hub = await GetHtmlAsync(client, "/Administration");
        var cards = hub[hub.IndexOf("admin-configuration-title", StringComparison.Ordinal)..];
        var reasonsCard = cards.IndexOf("class=\"admin-card\" href=\"/Administration/UnroadworthyReasons\"", StringComparison.Ordinal);
        var integrationsCard = cards.IndexOf($"class=\"admin-card\" href=\"{Route}\"", StringComparison.Ordinal);
        Assert.True(reasonsCard >= 0 && integrationsCard > reasonsCard, "The hub card must follow Unroadworthy reasons.");
        Assert.Contains(OperatorLabels.Admin.Integrations, cards[integrationsCard..], StringComparison.Ordinal);
    }

    /// <summary>
    /// The one Save writes a switch only when its checkbox differs from the
    /// stored state: a save that changes one switch writes that switch's
    /// history alone, and one that changes nothing writes none.
    /// </summary>
    [Fact]
    public async Task TheOneSaveWritesOnlyTheSwitchItChanges()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Route);
        var operationKey = InputValue(html, "OperationKey");

        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveAutomatic",
            AutomaticForm(html, operationKey, glasses: true, brego: true)))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(Route, response.Headers.Location?.OriginalString);
        }

        Assert.Equal("automatic_brego_valuation_enabled", Assert.Single(await HistoryAsync(factory, operationKey)));
        var savedHtml = await GetHtmlAsync(client, Route);
        Assert.Contains(OperatorLabels.Integrations.AutomaticSaved, savedHtml, StringComparison.Ordinal);
        Assert.True(CheckboxIsChecked(savedHtml, "BregoValuationEnabled"));

        var unchangedKey = InputValue(savedHtml, "OperationKey");
        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveAutomatic",
            AutomaticForm(savedHtml, unchangedKey, glasses: true, brego: true)))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.Empty(await HistoryAsync(factory, unchangedKey));
    }

    /// <summary>
    /// R546 (29 October 2026): the automatic Glass's valuation at Case creation
    /// is an Administrator switch, on until it is turned off; since R1289 it is
    /// on Integrations, and the automatic requests panel's one Save turns it off.
    /// </summary>
    [Fact]
    public async Task TheOneSaveTurnsTheAutomaticGlassValuationOff()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, Route);
        Assert.Contains(
            $"<span>{HtmlEncoder.Default.Encode(OperatorLabels.Integrations.AutomaticGlassValuation)}</span>",
            html,
            StringComparison.Ordinal);
        Assert.True(CheckboxIsChecked(html, "GlassesValuationEnabled"));
        var operationKey = InputValue(html, "OperationKey");

        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveAutomatic",
            AutomaticForm(html, operationKey, glasses: false, brego: false)))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider
                .GetRequiredService<IAutomaticRequestControl>()
                .IsEnabledAsync(AutomaticRequestKind.GlassesValuation, CancellationToken.None));
        }

        Assert.Equal("automatic_guide_valuation_disabled", Assert.Single(await HistoryAsync(factory, operationKey)));
        var savedHtml = await GetHtmlAsync(client, Route);
        Assert.False(CheckboxIsChecked(savedHtml, "GlassesValuationEnabled"));
    }

    [Fact]
    public async Task AnExpiredFormChangesNoSwitch()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Route);

        using var response = await client.PostAsync(
            $"{Route}?handler=SaveAutomatic",
            AutomaticForm(html, "not-a-key", glasses: false, brego: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(OperatorLabels.Integrations.Expired, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var control = scope.ServiceProvider.GetRequiredService<IAutomaticRequestControl>();
        Assert.True(await control.IsEnabledAsync(AutomaticRequestKind.GlassesValuation, CancellationToken.None));
        Assert.False(await control.IsEnabledAsync(AutomaticRequestKind.BregoValuation, CancellationToken.None));
    }

    /// <summary>
    /// Every composed integration has its panel, in the page's order, with
    /// exactly its own fields, every box empty.
    /// </summary>
    [Fact]
    public async Task EachComposedLoginHasItsPanelWithItsOwnEmptyFields()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithEveryIntegration(baseFactory);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, Route);

        var order = new[]
        {
            IntegrationLogin.GlassValuation,
            IntegrationLogin.Brego,
            IntegrationLogin.Cazana,
            IntegrationLogin.ExperianHistory,
            IntegrationLogin.ExperianCap,
        };
        var positions = order
            .Select(login => html.IndexOf(
                $">{HtmlEncoder.Default.Encode(OperatorLabels.Integrations.Heading(login))}</h2>",
                StringComparison.Ordinal))
            .ToList();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.Order().ToList(), positions);

        foreach (var login in order)
        {
            var panel = Panel(html, login);
            Assert.Equal(OperatorLabels.Integrations.NotSet, FactValue(panel, OperatorLabels.Integrations.Account));
            Assert.Equal("—", FactValue(panel, OperatorLabels.Integrations.Updated));
            var names = InputRegex().Matches(panel)
                .Select(input => NameRegex().Match(input.Value).Groups["name"].Value)
                .Where(name => name is not "login" and not "expectedVersion" and not "OperationKey" and not "__RequestVerificationToken")
                .ToList();
            Assert.Equal(
                IntegrationLoginFields.Of(login).Select(IntegrationsModel.FieldName).ToList(),
                names);
            foreach (var field in IntegrationLoginFields.Of(login))
            {
                var input = InputRegex().Matches(panel)
                    .Select(match => match.Value)
                    .Single(value => value.Contains($"name=\"{IntegrationsModel.FieldName(field)}\"", StringComparison.Ordinal));
                Assert.DoesNotContain("value=", input, StringComparison.Ordinal);
                Assert.Equal(
                    IntegrationsModel.IsSecret(field),
                    input.Contains("type=\"password\"", StringComparison.Ordinal));
            }
        }

        // Brego's panel, in this change, is its account's e-mail address alone.
        Assert.DoesNotContain("type=\"password\"", Panel(html, IntegrationLogin.Brego), StringComparison.Ordinal);
    }

    /// <summary>
    /// A replaced login shows its account and when it was replaced; the page
    /// never shows a typed password, client secret or client identifier back.
    /// </summary>
    [Fact]
    public async Task AReplacedLoginShowsItsAccountAndNeverATypedValue()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithEveryIntegration(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Route);

        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveLogin",
            LoginForm(html, IntegrationLogin.GlassValuation, field => field == IntegrationLoginField.Username ? " valuer@ce " : TypedSecret)))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(Route, response.Headers.Location?.OriginalString);
        }

        var experianClientId = "client-id-never-shown-77";
        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveLogin",
            LoginForm(html, IntegrationLogin.ExperianCap, field => field switch
            {
                IntegrationLoginField.Username => "cap-user",
                IntegrationLoginField.ClientId => experianClientId,
                _ => TypedSecret,
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var savedHtml = await GetHtmlAsync(client, Route);
        Assert.Contains(OperatorLabels.Integrations.Saved, savedHtml, StringComparison.Ordinal);
        var glass = Panel(savedHtml, IntegrationLogin.GlassValuation);
        Assert.Equal("valuer@ce", FactValue(glass, OperatorLabels.Integrations.Account));
        Assert.NotEqual("—", FactValue(glass, OperatorLabels.Integrations.Updated));
        Assert.Equal("1", InputValue(glass, "expectedVersion"));
        Assert.Equal("cap-user", FactValue(Panel(savedHtml, IntegrationLogin.ExperianCap), OperatorLabels.Integrations.Account));
        Assert.DoesNotContain(TypedSecret, savedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(experianClientId, savedHtml, StringComparison.Ordinal);
        // The account's name is a fact, not a box's value.
        Assert.DoesNotContain("value=\"valuer@ce\"", savedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStaleVersionIsNotAccepted()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithEveryIntegration(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Route);

        using (var first = await client.PostAsync(
            $"{Route}?handler=SaveLogin",
            LoginForm(html, IntegrationLogin.Cazana, field => field == IntegrationLoginField.Email ? "first@ce" : TypedSecret)))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        // The same form again, drawn at version 0, after the login moved to 1.
        using (var stale = await client.PostAsync(
            $"{Route}?handler=SaveLogin",
            LoginForm(html, IntegrationLogin.Cazana, field => field == IntegrationLoginField.Email ? "second@ce" : TypedSecret)))
        {
            Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        }

        var after = await GetHtmlAsync(client, Route);
        Assert.Contains(
            $"<div class=\"notice notice--danger\" role=\"alert\"><span>{OperatorLabels.Integrations.NotAccepted}</span></div>",
            after,
            StringComparison.Ordinal);
        Assert.Equal("first@ce", FactValue(Panel(after, IntegrationLogin.Cazana), OperatorLabels.Integrations.Account));
        Assert.DoesNotContain(TypedSecret, after, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(IntegrationLogin.GlassValuation, IntegrationLoginField.Username)]
    [InlineData(IntegrationLogin.GlassValuation, IntegrationLoginField.Password)]
    [InlineData(IntegrationLogin.Brego, IntegrationLoginField.Email)]
    [InlineData(IntegrationLogin.ExperianHistory, IntegrationLoginField.ClientId)]
    [InlineData(IntegrationLogin.ExperianCap, IntegrationLoginField.ClientSecret)]
    public async Task ABlankFieldShowsItsMessageAndNoTypedSecret(IntegrationLogin login, IntegrationLoginField blank)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithEveryIntegration(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Route);

        using var response = await client.PostAsync(
            $"{Route}?handler=SaveLogin",
            LoginForm(html, login, field => field == blank
                ? " "
                : IntegrationsModel.IsSecret(field) ? TypedSecret : TypedName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refusedHtml = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"<li>{HtmlEncoder.Default.Encode(OperatorLabels.Integrations.Required(blank))}</li>",
            refusedHtml,
            StringComparison.Ordinal);
        foreach (var field in IntegrationLoginFields.Of(login).Where(field => field != blank))
        {
            Assert.DoesNotContain(
                HtmlEncoder.Default.Encode(OperatorLabels.Integrations.Required(field)),
                refusedHtml,
                StringComparison.Ordinal);
        }
        Assert.DoesNotContain(TypedSecret, refusedHtml, StringComparison.Ordinal);
        Assert.Equal(OperatorLabels.Integrations.NotSet, FactValue(Panel(refusedHtml, login), OperatorLabels.Integrations.Account));
        await using var scope = factory.Services.CreateAsyncScope();
        var statuses = await scope.ServiceProvider
            .GetRequiredService<IIntegrationLoginAdministration>()
            .ListAsync(Administrator, CancellationToken.None);
        Assert.Equal(0, statuses.Single(status => status.Login == login).Version);
    }

    /// <summary>A stored login the key ring can no longer read is typed again.</summary>
    [Fact]
    public async Task AnUnreadableLoginAsksToBeSavedAgain()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithEveryIntegration(baseFactory);
        await SeedGlassLoginAsync(factory);
        var database = await baseFactory.DatabaseAsync;
        await database.ExecuteAsync(
            "UPDATE IntegrationLogins SET ProtectedLogin = N'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA' WHERE Id = N'glass-valuation'");
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, Route);

        var glass = Panel(html, IntegrationLogin.GlassValuation);
        Assert.Contains(
            $"<div class=\"notice notice--danger\" role=\"alert\"><span>{OperatorLabels.Integrations.Unreadable}</span></div>",
            glass,
            StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Integrations.Unreadable, Panel(html, IntegrationLogin.Cazana), StringComparison.Ordinal);
    }

    /// <summary>
    /// The page reads the five switches in one statement and every login in
    /// another, beside the shell's own: its load is at most the hub's plus
    /// those two, and stored logins do not add to it.
    /// </summary>
    [Fact]
    public async Task ThePageLoadIsTheShellAndTwoReadsWhateverIsStored()
    {
        var counter = new SqlStatementCounter();
        using var baseFactory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        using var factory = WithEveryIntegration(baseFactory);
        using var client = CreateClient(factory);
        // The first loads pay one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, "/Administration");
        _ = await GetHtmlAsync(client, Route);

        counter.Reset();
        _ = await GetHtmlAsync(client, "/Administration");
        var hub = counter.Count;

        counter.Reset();
        _ = await GetHtmlAsync(client, Route);
        var empty = counter.Count;
        var emptyDescription = counter.Describe();

        await SeedGlassLoginAsync(factory);
        counter.Reset();
        _ = await GetHtmlAsync(client, Route);
        var stored = counter.Count;

        Assert.True(empty > 0, "The interceptor observed no commands at all.");
        Assert.True(
            empty <= hub + 2,
            $"The Integrations page sent {empty} SQL commands; the hub sent {hub}, and at most two more are allowed."
                + Environment.NewLine + emptyDescription);
        Assert.True(
            stored == empty,
            $"The page grew with a stored login: {empty} commands with none and {stored} with one."
                + Environment.NewLine + counter.Describe());
    }

    private static async Task SeedGlassLoginAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IIntegrationLoginAdministration>()
            .ReplaceAsync(
                Administrator,
                IntegrationLogin.GlassValuation,
                expectedVersion: 0,
                new Dictionary<IntegrationLoginField, string>
                {
                    [IntegrationLoginField.Username] = "seeded-valuer",
                    [IntegrationLoginField.Password] = TypedSecret,
                },
                "integrations-page-seed",
                CancellationToken.None);
    }

    /// <summary>
    /// A provider for every integration, never asked, so the host composes each
    /// login's integration and the page draws its panel.
    /// </summary>
    private static WebApplicationFactory<Program> WithEveryIntegration(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            foreach (var source in GuideSources)
            {
                services.AddSingleton<IGuideValuationProvider>(new UnaskedGuideValuationProvider(source));
            }

            services.AddSingleton<IVehicleHistoryCheckProvider>(new UnaskedVehicleHistoryCheckProvider());
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static FormUrlEncodedContent AutomaticForm(string html, string operationKey, bool glasses, bool brego) =>
        Form(
            AntiforgeryValue(html),
            ("OperationKey", operationKey),
            ("GlassesValuationEnabled", glasses ? "true" : "false"),
            ("BregoValuationEnabled", brego ? "true" : "false"),
            ("CazanaValuationEnabled", "false"),
            ("CapValuationEnabled", "false"),
            ("VehicleHistoryCheckEnabled", "false"));

    /// <summary>One login's form as its panel was drawn, with each field typed by <paramref name="value"/>.</summary>
    private static FormUrlEncodedContent LoginForm(
        string html,
        IntegrationLogin login,
        Func<IntegrationLoginField, string> value)
    {
        var panel = Panel(html, login);
        return Form(
            AntiforgeryValue(html),
            [
                ("login", InputValue(panel, "login")),
                ("expectedVersion", InputValue(panel, "expectedVersion")),
                ("OperationKey", InputValue(panel, "OperationKey")),
                .. IntegrationLoginFields.Of(login).Select(field => (IntegrationsModel.FieldName(field), value(field))),
            ]);
    }

    private static async Task<string[]> HistoryAsync(WebApplicationFactory<Program> factory, string operationKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ActionHistory.AsNoTracking()
            .Where(item => item.CorrelationId == operationKey)
            .Select(item => item.EventKind)
            .ToArrayAsync();
    }

    /// <summary>One login's panel, by its heading's id.</summary>
    private static string Panel(string html, IntegrationLogin login)
    {
        var match = Regex.Match(
            html,
            $"<section class=\"panel\" aria-labelledby=\"login-{login}-title\">[\\s\\S]*?</section>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The page must draw the {login} panel.");
        return match.Value;
    }

    /// <summary>The value of one fact, by its label.</summary>
    private static string FactValue(string html, string label)
    {
        var match = Regex.Match(
            html,
            $"<dt>{Regex.Escape(label)}</dt>\\s*<dd[^>]*>(?<value>[^<]*)</dd>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The panel must draw the '{label}' fact.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    /// <summary>Whether the named checkbox opens checked.</summary>
    private static bool CheckboxIsChecked(string html, string name)
    {
        var tag = InputRegex().Matches(html)
            .Select(match => match.Value)
            .SingleOrDefault(value =>
                value.Contains($"name=\"{name}\"", StringComparison.Ordinal)
                && value.Contains("type=\"checkbox\"", StringComparison.Ordinal));
        Assert.True(tag is not null, $"The page must draw the '{name}' checkbox.");
        return tag!.Contains("checked", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<input[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputRegex();

    [GeneratedRegex("name=\"(?<name>[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    private sealed class UnaskedGuideValuationProvider(ValuationSource source) : IGuideValuationProvider
    {
        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The Integrations page never asks for a valuation.");
    }

    private sealed class UnaskedVehicleHistoryCheckProvider : IVehicleHistoryCheckProvider
    {
        public Task<VehicleHistoryCheckResult> CheckAsync(string registration, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The Integrations page never runs a check.");
    }
}
