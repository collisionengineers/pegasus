using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pegasus.Web.Health;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class HealthEndpointTests : IClassFixture<IntakeWebApplicationFactory>
{
    private readonly IntakeWebApplicationFactory factory;

    public HealthEndpointTests(IntakeWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/warm")]
    public async Task HealthEndpointReturnsSuccess(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReadinessWaitsForTheStartupWarmup()
    {
        var state = new StartupWarmupState(warms: true);
        var check = new StartupWarmupHealthCheck(state);

        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
        state.Complete();
        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
    }

    [Fact]
    public async Task AWarmingInstanceBecomesReadyOnceItsWarmupEnds()
    {
        using var warming = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Startup:Warmup"] = "true"
                })));
        using var client = warming.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // The warm-up is bounded, so readiness arrives well inside a minute.
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
        HttpStatusCode status;
        do
        {
            using var response = await client.GetAsync("/health/ready");
            status = response.StatusCode;
            if (status != HttpStatusCode.OK)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }
        while (status != HttpStatusCode.OK && DateTimeOffset.UtcNow < deadline);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public void TheKeepWarmIntervalIsThreeMinutesUnlessConfigured()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(3),
            factory.Services.GetRequiredService<StartupWarmupState>().KeepWarmInterval);

        // Zero is the once-only setting: the warm-up runs its first pass and ends.
        using var once = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Startup:WarmupInterval"] = "00:00:00"
                })));
        Assert.Equal(
            TimeSpan.Zero,
            once.Services.GetRequiredService<StartupWarmupState>().KeepWarmInterval);
    }

    [Theory]
    [InlineData(null, 180, null)]
    [InlineData(" ", 180, null)]
    [InlineData("00:05:00", 300, null)]
    [InlineData("00:00:00", 0, null)]
    [InlineData("banana", 180, "banana")]
    [InlineData("00:00:00.5", 180, "00:00:00.5")]
    // Only 00:00:00 means once only; a negative span is a mistake, not a switch.
    [InlineData("-00:03:00", 180, "-00:03:00")]
    [InlineData("-00:00:00.0000001", 180, "-00:00:00.0000001")]
    public void AnUnusableKeepWarmIntervalKeepsTheDefaultAndIsRemembered(
        string? text, int expectedSeconds, string? unusable)
    {
        var state = StartupWarmupState.FromSettings(warms: true, text);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), state.KeepWarmInterval);
        Assert.Equal(unusable, state.UnusableIntervalSetting);
    }

    [Fact]
    public async Task LandingPageExposesCaseIntakeWorkspace()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var html = await client.GetStringAsync("/");

        // The landing page exposes the case-intake workspace: the Work
        // Centre's heading, the one action that creates a Case, and the
        // metrics that open each queue directly.
        Assert.Contains("Work Centre", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Cases/Create\"", html, StringComparison.Ordinal);
        Assert.Contains("data-value=\"not_ready\" href=\"/Cases?tab=not_ready\"", html, StringComparison.Ordinal);
        Assert.Contains("data-value=\"unidentified\" href=\"/Cases?tab=unidentified\"", html, StringComparison.Ordinal);
        Assert.Contains("Unidentified", html, StringComparison.Ordinal);
    }
}
