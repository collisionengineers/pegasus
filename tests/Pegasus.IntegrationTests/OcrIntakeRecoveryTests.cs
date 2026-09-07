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

    [Fact]
    [Trait("Category", "Corpus")]
    public async Task RetainedCaseOcrBindsItsExactSourceAndRefusesChangedReplayIntent()
    {
        await using var harness = await Harness.CreateDocumentAsync();
        var source = Assert.IsType<CaseDocumentMetadata>(harness.CaseSource);
        var operation = await harness.ReadAsync();
        Assert.Null(operation.IntakeReceiptId);
        Assert.Null(operation.IntakeAssetId);
        Assert.Equal(source.CaseId, operation.CaseId);
        Assert.Equal(source.OccurrenceId, operation.OccurrenceId);
        Assert.Equal(source.VersionId, operation.DocumentVersionId);
        Assert.Equal(source.Sha256, operation.SourceSha256);
        Assert.Equal(source.ContentLength, operation.SourceContentLength);
        Assert.Equal([1, 3], operation.QualifiedPages);

        var recreatedStore = new EfIntakeOcrOperationStore(harness.ContextFactory);
        var replay = await IntakeOcrOperations.BeginDocumentAsync(recreatedStore, source, [3, 1, 3], default);
        Assert.Equal(operation.Id, replay.Id);
        Assert.Equal(operation.OperationKey, replay.OperationKey);
        await Assert.ThrowsAsync<IntakeOcrOperationConflictException>(() =>
            IntakeOcrOperations.BeginDocumentAsync(recreatedStore,
                source with { ContentLength = source.ContentLength + 1 }, [1, 3], default));

        IntakeOcrRequest[] changedRequests =
        [
            harness.Request with { CaseId = Guid.NewGuid() },
            harness.Request with { OccurrenceId = Guid.NewGuid() },
            harness.Request with { DocumentVersionId = Guid.NewGuid() },
            harness.Request with { SourceSha256 = new string('A', 64) },
            harness.Request with { SourceContentLength = source.ContentLength + 1 },
            harness.Request with { QualifiedPages = [1] }
        ];
        foreach (var changed in changedRequests)
            await Assert.ThrowsAsync<IntakeOcrOperationConflictException>(() =>
                recreatedStore.BeginAsync(operation.Id, changed, default));

        Assert.Equal(1, await harness.CountOperationsAsync());
        Assert.Equal(operation.Version, (await harness.ReadAsync()).Version);
        Assert.Equal(0, harness.Provider.Analyses);
        Assert.Equal(ExternalWorkStatePersistence.Pending, (await harness.ReadWorkItemAsync())!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Corpus")]
    public async Task RetainedCaseOcrCompletesWithoutInstructionAnalysisAndReusesOutputAfterRestart(bool outputAlreadyRetained)
    {
        await using var harness = await Harness.CreateDocumentAsync();
        harness.Provider.OnAnalyze = () => Harness.Completed([1, 3]);
        if (outputAlreadyRetained)
        {
            var pending = await harness.ReadAsync();
            await harness.Store.CompleteAsync(pending.Id, pending.Version, Harness.Completed([1, 3]), default);
            Assert.False((await harness.ReadAsync()).AnalysisCompleted);
            Assert.Equal(ExternalWorkStatePersistence.Pending, (await harness.ReadWorkItemAsync())!.State);
        }

        await harness.ExecuteAfterRestartAsync();
        var completed = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Completed, completed.State);
        Assert.True(completed.AnalysisCompleted);
        Assert.Equal([1, 3], completed.PageResults.Select(page => page.Number));
        Assert.Equal("response-hash-1", completed.ResponseSha256);
        Assert.NotNull(completed.Result);
        Assert.Equal(IntakeOcrProviderIdentity.ModelId, completed.Result.ModelId);
        Assert.Equal(IntakeOcrProviderIdentity.ApiVersion, completed.Result.ApiVersion);
        var work = await harness.ReadWorkItemAsync();
        Assert.Equal(ExternalWorkStatePersistence.Completed, work!.State);
        Assert.NotNull(work.CompletedAtUtc);

        await harness.ExecuteAfterRestartAsync();
        Assert.Equal(completed.Version, (await harness.ReadAsync()).Version);
        Assert.Equal(outputAlreadyRetained ? 0 : 1, harness.Provider.Analyses);
        Assert.Equal(0, harness.Provider.Reconciliations);
        Assert.Empty(harness.Analysis.Requests);
        Assert.Equal(1, await harness.CountOperationsAsync());
        if (!outputAlreadyRetained)
        {
            var submitted = Assert.IsType<IntakeOcrRequest>(harness.Provider.SubmittedRequest);
            Assert.Equal(harness.Request.CaseId, submitted.CaseId);
            Assert.Equal(harness.Request.OccurrenceId, submitted.OccurrenceId);
            Assert.Equal(harness.Request.DocumentVersionId, submitted.DocumentVersionId);
            Assert.Null(submitted.IntakeReceiptId);
            Assert.Null(submitted.IntakeAssetId);
            Assert.Equal(harness.Request.OperationKey, submitted.OperationKey);
            Assert.Equal(harness.Request.QualifiedPages, submitted.QualifiedPages);
            Assert.Equal(harness.Request.SourceSha256, submitted.SourceSha256);
            Assert.Equal(harness.Request.SourceContentLength, submitted.SourceContentLength);
            Assert.Equal(harness.Request.SourceSha256, harness.Provider.SubmittedContentHash);
            Assert.Equal(harness.Request.SourceContentLength, harness.Provider.SubmittedContentLength);
        }
        await using var db = await harness.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, (await db.CaseWorkflows.SingleAsync(value => value.CaseId == harness.CaseSource!.CaseId)).Version);
        Assert.Empty(await db.Set<CaseRepairSpecificationEntity>().ToListAsync());
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("wrong-case")]
    [InlineData("unconfirmed")]
    [Trait("Category", "Corpus")]
    public async Task RetainedCaseOcrRefusesUnavailableSourceBeforeProviderSubmission(string invalidation)
    {
        await using var harness = await Harness.CreateDocumentAsync(wrongCase: invalidation == "wrong-case");
        if (invalidation != "wrong-case")
        {
            await using var db = await harness.ContextFactory.CreateDbContextAsync();
            var version = await db.Set<DocumentVersionEntity>().SingleAsync(value => value.Id == harness.CaseSource!.VersionId);
            if (invalidation == "removed")
                version.IsLogicallyRemoved = true;
            else
                version.CustodyStatus = DocumentCustodyStatus.Pending;
            await db.SaveChangesAsync();
        }

        await harness.ExecuteAfterRestartAsync();
        var failed = await harness.ReadAsync();
        Assert.Equal(IntakeOcrState.Failed, failed.State);
        Assert.Contains("ocr_source_unavailable", failed.LastError, StringComparison.Ordinal);
        Assert.Null(failed.SubmitAttemptedAtUtc);
        Assert.Null(failed.ProviderOperationId);
        Assert.Empty(failed.PageResults);
        Assert.Equal(ExternalWorkStatePersistence.Failed, (await harness.ReadWorkItemAsync())!.State);
        Assert.Equal(0, harness.Provider.Analyses);
        Assert.Equal(0, harness.Provider.Reconciliations);
        Assert.Empty(harness.Analysis.Requests);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly IntakeWebApplicationFactory factory;
        private readonly IServiceScope scope;

        private Harness(
            IntakeWebApplicationFactory factory,
            IServiceScope scope,
            Guid workItemId,
            IntakeOcrRequest request,
            CaseDocumentMetadata? caseSource = null)
        {
            this.factory = factory;
            this.scope = scope;
            var services = scope.ServiceProvider;
            ContextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            Store = new EfIntakeOcrOperationStore(ContextFactory);
            WorkItemId = workItemId;
            Request = request;
            CaseSource = caseSource;
            Command = new ProcessIntakeOcr(
                Store,
                Provider,
                services.GetRequiredService<IReadLogicalDocumentVersion>(),
                Analysis,
                services.GetRequiredService<IIntakeReceiptQueries>(),
                services.GetRequiredService<IGetCaseDocumentMetadata>(),
                TimeProvider.System);
        }

        public Guid WorkItemId { get; }

        public IntakeOcrRequest Request { get; }

        public CaseDocumentMetadata? CaseSource { get; }

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
                new(receipt.Id, null, asset.Id, asset.ContentHash, asset.ContentLength,
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
                services.GetRequiredService<IGetCaseDocumentMetadata>(),
                services.GetRequiredService<TimeProvider>());
            await command.ExecuteAsync(WorkItemId, default);
        }

        public static async Task<Harness> CreateDocumentAsync(bool wrongCase = false)
        {
            const string fileName = "1952640666665__YL69YFO CALCULATION SHEET.pdf";
            var directory = new DirectoryInfo(CorpusPackage.RepositoryRoot);
            string? sourcePath = null;
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "pegasus_pack", "glasses-integration", "glass_ref_docs", fileName);
                if (File.Exists(candidate))
                {
                    sourcePath = candidate;
                    break;
                }
                directory = directory.Parent;
            }
            Assert.NotNull(sourcePath);
            var content = await File.ReadAllBytesAsync(sourcePath);
            var hash = Convert.ToHexStringLower(SHA256.HashData(content));
            Assert.Equal("6918c91fce058b5365446681045056158336b90e1de89ba1bdc5a1d7394f728c", hash);

            var factory = new IntakeWebApplicationFactory();
            var receiptId = await RetainAsync(factory);
            var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            var caseId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            const string reference = "QDOS31001";
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
            await using (var db = await contextFactory.CreateDbContextAsync())
            {
                var principal = await SeededPrincipals.QdosAsync(db);
                db.AddRange(
                    new CaseEntity
                    {
                        Id = caseId, PrincipalId = principal.Id, SequenceLineageId = principal.SequenceLineageId,
                        Year = 2031, Sequence = 1, Reference = reference, Type = "Inspection", InitialState = "NotReady",
                        CustodyState = "confirmed", OriginIntakeReceiptId = receiptId, CreatedAtUtc = now,
                        ConcurrencyToken = Guid.NewGuid()
                    },
                    new CaseWorkflowEntity { CaseId = caseId, State = "NotReady", Version = 0, ConcurrencyToken = Guid.NewGuid() },
                    new CaseDocumentEntity
                    {
                        Id = documentId, CaseId = caseId, Ordinal = 1,
                        SourceOccurrenceIdentity = $"ocr-source:{occurrenceId:N}"
                    },
                    new DocumentVersionEntity
                    {
                        Id = versionId, DocumentId = documentId, Version = 1, FileName = fileName, MediaType = "application/pdf",
                        ContentLength = content.LongLength, Sha256 = hash, CustodyStatus = DocumentCustodyStatus.Confirmed,
                        CreatedAtUtc = now, CreatedBy = "test", IsCurrent = true
                    },
                    new DocumentOccurrenceEntity
                    {
                        Id = occurrenceId, CaseId = caseId, DocumentId = documentId, VersionId = versionId, Ordinal = 1,
                        SemanticRole = DocumentSemanticRole.OriginalSource, Source = DocumentSource.StaffUpload,
                        SourceOccurrenceIdentity = $"ocr-source:{occurrenceId:N}", RecordedAtUtc = now,
                        OperationKey = $"ocr-source:{occurrenceId:N}"
                    });
                await db.SaveChangesAsync();
            }
            await services.GetRequiredService<IDocumentContentStore>().StoreAsync(
                caseId, reference, versionId, content, hash, default);
            var source = await services.GetRequiredService<IGetCaseDocumentMetadata>().ExecuteAsync(
                new(caseId, occurrenceId, versionId, ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId)), default);
            Assert.NotNull(source);
            var operation = await IntakeOcrOperations.BeginDocumentAsync(
                new EfIntakeOcrOperationStore(contextFactory),
                wrongCase ? source with { CaseId = Guid.NewGuid() } : source, [3, 1, 3], default);
            return new(factory, scope, operation.Id,
                new(null, versionId, null, hash, content.LongLength, operation.QualifiedPages,
                    operation.OperationKey, operation.CaseId, occurrenceId), source);
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
