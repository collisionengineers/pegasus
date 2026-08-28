using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Core.Tests.ProviderApi;

public sealed class ProviderSubmissionTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid PrincipalId = Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b");
    private static readonly Guid OtherPrincipalId = Guid.Parse("7a1b2c3d-0000-4000-8000-000000000001");
    private const string KeyId = "AAAAAAAAAAAAAAAA";
    private static readonly PrincipalCredentialAuthentication Active =
        new(PrincipalId, KeyId, PrincipalCredentialState.Active);
    private static readonly PrincipalCredentialAuthentication Paused =
        new(PrincipalId, KeyId, PrincipalCredentialState.Paused);

    private static ProviderSubmissionFile File(int ordinal, byte value = 1) =>
        new(ordinal, $"instruction-{ordinal}.pdf", "application/pdf", new byte[] { value, 2, 3 });

    private static ProviderSubmissionRequest Request(
        PrincipalCredentialAuthentication credential,
        string key = "order-1",
        params ProviderSubmissionFile[] files) =>
        new(credential, key, " REF-9 ", files.Length == 0 ? [File(0)] : files, "trace-1");

    [Fact]
    public async Task SubmissionEntersGroupedIntakeOnTheProviderChannelBoundToThePrincipalActor()
    {
        var store = new FakeStore();
        var grouped = new FakeGroupedSubmission();
        var history = new FakeHistory();
        var submit = new SubmitProviderInstruction(store, grouped, grouped, history, new FixedTime());

        var receipt = await submit.ExecuteAsync(Request(Active, files: [File(0), File(1, 9)]), default);

        var request = Assert.Single(grouped.Requests);
        Assert.Equal(IntakeSourceChannel.ProviderApi, request.Channel);
        Assert.Equal(ProviderSubmissionPolicy.SubmissionToken(receipt.SubmissionId), request.SubmissionToken);
        Assert.Equal($"provider:{PrincipalId:D}", request.Actor);
        Assert.All(request.Files, file => Assert.Equal(request.Actor, file.Source.Actor));
        Assert.Equal([0, 1], request.Files.Select(file => file.Ordinal));
        Assert.Equal("REF-9", receipt.ProviderReference);
        Assert.False(receipt.Replayed);
        Assert.Equal(2, receipt.Files.Count);
        var record = Assert.Single(store.Records.Values);
        Assert.Equal(PrincipalId, record.PrincipalId);
        Assert.Equal(KeyId, record.KeyId);
        Assert.Equal("order-1", record.IdempotencyKey);
        var entry = Assert.Single(history.Entries);
        Assert.Equal(ActorKind.Provider, entry.Actor.Kind);
        Assert.Equal(PrincipalId.ToString("D"), entry.Actor.SubjectId);
        Assert.Equal("Accepted", entry.Outcome);
        Assert.Equal(receipt.SubmissionId.ToString("D"), entry.AggregateId);
    }

    [Fact]
    public async Task PausedCredentialIsRefusedBeforeAnythingIsRetained()
    {
        var store = new FakeStore();
        var grouped = new FakeGroupedSubmission();
        var submit = new SubmitProviderInstruction(store, grouped, grouped, new FakeHistory(), new FixedTime());

        var refused = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => submit.ExecuteAsync(Request(Paused), default));

        Assert.Equal(ProviderSubmissionError.CredentialPaused, refused.Error);
        Assert.Empty(store.Records);
        Assert.Empty(grouped.Requests);
    }

    [Fact]
    public async Task ReplayOfTheSameKeyReturnsTheSameSubmissionAndDifferentContentFailsClosed()
    {
        var store = new FakeStore();
        var grouped = new FakeGroupedSubmission();
        var history = new FakeHistory();
        var submit = new SubmitProviderInstruction(store, grouped, grouped, history, new FixedTime());

        var first = await submit.ExecuteAsync(Request(Active), default);
        var replay = await submit.ExecuteAsync(Request(Active), default);
        Assert.Equal(first.SubmissionId, replay.SubmissionId);
        Assert.True(replay.Replayed);
        Assert.Single(store.Records);
        Assert.Equal("Replayed", history.Entries[^1].Outcome);

        var conflict = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => submit.ExecuteAsync(Request(Active, files: [File(0, 7)]), default));
        Assert.Equal(ProviderSubmissionError.IdempotencyKeyConflict, conflict.Error);
        Assert.Equal("Refused", history.Entries[^1].Outcome);

        var countConflict = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => submit.ExecuteAsync(Request(Active, files: [File(0), File(1)]), default));
        Assert.Equal(ProviderSubmissionError.IdempotencyKeyConflict, countConflict.Error);
    }

    [Fact]
    public async Task LosingAConcurrentInsertResolvesToTheWinnersSubmission()
    {
        var store = new FakeStore { ConflictOnce = true };
        var grouped = new FakeGroupedSubmission();
        var submit = new SubmitProviderInstruction(store, grouped, grouped, new FakeHistory(), new FixedTime());

        var receipt = await submit.ExecuteAsync(Request(Active), default);

        Assert.Equal(store.Records.Keys.Single(), receipt.SubmissionId);
    }

    [Fact]
    public async Task EnvelopeLimitsAreTheStaffUploadLimits()
    {
        var store = new FakeStore();
        var grouped = new FakeGroupedSubmission();
        var submit = new SubmitProviderInstruction(store, grouped, grouped, new FakeHistory(), new FixedTime());

        var tooMany = Enumerable.Range(0, IntakeEnvelopeLimits.MaximumBatchFileCount + 1)
            .Select(ordinal => File(ordinal))
            .ToArray();
        var count = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => submit.ExecuteAsync(Request(Active, files: tooMany), default));
        Assert.Equal(ProviderSubmissionError.EnvelopeExceeded, count.Error);

        var oversize = new ProviderSubmissionFile(
            0, "big.pdf", "application/pdf", new byte[IntakeEnvelopeLimits.MaximumContentLength + 1]);
        var size = await Assert.ThrowsAsync<ProviderSubmissionException>(
            () => submit.ExecuteAsync(Request(Active, files: oversize), default));
        Assert.Equal(ProviderSubmissionError.EnvelopeExceeded, size.Error);

        await Assert.ThrowsAsync<ArgumentException>(
            () => submit.ExecuteAsync(Request(Active, files: File(1)), default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => submit.ExecuteAsync(Request(Active, key: " "), default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => submit.ExecuteAsync(
                Request(Active, files: new ProviderSubmissionFile(0, "../x.pdf", "application/pdf", new byte[] { 1 })),
                default));
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task ResultIsReadableWhilePausedAndNeverAcrossPrincipals()
    {
        var store = new FakeStore();
        var grouped = new FakeGroupedSubmission();
        var submit = new SubmitProviderInstruction(store, grouped, grouped, new FakeHistory(), new FixedTime());
        var receipt = await submit.ExecuteAsync(Request(Active, files: [File(0), File(1)]), default);
        var member = grouped.Group!.Members[0];
        var status = new FakeStatus();
        status.Statuses[member.StagedReceiptId] = new(
            member.StagedReceiptId, member.SourceFileName, Now, QueuedIntakeStatusKind.Complete,
            Guid.NewGuid(), null, null);
        status.Receipts[status.Statuses[member.StagedReceiptId].ProcessedReceiptId!.Value] =
            Receipt(IntakeDecision.CaseCreated, "QDOS-000123", null);
        var get = new GetProviderSubmissionResult(store, grouped, status, status);

        var result = await get.ExecuteAsync(Paused, receipt.SubmissionId, default);

        Assert.NotNull(result);
        Assert.Equal(QueuedIntakeStatusKind.Received, result.Status);
        Assert.Equal("QDOS-000123", result.CaseReference);
        Assert.Equal(IntakeDecision.CaseCreated, result.Files[0].Decision);
        Assert.Null(result.Files[0].AllocationFailure);
        Assert.Equal("QDOS-000123", result.Files[0].CaseReference);
        Assert.Equal(QueuedIntakeStatusKind.Received, result.Files[1].Status);
        Assert.Null(result.Files[1].Decision);

        Assert.Null(await get.ExecuteAsync(
            new(OtherPrincipalId, KeyId, PrincipalCredentialState.Active), receipt.SubmissionId, default));
        Assert.Null(await get.ExecuteAsync(Active, Guid.NewGuid(), default));
    }

    [Fact]
    public void TheProviderActorHoldsOnlyItsOwnRight()
    {
        var actor = ActionActor.Provider(PrincipalId);
        Assert.Equal(ActorKind.Provider, actor.Kind);
        Assert.True(StaffAuthorization.IsAuthorized(actor, StaffAccessRight.SubmitProviderInstruction));
        Assert.All(
            Enum.GetValues<StaffAccessRight>().Where(right => right != StaffAccessRight.SubmitProviderInstruction),
            right => Assert.False(StaffAuthorization.IsAuthorized(actor, right)));
        Assert.False(StaffAuthorization.IsAuthorized(
            ActionActor.Automation("automation"), StaffAccessRight.SubmitProviderInstruction));
        Assert.Throws<ArgumentException>(() => ActionActor.Provider(Guid.Empty));
    }

    private static IntakeReceipt Receipt(
        IntakeDecision decision,
        string? caseReference,
        IntakeAllocationFailureKind? failureKind) =>
        new(
            Guid.NewGuid(), "instruction-0.pdf", "application/pdf", 3, "HASH",
            new(IntakeSourceChannel.ProviderApi, "token"), Now, Now, decision, "reason",
            [], [], null, [], null, null, false, "reader", "1", null, null,
            AllocationState: new(Guid.NewGuid(), IntakeAllocationProjectionStatus.Succeeded, null, Now, failureKind),
            AcceptedCaseReference: caseReference);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeStore : IProviderSubmissionStore
    {
        public Dictionary<Guid, ProviderSubmissionRecord> Records { get; } = [];
        public bool ConflictOnce { get; set; }

        public Task CreateAsync(ProviderSubmissionRecord record, CancellationToken cancellationToken)
        {
            if (ConflictOnce)
            {
                // The winner of the race is another row under the same key,
                // keyed by its own id as every other row in this fake is.
                ConflictOnce = false;
                var winner = record with { Id = Guid.NewGuid() };
                Records[winner.Id] = winner;
                throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
            }
            if (Records.Values.Any(item =>
                    item.PrincipalId == record.PrincipalId && item.IdempotencyKey == record.IdempotencyKey))
            {
                throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
            }

            Records[record.Id] = record;
            return Task.CompletedTask;
        }

        public Task<ProviderSubmissionRecord?> FindByIdempotencyKeyAsync(
            Guid principalId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Records.Values.SingleOrDefault(item =>
                item.PrincipalId == principalId && item.IdempotencyKey == idempotencyKey));

        public Task<ProviderSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Records.GetValueOrDefault(id));
    }

    /// <summary>
    /// Stands in for the grouped owner and its store together: one group per
    /// token, content-hash dedup per ordinal, conflict on a changed hash.
    /// </summary>
    private sealed class FakeGroupedSubmission : IGroupedIntakeSubmission, IIntakeSubmissionGroupStore
    {
        public List<GroupedIntakeSubmissionRequest> Requests { get; } = [];
        public IntakeSubmissionGroup? Group { get; private set; }

        public Task<GroupedIntakeSubmissionResult> ExecuteAsync(
            GroupedIntakeSubmissionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var members = new List<IntakeSubmissionGroupMember>();
            foreach (var file in request.Files)
            {
                var hash = ProviderSubmissionPolicy.Sha256(file.Source.Content);
                var existing = Group?.Members.SingleOrDefault(member => member.Ordinal == file.Ordinal);
                if (existing is not null && existing.SourceHash != hash)
                {
                    throw new IntakeSourceIdentityConflictException(existing.SourceHash, hash);
                }

                members.Add(existing is null
                    ? new(Group?.Id ?? Guid.NewGuid(), file.Ordinal, Guid.NewGuid(), file.Source.FileName, hash, false)
                    : existing with { IsDuplicate = true });
            }

            Group = Group is null
                ? new(members[0].GroupId, request.Channel, request.SubmissionToken, request.Files.Count,
                    request.Actor, request.ReceivedAtUtc, members)
                : Group with { Members = members };
            return Task.FromResult(new GroupedIntakeSubmissionResult(Group, members));
        }

        public Task<IntakeSubmissionGroup?> FindAsync(
            IntakeSourceChannel channel, string submissionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Group is { } group && group.Channel == channel && group.SubmissionToken == submissionToken
                ? group
                : null);

        public Task<IntakeSubmissionGroup?> GetAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(
            Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount,
            string actor, DateTimeOffset receivedAtUtc, Guid? parentReceiptId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember?> FindMemberAsync(
            Guid groupId, int ordinal, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(
            Guid groupId, int ordinal, ReceivedIntake received, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
            Guid groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeHistory : IActionHistoryWriter
    {
        public List<ActionHistoryEntry> Entries { get; } = [];

        public Task AppendAsync(ActionHistoryEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStatus : IQueuedIntakeStatusQueries, IIntakeReceiptQueries
    {
        public Dictionary<Guid, QueuedIntakeStatus> Statuses { get; } = [];
        public Dictionary<Guid, IntakeReceipt> Receipts { get; } = [];

        public Task<QueuedIntakeStatus?> GetAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Statuses.GetValueOrDefault(stagedReceiptId));

        Task<IntakeReceipt?> IIntakeReceiptQueries.GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Receipts.GetValueOrDefault(id));

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId, Guid assetId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
