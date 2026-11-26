using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using static Pegasus.IntegrationTests.DesktopSignInTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Sign-in from Pegasus Desktop (ADR-0092, R1297 to R1299): a member of staff
/// of any role signs in through the browser with their own Pegasus details,
/// the public native client exchanges the code with its PKCE verifier and no
/// secret, and the tokens name that member of staff. The host runs the real
/// staff cookie (<c>Features:PasswordSignIn</c>), so a User-role account
/// reaches the page as it would on a deployed estate.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DesktopSignInTests
{
    [Fact]
    public async Task AUserSignsInFromPegasusDesktopAndTheTokenNamesThemNotTheAutomationActor()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var staffId = await CreateStaffAsync(host, "desktop-user", StaffRole.User);
        using var browser = CreateBrowser(host);
        using var desktop = CreateConnector(host);
        await SignInAsync(browser, "desktop-user");
        var redirectUri = LoopbackRedirectUri(DesktopPorts[0]);
        var (verifier, challenge) = Pkce();

        // The page: the operator's wording, the account by name, two buttons
        // and no scope tick boxes.
        using var page = await browser.GetAsync(AuthorizeUrl(challenge, redirectUri));
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.StatusCode == HttpStatusCode.OK, $"{(int)page.StatusCode}: {html}");
        Assert.Contains($"<h1>{OperatorLabels.DesktopSignIn.Title}</h1>", html, StringComparison.Ordinal);
        Assert.Contains($"<p>{OperatorLabels.DesktopSignIn.Sentence}</p>", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<dt>{OperatorLabels.DesktopSignIn.SignedInAs}</dt><dd>desktop-user</dd>",
            html,
            StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.DesktopSignIn.SignIn}</button>", html, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.DesktopSignIn.Refuse}</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"ApprovedScopes\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorise the connector", html, StringComparison.Ordinal);

        // Sign in: the code goes back to the loopback address the application
        // is listening on.
        using var approved = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), SignInForm(html));
        Assert.True(
            approved.StatusCode == HttpStatusCode.Redirect,
            $"{(int)approved.StatusCode}: {await approved.Content.ReadAsStringAsync()}");
        Assert.StartsWith(redirectUri, approved.Headers.Location!.AbsoluteUri, StringComparison.Ordinal);
        var callback = ParseQuery(approved.Headers.Location!);
        Assert.Equal(State, callback["state"]);
        Assert.False(string.IsNullOrEmpty(callback["code"]));

        // Exchange: the code and its verifier, and no client secret.
        using var exchanged = await desktop.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callback["code"]!,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = AutomationMcp.DesktopClientId
            }));
        var exchangedBody = await exchanged.Content.ReadAsStringAsync();
        Assert.True(exchanged.IsSuccessStatusCode, exchangedBody);
        using var tokens = JsonDocument.Parse(exchangedBody);
        var accessToken = tokens.RootElement.GetProperty("access_token").GetString()!;
        Assert.False(string.IsNullOrEmpty(tokens.RootElement.GetProperty("refresh_token").GetString()));
        Assert.InRange(
            tokens.RootElement.GetProperty("expires_in").GetInt32(),
            (int)AutomationMcp.AccessTokenLifetime.TotalSeconds - 30,
            (int)AutomationMcp.AccessTokenLifetime.TotalSeconds);

        // The decision is permanent history under the member of staff's name.
        var database = await factory.DatabaseAsync;
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'desktop_sign_in'
              AND AggregateId LIKE N'desktop:%'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded'
              AND Reason = N'http://127.0.0.1:{DesktopPorts[0]}'
            """));

        // The token is the member of staff's (R1300): the connector names
        // them, and a call is recorded under their name in Pegasus Desktop's
        // area, never read as the Automation Actor.
        using (var session = await PostMcpAsync(desktop, accessToken, ToolCallPayload(1, "pegasus_session_get", new { })))
        {
            var who = await ReadStructuredContentAsync(session);
            Assert.Equal(nameof(ActorKind.Staff), who.GetProperty("actorKind").GetString());
            Assert.Equal(staffId, who.GetProperty("staffId").GetGuid());
            Assert.Equal("desktop-user", who.GetProperty("userName").GetString());
            Assert.Equal(nameof(StaffRole.User), who.GetProperty("role").GetString());
        }

        using (var search = await PostMcpAsync(
            desktop, accessToken, ToolCallPayload(2, "pegasus_case_search", new { query = "desktop" })))
        {
            Assert.Empty((await ReadStructuredContentAsync(search)).GetProperty("items").EnumerateArray());
        }

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'pegasus_case_search'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM SecurityEvents WHERE ReasonCode = N'{AutomationMcp.DesktopAccessDenied}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM SecurityEvents WHERE ReasonCode LIKE N'automation[_]%'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE ActorKind = N'Automation'"));
    }

    /// <summary>
    /// RFC 8252 section 7.3, as OpenIddict applies it to a native client: a
    /// registered loopback URI that names no port admits any port, and one
    /// that names a port admits that port only. Anything that is not the
    /// registered loopback address, and a request without PKCE, is refused
    /// before the page.
    /// </summary>
    [Fact]
    public async Task TheLoopbackRedirectAndPkceAreCheckedBeforeTheSignInPage()
    {
        using (var factory = new IntakeWebApplicationFactory(TimeProvider.System))
        using (var host = WithDesktopSignIn(factory))
        using (var browser = CreateBrowser(host))
        {
            await CreateStaffAsync(host, "desktop-ports", StaffRole.Engineer);
            await SignInAsync(browser, "desktop-ports");
            var (_, challenge) = Pkce();

            foreach (var port in DesktopPorts)
            {
                await AssertSignInPageAsync(browser, AuthorizeUrl(challenge, LoopbackRedirectUri(port)));
            }

            foreach (var refused in new[]
            {
                "http://localhost:51793/callback",
                "http://127.0.0.1:51793/another",
                "http://192.168.0.10:51793/callback",
                "https://evil.example/callback"
            })
            {
                await AssertRefusedBeforeThePageAsync(browser, AuthorizeUrl(challenge, refused));
            }

            using var noPkce = await browser.GetAsync(
                AuthorizeUrl(challenge: null, LoopbackRedirectUri(DesktopPorts[0])));
            Assert.DoesNotContain(
                OperatorLabels.DesktopSignIn.Title,
                await noPkce.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
            if (noPkce.StatusCode == HttpStatusCode.Redirect)
            {
                Assert.Equal("invalid_request", ParseQuery(noPkce.Headers.Location!)["error"]);
            }
        }

        // A deployment may pin the ports instead: then only those are admitted.
        var pinned = string.Join(';', DesktopPorts.Select(LoopbackRedirectUri));
        using (var factory = new IntakeWebApplicationFactory(TimeProvider.System))
        using (var host = WithDesktopSignIn(factory, pinned))
        using (var browser = CreateBrowser(host))
        {
            await CreateStaffAsync(host, "desktop-pinned", StaffRole.User);
            await SignInAsync(browser, "desktop-pinned");
            var (_, challenge) = Pkce();

            foreach (var port in DesktopPorts)
            {
                await AssertSignInPageAsync(browser, AuthorizeUrl(challenge, LoopbackRedirectUri(port)));
            }

            await AssertRefusedBeforeThePageAsync(browser, AuthorizeUrl(challenge, LoopbackRedirectUri(50000)));
        }
    }

    /// <summary>
    /// R1298: a renewal keeps the sign-in, and a disable, a password reset, a
    /// forced sign-out and a role change each end it at the next renewal,
    /// because each moves the account's security stamp.
    /// </summary>
    [Fact]
    public async Task ASignInIsRenewedUntilTheAccountIsDisabledResetSignedOutOrGivenAnotherRole()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        using var desktop = CreateConnector(host);
        var refreshTokens = new Dictionary<string, (Guid StaffId, string RefreshToken)>(StringComparer.Ordinal);
        foreach (var (userName, role) in new[]
        {
            ("desktop-disabled", StaffRole.User),
            ("desktop-reset", StaffRole.Engineer),
            ("desktop-signed-out", StaffRole.Administrator),
            ("desktop-role-changed", StaffRole.User)
        })
        {
            var staffId = await CreateStaffAsync(host, userName, role);
            using var browser = CreateBrowser(host);
            await SignInAsync(browser, userName);
            using var tokens = await SignInFromDesktopAsync(browser, desktop);
            refreshTokens[userName] = (staffId, tokens.RootElement.GetProperty("refresh_token").GetString()!);
        }

        // Renewal: a new access token and a new refresh token, twice over,
        // with no page in between.
        var renewed = refreshTokens["desktop-disabled"].RefreshToken;
        for (var round = 0; round < 2; round++)
        {
            using var refresh = await RefreshAsync(desktop, renewed);
            var body = await refresh.Content.ReadAsStringAsync();
            Assert.True(refresh.IsSuccessStatusCode, body);
            using var json = JsonDocument.Parse(body);
            Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("access_token").GetString()));
            var next = json.RootElement.GetProperty("refresh_token").GetString()!;
            Assert.NotEqual(renewed, next);
            renewed = next;
        }

        refreshTokens["desktop-disabled"] = (refreshTokens["desktop-disabled"].StaffId, renewed);

        // Each Administrator command in its own scope, as each is its own request.
        var disabled = refreshTokens["desktop-disabled"].StaffId;
        var disabledVersion = await VersionAsync(host, disabled);
        await AdministerAsync<IDisableStaffAccount>(host, service => service.ExecuteAsync(
            new(Administrator, disabled, "Left the company", OperationKey(), disabledVersion),
            CancellationToken.None));
        var reset = refreshTokens["desktop-reset"].StaffId;
        var resetVersion = await VersionAsync(host, reset);
        await AdministerAsync<IResetStaffPassword>(host, service => service.ExecuteAsync(
            new(Administrator, reset, "Forgotten password", OperationKey(), resetVersion),
            CancellationToken.None));
        var signedOut = refreshTokens["desktop-signed-out"].StaffId;
        var signedOutVersion = await VersionAsync(host, signedOut);
        await AdministerAsync<IForceStaffLogout>(host, service => service.ExecuteAsync(
            new(Administrator, signedOut, "Lost laptop", OperationKey(), signedOutVersion),
            CancellationToken.None));
        var roleChanged = refreshTokens["desktop-role-changed"].StaffId;
        var roleChangedVersion = await VersionAsync(host, roleChanged);
        await AdministerAsync<IUpdateStaffAccountSettings>(host, service => service.ExecuteAsync(
            new(
                Administrator,
                roleChanged,
                StaffRole.Engineer,
                IsSignOffEngineer: false,
                PrintedName: null,
                Qualifications: null,
                Signature: null,
                IsDefaultSignOffEngineer: false,
                OperationKey(),
                roleChangedVersion),
            CancellationToken.None));

        foreach (var (userName, (_, refreshToken)) in refreshTokens)
        {
            using var refused = await RefreshAsync(desktop, refreshToken);
            var body = await refused.Content.ReadAsStringAsync();
            Assert.True(refused.StatusCode == HttpStatusCode.BadRequest, $"{userName}: {(int)refused.StatusCode} {body}");
            Assert.Contains("invalid_grant", body, StringComparison.Ordinal);
        }

        // Each refusal is recorded as the desktop client's, not as Automation activity.
        var database = await factory.DatabaseAsync;
        Assert.Equal(refreshTokens.Count, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'{AutomationMcp.DesktopTokenRejected}'
              AND Outcome = N'Denied'
              AND SubjectId = N'{AutomationMcp.DesktopClientId}'
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM SecurityEvents WHERE ReasonCode LIKE N'automation[_]%'"));
    }

    /// <summary>
    /// R1298: the sign-in lasts eight hours from the sign-in. A renewal late
    /// in it does not extend it, and the last access token ends with it.
    /// </summary>
    [Fact]
    public async Task ASignInLastsEightHoursAndIsNotExtendedByUse()
    {
        var clock = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = new IntakeWebApplicationFactory(clock);
        using var host = WithDesktopSignIn(factory);
        await CreateStaffAsync(host, "desktop-eight-hours", StaffRole.User);
        using var browser = CreateBrowser(host);
        using var desktop = CreateConnector(host);
        await SignInAsync(browser, "desktop-eight-hours");
        using var tokens = await SignInFromDesktopAsync(browser, desktop);
        var refreshToken = tokens.RootElement.GetProperty("refresh_token").GetString()!;

        // Five minutes before the end: renewed, with an access token that
        // lasts only those five minutes.
        clock.Advance(AutomationMcp.DesktopSignInLifetime - TimeSpan.FromMinutes(5));
        using (var late = await RefreshAsync(desktop, refreshToken))
        {
            var body = await late.Content.ReadAsStringAsync();
            Assert.True(late.IsSuccessStatusCode, body);
            using var json = JsonDocument.Parse(body);
            Assert.InRange(json.RootElement.GetProperty("expires_in").GetInt32(), 1, 300);
            refreshToken = json.RootElement.GetProperty("refresh_token").GetString()!;
        }

        // Past the end: the renewed refresh token kept the first one's end.
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        using var ended = await RefreshAsync(desktop, refreshToken);
        var endedBody = await ended.Content.ReadAsStringAsync();
        Assert.True(ended.StatusCode == HttpStatusCode.BadRequest, $"{(int)ended.StatusCode} {endedBody}");
        Assert.Contains("invalid_grant", endedBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// The application's own sign-out revokes its refresh token (RFC 7009).
    /// The Automation client holds no permission for the endpoint.
    /// </summary>
    [Fact]
    public async Task TheApplicationsSignOutRevokesItsRefreshToken()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        await CreateStaffAsync(host, "desktop-sign-out", StaffRole.User);
        using var browser = CreateBrowser(host);
        using var desktop = CreateConnector(host);
        await SignInAsync(browser, "desktop-sign-out");
        using var tokens = await SignInFromDesktopAsync(browser, desktop);
        var refreshToken = tokens.RootElement.GetProperty("refresh_token").GetString()!;

        using (var foreign = await desktop.PostAsync(
            AutomationMcp.RevocationEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = refreshToken,
                ["token_type_hint"] = "refresh_token",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            })))
        {
            Assert.False(foreign.IsSuccessStatusCode);
            Assert.Contains(
                "unauthorized_client",
                await foreign.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        using (var revoked = await desktop.PostAsync(
            AutomationMcp.RevocationEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = refreshToken,
                ["token_type_hint"] = "refresh_token",
                ["client_id"] = AutomationMcp.DesktopClientId
            })))
        {
            Assert.True(
                revoked.StatusCode == HttpStatusCode.OK,
                $"{(int)revoked.StatusCode} {await revoked.Content.ReadAsStringAsync()}");
        }

        using var refused = await RefreshAsync(desktop, refreshToken);
        var body = await refused.Content.ReadAsStringAsync();
        Assert.True(refused.StatusCode == HttpStatusCode.BadRequest, $"{(int)refused.StatusCode} {body}");
        Assert.Contains("invalid_grant", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Refuse sends <c>access_denied</c> to the application and records the
    /// refusal; no code leaves the server.
    /// </summary>
    [Fact]
    public async Task RefuseReturnsAccessDeniedAndIsRecorded()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var staffId = await CreateStaffAsync(host, "desktop-refuses", StaffRole.User);
        using var browser = CreateBrowser(host);
        await SignInAsync(browser, "desktop-refuses");
        var (_, challenge) = Pkce();

        using var page = await browser.GetAsync(AuthorizeUrl(challenge, LoopbackRedirectUri(DesktopPorts[1])));
        var html = await page.Content.ReadAsStringAsync();
        using var refused = await browser.PostAsync(AuthorizeHandlerUrl("Deny"), SignInForm(html));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var callback = ParseQuery(refused.Headers.Location!);
        Assert.Equal("access_denied", callback["error"]);
        Assert.Equal(State, callback["state"]);
        Assert.False(callback.ContainsKey("code"));

        var database = await factory.DatabaseAsync;
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'desktop_sign_in_refused'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Denied'
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE EventKind = N'desktop_sign_in'"));
    }

    /// <summary>
    /// An account that must change its password reaches no page but the
    /// password change, this one included.
    /// </summary>
    [Fact]
    public async Task AnAccountThatMustChangeItsPasswordIsSentToChangeItFirst()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        await CreateStaffAsync(host, "desktop-must-change", StaffRole.User, mustChangePassword: true);
        using var browser = CreateBrowser(host);
        await SignInAsync(browser, "desktop-must-change");
        var (_, challenge) = Pkce();

        using var page = await browser.GetAsync(AuthorizeUrl(challenge, LoopbackRedirectUri(DesktopPorts[0])));
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal(
            "/Account/PasswordChange",
            new Uri(browser.BaseAddress!, page.Headers.Location!).AbsolutePath);
    }

    /// <summary>
    /// R1297: the sign-in is offered only where its configuration is present.
    /// Without it there is no such client, whoever asks.
    /// </summary>
    [Fact]
    public async Task WithoutItsConfigurationThereIsNoDesktopClient()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithAutomationMcp(factory);
        // The offline host signs every browser request in as the Administrator.
        using var browser = CreateBrowser(host);
        using var desktop = CreateConnector(host);
        var (_, challenge) = Pkce();

        await AssertRefusedBeforeThePageAsync(
            browser,
            AuthorizeUrl(challenge, LoopbackRedirectUri(DesktopPorts[0])));

        using var token = await RefreshAsync(desktop, "not-a-token");
        Assert.False(token.IsSuccessStatusCode);
        Assert.Contains("invalid_client", await token.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM OpenIddictApplications WHERE ClientId = N'{AutomationMcp.DesktopClientId}'"));
    }

    /// <summary>
    /// A registration left by an earlier configuration is rewritten to permit
    /// nothing: the Web's database role may not delete it.
    /// </summary>
    [Fact]
    public async Task RemovingItsConfigurationLeavesARegistrationThatPermitsNothing()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var database = await factory.DatabaseAsync;
        var registered =
            $"""
            SELECT COUNT(*) FROM OpenIddictApplications
            WHERE ClientId = N'{AutomationMcp.DesktopClientId}'
              AND ClientType = N'public'
              AND ApplicationType = N'native'
              AND ClientSecret IS NULL
            """;

        using (var configured = WithDesktopSignIn(factory))
        {
            await using var scope = configured.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AutomationClientRegistry>()
                .EnsureRegisteredAsync(CancellationToken.None);
            Assert.Equal(1, await database.ScalarAsync<int>(
                registered + " AND Permissions LIKE N'%ept:revocation%' AND RedirectUris LIKE N'%127.0.0.1%'"));
        }

        using var unconfigured = WithAutomationMcp(factory);
        await using (var scope = unconfigured.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutomationClientRegistry>()
                .EnsureRegisteredAsync(CancellationToken.None);
        }

        Assert.Equal(1, await database.ScalarAsync<int>(
            registered + " AND Permissions IS NULL AND RedirectUris IS NULL"));
        using var browser = CreateBrowser(unconfigured);
        var (_, challenge) = Pkce();
        await AssertRefusedBeforeThePageAsync(
            browser,
            AuthorizeUrl(challenge, LoopbackRedirectUri(DesktopPorts[0])));
    }

    private static async Task AssertSignInPageAsync(HttpClient browser, string authorizeUrl)
    {
        using var page = await browser.GetAsync(authorizeUrl);
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.StatusCode == HttpStatusCode.OK, $"{authorizeUrl}: {(int)page.StatusCode} {html}");
        Assert.Contains(OperatorLabels.DesktopSignIn.Title, html, StringComparison.Ordinal);
    }

    // OpenIddict refuses to redirect to an address it does not hold for the
    // client: no page, and no code ever leaves the server.
    private static async Task AssertRefusedBeforeThePageAsync(HttpClient browser, string authorizeUrl)
    {
        using var refused = await browser.GetAsync(authorizeUrl);
        Assert.True(
            refused.StatusCode != HttpStatusCode.Redirect && refused.StatusCode != HttpStatusCode.OK,
            $"{authorizeUrl}: {(int)refused.StatusCode}");
        Assert.DoesNotContain(
            OperatorLabels.DesktopSignIn.Title,
            await refused.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }
}
