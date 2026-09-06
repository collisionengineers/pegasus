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
    // The business reference, so a row of due work can name the case it is
    // about. Without it a due-work list can only offer "Open case", which
    // tells an operator nothing until they have opened all of them.
    string Reference,
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
    public const string PolicyKey = "case-chase-schedule";
    public const int PolicyVersion = 1;
    public const string PolicyIdentity = PolicyKey + "/v1";

    public static DateTimeOffset FirstChaseAt(DateTimeOffset enteredNotReadyAtUtc)
    {
        var local = LondonCalendar.TimeAt(enteredNotReadyAtUtc);
        return LondonCalendar.ToUtc(local.Date.AddDays(7).Add(local.TimeOfDay));
    }

    public static DateTimeOffset NextChaseAt(DateTimeOffset previousChaseAtUtc)
    {
        var local = LondonCalendar.TimeAt(previousChaseAtUtc);
        return LondonCalendar.ToUtc(local.Date.AddDays(7).Add(local.TimeOfDay));
    }

    public static TimeSpan RemainingInterval(DateTimeOffset nextChaseAtUtc, DateTimeOffset heldAtUtc)
    {
        var nextLocal = LondonCalendar.TimeAt(nextChaseAtUtc);
        var heldLocal = LondonCalendar.TimeAt(heldAtUtc);
        return nextLocal <= heldLocal ? TimeSpan.Zero : nextLocal - heldLocal;
    }

    public static DateTimeOffset ResumeAt(DateTimeOffset releasedAtUtc, TimeSpan remainingInterval)
    {
        if (remainingInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remainingInterval),
                "The held chase interval cannot be negative.");
        }

        var releasedLocal = LondonCalendar.TimeAt(releasedAtUtc);
        return LondonCalendar.ToUtc(releasedLocal + remainingInterval);
    }

}
