using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffAccountsAndRolesWebTests
{
    private const string AreaRoute = "/Administration/Accounts";

    [Fact]
    public async Task AccountsAreaUsesOneRoleAndTheCompactSettingsJourney()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync(AreaRoute);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Staff accounts", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Role</th>", html, StringComparison.Ordinal);
        Assert.Contains("Sign-off", html, StringComparison.Ordinal);
        Assert.Contains("Create staff account", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirm/Disable", html, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>();
        var accounts = await queries.ListAsync(0, ListStaffAccounts.MaximumPageSize, default);
        var administrator = accounts.Accounts.Single(item =>
            item.UserName == DevelopmentOfflineIdentity.UserName);
        Assert.Equal(StaffRole.Administrator, administrator.Role);

        using var settings = await client.GetAsync(
            AreaRoute + "?editStaffId=" + administrator.Id + "&expectedVersion=" + administrator.Version);
        var settingsHtml = await settings.Content.ReadAsStringAsync();
        Assert.Contains("Save account settings", settingsHtml, StringComparison.Ordinal);
        Assert.Contains("record why it was made", settingsHtml, StringComparison.Ordinal);
    }
}
