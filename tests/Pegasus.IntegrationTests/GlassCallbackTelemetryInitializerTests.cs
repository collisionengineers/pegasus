using Microsoft.ApplicationInsights.DataContracts;
using Pegasus.Web;

namespace Pegasus.IntegrationTests;

public sealed class GlassCallbackTelemetryInitializerTests
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

        new GlassCallbackTelemetryInitializer().Initialize(telemetry);

        Assert.Equal(
            "https://pegasus.example/Integrations/Glass/Callback/%7Bcorrelation%7D",
            telemetry.Url.AbsoluteUri);
        Assert.Equal("POST Integrations/Glass/Callback/{correlation}", telemetry.Name);
        Assert.Equal("200", telemetry.ResponseCode);
        Assert.Equal("operation-id", telemetry.Context.Operation.Id);
    }

    [Fact]
    public void OtherRequestUrlsAreUnchanged()
    {
        var url = new Uri("https://pegasus.example/Cases/123?tab=documents");
        var telemetry = new RequestTelemetry { Url = url };

        new GlassCallbackTelemetryInitializer().Initialize(telemetry);

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

        new GlassCallbackTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("https://api.box.com/2.0/files/content", telemetry.Data);
    }
}
