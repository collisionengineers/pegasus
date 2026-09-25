using System.Net;
using Pegasus.Core.Workflow;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class StaffSignInSecurityTests
{
    private const string UserName = "sign-in-audit-user";
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task DeniedAttemptIsRetainedAndSuccessfulCookieSignInWritesOneSuccessEvent()
    {
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        using var userLookupCounter = new UserLookupCommandCounter(testDatabase.DatabaseName);
        var subjectId = Guid.NewGuid();
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["ConnectionStrings:Pegasus"] = testDatabase.ConnectionString,
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false"
            });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            await context.Database.MigrateAsync();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = new PegasusIdentityUser
            {
                Id = subjectId,
                UserName = UserName,
                IsEnabled = true,
                MustChangePassword = false,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var result = await userManager.CreateAsync(
                user,
                Password);
            Assert.True(
                result.Succeeded,
                string.Join(", ", result.Errors.Select(error => error.Description)));
            Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.User)).Succeeded);
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });
        using var signInPage = await client.GetAsync("/Account/SignIn");
        var signInHtml = await signInPage.Content.ReadAsStringAsync();
        Assert.True(
            signInPage.StatusCode == HttpStatusCode.OK,
            $"Expected the anonymous sign-in page, but received {(int)signInPage.StatusCode} " +
            $"with Location '{signInPage.Headers.Location}'.");
        // v30 sign-in B: the identity panel names the product and the company,
        // the card reads "Sign in" and the password field carries Show / Hide.
        Assert.Contains("class=\"auth-identity\"", signInHtml, StringComparison.Ordinal);
        Assert.Contains("Collision Engineers", signInHtml, StringComparison.Ordinal);
        Assert.Contains("<h1>Sign in</h1>", signInHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign in to Pegasus", signInHtml, StringComparison.Ordinal);
        Assert.Contains("data-password-reveal aria-controls=\"Password\"", signInHtml, StringComparison.Ordinal);

        using var signedOutPage = await client.GetAsync("/Account/SignIn?signedOut=true");
        signedOutPage.EnsureSuccessStatusCode();
        Assert.Contains(
            "You are signed out",
            await signedOutPage.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var deniedResponse = await client.PostAsync(
            "/Account/SignIn",
            CreateSignInForm(ReadAntiforgeryToken(signInHtml), "incorrect password"));
        var deniedHtml = await deniedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, deniedResponse.StatusCode);
        Assert.Contains("The username or password is incorrect.", deniedHtml, StringComparison.Ordinal);
        // The refusal reads as the shared danger notice, once, above the form.
        Assert.Contains("class=\"notice notice--danger auth-notice\" role=\"alert\"", deniedHtml, StringComparison.Ordinal);

        using var successResponse = await client.PostAsync(
            "/Account/SignIn",
            CreateSignInForm(ReadAntiforgeryToken(deniedHtml), Password));
        Assert.Equal(HttpStatusCode.Redirect, successResponse.StatusCode);
        Assert.Contains(
            successResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));

        Assert.Equal(
            1L,
            await CountSignInEventsAsync(
                testDatabase,
                subjectId,
                outcome: "Denied",
                reasonCode: "invalid_credentials"));
        Assert.Equal(
            1L,
            await CountSignInEventsAsync(
                testDatabase,
                subjectId,
                outcome: "Succeeded",
                reasonCode: null));

        userLookupCounter.Reset();
        using var authenticatedRequest = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, authenticatedRequest.StatusCode);
        Assert.DoesNotContain(
            authenticatedRequest.Headers.TryGetValues("Set-Cookie", out var authenticatedCookies)
                ? authenticatedCookies
                : Array.Empty<string>(),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        Assert.Equal(1, userLookupCounter.ExecutedUserLookupCommands);

        var administratorId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var administrator = ActionActor.Staff(administratorId, [StaffRole.Administrator]);
            var user = await scope.ServiceProvider.GetRequiredService<PegasusDbContext>().Users
                .AsNoTracking()
                .SingleAsync(item => item.Id == subjectId);
            await scope.ServiceProvider.GetRequiredService<IForceStaffLogout>().ExecuteAsync(
                new(
                    administrator,
                    subjectId,
                    "Security recovery",
                    "force-logout-next-request",
                    user.Version),
                default);
        }

        // Every security event names the principal that acted. A sign-in is taken
        // by the staff member signing in; a forced logout is taken by the
        // operator, not by the account it landed on — which is the whole reason
        // the Action logs view could not attribute a Security-area row before.
        Assert.Equal(
            1L,
            await CountAttributedSecurityEventsAsync(
                testDatabase,
                type: "SignIn",
                subjectId: subjectId.ToString("D"),
                actorKind: "Staff",
                actorSubjectId: subjectId.ToString("D")));
        Assert.Equal(
            1L,
            await CountAttributedSecurityEventsAsync(
                testDatabase,
                type: "SecurityStampChanged",
                subjectId: subjectId.ToString("D"),
                actorKind: "Staff",
                actorSubjectId: administratorId.ToString("D")));

        userLookupCounter.Reset();
        using var rejectedOldCookie = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.Redirect, rejectedOldCookie.StatusCode);
        var signInRedirect = new Uri(client.BaseAddress!, rejectedOldCookie.Headers.Location!);
        Assert.Equal(client.BaseAddress!.Authority, signInRedirect.Authority);
        Assert.Equal("/Account/SignIn", signInRedirect.AbsolutePath);
        Assert.Equal(1, userLookupCounter.ExecutedUserLookupCommands);
    }

    [Fact]
    public async Task ForcedPasswordChangeAsksOnlyForTheNewPassword()
    {
        // An Administrator issued this account's password, so the forced-change
        // screen replaces it without asking the account to prove it: two boxes,
        // new password and confirmation. A voluntary change keeps the proof.
        const string issuedPassword = "issued-by-administrator";
        const string chosenPassword = "chosen-by-the-account";
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        var subjectId = Guid.NewGuid();
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["ConnectionStrings:Pegasus"] = testDatabase.ConnectionString,
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false"
            });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            await context.Database.MigrateAsync();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = new PegasusIdentityUser
            {
                Id = subjectId,
                UserName = UserName,
                IsEnabled = true,
                MustChangePassword = true,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var created = await userManager.CreateAsync(user, issuedPassword);
            Assert.True(
                created.Succeeded,
                string.Join(", ", created.Errors.Select(error => error.Description)));
            Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.User)).Succeeded);
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });
        using var signInPage = await client.GetAsync("/Account/SignIn");
        var signInHtml = await signInPage.Content.ReadAsStringAsync();
        using var signedIn = await client.PostAsync(
            "/Account/SignIn",
            CreateSignInForm(ReadAntiforgeryToken(signInHtml), issuedPassword));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/Account/PasswordChange", signedIn.Headers.Location?.OriginalString);

        // The gate holds every other destination until the password is replaced.
        using var gated = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, gated.StatusCode);
        Assert.Equal("/Account/PasswordChange", gated.Headers.Location?.OriginalString);

        using var forcedPage = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, forcedPage.StatusCode);
        var forcedHtml = await forcedPage.Content.ReadAsStringAsync();
        Assert.Contains("Set a new password before continuing", forcedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"CurrentPassword\"", forcedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"NewPassword\"", forcedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"ConfirmPassword\"", forcedHtml, StringComparison.Ordinal);

        // Choosing the issued password again would leave the gate's purpose unmet.
        using var unchanged = await client.PostAsync(
            "/Account/PasswordChange",
            CreatePasswordChangeForm(forcedHtml, issuedPassword));
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        var unchangedHtml = await unchanged.Content.ReadAsStringAsync();
        Assert.Contains(
            "The new password must be different from the current one.",
            unchangedHtml,
            StringComparison.Ordinal);

        using var changed = await client.PostAsync(
            "/Account/PasswordChange",
            CreatePasswordChangeForm(unchangedHtml, chosenPassword));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal("/", changed.Headers.Location?.OriginalString);

        // The account is no longer gated, and its next change is voluntary,
        // so the current-password proof is back.
        using var released = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        using var voluntaryPage = await client.GetAsync("/Account/PasswordChange");
        var voluntaryHtml = await voluntaryPage.Content.ReadAsStringAsync();
        Assert.Contains("name=\"CurrentPassword\"", voluntaryHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Set a new password before continuing", voluntaryHtml, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(subjectId.ToString("D"));
            Assert.NotNull(user);
            Assert.False(user.MustChangePassword);
            Assert.True(await userManager.CheckPasswordAsync(user, chosenPassword));
            Assert.False(await userManager.CheckPasswordAsync(user, issuedPassword));
        }

        Assert.Equal(
            1L,
            await CountAttributedSecurityEventsAsync(
                testDatabase,
                type: "PasswordChanged",
                subjectId: subjectId.ToString("D"),
                actorKind: "Staff",
                actorSubjectId: subjectId.ToString("D")));
    }

    private static FormUrlEncodedContent CreatePasswordChangeForm(
        string pageHtml,
        string newPassword) =>
        new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ReadAntiforgeryToken(pageHtml),
            ["OperationKey"] = ReadInputValue(pageHtml, "OperationKey"),
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword
        });

    private static string ReadInputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The form must render '{name}'.");
        var value = InputValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The form input '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static FormUrlEncodedContent CreateSignInForm(
        string antiforgeryToken,
        string password) =>
        new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["UserName"] = UserName,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        });

    private static async Task<long> CountAttributedSecurityEventsAsync(
        LocalDbTestDatabase database,
        string type,
        string subjectId,
        string actorKind,
        string actorSubjectId)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM SecurityEvents " +
            "WHERE Type = @type AND SubjectId = @subjectId " +
            "AND ActorKind = @actorKind AND ActorSubjectId = @actorSubjectId;";
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@subjectId", subjectId);
        command.Parameters.AddWithValue("@actorKind", actorKind);
        command.Parameters.AddWithValue("@actorSubjectId", actorSubjectId);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var tokenTag = AntiforgeryTagRegex().Match(html);
        Assert.True(tokenTag.Success, "The sign-in form must render an antiforgery token.");
        var tokenValue = InputValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The sign-in antiforgery token must have a value.");
        return WebUtility.HtmlDecode(tokenValue.Groups["value"].Value);
    }

    private static async Task<long> CountSignInEventsAsync(
        LocalDbTestDatabase database,
        Guid subjectId,
        string outcome,
        string? reasonCode)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM SecurityEvents " +
            "WHERE Type = 'SignIn' AND Outcome = @outcome AND SubjectId = @subjectId " +
            "AND ((@reasonCode IS NULL AND ReasonCode IS NULL) OR ReasonCode = @reasonCode);";
        command.Parameters.AddWithValue("@outcome", outcome);
        command.Parameters.AddWithValue("@subjectId", subjectId.ToString("D"));
        command.Parameters.AddWithValue("@reasonCode", (object?)reasonCode ?? DBNull.Value);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputValueRegex();

    private sealed class UserLookupCommandCounter :
        IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly string databaseName;
        private readonly IDisposable allListenersSubscription;
        private readonly List<IDisposable> listenerSubscriptions = [];
        private int executedUserLookupCommands;

        public UserLookupCommandCounter(string databaseName)
        {
            this.databaseName = databaseName;
            allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public int ExecutedUserLookupCommands =>
            Volatile.Read(ref executedUserLookupCommands);

        public void Reset() => Interlocked.Exchange(ref executedUserLookupCommands, 0);

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == DbLoggerCategory.Name)
            {
                listenerSubscriptions.Add(listener.Subscribe(
                    this,
                    eventName => eventName == RelationalEventId.CommandExecuted.Name));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is CommandExecutedEventData eventData
                && string.Equals(
                    eventData.Command.Connection?.Database,
                    databaseName,
                    StringComparison.Ordinal)
                && eventData.Command.CommandText.Contains(
                    "[AspNetUsers]",
                    StringComparison.Ordinal))
            {
                Interlocked.Increment(ref executedUserLookupCommands);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            allListenersSubscription.Dispose();
            foreach (var subscription in listenerSubscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
