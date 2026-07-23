using Microsoft.AspNetCore.Mvc.Testing;

namespace CollisionSpike.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
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

        Assert.Contains("Case intake", html, StringComparison.Ordinal);
        Assert.Contains("Needs sorting", html, StringComparison.Ordinal);
    }
}
