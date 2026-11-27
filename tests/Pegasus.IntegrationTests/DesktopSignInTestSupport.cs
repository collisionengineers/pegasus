using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The sign-in from Pegasus Desktop as the application runs it (ADR-0092),
/// for the tests of the sign-in itself and of what a signed-in member of
/// staff then does through the connector: the host that offers it, a staff
/// account of a named role, that person's own sign-in to Pegasus in the
/// browser, and the page, the code and its exchange.
/// </summary>
internal static class DesktopSignInTestSupport
{
    public const string State = "state-desktop-7f3a";
    public const string Password = "correct horse battery staple";
    public const string AdministratorPassword = "Local-Test-Instance-2026!";

    // What a deployment that offers the sign-in configures: no port, so the
    // application may listen on whichever of its ports is free.
    public const string RegisteredRedirectUri = "http://127.0.0.1/callback";

    // The ports Pegasus Desktop listens on, in the order it tries them.
    public static readonly int[] DesktopPorts = [51793, 56427, 62031];

    public static readonly ActionActor Administrator = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

    public static WebApplicationFactory<Program> WithDesktopSignIn(
        IntakeWebApplicationFactory factory,
        string desktopRedirectUris = RegisteredRedirectUri) =>
        WithAutomationMcp(factory).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:PasswordSignIn", "true");
            builder.UseSetting("DevelopmentOffline:AdministratorPassword", AdministratorPassword);
            builder.UseSetting("AutomationMcp:DesktopRedirectUris", desktopRedirectUris);
        });

    public static string LoopbackRedirectUri(int port) => $"http://127.0.0.1:{port}/callback";

    public static string AuthorizeUrl(string? challenge, string redirectUri)
    {
        var query = new List<string>
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(AutomationMcp.DesktopClientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"state={State}",
            $"resource={Uri.EscapeDataString("http://localhost" + McpServer)}"
        };
        if (challenge is not null)
        {
            query.Add($"code_challenge={challenge}");
            query.Add("code_challenge_method=S256");
        }

        return AutomationMcp.AuthorizationEndpointPath + "?" + string.Join('&', query);
    }

    public static string AuthorizeHandlerUrl(string handler) =>
        AutomationMcp.AuthorizationEndpointPath + "?handler=" + handler;

    public static FormUrlEncodedContent SignInForm(string html) =>
        RenderedForm(html, "data-desktop-sign-in", "</form>");

    public static string OperationKey() => Guid.NewGuid().ToString("N");

    public static async Task<Guid> CreateStaffAsync(
        WebApplicationFactory<Program> host,
        string userName,
        StaffRole role,
        bool mustChangePassword = false)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = true,
            MustChangePassword = mustChangePassword,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
        Assert.True((await userManager.AddToRoleAsync(user, role switch
        {
            StaffRole.Administrator => StaffRoleNames.Administrator,
            StaffRole.Engineer => StaffRoleNames.Engineer,
            _ => StaffRoleNames.User
        })).Succeeded);
        return user.Id;
    }

    public static async Task AdministerAsync<TService>(
        WebApplicationFactory<Program> host,
        Func<TService, Task> command)
        where TService : notnull
    {
        await using var scope = host.Services.CreateAsyncScope();
        await command(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public static async Task<long> VersionAsync(WebApplicationFactory<Program> host, Guid staffId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Users.AsNoTracking()
            .Where(user => user.Id == staffId)
            .Select(user => user.Version)
            .SingleAsync();
    }

    // The member of staff's own sign-in to Pegasus, in the browser.
    public static async Task SignInAsync(HttpClient browser, string userName)
    {
        using var signInPage = await browser.GetAsync("/Account/SignIn");
        Assert.Equal(HttpStatusCode.OK, signInPage.StatusCode);
        using var signedIn = await browser.PostAsync(
            "/Account/SignIn",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = CaseWebTestSupport.AntiforgeryValue(
                    await signInPage.Content.ReadAsStringAsync()),
                ["UserName"] = userName,
                ["Password"] = Password,
                ["ReturnUrl"] = "/"
            }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Contains(
            signedIn.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
    }

    /// <summary>
    /// The whole sign-in as the application runs it: the page, Sign in, and
    /// the code exchanged with its verifier. Answers the token response.
    /// </summary>
    public static async Task<JsonDocument> SignInFromDesktopAsync(HttpClient browser, HttpClient desktop)
    {
        var redirectUri = LoopbackRedirectUri(DesktopPorts[0]);
        var (verifier, challenge) = Pkce();
        using var page = await browser.GetAsync(AuthorizeUrl(challenge, redirectUri));
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.StatusCode == HttpStatusCode.OK, $"{(int)page.StatusCode}: {html}");
        using var approved = await browser.PostAsync(AuthorizeHandlerUrl("Accept"), SignInForm(html));
        Assert.True(
            approved.StatusCode == HttpStatusCode.Redirect,
            $"{(int)approved.StatusCode}: {await approved.Content.ReadAsStringAsync()}");
        using var exchanged = await desktop.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = ParseQuery(approved.Headers.Location!)["code"]!,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = AutomationMcp.DesktopClientId
            }));
        var body = await exchanged.Content.ReadAsStringAsync();
        Assert.True(exchanged.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// A new member of staff of <paramref name="role"/>, signed in to Pegasus
    /// in a browser of their own and then from Pegasus Desktop: their id and
    /// the access token the application holds for them.
    /// </summary>
    public static async Task<(Guid StaffId, string AccessToken)> SignedInStaffAsync(
        WebApplicationFactory<Program> host,
        HttpClient desktop,
        string userName,
        StaffRole role)
    {
        var staffId = await CreateStaffAsync(host, userName, role);
        using var browser = CreateBrowser(host);
        await SignInAsync(browser, userName);
        using var tokens = await SignInFromDesktopAsync(browser, desktop);
        return (staffId, tokens.RootElement.GetProperty("access_token").GetString()!);
    }

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient desktop, string refreshToken) =>
        desktop.PostAsync(
            AutomationMcp.TokenEndpointPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = AutomationMcp.DesktopClientId
            }));
}
