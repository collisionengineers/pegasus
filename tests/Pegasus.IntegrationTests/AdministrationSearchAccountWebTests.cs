using System.Net;
using System.Reflection;
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
    public async Task SearchIsAbsorbedByCasesAndItsRouteCarriesTheKeywordThrough()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // Search and Cases ran the identical Core query and differed only in
        // which filters they exposed, so two nav items led to one capability —
        // and the two screens disagreed about what a query failure meant.
        // Cases absorbs it; the route redirects so bookmarks land on results.
        using var bare = await client.GetAsync("/Search");
        Assert.Equal(HttpStatusCode.MovedPermanently, bare.StatusCode);
        Assert.Contains(
            "/Cases",
            bare.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.Ordinal);

        const string keyword = "QDOS-search-no-match";
        using var withKeyword = await client.GetAsync($"/Search?query={keyword}");
        Assert.Equal(HttpStatusCode.MovedPermanently, withKeyword.StatusCode);
        Assert.Contains(
            keyword,
            withKeyword.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.Ordinal);

        using var cases = await client.GetAsync($"/Cases?query={keyword}");
        cases.EnsureSuccessStatusCode();
        var html = await cases.Content.ReadAsStringAsync();
        Assert.Contains("No cases match these filters.", html, StringComparison.Ordinal);
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
