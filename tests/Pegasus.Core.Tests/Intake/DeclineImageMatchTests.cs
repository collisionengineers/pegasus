using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class DeclineImageMatchTests
{
    [Fact]
    public async Task ADeclineIsRecordedOnceAndEveryMemberIsRedriven()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);

        await harness.Command.ExecuteAsync(harness.Group.Id, harness.Staff);

        var declined = Assert.Single(harness.Groups.Declines);
        Assert.Equal(harness.Group.Id, declined.GroupId);
        Assert.Equal(harness.Staff.SubjectId, declined.StaffSubjectId);
        Assert.Equal(
            harness.Group.Members.Select(member => member.StagedReceiptId).Order(),
            harness.Enqueuer.Enqueued.Order());
    }

    [Fact]
    public async Task AFailedEnqueueNeverUndoesTheDecline()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);
        harness.Enqueuer.Fail = true;

        await harness.Command.ExecuteAsync(harness.Group.Id, harness.Staff);

        Assert.Single(harness.Groups.Declines);
    }

    [Fact]
    public async Task ADiscardedOrNonManualGroupIsLeftAlone()
    {
        var mailbox = new Harness(IntakeSourceChannel.Mailbox);
        var discarded = new Harness(
            IntakeSourceChannel.ManualUpload,
            new("Staff", "staff-1", DateTimeOffset.UtcNow, "op", "fingerprint"));

        await mailbox.Command.ExecuteAsync(mailbox.Group.Id, mailbox.Staff);
        await discarded.Command.ExecuteAsync(discarded.Group.Id, discarded.Staff);

        Assert.Empty(mailbox.Groups.Declines);
        Assert.Empty(mailbox.Enqueuer.Enqueued);
        Assert.Empty(discarded.Groups.Declines);
    }

    [Fact]
    public async Task ADeclineNeedsCaseworkAuthority()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            harness.Command.ExecuteAsync(harness.Group.Id, ActionActor.SystemWorker("worker")));

        Assert.Empty(harness.Groups.Declines);
    }

    private sealed class Harness
    {
        public Harness(IntakeSourceChannel channel, IntakeSubmissionGroupDiscard? discard = null)
        {
            var groupId = Guid.NewGuid();
            Group = new IntakeSubmissionGroup(
                groupId,
                channel,
                "token",
                2,
                "staff:test",
                DateTimeOffset.UtcNow,
                [
                    new(groupId, 0, Guid.NewGuid(), "a.jpg", "AA", false),
                    new(groupId, 1, Guid.NewGuid(), "b.jpg", "BB", false)
                ],
                Discard: discard);
            Groups = new RecordingGroupStore(Group);
            Command = new DeclineImageMatch(Groups, Enqueuer, TimeProvider.System);
        }

        public IntakeSubmissionGroup Group { get; }

        public RecordingGroupStore Groups { get; }

        public RecordingEnqueuer Enqueuer { get; } = new();

        public DeclineImageMatch Command { get; }

        public ActionActor Staff { get; } = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    }

    private sealed class RecordingEnqueuer : IIntakeWorkEnqueuer
    {
        public List<Guid> Enqueued { get; } = [];

        public bool Fail { get; set; }

        public Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new TimeoutException("The queue is unavailable.");
            }

            Enqueued.Add(stagedReceiptId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupStore(IntakeSubmissionGroup group) : IIntakeSubmissionGroupStore
    {
        public List<(Guid GroupId, string StaffSubjectId)> Declines { get; } = [];

        public Task<IntakeSubmissionGroup?> GetAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroup?>(groupId == group.Id ? group : null);

        public Task DeclineImageMatchAsync(
            Guid groupId,
            string staffSubjectId,
            DateTimeOffset declinedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Declines.Add((groupId, staffSubjectId));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> ListPendingImageGroupReceiptsAsync(
            int maximumItems, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroup?> FindAsync(
            IntakeSourceChannel channel, string submissionToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(
            Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount,
            string actor, DateTimeOffset receivedAtUtc, Guid? parentReceiptId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(
            Guid groupId, int ordinal, ReceivedIntake received, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
            Guid groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
