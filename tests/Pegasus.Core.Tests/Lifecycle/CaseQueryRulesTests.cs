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
    [InlineData("report-sent-query", CaseLifecycleState.ReportSent, true, false, true)]
    [InlineData("report-sent-already-open", CaseLifecycleState.ReportSent, true, true, false)]
    [InlineData("report-sent-ordinary-mail", CaseLifecycleState.ReportSent, false, false, false)]
    [InlineData("with-engineer", CaseLifecycleState.ReportPreparation, true, false, false)]
    [InlineData("review", CaseLifecycleState.Review, true, false, false)]
    [InlineData("held", CaseLifecycleState.Held, true, false, false)]
    [InlineData("cancelled", CaseLifecycleState.PrincipalCancelled, true, false, false)]
    public void AQueryMessageRaisesAQueryOnlyOnAReportSentCase(
        string name,
        CaseLifecycleState state,
        bool isQuery,
        bool alreadyOpenForMessage,
        bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseQueryRules.RaisesOnMailJoined(state, isQuery, alreadyOpenForMessage));
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
