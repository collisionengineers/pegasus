using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Page-restricted OCR against the real durable store, the real migration and a
/// real retained intake asset.
///
/// The provider is a structural fake — no genuine OCR output exists on this
/// machine and none is invented as evidence — but everything that decides
/// whether an outage costs the business a duplicate reading is real here: the
/// operation row, its version, its unique operation key, and the order the
/// writes happen in.
///
/// The composition is assembled in the test. The production registrations belong
/// to Stream A (C-F03) in <c>DependencyInjection.cs</c> and
/// <c>IntakeFunctions.cs</c>, and the exact hunks are stated in the C02 report
/// rather than smuggled in behind an optional dependency.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class OcrIntakeRecoveryTests
{
    private const string ScanLikeBody =
        "Please see the attached instruction. The pages are scans.\r\n";

    [Fact]
    public async Task ASubmittedOperationCompletesOnceAndReanalysesOnce()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Provider.OnAnalyze = () => Harness.Completed([1]);

        await harness.ExecuteAsync();

        var operation = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Completed, operation.State);
        Assert.Equal("provider-op-1", operation.ProviderOperationId);
        Assert.Equal("response-hash-1", operation.ResponseSha256);
        Assert.Equal([1], operation.PageResults.Select(page => page.Number));
        Assert.Equal("SYNTHETIC PAGE", Assert.Single(operation.PageResults).Text);
        Assert.NotNull(operation.Result);
        Assert.Equal(IntakeOcrState.Completed, operation.Result.State);
        Assert.Equal(IntakeOcrProviderIdentity.Provider, operation.Result.Provider);
        Assert.Equal(IntakeOcrProviderIdentity.ModelId, operation.Result.ModelId);
        Assert.Equal(IntakeOcrProviderIdentity.ApiVersion, operation.Result.ApiVersion);
        Assert.Equal("provider-op-1", operation.Result.ProviderOperationId);
        Assert.Equal("response-hash-1", operation.Result.ResponseSha256);
        Assert.Equal([1], operation.Result.PageResults.Select(page => page.Number));
        Assert.Single(harness.Analysis.Requests);

        var workItem = await harness.ReadWorkItemAsync();
        Assert.NotNull(workItem);
        Assert.Equal(ExternalWorkStatePersistence.Completed, workItem.State);
        Assert.Equal(ExternalWorkKinds.IntakeOcr, workItem.Kind);
        Assert.Equal(harness.Request.OperationKey, workItem.OperationKey);
        Assert.NotNull(workItem.CompletedAtUtc);
        Assert.Equal("provider-op-1", workItem.ExternalReceipt);
    }

    [Fact]
    public async Task ARedeliveredMessageFindsOneDurableOperationAndCausesNoSecondSideEffect()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Provider.OnAnalyze = () => Harness.Completed([1]);

        await harness.ExecuteAsync();
        await harness.ExecuteAsync();
        await harness.ExecuteAsync();

        Assert.Equal(1, harness.Provider.Analyses);
        Assert.Equal(0, harness.Provider.Reconciliations);
        Assert.Single(harness.Analysis.Requests);
        Assert.Equal(1, await harness.CountOperationsAsync());
    }

    [Fact]
    public async Task ABeginRepeatedUnderTheSameKeyReturnsTheRecordedOperationRatherThanASecondOne()
    {
        await using var harness = await Harness.CreateAsync();

        var again = await harness.Store.BeginAsync(
            harness.WorkItemId,
            harness.Request,
            CancellationToken.None);

        Assert.Equal(harness.WorkItemId, again.Id);
        Assert.Equal(1, await harness.CountOperationsAsync());

        var workItem = await harness.ReadWorkItemAsync();
        Assert.NotNull(workItem);
        Assert.Equal(ExternalWorkStatePersistence.Pending, workItem.State);
        Assert.Equal(ExternalWorkKinds.IntakeOcr, workItem.Kind);
        Assert.Equal(harness.Request.OperationKey, workItem.OperationKey);
    }

    [Fact]
    public async Task TheSameKeyForADifferentSourceIsRefusedRatherThanOverwritten()
    {
        await using var harness = await Harness.CreateAsync();

        await Assert.ThrowsAsync<IntakeOcrOperationConflictException>(() =>
            harness.Store.BeginAsync(
                Guid.NewGuid(),
                harness.Request with { QualifiedPages = [1, 2] },
                CancellationToken.None));
        Assert.Equal(1, await harness.CountOperationsAsync());
    }

    [Fact]
    public async Task AHostThatDiedAfterSubmittingLeavesAnOperationThatIsLookedUpAndNotResent()
    {
        await using var harness = await Harness.CreateAsync();

        // The first attempt reaches the provider, which names the operation, and
        // then the wait ends without an answer — a restart, or a bounded attempt
        // running out.
        harness.Provider.OnAnalyze = () => new IntakeOcrResult(
            IntakeOcrState.Unknown,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            "provider-op-1",
            Failure: new("ocr_operation_pending", "the attempt ended first", Retryable: true));
        await harness.ExecuteAsync();

        var afterRestart = await harness.ReadAsync();
        Assert.Equal("provider-op-1", afterRestart.ProviderOperationId);

        harness.Provider.OnReconcile = () => Harness.Completed([1]);
        await harness.ExecuteAsync();

        Assert.Equal(IntakeOcrState.Completed, (await harness.ReadAsync()).State);
        Assert.Equal(1, harness.Provider.Analyses);
        Assert.Equal(1, harness.Provider.Reconciliations);
        Assert.Single(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AnAmbiguousOperationLookupLeavesTheOperationRecordedAndUnrepeated()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Provider.OnAnalyze = () => throw new HttpRequestException("connection reset");

        await harness.ExecuteAsync();

        // Nothing named the operation and nothing knows whether it ran. It waits
        // for a person rather than being tried again on a timer.
        var operation = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Unknown, operation.State);
        Assert.Null(operation.RetryAtUtc);
        Assert.Null(operation.ProviderOperationId);

        await harness.ExecuteAsync();

        var afterSecondDelivery = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Unknown, afterSecondDelivery.State);
        Assert.Equal(1, harness.Provider.Analyses);
        Assert.Empty(harness.Analysis.Requests);

        var workItem = await harness.ReadWorkItemAsync();
        Assert.NotNull(workItem);
        Assert.Equal(ExternalWorkStatePersistence.Failed, workItem.State);
        Assert.Equal(harness.Request.OperationKey, workItem.OperationKey);
    }

    [Fact]
    public async Task AThrottledAttemptIsScheduledForARetryThatCountsTowardsTheCap()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Provider.OnAnalyze = () => new IntakeOcrResult(
            IntakeOcrState.Failed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            Failure: new("ocr_provider_unavailable", "throttled", Retryable: true));

        await harness.ExecuteAsync();
        var first = await harness.ReadAsync();
        var workAfterFirst = await harness.ReadWorkItemAsync();
        Assert.NotNull(workAfterFirst);
        Assert.Equal(ExternalWorkStatePersistence.Pending, workAfterFirst.State);
        Assert.Equal(first.RetryAtUtc, workAfterFirst.DueAtUtc);
        Assert.Equal(1, workAfterFirst.AttemptCount);

        await harness.ExecuteAsync();
        var second = await harness.ReadAsync();
        var workAfterSecond = await harness.ReadWorkItemAsync();

        Assert.Equal(IntakeOcrState.RetryScheduled, first.State);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal(IntakeOcrState.RetryScheduled, second.State);
        Assert.Equal(2, second.AttemptCount);
        Assert.NotNull(second.RetryAtUtc);
        Assert.NotNull(workAfterSecond);
        Assert.Equal(ExternalWorkStatePersistence.Pending, workAfterSecond.State);
        Assert.Equal(second.RetryAtUtc, workAfterSecond.DueAtUtc);
        Assert.Equal(2, workAfterSecond.AttemptCount);
        // A safe retry did resend, because the provider refused the submission
        // outright: nothing was read, so nothing can be read twice.
        Assert.Equal(2, harness.Provider.Analyses);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AResponseThatFailsClosedStoresNoPageOutputAndCreatesNoCandidate()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Provider.OnAnalyze = () => Harness.Completed([1, 4]);

        await harness.ExecuteAsync();

        var operation = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Failed, operation.State);
        Assert.Contains("ocr_pages_unexpected", operation.LastError, StringComparison.Ordinal);
        Assert.Null(operation.ResponseSha256);
        Assert.Empty(operation.PageResults);
        Assert.Empty(harness.Analysis.Requests);
    }

    [Fact]
    public async Task AStaleWriterLosesRatherThanOverwritingTheRecordedOutcome()
    {
        await using var harness = await Harness.CreateAsync();
        var stale = await harness.ReadAsync();
        harness.Provider.OnAnalyze = () => Harness.Completed([1]);

        await harness.ExecuteAsync();

        await Assert.ThrowsAsync<IntakeOcrOperationConflictException>(() =>
            harness.Store.RecordOutcomeAsync(
                stale.Id,
                stale.Version,
                IntakeOcrState.Failed,
                new("ocr_response_malformed", "a second worker's answer", Retryable: false),
                retryAtUtc: null,
                CancellationToken.None));
        Assert.Equal(IntakeOcrState.Completed, (await harness.ReadAsync()).State);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly IntakeWebApplicationFactory factory;
        private readonly IServiceScope scope;

        private Harness(
            IntakeWebApplicationFactory factory,
            IServiceScope scope,
            Guid receiptId,
            Guid assetId,
            string sourceSha256,
            long sourceContentLength)
        {
            this.factory = factory;
            this.scope = scope;
            var services = scope.ServiceProvider;
            ContextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            Store = new EfIntakeOcrOperationStore(ContextFactory);
            WorkItemId = Guid.NewGuid();
            Request = new(
                receiptId,
                null,
                assetId,
                sourceSha256,
                sourceContentLength,
                [1],
                $"ocr:{receiptId:N}:1");
            Command = new ProcessIntakeOcr(
                Store,
                Provider,
                new RetainedAssetReader(
                    services.GetRequiredService<IIntakeReceiptQueries>(),
                    services.GetRequiredService<IIntakeArtifactStore>()),
                Analysis,
                services.GetRequiredService<IIntakeReceiptQueries>(),
                TimeProvider.System);
        }

        public Guid WorkItemId { get; }

        public IntakeOcrRequest Request { get; }

        public IDbContextFactory<PegasusDbContext> ContextFactory { get; }

        public EfIntakeOcrOperationStore Store { get; }

        public FakeProvider Provider { get; } = new();

        public RecordingAnalysis Analysis { get; } = new();

        public ProcessIntakeOcr Command { get; }

        public static async Task<Harness> CreateAsync()
        {
            var factory = new IntakeWebApplicationFactory();
            var receiptId = await RetainAsync(factory);
            var scope = factory.Services.CreateScope();
            var receipt = await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None)
                ?? throw new InvalidOperationException("The fixture receipt was not retained.");
            var asset = IntakeFileIdentity.SourceAsset(receipt)
                ?? throw new InvalidOperationException("The fixture receipt retained no source asset.");
            var harness = new Harness(
                factory,
                scope,
                receipt.Id,
                asset.Id,
                asset.ContentHash,
                asset.ContentLength);
            await harness.Store.BeginAsync(harness.WorkItemId, harness.Request, CancellationToken.None);
            return harness;
        }

        public Task ExecuteAsync() => Command.ExecuteAsync(WorkItemId, CancellationToken.None);

        public async Task<IntakeOcrOperation> ReadAsync() =>
            await Store.FindAsync(WorkItemId, CancellationToken.None)
            ?? throw new InvalidOperationException("The operation was not recorded.");

        public async Task<ExternalWorkItemEntity?> ReadWorkItemAsync()
        {
            await using var context = await ContextFactory.CreateDbContextAsync(CancellationToken.None);
            return await context.Set<ExternalWorkItemEntity>().AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == WorkItemId, CancellationToken.None);
        }

        public async Task<int> CountOperationsAsync()
        {
            await using var context = await ContextFactory.CreateDbContextAsync(CancellationToken.None);
            return await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.IntakeOcrOperations")
                .SingleAsync(CancellationToken.None);
        }

        public static IntakeOcrResult Completed(IReadOnlyList<int> pages) => new(
            IntakeOcrState.Completed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            "provider-op-1",
            "response-hash-1",
            [.. pages.Select(page => new IntakeOcrPage(
                page,
                "SYNTHETIC PAGE",
                [new("SYNTHETIC PAGE", new(1, 2, 5, 6, "inch"), [])],
                []))]);

        public async ValueTask DisposeAsync()
        {
            this.scope.Dispose();
            await this.factory.DisposeAsync();
        }

        private static async Task<Guid> RetainAsync(IntakeWebApplicationFactory factory)
        {
            using var client = IntakeWebDriver.CreateClient(factory);
            var email = IntakeTestEvidence.CreateEmail(
                "scan-like-instruction.eml",
                ScanLikeBody,
                senderAddress: "post@an-unrecognised-broker.example",
                subject: "Instruction paperwork");
            var upload = await IntakeWebDriver.UploadAndProcessAsync(
                factory,
                client,
                email.FileName,
                email.MediaType,
                email.Content,
                Guid.NewGuid().ToString("N"));
            return IntakeWebDriver.ReceiptId(upload);
        }
    }

    private sealed class FakeProvider : IIntakeOcrProvider
    {
        public Func<IntakeOcrResult>? OnAnalyze { get; set; }

        public Func<IntakeOcrResult>? OnReconcile { get; set; }

        public int Analyses { get; private set; }

        public int Reconciliations { get; private set; }

        public Task<IntakeOcrResult> AnalyzeAsync(
            IntakeOcrRequest request,
            Stream content,
            Func<string, Task> onAccepted,
            CancellationToken cancellationToken)
        {
            IntakeOcrRequest.Validate(request);
            Analyses++;
            var result = OnAnalyze?.Invoke()
                ?? throw new InvalidOperationException("No submission was expected.");
            return AcceptedAsync(result, onAccepted);
        }

        private static async Task<IntakeOcrResult> AcceptedAsync(
            IntakeOcrResult result, Func<string, Task> onAccepted)
        {
            if (result.ProviderOperationId is { } providerOperationId)
            {
                await onAccepted(providerOperationId);
            }
            return result;
        }

        public Task<IntakeOcrResult> ReconcileAsync(
            IntakeOcrRequest request,
            string providerOperationId,
            CancellationToken cancellationToken)
        {
            Reconciliations++;
            return Task.FromResult(OnReconcile?.Invoke()
                ?? throw new InvalidOperationException("No reconciliation was expected."));
        }
    }

    private sealed class RecordingAnalysis : IAnalyzeRetainedInstruction
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
                "no profile matched the fixture",
                [],
                false));
        }
    }

    /// <summary>
    /// A C-owned stand-in for A04, covering only what a retained pre-case asset
    /// needs, and verifying the caller's expected hash and length before handing
    /// back a byte — the real reader's own guarantee, so the OCR path's claim to
    /// read the immutable source is a real dependence rather than a comment.
    /// </summary>
    private sealed class RetainedAssetReader(
        IIntakeReceiptQueries receiptQueries,
        IIntakeArtifactStore artifactStore) : IReadLogicalDocumentVersion
    {
        public async Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
            if (request.IntakeAssetId is not { } assetId
                || request.IntakeReceiptId is not { } receiptId)
            {
                throw new NotSupportedException("This reader serves retained intake assets only.");
            }

            var receipt = await receiptQueries.GetAsync(receiptId, cancellationToken)
                ?? throw new KeyNotFoundException("The intake receipt does not exist.");
            var asset = receipt.AssetRecords.SingleOrDefault(record => record.Id == assetId)
                ?? throw new KeyNotFoundException("The retained asset does not exist.");
            var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
                ?? throw new IntakeArtifactIntegrityException();
            var hash = Convert.ToHexStringLower(SHA256.HashData(content.Span));
            if (content.Length != request.ExpectedContentLength
                || !string.Equals(hash, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new IntakeArtifactIntegrityException();
            }

            return new(
                new MemoryStream(content.ToArray(), writable: false),
                null,
                null,
                assetId,
                hash,
                content.Length,
                asset.FileName,
                asset.MediaType);
        }
    }
}
