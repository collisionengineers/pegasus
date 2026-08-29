using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class TestUiFocusedRenderTests
{
    [Fact]
    public async Task AccountEditAndEmptyAccountStatesRenderThroughRazor()
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
            using var edit = await client.GetAsync($"/Administration/Accounts/Edit/{accountId:D}");
            edit.EnsureSuccessStatusCode();
            Assert.Contains("Manage ", await edit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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
        Assert.Contains("<h2>No staff accounts</h2>", await empty.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var register = scope.ServiceProvider.GetRequiredService<IRegisterUnidentified>();
            var result = await register.ExecuteAsync(new(
                UnidentifiedOrigin.SubmissionGroup(Guid.NewGuid()),
                UnidentifiedReasonCode.NoUsableIdentification,
                "test detail",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-test:{Guid.NewGuid():N}",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero)));
            id = result.Item.Id;
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync($"/Unidentified/{id:D}");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Unidentified", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private sealed class ThrowingGetCase : IGetCase
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("test unavailable query");
    }
}
