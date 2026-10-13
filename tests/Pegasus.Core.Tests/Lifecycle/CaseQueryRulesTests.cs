using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// FRD-13 "Report sent and queries" (operator, 11 October 2026): a query is
/// a record on the Case, raised by a post-report e-mail joining a Report sent
/// Case or logged by staff, and the Query stage follows the open-query mark.
/// </summary>
public sealed class CaseQueryRulesTests
{
    private static readonly Guid CaseId = Guid.NewGuid();

    [Theory]
    [InlineData("report-sent-query", true, true, false, true)]
    [InlineData("report-sent-already-open", true, true, true, false)]
    [InlineData("report-sent-ordinary-mail", true, false, false, false)]
    [InlineData("report-not-sent", false, true, false, false)]
    public void AQueryMessageRaisesAQueryOnlyOnceTheReportIsSent(
        string name,
        bool reportIsSent,
        bool isQuery,
        bool alreadyOpenForMessage,
        bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseQueryRules.RaisesOnMailJoined(reportIsSent, isQuery, alreadyOpenForMessage));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AMessageLeavingWithdrawsOnlyTheOpenQueryItRaised(bool openForMessage, bool expected) =>
        Assert.Equal(expected, CaseQueryRules.WithdrawsOnMailLeft(openForMessage));

    [Theory]
    [InlineData("flagged-open", true, true, true)]
    [InlineData("flagged-nothing-open", true, false, false)]
    [InlineData("unflagged-open", false, true, false)]
    public void OnlyAFlaggedReplyResolvesTheOpenQueries(string name, bool flagged, bool hasOpenQuery, bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseQueryRules.ResolvesOnObservedReply(flagged, hasOpenQuery));
    }

    [Theory]
    [InlineData("report-sent-open", CaseLifecycleState.ReportSent, true, true)]
    [InlineData("with-engineer-open", CaseLifecycleState.ReportPreparation, true, true)]
    [InlineData("held-open", CaseLifecycleState.Held, true, true)]
    [InlineData("report-sent-none", CaseLifecycleState.ReportSent, false, false)]
    [InlineData("cancelled-open", CaseLifecycleState.PrincipalCancelled, true, false)]
    public void TheQueryStageIsAnyOpenCaseWithAnOpenQuery(string name, CaseLifecycleState state, bool hasOpenQuery, bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseQueryRules.IsQueryStage(state, hasOpenQuery));
    }

    [Theory]
    [InlineData("report-sent", CaseLifecycleState.ReportSent, false, true)]
    [InlineData("report-sent-archived", CaseLifecycleState.ReportSent, true, false)]
    [InlineData("with-engineer", CaseLifecycleState.ReportPreparation, false, false)]
    [InlineData("review", CaseLifecycleState.Review, false, false)]
    public void AQueryIsLoggedOnlyOnceTheReportIsSent(string name, CaseLifecycleState state, bool archived, bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseQueryRules.CanLog(Workflow(state, hasOpenQuery: false, archived)));
    }

    /// <summary>
    /// Operator, 13 October 2026: between the Inspection report's send and Create
    /// audit an Inspection + Audit Case stays With Engineer, and a query can still
    /// be logged there. With Engineer with Sent evidence on any other Case (after
    /// Return to Engineer) is not that window.
    /// </summary>
    [Theory]
    [InlineData("inspection-audit-no-audit", CaseType.InspectionAndAudit, false, true)]
    [InlineData("inspection-audit-with-audit", CaseType.InspectionAndAudit, true, false)]
    [InlineData("inspection", CaseType.Inspection, false, false)]
    public void AQueryIsLoggedWithEngineerWhileTheInspectionReportIsSentAndTheAuditIsStillToCome(
        string name,
        CaseType caseType,
        bool hasAudit,
        bool expected)
    {
        Assert.NotEmpty(name);
        var workflow = Workflow(CaseLifecycleState.ReportPreparation, hasOpenQuery: false) with
        {
            CaseType = caseType,
            Identity = new(CaseId, "QDOS", 2026, 1, "QDOS260001", hasAudit ? "a.QDOS260001" : null),
            ReportSentEvidence = SentEvidence()
        };

        Assert.Equal(expected, CaseQueryRules.CanLog(workflow));
    }

    private static ApprovedMailboxReportSentEvidence SentEvidence() => new(
        Guid.Parse("3e4f5a6b-7c8d-4e9f-a0b1-c2d3e4f5a6b7"),
        "reports@example.test",
        "sent-items",
        "item-1",
        "<message-1@example.test>",
        "conversation-1",
        "chain-1",
        "occurrence-1",
        new string('a', 64),
        new string('b', 64),
        new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 12, 9, 5, 0, TimeSpan.Zero),
        ActionActor.SystemWorker("poller"),
        new DateTimeOffset(2026, 10, 12, 9, 5, 0, TimeSpan.Zero),
        ActionActor.SystemWorker("poller"));

    [Theory]
    [InlineData("open", true, false, true)]
    [InlineData("none", false, false, false)]
    [InlineData("open-archived", true, true, false)]
    public void ResolveQueryIsLiveWhileAQueryIsOpen(string name, bool hasOpenQuery, bool archived, bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseQueryRules.CanResolve(Workflow(CaseLifecycleState.ReportSent, hasOpenQuery, archived)));
    }

    internal static CaseWorkflowRecord Workflow(CaseLifecycleState state, bool hasOpenQuery, bool archived = false) => new(
        CaseId,
        new(CaseId, "QDOS", 2026, 1, "QDOS260001"),
        state,
        Guid.NewGuid(),
        null,
        null,
        null,
        null,
        null,
        null,
        4)
    {
        HasOpenQuery = hasOpenQuery,
        Archive = archived
            ? new(new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero), Pegasus.Core.Identity.ActionActor.SystemWorker("archive"), "Done")
            : null
    };
}
