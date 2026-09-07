using System.Net;
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
            var result = await userManager.CreateAsync(
                new PegasusIdentityUser
                {
                    Id = subjectId,
                    UserName = UserName,
                    IsEnabled = true,
                    MustChangePassword = false,
                    LockoutEnabled = false,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                },
                Password);
            Assert.True(
                result.Succeeded,
                string.Join(", ", result.Errors.Select(error => error.Description)));
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
        Assert.Equal(1, userLookupCounter.ExecutedUserLookupCommands);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IForceStaffLogout>().ExecuteAsync(
                new(
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    subjectId,
                    "Security recovery",
                    "force-logout-next-request"),
                default);
        }

        userLookupCounter.Reset();
        using var rejectedOldCookie = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.Redirect, rejectedOldCookie.StatusCode);
        var signInRedirect = new Uri(client.BaseAddress!, rejectedOldCookie.Headers.Location!);
        Assert.Equal(client.BaseAddress!.Authority, signInRedirect.Authority);
        Assert.Equal("/Account/SignIn", signInRedirect.AbsolutePath);
        Assert.Equal(1, userLookupCounter.ExecutedUserLookupCommands);
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
