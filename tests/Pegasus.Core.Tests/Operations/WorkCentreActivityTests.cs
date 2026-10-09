using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// The Activity figures' windows (FRD-15, v32 items D and G) and who may read
/// them. The counts themselves are the store's; Core fixes the day and week.
/// </summary>
public sealed class WorkCentreActivityTests
{
    [Fact]
    public void TodayStartsAtLondonMidnightAndTheWeekOnMondayInSummerTime()
    {
        // Wednesday 7 October 2026 09:41 BST is 08:41Z.
        var (dayStart, weekStart) = WorkCentreActivityPolicy.WindowsAt(new DateTimeOffset(2026, 10, 7, 8, 41, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 10, 6, 23, 0, 0, TimeSpan.Zero), dayStart);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 23, 0, 0, TimeSpan.Zero), weekStart);
    }

    [Fact]
    public void OnAMondayTheDayAndTheWeekStartTogether()
    {
        var (dayStart, weekStart) = WorkCentreActivityPolicy.WindowsAt(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(weekStart, dayStart);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 23, 0, 0, TimeSpan.Zero), weekStart);
    }

    [Fact]
    public void InWinterTimeTheBoundariesAreUtcMidnights()
    {
        // Wednesday 4 November 2026 10:00 GMT.
        var (dayStart, weekStart) = WorkCentreActivityPolicy.WindowsAt(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero), dayStart);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero), weekStart);
    }

    [Fact]
    public async Task AStaffReadPassesTheWindowsToTheStoreAndReturnsItsCounts()
    {
        var store = new RecordingQueries();
        var read = new GetWorkCentreActivity(store);
        var asOf = new DateTimeOffset(2026, 10, 7, 8, 41, 0, TimeSpan.Zero);

        var counts = await read.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), asOf, CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 10, 6, 23, 0, 0, TimeSpan.Zero), store.DayStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 23, 0, 0, TimeSpan.Zero), store.WeekStartUtc);
        Assert.Equal(store.Counts, counts);
    }

    [Fact]
    public async Task OnlyStaffMayReadTheFigures()
    {
        var read = new GetWorkCentreActivity(new RecordingQueries());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            read.ExecuteAsync(ActionActor.SystemWorker("worker"), DateTimeOffset.UtcNow, CancellationToken.None));
    }

    private sealed class RecordingQueries : IWorkCentreActivityQueries
    {
        public DateTimeOffset DayStartUtc { get; private set; }

        public DateTimeOffset WeekStartUtc { get; private set; }

        public WorkCentreActivityCounts Counts { get; } = new(new(3, 5), new(6, 17), new(3, 11), new(2, 9), new(18, 64));

        public Task<WorkCentreActivityCounts> GetAsync(DateTimeOffset dayStartUtc, DateTimeOffset weekStartUtc, CancellationToken cancellationToken)
        {
            DayStartUtc = dayStartUtc;
            WeekStartUtc = weekStartUtc;
            return Task.FromResult(Counts);
        }
    }
}
