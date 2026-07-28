using CollisionDocNet.Core;

namespace CollisionDocNet.Core.Tests;

[TestClass]
public sealed class ExtractionControlTests
{
    [TestMethod]
    public void Check_BeforeDeadline_ReturnsContinue()
    {
        var clock = new ManualTimeProvider();
        var control = new ExtractionControl(TimeSpan.FromSeconds(2), clock);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ExtractionControlState.Continue, control.Check());
    }

    [TestMethod]
    public void Check_AtDeadline_ReturnsTimedOut()
    {
        var clock = new ManualTimeProvider();
        var control = new ExtractionControl(TimeSpan.FromSeconds(2), clock);

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual(ExtractionControlState.TimedOut, control.Check());
    }

    [TestMethod]
    public void Check_CallerCancellationTakesPrecedenceOverDeadline()
    {
        var clock = new ManualTimeProvider();
        using var source = new CancellationTokenSource();
        var control = new ExtractionControl(TimeSpan.FromSeconds(1), clock, source.Token);
        clock.Advance(TimeSpan.FromSeconds(2));
        source.Cancel();

        Assert.AreEqual(ExtractionControlState.Cancelled, control.Check());
    }

    [TestMethod]
    public void Constructor_NonPositiveDeadline_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ExtractionControl(TimeSpan.Zero));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan elapsed) => _ticks = checked(_ticks + elapsed.Ticks);
    }
}
