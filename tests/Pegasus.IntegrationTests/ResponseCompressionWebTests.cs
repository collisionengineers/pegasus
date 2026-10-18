using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Staff pages and JSON are compressed over HTTPS; nothing else is.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ResponseCompressionWebTests : IClassFixture<IntakeWebApplicationFactory>
{
    private readonly IntakeWebApplicationFactory factory;

    public ResponseCompressionWebTests(IntakeWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("br, gzip", "br")]
    public async Task APageIsCompressedWithTheEncodingTheBrowserOffers(string offered, string expected)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", offered);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expected, Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task APageIsSentPlainWhenTheBrowserOffersNoEncoding()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task APlainTextHealthResponseIsNotCompressed()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "br, gzip");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("br, gzip", "br")]
    public async Task TheDiscoveryDocumentIsCompressedWithTheEncodingTheClientOffers(string offered, string expected)
    {
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = CreateClient(mcpFactory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/.well-known/oauth-protected-resource/mcp");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", offered);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expected, Assert.Single(response.Content.Headers.ContentEncoding));
        var body = await DecodedBodyAsync(response);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Contains("automation.cases", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATokenResponseIsCompressed()
    {
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = CreateClient(mcpFactory);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = AllScopes
            })
        };
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "br");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("br", Assert.Single(response.Content.Headers.ContentEncoding));
        using var document = JsonDocument.Parse(await DecodedBodyAsync(response));
        Assert.False(string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task AnMcpAnswerIsReadableInWhateverEncodingItComesBackIn()
    {
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = CreateClient(mcpFactory);
        // The token helper and the MCP helpers read a body without decoding it,
        // so the token is taken before the client offers an encoding.
        var accessToken = await RequestTokenAsync(client, AllScopes);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "br, gzip");

        using var response = await PostMcpAsync(client, accessToken, ToolsListPayload(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var streamed = response.Content.Headers.ContentType?.MediaType == "text/event-stream";
        if (streamed)
        {
            Assert.Empty(response.Content.Headers.ContentEncoding);
        }

        var body = await DecodedBodyAsync(response);
        var json = streamed
            ? body
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => line[5..].Trim())
                .First(line => line.Length > 0)
            : body;
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength() > 0);
    }

    private HttpClient CreateClient() => CreateClient(factory);

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    /// <summary>
    /// The body as the client would read it: decoded from the one encoding the
    /// response names, or as sent when it names none.
    /// </summary>
    private static async Task<string> DecodedBodyAsync(HttpResponseMessage response)
    {
        await using var body = await response.Content.ReadAsStreamAsync();
        await using Stream decoded = response.Content.Headers.ContentEncoding.SingleOrDefault() switch
        {
            null => body,
            "br" => new BrotliStream(body, CompressionMode.Decompress),
            "gzip" => new GZipStream(body, CompressionMode.Decompress),
            var other => throw new InvalidOperationException($"Unexpected Content-Encoding {other}.")
        };
        using var reader = new StreamReader(decoded, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
