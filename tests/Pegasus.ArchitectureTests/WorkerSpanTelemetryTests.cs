using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// What the Worker exports of its own running: one event per span Core opens on the
/// intake, image intake, custody and Triage paths, with no tag values, and the slow
/// successful SQL calls that the dependency filter used to drop with the rest.
/// </summary>
public sealed class WorkerSpanTelemetryTests
{
    private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";

    [Theory]
    [InlineData("Pegasus.Core.Intake", "process_intake")]
    [InlineData("Pegasus.Core.ImageIntake", "image_intake_pairing")]
    [InlineData("Pegasus.Core.Custody", "publish_committed_external_work")]
    [InlineData("Pegasus.Core.Triage", "triage_case_pairing")]
    public void ASpanFromEachCoreSourceBecomesOneEventWithoutTagValues(string sourceName, string spanName)
    {
        var channel = new RecordingTelemetryChannel();
        using var configuration = ConfigurationWith(channel);
        using var bridge = new WorkerSpanTelemetryBridge(new TelemetryClient(configuration));
        using var source = new ActivitySource(sourceName);
        var receiptId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        using var invocation = new Activity("test.invocation").Start();

        using (var span = source.StartActivity(spanName))
        {
            Assert.NotNull(span);
            span!.SetTag("intake.receipt_id", receiptId);
            span.SetTag("custody.work_item_id", workItemId);
        }

        var telemetry = Assert.Single(channel.Sent.OfType<EventTelemetry>());
        Assert.Equal("Pegasus.Worker.Span", telemetry.Name);
        Assert.Equal(spanName, telemetry.Properties["span"]);
        Assert.Equal(["span"], telemetry.Properties.Keys);
        Assert.Equal(["durationMs"], telemetry.Metrics.Keys);
        Assert.True(telemetry.Metrics["durationMs"] >= 0);
        Assert.Equal(invocation.TraceId.ToHexString(), telemetry.Context.Operation.Id);
        Assert.Equal(invocation.Id, telemetry.Context.Operation.ParentId);
        Assert.DoesNotContain(receiptId.ToString(), telemetry.Properties.Values.Concat(telemetry.Metrics.Keys));
        Assert.DoesNotContain(workItemId.ToString(), telemetry.Properties.Values.Concat(telemetry.Metrics.Keys));
        Assert.IsAssignableFrom<ISupportSampling>(telemetry);
    }

    [Fact]
    public void ASpanFromAnotherSourceIsNotExported()
    {
        var channel = new RecordingTelemetryChannel();
        using var configuration = ConfigurationWith(channel);
        using var bridge = new WorkerSpanTelemetryBridge(new TelemetryClient(configuration));
        using var source = new ActivitySource("Pegasus.Documents");

        using (source.StartActivity("document.preview"))
        {
        }

        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void ADisabledClientEmitsNothing()
    {
        var channel = new RecordingTelemetryChannel();
        using var configuration = ConfigurationWith(channel);
        configuration.DisableTelemetry = true;
        using var bridge = new WorkerSpanTelemetryBridge(new TelemetryClient(configuration));
        using var source = new ActivitySource("Pegasus.Core.Intake");

        using (source.StartActivity("process_intake"))
        {
        }

        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void ADisposedBridgeStopsListening()
    {
        var channel = new RecordingTelemetryChannel();
        using var configuration = ConfigurationWith(channel);
        var bridge = new WorkerSpanTelemetryBridge(new TelemetryClient(configuration));
        bridge.Dispose();
        using var source = new ActivitySource("Pegasus.Core.Intake");

        using (source.StartActivity("process_intake"))
        {
        }

        Assert.Empty(channel.Sent);
    }

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

    private static TelemetryConfiguration ConfigurationWith(ITelemetryChannel channel)
    {
        var configuration = TelemetryConfiguration.CreateDefault();
        configuration.ConnectionString = ConnectionString;
        configuration.TelemetryChannel = channel;
        return configuration;
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
