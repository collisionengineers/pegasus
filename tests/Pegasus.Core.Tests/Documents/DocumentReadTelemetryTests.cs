using System.Diagnostics;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Tests.Documents;

public sealed class DocumentReadTelemetryTests
{
    [Fact]
    public void RecordProducesAnActivityOfTheGivenDurationUnderTheCurrentParent()
    {
        // An ActivitySource and its listeners are process-wide and this
        // assembly runs its classes in parallel, so the listener keeps to the
        // spans rooted in this test's own scope.
        var stopped = new List<Activity>();
        using var scope = new Activity(nameof(RecordProducesAnActivityOfTheGivenDurationUnderTheCurrentParent))
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        using var listener = Listen(scope, stopped);
        var duration = TimeSpan.FromMilliseconds(250);
        var before = DateTime.UtcNow;

        DocumentReadTelemetry.Record("db.connection.open", duration);

        var activity = Assert.Single(stopped);
        Assert.Equal("db.connection.open", activity.DisplayName);
        Assert.Same(scope, activity.Parent);
        Assert.Same(scope, Activity.Current);
        Assert.True(activity.IsStopped);
        Assert.InRange(activity.Duration, duration, duration + TimeSpan.FromSeconds(1));
        // It started 250 ms before it was recorded, not when Record was called.
        Assert.True(activity.StartTimeUtc <= before);
        Assert.Empty(activity.TagObjects);
    }

    [Fact]
    public void RecordTreatsANegativeDurationAsZero()
    {
        var stopped = new List<Activity>();
        using var scope = new Activity(nameof(RecordTreatsANegativeDurationAsZero))
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        using var listener = Listen(scope, stopped);

        DocumentReadTelemetry.Record("db.connection.open", TimeSpan.FromSeconds(-3));

        var activity = Assert.Single(stopped);
        Assert.InRange(activity.Duration, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("db.connection.open")]
    [InlineData("web.workcentre.attention")]
    [InlineData("web.workcentre.newcases")]
    [InlineData("web.workcentre.aijobs")]
    [InlineData("report.photos.prepare")]
    [InlineData("report.pdf.generate")]
    [InlineData("report.pdf.pagecount")]
    public void EveryPhaseTheDiagnosticsLaneAddsIsAllowlisted(string phase)
    {
        Assert.True(DocumentReadTelemetry.IsAllowedPhase(phase));
        // Neither entry point refuses it.
        DocumentReadTelemetry.Start(phase)?.Dispose();
        DocumentReadTelemetry.Record(phase, TimeSpan.Zero);
    }

    [Fact]
    public void AnUnknownPhaseIsRefusedByBothEntryPoints()
    {
        Assert.False(DocumentReadTelemetry.IsAllowedPhase("db.connection.closed"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentReadTelemetry.Start("db.connection.closed"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DocumentReadTelemetry.Record("db.connection.closed", TimeSpan.FromMilliseconds(5)));
    }

    private static ActivityListener Listen(Activity scope, List<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DocumentReadTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == scope.TraceId)
                {
                    stopped.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
