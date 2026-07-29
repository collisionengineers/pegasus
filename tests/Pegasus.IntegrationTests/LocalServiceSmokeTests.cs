using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pegasus.IntegrationTests;

public sealed class LocalServiceSmokeTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task DevelopmentOfflineHostExposesOnlyHealthyLocalCallers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using (var live = await client.GetAsync("/health/live"))
        {
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        }

        using (var ready = await client.GetAsync("/health/ready"))
        {
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        }

        using (var version = await client.GetAsync("/diagnostics/version"))
        {
            Assert.Equal(HttpStatusCode.OK, version.StatusCode);
            using var body = JsonDocument.Parse(await version.Content.ReadAsByteArrayAsync());
            Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("version").GetString()));
            var sourceSha = body.RootElement.GetProperty("sourceSha").GetString();
            Assert.NotNull(sourceSha);
            Assert.Equal(40, sourceSha.Length);
            Assert.All(sourceSha, character => Assert.True(char.IsAsciiHexDigit(character)));
        }

        using (var evaluator = await client.GetAsync("/Intake/EmailEvaluation"))
        {
            Assert.Equal(HttpStatusCode.OK, evaluator.StatusCode);
            Assert.Contains("no-store", evaluator.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
            var html = await evaluator.Content.ReadAsStringAsync();
            Assert.Contains("Evaluate email campaign", html, StringComparison.Ordinal);
            Assert.Contains("Run one guarded, local batch", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task StaffMcpTransportFailsClosedWithoutAnOAuthPrincipal()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            })
        };
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"));
    }
}
