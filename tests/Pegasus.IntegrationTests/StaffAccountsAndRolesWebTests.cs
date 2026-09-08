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
/// Staff account and role administration reaches the current Core actions
/// through one administration area, including session and credential changes.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class StaffAccountsAndRolesWebTests
{
    private const string AreaRoute = "/Administration/Accounts";

    [Fact]
    public async Task ConsolidatedAreaCarriesAccountActionsAndRoles()
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
        foreach (var column in new[] { "Username", "Role", "State", "Save", "Account" })
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

        using var createdLanding = await client.GetAsync(AreaRoute);
        var createdHtml = await createdLanding.Content.ReadAsStringAsync();
        createdLanding.EnsureSuccessStatusCode();
        Assert.Contains(
            $"/Administration/Accounts/Confirm/Disable/{created.Id:D}",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"/Administration/Accounts/Confirm/ResetPassword/{created.Id:D}",
            createdHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"data-dialog-open=\"sign-off-{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/Administration/Accounts/Confirm/Disable/{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/Administration/Accounts/Confirm/ResetPassword/{created.Id:D}\"",
            createdHtml,
            StringComparison.Ordinal);
        using var scriptOffConfirm = await client.GetAsync(
            $"/Administration/Accounts/Confirm/ResetPassword/{created.Id:D}");
        var scriptOffConfirmHtml = await scriptOffConfirm.Content.ReadAsStringAsync();
        scriptOffConfirm.EnsureSuccessStatusCode();
        Assert.Contains("method=\"post\"", scriptOffConfirmHtml, StringComparison.Ordinal);
        Assert.Contains("handler=ResetPassword", scriptOffConfirmHtml, StringComparison.Ordinal);

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

        using var signOffLanding = await client.GetAsync(AreaRoute);
        var signOffHtml = await signOffLanding.Content.ReadAsStringAsync();
        signOffLanding.EnsureSuccessStatusCode();
        Assert.Contains(
            $"data-dialog-open=\"sign-off-{created.Id:D}\"",
            signOffHtml,
            StringComparison.Ordinal);
        Assert.Empty(InlineScriptRegex().Matches(signOffHtml));
        using var signOffPost = await client.PostAsync(
            $"{AreaRoute}?handler=SignOff",
            SignOffForm(
                InputValue(signOffHtml, "__RequestVerificationToken"),
                created.Id,
                "A Engineer",
                qualifications: null,
                isDefault: true,
                "PLAT-068 sign-off proof",
                Guid.NewGuid().ToString("N"),
                Png()));
        Assert.Equal(HttpStatusCode.Redirect, signOffPost.StatusCode);

        var afterSignOff = await FindAccountAsync(factory, "plat027-web-caller");
        Assert.True(afterSignOff.SignOff.IsSignOffEngineer);
        Assert.Equal("A Engineer", afterSignOff.SignOff.PrintedName);
        Assert.Null(afterSignOff.SignOff.Qualifications);
        Assert.True(afterSignOff.SignOff.HasSignature);
        Assert.True(afterSignOff.SignOff.IsDefault);
        await using (var signOffScope = factory.Services.CreateAsyncScope())
        {
            var context = signOffScope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            Assert.True(await context.ActionHistory.AnyAsync(item =>
                item.AggregateId == created.Id.ToString("D")
                && item.EventKind == "staff_account_sign_off_updated"
                && item.Reason == "PLAT-068 sign-off proof"));
        }

        using var missingNamePost = await client.PostAsync(
            $"{AreaRoute}?handler=SignOff",
            SignOffForm(
                InputValue(signOffHtml, "__RequestVerificationToken"),
                created.Id,
                string.Empty,
                qualifications: null,
                isDefault: false,
                "Missing name proof",
                Guid.NewGuid().ToString("N"),
                signature: null));
        Assert.Equal(HttpStatusCode.OK, missingNamePost.StatusCode);
        Assert.Contains(
            OperatorLabels.StaffAccounts.PrintedNameRequired,
            await missingNamePost.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        // Account disable remains an explicit, reasoned action.
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

        using var settled = await client.GetAsync(AreaRoute);
        var settledHtml = await settled.Content.ReadAsStringAsync();
        settled.EnsureSuccessStatusCode();
        Assert.Contains(">Disabled</span>", settledHtml, StringComparison.Ordinal);
        Assert.Contains(
            $"/Administration/Accounts/Confirm/Enable/{afterDisable.Id:D}",
            settledHtml,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupersededStaffAccessRoutesAreAbsent()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[] { "/Administration/Access", "/Administration/Accounts/Edit/00000000-0000-0000-0000-000000000000" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var landing = await client.GetAsync(AreaRoute);
        var html = await landing.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Administration/Access", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasswordResetReturnsTheTemporaryPasswordOnlyOnItsPostResponse()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var creationScope = factory.Services.CreateAsyncScope();
        var created = await CreateEngineerAsync(
            creationScope.ServiceProvider,
            ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
            "a01-reset-password");

        using var confirmation = await client.GetAsync(
            $"/Administration/Accounts/Confirm/ResetPassword/{created.Id:D}");
        var confirmationHtml = await confirmation.Content.ReadAsStringAsync();
        confirmation.EnsureSuccessStatusCode();
        Assert.DoesNotContain("Temporary password</h2>", confirmationHtml, StringComparison.Ordinal);

        using var reset = await client.PostAsync(
            $"{AreaRoute}?handler=ResetPassword",
            Form(
                ("__RequestVerificationToken", InputValue(confirmationHtml, "__RequestVerificationToken")),
                ("operationKey", InputValue(confirmationHtml, "operationKey")),
                ("staffId", created.Id.ToString("D")),
                ("reason", "A01 reset password proof")));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var resetHtml = await reset.Content.ReadAsStringAsync();
        Assert.Contains("Temporary password</h2>", resetHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Temporary-Password-1", resetHtml, StringComparison.Ordinal);
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

    [Fact]
    public async Task SignOffUpdatesAreReplaySafeAndTransferTheSingleDefault()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var first = await CreateEngineerAsync(services, actor, "plat068-first");
        var second = await CreateEngineerAsync(services, actor, "plat068-second");
        var update = services.GetRequiredService<IUpdateStaffAccountSignOff>();
        var operationKey = Guid.NewGuid().ToString("N");
        var request = new UpdateStaffAccountSignOffRequest(
            actor,
            first.Id,
            true,
            "First Engineer",
            null,
            Png(),
            true,
            "First default",
            operationKey);

        var initial = await update.ExecuteAsync(request, default);
        var replay = await update.ExecuteAsync(request, default);
        var conflict = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            update.ExecuteAsync(
                request with { Signature = [.. Png(), 0x01] },
                default));
        Assert.False(initial.WasReplay);
        Assert.True(replay.WasReplay);
        Assert.Equal(StaffAccountAdministrationError.OperationConflict, conflict.Error);

        await update.ExecuteAsync(
            new(
                actor,
                second.Id,
                true,
                "Second Engineer",
                "M.Inst.IAEA",
                Png(),
                true,
                "Transfer default",
                Guid.NewGuid().ToString("N")),
            default);

        var queries = services.GetRequiredService<IStaffAccountQueries>();
        var profiles = await queries.ListSignOffEngineersAsync(default);
        Assert.Equal(2, profiles.Count);
        Assert.False((await queries.GetSignOffEngineerAsync(first.Id, default))!.IsDefault);
        Assert.True((await queries.GetSignOffEngineerAsync(second.Id, default))!.IsDefault);

        await services.GetRequiredService<IDisableStaffAccount>().ExecuteAsync(
            new(
                actor,
                second.Id,
                "Disable current default",
                Guid.NewGuid().ToString("N")),
            default);
        var retainedIneligibleDefault = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            update.ExecuteAsync(
                new(
                    actor,
                    second.Id,
                    true,
                    "Second Engineer",
                    "M.Inst.IAEA",
                    null,
                    true,
                    "Retain ineligible default",
                    Guid.NewGuid().ToString("N")),
                default));
        Assert.Equal(
            StaffAccountAdministrationError.IneligibleSignOffEngineer,
            retainedIneligibleDefault.Error);

        await services.GetRequiredService<IAssignStaffRoles>().ExecuteAsync(
            new(
                actor,
                first.Id,
                [StaffRole.User],
                "Remove Engineer role",
                Guid.NewGuid().ToString("N")),
            default);
        Assert.Null(await queries.GetSignOffEngineerAsync(first.Id, default));
        var retained = await queries.GetAsync(first.Id, default);
        Assert.True(retained!.SignOff.IsSignOffEngineer);
        var ineligibleDefault = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            update.ExecuteAsync(
                request with
                {
                    Signature = null,
                    OperationKey = Guid.NewGuid().ToString("N")
                },
                default));
        Assert.Equal(
            StaffAccountAdministrationError.SignOffEngineerRequiresEngineerRole,
            ineligibleDefault.Error);

        var context = services.GetRequiredService<PegasusDbContext>();
        var history = await context.ActionHistory
            .Where(item => item.CorrelationId == operationKey)
            .SingleAsync();
        Assert.Contains("SignOffSignatureDigest", history.AfterJson, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(Png()), history.AfterJson, StringComparison.Ordinal);

        await using var invariantScope = factory.Services.CreateAsyncScope();
        var invariantContext = invariantScope.ServiceProvider
            .GetRequiredService<PegasusDbContext>();
        Assert.Equal(
            second.Id,
            await invariantContext.Users
                .Where(item => item.IsDefaultSignOffEngineer)
                .Select(item => item.Id)
                .SingleAsync());
        var firstUser = await invariantContext.Users
            .SingleAsync(item => item.Id == first.Id);
        firstUser.IsDefaultSignOffEngineer = true;
        await Assert.ThrowsAsync<DbUpdateException>(
            () => invariantContext.SaveChangesAsync());
    }

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] fields) =>
        new(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));

    private static MultipartFormDataContent SignOffForm(
        string token,
        Guid staffId,
        string printedName,
        string? qualifications,
        bool isDefault,
        string reason,
        string operationKey,
        byte[]? signature)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(staffId.ToString("D")), "staffId" },
            { new StringContent("true"), "isSignOffEngineer" },
            { new StringContent(printedName), "printedName" },
            { new StringContent(isDefault ? "true" : "false"), "isDefault" },
            { new StringContent(reason), "reason" },
            { new StringContent(operationKey), "operationKey" }
        };
        if (qualifications is not null)
        {
            form.Add(new StringContent(qualifications), "qualifications");
        }

        if (signature is not null)
        {
            var file = new ByteArrayContent(signature);
            file.Headers.ContentType = new MediaTypeHeaderValue(SignOffSignaturePolicy.MediaType);
            form.Add(file, "signature", "signature.png");
        }

        return form;
    }

    private static async Task<StaffAccountSummary> CreateEngineerAsync(
        IServiceProvider services,
        ActionActor actor,
        string userName)
    {
        var created = await services.GetRequiredService<ICreateStaffAccount>().ExecuteAsync(
            new(
                actor,
                userName,
                "Temporary-Password-1",
                "PLAT-068 account",
                Guid.NewGuid().ToString("N")),
            default);
        await services.GetRequiredService<IAssignStaffRoles>().ExecuteAsync(
            new(
                actor,
                created.Account.Id,
                [StaffRole.Engineer],
                "PLAT-068 Engineer role",
                Guid.NewGuid().ToString("N")),
            default);
        return created.Account;
    }

    private static byte[] Png() =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

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

    [GeneratedRegex(
        "<script(?![^>]*\\bsrc=)[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InlineScriptRegex();
}
