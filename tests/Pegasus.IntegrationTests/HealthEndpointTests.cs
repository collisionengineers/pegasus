using Microsoft.AspNetCore.Mvc.Testing;

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
