using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Staff pages are compressed over HTTPS; nothing else is.
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
    public async Task AResponseThatIsNotAPageIsNotCompressed()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "br, gzip");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
}
