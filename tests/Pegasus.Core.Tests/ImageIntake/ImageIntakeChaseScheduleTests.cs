using Pegasus.Core.ImageIntake;
using Pegasus.Core.Tasks;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class ImageIntakeChaseScheduleTests
{
    private static readonly DateTimeOffset RegisteredAtUtc =
        new(2026, 8, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NotDueImmediatelyAfterRegistration()
    {
        Assert.False(ImageIntakeChaseSchedule.IsChaseDue(RegisteredAtUtc, RegisteredAtUtc));
    }

    [Fact]
    public void NotDueOneTickBeforeTheFirstChaseInstant()
    {
        var firstChaseAtUtc = CaseChaseSchedule.FirstChaseAt(RegisteredAtUtc);

        Assert.False(ImageIntakeChaseSchedule.IsChaseDue(RegisteredAtUtc, firstChaseAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void DueExactlyAtTheFirstChaseInstant()
    {
        var firstChaseAtUtc = CaseChaseSchedule.FirstChaseAt(RegisteredAtUtc);

        Assert.True(ImageIntakeChaseSchedule.IsChaseDue(RegisteredAtUtc, firstChaseAtUtc));
    }

    [Fact]
    public void DueWellPastTheFirstChaseInstant()
    {
        var firstChaseAtUtc = CaseChaseSchedule.FirstChaseAt(RegisteredAtUtc);

        Assert.True(ImageIntakeChaseSchedule.IsChaseDue(RegisteredAtUtc, firstChaseAtUtc.AddDays(30)));
    }

    [Fact]
    public void ReusesTheCaseSideSevenCalendarDaySchedule()
    {
        // The image side deliberately owns no second cadence: this asserts
        // the exact instant it uses is CaseChaseSchedule.FirstChaseAt's own
        // result, not a re-derived approximation of it.
        var expected = CaseChaseSchedule.FirstChaseAt(RegisteredAtUtc);

        Assert.False(ImageIntakeChaseSchedule.IsChaseDue(RegisteredAtUtc, expected.AddSeconds(-1)));
        Assert.True(ImageIntakeChaseSchedule.IsChaseDue(RegisteredAtUtc, expected));
    }
}
