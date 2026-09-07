using Pegasus.Core.Eva;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.ProviderApi;
using Pegasus.Infrastructure.Custody;
using Pegasus.Worker;

namespace Pegasus.IntegrationTests;

public sealed class StagedArtifactReconciliationFunctionIntegrationTests
{
    [Fact]
    public async Task TimerCallsTheBoundedReconcilerAndLogsEveryResultField()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var pendingCustodyFactory = new CountingContextFactory(contextFactory);
        var workStore = new ReconciliationWorkStore(recoveredLeases: 7);
        var reconciler = new ReconcileStagedArtifacts(
            workStore,
            new RejectingStagedArtifactAuthority(),
            new EmptyStagedArtifactStore(),
            TimeProvider.System);
        var groupedImageReconciler = new ReconcileGroupedImageIntake(
            new EmptyIntakeReceiptQueries(),
            new UnreachableGroupStore(),
            workStore,
            new UnreachableProcessQueuedIntake(),
            TimeProvider.System,
            new UnreachableRegisterUnidentified());
        var unidentifiedReconciler = new ReconcileUnidentifiedDestinations(
            new EmptyUnidentifiedStore(),
            new UnreachableResolveUnidentified(),
            new EmptyIntakeReceiptQueries(),
            new UnreachableImageIntakeQueries(),
            new UnreachableTriageQueries(),
            TimeProvider.System);
        var vehicleLookupReconciler = new ReconcileAutomaticVehicleLookups(
            new UnreachableAutomaticVehicleLookupStore(),
            VehicleLookupAvailability.Unavailable);
        var providerSubmissionReconciler = new ReconcileProviderSubmissions(
            new EmptyProviderSubmissionStore(),
            new UnreachableActionHistoryWriter(),
            TimeProvider.System);
        var logger = new RecordingLogger<StagedArtifactReconciliationFunction>();
        var function = new StagedArtifactReconciliationFunction(
            reconciler,
            new EmptyCacheCleanup(),
            new ReconcilePendingArtifactCustody(
                pendingCustodyFactory,
                new EmptyDocumentContentStore(),
                new EmptyStagedArtifactStore()),
            groupedImageReconciler,
            unidentifiedReconciler,
            vehicleLookupReconciler,
            providerSubmissionReconciler,
            logger);

        await function.RunAsync(null!, CancellationToken.None);

        Assert.Equal(50, workStore.MaximumItems);
        Assert.True(pendingCustodyFactory.CreateCount > 0);
        Assert.Equal(5, logger.States.Count);
        var state = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(logger.States[0]);
        Assert.Equal(7, state["RecoveredWorkItems"]);
        Assert.Equal(0, state["Completed"]);
        Assert.Equal(0, state["Retained"]);
        Assert.Equal(0, state["Orphans"]);
        Assert.Equal(0, state["Unmatched"]);
        Assert.Equal(0, state["Failures"]);

        var groupedImageState = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(logger.States[1]);
        Assert.Equal(0, groupedImageState["Candidates"]);
        Assert.Equal(0, groupedImageState["Retried"]);
        Assert.Equal(0, groupedImageState["Escaped"]);
        Assert.Equal(0, groupedImageState["Failures"]);

        var unidentifiedState = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(logger.States[2]);
        Assert.Equal(0, unidentifiedState["Candidates"]);
        Assert.Equal(0, unidentifiedState["Resolved"]);
        Assert.Equal(0, unidentifiedState["Failures"]);

        var vehicleLookupState = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(logger.States[3]);
        Assert.Equal(0, vehicleLookupState["Enqueued"]);

        var providerSubmissionState = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(logger.States[4]);
        Assert.Equal(0, providerSubmissionState["Candidates"]);
        Assert.Equal(0, providerSubmissionState["Repaired"]);
        Assert.Equal(0, providerSubmissionState["Failures"]);
        Assert.Null(providerSubmissionState["FirstFailure"]);
    }


    private sealed class EmptyDocumentContentStore : IDocumentContentStore
    {
        public Task StoreAsync(Guid caseId, string caseReference, Guid versionId,
            ReadOnlyMemory<byte> content, string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An empty pending-custody batch must not store content.");

        public Task<Stream> OpenReadAsync(Guid caseId, string caseReference, Guid versionId,
            string expectedSha256, long expectedLength, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An empty pending-custody batch must not read content.");

        public Task DeleteAsync(Guid caseId, string caseReference, Guid versionId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An empty pending-custody batch must not delete content.");
    }

    private sealed class CountingContextFactory(
        IDbContextFactory<PegasusDbContext> inner) : IDbContextFactory<PegasusDbContext>
    {
        public int CreateCount { get; private set; }

        public PegasusDbContext CreateDbContext()
        {
            CreateCount++;
            return inner.CreateDbContext();
        }

        public Task<PegasusDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return inner.CreateDbContextAsync(cancellationToken);
        }
    }
    private sealed class EmptyCacheCleanup : IDocumentContentCacheCleanup
    {
        public Task<DocumentContentCacheCleanupResult> ExecuteAsync(
            int maximumItems,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentContentCacheCleanupResult(0, 0, 0, 0));
    }

    private sealed class UnreachableAutomaticVehicleLookupStore : IAutomaticVehicleLookupStore
    {
        public Task<int> EnqueueDueAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The sweep store must not be reached while lookups are unavailable.");
    }

    private sealed class EmptyProviderSubmissionStore : IProviderSubmissionStore
    {
        public Task CreateAsync(
            ProviderSubmissionRecord record,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<ProviderSubmissionRecord?> FindByIdempotencyKeyAsync(
            Guid principalId,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<ProviderSubmissionRecord?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<string?> FindPrincipalCodeAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task RecordStagedReceiptAsync(
            Guid submissionId,
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<ProviderSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
            int maximumItems,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderSubmissionAcceptCandidate>>([]);

        private static InvalidOperationException UnexpectedCall() =>
            new("An empty provider-submission reconciliation batch must not reach a write or unrelated read.");
    }

    private sealed class UnreachableActionHistoryWriter : IActionHistoryWriter
    {
        public Task AppendAsync(
            ActionHistoryEntry entry,
            CancellationToken cancellationToken) =>
            throw Unreachable();

        public Task<bool> TryAppendAsync(
            ActionHistoryEntry entry,
            CancellationToken cancellationToken) =>
            throw Unreachable();

        private static InvalidOperationException Unreachable() =>
            new("An empty provider-submission reconciliation batch must not append history.");
    }

    private sealed class ReconciliationWorkStore(int recoveredLeases) : IIntakeWorkStore
    {
        internal int MaximumItems { get; private set; }

        public Task<int> RecoverInterruptedWorkAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset staleDispatchedBeforeUtc,
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

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            Guid stagedReceiptId,
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

        public Task<Guid?> FindStagedReceiptIdForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall() =>
            new("The timer's empty reconciliation batch reached an unrelated work-store operation.");
    }

    private sealed class EmptyIntakeReceiptQueries : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The timer's grouped-image reconciliation must not query queue counts.");

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, TotalCount: 0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "An empty grouped-image reconciliation page must not fetch a receipt.");

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The timer's grouped-image reconciliation must not query an asset.");
    }

    private sealed class UnreachableGroupStore : IIntakeSubmissionGroupStore
    {
        public Task<IntakeSubmissionGroup?> GetAsync(
            Guid groupId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IntakeSubmissionGroup?> FindAsync(
            IntakeSourceChannel channel,
            string submissionToken,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(
            Guid groupId,
            IntakeSourceChannel channel,
            string submissionToken,
            int expectedMemberCount,
            string actor,
            DateTimeOffset receivedAtUtc,
            Guid? parentReceiptId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IntakeSubmissionGroupMember?> FindMemberAsync(
            Guid groupId,
            int ordinal,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(
            Guid groupId,
            int ordinal,
            ReceivedIntake received,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
            Guid groupId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall() =>
            new("An empty grouped-image reconciliation page must not reach the group store.");
    }

    private sealed class UnreachableProcessQueuedIntake : IProcessQueuedIntake
    {
        public Task<QueuedIntakeProcessingOutcome> ExecuteAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "An empty grouped-image reconciliation page must not re-drive any staged receipt.");
    }

    private sealed class UnreachableRegisterUnidentified : IRegisterUnidentified
    {
        public Task<UnidentifiedRegisterResult> ExecuteAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "An empty grouped-image reconciliation page must not register anything Unidentified.");
    }

    private sealed class EmptyUnidentifiedStore : IUnidentifiedStore
    {
        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedItem>>([]);

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall() =>
            new("An empty Unidentified reconciliation page must only list open items.");
    }

    private sealed class UnreachableResolveUnidentified : IResolveUnidentified
    {
        public Task<UnidentifiedResolveResult> ExecuteAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "An empty Unidentified reconciliation page must not resolve anything.");
    }

    private sealed class UnreachableTriageQueries : Pegasus.Core.Triage.ITriageQueries
    {
        public Task<IReadOnlyList<Pegasus.Core.Triage.TriageSummary>> ListAsync(
            Pegasus.Core.Triage.TriageState? state,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<Pegasus.Core.Triage.TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<Pegasus.Core.Triage.TriageSummary?> GetByOriginReceiptAsync(
            Guid originReceiptId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall() =>
            new("The timer's empty reconciliation batch reached an unrelated Triage query.");
    }

    private sealed class UnreachableImageIntakeQueries : IImageIntakeQueries
    {
        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall() =>
            new("An empty Unidentified reconciliation page must not reach image-intake queries.");
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
        internal List<IReadOnlyDictionary<string, object?>> States { get; } = [];

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
            States.Add(state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : throw new InvalidOperationException(
                    "The staged-artifact reconciliation log must retain structured fields."));
        }
    }
}
