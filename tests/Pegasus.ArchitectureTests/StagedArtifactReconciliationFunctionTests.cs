using Microsoft.Extensions.Logging;
using Pegasus.Core.Intake;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class StagedArtifactReconciliationFunctionTests
{
    [Fact]
    public async Task TimerCallsTheBoundedReconcilerAndLogsEveryResultField()
    {
        var workStore = new ReconciliationWorkStore(recoveredLeases: 7);
        var reconciler = new ReconcileStagedArtifacts(
            workStore,
            new RejectingStagedArtifactAuthority(),
            new EmptyStagedArtifactStore(),
            TimeProvider.System);
        var logger = new RecordingLogger<StagedArtifactReconciliationFunction>();
        var function = new StagedArtifactReconciliationFunction(reconciler, logger);

        await function.RunAsync(null!, CancellationToken.None);

        Assert.Equal(50, workStore.MaximumItems);
        var state = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(logger.State);
        Assert.Equal(7, state["RecoveredLeases"]);
        Assert.Equal(0, state["Completed"]);
        Assert.Equal(0, state["Retained"]);
        Assert.Equal(0, state["Orphans"]);
        Assert.Equal(0, state["Unmatched"]);
        Assert.Equal(0, state["Failures"]);
    }

    [Fact]
    public void FunctionDependsOnTheCanonicalStagedArtifactReconciler()
    {
        var constructor = Assert.Single(
            typeof(StagedArtifactReconciliationFunction).GetConstructors());

        Assert.Equal(
            [
                typeof(ReconcileStagedArtifacts),
                typeof(ILogger<StagedArtifactReconciliationFunction>)
            ],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private sealed class ReconciliationWorkStore(int recoveredLeases) : IIntakeWorkStore
    {
        internal int MaximumItems { get; private set; }

        public Task<int> RecoverExpiredLeasesAsync(
            DateTimeOffset nowUtc,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            MaximumItems = maximumItems;
            return Task.FromResult(recoveredLeases);
        }

        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IntakeWorkItem?> FindWorkItemAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<ReceivedIntake> ReceiveAsync(
            IntakeStagedReceipt receipt,
            string operationKey,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(
            Guid stagedReceiptId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IntakeEvaluationRevision> CompleteProcessingAsync(
            Guid workItemId,
            string leaseToken,
            Guid processedReceiptId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task RetryProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            string failureCode,
            bool terminal,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task MarkPoisonedAsync(
            Guid stagedReceiptId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task ScheduleReevaluationAsync(
            Guid stagedReceiptId,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall() =>
            new("The timer's empty reconciliation batch reached an unrelated work-store operation.");
    }

    private sealed class RejectingStagedArtifactAuthority : IStagedArtifactAuthority
    {
        public Task<StagedArtifactAuthority?> FindAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "An empty staged-artifact inventory must not query durable authority.");
    }

    private sealed class EmptyStagedArtifactStore : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The reconciliation timer must not store a new artifact.");

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The reconciliation timer must not download an empty inventory.");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        internal IReadOnlyDictionary<string, object?>? State { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            State = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : throw new InvalidOperationException(
                    "The staged-artifact reconciliation log must retain structured fields.");
        }
    }
}
