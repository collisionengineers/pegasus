namespace Pegasus.Core.Tests;

public sealed class LondonCalendarTests
{
    [Fact]
    public void WinterCivilDateUsesUtcMidnightBoundaries()
    {
        var date = new DateOnly(2026, 1, 15);

        var start = LondonCalendar.StartOfDay(date);
        var end = LondonCalendar.StartOfNextDay(date);

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 1, 16, 0, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void BstCivilDateIncludesLocalHalfHourAfterMidnight()
    {
        var date = new DateOnly(2026, 7, 15);
        var localHalfHourAfterMidnight =
            new DateTimeOffset(2026, 7, 14, 23, 30, 0, TimeSpan.Zero);

        var start = LondonCalendar.StartOfDay(date);
        var end = Assert.IsType<DateTimeOffset>(LondonCalendar.StartOfNextDay(date));

        Assert.Equal(new DateTimeOffset(2026, 7, 14, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 23, 0, 0, TimeSpan.Zero), end);
        Assert.InRange(localHalfHourAfterMidnight, start, end.AddTicks(-1));
    }
}
