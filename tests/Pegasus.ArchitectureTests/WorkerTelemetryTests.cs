using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// What the Worker exports of its own running: the slow successful SQL calls that
/// the dependency filter used to drop with the rest, and the exception rows the
/// worker process writes.
/// </summary>
public sealed class WorkerTelemetryTests
{
    private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// The ActivitySource the Functions worker starts each invocation's activity
    /// from, with the host's trace as its parent
    /// (Microsoft.Azure.Functions.Worker.ApplicationInsights TraceConstants).
    /// </summary>
    private const string FunctionsInvocationSource = "Microsoft.Azure.Functions.Worker";

    private const string SiteNameVariable = "WEBSITE_SITE_NAME";

    private const string StorageType = "InProc | Microsoft.Storage";

    private const string BlobAlreadyExistsError =
        "Status: 409 (The specified blob already exists.)\nErrorCode: BlobAlreadyExists";

    [Theory]
    [InlineData(249, true, false)]
    [InlineData(250, true, true)]
    [InlineData(4000, true, true)]
    [InlineData(5, false, true)]
    [InlineData(4000, false, true)]
    public void TheSqlFilterKeepsASlowSuccessAndAnyFailureAndDropsAFastSuccess(
        int milliseconds, bool success, bool kept)
    {
        var next = new RecordingProcessor();
        var filter = new SqlDependencyTelemetryFilter(next);

        filter.Process(new DependencyTelemetry
        {
            Type = "SQL",
            Success = success,
            Duration = TimeSpan.FromMilliseconds(milliseconds)
        });

        Assert.Equal(kept ? 1 : 0, next.Items.Count);
    }

    [Fact]
    public void TheSqlFilterKeepsEveryOtherKindOfTelemetry()
    {
        var next = new RecordingProcessor();
        var filter = new SqlDependencyTelemetryFilter(next);

        filter.Process(new DependencyTelemetry { Type = "Http", Success = true, Duration = TimeSpan.FromMilliseconds(3) });
        filter.Process(new RequestTelemetry { Success = true, Duration = TimeSpan.FromMilliseconds(3) });
        filter.Process(new TraceTelemetry("a line"));

        Assert.Equal(3, next.Items.Count);
    }

    /// <summary>
    /// #1484: an exception that leaves a function is logged by the worker process
    /// inside the invocation's activity (FunctionsApplication.InvokeFunctionAsync,
    /// Log.InvocationError), and EF logs a failed save the same way. The pipeline
    /// Program composes gives that row the Function App's site name as its role
    /// name, the invocation's operation id (the host's trace, which its AppRequests
    /// row carries) and the inner exception. The rows with neither are the
    /// Functions host's own copies of an escaped exception, which no worker
    /// composition reaches (Azure/azure-functions-dotnet-worker#2317 and #2982).
    /// </summary>
    [Fact]
    public async Task AnExceptionLoggedInsideAnInvocationCarriesTheSiteNameTheInvocationsOperationIdAndItsInnerError()
    {
        var program = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "src",
            "Pegasus.Worker",
            "Program.cs"));
        Assert.Contains(".AddApplicationInsightsTelemetryWorkerService()", program, StringComparison.Ordinal);
        Assert.Contains(".ConfigureFunctionsApplicationInsights()", program, StringComparison.Ordinal);

        const string siteName = "pegasus-test-worker";
        var previousSiteName = Environment.GetEnvironmentVariable(SiteNameVariable);
        Environment.SetEnvironmentVariable(SiteNameVariable, siteName);
        try
        {
            var channel = new RecordingTelemetryChannel();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddSingleton<ITelemetryChannel>(channel);
            // The pipeline Program composes. Its SQL filter passes exceptions
            // (TheSqlFilterKeepsEveryOtherKindOfTelemetry), and its ingestion
            // credential sets neither field, so it is left out.
            // This project also references the Web's ASP.NET Core SDK, whose
            // processor extension has the same name as the Worker's.
            services
                .AddApplicationInsightsTelemetryWorkerService()
                .ConfigureFunctionsApplicationInsights();
            // The modules that poll or call out in the background are off; none
            // of them sets a role name or an operation id.
            services.Configure<ApplicationInsightsServiceOptions>(options =>
            {
                options.ConnectionString = ConnectionString;
                options.EnableQuickPulseMetricStream = false;
                options.EnablePerformanceCounterCollectionModule = false;
                options.EnableEventCounterCollectionModule = false;
                options.EnableDependencyTrackingTelemetryModule = false;
                options.EnableAzureInstanceMetadataTelemetryModule = false;
                options.EnableAppServicesHeartbeatTelemetryModule = false;
                options.EnableDiagnosticsTelemetryModule = false;
                options.EnableHeartbeat = false;
            });
            await using var provider = services.BuildServiceProvider();
            // Building the configuration initializes the Functions module, whose
            // listener is what lets the invocation activity start.
            _ = provider.GetRequiredService<TelemetryConfiguration>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Pegasus.Worker");
            using var functions = new ActivitySource(FunctionsInvocationSource);
            var hostTrace = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded,
                isRemote: true);
            var failure = new DbUpdateException(
                "An error occurred while saving the entity changes.",
                new InvalidOperationException("The inner save failure."));

            using (var invocation = functions.StartActivity("Invoke", ActivityKind.Server, hostTrace))
            {
                Assert.NotNull(invocation);
                logger.Log(
                    LogLevel.Error,
                    default,
                    "Function 'UnifiedWorkFunction' failed.",
                    failure,
                    static (message, _) => message);
            }

            var exception = Assert.Single(channel.Sent.OfType<ExceptionTelemetry>());
            Assert.Equal(siteName, exception.Context.Cloud.RoleName);
            Assert.Equal(hostTrace.TraceId.ToHexString(), exception.Context.Operation.Id);
            Assert.Equal(
                [typeof(DbUpdateException).FullName!, typeof(InvalidOperationException).FullName!],
                exception.ExceptionDetailsInfoList.Select(details => details.TypeName));
            Assert.Equal("The inner save failure.", exception.ExceptionDetailsInfoList[1].Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SiteNameVariable, previousSiteName);
        }
    }

    /// <summary>
    /// A conditional blob write whose blob already exists is the by-design replay path
    /// (the store verifies the blob that is there), so its failed dependency row is
    /// dropped for an upload and for a server-side copy alike.
    /// </summary>
    [Theory]
    [InlineData("BlobClient.Upload")]
    [InlineData("BlobBaseClient.StartCopyFromUri")]
    public void TheFilterDropsABlobAlreadyExistsConflict(string name)
    {
        var next = new RecordingProcessor();
        var filter = new SqlDependencyTelemetryFilter(next);

        filter.Process(Dependency(name, StorageType, false, BlobAlreadyExistsError));

        Assert.Empty(next.Items);
    }

    [Theory]
    [InlineData("server-busy", StorageType, false, "Status: 503 (Server Busy)\nErrorCode: ServerBusy")]
    [InlineData("lease-held", StorageType, false, "Status: 409 (There is already a lease present.)\nErrorCode: LeaseAlreadyPresent")]
    [InlineData("no-error", StorageType, false, null)]
    [InlineData("succeeded", StorageType, true, null)]
    [InlineData("box-409", "Http", false, null)]
    [InlineData("http-code", "Http", false, BlobAlreadyExistsError)]
    public void TheFilterKeepsEveryOtherDependencyBesideABlobConflict(
        string name, string type, bool success, string? error)
    {
        var next = new RecordingProcessor();
        var filter = new SqlDependencyTelemetryFilter(next);

        filter.Process(Dependency(name, type, success, error));

        Assert.Single(next.Items);
    }

    [Fact]
    public void TheFilterKeepsAnExceptionAndATraceThatNameABlobConflict()
    {
        var next = new RecordingProcessor();
        var filter = new SqlDependencyTelemetryFilter(next);
        var exception = new ExceptionTelemetry(new InvalidOperationException(BlobAlreadyExistsError));
        exception.Properties["Error"] = BlobAlreadyExistsError;
        var trace = new TraceTelemetry(BlobAlreadyExistsError);
        trace.Properties["Error"] = BlobAlreadyExistsError;

        filter.Process(exception);
        filter.Process(trace);

        Assert.Equal(2, next.Items.Count);
    }

    private static DependencyTelemetry Dependency(string name, string type, bool success, string? error)
    {
        var dependency = new DependencyTelemetry
        {
            Name = name,
            Type = type,
            Success = success,
            Duration = TimeSpan.FromMilliseconds(40)
        };

        if (error is not null)
        {
            dependency.Properties["Error"] = error;
        }

        return dependency;
    }

    private sealed class RecordingProcessor : ITelemetryProcessor
    {
        public List<ITelemetry> Items { get; } = [];

        public void Process(ITelemetry item) => Items.Add(item);
    }

    private sealed class RecordingTelemetryChannel : ITelemetryChannel
    {
        private readonly ConcurrentQueue<ITelemetry> sent = [];

        public IEnumerable<ITelemetry> Sent => sent.ToArray();

        public bool? DeveloperMode { get; set; }

        public string EndpointAddress { get; set; } = string.Empty;

        public void Send(ITelemetry item) => sent.Enqueue(item);

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
