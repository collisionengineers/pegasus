using Pegasus.Core.Cases;

namespace Pegasus.Core.Reports;

/// <summary>
/// What counts as a report sent (FRD-15, FRD-17; operator, 4 November 2026,
/// R673). A report is one work's report, the Inspection report or the Audit
/// report (<see cref="CaseWorkPolicy.IsAuditReport"/>), so an Inspection +
/// Audit Case has two. A send of a report is an observed Pegasus report
/// e-mail that carried it, and one e-mail that carried both reports is a send
/// of each. A report's first send is its earliest, by when its Sent item was
/// observed, the lower send id first at the same instant: Reports sent counts
/// it, and the report it carried is the one whose frozen fee and outcome
/// count. Every later send is a re-send, counted on its own and never as
/// another report sent; it is the send that takes the Principal's Re-send
/// name (<see cref="CaseReportDeliveryNaming"/>). First send and re-send are
/// told apart over every send, before a period or a person narrows them.
/// </summary>
public static class ReportSendPolicy
{
    /// <summary>
    /// Each send with whether it is its report's first send, the sends of
    /// one report in the order the rule reads them.
    /// </summary>
    public static IEnumerable<(TSend Send, bool IsFirst)> Classify<TSend>(
        IEnumerable<TSend> sends,
        Func<TSend, Guid> report,
        Func<TSend, DateTimeOffset> sentAtUtc,
        Func<TSend, Guid> sendId)
    {
        ArgumentNullException.ThrowIfNull(sends);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(sentAtUtc);
        ArgumentNullException.ThrowIfNull(sendId);
        return sends
            .GroupBy(report)
            .SelectMany(group => group
                .OrderBy(sentAtUtc)
                .ThenBy(sendId)
                .Select((send, index) => (send, index == 0)));
    }

    /// <summary>
    /// When the turnaround to a report's send starts: the instruction's
    /// receipt, or for the Audit of an Inspection + Audit Case, Create audit,
    /// when its work began. Null when the Case has no receipt.
    /// </summary>
    public static DateTimeOffset? TurnaroundStart(
        CaseWorkKind workKind,
        DateTimeOffset? receivedAtUtc,
        DateTimeOffset workCreatedAtUtc) =>
        workKind == CaseWorkKind.Audit ? workCreatedAtUtc : receivedAtUtc;
}
