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
/// real retained intake asset or Case document.
///
/// The provider is a structural fake — no genuine OCR output exists on this
/// machine and none is invented as evidence — but everything that decides
/// whether an outage costs the business a duplicate reading is real here: the
/// operation row, its version, its unique operation key, and the order the
/// writes happen in.
///
/// The existing offline composition supplies the metadata query and verified
/// logical content reader. No provider call or genuine OCR acceptance is claimed.
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
    public async Task RetainedOutputKeepsExternalWorkPendingUntilAnalysisIsApplied()
    {
        await using var harness = await Harness.CreateAsync();
        var pending = await harness.ReadAsync();
        await harness.Store.CompleteAsync(pending.Id, pending.Version, Harness.Completed([1]), CancellationToken.None);
        var retained = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Completed, retained.State);
        Assert.False(retained.AnalysisCompleted);
        Assert.Equal(ExternalWorkStatePersistence.Pending, (await harness.ReadWorkItemAsync())!.State);
        Assert.Null((await harness.ReadWorkItemAsync())!.CompletedAtUtc);

        // A new delivery after a crash uses the SQL-retained output only.
        await harness.ExecuteAsync();
        Assert.True((await harness.ReadAsync()).AnalysisCompleted);
        Assert.Equal(ExternalWorkStatePersistence.Completed, (await harness.ReadWorkItemAsync())!.State);
        Assert.Single(harness.Analysis.Requests);
        Assert.Equal(0, harness.Provider.Analyses);
        Assert.Equal(0, harness.Provider.Reconciliations);
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
    public async Task ABeginRepeatedWhenPairedWorkItemIsMissingThrowsInvalidOperationException()
    {
        await using var harness = await Harness.CreateAsync();

        await using (var context = await harness.ContextFactory.CreateDbContextAsync())
        {
            var workItem = await context.Set<ExternalWorkItemEntity>()
                .SingleAsync(w => w.Id == harness.WorkItemId);
            context.Set<ExternalWorkItemEntity>().Remove(workItem);
            await context.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Store.BeginAsync(
                harness.WorkItemId,
                harness.Request,
                CancellationToken.None));

        Assert.Contains("paired external work item row does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUpdateWhenPairedWorkItemIsMissingThrowsInvalidOperationExceptionAndMutatesNothing()
    {
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.ReadAsync();

        await using (var context = await harness.ContextFactory.CreateDbContextAsync())
        {
            var workItem = await context.Set<ExternalWorkItemEntity>()
                .SingleAsync(w => w.Id == harness.WorkItemId);
            context.Set<ExternalWorkItemEntity>().Remove(workItem);
            await context.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Store.RecordSubmitAttemptAsync(
                harness.WorkItemId,
                initial.Version,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.Contains("paired external work item row does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);

        var unchanged = await harness.ReadAsync();
        Assert.Equal(initial.Version, unchanged.Version);
        Assert.Equal(initial.State, unchanged.State);
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
            Guid workItemId,
            IntakeOcrRequest request)
        {
            this.factory = factory;
            this.scope = scope;
            var services = scope.ServiceProvider;
            ContextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            Store = new EfIntakeOcrOperationStore(ContextFactory);
            WorkItemId = workItemId;
            Request = request;
            Command = new ProcessIntakeOcr(
                Store,
                Provider,
                services.GetRequiredService<IReadLogicalDocumentVersion>(),
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
                Guid.NewGuid(),
                new(receipt.Id, asset.Id, asset.ContentHash, asset.ContentLength,
                    [1], $"ocr:{receipt.Id:N}:1"));
            await harness.Store.BeginAsync(harness.WorkItemId, harness.Request, CancellationToken.None);
            return harness;
        }

        public Task ExecuteAsync() => Command.ExecuteAsync(WorkItemId, CancellationToken.None);

        public async Task ExecuteAfterRestartAsync()
        {
            using var restartedScope = factory.Services.CreateScope();
            var services = restartedScope.ServiceProvider;
            var command = new ProcessIntakeOcr(
                new EfIntakeOcrOperationStore(services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()),
                Provider,
                services.GetRequiredService<IReadLogicalDocumentVersion>(),
                Analysis,
                services.GetRequiredService<IIntakeReceiptQueries>(),
                services.GetRequiredService<TimeProvider>());
            await command.ExecuteAsync(WorkItemId, default);
        }

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

        public IntakeOcrRequest? SubmittedRequest { get; private set; }

        public string? SubmittedContentHash { get; private set; }

        public long SubmittedContentLength { get; private set; }

        public async Task<IntakeOcrResult> AnalyzeAsync(
            IntakeOcrRequest request,
            Stream content,
            Func<string, Task> onAccepted,
            CancellationToken cancellationToken)
        {
            IntakeOcrRequest.Validate(request);
            Analyses++;
            SubmittedRequest = request;
            using var received = new MemoryStream();
            await content.CopyToAsync(received, cancellationToken);
            SubmittedContentHash = Convert.ToHexStringLower(SHA256.HashData(received.ToArray()));
            SubmittedContentLength = received.Length;
            var result = OnAnalyze?.Invoke()
                ?? throw new InvalidOperationException("No submission was expected.");
            return await AcceptedAsync(result, onAccepted);
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

}
