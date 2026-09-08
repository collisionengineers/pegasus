using System.Net;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class TestUiFocusedRenderTests
{
    [Fact]
    public async Task AccountConfirmationAndEmptyAccountStatesRenderThroughRazor()
    {
        using (var populatedFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true))
        {
            Guid accountId;
            await using (var scope = populatedFactory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
                accountId = users.Users.Select(user => user.Id).First();
            }
            using var client = IntakeWebDriver.CreateClient(populatedFactory);
            foreach (var (operation, heading) in new[]
            {
                ("Disable", "Disable account"), ("Enable", "Enable account"),
                ("Delete", "Delete account"), ("ForceLogout", "Force logout"),
                ("ResetPassword", "Reset password")
            })
            {
                using var confirm = await client.GetAsync($"/Administration/Accounts/Confirm/{operation}/{accountId:D}");
                confirm.EnsureSuccessStatusCode();
                Assert.Contains(heading, await confirm.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }
        }

        using var emptyFactory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false);
        using var emptyClient = IntakeWebDriver.CreateClient(emptyFactory);
        using var empty = await emptyClient.GetAsync("/Administration/Accounts");
        empty.EnsureSuccessStatusCode();
        // PLAT-027: the consolidated Staff accounts & roles area states the
        // empty result and nothing else; the old sentence explained how
        // application initialization works, which is not the operator's
        // business.
        Assert.Contains("<h2>No staff accounts are available.</h2>", await empty.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeldLeaseConfirmationClearsOnlyTheCurrentLeaseThroughRazor()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await SendToAiIntegrationTests.SeedAcceptedCaseAsync(factory);
        var holderId = Guid.NewGuid();
        var administrator = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        CaseEditLease lease;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
            var holder = new PegasusIdentityUser
            {
                Id = holderId,
                UserName = $"held-lease-{holderId:N}"
            };
            Assert.True((await users.CreateAsync(holder, "LeaseTest!2031")).Succeeded);
            Assert.True((await users.AddToRoleAsync(holder, StaffRoleNames.User)).Succeeded);

            var workflow = await scope.ServiceProvider
                .GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(caseId, CancellationToken.None);
            Assert.NotNull(workflow);
            lease = await scope.ServiceProvider.GetRequiredService<IAcquireCaseEditLease>()
                .ExecuteAsync(
                    new(
                        caseId,
                        workflow.Version,
                        ActionActor.Staff(holderId, [StaffRole.User]),
                        $"held-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var forceLogoutHtml = await GetHtmlAsync(
            client,
            $"/Administration/Accounts/Confirm/ForceLogout/{holderId:D}");
        Assert.Contains("Open case edits", forceLogoutHtml, StringComparison.Ordinal);
        Assert.Contains(
            $"/Administration/Accounts/Confirm/ClearLease/{holderId:D}",
            forceLogoutHtml,
            StringComparison.Ordinal);

        using (var forceLogout = await client.PostAsync(
                   "/Administration/Accounts?handler=ForceLogout",
                   Form(
                       forceLogoutHtml,
                       ("staffId", holderId.ToString("D")),
                       ("operationKey", InputValue(forceLogoutHtml, "operationKey")),
                       ("reason", "End the held staff session."))))
        {
            Assert.Equal(HttpStatusCode.Found, forceLogout.StatusCode);
        }
        Assert.Contains(
            (await HeldLeasesAsync(factory, administrator, holderId)).Leases,
            item => item.CaseId == caseId && item.LeaseGeneration == lease.Generation);

        var clearLeaseHtml = await GetHtmlAsync(
            client,
            $"/Administration/Accounts/Confirm/ClearLease/{holderId:D}?caseId={caseId:D}&leaseGeneration={lease.Generation}");
        Assert.Contains("Clear case edit hold", clearLeaseHtml, StringComparison.Ordinal);

        using (var stale = await client.PostAsync(
                   "/Administration/Accounts?handler=ClearLease",
                   Form(
                       clearLeaseHtml,
                       ("staffId", holderId.ToString("D")),
                       ("caseId", caseId.ToString("D")),
                       ("expectedLeaseGeneration", (lease.Generation + 1).ToString(CultureInfo.InvariantCulture)),
                       ("operationKey", Guid.NewGuid().ToString("N")),
                       ("reason", "Reject the stale held-edit request."))))
        {
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
            Assert.Contains("The case edit hold changed.", await stale.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        Assert.Contains(
            (await HeldLeasesAsync(factory, administrator, holderId)).Leases,
            item => item.CaseId == caseId && item.LeaseGeneration == lease.Generation);

        using (var cleared = await client.PostAsync(
                   "/Administration/Accounts?handler=ClearLease",
                   Form(
                       clearLeaseHtml,
                       ("staffId", holderId.ToString("D")),
                       ("caseId", caseId.ToString("D")),
                       ("expectedLeaseGeneration", lease.Generation.ToString(CultureInfo.InvariantCulture)),
                       ("operationKey", InputValue(clearLeaseHtml, "operationKey")),
                       ("reason", "Clear the held edit after review."))))
        {
            Assert.Equal(HttpStatusCode.Found, cleared.StatusCode);
        }
        Assert.Empty((await HeldLeasesAsync(factory, administrator, holderId)).Leases);
    }

    [Theory]
    [InlineData("Health", "Health")]
    [InlineData("ActionLogs", "Action logs")]
    [InlineData("AiJobs", "AI jobs")]
    [InlineData("Reports", "Reports")]
    public async Task AdministrationMonitoringPagesRenderThroughTheirAuthorizedQueries(string route, string heading)
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync($"/Administration/{route}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(heading, html, StringComparison.Ordinal);
        if (route == "Reports")
        {
            Assert.Contains("value=\"2031-04-05T11:30", html, StringComparison.Ordinal);
            Assert.Contains("value=\"2031-05-06T11:30", html, StringComparison.Ordinal);
            Assert.Contains("Received to generated artifact", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task CaseUnavailableAndErrorStatesRenderThroughRazor()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGetCase>();
            services.AddSingleton<IGetCase, ThrowingGetCase>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var unavailable = await client.GetAsync($"/Cases/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("Case unavailable", await unavailable.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var error = await client.GetAsync("/Error");
        error.EnsureSuccessStatusCode();
        Assert.Contains("Something went wrong", await error.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenUnidentifiedDetailRendersThroughRazor()
    {
        // The repository PNG fixture with no readable registration is how an
        // image reaches Unidentified: automation abstains and the queued
        // processor registers the receipt itself, so the item carries a
        // retained source receipt and no test-authored domain text.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        UnidentifiedItem? item;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            item = await scope.ServiceProvider
                .GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        }
        Assert.NotNull(item);

        using var response = await client.GetAsync($"/Unidentified/{item.Id:D}");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Unidentified", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        // The offline snapshot shows this receipt's own image.
        using var image = await client.GetAsync($"/Received/{receiptId:D}/Image");
        image.EnsureSuccessStatusCode();
    }

    private sealed class ThrowingGetCase : IGetCase
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("test unavailable query");
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<GetStaffHeldCaseEditLeasesResult> HeldLeasesAsync(
        IntakeWebApplicationFactory factory,
        ActionActor administrator,
        Guid holderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGetStaffHeldCaseEditLeases>()
            .ExecuteAsync(new(administrator, holderId), CancellationToken.None);
    }

    private static FormUrlEncodedContent Form(
        string html,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken");
        return new FormUrlEncodedContent(fields);
    }

    private static string InputValue(string html, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            $"<input[^>]*name=\"{System.Text.RegularExpressions.Regex.Escape(name)}\"[^>]*value=\"(?<value>[^\"]*)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"The form did not render an input named '{name}'.");
        return System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value);
    }
}
