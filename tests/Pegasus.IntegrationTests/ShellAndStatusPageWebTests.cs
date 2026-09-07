using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Operations;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The shell contract: what every screen shares, and what the screens that are
/// not a place in the application deliberately do not share.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ShellAndStatusPageWebTests
{
    [Fact]
    public async Task NavigationSpeaksTheBusinessVocabularyAndNeverShowsAnInertItem()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(">Work Centre<", html, StringComparison.Ordinal);
        Assert.Contains(">Inbox<", html, StringComparison.Ordinal);
        Assert.Contains(">Cases<", html, StringComparison.Ordinal);
        Assert.Contains(">Search<", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Primary\"", html, StringComparison.Ordinal);

        // "intake" is internal code vocabulary; the office does not have intake
        // queues, because intake is automatic and nothing queues.
        Assert.DoesNotContain(">Intake<", html, StringComparison.Ordinal);

        // A capability a deployment has not composed is absent, not a disabled
        // nav span that says the product is broken.
        Assert.DoesNotContain("Intake unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain("nav-link--unavailable", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownRecordUrlRendersTheDesignedNotFoundPageRatherThanARawBrowserError()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Triage/{Guid.NewGuid():D}");

        // The status code is still the truth of the exchange; only the body
        // changes, from Chrome's default page to a worded one.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("We could not find that page", html, StringComparison.Ordinal);
        Assert.Contains("auth-card", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignOutRedirectsToAConfirmationThatTheSessionEndedRatherThanABareSignInForm()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // The navigation posts sign-out directly, so the unstyled interstitial
        // that used to answer this route was never reached; what was missing is
        // the confirmation. It is a one-time state of the sign-in page, not a
        // page of its own, so a bookmark cannot assert a sign-out that did not
        // just happen.
        var signOutPage = await client.GetStringAsync("/Search");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryValue(signOutPage)
        });
        using var response = await client.PostAsync("/Account/SignOut", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "signedOut=True",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string AntiforgeryValue(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var nameIndex = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(nameIndex >= 0, "The page rendered no antiforgery token.");

        const string valueMarker = "value=\"";
        var valueIndex = html.IndexOf(valueMarker, nameIndex, StringComparison.Ordinal) + valueMarker.Length;
        var end = html.IndexOf('"', valueIndex);
        return html[valueIndex..end];
    }

    [Fact]
    public async Task ScreensThatAreNotAPlaceInTheApplicationRenderWithoutTheStaffNavigation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[] { "/Account/AccessDenied", "/status/404" })
        {
            using var response = await client.GetAsync(route);
            var html = await response.Content.ReadAsStringAsync();

            // Around a sign-in form the navigation shows an unauthenticated
            // visitor the internal structure of the product; around a refusal
            // it offers a menu of destinations the page has just declined.
            Assert.DoesNotContain("aria-label=\"Primary\"", html, StringComparison.Ordinal);
            Assert.Contains("auth-card", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task StatusCodePagesDoNotHijackTheMachineSurfaces()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // Health probes and the version endpoint answer programs. A
        // re-executed HTML card in place of their body would break the caller
        // rather than help anyone read it.
        using var live = await client.GetAsync("/health/live");
        Assert.DoesNotContain(
            "auth-card",
            await live.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var version = await client.GetAsync("/diagnostics/version");
        version.EnsureSuccessStatusCode();
        Assert.Contains(
            "version",
            await version.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// C08: the required <see cref="IGetAttentionRows"/> dependency supplies
    /// the notifications menu through ordinary production composition.
    /// </summary>
    [Fact]
    public async Task NotificationsMenuShowsAttentionRowsOnceTheQueryIsRegistered()
    {
        var rows = new[]
        {
            new NeedsAttentionItem(
                NeedsAttentionKind.Triage, Guid.NewGuid(), "T/2031/041", "AB12 CDE",
                Detail: null, Reason: "open", NeedsAttentionPriority.High,
                Owner: null, Due: null, LastOutcome: null, Source: null, Attempts: null)
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetAttentionRows>();
                services.AddSingleton<IGetAttentionRows>(new StubAttentionRows(rows));
            }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        // Not Work Centre ("/") — that page supplies its own rows and never
        // asks the filter for this query.
        using var response = await client.GetAsync("/Search");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("AB12 CDE", html, StringComparison.Ordinal);
    }

    private sealed class StubAttentionRows(IReadOnlyList<NeedsAttentionItem> rows) : IGetAttentionRows
    {
        public Task<IReadOnlyList<NeedsAttentionItem>> ExecuteAsync(
            Pegasus.Core.Identity.ActionActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows);
    }
}
