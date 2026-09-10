using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
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
        Assert.Contains("Save settings", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Save account settings", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("record why it was made", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("create-reason", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsSaveRequiresAndRecordsAConsequentialReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>();
        var accounts = await queries.ListAsync(0, ListStaffAccounts.MaximumPageSize, default);
        var administrator = accounts.Accounts.Single(item =>
            item.UserName == DevelopmentOfflineIdentity.UserName);

        using var editResponse = await client.GetAsync(
            AreaRoute + "?editStaffId=" + administrator.Id + "&expectedVersion=" + administrator.Version);
        var editHtml = await editResponse.Content.ReadAsStringAsync();

        const string reason = "Confirming settings after quarterly access review";
        var fields = new Dictionary<string, string>
        {
            ["staffId"] = administrator.Id.ToString("D"),
            ["role"] = administrator.Role.ToString(),
            ["isSignOffEngineer"] = administrator.SignOff.IsSignOffEngineer ? "true" : "false",
            ["printedName"] = administrator.SignOff.PrintedName ?? string.Empty,
            ["qualifications"] = administrator.SignOff.Qualifications ?? string.Empty,
            ["expectedVersion"] = Field(editHtml, "expectedVersion"),
            ["editLeaseToken"] = Field(editHtml, "editLeaseToken"),
            ["operationKey"] = Field(editHtml, "operationKey"),
            ["__RequestVerificationToken"] = Field(editHtml, "__RequestVerificationToken")
        };
        if (administrator.SignOff.IsDefault)
        {
            fields["isDefaultSignOffEngineer"] = "true";
        }

        // Without a reason the save is refused and nothing is persisted.
        using var missingReason = await client.PostAsync(
            AreaRoute + "?handler=Settings", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, missingReason.StatusCode);
        Assert.Contains("Enter a reason.", await missingReason.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        fields["reason"] = reason;
        using var saved = await client.PostAsync(
            AreaRoute + "?handler=Settings", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var recorded = await context.ActionHistory
            .Where(item => item.AggregateId == administrator.Id.ToString("D")
                && item.EventKind == "staff_account_settings_updated")
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(recorded);
        Assert.Equal(reason, recorded!.Reason);
    }

    private static string Field(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, "Missing field " + name);
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }
}
