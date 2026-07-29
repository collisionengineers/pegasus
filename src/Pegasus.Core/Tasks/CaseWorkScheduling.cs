namespace Pegasus.Core.Tasks;

/// <summary>
/// The current operational state of a case's missing-material work. A task is a
/// projection of case workflow; it does not send correspondence or prove delivery.
/// </summary>
public enum CaseDueWorkState
{
    Scheduled,
    Held,
    Stopped
}

public sealed record CaseDueWork(
    Guid CaseId,
    string MissingMaterialReason,
    DateOnly? DueBy,
    CaseDueWorkState State,
    DateTimeOffset? NextChaseAtUtc,
    DateTimeOffset? HeldAtUtc,
    TimeSpan? RemainingChaseInterval,
    string? MostRecentChannel,
    string? MostRecentOutcome,
    string? MostRecentNote,
    long Version);

public sealed record ManualChaseRecord(
    Guid CaseId,
    long ExpectedCaseVersion,
    string EditLeaseToken,
    Pegasus.Core.Identity.ActionActor Actor,
    string OperationKey,
    string Reason,
    string Channel,
    string TargetPartyOrAddress,
    DateTimeOffset AttemptedAtUtc,
    string Outcome,
    string? Note = null);

public interface ICaseDueWorkQueries
{
    Task<CaseDueWork?> GetAsync(Guid caseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseDueWork>> GetDueAsync(
        DateTimeOffset asOfUtc,
        int maximumResults,
        CancellationToken cancellationToken);
}

/// <summary>
/// Persists a manual operational assertion and its current due-work projection. Implementations
/// must write permanent action history in the same transaction; no implementation may send mail.
/// </summary>
public interface ICaseDueWorkStore : ICaseDueWorkQueries
{
    Task<CaseDueWork> RecordManualChaseAsync(
        ManualChaseRecord request,
        CancellationToken cancellationToken);
}

public interface IRecordManualCaseChase
{
    Task<CaseDueWork> ExecuteAsync(ManualChaseRecord request, CancellationToken cancellationToken);
}

/// <summary>
/// Calculates the local-calendar schedule required for missing material. Persistence owns
/// atomic coupling to the lifecycle transition and permanent action history.
/// </summary>
public static class CaseChaseSchedule
{
    private static readonly TimeZoneInfo LondonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static DateTimeOffset FirstChaseAt(DateTimeOffset enteredNotReadyAtUtc)
    {
        var local = TimeZoneInfo.ConvertTime(enteredNotReadyAtUtc, LondonTimeZone).DateTime;
        return ToLondonInstant(local.Date.AddDays(7).Add(local.TimeOfDay));
    }

    public static DateTimeOffset NextChaseAt(DateTimeOffset previousChaseAtUtc)
    {
        var local = TimeZoneInfo.ConvertTime(previousChaseAtUtc, LondonTimeZone).DateTime;
        return ToLondonInstant(local.Date.AddDays(7).Add(local.TimeOfDay));
    }

    public static TimeSpan RemainingInterval(DateTimeOffset nextChaseAtUtc, DateTimeOffset heldAtUtc) =>
        nextChaseAtUtc <= heldAtUtc ? TimeSpan.Zero : nextChaseAtUtc - heldAtUtc;

    public static DateTimeOffset ResumeAt(DateTimeOffset releasedAtUtc, TimeSpan remainingInterval)
    {
        if (remainingInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remainingInterval),
                "The held chase interval cannot be negative.");
        }

        return releasedAtUtc + remainingInterval;
    }

    private static DateTimeOffset ToLondonInstant(DateTime local)
    {
        // A local time in the spring-forward gap has no instant. Move to the first valid
        // London local time so scheduling remains deterministic and never silently skips work.
        while (LondonTimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = LondonTimeZone.IsAmbiguousTime(local)
            ? LondonTimeZone.GetAmbiguousTimeOffsets(local).Min()
            : LondonTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
