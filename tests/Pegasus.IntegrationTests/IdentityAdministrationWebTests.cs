using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Administration;
using Pegasus.Web.Pages.Administration.Accounts;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class IdentityAdministrationWebTests
{
    [Fact]
    public async Task IdentityAdministrationRoutesRenderNamedAntiforgeryCallersWithoutSecrets()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[]
                 {
                     "/Administration/Accounts",
                     "/Administration/Access",
                     "/Administration/Roles",
                     "/Administration/Clients"
                 })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        }

        using var clients = await client.GetAsync("/Administration/Clients");
        var clientsHtml = await clients.Content.ReadAsStringAsync();
        Assert.Contains(StaffMcpClientContract.ReadScope, clientsHtml, StringComparison.Ordinal);
        Assert.Contains(StaffMcpClientContract.WriteScope, clientsHtml, StringComparison.Ordinal);
        Assert.Contains("without a client secret", clientsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"ClientSecret\"", clientsHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IdentityAdministrationRoutesDenyARequestAfterCurrentRoleRemoval()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True((await userManager.RemoveFromRoleAsync(
                user,
                StaffRoleNames.Administrator)).Succeeded);
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        foreach (var route in new[]
                 {
                     "/Administration/Accounts",
                     "/Administration/Access",
                     "/Administration/Roles",
                     "/Administration/Clients"
                 })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public void IdentityAdministrationPageModelsDeclareAdministratorPolicy()
    {
        foreach (var pageModel in new[]
                 {
                     typeof(Pegasus.Web.Pages.Administration.Accounts.IndexModel),
                     typeof(EditModel),
                     typeof(Pegasus.Web.Pages.Administration.Access.IndexModel),
                     typeof(Pegasus.Web.Pages.Administration.Roles.IndexModel),
                     typeof(ClientsModel)
                 })
        {
            Assert.Equal(
                StaffRoleNames.Administrator,
                pageModel.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        }
    }
}
