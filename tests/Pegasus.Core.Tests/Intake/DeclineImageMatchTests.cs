using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Leave declines the image match only while the upload awaits its decision
/// and every file is a possible match and image-only (R1092); an upload
/// already left is answered as done.
/// </summary>
public sealed class DeclineImageMatchTests
{
    private static readonly ImageMatchLeaveFile[] PossibleImageMatches =
        [new(true, ImageReceipt()), new(true, ImageReceipt())];

    [Fact]
    public async Task ADeclineIsRecordedOnceAndEveryMemberIsRedriven()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);

        Assert.True(ImageMatchLeave.IsOffered(true, PossibleImageMatches));
        await harness.Command.ExecuteAsync(harness.Group.Id, true, PossibleImageMatches, harness.Staff);

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

        await harness.Command.ExecuteAsync(harness.Group.Id, true, PossibleImageMatches, harness.Staff);

        Assert.Single(harness.Groups.Declines);
    }

    [Fact]
    public async Task ADiscardedOrNonManualGroupIsLeftAlone()
    {
        var photoLink = new Harness(IntakeSourceChannel.PhotoCapture, declaredRegistration: "AB12CDE");
        var discarded = new Harness(
            IntakeSourceChannel.ManualUpload,
            new("Staff", "staff-1", DateTimeOffset.UtcNow, "op", "fingerprint"));

        // Left alone before the offer is asked: neither is refused.
        await photoLink.Command.ExecuteAsync(photoLink.Group.Id, false, [], photoLink.Staff);
        await discarded.Command.ExecuteAsync(discarded.Group.Id, false, [], discarded.Staff);

        Assert.Empty(photoLink.Groups.Declines);
        Assert.Empty(photoLink.Enqueuer.Enqueued);
        Assert.Empty(discarded.Groups.Declines);
    }

    [Fact]
    public async Task ADeclineNeedsCaseworkAuthority()
    {
        var harness = new Harness(IntakeSourceChannel.ManualUpload);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            harness.Command.ExecuteAsync(
                harness.Group.Id, true, PossibleImageMatches, ActionActor.SystemWorker("worker")));

        Assert.Empty(harness.Groups.Declines);
    }

    [Fact]
    public async Task ALeaveNotOfferedIsRefusedAndWritesNothing()
    {
        (bool AwaitsDecision, ImageMatchLeaveFile[] Files)[] notOffered =
        [
            (false, PossibleImageMatches),
            (true, Array.Empty<ImageMatchLeaveFile>()),
            (true, new ImageMatchLeaveFile[] { new(true, ImageReceipt()), new(false, ImageReceipt()) }),
            (true, new ImageMatchLeaveFile[] { new(true, ImageReceipt()), new(true, null) }),
            (true, new ImageMatchLeaveFile[] { new(true, ImageReceipt()), new(true, DocumentReceipt()) }),
        ];

        foreach (var (awaitsDecision, files) in notOffered)
        {
            var harness = new Harness(IntakeSourceChannel.ManualUpload);

            Assert.False(ImageMatchLeave.IsOffered(awaitsDecision, files));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.Command.ExecuteAsync(harness.Group.Id, awaitsDecision, files, harness.Staff));
            Assert.Empty(harness.Groups.Declines);
            Assert.Empty(harness.Enqueuer.Enqueued);
        }
    }

    [Fact]
    public async Task AnUploadAlreadyLeftIsAnsweredAsDone()
    {
        var harness = new Harness(
            IntakeSourceChannel.ManualUpload,
            declined: new("staff-1", DateTimeOffset.UtcNow));

        // Not offered any longer, and not refused: the members are re-driven as before.
        await harness.Command.ExecuteAsync(harness.Group.Id, false, [], harness.Staff);

        Assert.Equal(
            harness.Group.Members.Select(member => member.StagedReceiptId).Order(),
            harness.Enqueuer.Enqueued.Order());
    }

    private static IntakeReceipt ImageReceipt() => Receipt("vehicle.jpg", "image/jpeg");

    private static IntakeReceipt DocumentReceipt() => Receipt("report.pdf", "application/pdf");

    private static IntakeReceipt Receipt(string fileName, string mediaType)
    {
        var id = Guid.NewGuid();
        var hash = new string('a', 64);
        return new(
            id,
            fileName,
            mediaType,
            10,
            hash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, id.ToString("N")),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            IntakeDecision.NeedsSorting,
            "Waiting for a person.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            null,
            null,
            [
                new IntakeAssetRecord(
                    Guid.NewGuid(), "uploaded source", fileName, mediaType,
                    IntakeAssetKind.Source, IntakeAssetDisposition.Source,
                    10, hash, "storage/0", null, null, null, null)
            ]);
    }

    private sealed class Harness
    {
        public Harness(
            IntakeSourceChannel channel,
            IntakeSubmissionGroupDiscard? discard = null,
            string? declaredRegistration = null,
            ImageMatchDeclined? declined = null)
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
                Discard: discard,
                ImageMatchDeclined: declined)
            {
                DeclaredRegistration = declaredRegistration
            };
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
