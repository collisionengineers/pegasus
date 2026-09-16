using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class IdentityCookieLifetimeWebTests
{
    private const string UserName = "identity-cookie-lifetime-user";
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task ValidationDoesNotReissueAnIdentityCookieButSlidingExpirationStillDoes()
    {
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        using var userLookupCounter = new UserLookupCommandCounter(testDatabase.DatabaseName);
        var clock = new AdjustableTimeProvider(
            new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var baseFactory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["ConnectionStrings:Pegasus"] = testDatabase.ConnectionString,
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false"
            });
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    options => options.TimeProvider = clock);
                services.PostConfigure<SecurityStampValidatorOptions>(
                    options => options.TimeProvider = clock);
            }));

        await CreateUserAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        await SignInAsync(client);

        // Advance past the sign-in instant so this request proves the
        // zero-interval SecurityStampValidator callback, not only the cookie
        // handler's ticket deserialization.
        clock.Advance(TimeSpan.FromSeconds(1));
        userLookupCounter.Reset();
        using var validationOnly = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, validationOnly.StatusCode);
        Assert.DoesNotContain(
            validationOnly.Headers.TryGetValues("Set-Cookie", out var validationCookies)
                ? validationCookies
                : Array.Empty<string>(),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        Assert.True(validationOnly.Headers.CacheControl?.NoStore);
        // One lookup belongs to zero-interval security-stamp validation and
        // one to the current password-change gate. This makes the test fail if
        // the controlled clock accidentally bypasses validation.
        Assert.Equal(2, userLookupCounter.ExecutedUserLookupCommands);

        var concurrent = await Task.WhenAll(
            client.GetAsync("/Account/PasswordChange"),
            client.GetAsync("/Account/PasswordChange"));
        try
        {
            Assert.All(concurrent, response =>
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.DoesNotContain(
                    response.Headers.TryGetValues("Set-Cookie", out var concurrentCookies)
                        ? concurrentCookies
                        : Array.Empty<string>(),
                    value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
            });
        }
        finally
        {
            foreach (var response in concurrent)
            {
                response.Dispose();
            }
        }

        // Half of the two-hour idle lifetime has passed, so the cookie handler
        // itself must still renew the session after validation has finished.
        clock.Advance(TimeSpan.FromMinutes(70));
        using var slidingRenewal = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, slidingRenewal.StatusCode);
        Assert.Contains(
            slidingRenewal.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        Assert.True(slidingRenewal.Headers.CacheControl?.NoStore);

        // Just before the two-hour boundary, the handler renews the still-valid
        // ticket. Letting the renewed ticket pass the boundary then expires it.
        clock.Advance(TimeSpan.FromMinutes(119) + TimeSpan.FromSeconds(59));
        using var justBeforeIdleExpiry = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, justBeforeIdleExpiry.StatusCode);
        Assert.Contains(
            justBeforeIdleExpiry.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));

        clock.Advance(TimeSpan.FromMinutes(121));
        using var idleExpired = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.Redirect, idleExpired.StatusCode);

        // A ticket is still valid at its exact expiry instant, where the
        // sliding policy refreshes it. A later request beyond that boundary is
        // the idle-expiry case above.
        using var exactIdleClient = CreateClient(factory);
        await SignInAsync(exactIdleClient);
        clock.Advance(TimeSpan.FromHours(2));
        using var exactIdleBoundary = await exactIdleClient.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, exactIdleBoundary.StatusCode);
        Assert.Contains(
            exactIdleBoundary.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));

        // A separate, continuously active session must still stop at the
        // eight-hour absolute boundary. Every prior request forces Identity to
        // refresh its principal, which proves the original-issue claim survives
        // those refreshes and continues to govern the session.
        using var absoluteClient = CreateClient(factory);
        await SignInAsync(absoluteClient);
        clock.Advance(TimeSpan.FromSeconds(1));
        for (var renewal = 0; renewal < 6; renewal++)
        {
            clock.Advance(TimeSpan.FromMinutes(70));
            using var continuedSession = await absoluteClient.GetAsync("/Account/PasswordChange");
            Assert.Equal(HttpStatusCode.OK, continuedSession.StatusCode);
            Assert.Contains(
                continuedSession.Headers.GetValues("Set-Cookie"),
                value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        }

        clock.Advance(TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(58));
        using var justBeforeAbsoluteExpiry = await absoluteClient.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, justBeforeAbsoluteExpiry.StatusCode);

        clock.Advance(TimeSpan.FromSeconds(1));
        using var absoluteExpired = await absoluteClient.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.Redirect, absoluteExpired.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static async Task CreateUserAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        await context.Database.MigrateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = UserName,
            IsEnabled = true,
            MustChangePassword = false,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.User)).Succeeded);
    }

    private static async Task SignInAsync(HttpClient client)
    {
        using var signInPage = await client.GetAsync("/Account/SignIn");
        signInPage.EnsureSuccessStatusCode();
        var signInHtml = await signInPage.Content.ReadAsStringAsync();
        using var signIn = await client.PostAsync(
            "/Account/SignIn",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ReadAntiforgeryToken(signInHtml),
                ["UserName"] = UserName,
                ["Password"] = Password,
                ["ReturnUrl"] = "/"
            }));
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Contains(
            signIn.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var tokenTag = AntiforgeryTagRegex().Match(html);
        Assert.True(tokenTag.Success, "The sign-in form must render an antiforgery token.");
        var tokenValue = InputValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The sign-in antiforgery token must have a value.");
        return WebUtility.HtmlDecode(tokenValue.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputValueRegex();

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan value) => current = current.Add(value);
    }

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
