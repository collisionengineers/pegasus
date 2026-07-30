using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class StaffMcpSecurityTests
{
    private const int AnonymousAttempts = 61;

    [Fact]
    public async Task McpTransportRejectsABrowserCookieAndAdvertisesBearerMetadata()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var request = CreateInitializeRequest();
        request.Headers.TryAddWithoutValidation("Cookie", "__Host-Pegasus=browser-session");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        var challengeParameters = Assert.IsType<string>(challenge.Parameter);
        Assert.Contains("resource_metadata", challengeParameters, StringComparison.Ordinal);
        Assert.Contains(
            "/.well-known/oauth-protected-resource/mcp",
            challengeParameters,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousCallsRemainAuthenticationFailuresAndDoNotConsumeActorClientLimits()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        for (var attempt = 0; attempt < AnonymousAttempts; attempt++)
        {
            using var request = CreateInitializeRequest();
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Retry-After"));
        }

        Assert.Equal(
            0L,
            await factory.Database.ScalarAsync<long>(
                "SELECT COUNT(*) FROM SecurityEvents " +
                "WHERE Type = 'RateLimited' AND ReasonCode LIKE 'mcp_%_rate_limited';"));
    }

    private static HttpRequestMessage CreateInitializeRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new { name = "Pegasus integration tests", version = "1" }
                }
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }
}
