using Microsoft.ApplicationInsights.DataContracts;
using Pegasus.Web;

namespace Pegasus.IntegrationTests;

public sealed class PublicUploadTelemetryInitializerTests
{
    [Theory]
    [InlineData("GET Uploads/Request", "https://pegasus.example/Uploads/secret-token?return=case#fragment")]
    [InlineData("POST Uploads/Request", "https://pegasus.example/uPlOaDs/secret-token")]
    public void PublicUploadRequestUrlDropsTheTokenQueryAndFragment(
        string requestName,
        string requestUrl)
    {
        var telemetry = new RequestTelemetry
        {
            Name = requestName,
            ResponseCode = "200",
            Url = new Uri(requestUrl)
        };
        telemetry.Context.Operation.Id = "operation-id";

        new PublicUploadTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("https://pegasus.example/Uploads/Request", telemetry.Url.AbsoluteUri);
        Assert.Equal(requestName, telemetry.Name);
        Assert.Equal("200", telemetry.ResponseCode);
        Assert.Equal("operation-id", telemetry.Context.Operation.Id);
    }

    [Fact]
    public void GlassCallbackRequestUrlDropsTheCorrelationQueryAndFragment()
    {
        var telemetry = new RequestTelemetry
        {
            Name = "POST Integrations/Glass/Callback/{correlation}",
            Url = new Uri(
                "https://pegasus.example/Integrations/Glass/Callback/secret-correlation?state=secret#fragment")
        };

        new PublicUploadTelemetryInitializer().Initialize(telemetry);

        Assert.Equal(
            "https://pegasus.example/Integrations/Glass/Callback/%7Bcorrelation%7D",
            telemetry.Url.AbsoluteUri);
        Assert.Equal("POST Integrations/Glass/Callback/{correlation}", telemetry.Name);
    }

    [Fact]
    public void NonUploadRequestUrlIsUnchanged()
    {
        var url = new Uri("https://pegasus.example/Cases/123?tab=documents");
        var telemetry = new RequestTelemetry { Url = url };

        new PublicUploadTelemetryInitializer().Initialize(telemetry);

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

        new PublicUploadTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("https://api.box.com/2.0/files/content", telemetry.Data);
    }
}
