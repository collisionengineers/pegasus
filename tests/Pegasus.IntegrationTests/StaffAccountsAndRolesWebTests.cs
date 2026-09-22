using System.Net;
using System.Net.Http.Headers;
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
    public async Task SettingsSavePostsDirectlyAndRecordsHistoryWithoutAReason()
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

        var fields = new Dictionary<string, string>
        {
            ["staffId"] = administrator.Id.ToString("D"),
            ["role"] = administrator.Role.ToString(),
            ["isSignOffEngineer"] = administrator.SignOff.IsSignOffEngineer ? "true" : "false",
            ["printedName"] = administrator.SignOff.PrintedName ?? string.Empty,
            ["qualifications"] = administrator.SignOff.Qualifications ?? string.Empty,
            ["expectedVersion"] = Field(editHtml, "expectedVersion"),
            ["operationKey"] = Field(editHtml, "operationKey"),
            ["__RequestVerificationToken"] = Field(editHtml, "__RequestVerificationToken")
        };
        if (administrator.SignOff.IsDefault)
        {
            fields["isDefaultSignOffEngineer"] = "true";
        }

        // Save posts on the click: no confirmation step and no reason field.
        Assert.DoesNotContain("name=\"reason\"", editHtml, StringComparison.Ordinal);
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
        Assert.Null(recorded!.Reason);
    }

    [Fact]
    public async Task SettingsSaveRejectsAStaleAccountVersion()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = await scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>()
            .ListAsync(0, ListStaffAccounts.MaximumPageSize, default);
        var administrator = accounts.Accounts.Single(item =>
            item.UserName == DevelopmentOfflineIdentity.UserName);
        var path = AreaRoute + "?editStaffId=" + administrator.Id
            + "&expectedVersion=" + administrator.Version;
        using var editResponse = await client.GetAsync(path);
        var editHtml = await editResponse.Content.ReadAsStringAsync();
        var refused = await client.PostAsync(
            AreaRoute + "?handler=Settings",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["staffId"] = administrator.Id.ToString("D"),
                ["role"] = administrator.Role.ToString(),
                ["isSignOffEngineer"] = administrator.SignOff.IsSignOffEngineer ? "true" : "false",
                ["printedName"] = administrator.SignOff.PrintedName ?? string.Empty,
                ["qualifications"] = administrator.SignOff.Qualifications ?? string.Empty,
                ["expectedVersion"] = (administrator.Version + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["operationKey"] = Field(editHtml, "operationKey"),
                ["__RequestVerificationToken"] = Field(editHtml, "__RequestVerificationToken")
            }));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("The staff account changed. Reload and try again.",
            await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsSaveCanMakeAUserTheDefaultSignOffEngineer()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>();
        var administrator = (await queries.ListAsync(0, ListStaffAccounts.MaximumPageSize, default))
            .Accounts.Single(item => item.UserName == DevelopmentOfflineIdentity.UserName);
        var created = await scope.ServiceProvider.GetRequiredService<ICreateStaffAccount>()
            .ExecuteAsync(
                new(
                    ActionActor.Staff(administrator.Id, [administrator.Role]),
                    "user-sign-off-profile",
                    "Tempor4ryPassword!",
                    Guid.NewGuid().ToString("D")),
                default);
        Assert.Equal(StaffRole.User, created.Account.Role);

        var path = AreaRoute + "?editStaffId=" + created.Account.Id + "&expectedVersion=" + created.Account.Version;
        using var edit = await client.GetAsync(path);
        var html = await edit.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in new[]
        {
            ("staffId", created.Account.Id.ToString("D")),
            ("role", StaffRole.User.ToString()),
            ("isSignOffEngineer", "true"),
            ("printedName", "User Signatory"),
            ("qualifications", "MEng"),
            ("isDefaultSignOffEngineer", "true"),
            ("expectedVersion", Field(html, "expectedVersion")),
            ("operationKey", Field(html, "operationKey")),
            ("__RequestVerificationToken", Field(html, "__RequestVerificationToken"))
        })
        {
            form.Add(new StringContent(value), name);
        }

        var signature = new ByteArrayContent([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        signature.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(signature, "signature", "signature.png");
        using var saved = await client.PostAsync(AreaRoute + "?handler=Settings", form);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var updated = await queries.GetAsync(created.Account.Id, default);
        Assert.NotNull(updated);
        Assert.Equal(StaffRole.User, updated!.Role);
        Assert.True(updated.SignOff.IsSignOffEngineer);
        Assert.True(updated.SignOff.IsDefault);
        Assert.True(updated.SignOff.HasSignature);
    }

    // The settings dialog auto-opens (data-dialog-open-on-load) and carries
    // Disable/Force logout/Reset password as direct row forms: no
    // confirmation dialog, no reason. Delete confirms in its own native dialog
    // because the row is removed. Disable posts on the click and its history
    // row records no reason.
    [Fact]
    public async Task SettingsDialogPostsAccountActionsDirectly()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>();
        var accounts = await queries.ListAsync(0, ListStaffAccounts.MaximumPageSize, default);
        var administrator = accounts.Accounts.Single(item =>
            item.UserName == DevelopmentOfflineIdentity.UserName);

        var createStaffAccount = scope.ServiceProvider.GetRequiredService<ICreateStaffAccount>();
        var created = await createStaffAccount.ExecuteAsync(
            new CreateStaffAccountRequest(
                ActionActor.Staff(administrator.Id, [administrator.Role]),
                "dialog-stack-check",
                "Tempor4ryPassword!",
                Guid.NewGuid().ToString("D")),
            default);
        var staffId = created.Account.Id.ToString("D");
        var settingsId = "settings-" + staffId;

        using var response = await client.GetAsync(
            AreaRoute + "?editStaffId=" + created.Account.Id + "&expectedVersion=" + created.Account.Version);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "data-dialog=\"" + settingsId + "\" data-dialog-open-on-load=\"true\"",
            html,
            StringComparison.Ordinal);

        foreach (var handler in new[] { "Disable", "Delete", "ForceLogout", "ResetPassword" })
        {
            Assert.Contains("?handler=" + handler + "\"", html, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("data-dialog=\"" + settingsId + "-", html, StringComparison.Ordinal);
        Assert.Contains("<dialog id=\"" + settingsId + "-delete\"", html, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"" + settingsId + "-delete\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", html, StringComparison.Ordinal);

        using var disabled = await client.PostAsync(
            AreaRoute + "?handler=Disable",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["staffId"] = staffId,
                ["expectedVersion"] = Field(html, "expectedVersion"),
                ["operationKey"] = Field(html, "operationKey"),
                ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken")
            }));
        Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var recorded = await context.ActionHistory
            .Where(item => item.AggregateId == staffId && item.EventKind == "staff_account_disabled")
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(recorded);
        Assert.Null(recorded!.Reason);
    }

    private static string Field(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, "Missing field " + name);
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }
}
