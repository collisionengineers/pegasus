using System.Data.Common;
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
    public void ACancellationRaisedOnAnAbortedRequestIsDropped()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };

        var kept = RunOn(
            context,
            new ExceptionTelemetry(new OperationCanceledException()),
            new ExceptionTelemetry(new TaskCanceledException()),
            new ExceptionTelemetry(new InvalidOperationException("Operation cancelled by user.")),
            new ExceptionTelemetry(new DatabaseException(
                "A severe error occurred on the current command.  The results, if any, should be discarded.\r\nOperation cancelled by user.")));

        Assert.Empty(kept);
    }

    [Fact]
    public void AnotherExceptionOnAnAbortedRequestIsKept()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };

        var kept = RunOn(
            context,
            new ExceptionTelemetry(new KeyNotFoundException()),
            new ExceptionTelemetry(new InvalidOperationException("The sequence contains no elements.")),
            new ExceptionTelemetry(new DatabaseException("The login failed.")));

        Assert.Equal(3, kept.Count);
    }

    [Fact]
    public void ACancellationOnALiveRequestOrOutsideARequestIsKept()
    {
        var live = new DefaultHttpContext();
        var cancellation = new ExceptionTelemetry(new OperationCanceledException());
        var cancelledCommand = new ExceptionTelemetry(new InvalidOperationException("Operation cancelled by user."));

        Assert.Equal(2, RunOn(live, cancellation, cancelledCommand).Count);
        Assert.Equal(2, RunOn(null, cancellation, cancelledCommand).Count);
        // Built without the accessor, as the other facts here build it.
        Assert.Equal(2, Run(cancellation, cancelledCommand).Count);
    }

    [Theory]
    [InlineData("WatchEditRequest")]
    [InlineData("HeartbeatLease")]
    public void ACasePageLongPollOrHeartbeatIsDroppedUnlessItFailed(string handler)
    {
        var path = $"/Cases/7f3c1f5e-0d7a-4b8e-9c55-2f6d1b7a9e10?handler={handler}";

        Assert.Empty(Run(Request("POST", path, "200")));
        Assert.Single(Run(Request("POST", path, "500")));
    }

    [Fact]
    public void ACasePageClaimIsKept()
    {
        var path = "/Cases/7f3c1f5e-0d7a-4b8e-9c55-2f6d1b7a9e10?handler=ClaimLease";

        Assert.Single(Run(Request("POST", path, "200")));
    }

    [Fact]
    public void ASqlCallOnACasePageLongPollIsDroppedUnlessItFailed()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Cases/7f3c1f5e-0d7a-4b8e-9c55-2f6d1b7a9e10";
        context.Request.QueryString = new QueryString("?handler=WatchEditRequest");

        Assert.Empty(RunOn(context, Dependency(null, success: true)));
        Assert.Single(RunOn(context, Dependency(null, success: false)));

        context.Request.QueryString = new QueryString("?handler=ClaimLease");
        Assert.Single(RunOn(context, Dependency(null, success: true)));
    }

    [Fact]
    public void AQuietPathIsMatchedByWholeSegments()
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

    /// <summary>The filter as production composes it: it can ask which request the item was raised on.</summary>
    private static List<ITelemetry> RunOn(HttpContext? httpContext, params ITelemetry[] items)
    {
        var kept = new List<ITelemetry>();
        var filter = new QuietRequestTelemetryFilter(
            new Collector(kept), new HttpContextAccessor { HttpContext = httpContext });
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

    /// <summary>A database error carrying the message SqlClient gives it.</summary>
    private sealed class DatabaseException(string message) : DbException(message);
}
