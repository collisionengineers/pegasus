using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

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
        var administrator = await FindAccountAsync(factory, DevelopmentOfflineIdentity.UserName);

        // One area, drawn as the design contract draws it.
        Assert.Contains("Staff accounts &amp; roles", html, StringComparison.Ordinal);
        Assert.Contains("class=\"admin-layout\"", html, StringComparison.Ordinal);
        foreach (var column in new[] { "Username", "Role", "State", "Last reviewed", "Save", "Account" })
        {
            Assert.Contains($"<th scope=\"col\">{column}</th>", html, StringComparison.Ordinal);
        }

        Assert.Contains("Create staff account", html, StringComparison.Ordinal);

        // The role control reuses Roles/Index's independent checkbox set, so
        // selecting another role never clears the account's existing roles.
        var roleCheckboxes = RoleCheckboxRegex().Matches(html);
        Assert.Equal(Enum.GetValues<StaffRole>().Length, roleCheckboxes.Count);
        foreach (var role in Enum.GetValues<StaffRole>())
        {
            Assert.Contains($"value=\"{role}\"", html, StringComparison.Ordinal);
        }

        // The signed-in administrator has no self-action controls or dialogs.
        Assert.DoesNotContain(
            $"data-dialog-open=\"disable-{administrator.Id:D}\"",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"data-dialog-open=\"review-{administrator.Id:D}\"",
            html,
            StringComparison.Ordinal);
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

        using var createdLanding = await client.GetAsync(AreaRoute);
        var createdHtml = await createdLanding.Content.ReadAsStringAsync();
        createdLanding.EnsureSuccessStatusCode();
        Assert.Contains(
            $"data-dialog-open=\"disable-{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"data-dialog-open=\"review-{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"id=\"disable-{created.Id:D}_reason\" name=\"Reason\" rows=\"3\" required maxlength=\"{StaffAccountAdministrationPolicy.MaximumReasonLength}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"id=\"review-{created.Id:D}_reason\" name=\"Reason\" rows=\"3\" required maxlength=\"{StaffAccountAdministrationPolicy.MaximumReasonLength}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Disabling revokes existing browser sessions; the account is retained permanently.",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/Administration/Accounts/Confirm/Disable/{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/Administration/Accounts/Confirm/Review/{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        using var scriptOffConfirm = await client.GetAsync(
            $"/Administration/Accounts/Confirm/Review/{created.Id:D}");
        var scriptOffConfirmHtml = await scriptOffConfirm.Content.ReadAsStringAsync();
        scriptOffConfirm.EnsureSuccessStatusCode();
        Assert.Contains("method=\"post\"", scriptOffConfirmHtml, StringComparison.Ordinal);
        Assert.Contains("handler=Review", scriptOffConfirmHtml, StringComparison.Ordinal);

        // The Disable branch of the same page, which is the one that carries
        // the consequence notice; both branches are Test UI catalogue states.
        using var scriptOffDisable = await client.GetAsync(
            $"/Administration/Accounts/Confirm/Disable/{created.Id:D}");
        var scriptOffDisableHtml = await scriptOffDisable.Content.ReadAsStringAsync();
        scriptOffDisable.EnsureSuccessStatusCode();
        Assert.Contains("handler=Disable", scriptOffDisableHtml, StringComparison.Ordinal);
        Assert.Contains(
            OperatorLabels.StaffAccounts.DisableConsequence,
            scriptOffDisableHtml,
            StringComparison.Ordinal);

        // Creation keeps the reason when Core rejects a duplicate username,
        // while the temporary password remains intentionally unrendered.
        const string rejectedCreateReason = "PLAT-027 rejected create reason";
        using var duplicateCreatePost = await client.PostAsync(
            $"{AreaRoute}?handler=Create",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("userName", "plat027-web-caller"),
                ("temporaryPassword", "Temporary-Password-2"),
                ("reason", rejectedCreateReason)));
        Assert.Equal(HttpStatusCode.OK, duplicateCreatePost.StatusCode);
        var duplicateCreateHtml = await duplicateCreatePost.Content.ReadAsStringAsync();
        var createReasonInput = InputTagRegex().Matches(duplicateCreateHtml)
            .Cast<Match>()
            .Single(candidate => candidate.Value.Contains(
                "id=\"create-reason\"",
                StringComparison.Ordinal));
        Assert.Equal(
            rejectedCreateReason,
            WebUtility.HtmlDecode(createReasonInput.Groups["value"].Value));

        // A rejected role post keeps both its selected roles and reason on the
        // targeted row, matching the superseded Roles page's behaviour.
        const string rejectedRoleReason = "PLAT-027 rejected role reason";
        using var rejectedRolesPost = await client.PostAsync(
            $"{AreaRoute}?handler=Roles",
            Form(
                ("__RequestVerificationToken", token),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("staffId", created.Id.ToString("D")),
                ("selectedRoles", nameof(StaffRole.Engineer)),
                ("selectedRoles", nameof(StaffRole.User)),
                ("selectedRoles", "UnsupportedRole"),
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
        Assert.True(CheckboxIsChecked(rejectedRolesHtml, created.Id, StaffRole.Engineer));
        Assert.True(CheckboxIsChecked(rejectedRolesHtml, created.Id, StaffRole.User));

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

    private static bool CheckboxIsChecked(string html, Guid staffId, StaffRole role)
    {
        var id = $"roles-{staffId:D}-select-{role}";
        var match = RoleCheckboxRegex().Matches(html)
            .Cast<Match>()
            .SingleOrDefault(candidate => candidate.Value.Contains(
                $"id=\"{id}\"",
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The role checkbox '{id}' must be rendered.");
        return match!.Value.Contains("checked", StringComparison.Ordinal);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(
        "<input\\b[^>]*\\btype=\"checkbox\"[^>]*\\bname=\"selectedRoles\"[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoleCheckboxRegex();

    [GeneratedRegex(
        "<time\\b[^>]*\\bdatetime=\"(?<value>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeStampRegex();
}
