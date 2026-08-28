using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
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
        Assert.Contains("No staff accounts are available.", await empty.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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
}
