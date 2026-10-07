using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The DevelopmentOffline opt-ins that turn the local fixture into a hosted
/// test instance: password sign-in for the role matrix, and the custody flag
/// that must never resolve beside the local store.
/// </summary>
public sealed partial class DevelopmentOfflineLiveOptInTests
{
    private const string AdministratorPassword = "Local-Test-Instance-2026!";

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task PasswordSignInHandsTheOfflineHostToTheIdentityCookie()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"pegasus-password-sign-in-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        try
        {
            using var factory = new ConfiguredWebApplicationFactory(
                "Development",
                new Dictionary<string, string?>
                {
                    ["Runtime:Profile"] = "DevelopmentOffline",
                    ["ConnectionStrings:Pegasus"] = database.ConnectionString,
                    ["Intake:LocalArtifactPath"] = Path.Combine(workingDirectory, "intake"),
                    ["Features:LocalIntake"] = "false",
                    ["Features:PasswordSignIn"] = "true",
                    ["DevelopmentOffline:AdministratorPassword"] = AdministratorPassword
                });
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider);
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });

            // The automatic scheme no longer answers: an anonymous request is
            // challenged to the sign-in page instead of becoming the Administrator.
            using var anonymous = await client.GetAsync("/Cases");
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            Assert.Equal(
                "/Account/SignIn",
                new Uri(client.BaseAddress!, anonymous.Headers.Location!).AbsolutePath);

            using var signInPage = await client.GetAsync("/Account/SignIn");
            var signInHtml = await signInPage.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, signInPage.StatusCode);

            using var signedIn = await client.PostAsync(
                "/Account/SignIn",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = ReadAntiforgeryToken(signInHtml),
                    ["UserName"] = DevelopmentOfflineIdentity.UserName,
                    ["Password"] = AdministratorPassword,
                    ["ReturnUrl"] = "/"
                }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            Assert.Contains(
                signedIn.Headers.GetValues("Set-Cookie"),
                value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));

            using var administration = await client.GetAsync("/Administration");
            Assert.Equal(HttpStatusCode.OK, administration.StatusCode);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PasswordSignInWithoutAPasswordRefusesToInitialize()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"pegasus-password-sign-in-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        try
        {
            using var factory = new ConfiguredWebApplicationFactory(
                "Development",
                new Dictionary<string, string?>
                {
                    ["Runtime:Profile"] = "DevelopmentOffline",
                    ["Intake:LocalArtifactPath"] = Path.Combine(workingDirectory, "intake"),
                    ["Features:LocalIntake"] = "false",
                    ["Features:PasswordSignIn"] = "true"
                });
            using var scope = factory.Services.CreateScope();

            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider));

            Assert.Contains(
                "DevelopmentOffline:AdministratorPassword is required",
                error.GetBaseException().Message,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void LiveBoxCustodyAndLocalDocumentCustodyTogetherFailAtStartNamingBoth()
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Development",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "DevelopmentOffline",
                ["Intake:LocalArtifactPath"] = Path.Combine(Path.GetTempPath(), "pegasus-custody-conflict"),
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "true",
                ["Features:LiveBoxCustody"] = "true"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var configurationException = Assert.IsType<InvalidOperationException>(exception.GetBaseException());

        Assert.Equal(
            "Features:LocalDocumentCustody and Features:LiveBoxCustody name two custody stores; configure one.",
            configurationException.Message);
    }

    [Theory]
    [InlineData("Features:LiveVehicleLookup")]
    [InlineData("Features:LiveBoxCustody")]
    [InlineData("Features:LiveGlass")]
    [InlineData("Features:PasswordSignIn")]
    public void ProductionRefusesTheLocalLiveOptIns(string key)
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false",
                [key] = "true"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var configurationException = Assert.IsType<InvalidOperationException>(exception.GetBaseException());

        Assert.Equal($"{key} requires the DevelopmentOffline runtime profile.", configurationException.Message);
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
}
