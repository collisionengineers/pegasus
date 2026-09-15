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
        var projectionStore = new RecordingRequestStore(EmptyRequestProjection());
        var retryStore = new RecordingExternalRetryStore();
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var query = new GetRequestOperations(projectionStore, timeProvider);
        var retry = new RetryExternalWork(retryStore, timeProvider);
        var actor = StaffActor();
        var workId = Guid.NewGuid();

        var projection = await query.ExecuteAsync(actor, cancellationToken: CancellationToken.None);
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

    [Fact]
    public async Task RequestProjectionUsesCallerInstantAtUploadExpiryBoundary()
    {
        var expiryUtc = FixedUtcNow;
        var capturedBeforeExpiryUtc = expiryUtc.AddTicks(-1);
        var projectionStore = new RecordingRequestStore(new(
            ImmutableArray.Create(ActiveUploadLink(expiryUtc)),
            LimitReached: false));
        var query = new GetRequestOperations(
            projectionStore,
            new FixedTimeProvider(expiryUtc.AddTicks(1)));

        var result = await query.ExecuteAsync(
            StaffActor(),
            asOfUtc: capturedBeforeExpiryUtc,
            cancellationToken: CancellationToken.None);

        Assert.Equal(capturedBeforeExpiryUtc, projectionStore.AsOfUtc);
        Assert.NotEqual(expiryUtc.AddTicks(1), projectionStore.AsOfUtc);
        Assert.Equal(RequestOperationState.Active, Assert.Single(result.Items).State);
    }

    private static ActionActor StaffActor() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    private static EmailOperationsProjection EmptyEmailProjection() => new(
        ImmutableArray<EmailOperationProjection>.Empty,
        ImmutableArray<EmailOperationProjection>.Empty,
        ReceivedLimitReached: false,
        SentLimitReached: false);

    private static RequestOperationsProjection EmptyRequestProjection() => new(
        ImmutableArray<RequestOperationProjection>.Empty,
        LimitReached: false);

    private static RequestOperationProjection ActiveUploadLink(DateTimeOffset expiryUtc) => new(
        Guid.NewGuid(),
        RequestOperationKind.PegasusUploadLink,
        RequestOperationState.Active,
        Guid.NewGuid(),
        "QDOS31001",
        "QDOS",
        expiryUtc.AddMinutes(-1),
        expiryUtc,
        1,
        AcceptedFileCount: 0,
        AcceptedByteCount: 0,
        MaximumFileCount: 10,
        MaximumByteCount: 10_000,
        LimitsVersion: "1",
        ExternalKind: null,
        AttemptCount: null,
        FailureCode: null,
        FailureReason: null,
        CanRetry: false,
        CanRevoke: true,
        CaseVersion: 1,
        CaseEditLeaseState: RequestCaseEditLeaseState.Available,
        CaseEditLeaseExpiresAtUtc: null);

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

    private sealed class RecordingRequestStore(RequestOperationsProjection result)
        : IRequestOperationsProjectionStore
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
            return Task.FromResult(result);
        }

        public Task<int> CountRetryableExternalFailuresAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(0);
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
