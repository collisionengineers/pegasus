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

    /// <summary>
    /// Features:LiveGraphMail composes the Web's Graph mail side over an app
    /// registration credential, as Production does: the resolver and staff send,
    /// with the offline Unavailable senders gone rather than shadowed (ADR-0088
    /// as amended, R675).
    /// </summary>
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task LiveGraphMailComposesTheGraphReadsAndStaffSend()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        using var factory = new ConfiguredWebApplicationFactory(
            "Development",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "DevelopmentOffline",
                ["ConnectionStrings:Pegasus"] = database.ConnectionString,
                ["Intake:LocalArtifactPath"] = Path.Combine(Path.GetTempPath(), $"pegasus-live-graph-mail-{Guid.NewGuid():N}"),
                ["Features:LocalIntake"] = "false",
                ["Features:LiveGraphMail"] = "true",
                ["Graph:ClientId"] = "4f0d4c7e-1b2a-4c3d-9e8f-0a1b2c3d4e5f",
                ["Graph:ClientSecret"] = "local-test-instance-secret"
            });

        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Equal(
            "Pegasus.Infrastructure.Email.GraphApprovedMailboxResolver",
            scope.ServiceProvider.GetRequiredService<Pegasus.Core.Identity.IResolveApprovedMailboxIdentity>().GetType().FullName);
        Assert.Equal(
            "Azure.Identity.ClientSecretCredential",
            scope.ServiceProvider.GetRequiredService<Azure.Core.TokenCredential>().GetType().FullName);
        Assert.IsType<Pegasus.Core.Operations.StaffMailSend>(
            scope.ServiceProvider.GetRequiredService<Pegasus.Core.Operations.IStaffMailSend>());
        Assert.IsType<Pegasus.Core.Operations.StaffReportSend>(
            scope.ServiceProvider.GetRequiredService<Pegasus.Core.Operations.IStaffReportSend>());
        Assert.Single(scope.ServiceProvider.GetServices<Pegasus.Core.Operations.IStaffMailSend>());
        Assert.Single(scope.ServiceProvider.GetServices<Pegasus.Core.Operations.IStaffReportSend>());
    }

    /// <summary>
    /// Features:LiveGuideValuation keeps Brego's session cookie in the run's
    /// Azurite account, as Production keeps it in custody storage, so a Web
    /// start reuses it instead of signing in through Brego's device-approval
    /// challenge (R933, #2062).
    /// </summary>
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task LiveGuideValuationKeepsTheBregoSessionInABlobStore()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        using var factory = new ConfiguredWebApplicationFactory(
            "Development",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "DevelopmentOffline",
                ["ConnectionStrings:Pegasus"] = database.ConnectionString,
                ["Intake:LocalArtifactPath"] = Path.Combine(Path.GetTempPath(), $"pegasus-live-guide-valuation-{Guid.NewGuid():N}"),
                ["Features:LocalIntake"] = "false",
                ["Features:LiveGlass"] = "true",
                ["Features:LiveGuideValuation"] = "true",
                ["Glass:MarketValueAssessorBaseUri"] = "https://mva.test/",
                ["Glass:EstimatorBaseUri"] = "https://ere.test/",
                ["Glass:CallbackBaseUri"] = "https://pegasus.test/",
                ["Glass:RepairProfileId"] = "4063",
                ["Glass:ValuationAccount:Username"] = "valuation-test",
                ["Glass:ValuationAccount:Password"] = "synthetic-password",
                ["Glass:AutomationAccount:Username"] = "automation-test",
                ["Glass:AutomationAccount:Password"] = "synthetic-automation-password",
                ["Cazana:CompanionAccount:Email"] = "companion@example.test",
                ["Cazana:CompanionAccount:Password"] = "synthetic-cazana-password",
                ["Brego:ValuationAccount:Email"] = "brego@example.test",
                ["Brego:ValuationAccount:Password"] = "synthetic-brego-password"
            });

        await using var scope = factory.Services.CreateAsyncScope();

        Assert.IsType<Pegasus.Infrastructure.Brego.AzureBlobBregoSessionStore>(
            scope.ServiceProvider.GetRequiredService<Pegasus.Infrastructure.Brego.IBregoSessionStore>());
        Assert.Contains(
            scope.ServiceProvider.GetServices<Pegasus.Core.Assessment.IGuideValuationProvider>(),
            provider => provider is Pegasus.Infrastructure.Brego.BregoGuideValuationProvider);
    }

    /// <summary>
    /// Features:LiveExperian composes Experian's two acts on an offline host
    /// (the hosted test instance), as Production does: the vehicle history
    /// check and CAP's guide valuation, each on its own account (ADR-0089,
    /// R1201, R1215).
    /// </summary>
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task LiveExperianComposesTheHistoryCheckAndTheCapValuation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        using var factory = new ConfiguredWebApplicationFactory(
            "Development",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "DevelopmentOffline",
                ["ConnectionStrings:Pegasus"] = database.ConnectionString,
                ["Intake:LocalArtifactPath"] = Path.Combine(Path.GetTempPath(), $"pegasus-live-experian-{Guid.NewGuid():N}"),
                ["Features:LocalIntake"] = "false",
                ["Features:LiveExperian"] = "true"
            });

        await using var scope = factory.Services.CreateAsyncScope();

        Assert.IsType<Pegasus.Infrastructure.Experian.ExperianCapValuationProvider>(
            Assert.Single(scope.ServiceProvider.GetServices<Pegasus.Core.Assessment.IGuideValuationProvider>()));
        Assert.IsType<Pegasus.Infrastructure.Experian.ExperianVehicleHistoryProvider>(
            Assert.Single(scope.ServiceProvider.GetServices<Pegasus.Core.Vehicle.IVehicleHistoryCheckProvider>()));
    }

    [Fact]
    public void LiveGraphMailWithoutAClientSecretFailsAtStartNamingTheKey()
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Development",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "DevelopmentOffline",
                ["Intake:LocalArtifactPath"] = Path.Combine(Path.GetTempPath(), "pegasus-live-graph-mail-no-secret"),
                ["Features:LocalIntake"] = "false",
                ["Features:LiveGraphMail"] = "true",
                ["Graph:ClientId"] = "4f0d4c7e-1b2a-4c3d-9e8f-0a1b2c3d4e5f"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var configurationException = Assert.IsType<InvalidOperationException>(exception.GetBaseException());

        Assert.Equal("Graph:ClientSecret is required for Features:LiveGraphMail.", configurationException.Message);
    }

    /// <summary>
    /// The Automation's own Glass's login (R1177) is required wherever
    /// Glass's is composed: a host without it stops at start naming the key,
    /// not at the Automation's first start.
    /// </summary>
    [Theory]
    [InlineData("Glass:AutomationAccount:Username")]
    [InlineData("Glass:AutomationAccount:Password")]
    public void LiveGlassWithoutTheAutomationLoginFailsAtStartNamingTheKey(string missingKey)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Runtime:Profile"] = "DevelopmentOffline",
            ["Intake:LocalArtifactPath"] = Path.Combine(Path.GetTempPath(), "pegasus-live-glass-no-automation-login"),
            ["Features:LocalIntake"] = "false",
            ["Features:LiveGlass"] = "true",
            ["Glass:AutomationAccount:Username"] = "automation-test",
            ["Glass:AutomationAccount:Password"] = "synthetic-automation-password",
        };
        settings[missingKey] = null;
        using var factory = new ConfiguredWebApplicationFactory("Development", settings);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var configurationException = Assert.IsType<InvalidOperationException>(exception.GetBaseException());

        Assert.Equal($"{missingKey} is required for Features:LiveGlass.", configurationException.Message);
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
    [InlineData("Features:LiveGuideValuation")]
    [InlineData("Features:LiveExperian")]
    [InlineData("Features:PasswordSignIn")]
    [InlineData("Features:LiveGraphMail")]
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
