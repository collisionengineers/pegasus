using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;

namespace Pegasus.Core.Tests.AiWork;

/// <summary>
/// A drafted reply is text offered to the composer (FRD-27): the message a
/// Query response job answers, the one live job a message shows, what counts
/// as a draft the composer may prefill, and where a Draft ready job opens.
/// </summary>
public sealed class QueryResponseDraftTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid MessageId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheMessageIsTheResultReferenceThenTheInstructionAndOnlyForAQueryResponse()
    {
        var other = Guid.NewGuid();

        Assert.Equal(MessageId, AiJobPolicy.QueryResponseMessageId(Job(AiJobState.Queued)));
        Assert.Equal(other, AiJobPolicy.QueryResponseMessageId(
            Job(AiJobState.DraftReady) with { ResultReference = other.ToString("D") }));
        Assert.Null(AiJobPolicy.QueryResponseMessageId(Job(AiJobState.Queued) with { Instruction = "Draft a reply." }));
        Assert.Null(AiJobPolicy.QueryResponseMessageId(Job(AiJobState.Queued) with { Kind = AiJobKind.Estimate }));
    }

    [Fact]
    public void TheLiveJobIsTheNewestQueuedTakenOrDraftReadyOneForTheMessage()
    {
        var completed = Job(AiJobState.Completed) with { CreatedAtUtc = Now.AddHours(-1) };
        var ready = Job(AiJobState.DraftReady) with { CreatedAtUtc = Now.AddHours(-2) };
        var older = Job(AiJobState.Queued) with { CreatedAtUtc = Now.AddHours(-3) };
        var otherMessage = Job(AiJobState.Queued) with { Instruction = Guid.NewGuid().ToString("D") };

        var live = AiJobPolicy.QueryResponseJobFor([older, otherMessage, completed, ready], MessageId, Now);

        Assert.Equal(ready.JobId, live!.JobId);
        Assert.Null(AiJobPolicy.QueryResponseJobFor([completed, otherMessage], MessageId, Now));
    }

    [Fact]
    public void ALapsedLeaseReadsQueuedAndAnExpiredQueuedJobIsNotLive()
    {
        var lapsed = Job(AiJobState.Taken) with { LeaseExpiresAtUtc = Now.AddMinutes(-1) };
        var expired = Job(AiJobState.Queued) with { ExpiresAtUtc = Now.AddMinutes(-1) };

        Assert.Equal(lapsed.JobId, AiJobPolicy.QueryResponseJobFor([lapsed], MessageId, Now)!.JobId);
        Assert.Equal(AiJobState.Queued, AiJobPolicy.EffectiveState(lapsed, Now));
        Assert.Null(AiJobPolicy.QueryResponseJobFor([expired], MessageId, Now));
    }

    [Fact]
    public void ADraftReplyIsDraftReadyWithTextForTheSameMessageOnTheSameCase()
    {
        var ready = Job(AiJobState.DraftReady);

        Assert.True(AiJobPolicy.IsDraftReplyFor(ready, MessageId, CaseId));
        Assert.False(AiJobPolicy.IsDraftReplyFor(ready, Guid.NewGuid(), CaseId));
        Assert.False(AiJobPolicy.IsDraftReplyFor(ready, MessageId, Guid.NewGuid()));
        Assert.False(AiJobPolicy.IsDraftReplyFor(ready with { ResultText = " " }, MessageId, CaseId));
        Assert.False(AiJobPolicy.IsDraftReplyFor(ready with { State = AiJobState.Completed }, MessageId, CaseId));
        Assert.False(AiJobPolicy.IsDraftReplyFor(Job(AiJobState.Taken) with { ResultText = null }, MessageId, CaseId));
    }

    [Fact]
    public void ADraftReadyReplyOpensTheMessagesComposerOnTheJob()
    {
        var ready = Job(AiJobState.DraftReady);

        var route = StaffNotificationPolicy.AiDraftRoute(ready);

        Assert.Equal(
            $"/Inbox/{MessageId:D}?compose=reply&correspondenceCaseReference=QDOS260001&resolveQuery=true&aiJob={ready.JobId:D}",
            route);
        Assert.Equal(route, AiDraftPolicy.ToDraft(ready, 1)!.Route);
    }

    private static AiJobRecord Job(AiJobState state) => new(
        Guid.NewGuid(), AiJobKind.QueryResponse, AiJobSubjectKind.Case, CaseId, "QDOS260001",
        MessageId.ToString("D"), null, null, state, ActorKind.Staff, Guid.NewGuid().ToString("D"), Now.AddHours(-1),
        Now + AiJobPolicy.DefaultExpiry, "client", Now.AddMinutes(-30), Now + AiJobPolicy.LeaseDuration, null,
        state == AiJobState.DraftReady ? AiJobResultKind.DraftReply : null,
        state == AiJobState.DraftReady ? MessageId.ToString("D") : null,
        state == AiJobState.DraftReady ? "Dear Ms Patel, thank you for your e-mail." : null,
        null, null, 1)
    {
        DraftReadyAtUtc = state == AiJobState.DraftReady ? Now.AddMinutes(-5) : null
    };
}
