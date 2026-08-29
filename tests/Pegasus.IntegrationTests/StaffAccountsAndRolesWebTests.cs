using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// PLAT-027: Staff accounts, staff roles and the access review are one
/// administration area. These tests pin the fold itself — that the single
/// page still reaches every Core use case the three superseded pages reached,
/// and that no capability was dropped on the way.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class StaffAccountsAndRolesWebTests
{
    private const string AreaRoute = "/Administration/Accounts";

    [Fact]
    public async Task ConsolidatedAreaCarriesAccountsRolesAndAccessReview()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var landing = await client.GetAsync(AreaRoute);
        var html = await landing.Content.ReadAsStringAsync();
        landing.EnsureSuccessStatusCode();

        // One area, drawn as the design contract draws it.
        Assert.Contains("Staff accounts &amp; roles", html, StringComparison.Ordinal);
        Assert.Contains("class=\"admin-layout\"", html, StringComparison.Ordinal);
        foreach (var column in new[] { "Username", "Role", "State", "Last reviewed", "Save", "Account" })
        {
            Assert.Contains($"<th scope=\"col\">{column}</th>", html, StringComparison.Ordinal);
        }

        Assert.Contains("Create staff account", html, StringComparison.Ordinal);

        // The role control is one inline select carrying the whole Core role
        // set: a single-valued select would strip engineer eligibility from a
        // multi-role account, because CaseEngineerEligibility gates on the
        // Engineer role specifically.
        var roleSelect = RoleSelectRegex().Match(html);
        Assert.True(roleSelect.Success, "The accounts table must render the inline role select.");
        Assert.Contains("multiple", roleSelect.Value, StringComparison.Ordinal);
        foreach (var role in Enum.GetValues<StaffRole>())
        {
            Assert.Contains($"value=\"{role}\"", html, StringComparison.Ordinal);
        }

        // Both folded account actions are drawn, each wired to its own reason
        // dialog rather than to a page that no longer exists.
        Assert.Contains("data-dialog-open=\"disable-", html, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"review-", html, StringComparison.Ordinal);
        Assert.Contains(
            $"id=\"disable-{DevelopmentOfflineIdentity.AdministratorId:D}_reason\" name=\"Reason\" rows=\"3\" required maxlength=\"{StaffAccountAdministrationPolicy.MaximumReasonLength}\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            $"id=\"review-{DevelopmentOfflineIdentity.AdministratorId:D}_reason\" name=\"Reason\" rows=\"3\" required maxlength=\"{StaffAccountAdministrationPolicy.MaximumReasonLength}\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "Disabling revokes existing browser sessions; the account is retained permanently.",
            html,
            StringComparison.Ordinal);

        var administrator = await FindAccountAsync(factory, DevelopmentOfflineIdentity.UserName);
        Assert.False(administrator.MustChangePassword);
        Assert.Contains(">Password change complete</span>", html, StringComparison.Ordinal);

        // The superseded pages' explanatory copy did not travel with them.
        Assert.DoesNotContain("At least eight characters", html, StringComparison.Ordinal);
        Assert.DoesNotContain("records that a named administrator looked at", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Removing the final enabled Administrator is denied", html, StringComparison.Ordinal);

        // Create staff account — the first of the four handlers.
        var token = InputValue(html, "__RequestVerificationToken");
        using var createPost = await client.PostAsync(
            $"{AreaRoute}?handler=Create",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("userName", "plat027-web-caller"),
                ("temporaryPassword", "Temporary-Password-1"),
                ("reason", "PLAT-027 consolidated area proof")));
        Assert.Equal(HttpStatusCode.Redirect, createPost.StatusCode);

        var created = await FindAccountAsync(factory, "plat027-web-caller");
        Assert.Equal(new[] { StaffRole.User }, created.Roles);
        Assert.True(created.IsEnabled);
        Assert.Null(created.LastAccessReviewAtUtc);

        // A rejected role post keeps its entered reason on the targeted row,
        // matching the superseded Roles page's bound-property behaviour.
        const string rejectedRoleReason = "PLAT-027 rejected role reason";
        using var rejectedRolesPost = await client.PostAsync(
            $"{AreaRoute}?handler=Roles",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("staffId", created.Id.ToString("D")),
                ("reason", rejectedRoleReason)));
        Assert.Equal(HttpStatusCode.OK, rejectedRolesPost.StatusCode);
        var rejectedRolesHtml = await rejectedRolesPost.Content.ReadAsStringAsync();
        var rejectedRoleReasonInput = InputTagRegex().Matches(rejectedRolesHtml)
            .Cast<Match>()
            .Single(candidate => candidate.Value.Contains(
                $"id=\"roles-{created.Id:D}-reason\"",
                StringComparison.Ordinal));
        Assert.Equal(
            rejectedRoleReason,
            WebUtility.HtmlDecode(rejectedRoleReasonInput.Groups["value"].Value));

        // Role assignment — the capability the separate Roles page carried.
        using var rolesPost = await client.PostAsync(
            $"{AreaRoute}?handler=Roles",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("staffId", created.Id.ToString("D")),
                ("selectedRoles", nameof(StaffRole.Engineer)),
                ("selectedRoles", nameof(StaffRole.User)),
                ("reason", "PLAT-027 role assignment proof")));
        Assert.Equal(HttpStatusCode.Redirect, rolesPost.StatusCode);

        var afterRoles = await FindAccountAsync(factory, "plat027-web-caller");
        Assert.Equal(new[] { StaffRole.Engineer, StaffRole.User }, afterRoles.Roles);

        // Access review — the capability the separate Access page carried.
        using var reviewPost = await client.PostAsync(
            $"{AreaRoute}?handler=Review",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("staffId", created.Id.ToString("D")),
                ("reason", new string('R', StaffAccountAdministrationPolicy.MaximumReasonLength))));
        Assert.Equal(HttpStatusCode.Redirect, reviewPost.StatusCode);

        var afterReview = await FindAccountAsync(factory, "plat027-web-caller");
        Assert.NotNull(afterReview.LastAccessReviewAtUtc);

        // Account disable — the capability the separate Edit page carried.
        using var disablePost = await client.PostAsync(
            $"{AreaRoute}?handler=Disable",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("staffId", created.Id.ToString("D")),
                ("reason", "PLAT-027 disable proof")));
        Assert.Equal(HttpStatusCode.Redirect, disablePost.StatusCode);

        var afterDisable = await FindAccountAsync(factory, "plat027-web-caller");
        Assert.False(afterDisable.IsEnabled);

        // The readout the fold had to keep: a reviewed account shows when, and
        // the account state chip follows the disable.
        using var settled = await client.GetAsync(AreaRoute);
        var settledHtml = await settled.Content.ReadAsStringAsync();
        settled.EnsureSuccessStatusCode();
        // Razor encodes the round-trip stamp's "+" as &#x2B;, so the attribute
        // is compared decoded rather than byte for byte.
        var renderedTimes = TimeStampRegex().Matches(settledHtml)
            .Select(match => WebUtility.HtmlDecode(match.Groups["value"].Value))
            .ToArray();
        Assert.Contains(afterReview.LastAccessReviewAtUtc!.Value.ToString("O"), renderedTimes);
        Assert.Contains(">Disabled</span>", settledHtml, StringComparison.Ordinal);
        // A disabled account keeps its Review action and loses only Disable.
        Assert.DoesNotContain(
            $"data-dialog-open=\"disable-{afterDisable.Id:D}\"",
            settledHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"data-dialog-open=\"review-{afterDisable.Id:D}\"",
            settledHtml,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutstandingAccessReviewIsShownForAnAccountCoreCallsDue()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // The seeded administrator has never been reviewed and is enabled, so
        // Core's ReviewIsOutstanding is true for it.
        var administrator = await FindAccountAsync(factory, DevelopmentOfflineIdentity.UserName);
        Assert.Null(administrator.LastAccessReviewAtUtc);

        using var landing = await client.GetAsync(AreaRoute);
        var html = await landing.Content.ReadAsStringAsync();
        landing.EnsureSuccessStatusCode();
        Assert.Contains(">Due</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupersededStaffAccessRoutesStillAnswerUntilTheRemovalTicket()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // PLAT-027 builds the replacement; UIIMP-009 deletes these. Until then
        // they must not 404 — and they must not be linked from the area rail.
        foreach (var route in new[] { "/Administration/Roles", "/Administration/Access" })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
        }

        using var landing = await client.GetAsync(AreaRoute);
        var html = await landing.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Administration/Roles", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/Administration/Access", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsolidatedAreaDeniesANonAdministratorSession()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: false);
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True((await userManager.RemoveFromRoleAsync(
                user,
                StaffRoleNames.Administrator)).Succeeded);
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync(AreaRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] fields) =>
        new(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));

    private static async Task<StaffAccountSummary> FindAccountAsync(
        IntakeWebApplicationFactory factory,
        string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>();
        var slice = await queries.ListAsync(0, ListStaffAccounts.MaximumPageSize, CancellationToken.None);
        var account = slice.Accounts.SingleOrDefault(
            item => string.Equals(item.UserName, userName, StringComparison.Ordinal));
        Assert.True(account is not null, $"The staff account '{userName}' must exist.");
        return account!;
    }

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The staff accounts area must render input '{name}'.");
        return WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(
        "<select\\b[^>]*\\bname=\"selectedRoles\"[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoleSelectRegex();

    [GeneratedRegex(
        "<time\\b[^>]*\\bdatetime=\"(?<value>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeStampRegex();
}
