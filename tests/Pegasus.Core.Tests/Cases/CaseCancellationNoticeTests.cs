using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// FRD-13 "Cancellation messages": an open Case's Next action names the newest
/// linked message whose current classification is a cancellation; a closed
/// Case names none.
/// </summary>
public sealed class CaseCancellationNoticeTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheNewestLinkedCancellationNamesAnOpenCase()
    {
        var older = Email(Now.AddHours(-2), Cancellation());
        var newer = Email(Now.AddHours(-1), Cancellation());
        var update = Email(Now, MailCategory.Received(ReceivedMailFamily.InProgressCases, "case-update"));
        var unclassified = Email(Now.AddMinutes(1), null);

        Assert.Equal(
            newer.RetainedMessageId,
            CaseCancellationNotice.MessageId(Workflow(CaseLifecycleState.ReportPreparation), [update, older, unclassified, newer]));
        Assert.Null(CaseCancellationNotice.MessageId(Workflow(CaseLifecycleState.ReportPreparation), [update, unclassified]));
        Assert.Null(CaseCancellationNotice.MessageId(Workflow(CaseLifecycleState.NotReady), []));
    }

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    public void AClosedCaseShowsNoCancellation(CaseLifecycleState state) =>
        Assert.Null(CaseCancellationNotice.MessageId(Workflow(state), [Email(Now, Cancellation())]));

    private static MailCategory Cancellation() =>
        MailCategory.Received(ReceivedMailFamily.InProgressCases, MailCategory.CancellationSubtype);

    private static CaseWorkflowRecord Workflow(CaseLifecycleState state) => new(
        CaseId, new(CaseId, "QDOS", 2026, 1, "QDOS260001"), state, null, null, null, null, null, null, null, 1);

    private static CaseCorrespondenceEmail Email(DateTimeOffset receivedAtUtc, MailCategory? classification) =>
        new(Guid.NewGuid(), receivedAtUtc, null, "Sender", "sender@example.invalid", "Subject", classification);
}
