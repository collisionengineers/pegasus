using Pegasus.Core.Cases;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// R673: a report counts once, at its first observed send, and every later
/// send of it is a re-send; one e-mail carrying both of a Case's reports is a
/// send of each.
/// </summary>
public sealed class ReportSendPolicyTests
{
    private static readonly DateTimeOffset Observed = new(2026, 11, 4, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid Inspection = Guid.NewGuid();
    private static readonly Guid Audit = Guid.NewGuid();

    [Fact]
    public void AReportIsFirstSentAtItsEarliestObservedSendAndEveryLaterSendIsAReSend()
    {
        var first = Send("first", Inspection, Observed);
        var second = Send("second", Inspection, Observed.AddHours(2));
        var third = Send("third", Inspection, Observed.AddDays(3));

        Assert.Equal([("first", true), ("second", false), ("third", false)], Read(third, first, second));
    }

    [Fact]
    public void SendsObservedAtTheSameInstantPutTheLowerSendIdFirst()
    {
        var lower = new ReportSend("lower", Inspection, Guid.Parse("10000000-0000-4000-8000-000000000000"), Observed);
        var higher = new ReportSend("higher", Inspection, Guid.Parse("20000000-0000-4000-8000-000000000000"), Observed);

        Assert.Equal([("lower", true), ("higher", false)], Read(higher, lower));
    }

    [Fact]
    public void EachReportIsReadOnItsOwn()
    {
        // The Audit's first send comes after the Inspection's re-send and is still its first.
        var read = Read(
            Send("inspection 2", Inspection, Observed.AddDays(1)),
            Send("audit 2", Audit, Observed.AddDays(3)),
            Send("audit 1", Audit, Observed.AddDays(2)),
            Send("inspection 1", Inspection, Observed));

        Assert.Equal(
            [("audit 1", true), ("audit 2", false), ("inspection 1", true), ("inspection 2", false)],
            read.OrderBy(item => item.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void OneEMailCarryingBothReportsIsASendOfEach()
    {
        var both = Guid.NewGuid();
        var again = Guid.NewGuid();

        var read = Read(
            new("again: audit", Audit, again, Observed.AddDays(1)),
            new("again: inspection", Inspection, again, Observed.AddDays(1)),
            new("both: audit", Audit, both, Observed),
            new("both: inspection", Inspection, both, Observed));

        Assert.Equal(
            [("again: audit", false), ("again: inspection", false), ("both: audit", true), ("both: inspection", true)],
            read.OrderBy(item => item.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void AnEMailCarryingAReportAlreadySentIsItsReSendAndTheOtherReportsFirstSend()
    {
        var both = Guid.NewGuid();

        var read = Read(
            new("both: audit", Audit, both, Observed.AddDays(1)),
            new("both: inspection", Inspection, both, Observed.AddDays(1)),
            Send("alone: inspection", Inspection, Observed));

        Assert.Equal(
            [("alone: inspection", true), ("both: audit", true), ("both: inspection", false)],
            read.OrderBy(item => item.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void AnInspectionAndAuditCasesAuditCountsFromCreateAuditAndEveryOtherReportFromReceipt()
    {
        var received = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var created = received.AddDays(12);

        Assert.Equal(received, ReportSendPolicy.TurnaroundStart(CaseWorkKind.Primary, received, created));
        Assert.Equal(created, ReportSendPolicy.TurnaroundStart(CaseWorkKind.Audit, received, created));
        Assert.Equal(created, ReportSendPolicy.TurnaroundStart(CaseWorkKind.Audit, null, created));
        // A Case with no receipt has no start for its own work's report.
        Assert.Null(ReportSendPolicy.TurnaroundStart(CaseWorkKind.Primary, null, created));
    }

    private static ReportSend Send(string name, Guid report, DateTimeOffset sentAtUtc) =>
        new(name, report, Guid.NewGuid(), sentAtUtc);

    /// <summary>Each send's name and whether it is its report's first send, in the order the rule reads them.</summary>
    private static (string Name, bool IsFirst)[] Read(params ReportSend[] sends) =>
    [
        .. ReportSendPolicy
            .Classify(sends, send => send.Report, send => send.SentAtUtc, send => send.Id)
            .Select(item => (item.Send.Name, item.IsFirst))
    ];

    /// <summary>One report carried by one observed send: the report's key, the send's id and when it was observed.</summary>
    private sealed record ReportSend(string Name, Guid Report, Guid Id, DateTimeOffset SentAtUtc);
}
