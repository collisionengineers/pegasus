using System.Collections.Immutable;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class OperationsUseCaseTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmailProjectionUsesTheCoreBoundAndCurrentStaffActor()
    {
        var store = new RecordingEmailStore(EmptyEmailProjection());
        var query = new GetEmailOperations(store, new FixedTimeProvider(FixedUtcNow));

        var result = await query.ExecuteAsync(StaffActor(), CancellationToken.None);

        Assert.Empty(result.Received);
        Assert.Equal(GetEmailOperations.MaximumItemsPerDirection, store.MaximumItems);
        Assert.Equal(FixedUtcNow, store.AsOfUtc);
    }

    [Fact]
    public async Task EmailProjectionRejectsAutomatedActorsBeforeReadingState()
    {
        var store = new RecordingEmailStore(EmptyEmailProjection());
        var query = new GetEmailOperations(store, new FixedTimeProvider(FixedUtcNow));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            query.ExecuteAsync(ActionActor.SystemWorker("worker"), CancellationToken.None));

        Assert.Null(store.MaximumItems);
    }

    [Fact]
    public async Task EmailProjectionRejectsAnAdapterResultBeyondTheCoreBound()
    {
        var items = Enumerable.Range(0, GetEmailOperations.MaximumItemsPerDirection + 1)
            .Select(index => EmailItem($"received:{index}"))
            .ToImmutableArray();
        var store = new RecordingEmailStore(new(
            items,
            ImmutableArray<EmailOperationProjection>.Empty,
            ReceivedLimitReached: true,
            SentLimitReached: false));
        var query = new GetEmailOperations(store, new FixedTimeProvider(FixedUtcNow));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            query.ExecuteAsync(StaffActor(), CancellationToken.None));
    }

    [Fact]
    public async Task MailboxRetryCarriesDirectionFailureVersionAndServerTime()
    {
        var store = new RecordingMailboxRetryStore();
        var command = new RetryMailboxProcessingCommand(
            "approved-inbox",
            EmailOperationDirection.Received,
            "source_unavailable",
            FixedUtcNow.AddMinutes(5),
            StaffActor(),
            "retry-operation");
        var retry = new RetryMailboxProcessing(store, new FixedTimeProvider(FixedUtcNow));

        var result = await retry.ExecuteAsync(command, CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.Equal(command, store.Command);
        Assert.Equal(FixedUtcNow, store.RetryAtUtc);
    }

    [Fact]
    public async Task RequestProjectionAndExternalRetryAreStaffBounded()
    {
        var projectionStore = new RecordingRequestStore();
        var retryStore = new RecordingExternalRetryStore();
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var query = new GetRequestOperations(projectionStore, timeProvider);
        var retry = new RetryExternalWork(retryStore, timeProvider);
        var actor = StaffActor();
        var workId = Guid.NewGuid();

        var projection = await query.ExecuteAsync(actor, CancellationToken.None);
        var result = await retry.ExecuteAsync(
            new(workId, 4, actor, "external-retry"),
            CancellationToken.None);

        Assert.Empty(projection.Items);
        Assert.Equal(GetRequestOperations.MaximumItems, projectionStore.MaximumItems);
        Assert.Equal(FixedUtcNow, projectionStore.AsOfUtc);
        Assert.False(result.IsReplay);
        Assert.Equal(workId, retryStore.Command?.WorkItemId);
        Assert.Equal(4, retryStore.Command?.ExpectedAttemptCount);
        Assert.Equal(FixedUtcNow, retryStore.RetryAtUtc);
    }

    private static ActionActor StaffActor() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    private static EmailOperationsProjection EmptyEmailProjection() => new(
        ImmutableArray<EmailOperationProjection>.Empty,
        ImmutableArray<EmailOperationProjection>.Empty,
        ReceivedLimitReached: false,
        SentLimitReached: false);

    private static EmailOperationProjection EmailItem(string id) => new(
        id,
        EmailOperationDirection.Received,
        EmailOperationState.Succeeded,
        MailboxIdentity: null,
        FixedUtcNow,
        IntakeId: null,
        TriageId: null,
        CaseId: null,
        CaseReference: null,
        PrincipalCode: null,
        FailureCode: null,
        RetryMailboxId: null,
        RetryExpectedDueAtUtc: null);

    private sealed class RecordingEmailStore(EmailOperationsProjection result)
        : IEmailOperationsProjectionStore
    {
        public int? MaximumItems { get; private set; }

        public DateTimeOffset? AsOfUtc { get; private set; }

        public Task<EmailOperationsProjection> GetAsync(
            int maximumItemsPerDirection,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            MaximumItems = maximumItemsPerDirection;
            AsOfUtc = nowUtc;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingMailboxRetryStore : IMailboxProcessingRetryStore
    {
        public RetryMailboxProcessingCommand? Command { get; private set; }

        public DateTimeOffset? RetryAtUtc { get; private set; }

        public Task<OperationsRetryResult> RetryAsync(
            RetryMailboxProcessingCommand command,
            DateTimeOffset retryAtUtc,
            CancellationToken cancellationToken)
        {
            Command = command;
            RetryAtUtc = retryAtUtc;
            return Task.FromResult(new OperationsRetryResult(IsReplay: false));
        }
    }

    private sealed class RecordingRequestStore : IRequestOperationsProjectionStore
    {
        public int? MaximumItems { get; private set; }

        public DateTimeOffset? AsOfUtc { get; private set; }

        public Task<RequestOperationsProjection> GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            MaximumItems = maximumItems;
            AsOfUtc = nowUtc;
            return Task.FromResult(new RequestOperationsProjection(
                ImmutableArray<RequestOperationProjection>.Empty,
                LimitReached: false));
        }
    }

    private sealed class RecordingExternalRetryStore : IExternalWorkRetryStore
    {
        public RetryExternalWorkCommand? Command { get; private set; }

        public DateTimeOffset? RetryAtUtc { get; private set; }

        public Task<OperationsRetryResult> RetryAsync(
            RetryExternalWorkCommand command,
            DateTimeOffset retryAtUtc,
            CancellationToken cancellationToken)
        {
            Command = command;
            RetryAtUtc = retryAtUtc;
            return Task.FromResult(new OperationsRetryResult(IsReplay: false));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
