using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class StreamedGroupedIntakeTests
{
    [Fact]
    public async Task StagesStreamedMembersInOrdinalOrderUnderOneManualGroup()
    {
        var submission = new RecordingSubmission();
        var groups = new RecordingGroupStore(submission);
        var request = new StreamedGroupedIntakeSubmissionRequest(
            "manual-stream-group",
            "staff:test",
            DateTimeOffset.UtcNow,
            [
                new(1, Source("two.jpg", [0x02])),
                new(0, Source("one.jpg", [0x01]))
            ],
            IntakeSourceChannel.ManualUpload);

        var result = await new SubmitGroupedIntake(submission, groups, TimeProvider.System)
            .ExecuteStreamedAsync(request);

        Assert.Equal(["one.jpg", "two.jpg"], submission.Sources.Select(source => source.FileName));
        Assert.Equal([0, 1], result.Members.Select(member => member.Ordinal));
        Assert.Equal(1, groups.CreationCalls);
        Assert.All(submission.Sources, source =>
            Assert.Equal(IntakeSourceChannel.ManualUpload, source.SourceIdentity.Channel));
    }

    [Fact]
    public async Task RejectsAnOverBudgetStreamedGroupBeforeWritingItsGroup()
    {
        var submission = new RecordingSubmission();
        var groups = new RecordingGroupStore(submission);
        var request = new StreamedGroupedIntakeSubmissionRequest(
            "manual-stream-over-budget",
            "staff:test",
            DateTimeOffset.UtcNow,
            [
                new(0, Source("one.jpg", [0x01], IntakeEnvelopeLimits.MaximumContentLength)),
                new(1, Source("two.jpg", [0x02], IntakeEnvelopeLimits.MaximumContentLength)),
                new(2, Source("three.jpg", [0x03], IntakeEnvelopeLimits.MaximumContentLength))
            ],
            IntakeSourceChannel.ManualUpload);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SubmitGroupedIntake(submission, groups, TimeProvider.System)
                .ExecuteStreamedAsync(request));

        Assert.Equal(0, groups.CreationCalls);
        Assert.Empty(submission.Sources);
    }

    private static StreamedIntakeSource Source(
        string fileName,
        byte[] content,
        long? declaredLength = null) =>
        new(
            fileName,
            "image/jpeg",
            declaredLength ?? content.LongLength,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
            DateTimeOffset.UtcNow,
            "staff:test",
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "manual-stream-group"));

    private sealed class RecordingSubmission : IIntakeSubmission
    {
        private readonly Dictionary<Guid, string> hashByReceiptId = [];

        public List<StreamedIntakeSource> Sources { get; } = [];

        public Task<ReceivedIntake> ExecuteAsync(
            IntakeSource source,
            string operationKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This test exercises streamed intake only.");

        public async Task<ReceivedIntake> ExecuteStreamedAsync(
            StreamedIntakeSource source,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            var receiptId = Guid.NewGuid();
            hashByReceiptId[receiptId] = await ReceiveIntake.StreamHashAsync(source, cancellationToken);
            Sources.Add(source);
            return new ReceivedIntake(receiptId, IsDuplicate: false);
        }

        public string HashFor(Guid receiptId) => hashByReceiptId[receiptId];
    }

    private sealed class RecordingGroupStore(RecordingSubmission submission) : IIntakeSubmissionGroupStore
    {
        private readonly Guid groupId = Guid.NewGuid();
        private readonly List<IntakeSubmissionGroupMember> members = [];
        private IntakeSubmissionGroup? group;

        public int CreationCalls { get; private set; }

        public Task<IReadOnlyList<Guid>> ListPendingImageGroupReceiptsAsync(
            int maximumItems,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IntakeSubmissionGroup?> GetAsync(
            Guid requestedGroupId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedGroupId == groupId ? group : null);

        public Task<IntakeSubmissionGroup?> FindAsync(
            IntakeSourceChannel channel,
            string submissionToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(group?.Channel == channel && group.SubmissionToken == submissionToken
                ? group
                : null);

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(
            Guid requestedGroupId,
            IntakeSourceChannel channel,
            string submissionToken,
            int expectedMemberCount,
            string actor,
            DateTimeOffset receivedAtUtc,
            Guid? parentReceiptId,
            CancellationToken cancellationToken = default)
        {
            CreationCalls++;
            group ??= new IntakeSubmissionGroup(
                groupId,
                channel,
                submissionToken,
                expectedMemberCount,
                actor,
                receivedAtUtc,
                members,
                parentReceiptId);
            return Task.FromResult(group);
        }

        public Task<IntakeSubmissionGroupMember?> FindMemberAsync(
            Guid requestedGroupId,
            int ordinal,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroupMember?>(
                members.SingleOrDefault(member => member.GroupId == requestedGroupId && member.Ordinal == ordinal));

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(
            Guid requestedGroupId,
            int ordinal,
            ReceivedIntake received,
            CancellationToken cancellationToken = default)
        {
            var member = new IntakeSubmissionGroupMember(
                requestedGroupId,
                ordinal,
                received.StagedReceiptId,
                $"file-{ordinal}.jpg",
                submission.HashFor(received.StagedReceiptId),
                received.IsDuplicate);
            members.Add(member);
            return Task.FromResult(member);
        }

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
            Guid requestedGroupId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IntakeSubmissionGroupMember>>(
                members.OrderBy(member => member.Ordinal).ToArray());
    }
}
