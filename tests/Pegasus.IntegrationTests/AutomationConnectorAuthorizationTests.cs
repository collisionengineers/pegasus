using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The authorization-code + PKCE flow for external MCP connectors: an
/// Administrator consents at <c>/authorize</c>, the connector exchanges the
/// code (with its client secret and PKCE verifier) for tokens shaped exactly
/// like client-credentials tokens, and refreshes without a new consent. The
/// DevelopmentOffline host authenticates every browser request as the seeded
/// Administrator, so the consent page is reached without a sign-in dance.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AutomationConnectorAuthorizationTests
{
    private const string State = "state-4c1c0d0f";

    [Fact]
    public async Task AdministrationHealthReadsTheConfiguredClientRegistryAfterEachChange()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        var administrator = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

        foreach (var enabled in new[] { true, false, true })
        {
            await using (var scope = mcpFactory.Services.CreateAsyncScope())
            {
                var registry = scope.ServiceProvider.GetRequiredService<AutomationClientRegistry>();
                await registry.SetEnabledAsync(
                    enabled, administrator, "Verify configured health state", Guid.NewGuid().ToString("N"), default);
            }
            await using (var scope = mcpFactory.Services.CreateAsyncScope())
            {
                var status = scope.ServiceProvider.GetRequiredService<Pegasus.Core.Operations.IAutomationIngressStatusQueries>();
                Assert.IsType<AutomationIngressStatusQueries>(status);
                Assert.Equal(enabled, await status.IsEnabledAsync(default));
            }
            using var response = await browser.GetAsync("/Administration/Health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Automation ingress", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SeparateConsentsRetainDistinctGrantAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        using var connector = CreateConnector(mcpFactory);

        for (var index = 0; index < 2; index++)
        {
            var (verifier, challenge) = Pkce();
            using var consent = await browser.GetAsync(AuthorizeUrl(challenge, "automation.cases"));
            var html = await consent.Content.ReadAsStringAsync();
            using var approval = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), ConsentForm(html));
            Assert.Equal(HttpStatusCode.Redirect, approval.StatusCode);
            var code = ParseQuery(approval.Headers.Location!)["code"]!;
            using var tokens = await connector.PostAsync(
                AutomationMcp.TokenEndpointPath,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["code_verifier"] = verifier,
                    ["redirect_uri"] = ConnectorRedirectUri,
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret
                }));
            using var json = JsonDocument.Parse(await tokens.Content.ReadAsStringAsync());
            var accessToken = json.RootElement.GetProperty("access_token").GetString()!;
            using var call = await PostMcpAsync(connector, accessToken, ToolCallPayload(
                90 + index, "pegasus_case_search", new { query = $"no-match-{index}" }));
            Assert.Equal(HttpStatusCode.OK, call.StatusCode);
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT AggregateId) FROM ActionHistory
            WHERE EventKind = N'automation_connector_authorized'
              AND AggregateId LIKE N'grant:%'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT ActorSubjectId) FROM ActionHistory
            WHERE EventKind = N'pegasus_case_search'
              AND ActorKind = N'Automation'
              AND ActorSubjectId LIKE N'grant:%'
              AND Outcome = N'Succeeded'
            """));
    }

    [Fact]
    public async Task AdministratorConsentIssuesCodeThenTokensThatReachMcpAndRefresh()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        using var connector = CreateConnector(mcpFactory);
        var (verifier, challenge) = Pkce();

        // Consent page: the Administrator sees the connector and the scopes.
        using var consent = await browser.GetAsync(AuthorizeUrl(challenge, "automation.cases automation.documents"));
        var html = await consent.Content.ReadAsStringAsync();
        Assert.True(consent.StatusCode == HttpStatusCode.OK, $"{(int)consent.StatusCode}: {html}");
        Assert.Contains("connector.example", html, StringComparison.Ordinal);
        Assert.Contains("automation.cases", html, StringComparison.Ordinal);
        Assert.Contains("automation.documents", html, StringComparison.Ordinal);
        Assert.DoesNotContain("automation.intake", html, StringComparison.Ordinal);

        // Approve: OpenIddict redirects back to the exact registered URI with the code.
        using var approve = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), ConsentForm(html));
        Assert.True(approve.StatusCode == HttpStatusCode.Redirect, $"{(int)approve.StatusCode}: {await approve.Content.ReadAsStringAsync()}");
        var location = approve.Headers.Location!;
        Assert.StartsWith(ConnectorRedirectUri, location.AbsoluteUri, StringComparison.Ordinal);
        var callback = ParseQuery(location);
        Assert.Equal(State, callback["state"]);
        Assert.False(string.IsNullOrEmpty(callback["code"]));

        // Exchange: code + verifier + client secret.
        using var tokens = await connector.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callback["code"]!,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = ConnectorRedirectUri,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        var tokenBody = await tokens.Content.ReadAsStringAsync();
        Assert.True(tokens.IsSuccessStatusCode, tokenBody);
        using var tokenJson = JsonDocument.Parse(tokenBody);
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = tokenJson.RootElement.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken));

        // The connector token reaches /mcp with the consented scopes only.
        using (var list = await PostMcpAsync(connector, accessToken, ToolsListPayload(1)))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var document = await ReadJsonRpcAsync(list);
            Assert.NotEmpty(document.RootElement.GetProperty("result")
                .GetProperty("tools")
                .EnumerateArray());
        }
        using (var denied = await PostMcpAsync(
            connector,
            accessToken,
            ToolCallPayload(2, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            using var document = await ReadJsonRpcAsync(denied);
            Assert.Contains("automation.intake", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        // Refresh: a new access token without a new consent.
        using var refreshed = await connector.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken!,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        var refreshedBody = await refreshed.Content.ReadAsStringAsync();
        Assert.True(refreshed.IsSuccessStatusCode, refreshedBody);
        using var refreshedJson = JsonDocument.Parse(refreshedBody);
        var refreshedAccess = refreshedJson.RootElement.GetProperty("access_token").GetString()!;
        using (var list = await PostMcpAsync(connector, refreshedAccess, ToolsListPayload(3)))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }

        // The Administrator's decision is permanent history.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'automation_connector_authorized'
              AND AggregateId LIKE N'grant:%'
              AND Outcome = N'Succeeded'
              AND Reason LIKE N'Connector https://connector.example%automation.cases automation.documents%'
            """));
    }

    [Fact]
    public async Task AccessAndRefreshTokensSurviveAHostRestartWithSharedPersistentCredentials()
    {
        using var signing = Certificate(
            "persistent-signing",
            new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(2035, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var encryption = Certificate(
            "persistent-encryption",
            new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(2035, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var baseFactory = new IntakeWebApplicationFactory(TimeProvider.System);
        string accessToken;
        string refreshToken;

        using (var firstHost = WithPersistentOAuthCredentials(baseFactory, signing, encryption))
        using (var browser = CreateBrowser(firstHost))
        using (var connector = CreateConnector(firstHost))
        {
            var (verifier, challenge) = Pkce();
            using var consent = await browser.GetAsync(AuthorizeUrl(challenge, "automation.cases"));
            var html = await consent.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
            using var approval = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), ConsentForm(html));
            var code = ParseQuery(approval.Headers.Location!)["code"]!;
            using var exchange = await connector.PostAsync(
                AutomationMcp.TokenEndpointPath,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["code_verifier"] = verifier,
                    ["redirect_uri"] = ConnectorRedirectUri,
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret
                }));
            var body = await exchange.Content.ReadAsStringAsync();
            Assert.True(exchange.IsSuccessStatusCode, body);
            using var tokens = JsonDocument.Parse(body);
            accessToken = tokens.RootElement.GetProperty("access_token").GetString()!;
            refreshToken = tokens.RootElement.GetProperty("refresh_token").GetString()!;
        }

        using var secondHost = WithPersistentOAuthCredentials(baseFactory, signing, encryption);
        using var secondConnector = CreateConnector(secondHost);
        using (var originalAccess = await PostMcpAsync(
            secondConnector, accessToken, ToolsListPayload(401)))
        {
            Assert.Equal(HttpStatusCode.OK, originalAccess.StatusCode);
        }

        using var refresh = await secondConnector.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        var refreshBody = await refresh.Content.ReadAsStringAsync();
        Assert.True(refresh.IsSuccessStatusCode, refreshBody);
        using var refreshedTokens = JsonDocument.Parse(refreshBody);
        var rotatedRefreshToken = refreshedTokens.RootElement.GetProperty("refresh_token").GetString()!;
        Assert.NotEqual(refreshToken, rotatedRefreshToken);

        using var rotatedRefresh = await secondConnector.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = rotatedRefreshToken,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        var rotatedRefreshBody = await rotatedRefresh.Content.ReadAsStringAsync();
        Assert.True(rotatedRefresh.IsSuccessStatusCode, rotatedRefreshBody);
        using var rotatedTokens = JsonDocument.Parse(rotatedRefreshBody);
        var rotatedAccessToken = rotatedTokens.RootElement.GetProperty("access_token").GetString()!;
        using var rotatedAccess = await PostMcpAsync(
            secondConnector, rotatedAccessToken, ToolsListPayload(402));
        Assert.Equal(HttpStatusCode.OK, rotatedAccess.StatusCode);
    }

    [Fact]
    public async Task RefusedConsentReturnsAccessDeniedAndIsRecorded()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        var (_, challenge) = Pkce();

        using var consent = await browser.GetAsync(AuthorizeUrl(challenge, "automation.cases"));
        var html = await consent.Content.ReadAsStringAsync();
        using var deny = await browser.PostAsync(AuthorizeHandlerUrl("Deny"), ConsentForm(html));
        Assert.Equal(HttpStatusCode.Redirect, deny.StatusCode);
        var callback = ParseQuery(deny.Headers.Location!);
        Assert.Equal("access_denied", callback["error"]);
        Assert.Equal(State, callback["state"]);
        Assert.False(callback.ContainsKey("code"));

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'automation_connector_denied' AND Outcome = N'Denied'
            """));
    }

    [Fact]
    public async Task UnregisteredRedirectUriAndMissingPkceAreRefusedBeforeConsent()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        var (_, challenge) = Pkce();

        using (var wrongRedirect = await browser.GetAsync(
            AuthorizeUrl(challenge, "automation.cases", redirectUri: "https://evil.example/callback")))
        {
            // OpenIddict refuses to redirect to an unregistered URI: no consent
            // page, and no code ever leaves the server.
            Assert.NotEqual(HttpStatusCode.Redirect, wrongRedirect.StatusCode);
            var body = await wrongRedirect.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Authorise the connector", body, StringComparison.Ordinal);
        }

        using (var noPkce = await browser.GetAsync(AuthorizeUrl(challenge: null, "automation.cases")))
        {
            var body = await noPkce.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Authorise the connector", body, StringComparison.Ordinal);
            if (noPkce.StatusCode == HttpStatusCode.Redirect)
            {
                Assert.Equal("invalid_request", ParseQuery(noPkce.Headers.Location!)["error"]);
            }
        }
    }

    [Fact]
    public async Task DisabledClientCannotAuthoriseOrExchange()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        using var connector = CreateConnector(mcpFactory);
        var (verifier, challenge) = Pkce();

        using var consent = await browser.GetAsync(AuthorizeUrl(challenge, "automation.cases"));
        var html = await consent.Content.ReadAsStringAsync();
        using var approve = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), ConsentForm(html));
        var code = ParseQuery(approve.Headers.Location!)["code"]!;

        using (var scope = mcpFactory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<AutomationClientRegistry>();
            await registry.SetEnabledAsync(
                enabled: false,
                ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                "Integration-test kill switch",
                Guid.NewGuid().ToString("N"),
                CancellationToken.None);
        }

        // The disabled registration refuses the code exchange...
        using var exchange = await connector.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = ConnectorRedirectUri,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        Assert.False(exchange.IsSuccessStatusCode);

        // ...and a fresh authorization request never reaches the consent form.
        var (_, challenge2) = Pkce();
        using var refused = await browser.GetAsync(AuthorizeUrl(challenge2, "automation.cases"));
        var refusedBody = await refused.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Authorise the connector", refusedBody, StringComparison.Ordinal);
    }

    // The staff cookies (identity, antiforgery) are Secure, so the browser
    // half of the flow talks to the test host over https like the case web
    // tests do; the connector half uses the same origin so tokens issued and
    // validated by the local server share one issuer.
    private static HttpClient CreateBrowser(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    private static HttpClient CreateConnector(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static WebApplicationFactory<Program> WithPersistentOAuthCredentials(
        IntakeWebApplicationFactory factory,
        X509Certificate2 signing,
        X509Certificate2 encryption) => WithAutomationMcp(factory)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.PostConfigure<OpenIddictServerOptions>(options =>
                {
                    options.SigningCredentials.Clear();
                    options.EncryptionCredentials.Clear();
                    options.SigningCredentials.Add(new(
                        new X509SecurityKey(signing),
                        SecurityAlgorithms.RsaSha256));
                    options.EncryptionCredentials.Add(new(
                        new X509SecurityKey(encryption),
                        SecurityAlgorithms.RsaOAEP,
                        SecurityAlgorithms.Aes256CbcHmacSha512));
                })));

    private static X509Certificate2 Certificate(
        string name,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(notBefore, notAfter);
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pkcs12),
            null,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string AuthorizeUrl(
        string? challenge,
        string scope,
        string redirectUri = ConnectorRedirectUri)
    {
        var query = new List<string>
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(scope)}",
            $"state={State}",
            $"resource={Uri.EscapeDataString("http://localhost/mcp")}"
        };
        if (challenge is not null)
        {
            query.Add($"code_challenge={challenge}");
            query.Add("code_challenge_method=S256");
        }

        return AutomationMcp.AuthorizationEndpointPath + "?" + string.Join('&', query);
    }

    private static string AuthorizeHandlerUrl(string handler) =>
        AutomationMcp.AuthorizationEndpointPath + "?handler=" + handler;

    private static FormUrlEncodedContent ConsentForm(string html)
    {
        // Re-post exactly what the page rendered: the echoed OAuth parameters,
        // the operation key and the antiforgery token.
        // Only the consent panel's form: the shell has its own sign-out form
        // with its own antiforgery token.
        var start = html.IndexOf("aria-labelledby=\"connector-heading\"", StringComparison.Ordinal);
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "consent panel not found");
        var panel = html[start..end];
        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match input in InputTag().Matches(panel))
        {
            var tag = input.Value;
            if (!tag.Contains("type=\"hidden\"", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Attribute().Match(tag);
            var value = ValueAttribute().Match(tag);
            if (name.Success && value.Success)
            {
                fields.Add(new(WebUtilityDecode(name.Groups["v"].Value), WebUtilityDecode(value.Groups["v"].Value)));
            }
        }

        Assert.Contains(fields, field => field.Key == "__RequestVerificationToken");
        Assert.Contains(fields, field => field.Key == "OperationKey");
        return new FormUrlEncodedContent(fields);
    }

    private static string WebUtilityDecode(string value) => WebUtility.HtmlDecode(value);

    private static Dictionary<string, string?> ParseQuery(Uri uri)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            values[key] = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return values;
    }

    [GeneratedRegex("<input[^>]*>")]
    private static partial Regex InputTag();

    [GeneratedRegex(@"\sname=""(?<v>[^""]+)""")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"\svalue=""(?<v>[^""]*)""")]
    private static partial Regex ValueAttribute();
}
