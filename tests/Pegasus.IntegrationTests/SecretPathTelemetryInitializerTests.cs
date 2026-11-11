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

    [Fact]
    public void DependencyAndExceptionRowsLoseTheSecretFromTheirOperationName()
    {
        const string token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var dependency = new DependencyTelemetry { Name = "SQL" };
        dependency.Context.Operation.Name = "GET /c/" + token;
        dependency.Context.Operation.Id = "operation-id";
        var exception = new ExceptionTelemetry(new InvalidOperationException("x"));
        exception.Context.Operation.Name = "GET /s/abcdefghjkmn";
        var callback = new TraceTelemetry("t");
        callback.Context.Operation.Name = "POST /Integrations/Glass/Callback/secret";

        var initializer = new SecretPathTelemetryInitializer();
        initializer.Initialize(dependency);
        initializer.Initialize(exception);
        initializer.Initialize(callback);

        Assert.Equal("GET /c/{token}", dependency.Context.Operation.Name);
        Assert.Equal("operation-id", dependency.Context.Operation.Id);
        Assert.Equal("GET /s/{slug}", exception.Context.Operation.Name);
        Assert.Equal("POST /Integrations/Glass/Callback/{correlation}", callback.Context.Operation.Name);
    }

    [Fact]
    public void ARequestsOwnOperationNameLosesTheSecretToo()
    {
        var telemetry = new RequestTelemetry { Url = new Uri("https://pegasus.example/c/secrettoken") };
        telemetry.Context.Operation.Name = "GET /c/secrettoken";

        new SecretPathTelemetryInitializer().Initialize(telemetry);

        Assert.Equal("GET /c/{token}", telemetry.Context.Operation.Name);
    }

    [Fact]
    public void ANonSecretOperationNameIsUntouched()
    {
        var dependency = new DependencyTelemetry();
        dependency.Context.Operation.Name = "GET /Cases/0f8fad5b-d9cb-469f-a165-70867728950e";

        new SecretPathTelemetryInitializer().Initialize(dependency);

        Assert.Equal("GET /Cases/0f8fad5b-d9cb-469f-a165-70867728950e", dependency.Context.Operation.Name);
    }

    [Fact]
    public void ALogWrittenDuringASecretRequestLosesTheSecretFromItsRequestPath()
    {
        const string token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var exception = new ExceptionTelemetry(new InvalidOperationException("x"));
        exception.Properties["RequestPath"] = "/c/" + token;
        exception.Properties["RequestId"] = "request-id";
        var trace = new TraceTelemetry("t");
        trace.Properties["RequestPath"] = "/s/abcdefghjkmn/items";
        var other = new TraceTelemetry("t");
        other.Properties["RequestPath"] = "/Cases/0f8fad5b-d9cb-469f-a165-70867728950e";

        var initializer = new SecretPathTelemetryInitializer();
        initializer.Initialize(exception);
        initializer.Initialize(trace);
        initializer.Initialize(other);

        Assert.Equal("/c/{token}", exception.Properties["RequestPath"]);
        Assert.Equal("request-id", exception.Properties["RequestId"]);
        Assert.Equal("/s/{slug}", trace.Properties["RequestPath"]);
        Assert.Equal("/Cases/0f8fad5b-d9cb-469f-a165-70867728950e", other.Properties["RequestPath"]);
    }
}
