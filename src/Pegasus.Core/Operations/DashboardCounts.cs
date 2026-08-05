namespace Pegasus.Core.Operations;

/// <summary>
/// How many cases are sitting in each of the three stages an operator can act
/// on from the dashboard.
/// </summary>
/// <remarks>
/// These three counts did not exist before. The dashboard rendered the literal
/// string "Unavailable" for Not ready and Held, and backed the Review tile with
/// an intake-receipt count — a different entity entirely, and one that was
/// cumulative for all time.
/// </remarks>
public sealed record CaseStageCounts(int NotReady, int Review, int Held);

/// <summary>
/// What moved today and this week.
/// </summary>
/// <remarks>
/// "Sent to Engineer" is counted from the first-handoff proxy, which is the
/// recorded fact that a case reached an Engineer, rather than from a workflow
/// transition that only says the case became eligible. "Reports sent" counts
/// case-linked sent evidence, so a sent message that was never attributed to a
/// case is not claimed as a delivered report.
/// </remarks>
public sealed record CaseActivityCounts(
    int NewCasesToday,
    int SentToEngineerToday,
    int SentToEngineerThisWeek,
    int ReportsSentToday,
    int ReportsSentThisWeek);

/// <summary>
/// What arrived, and what is waiting for a person.
/// </summary>
public sealed record MailActivityCounts(int ReceivedToday, int NeedsSorting);

/// <summary>
/// The dashboard's counts. Every member returns a real number or the tile that
/// would have shown it is not rendered — there is no placeholder value.
/// </summary>
public interface IDashboardQueries
{
    Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken);

    Task<CaseActivityCounts> GetCaseActivityCountsAsync(
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken);

    Task<MailActivityCounts> GetMailActivityCountsAsync(
        DateTimeOffset dayStartUtc,
        CancellationToken cancellationToken);
}
