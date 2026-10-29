using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Accounts page's own posts: Create, Enable, Force logout, Reset
/// password and Delete from the forms the page renders, the Settings dialog
/// opened by link, and the refusals that keep what the person entered. One
/// host serves every Fact; each Fact works on accounts it creates.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class StaffAccountActionsWebTests : IClassFixture<IntakeWebApplicationFactory>
{
    private const string AreaRoute = "/Administration/Accounts";
    private const string TemporaryPassword = "Tempor4ryPassword!";
    private const string ExpiredForm = "The form has expired. Retry the operation.";
    private const string StaleAccount = "The staff account changed. Reload and try again.";
    private const string GenericRefusal = "The change was not accepted.";

    private readonly IntakeWebApplicationFactory factory;

    public StaffAccountActionsWebTests(IntakeWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CreatePostsFromTheDialogAndTheAccountReadsBack()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var list = await GetHtmlAsync(client, AreaRoute);
        var form = FormFields(list, "Create");
        form["userName"] = "create-from-dialog";
        form["temporaryPassword"] = TemporaryPassword;

        using (var created = await PostAsync(client, "Create", form))
        {
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var after = await GetHtmlAsync(client, AreaRoute);
        Assert.Contains(
            "The staff account was created and must change its password at first sign-in.",
            after,
            StringComparison.Ordinal);
        var account = await FindByUserNameAsync("create-from-dialog");
        Assert.Equal(StaffRole.User, account.Role);
        Assert.True(account.IsEnabled);
        Assert.True(account.MustChangePassword);
        Assert.True(await PasswordVerifiesAsync(account.Id, TemporaryPassword));
        var history = await HistoryAsync(form["operationKey"]);
        Assert.Equal("staff_account_created", history.EventKind);
        Assert.Equal(account.Id.ToString("D"), history.AggregateId);
        Assert.Null(history.Reason);
        Assert.DoesNotContain(TemporaryPassword, history.AfterJson ?? string.Empty, StringComparison.Ordinal);

        // The same name again is refused inside the open dialog, keeps the
        // name and creates nothing.
        var again = FormFields(after, "Create");
        again["userName"] = "create-from-dialog";
        again["temporaryPassword"] = TemporaryPassword;
        using var refused = await PostAsync(client, "Create", again);
        var refusedHtml = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("That username is already assigned.", refusedHtml, StringComparison.Ordinal);
        Assert.Contains(
            "data-dialog=\"create-account-dialog\" data-dialog-open-on-load=\"true\"",
            refusedHtml,
            StringComparison.Ordinal);
        Assert.Contains("value=\"create-from-dialog\"", refusedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(TemporaryPassword, refusedHtml, StringComparison.Ordinal);
        await using var context = await CreateContextAsync();
        Assert.Equal(1, await context.Users.CountAsync(user => user.NormalizedUserName == "CREATE-FROM-DIALOG"));
    }

    [Fact]
    public async Task EnablePostsFromTheSettingsDialogAndTheAccountReadsEnabled()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("enable-from-dialog");
        var settings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        using (var disabled = await PostAsync(client, "Disable", FormFields(settings, "Disable")))
        {
            Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        }

        var disabledSettings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        Assert.Contains("?handler=Enable\"", disabledSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("?handler=ForceLogout\"", disabledSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("?handler=ResetPassword\"", disabledSettings, StringComparison.Ordinal);
        var before = await ReadUserAsync(account.Id);
        Assert.False(before.IsEnabled);

        var enable = FormFields(disabledSettings, "Enable");
        using (var enabled = await PostAsync(client, "Enable", enable))
        {
            Assert.Equal(HttpStatusCode.Redirect, enabled.StatusCode);
        }

        var after = await ReadUserAsync(account.Id);
        Assert.True(after.IsEnabled);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        var history = await HistoryAsync(enable["operationKey"]);
        Assert.Equal("staff_account_enabled", history.EventKind);
        Assert.Null(history.Reason);
    }

    [Fact]
    public async Task ForceLogoutPostsFromTheSettingsDialogAndRotatesTheStamp()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("force-logout-from-dialog");
        var before = await ReadUserAsync(account.Id);
        var settings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        var forceLogout = FormFields(settings, "ForceLogout");

        using (var response = await PostAsync(client, "ForceLogout", forceLogout))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var after = await ReadUserAsync(account.Id);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.True(after.IsEnabled);
        var history = await HistoryAsync(forceLogout["operationKey"]);
        Assert.Equal("staff_logout_forced", history.EventKind);
        Assert.Null(history.Reason);

        // A form whose key is not the page's refuses and changes nothing.
        var refusedSettings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        var expired = FormFields(refusedSettings, "ForceLogout");
        expired["operationKey"] = "expired";
        using var refused = await PostAsync(client, "ForceLogout", expired);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(ExpiredForm, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(after.SecurityStamp, (await ReadUserAsync(account.Id)).SecurityStamp);
    }

    [Fact]
    public async Task ResetPasswordShowsTheTemporaryPasswordOnceAndForcesAChange()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("reset-from-dialog");
        var before = await ReadUserAsync(account.Id);
        var settings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        var reset = FormFields(settings, "ResetPassword");

        using var response = await PostAsync(client, "ResetPassword", reset);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var shown = Regex.Match(html, "<output class=\"mono\">(?<value>[^<]+)</output>");
        Assert.True(shown.Success, "The temporary password is not shown.");
        var temporaryPassword = WebUtility.HtmlDecode(shown.Groups["value"].Value);

        Assert.True(await PasswordVerifiesAsync(account.Id, temporaryPassword));
        Assert.False(await PasswordVerifiesAsync(account.Id, TemporaryPassword));
        var after = await ReadUserAsync(account.Id);
        Assert.True(after.MustChangePassword);
        Assert.Equal(before.Version + 1, after.Version);
        var history = await HistoryAsync(reset["operationKey"]);
        Assert.Equal("staff_password_reset", history.EventKind);
        Assert.DoesNotContain(temporaryPassword, history.AfterJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(temporaryPassword, await GetHtmlAsync(client, AreaRoute), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AResetRenderedBeforeTheAccountWasDisabledAsksForAReload()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("reset-after-disable");
        var settings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        var reset = FormFields(settings, "ResetPassword");
        using (var disabled = await PostAsync(client, "Disable", FormFields(settings, "Disable")))
        {
            Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        }

        using var refused = await PostAsync(client, "ResetPassword", reset);
        var html = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(StaleAccount, html, StringComparison.Ordinal);
        Assert.DoesNotContain(GenericRefusal, html, StringComparison.Ordinal);
        Assert.DoesNotContain("<output", html, StringComparison.Ordinal);
        Assert.True(await PasswordVerifiesAsync(account.Id, TemporaryPassword));
    }

    [Fact]
    public async Task DeletePostsFromItsDialogAndKeepsTheHistory()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("delete-from-dialog");
        var settings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        var delete = FormFields(settings, "Delete");

        using (var response = await PostAsync(client, "Delete", delete))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>()
                .GetAsync(account.Id, default));
        }
        Assert.DoesNotContain("delete-from-dialog", await GetHtmlAsync(client, AreaRoute), StringComparison.Ordinal);
        using (var gone = await client.GetAsync(SettingsRoute(account.Id)))
        {
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }
        var history = await HistoryAsync(delete["operationKey"]);
        Assert.Equal("staff_account_deleted", history.EventKind);
        Assert.Equal(account.Id.ToString("D"), history.AggregateId);
    }

    // FRD-17 §Accounts: Settings loads its current values and expected
    // version; each change sends the rendered version and a stale one is
    // refused. Opening the dialog changes nothing, so no version in the link
    // can refuse it.
    [Fact]
    public async Task SettingsOpensByLinkAtTheCurrentVersionAndAStalePostIsStillRefused()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("settings-by-link");
        var settingsId = "settings-" + account.Id.ToString("D");

        var opened = await GetHtmlAsync(client, SettingsRoute(account.Id));
        Assert.Contains(
            "data-dialog=\"" + settingsId + "\" data-dialog-open-on-load=\"true\"",
            opened,
            StringComparison.Ordinal);
        var firstSettings = FormFields(opened, "Settings");
        Assert.Equal(Invariant(account.Version), firstSettings["expectedVersion"]);

        using (var disabled = await PostAsync(client, "Disable", FormFields(opened, "Disable")))
        {
            Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        }

        var current = await ReadUserAsync(account.Id);
        var oldLink = await GetHtmlAsync(
            client,
            SettingsRoute(account.Id) + "&expectedVersion=" + Invariant(account.Version));
        Assert.Contains(
            "data-dialog=\"" + settingsId + "\" data-dialog-open-on-load=\"true\"",
            oldLink,
            StringComparison.Ordinal);
        Assert.Equal(Invariant(current.Version), FormFields(oldLink, "Settings")["expectedVersion"]);

        firstSettings["role"] = nameof(StaffRole.User);
        firstSettings["isSignOffEngineer"] = "false";
        using var stale = await PostAsync(client, "Settings", firstSettings);
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Contains(StaleAccount, await stale.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // FRD-04 §Staff accounts: a refused settings change keeps the entered
    // values inside Settings, the Role among them.
    [Fact]
    public async Task ARefusedSettingsPostKeepsTheRoleThePersonPicked()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("settings-keeps-role");
        var settings = await GetHtmlAsync(client, SettingsRoute(account.Id));
        var save = FormFields(settings, "Settings");
        save["role"] = nameof(StaffRole.Engineer);
        save["isSignOffEngineer"] = "false";
        using (var saved = await PostAsync(client, "Settings", save))
        {
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        }
        Assert.Equal(StaffRole.Engineer, (await FindByUserNameAsync("settings-keeps-role")).Role);

        // A refused key keeps the Role that was posted.
        var expired = FormFields(await GetHtmlAsync(client, SettingsRoute(account.Id)), "Settings");
        expired["role"] = nameof(StaffRole.Administrator);
        expired["isSignOffEngineer"] = "false";
        expired["operationKey"] = "expired";
        using (var refused = await PostAsync(client, "Settings", expired))
        {
            var html = await refused.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains(ExpiredForm, html, StringComparison.Ordinal);
            Assert.Contains(SelectedRole(StaffRole.Administrator), html, StringComparison.Ordinal);
            Assert.DoesNotContain(SelectedRole(StaffRole.User), html, StringComparison.Ordinal);
        }
        Assert.Equal(StaffRole.Engineer, (await FindByUserNameAsync("settings-keeps-role")).Role);

        // A Role that is not one keeps the stored Role.
        var bogus = FormFields(await GetHtmlAsync(client, SettingsRoute(account.Id)), "Settings");
        bogus["role"] = "Bogus";
        bogus["isSignOffEngineer"] = "false";
        using (var refused = await PostAsync(client, "Settings", bogus))
        {
            var html = await refused.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains("Select a supported staff role.", html, StringComparison.Ordinal);
            Assert.Contains(SelectedRole(StaffRole.Engineer), html, StringComparison.Ordinal);
        }

        // The signed-in Administrator's own Role travels in a hidden input,
        // so a resubmit after a refusal must not carry a different one.
        var administrator = await FindByUserNameAsync(DevelopmentOfflineIdentity.UserName);
        var own = FormFields(await GetHtmlAsync(client, SettingsRoute(administrator.Id)), "Settings");
        own["operationKey"] = "expired";
        own["isSignOffEngineer"] = administrator.SignOff.IsSignOffEngineer ? "true" : "false";
        own["printedName"] = administrator.SignOff.PrintedName ?? string.Empty;
        using (var refused = await PostAsync(client, "Settings", own))
        {
            var html = await refused.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains(ExpiredForm, html, StringComparison.Ordinal);
            Assert.Equal(nameof(StaffRole.Administrator), FormFields(html, "Settings")["role"]);
        }
    }

    [Fact]
    public async Task ASignatureThatIsNotAPngIsRefusedWithTheSignatureSentence()
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        var account = await CreateAccountAsync("signature-not-png");
        var fields = FormFields(await GetHtmlAsync(client, SettingsRoute(account.Id)), "Settings");
        fields["role"] = nameof(StaffRole.User);
        fields["isSignOffEngineer"] = "true";
        fields["printedName"] = "Signature Check";

        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
        {
            form.Add(new StringContent(value), name);
        }
        var signature = new ByteArrayContent("not a png image"u8.ToArray());
        signature.Headers.ContentType = new MediaTypeHeaderValue(SignOffSignaturePolicy.MediaType);
        form.Add(signature, "signature", "sig.png");

        using var refused = await client.PostAsync(AreaRoute + "?handler=Settings", form);
        var html = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(OperatorLabels.StaffAccounts.SignatureInvalid, html, StringComparison.Ordinal);
        Assert.DoesNotContain(GenericRefusal, html, StringComparison.Ordinal);
        Assert.Contains("value=\"Signature Check\"", html, StringComparison.Ordinal);
        var stored = await FindByUserNameAsync("signature-not-png");
        Assert.False(stored.SignOff.IsSignOffEngineer);
        Assert.False(stored.SignOff.HasSignature);
    }

    private static string SettingsRoute(Guid staffId) => AreaRoute + "?editStaffId=" + staffId.ToString("D");

    private static string SelectedRole(StaffRole role) =>
        "<option value=\"" + role + "\" selected=\"selected\">";

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string handler,
        Dictionary<string, string> fields) =>
        client.PostAsync(AreaRoute + "?handler=" + handler, new FormUrlEncodedContent(fields));

    /// <summary>
    /// The hidden inputs of the one form that posts to <paramref name="handler"/>.
    /// The dialog holds a form per action, each with its own key and version,
    /// and a key a second handler reuses is an operation conflict.
    /// </summary>
    private static Dictionary<string, string> FormFields(string html, string handler)
    {
        var form = Regex.Match(
            html,
            "<form\\b[^>]*\\?handler=" + Regex.Escape(handler) + "\"[^>]*>(?<body>.*?)</form>",
            RegexOptions.Singleline);
        Assert.True(form.Success, "Missing form for handler " + handler);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(form.Groups["body"].Value, "<input\\b[^>]*>"))
        {
            if (!input.Value.Contains("type=\"hidden\"", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Regex.Match(input.Value, "name=\"([^\"]*)\"").Groups[1].Value;
            var value = Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value;
            fields[name] = WebUtility.HtmlDecode(value);
        }

        Assert.True(fields.ContainsKey("__RequestVerificationToken"), "Missing antiforgery token for " + handler);
        return fields;
    }

    private async Task<StaffAccountSummary> CreateAccountAsync(string userName)
    {
        var administrator = await FindByUserNameAsync(DevelopmentOfflineIdentity.UserName);
        await using var scope = factory.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<ICreateStaffAccount>().ExecuteAsync(
            new CreateStaffAccountRequest(
                ActionActor.Staff(administrator.Id, [administrator.Role]),
                userName,
                TemporaryPassword,
                Guid.NewGuid().ToString("N")),
            default);
        return created.Account;
    }

    private async Task<StaffAccountSummary> FindByUserNameAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = await scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>()
            .ListAsync(0, ListStaffAccounts.MaximumPageSize, default);
        return accounts.Accounts.Single(item => item.UserName == userName);
    }

    private async Task<PegasusIdentityUser> ReadUserAsync(Guid staffId)
    {
        await using var context = await CreateContextAsync();
        return await context.Users.AsNoTracking().SingleAsync(user => user.Id == staffId);
    }

    private async Task<bool> PasswordVerifiesAsync(Guid staffId, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = await users.FindByIdAsync(staffId.ToString("D"));
        Assert.NotNull(user);
        return await users.CheckPasswordAsync(user!, password);
    }

    private async Task<ActionHistoryEntity> HistoryAsync(string operationKey)
    {
        await using var context = await CreateContextAsync();
        return await context.ActionHistory.AsNoTracking().SingleAsync(item =>
            item.AggregateType == "staff_account" && item.CorrelationId == operationKey);
    }

    private async Task<PegasusDbContext> CreateContextAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
    }
}
