using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class StaticAssetCacheWebTests
{
    private const string UserName = "static-asset-cache-user";
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task ASlidingIdentityCookieDoesNotChangeFingerprintedAssetCaching()
    {
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var baseFactory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["ConnectionStrings:Pegasus"] = testDatabase.ConnectionString,
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false"
            });
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.PostConfigure<CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme,
                options => options.TimeProvider = clock);
        }));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            await context.Database.MigrateAsync();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
            var result = await userManager.CreateAsync(
                new PegasusIdentityUser
                {
                    Id = Guid.NewGuid(),
                    UserName = UserName,
                    IsEnabled = true,
                    MustChangePassword = false,
                    LockoutEnabled = false,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                },
                Password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        using var signInPage = await client.GetAsync("/Account/SignIn");
        signInPage.EnsureSuccessStatusCode();
        var signInHtml = await signInPage.Content.ReadAsStringAsync();
        var assetMatch = FingerprintedSiteCssRegex().Match(signInHtml);
        Assert.True(assetMatch.Success, "The production sign-in page must reference fingerprinted site CSS.");

        using var signedIn = await client.PostAsync(
            "/Account/SignIn",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ReadAntiforgeryToken(signInHtml),
                ["UserName"] = UserName,
                ["Password"] = Password,
                ["ReturnUrl"] = "/"
            }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Contains(
            signedIn.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));

        // Sliding renewal is due after half of the two-hour idle lifetime. The
        // request therefore carries a valid, renewal-eligible application cookie.
        clock.Advance(TimeSpan.FromMinutes(70));

        using var asset = await client.GetAsync(assetMatch.Groups["path"].Value);
        asset.EnsureSuccessStatusCode();
        Assert.False(asset.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Equal("max-age=31536000, immutable", asset.Headers.CacheControl?.ToString());

        // The same cookie still reaches authentication for protected routes and
        // therefore renews when it is eligible.
        using var protectedPage = await client.GetAsync("/Account/PasswordChange");
        protectedPage.EnsureSuccessStatusCode();
        Assert.Contains(
            protectedPage.Headers.GetValues("Set-Cookie"),
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

    [GeneratedRegex("href=\"(?<path>/css/site\\.[a-z0-9]+\\.css)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FingerprintedSiteCssRegex();

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan value) => current = current.Add(value);
    }
}
