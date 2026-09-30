using System.Globalization;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Pegasus.Web;
using Pegasus.Web.Health;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The rows that carry no information are dropped so the daily cap is spent
/// on staff pages (decision D5); a failed one is always kept.
/// </summary>
public sealed class TelemetryFilterTests
{
    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    [InlineData("/health/warm")]
    [InlineData("/diagnostics/version")]
    [InlineData("/css/work-centre.rn7ouf2o01.css")]
    [InlineData("/js/site.js")]
    [InlineData("/fonts/inter/InterVariable.woff2")]
    [InlineData("/images/pegasus-mark-refined-128.tctmpgz0uo.png")]
    public void AQuietRequestIsDropped(string path)
    {
        var kept = Run(Request("GET", path, "200"));

        Assert.Empty(kept);
    }

    [Fact]
    public void AQuietRequestNamedOnlyByItsOperationNameIsStillDropped()
    {
        var request = new RequestTelemetry { Name = "GET /health/ready", ResponseCode = "200", Success = true };

        Assert.Empty(Run(request));
    }

    [Theory]
    [InlineData("503", false)]
    [InlineData("500", null)]
    [InlineData("200", false)]
    public void AFailedProbeIsKept(string responseCode, bool? success)
    {
        var request = Request("GET", "/health/ready", responseCode);
        request.Success = success;

        Assert.Single(Run(request));
    }

    [Fact]
    public void AStaffPageIsKept()
    {
        Assert.Single(Run(Request("GET", "/Index", "200")));
        Assert.Single(Run(Request("GET", "/Cases/Details", "200")));
        Assert.Single(Run(Request("POST", "/Cases/Details", "302")));
    }

    [Fact]
    public void TheAlwaysOnPingIsDroppedOnlyWhileItIsARedirect()
    {
        Assert.Empty(Run(Request("GET", "/", "302")));
        Assert.Single(Run(Request("GET", "/", "200")));
        Assert.Single(Run(Request("GET", "/", "500")));
        Assert.Single(Run(Request("POST", "/", "302")));
    }

    [Fact]
    public void ASqlCallOfAProbeIsDropped()
    {
        Assert.Empty(Run(Dependency("GET /health/ready", success: true)));
        Assert.Empty(Run(Dependency("GET /css/site.css", success: true)));
    }

    [Fact]
    public void AFailedSqlCallOfAProbeIsKept()
    {
        Assert.Single(Run(Dependency("GET /health/ready", success: false)));
        var serverError = Dependency("GET /health/ready", success: true);
        serverError.ResultCode = "503";
        Assert.Single(Run(serverError));
    }

    [Fact]
    public void ASqlCallOfAStaffPageIsKept()
    {
        Assert.Single(Run(Dependency("GET /Index", success: true)));
        Assert.Single(Run(Dependency(null, success: true)));
    }

    [Fact]
    public void ASqlCallOfAKeepWarmPassIsDroppedUnlessItFailed()
    {
        using (var pass = new System.Diagnostics.Activity(StartupWarmup.PassActivityName).Start())
        using (var call = new System.Diagnostics.Activity("sql").Start())
        {
            Assert.Empty(Run(Dependency(null, success: true)));
            Assert.Single(Run(Dependency(null, success: false)));
        }

        Assert.Single(Run(Dependency(null, success: true)));
    }

    [Fact]
    public void OtherTelemetryPassesThrough()
    {
        var kept = Run(
            new TraceTelemetry("Startup warm-up step model finished"),
            new EventTelemetry("Pegasus.Document.Read"),
            new MetricTelemetry("System.Runtime|Working Set", 1),
            new ExceptionTelemetry(new InvalidOperationException("boom")));

        Assert.Equal(4, kept.Count);
    }

    [Fact]
    public void TheStampsMiddlewareSkipsTheSamePaths()
    {
        foreach (var path in new[] { "/health/ready", "/css/x.css", "/js/x.js", "/fonts/x.woff2", "/images/x.png", "/diagnostics/version" })
        {
            Assert.True(QuietRequestTelemetryFilter.IsQuietPath(path), path);
        }

        foreach (var path in new[] { "/", "/Index", "/Cases/Details", "/Mail/Index", "/healthy-food", "/cssx" })
        {
            Assert.False(QuietRequestTelemetryFilter.IsQuietPath(path), path);
        }
    }

    [Fact]
    public async Task ThePageStampsAreAddedToThePageRequestAndNotToAQuietOne()
    {
        var page = new DefaultHttpContext();
        page.Request.Path = "/Index";
        var pageTelemetry = new RequestTelemetry();
        page.Features.Set(pageTelemetry);
        var probe = new DefaultHttpContext();
        probe.Request.Path = "/health/ready";
        var probeTelemetry = new RequestTelemetry();
        probe.Features.Set(probeTelemetry);

        await RequestRuntimeStamps.InvokeAsync(page, _ => Task.CompletedTask);
        await RequestRuntimeStamps.InvokeAsync(probe, _ => Task.CompletedTask);

        foreach (var key in new[] { "tp.threads", "tp.pending", "gc.gen2.delta", "gc.pause.ms.delta" })
        {
            Assert.True(pageTelemetry.Properties.ContainsKey(key), key);
            Assert.False(probeTelemetry.Properties.ContainsKey(key), key);
        }

        Assert.True(int.Parse(pageTelemetry.Properties["tp.threads"], CultureInfo.InvariantCulture) >= 1);
        Assert.True(int.Parse(pageTelemetry.Properties["gc.gen2.delta"], CultureInfo.InvariantCulture) >= 0);
        // Present where /proc is (Linux), absent elsewhere; never wrong.
        Assert.Equal(
            ProcFs.ReadMajorFaults() is not null,
            pageTelemetry.Properties.ContainsKey("majflt.delta"));
    }

    [Fact]
    public async Task APageRequestWithoutTelemetryConfiguredRunsUntouched()
    {
        var ran = false;

        await RequestRuntimeStamps.InvokeAsync(new DefaultHttpContext(), _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.True(ran);
    }

    [Fact]
    public async Task ThePageStampsAreStillAddedWhenTheRequestThrows()
    {
        var page = new DefaultHttpContext();
        page.Request.Path = "/Cases/Details";
        var telemetry = new RequestTelemetry();
        page.Features.Set(telemetry);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RequestRuntimeStamps.InvokeAsync(page, _ => throw new InvalidOperationException("page failed")));

        Assert.True(telemetry.Properties.ContainsKey("tp.threads"));
    }

    private static List<ITelemetry> Run(params ITelemetry[] items)
    {
        var kept = new List<ITelemetry>();
        var filter = new QuietRequestTelemetryFilter(new Collector(kept));
        foreach (var item in items)
        {
            filter.Process(item);
        }

        return kept;
    }

    private static RequestTelemetry Request(string method, string path, string responseCode)
    {
        var request = new RequestTelemetry
        {
            Name = $"{method} {path}",
            ResponseCode = responseCode,
            Success = int.Parse(responseCode, CultureInfo.InvariantCulture) < 400,
            Url = new Uri("https://pegasus.example" + path)
        };
        request.Context.Operation.Name = request.Name;
        return request;
    }

    private static DependencyTelemetry Dependency(string? operationName, bool success)
    {
        var dependency = new DependencyTelemetry
        {
            Type = "SQL",
            Name = "pegasus",
            Success = success
        };
        dependency.Context.Operation.Name = operationName;
        return dependency;
    }

    private sealed class Collector(List<ITelemetry> kept) : ITelemetryProcessor
    {
        public void Process(ITelemetry item) => kept.Add(item);
    }
}
