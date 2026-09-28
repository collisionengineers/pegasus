using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
    private const string AdministratorName = "identity-cookie-lifetime-administrator";

    [Fact]
    public async Task ValidationDoesNotReissueAnIdentityCookieButSlidingExpirationStillDoes()
    {
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        var principals = new PrincipalBuildCounter();
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
                CountPrincipalBuilds(services, principals);
            }));

        await CreateUserAsync(factory, UserName, StaffRole.User);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        await SignInAsync(client);
        Assert.Equal(1, principals.Builds);

        // Advance past the sign-in instant so this request proves the
        // per-request account check, not only the cookie handler's ticket
        // deserialization. The check never rebuilds the principal.
        clock.Advance(TimeSpan.FromSeconds(1));
        using var validationOnly = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, validationOnly.StatusCode);
        Assert.DoesNotContain(
            validationOnly.Headers.TryGetValues("Set-Cookie", out var validationCookies)
                ? validationCookies
                : Array.Empty<string>(),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        Assert.True(validationOnly.Headers.CacheControl?.NoStore);
        Assert.Equal(1, principals.Builds);

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
            Assert.Equal(1, principals.Builds);
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
        // eight-hour absolute boundary. The sliding renewals reissue the cookie,
        // which proves the original-issue claim survives them and continues to
        // govern the session.
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

    [Fact]
    public async Task ARoleChangeRefusesTheSessionOnTheNextRequest()
    {
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        var principals = new PrincipalBuildCounter();
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
            builder.ConfigureServices(services => CountPrincipalBuilds(services, principals)));
        var staffId = await CreateUserAsync(factory, UserName, StaffRole.User);
        var administratorId = await CreateUserAsync(factory, AdministratorName, StaffRole.Administrator);
        using var client = CreateClient(factory);
        await SignInAsync(client);
        using var before = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var version = await scope.ServiceProvider.GetRequiredService<PegasusDbContext>().Users
                .AsNoTracking()
                .Where(user => user.Id == staffId)
                .Select(user => user.Version)
                .SingleAsync();
            await scope.ServiceProvider.GetRequiredService<IUpdateStaffAccountSettings>().ExecuteAsync(
                new(
                    ActionActor.Staff(administratorId, [StaffRole.Administrator]),
                    staffId,
                    StaffRole.Engineer,
                    IsSignOffEngineer: false,
                    PrintedName: null,
                    Qualifications: null,
                    Signature: null,
                    IsDefaultSignOffEngineer: false,
                    OperationKey: "identity-cookie-lifetime-role-change",
                    ExpectedVersion: version),
                default);
        }

        // The old cookie still names the User role. It is refused, never
        // refreshed into the new role.
        using var after = await client.GetAsync("/Account/PasswordChange");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains(
            "/Account/SignIn",
            after.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, principals.Builds);
    }

    /// <summary>
    /// Counts every principal Identity builds for a staff account. Sign-in
    /// builds one; the per-request check must build none.
    /// </summary>
    private static void CountPrincipalBuilds(IServiceCollection services, PrincipalBuildCounter counter)
    {
        services.RemoveAll<IUserClaimsPrincipalFactory<PegasusIdentityUser>>();
        services.AddScoped<UserClaimsPrincipalFactory<PegasusIdentityUser, IdentityRole<Guid>>>();
        services.AddScoped<IUserClaimsPrincipalFactory<PegasusIdentityUser>>(provider =>
            new CountingPrincipalFactory(
                provider.GetRequiredService<UserClaimsPrincipalFactory<PegasusIdentityUser, IdentityRole<Guid>>>(),
                counter));
    }

    private sealed class PrincipalBuildCounter
    {
        private int builds;

        public int Builds => Volatile.Read(ref builds);

        public void Increment() => Interlocked.Increment(ref builds);
    }

    private sealed class CountingPrincipalFactory(
        IUserClaimsPrincipalFactory<PegasusIdentityUser> inner,
        PrincipalBuildCounter counter) : IUserClaimsPrincipalFactory<PegasusIdentityUser>
    {
        public Task<System.Security.Claims.ClaimsPrincipal> CreateAsync(PegasusIdentityUser user)
        {
            counter.Increment();
            return inner.CreateAsync(user);
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static async Task<Guid> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        StaffRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        await context.Database.MigrateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = true,
            MustChangePassword = false,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        Assert.True((await userManager.AddToRoleAsync(user, role switch
        {
            StaffRole.Administrator => StaffRoleNames.Administrator,
            StaffRole.Engineer => StaffRoleNames.Engineer,
            _ => StaffRoleNames.User
        })).Succeeded);
        return user.Id;
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
}
