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

    [Fact]
    public void BstStartDateHasTwentyThreeHourUtcRange()
    {
        var date = new DateOnly(2026, 3, 29);

        var start = LondonCalendar.StartOfDay(date);
        var end = Assert.IsType<DateTimeOffset>(LondonCalendar.StartOfNextDay(date));

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 23, 0, 0, TimeSpan.Zero), end);
        Assert.Equal(TimeSpan.FromHours(23), end - start);
    }

    [Fact]
    public void GmtStartDateHasTwentyFiveHourUtcRange()
    {
        var date = new DateOnly(2026, 10, 25);

        var start = LondonCalendar.StartOfDay(date);
        var end = Assert.IsType<DateTimeOffset>(LondonCalendar.StartOfNextDay(date));

        Assert.Equal(new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), end);
        Assert.Equal(TimeSpan.FromHours(25), end - start);
    }

    [Fact]
    public void DayAndWeekBoundariesStartTheWeekOnMonday()
    {
        var wednesday = new DateTimeOffset(2026, 8, 5, 11, 0, 0, TimeSpan.Zero);

        var (dayStart, weekStart) = LondonCalendar.DayAndWeekBoundariesAt(wednesday);

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 23, 0, 0, TimeSpan.Zero), dayStart);
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 23, 0, 0, TimeSpan.Zero), weekStart);
    }

    [Fact]
    public void InclusiveDateRangeEndsAtTheStartOfTheDayAfterToDate()
    {
        var from = new DateOnly(2026, 7, 15);
        var to = new DateOnly(2026, 7, 16);

        var (start, endExclusive) = LondonCalendar.ToUtcRange(from, to);

        Assert.Equal(new DateTimeOffset(2026, 7, 14, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 23, 0, 0, TimeSpan.Zero), endExclusive);
    }

    [Fact]
    public void InclusiveDateRangeRejectsAnEndBeforeItsStart()
    {
        var from = new DateOnly(2026, 7, 16);
        var to = new DateOnly(2026, 7, 15);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => LondonCalendar.ToUtcRange(from, to));

        Assert.Equal("toInclusive", exception.ParamName);
    }
}
