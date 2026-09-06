using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The OCR operation state machine and its recovery rules. No provider is
/// reached: the fake here is structural, and nothing in it stands in for a real
/// reading of a real document.
/// </summary>
public sealed class IntakeOcrTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SourceBytes = [1, 2, 3, 4, 5, 6, 7, 8];
    private const string SourceHash = "aa11bb22cc33dd44ee55ff6600112233445566778899aabbccddeeff0011223344";

    [Fact]
    public async Task AnUnstartedOperationIsSubmittedCompletedAndReanalysedExactlyOnce()
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => Harness.Completed([2, 5]);

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.Completed, operation.State);
        Assert.Equal("provider-op-1", operation.ProviderOperationId);
        Assert.Equal("response-hash", operation.ResponseSha256);
        Assert.Equal([2, 5], operation.PageResults.Select(page => page.Number));
        Assert.Equal(1, harness.Provider.Analyses);
        Assert.Equal(0, harness.Provider.Reconciliations);

        // Re-analysis runs after the completion is stored, once, under a key
        // derived from the operation's own key.
        var request = Assert.Single(harness.Analysis.Requests);
        Assert.Equal("ocr:ocr-1", request.OperationKey);
        Assert.Equal(harness.Receipt.Id, request.ReceiptId);
        Assert.Equal(harness.Receipt.Version, request.ExpectedReceiptVersion);
        Assert.Equal(harness.SourceAssetId, request.IntakeAssetId);
        Assert.Equal(ActorKind.Automation, request.Actor.Kind);
    }

    [Fact]
    public async Task AReplayOfATerminalOperationHasNoSecondSideEffect()
    {
        foreach (var terminal in new[] { IntakeOcrState.Completed, IntakeOcrState.Failed })
        {
            var harness = new Harness(terminal);

            await harness.ExecuteAsync();

            Assert.Equal(0, harness.Provider.Analyses);
            Assert.Equal(0, harness.Provider.Reconciliations);
            Assert.Empty(harness.Analysis.Requests);
            Assert.Equal(terminal, harness.Store.Single().State);
        }
    }

    [Fact]
    public async Task ATimeoutSchedulesASafeRetryAndDoesNotResubmitWithinTheAttempt()
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => Harness.Failure(
            IntakeOcrState.Failed,
            new("ocr_provider_unavailable", "timed out", Retryable: true));

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.RetryScheduled, operation.State);
        Assert.Equal(Now.AddSeconds(30), operation.RetryAtUtc);
        Assert.Equal(1, operation.AttemptCount);
        Assert.Equal(1, harness.Provider.Analyses);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AThrottleHonoursTheProvidersOwnRetryAfterWhenItIsLonger()
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => Harness.Failure(
            IntakeOcrState.Failed,
            new(
                "ocr_provider_unavailable",
                "throttled",
                Retryable: true,
                RetryAfter: TimeSpan.FromMinutes(9)));

        await harness.ExecuteAsync();

        Assert.Equal(Now.AddMinutes(9), harness.Store.Single().RetryAtUtc);
    }

    [Fact]
    public async Task AnExhaustedRetrySchedulePutsTheOperationBeyondRetryRatherThanLooping()
    {
        var harness = new Harness(attemptCount: IntakeOcrPolicy.MaximumAttempts);
        harness.Provider.OnAnalyze = () => Harness.Failure(
            IntakeOcrState.Failed,
            new("ocr_provider_unavailable", "still down", Retryable: true));

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.Failed, operation.State);
        Assert.Null(operation.RetryAtUtc);
    }

    [Fact]
    public async Task AnUncertainSideEffectBecomesUnknownAndIsThenReconciledRatherThanResent()
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => new IntakeOcrResult(
            IntakeOcrState.Unknown,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            "provider-op-1",
            Failure: new("ocr_operation_pending", "the attempt ended first", Retryable: true));

        await harness.ExecuteAsync();

        // The provider's identity for the operation is recorded even though the
        // outcome is not known, which is what makes the next attempt a lookup.
        var afterFirst = harness.Store.Single();
        Assert.Equal(IntakeOcrState.RetryScheduled, afterFirst.State);
        Assert.Equal("provider-op-1", afterFirst.ProviderOperationId);

        harness.Provider.OnReconcile = () => Harness.Completed([2, 5]);
        await harness.ExecuteAsync();

        Assert.Equal(IntakeOcrState.Completed, harness.Store.Single().State);
        // One submission, ever. The second attempt asked about the operation.
        Assert.Equal(1, harness.Provider.Analyses);
        Assert.Equal(1, harness.Provider.Reconciliations);
        Assert.Equal("provider-op-1", harness.Provider.ReconciledId);
        Assert.Single(harness.Analysis.Requests);
    }

    [Fact]
    public async Task ASendThatDidNotCompleteAndNamedNoOperationIsNotScheduledForAnotherAttempt()
    {
        // The uncertain case: the request went out, something broke, and the
        // provider named nothing. It may or may not have read the pages, so it
        // waits for a person rather than being tried again on a timer.
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => throw new HttpRequestException("connection reset");

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.Unknown, operation.State);
        Assert.Null(operation.RetryAtUtc);
        Assert.Null(operation.ProviderOperationId);
        Assert.Contains("ocr_dependency_failure", operation.LastError, StringComparison.Ordinal);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task ASourceThatCouldNotBeOpenedIsSafeToTryAgainBecauseNothingWasSent()
    {
        var harness = new Harness();
        harness.Documents.FailToOpen = true;

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.RetryScheduled, operation.State);
        Assert.Equal(Now.AddSeconds(30), operation.RetryAtUtc);
        Assert.Equal(0, harness.Provider.Analyses);
    }

    [Fact]
    public async Task AnOperationRecordedAsSentWithoutAProviderIdentityStaysUnknownAndIsNeverRepeated()
    {
        var harness = new Harness(IntakeOcrState.Processing);

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.Unknown, operation.State);
        Assert.Contains("ocr_operation_unidentified", operation.LastError, StringComparison.Ordinal);
        Assert.Equal(0, harness.Provider.Analyses);
        Assert.Equal(0, harness.Provider.Reconciliations);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AReconciliationThatCannotReachTheProviderLeavesTheOperationLookupableNotResent()
    {
        var harness = new Harness(IntakeOcrState.Unknown, providerOperationId: "provider-op-1");
        harness.Provider.OnReconcile = () => throw new HttpRequestException("no route");

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.RetryScheduled, operation.State);
        Assert.Equal("provider-op-1", operation.ProviderOperationId);
        Assert.Equal(0, harness.Provider.Analyses);
    }

    [Theory]
    [InlineData(new[] { 2 }, "ocr_pages_missing")]
    [InlineData(new[] { 2, 5, 9 }, "ocr_pages_unexpected")]
    public async Task AResponseThatDoesNotCoverExactlyTheSubmittedPagesFailsClosed(
        int[] returnedPages,
        string expectedCode)
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => Harness.Completed(returnedPages);

        await harness.ExecuteAsync();

        var operation = harness.Store.Single();
        Assert.Equal(IntakeOcrState.Failed, operation.State);
        Assert.Contains(expectedCode, operation.LastError, StringComparison.Ordinal);
        // Nothing partially accepted: no page output, and no re-analysis, so no
        // candidate and no Case can come of it.
        Assert.Empty(operation.PageResults);
        Assert.Null(operation.ResponseSha256);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AResponseFromAnUnpinnedApiVersionIsRefused()
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => Harness.Completed([2, 5]) with { ApiVersion = "2023-07-31" };

        await harness.ExecuteAsync();

        Assert.Equal(IntakeOcrState.Failed, harness.Store.Single().State);
        Assert.Contains(
            "ocr_api_version_unexpected",
            harness.Store.Single().LastError,
            StringComparison.Ordinal);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AResponseWithNoContentHashIsRefusedBecauseItCouldNeverBeEvidenced()
    {
        var harness = new Harness();
        harness.Provider.OnAnalyze = () => Harness.Completed([2, 5]) with { ResponseSha256 = null };

        await harness.ExecuteAsync();

        Assert.Equal(IntakeOcrState.Failed, harness.Store.Single().State);
        Assert.Contains(
            "ocr_response_unattributable",
            harness.Store.Single().LastError,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASourceThatNoLongerMatchesTheRecordedHashIsRefusedWithoutBeingOpened()
    {
        var harness = new Harness(sourceSha256: "00000000000000000000000000000000000000000000000000000000000000ff");

        await harness.ExecuteAsync();

        Assert.Equal(IntakeOcrState.Failed, harness.Store.Single().State);
        Assert.Contains(
            "ocr_source_unavailable",
            harness.Store.Single().LastError,
            StringComparison.Ordinal);
        Assert.Equal(0, harness.Documents.Opens);
        Assert.Equal(0, harness.Provider.Analyses);
    }

    [Fact]
    public void ARequestMustNameExactlyOneSourceAndAtLeastOnePageEachOnce()
    {
        Assert.Throws<ArgumentException>(() => IntakeOcrRequest.Validate(
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SourceHash, 8, [1], "k")));
        Assert.Throws<ArgumentException>(() => IntakeOcrRequest.Validate(
            new(Guid.NewGuid(), null, null, SourceHash, 8, [1], "k")));
        Assert.Throws<ArgumentException>(() => IntakeOcrRequest.Validate(
            new(Guid.NewGuid(), null, Guid.NewGuid(), SourceHash, 8, [], "k")));
        Assert.Throws<ArgumentException>(() => IntakeOcrRequest.Validate(
            new(Guid.NewGuid(), null, Guid.NewGuid(), SourceHash, 8, [1, 1], "k")));
        Assert.Throws<ArgumentException>(() => IntakeOcrRequest.Validate(
            new(Guid.NewGuid(), null, Guid.NewGuid(), SourceHash, 8, [0], "k")));
    }

    [Fact]
    public void TheRetryScheduleIsBoundedAndRefusesToRepeatAnUnsafeFailure()
    {
        IntakeOcrFailure retryable = new("ocr_provider_unavailable", "down", Retryable: true);
        IntakeOcrFailure terminal = new("ocr_response_malformed", "nonsense", Retryable: false);

        Assert.Null(IntakeOcrPolicy.NextAttemptDelay(1, terminal));
        Assert.Null(IntakeOcrPolicy.NextAttemptDelay(1, null));
        Assert.Null(IntakeOcrPolicy.NextAttemptDelay(0, retryable));
        Assert.Null(IntakeOcrPolicy.NextAttemptDelay(IntakeOcrPolicy.MaximumAttempts, retryable));
        Assert.Equal(TimeSpan.FromSeconds(30), IntakeOcrPolicy.NextAttemptDelay(1, retryable));
        Assert.Equal(
            TimeSpan.FromHours(2),
            IntakeOcrPolicy.NextAttemptDelay(IntakeOcrPolicy.MaximumAttempts - 1, retryable));
    }

    [Fact]
    public void ADuplicatedPageInAResponseIsInconsistentRatherThanDeduplicated()
    {
        IntakeOcrRequest request = new(Guid.NewGuid(), null, Guid.NewGuid(), SourceHash, 8, [2], "k");
        var result = new IntakeOcrResult(
            IntakeOcrState.Completed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            "op",
            "hash",
            [new(2, "a", [], []), new(2, "b", [], [])]);

        var failure = IntakeOcrPolicy.Validate(request, result);

        Assert.Equal("ocr_pages_inconsistent", failure?.Code);
        Assert.False(failure?.Retryable);
    }

    private sealed class Harness
    {
        public Harness(
            IntakeOcrState state = IntakeOcrState.Pending,
            string? providerOperationId = null,
            int attemptCount = 0,
            string? sourceSha256 = null)
        {
            SourceAssetId = Guid.NewGuid();
            Receipt = BuildReceipt(SourceAssetId);
            WorkItemId = Guid.NewGuid();
            Store.Seed(new(
                WorkItemId,
                Receipt.Id,
                null,
                SourceAssetId,
                sourceSha256 ?? SourceHash,
                [2, 5],
                "ocr-1",
                state,
                1,
                providerOperationId,
                AttemptCount: attemptCount));
            Command = new ProcessIntakeOcr(
                Store,
                Provider,
                Documents,
                Analysis,
                new FakeReceipts(Receipt),
                new FixedTime(Now));
        }

        public Guid WorkItemId { get; }

        public Guid SourceAssetId { get; }

        public IntakeReceipt Receipt { get; }

        public FakeStore Store { get; } = new();

        public FakeProvider Provider { get; } = new();

        public FakeDocuments Documents { get; } = new();

        public FakeAnalysis Analysis { get; } = new();

        public ProcessIntakeOcr Command { get; }

        public Task ExecuteAsync() => Command.ExecuteAsync(WorkItemId, CancellationToken.None);

        public static IntakeOcrResult Completed(IReadOnlyList<int> pages) => new(
            IntakeOcrState.Completed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            "provider-op-1",
            "response-hash",
            [.. pages.Select(page => new IntakeOcrPage(page, $"page {page}", [], []))]);

        public static IntakeOcrResult Failure(IntakeOcrState state, IntakeOcrFailure failure) => new(
            state,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            Failure: failure);
    }

    private static IntakeReceipt BuildReceipt(Guid sourceAssetId) =>
        new(
            Guid.NewGuid(),
            "instruction.pdf",
            "application/pdf",
            SourceBytes.Length,
            SourceHash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
            Now,
            Now,
            IntakeDecision.OcrRequired,
            "Recorded by the pipeline.",
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
                    sourceAssetId,
                    "uploaded instruction.pdf",
                    "instruction.pdf",
                    "application/pdf",
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    SourceBytes.Length,
                    SourceHash,
                    "storage/0",
                    null,
                    null,
                    null,
                    null)
            ],
            Version: 7);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// An in-memory operation store that behaves like the durable one where it
    /// matters: every write is optimistic on the recorded version, so a stale
    /// caller loses instead of overwriting.
    /// </summary>
    private sealed class FakeStore : IIntakeOcrOperationStore
    {
        private readonly Dictionary<Guid, IntakeOcrOperation> operations = [];

        public void Seed(IntakeOcrOperation operation) => this.operations[operation.Id] = operation;

        public IntakeOcrOperation Single() => this.operations.Values.Single();

        public Task<IntakeOcrOperation?> FindAsync(Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.operations.TryGetValue(operationId, out var operation) ? operation : null);

        public Task<IntakeOcrOperation?> FindByOperationKeyAsync(
            string operationKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.operations.Values
                .FirstOrDefault(operation => operation.OperationKey == operationKey));

        public Task<IntakeOcrOperation> BeginAsync(
            Guid operationId,
            IntakeOcrRequest request,
            CancellationToken cancellationToken)
        {
            IntakeOcrRequest.Validate(request);
            if (this.operations.TryGetValue(operationId, out var existing))
            {
                return Task.FromResult(existing);
            }

            return Task.FromResult(Apply(
                new(
                    operationId,
                    request.IntakeReceiptId,
                    request.DocumentVersionId,
                    request.IntakeAssetId,
                    request.SourceSha256,
                    [.. request.QualifiedPages.Order()],
                    request.OperationKey,
                    IntakeOcrState.Pending,
                    1),
                operation => operation));
        }

        public Task<IntakeOcrOperation> RecordSubmittedAsync(
            Guid operationId,
            long expectedVersion,
            string providerOperationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Update(
                operationId,
                expectedVersion,
                operation => operation with
                {
                    ProviderOperationId = providerOperationId,
                    State = IntakeOcrState.Processing,
                    RetryAtUtc = null
                }));

        public Task<IntakeOcrOperation> CompleteAsync(
            Guid operationId,
            long expectedVersion,
            IntakeOcrResult result,
            CancellationToken cancellationToken) =>
            Task.FromResult(Update(
                operationId,
                expectedVersion,
                operation => operation with
                {
                    State = IntakeOcrState.Completed,
                    ProviderOperationId = result.ProviderOperationId ?? operation.ProviderOperationId,
                    ResponseSha256 = result.ResponseSha256,
                    Pages = result.PageResults,
                    LastError = null,
                    RetryAtUtc = null
                }));

        public Task<IntakeOcrOperation> RecordOutcomeAsync(
            Guid operationId,
            long expectedVersion,
            IntakeOcrState state,
            IntakeOcrFailure failure,
            DateTimeOffset? retryAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(Update(
                operationId,
                expectedVersion,
                operation => operation with
                {
                    State = state,
                    LastError = $"{failure.Code}: {failure.Reason}",
                    RetryAtUtc = retryAtUtc,
                    AttemptCount = operation.AttemptCount + 1
                }));

        private IntakeOcrOperation Update(
            Guid operationId,
            long expectedVersion,
            Func<IntakeOcrOperation, IntakeOcrOperation> apply)
        {
            if (!this.operations.TryGetValue(operationId, out var operation)
                || operation.Version != expectedVersion)
            {
                throw new IntakeOcrOperationConflictException();
            }

            return Apply(apply(operation), value => value with { Version = operation.Version + 1 });
        }

        private IntakeOcrOperation Apply(
            IntakeOcrOperation operation,
            Func<IntakeOcrOperation, IntakeOcrOperation> version)
        {
            var stored = version(operation);
            this.operations[stored.Id] = stored;
            return stored;
        }
    }

    private sealed class FakeProvider : IIntakeOcrProvider
    {
        public Func<IntakeOcrResult>? OnAnalyze { get; set; }

        public Func<IntakeOcrResult>? OnReconcile { get; set; }

        public int Analyses { get; private set; }

        public int Reconciliations { get; private set; }

        public string? ReconciledId { get; private set; }

        public Task<IntakeOcrResult> AnalyzeAsync(
            IntakeOcrRequest request,
            Stream content,
            CancellationToken cancellationToken)
        {
            IntakeOcrRequest.Validate(request);
            Analyses++;
            return Task.FromResult(OnAnalyze?.Invoke()
                ?? throw new InvalidOperationException("No submission was expected."));
        }

        public Task<IntakeOcrResult> ReconcileAsync(
            IntakeOcrRequest request,
            string providerOperationId,
            CancellationToken cancellationToken)
        {
            Reconciliations++;
            ReconciledId = providerOperationId;
            return Task.FromResult(OnReconcile?.Invoke()
                ?? throw new InvalidOperationException("No reconciliation was expected."));
        }
    }

    private sealed class FakeDocuments : IReadLogicalDocumentVersion
    {
        public int Opens { get; private set; }

        public bool FailToOpen { get; set; }

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Opens++;
            if (FailToOpen)
            {
                throw new IOException("The retained asset is not available.");
            }

            if (!string.Equals(request.ExpectedSha256, SourceHash, StringComparison.OrdinalIgnoreCase)
                || request.ExpectedContentLength != SourceBytes.Length)
            {
                throw new InvalidOperationException("The expected identity does not match.");
            }

            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(SourceBytes, writable: false),
                null,
                null,
                request.IntakeAssetId,
                SourceHash,
                SourceBytes.Length,
                "instruction.pdf",
                "application/pdf"));
        }
    }

    private sealed class FakeReceipts(IntakeReceipt receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == receipt.Id ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult(receiptId == receipt.Id
                ? receipt.AssetRecords.SingleOrDefault(asset => asset.Id == assetId)
                : null);

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));
    }

    private sealed class FakeAnalysis : IAnalyzeRetainedInstruction
    {
        public List<AnalyzeRetainedInstructionRequest> Requests { get; } = [];

        public Task<AnalyzeRetainedInstructionResult> ExecuteAsync(
            AnalyzeRetainedInstructionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new AnalyzeRetainedInstructionResult(
                RetainedInstructionAnalysisOutcome.NoProfile,
                null,
                "no profile",
                [],
                false));
        }
    }
}
