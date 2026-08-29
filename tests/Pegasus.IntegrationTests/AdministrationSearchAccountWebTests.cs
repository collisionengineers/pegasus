using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Administration;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AdministrationSearchAccountWebTests
{
    [Fact]
    public async Task CanonicalAdministrationSearchAndPasswordRoutesRenderRealCallers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[]
                 {
                     "/Administration/Configuration",
                     "/Administration/Mailboxes",
                     "/Administration/MailCategories",
                     "/Account/PasswordChange"
                 })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<main", html, StringComparison.OrdinalIgnoreCase);
        }

        using var administration = await client.GetAsync("/Administration");
        var administrationHtml = await administration.Content.ReadAsStringAsync();
        Assert.Contains("/Administration/Configuration", administrationHtml, StringComparison.Ordinal);
        Assert.Contains("/Administration/Mailboxes", administrationHtml, StringComparison.Ordinal);
        Assert.Contains("/Administration/MailCategories", administrationHtml, StringComparison.Ordinal);

        using var shell = await client.GetAsync("/");
        var shellHtml = await shell.Content.ReadAsStringAsync();

        Assert.Contains("href=\"/Account/PasswordChange\"", shellHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministrationAndPasswordFormsRenderAntiforgeryTokens()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[]
                 {
                     "/Administration/Configuration",
                     "/Administration/Mailboxes",
                     "/Administration/MailCategories",
                     "/Account/PasswordChange"
                 })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AdministrationRoutesDenyARequestWithoutCurrentAdministratorRole()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: false);
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True(await userManager.RemoveFromRoleAsync(
                user,
                StaffRoleNames.Administrator) is { Succeeded: true });
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        foreach (var route in new[]
                 {
                     "/Administration/Configuration",
                     "/Administration/Mailboxes",
                     "/Administration/MailCategories"
                 })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public void AdministrationPageModelsDeclareAdministratorPolicy()
    {
        Assert.Equal(
            StaffRoleNames.Administrator,
            typeof(ConfigurationModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            StaffRoleNames.Administrator,
            typeof(MailboxesModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            StaffRoleNames.Administrator,
            typeof(MailCategoriesModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public async Task OldCasesSearchLinksRedirectToSearchWithTheirValuesIntact()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // EPIC-011 moved the case search to /Search and the workflow tabs to
        // /Cases. A /Cases link that carries a search-only parameter is an
        // old search bookmark and lands on its results, values intact.
        const string keyword = "QDOS-search-no-match";
        using var oldLink = await client.GetAsync($"/Cases?query={keyword}");
        Assert.Equal(HttpStatusCode.MovedPermanently, oldLink.StatusCode);
        Assert.Equal(
            $"/Search?query={keyword}",
            oldLink.Headers.Location?.OriginalString ?? string.Empty);

        using var search = await client.GetAsync($"/Search?query={keyword}");
        search.EnsureSuccessStatusCode();
        var html = await search.Content.ReadAsStringAsync();
        Assert.Contains("No cases match these filters.", html, StringComparison.Ordinal);

        // A real bookmark carries a whole filter set, including the two
        // parameters the ported grid no longer draws. Every value survives the
        // move byte for byte, in its original order, and the page it lands on
        // accepts all of them.
        const string wholeFilterSet =
            "?case=QDOS3100042&registration=AB12CDE&claimant=Claimant&claimNumber=CLM42"
            + "&principal=QDOS&state=Review&receivedDate=2031-05-01"
            + "&instructionDate=2031-05-02&fromDate=2031-04-01&toDate=2031-05-31"
            + "&origin=Email&query=" + keyword + "&page=2";
        using var wholeBookmark = await client.GetAsync("/Cases" + wholeFilterSet);
        Assert.Equal(HttpStatusCode.MovedPermanently, wholeBookmark.StatusCode);
        Assert.Equal(
            "/Search" + wholeFilterSet,
            wholeBookmark.Headers.Location?.OriginalString ?? string.Empty);

        using var landed = await client.GetAsync("/Search" + wholeFilterSet);
        landed.EnsureSuccessStatusCode();
        var landedHtml = await landed.Content.ReadAsStringAsync();
        foreach (var (field, value) in new[]
                 {
                     ("search-query", keyword), ("search-registration", "AB12CDE"),
                     ("search-claimant", "Claimant"), ("search-claim-number", "CLM42"),
                     ("search-principal", "QDOS"), ("search-from-date", "2031-04-01"),
                     ("search-to-date", "2031-05-31"), ("search-origin", "Email")
                 })
        {
            Assert.Matches(
                $"id=\"{field}\"[^>]*value=\"{Regex.Escape(value)}\"",
                landedHtml);
        }
    }

    [Fact]
    public async Task ObsoleteChangePasswordRouteIsAbsent()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Account/ChangePassword");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
