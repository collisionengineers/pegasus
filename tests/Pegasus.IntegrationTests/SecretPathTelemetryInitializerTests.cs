using Microsoft.ApplicationInsights.DataContracts;
using Pegasus.Web;

namespace Pegasus.IntegrationTests;

public sealed class SecretPathTelemetryInitializerTests
{
    [Fact]
    public void GlassCallbackRequestUrlDropsTheCorrelationQueryAndFragment()
    {
        var telemetry = new RequestTelemetry
        {
            Name = "POST Integrations/Glass/Callback/{correlation}",
            ResponseCode = "200",
            Url = new Uri(
                "https://pegasus.example/Integrations/Glass/Callback/secret-correlation?state=secret#fragment")
        };
        telemetry.Context.Operation.Id = "operation-id";

        new SecretPathTelemetryInitializer().Initialize(telemetry);

        Assert.Equal(
            "https://pegasus.example/Integrations/Glass/Callback/%7Bcorrelation%7D",
            telemetry.Url.AbsoluteUri);
        Assert.Equal("POST Integrations/Glass/Callback/{correlation}", telemetry.Name);
        Assert.Equal("200", telemetry.ResponseCode);
        Assert.Equal("operation-id", telemetry.Context.Operation.Id);
    }

    [Fact]
    public void PhotoLinkRequestDropsTheTokenFromTheUrlAndTheName()
    {
        const string token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var telemetry = new RequestTelemetry
        {
            Name = "GET /c/" + token,
            ResponseCode = "404",
            Url = new Uri("https://pegasus.example/c/" + token)
        };

        new SecretPathTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("https://pegasus.example/c/%7Btoken%7D", telemetry.Url.AbsoluteUri);
        Assert.Equal("GET /c/{token}", telemetry.Name);
        Assert.DoesNotContain(token, telemetry.Url.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void StandingLinkRequestDropsTheSlugFromTheUrl()
    {
        var telemetry = new RequestTelemetry
        {
            Name = "GET /s/{slug}",
            Url = new Uri("https://pegasus.example/s/abcdefghjkmn")
        };

        new SecretPathTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("https://pegasus.example/s/%7Bslug%7D", telemetry.Url.AbsoluteUri);
        Assert.Equal("GET /s/{slug}", telemetry.Name);
    }

    [Fact]
    public void OtherRequestUrlsAreUnchanged()
    {
        var url = new Uri("https://pegasus.example/Cases/123?tab=documents");
        var telemetry = new RequestTelemetry { Url = url };

        new SecretPathTelemetryInitializer().Initialize(telemetry);

        Assert.Same(url, telemetry.Url);
    }

    [Fact]
    public void NonRequestTelemetryIsUnchanged()
    {
        var telemetry = new DependencyTelemetry
        {
            Name = "Box upload",
            Data = "https://api.box.com/2.0/files/content"
        };

        new SecretPathTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("https://api.box.com/2.0/files/content", telemetry.Data);
    }
}
