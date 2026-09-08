namespace Pegasus.Core.Tests;

public sealed class LondonCalendarTests
{
    [Fact]
    public void RepeatedAutumnHourPreservesTheTwoDistinctOffsets()
    {
        var summer = LondonCalendar.LocalAt(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero));
        var winter = LondonCalendar.LocalAt(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero));

        Assert.Equal(summer.DateTime, winter.DateTime);
        Assert.Equal(TimeSpan.FromHours(1), summer.Offset);
        Assert.Equal(TimeSpan.Zero, winter.Offset);
        Assert.Equal(TimeSpan.FromHours(1), winter - summer);
    }

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

        var (dayStart, dayEnd, weekStart) = LondonCalendar.DayAndWeekBoundariesAt(wednesday);

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 23, 0, 0, TimeSpan.Zero), dayStart);
        Assert.Equal(new DateTimeOffset(2026, 8, 5, 23, 0, 0, TimeSpan.Zero), dayEnd);
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 23, 0, 0, TimeSpan.Zero), weekStart);
    }

    [Fact]
    public void DayAndWeekBoundariesOnTheGmtTransitionSundayUseBstMidnights()
    {
        // The clocks go back at 02:00 local on Sunday 25 October 2026. Asked
        // after the transition, both boundaries still fall on BST midnights:
        // the day started at 23:00Z on the 24th, and Monday the 19th started
        // at 23:00Z on the 18th.
        var afterTheTransition = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        var (dayStart, dayEnd, weekStart) =
            LondonCalendar.DayAndWeekBoundariesAt(afterTheTransition);

        Assert.Equal(new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero), dayStart);
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), dayEnd);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 23, 0, 0, TimeSpan.Zero), weekStart);
    }

    [Fact]
    public void DayAndWeekBoundariesOnTheBstTransitionSundayUseGmtMidnights()
    {
        // The clocks go forward at 01:00 local on Sunday 29 March 2026. Asked
        // after the transition, both boundaries still fall on GMT midnights:
        // the day started at 00:00Z, and Monday the 23rd started at 00:00Z.
        var afterTheTransition = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero);

        var (dayStart, dayEnd, weekStart) =
            LondonCalendar.DayAndWeekBoundariesAt(afterTheTransition);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero), dayStart);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 23, 0, 0, TimeSpan.Zero), dayEnd);
        Assert.Equal(new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero), weekStart);
    }
}
