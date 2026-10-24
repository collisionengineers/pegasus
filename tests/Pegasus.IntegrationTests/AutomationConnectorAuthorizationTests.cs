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
using Pegasus.Web.Pages.Connect;
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
            var html = await response.Content.ReadAsStringAsync();
            var automationStart = html.IndexOf("Automation clients", StringComparison.Ordinal);
            Assert.True(automationStart >= 0, "Service health must include Automation clients.");
            var automationEnd = html.IndexOf("</tr>", automationStart, StringComparison.Ordinal);
            Assert.True(automationEnd > automationStart, "The Automation clients health row must close.");
            var automationRow = html[automationStart..automationEnd];
            Assert.Contains(enabled ? "Working" : "No recorded activity", automationRow, StringComparison.Ordinal);
            Assert.DoesNotContain("Automation ingress", html, StringComparison.Ordinal);
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
            using var call = await PostMcpAsync(connector, CasesServer, accessToken, ToolCallPayload(
                90 + index, "pegasus_case_search", new { query = $"no-match-{index}" }));
            Assert.Equal(HttpStatusCode.OK, call.StatusCode);
        }

        Assert.Equal(2, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT AggregateId) FROM ActionHistory
            WHERE EventKind = N'automation_connector_authorized'
              AND AggregateId LIKE N'grant:%'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(2, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT ActorSubjectId) FROM ActionHistory
            WHERE EventKind = N'pegasus_case_search'
              AND ActorKind = N'Automation'
              AND ActorSubjectId LIKE N'grant:%'
              AND Outcome = N'Succeeded'
            """));
    }

    /// <summary>
    /// A staged upload is the Automation client's, not one grant's: each
    /// connector consents with its own grant, so a document staged under one
    /// is begun again, and added, under another of the same client.
    /// </summary>
    [Fact]
    public async Task AnUploadStagedUnderOneGrantIsUsedUnderAnotherOfTheSameClient()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var browser = CreateBrowser(mcpFactory);
        using var connector = CreateConnector(mcpFactory);
        var tokens = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            var (verifier, challenge) = Pkce();
            using var consent = await browser.GetAsync(
                AuthorizeUrl(challenge, "automation.documents", server: DocumentsServer));
            var html = await consent.Content.ReadAsStringAsync();
            using var approval = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), ConsentForm(html));
            var code = ParseQuery(approval.Headers.Location!)["code"]!;
            using var exchanged = await connector.PostAsync(
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
            using var json = JsonDocument.Parse(await exchanged.Content.ReadAsStringAsync());
            tokens.Add(json.RootElement.GetProperty("access_token").GetString()!);
        }

        var document = Encoding.UTF8.GetBytes(new string('p', 70_000));
        const string operationKey = "mcp:upload-across-grants";
        var uploadId = await StageUploadAsync(connector, tokens[0], document, operationKey);
        using (var begin = await PostMcpAsync(connector, DocumentsServer, tokens[1], ToolCallPayload(
            30,
            "pegasus_upload_begin",
            new { totalBytes = document.Length, sha256 = Sha256Hex(document), operationKey })))
        {
            var ticket = await ReadStructuredContentAsync(begin);
            Assert.Equal(uploadId, ticket.GetProperty("uploadId").GetGuid());
            Assert.True(ticket.GetProperty("isReplay").GetBoolean());
            Assert.Equal(2, ticket.GetProperty("receivedParts").GetArrayLength());
        }

        using var add = await PostMcpAsync(connector, DocumentsServer, tokens[1], ToolCallPayload(
            31,
            "pegasus_document_add",
            new
            {
                caseId,
                fileName = "staged.txt",
                mediaType = "text/plain",
                semanticRole = "Other",
                expectedCaseVersion = 0,
                operationKey = "mcp:upload-across-grants-add",
                uploadId
            }));
        var added = await ReadStructuredContentAsync(add);
        Assert.Equal(Sha256Hex(document), added.GetProperty("sha256").GetString(), ignoreCase: true);
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
        // Each requested scope reads as its description alone (v41 S5).
        Assert.Contains($"<li>{AuthorizeModel.ScopeDescriptions[AutomationMcp.CasesScope]}</li>", html, StringComparison.Ordinal);
        Assert.Contains($"<li>{AuthorizeModel.ScopeDescriptions[AutomationMcp.DocumentsScope]}</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthorizeModel.ScopeDescriptions[AutomationMcp.IntakeScope], html, StringComparison.Ordinal);

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

        // The cases tools with the edit-lease tools, and the documents tools.
        string[] casesTools =
        [
            "pegasus_case_action",
            "pegasus_case_get",
            "pegasus_case_note_add",
            "pegasus_case_search",
            "pegasus_case_update_details",
            "pegasus_directory_search",
            "pegasus_edit_begin",
            "pegasus_edit_end",
            "pegasus_edit_renew",
            "pegasus_vocabulary_get",
            "pegasus_work_centre_dismiss"
        ];
        string[] documentsTools =
        [
            "pegasus_document_action",
            "pegasus_document_add",
            "pegasus_document_download",
            "pegasus_document_download_batch",
            "pegasus_image_preparation_get",
            "pegasus_image_prepare",
            "pegasus_report_action",
            "pegasus_report_list",
            "pegasus_report_wording_get",
            "pegasus_report_wording_save",
            "pegasus_upload_begin",
            "pegasus_upload_part"
        ];

        // The connector token reaches the server of each consented scope and
        // lists that server's tools; the intake server refuses it.
        foreach (var (server, expected, rpcId) in new[] { (CasesServer, casesTools, 1), (DocumentsServer, documentsTools, 2) })
        {
            using var list = await PostMcpAsync(connector, server, accessToken, ToolsListPayload(rpcId));
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var document = await ReadJsonRpcAsync(list);
            Assert.Equal(
                expected,
                document.RootElement.GetProperty("result")
                    .GetProperty("tools")
                    .EnumerateArray()
                    .Select(tool => tool.GetProperty("name").GetString())
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
        }
        using (var denied = await PostMcpAsync(
            connector,
            IntakeServer,
            accessToken,
            ToolCallPayload(2, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
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
        using (var list = await PostMcpAsync(connector, CasesServer, refreshedAccess, ToolsListPayload(3)))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }

        // The Administrator's decision is permanent history.
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'automation_connector_authorized'
              AND AggregateId LIKE N'grant:%'
              AND Outcome = N'Succeeded'
              AND Reason LIKE N'Connector https://connector.example%automation.cases automation.documents%'
            """));
    }

    /// <summary>
    /// ADR-0080 decision 6, stated from the code. A token carries the shared
    /// audience, its scopes and its grant, never the server its connector
    /// named, and the split changes none of these: an access or refresh token
    /// issued before it is accepted by each server whose scope it holds until
    /// it expires, and a refresh naming no resource still works. The old
    /// resource does not survive: a refresh naming it is refused, so an
    /// Administrator re-adds each connector at its server's URL and consents
    /// once per server.
    /// </summary>
    [Fact]
    public async Task ATokenFromBeforeTheSplitServesItsScopesButItsConnectorIsReAddedPerServer()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var browser = CreateBrowser(mcpFactory);
        using var connector = CreateConnector(mcpFactory);

        // The consent and the code exchange are unchanged by the split, so this
        // token has the shape a consent before it gave.
        var (verifier, challenge) = Pkce();
        using var consent = await browser.GetAsync(AuthorizeUrl(challenge, "automation.cases automation.documents"));
        var html = await consent.Content.ReadAsStringAsync();
        using var approve = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), ConsentForm(html));
        using var exchange = await connector.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = ParseQuery(approve.Headers.Location!)["code"]!,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = ConnectorRedirectUri,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        var exchangeBody = await exchange.Content.ReadAsStringAsync();
        Assert.True(exchange.IsSuccessStatusCode, exchangeBody);
        using var tokens = JsonDocument.Parse(exchangeBody);
        var accessToken = tokens.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = tokens.RootElement.GetProperty("refresh_token").GetString()!;

        // Named for the cases server, the token serves the documents server too.
        foreach (var (server, rpcId) in new[] { (CasesServer, 1), (DocumentsServer, 2) })
        {
            using var list = await PostMcpAsync(connector, server, accessToken, ToolsListPayload(rpcId));
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }

        // A refresh naming the resource a connector added before the split held is refused.
        using (var oldResource = await RefreshAsync(connector, refreshToken, "http://localhost/mcp"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, oldResource.StatusCode);
            using var error = JsonDocument.Parse(await oldResource.Content.ReadAsStringAsync());
            Assert.Equal("invalid_target", error.RootElement.GetProperty("error").GetString());
        }

        // One naming no resource still works, and its token serves both servers.
        using var refreshed = await RefreshAsync(connector, refreshToken, resource: null);
        var refreshedBody = await refreshed.Content.ReadAsStringAsync();
        Assert.True(refreshed.IsSuccessStatusCode, refreshedBody);
        using var refreshedTokens = JsonDocument.Parse(refreshedBody);
        using (var list = await PostMcpAsync(
            connector,
            DocumentsServer,
            refreshedTokens.RootElement.GetProperty("access_token").GetString()!,
            ToolsListPayload(3)))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }

        // Re-adding: each server's own resource reaches the consent page.
        foreach (var server in new[]
        {
            CasesServer, IntakeServer, DocumentsServer, AssessmentServer, MailServer, JobsServer, SendServer
        })
        {
            var (_, serverChallenge) = Pkce();
            using var serverConsent = await browser.GetAsync(
                AuthorizeUrl(serverChallenge, "automation." + server["/mcp/".Length..], server: server));
            var serverHtml = await serverConsent.Content.ReadAsStringAsync();
            Assert.True(serverConsent.StatusCode == HttpStatusCode.OK, $"{server}: {(int)serverConsent.StatusCode}");
            Assert.Contains("connector.example", serverHtml, StringComparison.Ordinal);
        }
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
            secondConnector, CasesServer, accessToken, ToolsListPayload(401)))
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
            secondConnector, CasesServer, rotatedAccessToken, ToolsListPayload(402));
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

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
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
        string redirectUri = ConnectorRedirectUri,
        string server = CasesServer)
    {
        var query = new List<string>
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(scope)}",
            $"state={State}",
            $"resource={Uri.EscapeDataString("http://localhost" + server)}"
        };
        if (challenge is not null)
        {
            query.Add($"code_challenge={challenge}");
            query.Add("code_challenge_method=S256");
        }

        return AutomationMcp.AuthorizationEndpointPath + "?" + string.Join('&', query);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient connector, string refreshToken, string? resource)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret
        };
        if (resource is not null)
        {
            form["resource"] = resource;
        }

        return connector.PostAsync(AutomationMcp.TokenEndpointPath, new FormUrlEncodedContent(form));
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
