using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class GroupedIntakeTests
{
    [Fact]
    public async Task SubmitsEveryFileInOrdinalOrderAndKeepsOneGroup()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var result = await new SubmitGroupedIntake(
            submission,
            store,
            TimeProvider.System).ExecuteAsync(Request("batch"));

        Assert.NotEqual(Guid.Empty, result.Group.Id);
        Assert.Equal(["one.jpg", "two.jpg"], submission.Sources.Select(item => item.FileName));
        Assert.Equal([0, 1], result.Members.Select(item => item.Ordinal));
        Assert.Single(result.Members.Select(item => item.GroupId).Distinct());
        Assert.Equal(2, result.Group.Members.Count);
    }

    [Fact]
    public async Task ReplaysExistingMembersWithoutSubmittingThemAgain()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var request = Request("replay");
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteAsync(request);
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteAsync(request);

        Assert.Equal(2, submission.Sources.Count);
        Assert.Equal(2, store.Members.Count);
    }

    [Fact]
    public async Task RejectsConflictingReplayAtTheSameOrdinal()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var request = Request("conflict");
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteAsync(request);

        var changed = request with
        {
            Files = [new(0, Source("different.jpg", [9, 9])) , new(1, Source("two.jpg", [2]))]
        };
        await Assert.ThrowsAsync<IntakeSourceIdentityConflictException>(() =>
            new SubmitGroupedIntake(submission, store, TimeProvider.System)
                .ExecuteAsync(changed));
    }

    private static GroupedIntakeSubmissionRequest Request(string token) =>
        new(token, "staff:test", DateTimeOffset.UtcNow,
        [
            new(0, Source("one.jpg", [1])),
            new(1, Source("two.jpg", [2]))
        ]);

    private static IntakeSource Source(string name, byte[] bytes) =>
        new(name, "image/jpeg", bytes, DateTimeOffset.UtcNow, "staff:test", new(IntakeSourceChannel.ManualUpload, "form"));

    private sealed class FakeSubmission : IIntakeSubmission
    {
        public List<IntakeSource> Sources { get; } = [];

        public Task<ReceivedIntake> ExecuteAsync(
            IntakeSource source,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            Sources.Add(source);
            return Task.FromResult(new ReceivedIntake(Guid.NewGuid(), false));
        }
    }

    private sealed class FakeGroupStore : IIntakeSubmissionGroupStore
    {
        public Guid GroupId { get; } = Guid.NewGuid();
        public List<IntakeSubmissionGroupMember> Members { get; } = [];
        private string? Token { get; set; }
        private int ExpectedMemberCount { get; set; } = 1;
        private string? Actor { get; set; }
        private DateTimeOffset ReceivedAt { get; set; }

        public Task<IntakeSubmissionGroup?> GetAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroup?>(groupId == GroupId ? Group() : null);

        public Task<IntakeSubmissionGroup?> FindAsync(IntakeSourceChannel channel, string submissionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroup?>(Token == submissionToken ? Group() : null);

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount, string actor, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(EnsureGroup(channel, submissionToken, expectedMemberCount, actor, receivedAtUtc));

        public Task<IntakeSubmissionGroupMember?> FindMemberAsync(Guid groupId, int ordinal, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroupMember?>(Members.SingleOrDefault(item => item.GroupId == GroupId && item.Ordinal == ordinal));

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(Guid groupId, int ordinal, ReceivedIntake received, CancellationToken cancellationToken = default)
        {
            var member = new IntakeSubmissionGroupMember(groupId, ordinal, received.StagedReceiptId, $"file-{ordinal}.jpg", Convert.ToHexString(SHA256.HashData([(byte)(ordinal + 1)])), received.IsDuplicate);
            Members.Add(member);
            return Task.FromResult(member);
        }

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IntakeSubmissionGroupMember>>(Members.OrderBy(item => item.Ordinal).ToArray());

        private IntakeSubmissionGroup EnsureGroup(IntakeSourceChannel channel, string token, int expectedMemberCount, string actor, DateTimeOffset received)
        {
            if (Token is null)
            {
                Token = token;
                ExpectedMemberCount = expectedMemberCount;
            }

            Actor ??= actor;
            ReceivedAt = received;
            return Group();
        }

        private IntakeSubmissionGroup Group() =>
            new(GroupId, IntakeSourceChannel.ManualUpload, Token ?? "", ExpectedMemberCount, Actor ?? "staff:test", ReceivedAt, Members);
    }
}
