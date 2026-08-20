using Pegasus.Core.Tasks;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// Whether an Image-initiated Case still Awaiting instruction has been
/// waiting long enough to chase. This is a derived read, not a persisted
/// projection: it reuses the Case side's seven-calendar-day schedule
/// (<see cref="CaseChaseSchedule.FirstChaseAt"/>) rather than defining a
/// second cadence, exactly the way the pairing-visibility half of INT-32
/// derives <c>Associated with Case</c> from the origin receipt instead of
/// storing a second, independently-set copy of the same fact.
/// </summary>
/// <remarks>
/// There is no Held or Stopped state here. Those exist on
/// <see cref="Tasks.CaseDueWorkState"/> because a formal Case has manual
/// chase-pause machinery and generated chaser drafts
/// (<see cref="RunDueChasers"/>); an Image-initiated Case has neither, so a
/// due/not-due read is the whole shape, not a truncated one.
/// </remarks>
public static class ImageIntakeChaseSchedule
{
    public static bool IsChaseDue(DateTimeOffset registeredAtUtc, DateTimeOffset asOfUtc) =>
        asOfUtc >= CaseChaseSchedule.FirstChaseAt(registeredAtUtc);
}
