using System.Security.Cryptography;
using Pegasus.Core.Documents;
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
            TimeProvider.System).ExecuteStreamedAsync(Request("batch"));

        Assert.NotEqual(Guid.Empty, result.Group.Id);
        Assert.Equal(["one.jpg", "two.jpg"], submission.Sources.Select(item => item.FileName));
        Assert.Equal([0, 1], result.Members.Select(item => item.Ordinal));
        Assert.Single(result.Members.Select(item => item.GroupId).Distinct());
        Assert.Equal(2, result.Group.Members.Count);
    }

    [Fact]
    public async Task TheRosterIsTheHeldMembersPlusTheOnesAddedWithoutReadingTheGroupAgain()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var request = Request("roster");
        // The first call adds both files; the group then holds only ordinal 0, so the second adds ordinal 1.
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteStreamedAsync(request);
        store.Members.RemoveAll(member => member.Ordinal == 1);

        var result = await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteStreamedAsync(request);

        Assert.Equal([0, 1], result.Members.Select(item => item.Ordinal));
        Assert.Equal([true, false], result.Members.Select(item => item.IsDuplicate));
        Assert.Equal(0, store.ListMembersCalls);
    }

    [Fact]
    public async Task ReplaysExistingMembersWithoutSubmittingThemAgain()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var request = Request("replay");
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteStreamedAsync(request);
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteStreamedAsync(request);

        Assert.Equal(2, submission.Sources.Count);
        Assert.Equal(2, store.Members.Count);
    }

    [Fact]
    public async Task PreservesParentReceiptAndMemberTokensForEveryChild()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var parentReceiptId = Guid.NewGuid();
        var request = Request("mail-images") with { ParentReceiptId = parentReceiptId };

        var result = await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteStreamedAsync(request);

        Assert.Equal(IntakeSourceChannel.ManualUpload, result.Group.Channel);
        Assert.Equal(parentReceiptId, result.Group.ParentReceiptId);
        Assert.All(submission.Sources, source =>
            Assert.Equal(IntakeSourceChannel.ManualUpload, source.SourceIdentity.Channel));
        Assert.Equal(
            ["mail-images", "mail-images:1"],
            submission.Sources.Select(source => source.SourceIdentity.ExternalReceiptToken));
    }

    /// <summary>
    /// A type chosen at upload (R564) belongs to a declared Case and is one
    /// the Upload page offers; a submission that says otherwise is malformed
    /// and is refused before any file is staged.
    /// </summary>
    [Fact]
    public async Task RefusesADocumentTypeWithoutADeclaredCase()
    {
        var submission = new FakeSubmission();
        var request = Request("typed-no-case") with
        {
            Files = [new(0, Source("estimate.pdf", [1]) with { DeclaredDocumentType = DocumentSemanticRole.Estimate })]
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SubmitGroupedIntake(submission, new FakeGroupStore(), TimeProvider.System)
                .ExecuteStreamedAsync(request));
        Assert.Empty(submission.Sources);
    }

    [Fact]
    public async Task RefusesADocumentTypeTheUploadDoesNotOffer()
    {
        var submission = new FakeSubmission();
        var request = Request("typed-audit") with
        {
            DeclaredCaseId = Guid.NewGuid(),
            Files = [new(0, Source("report.pdf", [1]) with { DeclaredDocumentType = DocumentSemanticRole.AuditReport })]
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new SubmitGroupedIntake(submission, new FakeGroupStore(), TimeProvider.System)
                .ExecuteStreamedAsync(request));
        Assert.Empty(submission.Sources);
    }

    [Fact]
    public async Task EachFileKeepsItsOwnDocumentTypeUnderTheSubmissionsCase()
    {
        var submission = new FakeSubmission();
        var declaredCaseId = Guid.NewGuid();
        var request = Request("typed") with
        {
            DeclaredCaseId = declaredCaseId,
            Files =
            [
                new(0, Source("estimate.pdf", [1]) with { DeclaredDocumentType = DocumentSemanticRole.Estimate }),
                new(1, Source("letter.pdf", [2]))
            ]
        };

        await new SubmitGroupedIntake(submission, new FakeGroupStore(), TimeProvider.System)
            .ExecuteStreamedAsync(request);

        Assert.Equal(
            [DocumentSemanticRole.Estimate, null],
            submission.Sources.Select(source => source.DeclaredDocumentType));
        Assert.All(submission.Sources, source => Assert.Equal(declaredCaseId, source.DeclaredCaseId));
    }

    [Fact]
    public async Task RejectsConflictingReplayAtTheSameOrdinal()
    {
        var submission = new FakeSubmission();
        var store = new FakeGroupStore();
        var request = Request("conflict");
        await new SubmitGroupedIntake(submission, store, TimeProvider.System)
            .ExecuteStreamedAsync(request);

        var changed = request with
        {
            Files = [new(0, Source("different.jpg", [9, 9])) , new(1, Source("two.jpg", [2]))]
        };
        await Assert.ThrowsAsync<IntakeSourceIdentityConflictException>(() =>
            new SubmitGroupedIntake(submission, store, TimeProvider.System)
                .ExecuteStreamedAsync(changed));
    }

    [Theory]
    // A ':{ordinal>=1}' suffix names its parent by stripping; the bare token
    // remains a candidate because the shape alone cannot exclude a literal
    // colon-digit tail in a parent token.
    [InlineData("batch:1", new[] { "batch", "batch:1" })]
    [InlineData("batch:12", new[] { "batch", "batch:12" })]
    // An ordinal-0 member carries the parent token verbatim, so
    // the bare token IS the parent candidate.
    [InlineData("batch", new[] { "batch" })]
    // Shapes Create never emits are not member suffixes: ':0', a signed or
    // non-numeric tail, and a leading colon all resolve as bare tokens only.
    [InlineData("batch:0", new[] { "batch:0" })]
    [InlineData("batch:+1", new[] { "batch:+1" })]
    [InlineData("batch:abc", new[] { "batch:abc" })]
    [InlineData(":1", new[] { ":1" })]
    public void ParentTokenCandidatesInvertTheMemberTokenConvention(
        string memberToken,
        string[] expected)
    {
        Assert.Equal(expected, GroupedIntakeMemberToken.ParentTokenCandidates(memberToken));
    }

    [Fact]
    public void ParentTokenCandidatesRoundTripEveryOrdinalCreateEmits()
    {
        // The inverse must reach the parent for every token Create can
        // produce — the ordinal-0 gap was exactly this property failing.
        for (var ordinal = 0; ordinal < 4; ordinal++)
        {
            var memberToken = GroupedIntakeMemberToken.Create("submission", ordinal);
            Assert.Contains(
                "submission",
                GroupedIntakeMemberToken.ParentTokenCandidates(memberToken));
        }
    }

    private static StreamedGroupedIntakeSubmissionRequest Request(string token) =>
        new(token, "staff:test", DateTimeOffset.UtcNow,
        [
            new(0, Source("one.jpg", [1])),
            new(1, Source("two.jpg", [2]))
        ],
        IntakeSourceChannel.ManualUpload);

    private static StreamedIntakeSource Source(string name, byte[] bytes) =>
        new(
            name,
            "image/jpeg",
            bytes.LongLength,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false)),
            DateTimeOffset.UtcNow,
            "staff:test",
            new(IntakeSourceChannel.ManualUpload, "form"));

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

        public Task<ReceivedIntake> ExecuteStreamedAsync(
            StreamedIntakeSource source,
            string operationKey,
            CancellationToken cancellationToken = default) =>
            ReceiveStreamedAsync(source, operationKey, cancellationToken);

        public Task PublishCommittedAsync(
            ReceivedIntake received,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<ReceivedIntake> ReceiveStreamedAsync(
            StreamedIntakeSource source,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            using var content = await source.OpenContentAsync(cancellationToken);
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            return await ExecuteAsync(
                new(
                    source.FileName,
                    source.MediaType,
                    buffer.ToArray(),
                    source.ReceivedAtUtc,
                    source.Actor,
                    source.SourceIdentity,
                    source.DeclaredCaseId,
                    source.DeclaredDocumentType),
                operationKey,
                cancellationToken);
        }
    }

    private sealed class FakeGroupStore : IIntakeSubmissionGroupStore
    {
        public Task<IReadOnlyList<Guid>> ListPendingImageGroupReceiptsAsync(int maximumItems, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Guid GroupId { get; } = Guid.NewGuid();
        public List<IntakeSubmissionGroupMember> Members { get; } = [];
        private string? Token { get; set; }
        private int ExpectedMemberCount { get; set; } = 1;
        private IntakeSourceChannel Channel { get; set; } = IntakeSourceChannel.ManualUpload;
        private Guid? ParentReceiptId { get; set; }
        private string? Actor { get; set; }
        private DateTimeOffset ReceivedAt { get; set; }

        public Task<IntakeSubmissionGroup?> GetAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroup?>(groupId == GroupId ? Group() : null);

        public Task<IntakeSubmissionGroup?> FindAsync(IntakeSourceChannel channel, string submissionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeSubmissionGroup?>(Token == submissionToken ? Group() : null);

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount, string actor, DateTimeOffset receivedAtUtc, Guid? parentReceiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(EnsureGroup(channel, submissionToken, expectedMemberCount, actor, receivedAtUtc, parentReceiptId));

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(Guid groupId, int ordinal, ReceivedIntake received, CancellationToken cancellationToken = default)
        {
            var member = new IntakeSubmissionGroupMember(groupId, ordinal, received.StagedReceiptId, $"file-{ordinal}.jpg", Convert.ToHexString(SHA256.HashData([(byte)(ordinal + 1)])), received.IsDuplicate);
            Members.Add(member);
            return Task.FromResult(member);
        }

        public int ListMembersCalls { get; private set; }

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            ListMembersCalls++;
            return Task.FromResult<IReadOnlyList<IntakeSubmissionGroupMember>>(Members.OrderBy(item => item.Ordinal).ToArray());
        }

        private IntakeSubmissionGroup EnsureGroup(IntakeSourceChannel channel, string token, int expectedMemberCount, string actor, DateTimeOffset received, Guid? parentReceiptId)
        {
            if (Token is null)
            {
                Token = token;
                ExpectedMemberCount = expectedMemberCount;
                Channel = channel;
                ParentReceiptId = parentReceiptId;
            }

            Actor ??= actor;
            ReceivedAt = received;
            return Group();
        }

        private IntakeSubmissionGroup Group() =>
            new(GroupId, Channel, Token ?? "", ExpectedMemberCount, Actor ?? "staff:test", ReceivedAt, Members.ToArray(), ParentReceiptId);
    }
}
