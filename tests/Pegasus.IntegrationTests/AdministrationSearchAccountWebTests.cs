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

[Collection(LocalDbFixtureDefinition.Name)]
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
                     "/Search",
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

        using var shell = await client.GetAsync("/");
        var shellHtml = await shell.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/Search\"", shellHtml, StringComparison.Ordinal);
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
        using var factory = new IntakeWebApplicationFactory();
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
                     "/Administration/Mailboxes"
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
    }

    [Fact]
    public async Task SearchHasDistinctEmptyNoMatchAndValidationErrorStates()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var emptyResponse = await client.GetAsync("/Search");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Enter a search query", emptyHtml, StringComparison.Ordinal);

        const string exactQuery = "QDOS-search-no-match";
        using var noMatchResponse = await client.GetAsync($"/Search?q={exactQuery}");
        var noMatchHtml = await noMatchResponse.Content.ReadAsStringAsync();
        Assert.Contains("No matching cases", noMatchHtml, StringComparison.Ordinal);
        Assert.Contains(exactQuery, noMatchHtml, StringComparison.Ordinal);
        Assert.Equal($"?q={exactQuery}", noMatchResponse.RequestMessage?.RequestUri?.Query);

        var overlongQuery = new string('q', 301);
        using var invalidResponse = await client.GetAsync(
            $"/Search?q={Uri.EscapeDataString(overlongQuery)}");
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
        Assert.Contains("cannot exceed 300 characters", invalidHtml, StringComparison.Ordinal);
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
