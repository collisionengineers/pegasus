using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.CaseExport;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.IntegrationTests.Support;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CustodyOutboxIntegrationTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReevaluationReadsTheRetainedSourceAndHoldsItAfterAutomation(bool repairHolding)
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = CreatePreCaseReevaluationSource();
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            source.Source,
            $"reevaluation-retained-source:{Guid.NewGuid():N}",
            CancellationToken.None);
        var firstEvaluation = await DrainStagedAsync(
            services,
            received.StagedReceiptId,
            CancellationToken.None);
        await using (var db = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var stagedStorageKey = await db.IntakeStagedReceipts
                .Where(item => item.Id == received.StagedReceiptId)
                .Select(item => item.StorageKey)
                .SingleAsync();
            Assert.Null(await services.GetRequiredService<IIntakeArtifactStore>()
                .GetStagedAsync(stagedStorageKey, CancellationToken.None));
        }
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var original = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            firstEvaluation.ProcessedReceiptId,
            CancellationToken.None));
        var originalSource = Assert.Single(original.AssetRecords, asset =>
            asset.Kind == IntakeAssetKind.Source
            && asset.Disposition == IntakeAssetDisposition.Source);

        if (repairHolding)
        {
            await using var db = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var assets = await db.IntakeAssets.Where(asset => asset.IntakeReceiptId == original.Id).ToListAsync();
            Assert.NotEmpty(assets);
            foreach (var asset in assets)
            {
                asset.CustodyStatus = "unknown";
                asset.BoxFileId = null;
                asset.BoxVersionId = null;
            }
            await db.SaveChangesAsync();
        }

        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(
            new(
                original.Id,
                original.Version,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                $"reevaluate-retained-source:{Guid.NewGuid():N}",
                "Re-evaluate the retained source under the current policy."),
            CancellationToken.None);

        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            received.StagedReceiptId,
            now,
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await workStore.MarkDispatchedAsync(
            dispatch.Id,
            Assert.IsType<string>(dispatch.LeaseToken),
            now,
            CancellationToken.None);
        // Re-evaluation reads the retained bytes; holding - repaired here when
        // it was left unknown - follows destination automation.
        var outcome = await ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services)
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);

        Assert.Equal(QueuedIntakeProcessingOutcome.Completed, outcome);
        var reevaluated = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            original.Id,
            CancellationToken.None));
        Assert.True(reevaluated.Version > original.Version);
        var reevaluatedSource = Assert.Single(reevaluated.AssetRecords, asset =>
            asset.Kind == IntakeAssetKind.Source
            && asset.Disposition == IntakeAssetDisposition.Source);
        Assert.Equal(originalSource.Id, reevaluatedSource.Id);
        Assert.Equal(originalSource.StorageKey, reevaluatedSource.StorageKey);
        Assert.Equal(originalSource.ContentHash, reevaluatedSource.ContentHash);
        Assert.Equal(IncomingArtifactCustodyState.Confirmed, reevaluatedSource.CustodyState);
        Assert.Equal(original.AssetRecords.Count, reevaluated.AssetRecords.Count);
    }

    [Fact]
    public async Task ReevaluationFailsClosedWhenTheRetainedSourceBytesAreCorrupt()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = CreatePreCaseReevaluationSource();
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            source.Source, $"reevaluation-corrupt-source:{Guid.NewGuid():N}", CancellationToken.None);
        var firstEvaluation = await DrainStagedAsync(
            services, received.StagedReceiptId, CancellationToken.None);
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var original = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            firstEvaluation.ProcessedReceiptId, CancellationToken.None));
        var originalSource = Assert.Single(original.AssetRecords, asset =>
            asset.Kind == IntakeAssetKind.Source
            && asset.Disposition == IntakeAssetDisposition.Source);
        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(
            new(
                original.Id,
                original.Version,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                $"reevaluate-corrupt-source:{Guid.NewGuid():N}",
                "Re-evaluate the retained source under the current policy."),
            CancellationToken.None);
        var queued = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            original.Id, CancellationToken.None));
        var retainedPath = Path.Combine(
            factory.ArtifactDirectory,
            originalSource.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        await File.WriteAllBytesAsync(retainedPath, [1, 2, 3]);

        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            received.StagedReceiptId, now, TimeSpan.FromMinutes(1), CancellationToken.None));
        await workStore.MarkDispatchedAsync(
            dispatch.Id, Assert.IsType<string>(dispatch.LeaseToken), now, CancellationToken.None);
        var outcome = await ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services)
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);

        Assert.Equal(QueuedIntakeProcessingOutcome.Failed, outcome);
        Assert.Equal(queued.Version, Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            original.Id, CancellationToken.None)).Version);
        var failedWork = Assert.IsType<IntakeWorkItem>(await IntakeWorkItemReads.FindAsync(
            services, received.StagedReceiptId));
        Assert.Equal(IntakeWorkState.Failed, failedWork.State);
        Assert.Equal("staged_artifact_integrity_failure", failedWork.FailureCode);
    }

    [Fact]
    public async Task AFirstHoldingFailureIsRetriedOnItsRecordedEvaluation()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = CreatePreCaseReevaluationSource();
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            source.Source, $"first-holding-failure:{Guid.NewGuid():N}", CancellationToken.None);
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            received.StagedReceiptId, now, TimeSpan.FromMinutes(1), CancellationToken.None));
        await workStore.MarkDispatchedAsync(dispatch.Id, dispatch.LeaseToken!, now, CancellationToken.None);
        var handover = new FailFirstHoldingHandover(services.GetRequiredService<ICaseArtifactCustody>());
        var retention = new RetainIncomingArtifact(
            handover, services.GetRequiredService<IIncomingArtifactRetentionStore>());
        var processing = ActivatorUtilities.CreateInstance<ProcessIntake>(services, retention);
        var processor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services, processing);

        // Holding follows destination automation, so the failed hand-over
        // leaves the recorded evaluation pending on the ordinary retry.
        Assert.Equal(QueuedIntakeProcessingOutcome.RetryScheduled,
            await processor.ExecuteAsync(received.StagedReceiptId));
        Assert.Null(await workStore.GetCompletedEvaluationAsync(received.StagedReceiptId, CancellationToken.None));
        Assert.Equal(1L, await factory.Database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeEvaluations"));
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var original = Assert.IsType<IntakeReceipt>(await receipts.FindBySourceIdentityAsync(
            source.Source.SourceIdentity, CancellationToken.None));
        Assert.Contains(original.AssetRecords, asset => asset.CustodyState == IncomingArtifactCustodyState.Unknown);
        var originalIds = original.AssetRecords.Select(asset => asset.Id).Order().ToArray();

        var retryAt = now.AddMinutes(1);
        dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            received.StagedReceiptId, retryAt, TimeSpan.FromMinutes(1), CancellationToken.None));
        await workStore.MarkDispatchedAsync(dispatch.Id, dispatch.LeaseToken!, retryAt, CancellationToken.None);

        Assert.Equal(QueuedIntakeProcessingOutcome.Completed,
            await processor.ExecuteAsync(received.StagedReceiptId));
        var repaired = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(original.Id, CancellationToken.None));
        Assert.Equal(originalIds, repaired.AssetRecords.Select(asset => asset.Id).Order());
        Assert.All(repaired.AssetRecords, asset => Assert.Equal(IncomingArtifactCustodyState.Confirmed, asset.CustodyState));
        Assert.Equal(1L, await factory.Database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeReceipts"));
        Assert.Equal(1L, await factory.Database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeEvaluations"));
    }

    private sealed class FailFirstHoldingHandover(ICaseArtifactCustody inner) : ICaseArtifactCustody
    {
        private bool failed;

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            if (!failed)
            {
                failed = true;
                throw new IOException("Synthetic holding handover interruption.");
            }
            return inner.RetainAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = CreatePreCaseReevaluationSource();
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            source.Source,
            $"reevaluation-mismatched-source:{Guid.NewGuid():N}",
            CancellationToken.None);
        var firstEvaluation = await DrainStagedAsync(
            services, received.StagedReceiptId, CancellationToken.None);
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var original = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            firstEvaluation.ProcessedReceiptId, CancellationToken.None));
        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(
            new(
                original.Id,
                original.Version,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                $"reevaluate-mismatched-source:{Guid.NewGuid():N}",
                "Re-evaluate the retained source under the current policy."),
            CancellationToken.None);
        var queued = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            original.Id, CancellationToken.None));

        await using (var db = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var sourceAsset = await db.IntakeAssets.SingleAsync(asset =>
                asset.IntakeReceiptId == original.Id
                && asset.Kind == "source"
                && asset.Disposition == "source");
            sourceAsset.ContentHash = new string('F', 64);
            await db.SaveChangesAsync();
        }

        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            received.StagedReceiptId, now, TimeSpan.FromMinutes(1), CancellationToken.None));
        await workStore.MarkDispatchedAsync(
            dispatch.Id, Assert.IsType<string>(dispatch.LeaseToken), now, CancellationToken.None);
        var outcome = await ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services)
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);

        Assert.Equal(QueuedIntakeProcessingOutcome.Failed, outcome);
        var unchanged = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(
            original.Id, CancellationToken.None));
        Assert.Equal(queued.Version, unchanged.Version);
        Assert.Equal("reevaluation_pending", unchanged.FailureCode);
        var failedWork = Assert.IsType<IntakeWorkItem>(await IntakeWorkItemReads.FindAsync(
            services, received.StagedReceiptId));
        Assert.Equal(IntakeWorkState.Failed, failedWork.State);
        Assert.Equal("staged_artifact_integrity_failure", failedWork.FailureCode);
    }

    [Fact]
    public async Task AcceptedOfflineCaseRecoversDispatchLeaseAndRetainsExactSourceReplaySafely()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var store = scope.ServiceProvider.GetRequiredService<IExternalWorkStore>();
        var abandoned = Assert.IsType<ExternalWorkDispatchClaim>(
            await store.ClaimDispatchAsync(
                FixedUtcNow,
                TimeSpan.FromMinutes(1),
                CancellationToken.None));
        Assert.Equal(accepted.CustodyWorkId, abandoned.WorkItemId);

        var timeProvider = new MutableTimeProvider(FixedUtcNow.AddMinutes(2));
        var queue = new RecordingExternalWorkQueue();
        var dispatcher = new DispatchPendingExternalWork(store, queue, timeProvider);

        Assert.Equal(1, await dispatcher.ExecuteAsync(10, CancellationToken.None));
        Assert.Equal([accepted.CustodyWorkId], queue.WorkItemIds);
        Assert.Equal(0, await dispatcher.ExecuteAsync(10, CancellationToken.None));

        var editor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var workflowBeforeCustody = Assert.IsType<CaseWorkflowRecord>(
            await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        var editorLease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
            .ClaimAsync(
                new(
                    accepted.CaseId,
                    workflowBeforeCustody.Version,
                    editor,
                    $"custody-editor-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
        var processor = scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        await processor.ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        await new ReconcilePoisonedExternalWork(store, timeProvider)
            .ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        var workflowAfterCustody = Assert.IsType<CaseWorkflowRecord>(
            await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        Assert.Equal(checked(workflowBeforeCustody.Version + 1), workflowAfterCustody.Version);
        // Custody is system work: it moved the version under the editor's
        // lease without ending it, so the editor's next action at the version
        // their page read still lands (operator, 6 October 2026).
        var editorAdded = await scope.ServiceProvider.GetRequiredService<IAddCaseDocument>()
            .ExecuteAsync(
                new(
                    accepted.CaseId,
                    "editor.txt",
                    "text/plain",
                    "editor content"u8.ToArray(),
                    DocumentSemanticRole.Other,
                    DocumentSource.StaffUpload,
                    $"editor:{Guid.NewGuid():N}",
                    editor,
                    $"editor-add:{Guid.NewGuid():N}",
                    editorLease.Version,
                    editorLease.Token),
                CancellationToken.None);
        Assert.False(editorAdded.IsReplay);
        Assert.Equal(
            checked(workflowAfterCustody.Version + 1),
            Assert.IsType<CaseWorkflowRecord>(
                await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                    .GetAsync(accepted.CaseId, CancellationToken.None)).Version);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.CustodyWorkId));
        Assert.Equal(
            "confirmed",
            await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.CaseId));
        Assert.Equal(
            1,
            await CountCaseHistoryAsync(
                scope.ServiceProvider,
                accepted.CaseId,
                "custody_confirmed"));
        Assert.Equal(
            0,
            await CountCaseHistoryAsync(
                scope.ServiceProvider,
                accepted.CaseId,
                "custody_failed"));

        var expectedHash = Convert.ToHexString(SHA256.HashData(accepted.Content)).ToLowerInvariant();
        var retainedPath = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            accepted.CaseId.ToString("N"),
            "documents",
            accepted.ReceiptId.ToString("N"),
            expectedHash,
            "content");
        Assert.Equal(accepted.Content, await File.ReadAllBytesAsync(retainedPath));
    }

    /// <summary>
    /// Custody confirming under a member of staff's edit lease never ends their session
    /// (operator, 6 October 2026): the lease survives, the editor's heartbeat answers the
    /// version custody moved the Case to, and their Save at the version the page read lands.
    /// </summary>
    [Fact]
    public async Task CustodyUnderAHeldLeaseKeepsItAndTheEditorsSaveAtTheReadVersionLands()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accepted = await AcceptDirectSourceAsync(services);
        var queries = services.GetRequiredService<ICaseWorkflowQueries>();
        var editor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var read = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(accepted.CaseId, CancellationToken.None));
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(accepted.CaseId, read.Version, editor, $"custody-held-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "confirmed",
            await ReadCaseCustodyStateAsync(services, accepted.CaseId));
        var afterCustody = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(accepted.CaseId, CancellationToken.None));
        Assert.Equal(checked(read.Version + 1), afterCustody.Version);
        await using (var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.AsNoTracking()
                .SingleAsync(item => item.CaseId == accepted.CaseId);
            Assert.Equal(editor.SubjectId, workflow.EditLeaseHolder);
            Assert.False(string.IsNullOrWhiteSpace(workflow.EditLeaseTokenHash));
        }

        var beat = await services.GetRequiredService<IHeartbeatCaseEditLease>().ExecuteAsync(
            new(accepted.CaseId, editor, lease.Token),
            CancellationToken.None);
        Assert.Equal(lease.Token, beat.Token);
        Assert.Equal(afterCustody.Version, beat.Version);

        var saved = await services.GetRequiredService<ICaseWorkspaceStore>().SaveAsync(
            new SaveCaseWorkspaceRequest(
                accepted.CaseId,
                read.Version,
                editor,
                $"custody-held-save:{Guid.NewGuid():N}",
                "Recorded the damage",
                lease.Token)
            {
                Damage = new([new(["front"], "light", "Scuffed")], null)
            },
            CancellationToken.None);
        Assert.False(saved.WasReplay);
        Assert.Equal(checked(afterCustody.Version + 1), saved.Version);
    }

    [Fact]
    public async Task QueuedCustodyResolvesTheProcessedReceiptThroughItsStagedLineage()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptQueuedSourceAsync(scope.ServiceProvider);

        var processor = scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "confirmed",
            await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.CaseId));
        var expectedHash = Convert.ToHexString(SHA256.HashData(accepted.Content)).ToLowerInvariant();
        var retainedPath = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            accepted.CaseId.ToString("N"),
            "documents",
            accepted.ReceiptId.ToString("N"),
            expectedHash,
            "content");
        Assert.Equal(accepted.Content, await File.ReadAllBytesAsync(retainedPath));
    }

    [Fact]
    public async Task PoisonedCustodyIsTerminallyRecordedWithoutRedispatchOrDuplicateHistory()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var store = scope.ServiceProvider.GetRequiredService<IExternalWorkStore>();
        var reconciliation = new ReconcilePoisonedExternalWork(
            store,
            new MutableTimeProvider(FixedUtcNow));
        var initialQueue = new RecordingExternalWorkQueue();
        Assert.Equal(
            1,
            await new DispatchPendingExternalWork(
                    store,
                    initialQueue,
                    new MutableTimeProvider(FixedUtcNow))
                .ExecuteAsync(10, CancellationToken.None));
        Assert.Equal([accepted.CustodyWorkId], initialQueue.WorkItemIds);

        await reconciliation.ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        await reconciliation.ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);

        var replayQueue = new RecordingExternalWorkQueue();
        Assert.Equal(
            0,
            await new DispatchPendingExternalWork(
                    store,
                    replayQueue,
                    new MutableTimeProvider(FixedUtcNow.AddMinutes(10)))
                .ExecuteAsync(10, CancellationToken.None));
        Assert.Empty(replayQueue.WorkItemIds);
        Assert.Equal(
            "failed",
            await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.CustodyWorkId));
        Assert.Equal(
            "failed",
            await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.CaseId));
        Assert.Equal(
            1,
            await CountCaseHistoryAsync(
                scope.ServiceProvider,
                accepted.CaseId,
                "custody_failed"));
    }

    [Theory]
    [InlineData(IntakeOcrState.Pending, IntakeOcrState.Failed, "failed", null)]
    [InlineData(IntakeOcrState.Processing, IntakeOcrState.Unknown, "failed", "provider-operation")]
    [InlineData(IntakeOcrState.Unknown, IntakeOcrState.Unknown, "failed", null)]
    [InlineData(IntakeOcrState.Completed, IntakeOcrState.Completed, "completed", "provider-operation")]
    public async Task PoisonedOcrWorkConvergesWithoutRedispatchOrLosingProviderIdentity(
        IntakeOcrState initialState,
        IntakeOcrState expectedState,
        string expectedWorkState,
        string? providerOperationId)
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var retained = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(CreateSource().Source, CancellationToken.None);
        var sourceAsset = Assert.IsType<IntakeAssetRecord>(
            IntakeFileIdentity.SourceAsset(retained));
        var workItemId = Guid.NewGuid();
        var operationKey = $"ocr-poison:{workItemId:N}";
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.Set<IntakeOcrOperationEntity>().Add(new()
            {
                Id = workItemId,
                IntakeAssetId = sourceAsset.Id,
                SourceSha256 = sourceAsset.ContentHash,
                QualifiedPagesJson = "{}",
                OperationKey = operationKey,
                State = initialState.ToString(),
                ProviderOperationId = providerOperationId,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
            context.Set<ExternalWorkItemEntity>().Add(new()
            {
                Id = workItemId,
                Kind = ExternalWorkKinds.IntakeOcr,
                OperationKey = operationKey,
                State = ExternalWorkStatePersistence.Queued,
                DueAtUtc = FixedUtcNow
            });
            await context.SaveChangesAsync();
        }

        var store = services.GetRequiredService<IExternalWorkStore>();
        await new ReconcilePoisonedExternalWork(store, new MutableTimeProvider(FixedUtcNow))
            .ExecuteAsync(workItemId, CancellationToken.None);

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var operation = await context.Set<IntakeOcrOperationEntity>().AsNoTracking()
                .SingleAsync(item => item.Id == workItemId);
            var work = await context.Set<ExternalWorkItemEntity>().AsNoTracking()
                .SingleAsync(item => item.Id == workItemId);
            Assert.Equal(expectedState.ToString(), operation.State);
            Assert.Equal(providerOperationId, operation.ProviderOperationId);
            Assert.Equal(expectedWorkState, work.State);
        }

        Assert.Null(await store.ClaimDispatchAsync(
            workItemId,
            FixedUtcNow.AddMinutes(10),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
    }

    /// <summary>
    /// A staff upload is published to the read cache once its version has
    /// committed, from the bytes the upload holds. A replay of the same upload
    /// files nothing, so it publishes nothing.
    /// </summary>
    [Fact]
    public async Task AStaffUploadIsPublishedToTheReadCacheOnceItsVersionHasCommitted()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accepted = await AcceptDirectSourceAsync(services);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        long caseVersion;
        await using (var workflowContext = await contextFactory.CreateDbContextAsync())
        {
            caseVersion = await workflowContext.CaseWorkflows
                .AsNoTracking()
                .Where(workflow => workflow.CaseId == accepted.CaseId)
                .Select(workflow => workflow.Version)
                .SingleAsync();
        }
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(accepted.CaseId, caseVersion, actor, $"document-add-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var statusWhenPublished = new List<DocumentCustodyStatus>();
        var publisher = new RecordingCachePublisher(async key =>
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            statusWhenPublished.Add((await db.Set<DocumentVersionEntity>().AsNoTracking()
                .SingleAsync(version => version.Id == key.DocumentVersionId)).CustodyStatus);
        });
        var store = new EfDocumentCustodyStore(
            contextFactory,
            services.GetRequiredService<IDocumentContentStore>(),
            services.GetRequiredService<TimeProvider>(),
            publisher);
        var content = "evidence a member of staff uploaded"u8.ToArray();
        var command = new AddCaseDocumentCommand(
            accepted.CaseId,
            "evidence.txt",
            "text/plain",
            content,
            DocumentSemanticRole.Other,
            DocumentSource.StaffUpload,
            $"staff:{Guid.NewGuid():N}",
            actor,
            $"document-add:{Guid.NewGuid():N}",
            lease.Version,
            lease.Token);

        var added = await store.ExecuteAsync(command, CancellationToken.None);
        var replay = await store.ExecuteAsync(command, CancellationToken.None);

        Assert.False(added.IsReplay);
        Assert.True(replay.IsReplay);
        var published = Assert.Single(publisher.Published);
        Assert.Equal(DocumentContentCacheKey.ForVersion(added.Version.Id), published.Key);
        Assert.Equal(content, published.Bytes);
        Assert.Equal(added.Version.Sha256, published.Sha256);
        Assert.Equal(content.LongLength, published.ContentLength);
        Assert.Equal([DocumentCustodyStatus.Confirmed], statusWhenPublished);
    }

    [Fact]
    public async Task LogicallyRemovedVersionCannotBeDownloaded()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var content = "retained document"u8.ToArray();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await using var workflowContext = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var caseVersion = await workflowContext.CaseWorkflows
            .AsNoTracking()
            .Where(w => w.CaseId == accepted.CaseId)
            .Select(w => w.Version)
            .SingleAsync();
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var addLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                caseVersion,
                actor,
                $"document-add-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var added = await scope.ServiceProvider.GetRequiredService<IAddCaseDocument>()
            .ExecuteAsync(
                new(
                    accepted.CaseId,
                    "evidence.txt",
                    "text/plain",
                    content,
                    DocumentSemanticRole.Other,
                    DocumentSource.StaffUpload,
                    $"staff:{Guid.NewGuid():N}",
                    actor,
                    $"document-add:{Guid.NewGuid():N}",
                    addLease.Version,
                    addLease.Token),
                CancellationToken.None);
        // The add ended the lease it was made under.
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            scope.ServiceProvider.GetRequiredService<IAddCaseDocument>()
                .ExecuteAsync(
                    new(
                        accepted.CaseId,
                        "stale.txt",
                        "text/plain",
                        "stale upload"u8.ToArray(),
                        DocumentSemanticRole.Other,
                        DocumentSource.StaffUpload,
                        $"staff:{Guid.NewGuid():N}",
                        actor,
                        $"document-add-stale:{Guid.NewGuid():N}",
                        addLease.Version,
                        addLease.Token),
                    CancellationToken.None));

        var downloadOperationKey = $"document-download:{Guid.NewGuid():N}";

        await using (var download = Assert.IsType<DocumentDownload>(
                         await scope.ServiceProvider.GetRequiredService<IDownloadCaseDocument>()
                             .ExecuteAsync(
                                new(
                                    accepted.CaseId,
                                    added.Occurrence.Id,
                                    added.Version.Id,
                                    actor,
                                    downloadOperationKey),
                                 CancellationToken.None)))
        {
            using var copy = new MemoryStream();
            await download.Content.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }
        await using (var replay = Assert.IsType<DocumentDownload>(
                         await scope.ServiceProvider.GetRequiredService<IDownloadCaseDocument>()
                             .ExecuteAsync(
                                 new(
                                     accepted.CaseId,
                                     added.Occurrence.Id,
                                     added.Version.Id,
                                     actor,
                                     downloadOperationKey),
                                 CancellationToken.None)))
        {
            Assert.Equal(added.Version.Sha256, replay.Sha256);
        }

        var removeLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                checked(addLease.Version + 1),
                actor,
                $"document-remove-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var auditContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var auditContext = await auditContextFactory.CreateDbContextAsync())
        {
            var auditEntries = await auditContext.ActionHistory
                .Where(value => value.AggregateType == "case_document"
                    && value.CorrelationId == downloadOperationKey)
                .ToArrayAsync();
            var entry = Assert.Single(auditEntries);
            Assert.Equal(actor.SubjectId, entry.ActorSubjectId);
            Assert.False(string.IsNullOrWhiteSpace(entry.AfterJson));
        }

        await scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
            .ExecuteAsync(
                new(
                    accepted.CaseId,
                    added.Occurrence.Id,
                    actor,
                    "Removed from the active case file.",
                    $"document-remove:{Guid.NewGuid():N}",
                    removeLease.Version,
                    removeLease.Token),
                CancellationToken.None);

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IDownloadCaseDocument>()
            .ExecuteAsync(
                new(
                    accepted.CaseId,
                    added.Occurrence.Id,
                    added.Version.Id,
                    actor,
                    $"document-download-removed:{Guid.NewGuid():N}"),
                CancellationToken.None));
    }

    [Fact]
    public async Task StaffDocumentMutationRejectsMissingWrongHolderAndExpiredLease()
    {
        var timeProvider = new MutableTimeProvider(FixedUtcNow);
        using var factory = new IntakeWebApplicationFactory(timeProvider);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
            .ClaimAsync(
                new(
                    accepted.CaseId,
                    workflow.Version,
                    actor,
                    $"document-guard-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
        var add = scope.ServiceProvider.GetRequiredService<IAddCaseDocument>();
        AddCaseDocumentCommand Command(ActionActor commandActor, string token) => new(
            accepted.CaseId,
            "guard.txt",
            "text/plain",
            "guard content"u8.ToArray(),
            DocumentSemanticRole.Other,
            DocumentSource.StaffUpload,
            $"guard:{Guid.NewGuid():N}",
            commandActor,
            $"document-guard:{Guid.NewGuid():N}",
            lease.Version,
            token);

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            add.ExecuteAsync(Command(actor, string.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            add.ExecuteAsync(Command(actor, "wrong-token"), CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            add.ExecuteAsync(
                Command(
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                    lease.Token),
                CancellationToken.None));

        // A lapsed lease carries on only while nobody claims the Case; once a
        // colleague has it, the lapsed token is no authority.
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
            .ClaimAsync(
                new(
                    accepted.CaseId,
                    workflow.Version,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                    $"document-guard-colleague-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            add.ExecuteAsync(Command(actor, lease.Token), CancellationToken.None));
        var unchanged = Assert.IsType<CaseWorkflowRecord>(
            await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        Assert.Equal(workflow.Version, unchanged.Version);
    }

    [Theory]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    public async Task EveryTerminalCaseStateRejectsNewCustodyMutationsButPreservesExactReplay(
        CaseLifecycleState terminalState)
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var caseId = accepted.CaseId;
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var queries = scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>();
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(caseId, CancellationToken.None));

        workflow = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(caseId, CancellationToken.None));
        var addLease = await leases.ClaimAsync(
            new(
                caseId,
                workflow.Version,
                actor,
                $"terminal-document-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var addCommand = new AddCaseDocumentCommand(
            caseId,
            "terminal-evidence.txt",
            "text/plain",
            "terminal evidence"u8.ToArray(),
            DocumentSemanticRole.Other,
            DocumentSource.StaffUpload,
            $"terminal-document:{Guid.NewGuid():N}",
            actor,
            $"terminal-document-add:{Guid.NewGuid():N}",
            addLease.Version,
            addLease.Token);
        var addDocument = scope.ServiceProvider.GetRequiredService<IAddCaseDocument>();
        var added = await addDocument.ExecuteAsync(addCommand, CancellationToken.None);

        workflow = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(caseId, CancellationToken.None));
        var terminalLease = await leases.ClaimAsync(
            new(
                caseId,
                workflow.Version,
                actor,
                $"terminal-mutation-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var entity = await context.CaseWorkflows.SingleAsync(
                value => value.CaseId == caseId);
            entity.State = terminalState.ToString();
            await context.SaveChangesAsync();
        }

        Assert.True((await addDocument.ExecuteAsync(
            addCommand,
            CancellationToken.None)).IsReplay);

        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            addDocument.ExecuteAsync(
                addCommand with
                {
                    OperationKey = $"terminal-document-new:{Guid.NewGuid():N}",
                    SourceOccurrenceIdentity = $"terminal-document-new:{Guid.NewGuid():N}",
                    ExpectedCaseVersion = terminalLease.Version,
                    EditLeaseToken = terminalLease.Token
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
                .ExecuteAsync(
                    new(
                        caseId,
                        added.Occurrence.Id,
                        actor,
                        "Terminal cases are read-only.",
                        $"terminal-document-remove:{Guid.NewGuid():N}",
                        terminalLease.Version,
                        terminalLease.Token),
                    CancellationToken.None));
    }

    [Fact]
    public async Task WorkerDispatchPoisonAndTerminalRedeliveryPreserveOneCustodyEffect()
    {
        await AcceptedOfflineCaseRecoversDispatchLeaseAndRetainsExactSourceReplaySafely();
        await PoisonedCustodyIsTerminallyRecordedWithoutRedispatchOrDuplicateHistory();
    }

    [Fact]
    public async Task CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var processor = scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            processor.ExecuteAsync(accepted.CustodyWorkId, cancellation.Token));
        Assert.Equal("pending", await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.CustodyWorkId));

        var store = scope.ServiceProvider.GetRequiredService<IExternalWorkStore>();
        await new ReconcilePoisonedExternalWork(store, new MutableTimeProvider(FixedUtcNow))
            .ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        Assert.Equal("failed", await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.CustodyWorkId));
        Assert.Equal("failed", await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.CaseId));

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var workflow = Assert.IsType<CaseWorkflowRecord>(await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowQueries>().GetAsync(accepted.CaseId, default));
        var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(accepted.CaseId, workflow.Version, actor, "custody-recovery-lease"), default);
        var retried = await scope.ServiceProvider.GetRequiredService<IRetryCaseCustody>().ExecuteAsync(
            new(
                accepted.CaseId,
                lease.Version,
                actor,
                "custody-recovery-after-cancellation",
                "Retry after the persisted custody failure was reviewed.",
                lease.Token,
                CustodyTargetKind.CaseSource),
            default);

        Assert.Equal(RetryCaseCustodyOutcome.Pending, retried.Outcome);
        Assert.Equal("pending", await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.CustodyWorkId));

        // The adapter effect can succeed while the following SQL commit fails.
        // The work becomes a visible, staff-recoverable failure and a reasoned
        // retry reconciles the idempotent custody effect instead of duplicating it.
        var sqlFault = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var completionFault = new FailNextCustodyCompletionInterceptor();
        var faultOptions = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(factory.Database.ConnectionString)
            .AddInterceptors(completionFault)
            .Options;
        var faultFactory = new OptionsDbContextFactory(faultOptions);
        var faultStore = new EfExternalWorkStore(faultFactory, new MutableTimeProvider(FixedUtcNow));
        var countedFaultCustody = new CountingCustody(
            scope.ServiceProvider.GetRequiredService<ICaseCustody>());
        completionFault.FailNextCompletion();
        await Assert.ThrowsAsync<DbUpdateException>(() => new EfQueuedCustodyProcessor(
            faultFactory,
            faultStore,
            countedFaultCustody,
            new MutableTimeProvider(FixedUtcNow)).ExecuteAsync(sqlFault.CustodyWorkId, default));
        Assert.True(countedFaultCustody.EffectCalls > 0);
        Assert.Equal("failed", await ReadExternalWorkStateAsync(scope.ServiceProvider, sqlFault.CustodyWorkId));
        Assert.Equal("failed", await ReadCaseCustodyStateAsync(scope.ServiceProvider, sqlFault.CaseId));
        await RetryFailedCustodyAsync(scope.ServiceProvider, sqlFault, "custody-recovery-after-sql-fault");
        await processor.ExecuteAsync(sqlFault.CustodyWorkId, default);
        Assert.Equal("confirmed", await ReadCaseCustodyStateAsync(scope.ServiceProvider, sqlFault.CaseId));

        // A newer lease that appears before any adapter call stops the stale
        // holder. Its failure write is lease-guarded and cannot overwrite the
        // newer holder; once that technical lease expires, normal dispatch may run.
        var preEffectLeaseLoss = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var normalStore = new EfExternalWorkStore(dbFactory, new MutableTimeProvider(FixedUtcNow));
        var stolenBeforeEffect = new StealLeaseOnCheckStore(
            normalStore,
            dbFactory,
            "newer-holder-before-effect",
            FixedUtcNow.AddMinutes(5));
        var preEffectCustody = new CountingCustody(
            scope.ServiceProvider.GetRequiredService<ICaseCustody>());
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => new EfQueuedCustodyProcessor(
            dbFactory,
            stolenBeforeEffect,
            preEffectCustody,
            new MutableTimeProvider(FixedUtcNow)).ExecuteAsync(preEffectLeaseLoss.CustodyWorkId, default));
        Assert.Equal(0, preEffectCustody.EffectCalls);
        Assert.Equal(
            ("processing", "newer-holder-before-effect"),
            await ReadWorkLeaseAsync(dbFactory, preEffectLeaseLoss.CustodyWorkId));
        await ExpireLeaseAsync(dbFactory, preEffectLeaseLoss.CustodyWorkId, FixedUtcNow.AddMinutes(-1));
        await processor.ExecuteAsync(preEffectLeaseLoss.CustodyWorkId, default);
        Assert.Equal("confirmed", await ReadCaseCustodyStateAsync(scope.ServiceProvider, preEffectLeaseLoss.CaseId));

        // Lease loss after the remote effect likewise cannot persist stale
        // success or failure. Poison reconciliation makes it a human decision;
        // the staff retry then verifies/reuses the already-created custody.
        var postEffectLeaseLoss = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var postEffectCustody = new StealLeaseAfterEffectCustody(
            scope.ServiceProvider.GetRequiredService<ICaseCustody>(),
            dbFactory,
            postEffectLeaseLoss.CustodyWorkId,
            "newer-holder-after-effect",
            FixedUtcNow.AddMinutes(5));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => new EfQueuedCustodyProcessor(
            dbFactory,
            normalStore,
            postEffectCustody,
            new MutableTimeProvider(FixedUtcNow)).ExecuteAsync(postEffectLeaseLoss.CustodyWorkId, default));
        Assert.True(postEffectCustody.EffectCalls > 0);
        Assert.Equal(
            ("processing", "newer-holder-after-effect"),
            await ReadWorkLeaseAsync(dbFactory, postEffectLeaseLoss.CustodyWorkId));
        Assert.Equal("pending", await ReadCaseCustodyStateAsync(scope.ServiceProvider, postEffectLeaseLoss.CaseId));
        await new ReconcilePoisonedExternalWork(normalStore, new MutableTimeProvider(FixedUtcNow.AddMinutes(6)))
            .ExecuteAsync(postEffectLeaseLoss.CustodyWorkId, default);
        Assert.Equal("failed", await ReadExternalWorkStateAsync(scope.ServiceProvider, postEffectLeaseLoss.CustodyWorkId));
        await RetryFailedCustodyAsync(scope.ServiceProvider, postEffectLeaseLoss, "custody-recovery-after-lease-loss");
        await processor.ExecuteAsync(postEffectLeaseLoss.CustodyWorkId, default);
        Assert.Equal("confirmed", await ReadCaseCustodyStateAsync(scope.ServiceProvider, postEffectLeaseLoss.CaseId));

        // Expiry, without token replacement, is equally authoritative. Expiry
        // before the first effect makes zero adapter calls; expiry after the
        // source effect prevents both stale completion and stale failure.
        var expiredBeforeEffect = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var expireOnCheck = new ExpireLeaseOnCheckStore(normalStore, dbFactory, FixedUtcNow.AddSeconds(-1));
        var noEffectCustody = new CountingCustody(
            scope.ServiceProvider.GetRequiredService<ICaseCustody>());
        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() =>
            new EfQueuedCustodyProcessor(
                dbFactory,
                expireOnCheck,
                noEffectCustody,
                new MutableTimeProvider(FixedUtcNow))
            .ExecuteAsync(expiredBeforeEffect.CustodyWorkId, default));
        Assert.Equal(0, noEffectCustody.EffectCalls);
        Assert.Equal("processing", await ReadExternalWorkStateAsync(
            scope.ServiceProvider, expiredBeforeEffect.CustodyWorkId));

        var expiredBeforeCompletion = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var expiresAfterSource = new ExpireLeaseAfterEffectCustody(
            scope.ServiceProvider.GetRequiredService<ICaseCustody>(),
            dbFactory,
            expiredBeforeCompletion.CustodyWorkId,
            FixedUtcNow.AddSeconds(-1));
        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() =>
            new EfQueuedCustodyProcessor(
                dbFactory,
                normalStore,
                expiresAfterSource,
                new MutableTimeProvider(FixedUtcNow))
            .ExecuteAsync(expiredBeforeCompletion.CustodyWorkId, default));
        Assert.True(expiresAfterSource.EffectCalls > 0);
        Assert.Equal("processing", await ReadExternalWorkStateAsync(
            scope.ServiceProvider, expiredBeforeCompletion.CustodyWorkId));
        Assert.Equal("pending", await ReadCaseCustodyStateAsync(
            scope.ServiceProvider, expiredBeforeCompletion.CaseId));
        Assert.Equal(0, await CountCaseHistoryAsync(
            scope.ServiceProvider, expiredBeforeCompletion.CaseId, "custody_failed"));
    }

    [Fact]
    public async Task ReasonedRetryReplayConflictConcurrencyAndSecondFailureHaveExactCounts()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var workStore = scope.ServiceProvider.GetRequiredService<IExternalWorkStore>();
        await new ReconcilePoisonedExternalWork(workStore, new MutableTimeProvider(FixedUtcNow))
            .ExecuteAsync(accepted.CustodyWorkId, default);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var workflows = scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>();
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var retry = scope.ServiceProvider.GetRequiredService<IRetryCaseCustody>();
        var failed = Assert.IsType<CaseWorkflowRecord>(await workflows.GetAsync(accepted.CaseId, default));
        var lease = await leases.ClaimAsync(
            new(accepted.CaseId, failed.Version, actor, "custody-retry-lease-1"), default);
        var command = new RetryCaseCustodyRequest(
            accepted.CaseId,
            lease.Version,
            actor,
            "custody-retry-command-1",
            "Staff reviewed the provider failure and approved one retry.",
            lease.Token,
            CustodyTargetKind.CaseSource);

        var first = await retry.ExecuteAsync(command, default);
        var replay = await retry.ExecuteAsync(command, default);
        var conflict = await retry.ExecuteAsync(command with { Reason = "A changed reason must conflict." }, default);

        Assert.Equal(RetryCaseCustodyOutcome.Pending, first.Outcome);
        Assert.Equal(RetryCaseCustodyOutcome.Replay, replay.Outcome);
        Assert.Equal(RetryCaseCustodyOutcome.Conflict, conflict.Outcome);
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var failingProcessor = new EfQueuedCustodyProcessor(
            contextFactory,
            workStore,
            new AlwaysFailingCustody(),
            new MutableTimeProvider(FixedUtcNow.AddMinutes(1)));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            failingProcessor.ExecuteAsync(accepted.CustodyWorkId, default));

        var failedAgain = Assert.IsType<CaseWorkflowRecord>(await workflows.GetAsync(accepted.CaseId, default));
        var secondLease = await leases.ClaimAsync(
            new(accepted.CaseId, failedAgain.Version, actor, "custody-retry-lease-2"), default);
        var contenders = await Task.WhenAll(
            retry.ExecuteAsync(new(
                accepted.CaseId, secondLease.Version, actor, "custody-retry-command-2a",
                "Second reviewed recovery attempt A.", secondLease.Token, CustodyTargetKind.CaseSource)),
            retry.ExecuteAsync(new(
                accepted.CaseId, secondLease.Version, actor, "custody-retry-command-2b",
                "Second reviewed recovery attempt B.", secondLease.Token, CustodyTargetKind.CaseSource)));

        Assert.Single(contenders, result => result.Outcome == RetryCaseCustodyOutcome.Pending);
        Assert.Single(contenders, result => result.Outcome == RetryCaseCustodyOutcome.Conflict);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            failingProcessor.ExecuteAsync(accepted.CustodyWorkId, default));
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        Assert.Equal(2, await context.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == accepted.CaseId && item.EventType == "custody_retry_requested"));
        var work = await context.ExternalWorkItems.SingleAsync(item => item.Id == accepted.CustodyWorkId);
        Assert.Equal("failed", work.State);
        Assert.Equal(2, work.AttemptCount);
    }

    /// <summary>
    /// An accepted instruction's attachments land beside the retained
    /// source as their own custody files, and no binding JSON accompanies them.
    /// </summary>
    [Fact]
    public async Task AcceptedCaseRetainsInstructionAttachmentsBesideTheSource()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var attachmentBytes = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"Attachment Test {fixtureId}", claimNumber: $"ATT-{fixtureId}");
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "Please see the attached instruction.",
        };
        builder.Attachments.Add(
            "estimate.pdf", attachmentBytes, MimeKit.ContentType.Parse("application/pdf"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);
        var content = output.ToArray();

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"custody-attachment-{fixtureId}.eml",
                "message/rfc822",
                content,
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"custody-attachment:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Contains(receipt.Assets ?? [], asset => asset.Kind == IntakeAssetKind.Attachment);
        var outcome = await AcceptAsync(services, receipt.Id);

        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));
        var attachmentHash = Convert.ToHexString(SHA256.HashData(attachmentBytes)).ToLowerInvariant();
        var attachmentPath = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            outcome.Identity.CaseId.ToString("N"),
            "documents",
            receipt.Id.ToString("N"),
            "attachments",
            $"002-{attachmentHash}",
            "content");
        Assert.Equal(attachmentBytes, await File.ReadAllBytesAsync(attachmentPath));
    }

    /// <summary>
    /// The production shape is more than one attachment. QDOS26009
    /// arrived with two PDFs and failed custody with an unclassified exception
    /// after its files had already reached Box, so the fault is in the records
    /// written inside the completing transaction rather than in the upload.
    /// The single-attachment test above could not see it.
    /// </summary>
    [Fact]
    public async Task AcceptedCaseRecordsEveryAttachmentWhenMoreThanOneArrives()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var first = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"Two Attachments {fixtureId}", claimNumber: $"ATT2-{fixtureId}");
        var second = "%PDF-1.4 synthetic bodyshop report"u8.ToArray();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "Please see the attached instruction.",
        };
        builder.Attachments.Add(
            "43127_1_LtrtoAuditEngin.pdf", first, MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(
            "Bodyshopreport236502-V1.pdf", second, MimeKit.ContentType.Parse("application/pdf"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"custody-two-attachments-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"custody-two:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var outcome = await AcceptAsync(services, receipt.Id);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));
    }

    /// <summary>
    /// The operator's own export of a case, end to end — a real
    /// instruction accepted through the pipeline, its custody completed, then
    /// the archive built and opened. The Core tests cover the field mapping;
    /// this is the only thing that proves an archive comes out at all, which
    /// is what the operator asked for and what never worked.
    /// </summary>
    [Trait("Category", "Corpus")]
    [QdosMappingCustodyFact]
    public async Task ACaseExportsInEveryStateWithoutChangingIt()
    {
        using var factory = new IntakeWebApplicationFactory();
        var host = factory;
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var originalPath = Path.Combine(
            QdosCorpus.Root,
            "qdosmapping",
            "(EREF10) RTA on 14_08_2026  Mr Paul Larcombe (Our Ref AMA_47857_1, Vehicle PG18 BTY).eml");
        var originalBytes = await File.ReadAllBytesAsync(originalPath);
        Assert.Equal(
            "3063FF9ECB31878F582FB439047D999A41A7C6FE5B978CFBEE5C7E7F277553B4",
            Convert.ToHexString(SHA256.HashData(originalBytes)));
        using var originalStream = new MemoryStream(originalBytes);
        using var message = await MimeKit.MimeMessage.LoadAsync(originalStream);
        var mixed = Assert.IsType<MimeKit.Multipart>(message.Body);

        // Derived export probe: retain the genuine envelope, body and instruction,
        // adding only this fixture's existing two image assets in memory.
        var builder = new MimeKit.BodyBuilder();
        var first = SyntheticJpeg();
        var second = SyntheticJpeg(shade: 90);
        builder.Attachments.Add(
            "1_CLVoffside-V1.jpg", first, MimeKit.ContentType.Parse("image/jpeg"));
        builder.Attachments.Add(
            "2_CLVnearside-V1.jpg", second, MimeKit.ContentType.Parse("image/jpeg"));
        foreach (var attachment in builder.Attachments)
        {
            mixed.Add(attachment);
        }
        using var output = new MemoryStream();
        message.WriteTo(output);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"case-export-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"case-export:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.True(
            receipt.Decision == IntakeDecision.CaseCreated,
            $"Expected CaseCreated, got {receipt.Decision}: {receipt.DecisionReason}; "
            + $"route={receipt.MailRouteDecision?.Reason}; profile={receipt.ExtractionPolicyKey}; "
            + $"classification={receipt.MailClassificationDecision?.CaseType}.");
        Assert.Equal(QdosInstructionExtractionPolicy.Key, receipt.ExtractionPolicyKey);
        Assert.Equal(CaseType.InspectionAndAudit, receipt.MailClassificationDecision?.CaseType);
        Assert.Equal("AMA/47857/1", receipt.InstructionDraft?.ClaimNumber);
        var outcome = await AcceptAsync(services, receipt.Id, caseType: CaseType.InspectionAndAudit);
        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        // The local profile's document content store resolves by case reference
        // and version id, while intake's local custody adapter writes its own
        // layout — so an intake-retained document has records here but no
        // readable content. Production does not have that gap:
        // BoxDocumentContentStore overrides OpenReadVersionAsync to resolve the
        // full occurrence address, which is where Box already holds the file
        // custody uploaded. Putting the bytes where this store expects them is
        // the local stand-in for that, and is the only way to exercise the
        // export end to end off Box. The gap itself is a known Box-bound one.
        await using (var seed = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var contentStore = services.GetRequiredService<IDocumentContentStore>();
            var images = await (
                    from occurrence in seed.Set<DocumentOccurrenceEntity>().AsNoTracking()
                    join version in seed.Set<DocumentVersionEntity>().AsNoTracking()
                        on occurrence.VersionId equals version.Id
                    where occurrence.CaseId == outcome.Identity.CaseId
                          && occurrence.SemanticRole == DocumentSemanticRole.Image
                    select new { version.Id, version.FileName, version.Sha256 })
                .ToArrayAsync();
            Assert.Equal(2, images.Length);
            foreach (var image in images)
            {
                var bytes = image.FileName.StartsWith("1_", StringComparison.Ordinal)
                    ? first
                    : second;
                await contentStore.StoreAsync(
                    outcome.Identity.CaseId,
                    outcome.Identity.Reference,
                    image.Id,
                    bytes,
                    image.Sha256,
                    CancellationToken.None);
            }
        }

        var exporter = services.GetRequiredService<IExportCaseBundle>();
        var beforeExport = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;

        // No Sign-off Engineer is configured and no Engineer is assigned: the
        // export needs neither, and it changes no case state or version.
        var firstActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        const string firstOperationKey = "11111111111111111111111111111111";
        var export = await exporter.ExecuteAsync(
            new(outcome.Identity.CaseId, firstActor, firstOperationKey),
            CancellationToken.None);

        Assert.NotNull(export);
        Assert.Equal(
            beforeExport,
            await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(outcome.Identity.CaseId, CancellationToken.None));
        Assert.Empty(export!.BlockingReasons);
        var bundle = Assert.IsType<CaseExportBundle>(export.Bundle);
        var reference = outcome.Identity.Reference;
        Assert.Equal($"{reference}.zip", bundle.FileName);

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        // The shape the operator asked for: a zip of the images and a JSON,
        // and nothing else -- no manifest.sha256, no
        // provenance.json, neither of which was ever an operator requirement.
        Assert.Contains($"{reference}.json", entries);
        Assert.Equal(2, entries.Count(name => name.StartsWith("Images/", StringComparison.Ordinal)));
        Assert.Equal(3, entries.Length);
        Assert.Contains(entries, name => name.EndsWith("1_CLVoffside-V1.jpg", StringComparison.Ordinal));
        // The instruction PDF and the .eml are not photographs and stay out.
        Assert.DoesNotContain(entries, name => name.EndsWith(".pdf", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, name => name.EndsWith(".eml", StringComparison.Ordinal));

        using var json = JsonDocument.Parse(bundle.JsonContent);
        var fields = json.RootElement.EnumerateObject().ToArray();
        Assert.Equal(
            [
                "Work Provider", "VRM", "Vehicle Model", "Claimant Name", "Reference",
                "Incident Date", "Instruction Date", "Inspection Date", "Inspection Address",
                "Accident Circumstances", "VAT Status", "Mileage", "Mileage Unit"
            ],
            fields.Select(field => field.Name));
        // Every key is a string, present whether or not the case knows it.
        Assert.All(fields, field => Assert.Equal(JsonValueKind.String, field.Value.ValueKind));
        // Reference is the work provider's own reference -- the claim
        // number the letter carried -- not the Pegasus case reference. The
        // archive is still named by the case, asserted above.
        Assert.Equal("AMA/47857/1", json.RootElement.GetProperty("Reference").GetString());
        Assert.Equal(QdosPrincipal.Code, json.RootElement.GetProperty("Work Provider").GetString());
        // The Case's Received date is its instruction date (operator,
        // 24 September 2026), whatever the letter printed.
        Assert.Equal(
            Pegasus.Core.LondonCalendar.DateAt(receipt.ReceivedAtUtc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            json.RootElement.GetProperty("Instruction Date").GetString());
        // Operator direction (2026-08-22): an absent inspection date is today's.
        Assert.False(
            string.IsNullOrWhiteSpace(json.RootElement.GetProperty("Inspection Date").GetString()),
            "An inspection date must always be present, defaulting to the export date.");

        // The JSON is indented, which is the layout every known-good sample uses.
        Assert.StartsWith(
            "{\n  \"Work Provider\": ",
            Encoding.UTF8.GetString(bundle.JsonContent),
            StringComparison.Ordinal);

        await using (var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            Assert.Single(await context.ActionHistory
                .Where(item => item.AggregateType == "Case"
                    && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                    && item.EventKind == CaseExportPolicy.BundleExportedHistoryEventKind)
                .ToListAsync());
        }

        // A second export records a second history row and still changes nothing.
        var again = await exporter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "22222222222222222222222222222222"),
            CancellationToken.None);
        Assert.Equal(bundle.Sha256, again!.Bundle!.Sha256);
        Assert.Equal(
            beforeExport,
            await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(outcome.Identity.CaseId, CancellationToken.None));

        // An exact replay of the first export writes nothing new.
        var replay = await exporter.ExecuteAsync(
            new(outcome.Identity.CaseId, firstActor, firstOperationKey),
            CancellationToken.None);
        Assert.Equal(bundle.Sha256, replay!.Bundle!.Sha256);
        await using var replayCheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(2, await replayCheck.ActionHistory.CountAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == outcome.Identity.CaseId.ToString("D")
            && item.EventKind == CaseExportPolicy.BundleExportedHistoryEventKind));

        // Every lifecycle state exports, Not ready and closed ones included.
        foreach (var state in new[] { CaseLifecycleState.NotReady, CaseLifecycleState.PrincipalCancelled })
        {
            await replayCheck.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {state.ToString()} WHERE CaseId = {outcome.Identity.CaseId}");
            var exported = await exporter.ExecuteAsync(
                new(
                    outcome.Identity.CaseId,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    Guid.NewGuid().ToString("N")),
                CancellationToken.None);
            Assert.Equal(bundle.Sha256, exported!.Bundle!.Sha256);
        }
    }

    /// <summary>
    /// The production shape is a PDF instruction plus photographs.
    /// Every attachment used to be filed as an instruction document whatever
    /// its media type, so a case's own damage photographs were invisible to
    /// both the evidence gallery's image test and case export image selection — an
    /// export of QDOS26011 would have contained no photographs at all.
    /// </summary>
    [Fact]
    public async Task AnAcceptedInstructionFilesItsPhotographsAsImagesAndItsLetterAsAnInstruction()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var letter = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"Photograph Roles {fixtureId}", claimNumber: $"IMG-{fixtureId}");
        var photograph = SyntheticJpeg();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "Please see the attached instruction.",
        };
        builder.Attachments.Add(
            "53364_1_LtrtoEngineerIn.pdf", letter, MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(
            "1_CLVoffside-V1.jpg", photograph, MimeKit.ContentType.Parse("image/jpeg"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"custody-photograph-roles-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"custody-photograph:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var outcome = await AcceptAsync(services, receipt.Id);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);
        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));

        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var filed = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == outcome.Identity.CaseId
                select new { version.FileName, occurrence.SemanticRole, version.Sha256 })
            .ToDictionaryAsync(item => item.FileName);

        Assert.Equal(
            DocumentSemanticRole.Image,
            filed["1_CLVoffside-V1.jpg"].SemanticRole);
        Assert.Equal(
            DocumentSemanticRole.Instruction,
            filed["53364_1_LtrtoEngineerIn.pdf"].SemanticRole);
        // A document's hash is recorded in small letters, whoever filed it:
        // the report reads the photograph's hash exactly as it is stored.
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(photograph)),
            filed["1_CLVoffside-V1.jpg"].Sha256);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(letter)),
            filed["53364_1_LtrtoEngineerIn.pdf"].Sha256);
    }

    /// <summary>
    /// The attachments of one accepted instruction are filed three at a time,
    /// after the source, and each keeps the ordinal its place in the file-name
    /// order gave it, whichever upload finishes first. The probe holds the
    /// first three uploads until all three are in flight together, so only a
    /// concurrent loop gets past it. A throttled second attachment fails the
    /// work item, as it always did; a Case's failed custody stays failed until
    /// staff's reasoned retry re-arms it.
    /// </summary>
    [Fact]
    public async Task AnInstructionsAttachmentsAreFiledTogetherAndKeepTheirOrdinals()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var letter = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"Three Attachments {fixtureId}", claimNumber: $"ATT3-{fixtureId}");
        var report = "%PDF-1.4 synthetic bodyshop report"u8.ToArray();
        var photograph = SyntheticJpeg();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder { TextBody = "Please see the attached instruction." };
        builder.Attachments.Add(
            "43127_1_LtrtoAuditEngin.pdf", letter, MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(
            "Bodyshopreport236502-V1.pdf", report, MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(
            "1_CLVoffside-V1.jpg", photograph, MimeKit.ContentType.Parse("image/jpeg"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);
        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"custody-three-attachments-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"custody-three:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var outcome = await AcceptAsync(services, receipt.Id);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        EfQueuedCustodyProcessor ProcessorWith(
            ICaseCustody custody, IDocumentContentCachePublisher? publisher = null) => new(
            contextFactory,
            services.GetRequiredService<IExternalWorkStore>(),
            custody,
            services.GetRequiredService<TimeProvider>(),
            publisher);

        // A Box 429 on the second attachment fails the item, and the first
        // failure is what surfaces. A Case's custody work has no automatic
        // re-arm (only the image-case kinds do), so it stays failed until
        // staff retry it with a reason.
        var throttled = new CustodyBatchProbe(services.GetRequiredService<ICaseCustody>(), holdUntilConcurrent: 3)
        {
            Failure = ordinal => ordinal == 3 ? new Pegasus.Infrastructure.Custody.BoxThrottledException(TimeSpan.FromSeconds(1)) : null
        };
        await Assert.ThrowsAsync<Pegasus.Infrastructure.Custody.BoxThrottledException>(() =>
            ProcessorWith(throttled).ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None));
        Assert.Equal("failed", await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));
        await RetryFailedCustodyAsync(
            services,
            new(outcome.Identity.CaseId, outcome.CustodyWorkId, receipt.Id, output.ToArray()),
            "custody-three-attachments-retry");
        Assert.Equal("pending", await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));

        // The retry files all three at once, and each keeps its ordinal.
        var probe = new CustodyBatchProbe(services.GetRequiredService<ICaseCustody>(), holdUntilConcurrent: 3);
        var publisher = new RecordingCachePublisher();
        await ProcessorWith(probe, publisher).ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal("completed", await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));
        Assert.Equal(3, probe.MaxInFlight);
        await using var context = await contextFactory.CreateDbContextAsync();
        var ordinals = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == outcome.Identity.CaseId
                select new { version.FileName, occurrence.Ordinal })
            .ToDictionaryAsync(item => item.FileName, item => item.Ordinal);
        Assert.Equal(2, ordinals["1_CLVoffside-V1.jpg"]);
        Assert.Equal(3, ordinals["43127_1_LtrtoAuditEngin.pdf"]);
        Assert.Equal(4, ordinals["Bodyshopreport236502-V1.pdf"]);

        // Nobody held the bytes when the completion recorded these versions, so
        // once it committed each one was published to the read cache, once,
        // under its own identity, from the copy intake retained. The throttled
        // first attempt recorded and published nothing.
        var versions = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == outcome.Identity.CaseId
                select version)
            .ToListAsync();
        Assert.Equal(
            versions.Select(version => version.Id).Order(),
            publisher.Published.Select(copy => copy.Key.DocumentVersionId!.Value).Order());
        Assert.All(publisher.Published, copy =>
        {
            var version = versions.Single(item => item.Id == copy.Key.DocumentVersionId);
            Assert.Equal(version.Sha256, copy.Sha256);
            Assert.Equal(version.ContentLength, copy.ContentLength);
            Assert.False(string.IsNullOrWhiteSpace(copy.StorageKey));
        });
    }

    /// <summary>
    /// A JPEG large enough to clear the embedded-photograph byte floor and
    /// square enough to clear the banner shape test, so it is judged a
    /// photograph on its own merits rather than by its file name.
    /// </summary>
    private static byte[] SyntheticJpeg(byte shade = 112)
    {
        using var bitmap = new SkiaSharp.SKBitmap(709, 768);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(new SkiaSharp.SKColor(shade, shade, shade));
        using var encoded = SkiaSharp.SKImage.FromBitmap(bitmap)
            .Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    /// <summary>
    /// No custody test has ever run an audit case — every other
    /// fixture accepts with CaseType.Inspection — and both audits that reached
    /// production failed custody with an unclassified exception after their
    /// files had already reached Box. This is that shape.
    /// </summary>
    [Fact]
    public async Task AnAuditCaseCompletesCustody()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var instruction = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"Audit Custody {fixtureId}", claimNumber: $"AUD-{fixtureId}",
            notificationTitle: "AUDIT REPORT NOTIFICATION");
        var report = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            notificationTitle: "ORIGINAL BODYSHOP REPORT",
            additionalLines: ["Assessment outcome: Repairable"],
            addSignatureLines: false);
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS audit instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "Please see the attached audit instruction.",
        };
        builder.Attachments.Add(
            "Bodyshopreport236502-V1.pdf", report, MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(
            "AuditReportNotification236502-V1.pdf", instruction, MimeKit.ContentType.Parse("application/pdf"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"custody-audit-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"custody-audit:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);

        var evidence = Assert.IsType<StandaloneAuditEvidence>(
            await services.GetRequiredService<IStandaloneAuditEvidenceQueries>()
                .GetForReceiptAsync(receipt.Id, CancellationToken.None));
        var outcome = await AcceptAsync(
            services,
            receipt.Id,
            caseType: CaseType.Audit,
            expectedVersion: evidence.ReceiptVersion,
            standaloneAuditEvidenceId: evidence.Id);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));
    }

    [Fact]
    public async Task MailboxAuditWithoutOriginalReportAllocatesOneCaseWithNoAssessment()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await SeedPrincipalAsync(services, QdosPrincipal.Code);

        var fixtureId = Guid.NewGuid().ToString("N");
        var instruction = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"Reportless Audit {fixtureId}",
            claimNumber: $"AUD-NO-REPORT-{fixtureId}",
            notificationTitle: "AUDIT REPORT NOTIFICATION");
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(
            "Synthetic sender",
            "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress(
            "Pegasus Intake",
            "intake@example.test"));
        message.Subject = "QDOS audit instruction without original report";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "Please see the attached audit instruction."
        };
        builder.Attachments.Add(
            "AuditReportNotification.pdf",
            instruction,
            MimeKit.ContentType.Parse("application/pdf"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);
        var sourceIdentity = new IntakeSourceIdentity(
            IntakeSourceChannel.Mailbox,
            $"mailbox-audit-without-report:{Guid.NewGuid():N}");
        var received = await services.GetRequiredService<ReceiveIntake>()
            .ExecuteAsync(
                new(
                    $"mailbox-audit-{fixtureId}.eml",
                    "message/rfc822",
                    output.ToArray(),
                    FixedUtcNow,
                    "mailbox-test",
                    sourceIdentity),
                $"mailbox-audit-receive:{Guid.NewGuid():N}",
                CancellationToken.None);
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var claim = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            FixedUtcNow,
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await workStore.MarkDispatchedAsync(
            claim.Id,
            Assert.IsType<string>(claim.LeaseToken),
            FixedUtcNow,
            CancellationToken.None);
        await new ProcessQueuedIntake(
                workStore,
                services.GetRequiredService<IIntakeArtifactStore>(),
                services.GetRequiredService<ProcessIntake>(),
                services.GetRequiredService<IIntakeReceiptQueries>(),
                services.GetRequiredService<ICreateTriageFromIntake>(),
                services.GetRequiredService<ITriagePrincipalGate>(),
                services.GetRequiredService<IAutomaticCaseAssociationStore>(),
                services.GetRequiredService<IAllocateIntake>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IIntakeOcrOperationStore>())
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);

        var receipt = Assert.IsType<IntakeReceipt>(
            await services.GetRequiredService<IIntakeReceiptStore>()
                .FindBySourceIdentityAsync(sourceIdentity, CancellationToken.None));
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.NotNull(receipt.CurrentCaseId);
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var cases = await context.Set<CaseEntity>()
            .AsNoTracking()
            .Where(item => item.OriginIntakeReceiptId == receipt.Id)
            .Select(item => new
            {
                item.Reference,
                item.StandaloneAuditAssessment,
                item.StandaloneAuditEvidenceId
            })
            .ToArrayAsync();
        var allocated = Assert.Single(cases);
        Assert.StartsWith("a.", allocated.Reference, StringComparison.Ordinal);
        Assert.Null(allocated.StandaloneAuditAssessment);
        Assert.Null(allocated.StandaloneAuditEvidenceId);
        Assert.Empty(await context.UnidentifiedItems.AsNoTracking().ToArrayAsync());
    }

    /// <summary>
    /// The three things an operator reported about QDOS26009 that only appear
    /// once custody has actually completed, asserted on one case at the
    /// production shape: it reaches Review rather than sitting at Not ready
    /// unconfirmed, it carries one prefixed reference and no second audit
    /// identity, and its retained files are registered as case
    /// documents.
    ///
    /// Each was verifiable only by a live case until this existed, because the
    /// promotion, the identity and the document rows are all written inside
    /// CompleteCaseCustodyAsync's single transaction. Custody failing in
    /// production meant none of them ever ran.
    ///
    /// The completeness is the automatic shape — instruction and images
    /// complete, neither confirmed by staff — because that is what the
    /// pipeline's own allocation records, and demanding staff confirmation
    /// nobody would ever give is exactly what stranded QDOS26009.
    /// </summary>
    [Fact]
    public async Task AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var instruction = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            claimantName: $"End To End {fixtureId}", claimNumber: $"E2E-{fixtureId}",
            notificationTitle: "AUDIT REPORT NOTIFICATION");
        var report = IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
            notificationTitle: "ORIGINAL BODYSHOP REPORT",
            additionalLines: ["Assessment outcome: Repairable"],
            addSignatureLines: false);
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS audit instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "Please see the attached audit instruction.",
        };
        builder.Attachments.Add(
            "Bodyshopreport236503-V1.pdf", report, MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(
            "AuditReportNotification236503-V1.pdf", instruction, MimeKit.ContentType.Parse("application/pdf"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"e2e-audit-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"e2e-audit:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);

        var evidence = Assert.IsType<StandaloneAuditEvidence>(
            await services.GetRequiredService<IStandaloneAuditEvidenceQueries>()
                .GetForReceiptAsync(receipt.Id, CancellationToken.None));
        var outcome = await AcceptAsync(
            services,
            receipt.Id,
            completeness: new(true, true),
            caseType: CaseType.Audit,
            expectedVersion: evidence.ReceiptVersion,
            standaloneAuditEvidenceId: evidence.Id);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));

        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

        // The case moves off Not ready without staff confirmation
        // the automatic route was never going to receive.
        var state = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .Select(item => item.State)
            .SingleAsync();
        Assert.Equal(nameof(CaseLifecycleState.Review), state);

        // One identity. The reference itself carries the audit
        // prefix and nothing allocates a second one beside it.
        var identity = await context.Set<CaseEntity>()
            .AsNoTracking()
            .Where(item => item.Id == outcome.Identity.CaseId)
            .Select(item => new { item.Reference, item.AuditReference })
            .SingleAsync();
        Assert.StartsWith("a.", identity.Reference, StringComparison.Ordinal);
        Assert.Null(identity.AuditReference);

        // The retained files are case documents, not just bytes in
        // custody storage, so the Evidence tab can serve them.
        var documents = await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .CountAsync();
        Assert.True(
            documents > 0,
            $"Custody completed but registered {documents} case documents.");

        // The report the intake identified is filed as the Audit report, not
        // as a second instruction document: nothing recognises it later,
        // because a Case that holds its report does not await one.
        var filed = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == outcome.Identity.CaseId
                select new { version.FileName, occurrence.SemanticRole })
            .ToDictionaryAsync(item => item.FileName, item => item.SemanticRole);
        Assert.Equal(DocumentSemanticRole.AuditReport, filed["Bodyshopreport236503-V1.pdf"]);
        Assert.Equal(DocumentSemanticRole.Instruction, filed["AuditReportNotification236503-V1.pdf"]);
        Assert.Equal(DocumentSemanticRole.OriginalSource, filed[$"e2e-audit-{fixtureId}.eml"]);
    }

    /// <summary>
    /// An instruction's evidence photographs — embedded in its PDF
    /// documents — land beside the source as their own custody files after
    /// the attachments, while letterhead art stays out. Runs against the
    /// operator-supplied mapping corpus (local, git-ignored).
    /// </summary>
    [Trait("Category", "Corpus")]
    [QdosMappingCustodyFact]
    public async Task AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource()
    {
        var path = Path.Combine(
            QdosCorpus.Root,
            "qdosmapping",
            "(EREF9) RTA on 11_08_2026  Mr Tomasz Mydlowski (Our Ref AKH_ND_47630_1).eml");
        Assert.True(File.Exists(path), "The mapping corpus lost its EREF9 email.");

        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = await File.ReadAllBytesAsync(path);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                Path.GetFileName(path),
                "message/rfc822",
                content,
                FixedUtcNow,
                "custody-photo-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"custody-photos:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var assets = receipt.Assets ?? [];
        var attachments = assets
            .Where(asset => asset.Kind == IntakeAssetKind.Attachment)
            .OrderBy(asset => asset.FileName, StringComparer.Ordinal)
            .ThenBy(asset => asset.Id)
            .ToArray();
        var photographs = InstructionEvidenceImages.Select(assets)
            .Where(asset => asset.Kind == IntakeAssetKind.EmbeddedImage)
            .ToArray();
        var letterhead = assets.Where(asset =>
                asset.Kind == IntakeAssetKind.EmbeddedImage
                && asset.ContentLength < InstructionEvidenceImages.EmbeddedPhotographMinimumBytes)
            .ToArray();
        Assert.True(photographs.Length >= 5, $"Only {photographs.Length} photographs selected.");
        Assert.NotEmpty(letterhead);

        var current = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receipt.Id, CancellationToken.None);
        var outcome = await AcceptAsync(
            services,
            receipt.Id,
            expectedVersion: current!.Version);
        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);
        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));

        var attachmentsDirectory = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            outcome.Identity.CaseId.ToString("N"),
            "documents",
            receipt.Id.ToString("N"),
            "attachments");
        for (var index = 0; index < photographs.Length; index++)
        {
            var expected = Path.Combine(
                attachmentsDirectory,
                $"{attachments.Length + index + 2:D3}-{photographs[index].ContentHash.ToLowerInvariant()}",
                "content");
            Assert.True(File.Exists(expected), $"Missing photograph file {expected}.");
        }

        var retainedHashes = Directory.EnumerateDirectories(attachmentsDirectory)
            .Select(directory => Path.GetFileName(directory)!.Split('-', 2)[1])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(
            letterhead,
            art => Assert.DoesNotContain(art.ContentHash, retainedHashes, StringComparer.OrdinalIgnoreCase));

        // The evidence gallery's download path serves the same verified bytes.
        var download = await services.GetRequiredService<IDownloadIntakeAsset>().ExecuteAsync(
            new DownloadIntakeAssetQuery(
                receipt.Id,
                photographs[0].Id,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer])),
            CancellationToken.None);
        Assert.NotNull(download);
        Assert.Equal(photographs[0].ContentLength, download!.ContentLength);
        Assert.StartsWith("image/", download.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<AcceptedSource> AcceptDirectSourceAsync(IServiceProvider services)
    {
        var source = CreateSource();
        var receipt = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(source.Source, CancellationToken.None);
        Assert.True(
            receipt.Decision == IntakeDecision.CaseCreated,
            $"decision={receipt.Decision}; reason={receipt.DecisionReason}; route={receipt.MailRouteDecision?.Disposition}/{receipt.MailRouteDecision?.SelectedRoute?.PrincipalCode}; sender={receipt.MailRouteDecision?.EffectiveSender?.Address}");
        var outcome = await AcceptAsync(services, receipt.Id);
        return new(
            outcome.Identity.CaseId,
            outcome.CustodyWorkId,
            receipt.Id,
            source.Content);
    }

    private static async Task<AcceptedSource> AcceptQueuedSourceAsync(IServiceProvider services)
    {
        var source = CreateSource();
        var received = await services.GetRequiredService<ReceiveIntake>()
            .ExecuteAsync(
                source.Source,
                $"intake-receive:{Guid.NewGuid():N}",
                CancellationToken.None);
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var dispatchClaim = Assert.IsType<IntakeWorkItem>(
            await store.ClaimDispatchAsync(
                FixedUtcNow,
                TimeSpan.FromMinutes(1),
                CancellationToken.None));
        await store.MarkDispatchedAsync(
            dispatchClaim.Id,
            Assert.IsType<string>(dispatchClaim.LeaseToken),
            FixedUtcNow,
            CancellationToken.None);
        await new ProcessQueuedIntake(
                store,
                services.GetRequiredService<IIntakeArtifactStore>(),
                services.GetRequiredService<ProcessIntake>(),
                services.GetRequiredService<IIntakeReceiptQueries>(),
                services.GetRequiredService<ICreateTriageFromIntake>(),
                services.GetRequiredService<ITriagePrincipalGate>(),
                services.GetRequiredService<IAutomaticCaseAssociationStore>(),
                services.GetRequiredService<IAllocateIntake>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IIntakeOcrOperationStore>())
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);
        var receipt = Assert.IsType<IntakeReceipt>(
            await services.GetRequiredService<IIntakeReceiptStore>()
                .FindBySourceIdentityAsync(source.Source.SourceIdentity, CancellationToken.None));
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);

        await SeedPrincipalAsync(services, QdosPrincipal.Code);
        var accepted = await services.GetRequiredService<IAllocateIntake>()
            .AttemptStaffCreateAsync(
                new(
                    receipt.Id,
                    receipt.Version,
                    ActionActor.Staff(
                        DevelopmentOfflineIdentity.AdministratorId,
                        [StaffRole.Administrator]),
                    $"custody-accept:{Guid.NewGuid():N}",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(InstructionComplete: true, ImagesComplete: false),
                    null,
                    receipt.InstructionDraft?.InspectionDate),
                CancellationToken.None);
        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, accepted.State.Status);

        var allocation = Assert.IsType<IntakeAllocationState>(
            (await services.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receipt.Id, CancellationToken.None))!.AllocationState);
        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, allocation.Status);

        await using var db = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var link = await db.CaseIntakeLinks.SingleAsync(
            item => item.IntakeReceiptId == receipt.Id,
            CancellationToken.None);
        Assert.Equal(allocation.CaseId, link.CaseId);
        return new(link.CaseId, link.CustodyWorkId, receipt.Id, source.Content);
    }

    private static async Task<IntakeEvaluationRevision> DrainStagedAsync(
        IServiceProvider services,
        Guid stagedReceiptId,
        CancellationToken cancellationToken)
    {
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var processor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services);
        var dispatcher = new DispatchPendingIntakeWork(
            workStore,
            new ImmediateIntakeWorkEnqueuer(processor),
            services.GetRequiredService<TimeProvider>());
        Assert.Equal(1, await dispatcher.ExecuteAsync(1, cancellationToken));
        return Assert.IsType<IntakeEvaluationRevision>(
            await workStore.GetCompletedEvaluationAsync(stagedReceiptId, cancellationToken));
    }

    private sealed class ImmediateIntakeWorkEnqueuer(ProcessQueuedIntake processor)
        : IIntakeWorkEnqueuer
    {
        public Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken) =>
            processor.ExecuteAsync(stagedReceiptId, cancellationToken);
    }

    private static async Task<CaseAcceptanceOutcome> AcceptAsync(
        IServiceProvider services,
        Guid receiptId,
        CaseCompleteness? completeness = null,
        long expectedVersion = 0,
        CaseType caseType = CaseType.Inspection,
        Guid? standaloneAuditEvidenceId = null)
    {
        const string principalCode = QdosPrincipal.Code;
        await SeedPrincipalAsync(services, principalCode);
        // Acceptance must carry the reviewed inspection date, which the draft
        // defaults to the received date when the letter states none.
        var reviewed = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        return await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receiptId,
                    expectedVersion,
                    // A manual-upload source has no accepted route or credential
                    // binding, so the Principal is the accepting person's decision.
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    $"case-accept:{Guid.NewGuid():N}",
                    caseType,
                    principalCode,
                    completeness ?? new(true, true),
                    standaloneAuditEvidenceId,
                    AcceptedInspectionDeadline: reviewed?.InstructionDraft?.InspectionDate),
                CancellationToken.None);
    }

    private static SourceFixture CreateSource()
    {
        var fixtureId = Guid.NewGuid().ToString("N");
        var email = IntakeTestEvidence.CreateEmail(
            $"custody-{fixtureId}.eml",
            "Please see the attached instruction.",
            attachments:
            [
                ("instruction.pdf", "application/pdf",
                    IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
                        claimantName: $"Custody Test {fixtureId}", claimNumber: $"CUS-{fixtureId}"))
            ]);
        var identity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            $"custody-source:{Guid.NewGuid():N}");
        return new(
            new(
                email.FileName,
                email.MediaType,
                email.Content,
                FixedUtcNow,
                "custody-test",
                identity),
            email.Content);
    }

    private static SourceFixture CreatePreCaseReevaluationSource()
    {
        var fixtureId = Guid.NewGuid().ToString("N");
        var email = IntakeTestEvidence.CreateEmail(
            $"custody-reevaluation-{fixtureId}.eml",
            "Please see the retained source awaiting work-type classification.",
            attachments:
            [
                ("source-awaiting-classification.pdf", "application/pdf",
                    IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
                        claimantName: $"Reevaluation Source {fixtureId}",
                        claimNumber: $"REV-{fixtureId}",
                        notificationTitle: "RE-EVALUATION SOURCE — WORK TYPE NOT YET CLASSIFIED"))
            ]);
        var identity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            $"custody-reevaluation-source:{Guid.NewGuid():N}");
        return new(
            new(
                email.FileName,
                email.MediaType,
                email.Content,
                FixedUtcNow,
                "custody-test",
                identity),
            email.Content);
    }

    private static async Task SeedPrincipalAsync(
        IServiceProvider services,
        string principalCode)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        const string organizationName = "Custody test organization";
        const string organizationRole = "principal";
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        if (await context.Principals
            .AnyAsync(
                value => value.Code == principalCode && value.IsActive,
                CancellationToken.None))
        {
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {organizationName}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO OrganizationRoles (OrganizationId, Role) VALUES ({organizationId}, {organizationRole})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})
            """);
        await transaction.CommitAsync();
    }

    private static async Task<string> ReadExternalWorkStateAsync(
        IServiceProvider services,
        Guid workItemId)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Database.SqlQuery<string>(
                $"SELECT \"State\" AS \"Value\" FROM \"ExternalWorkItems\" WHERE \"Id\" = {workItemId}")
            .SingleAsync();
    }

    private static async Task<string> ReadCaseCustodyStateAsync(
        IServiceProvider services,
        Guid caseId)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Database.SqlQuery<string>(
                $"SELECT \"CustodyState\" AS \"Value\" FROM \"Cases\" WHERE \"Id\" = {caseId}")
            .SingleAsync();
    }

    private static async Task<int> CountCaseHistoryAsync(
        IServiceProvider services,
        Guid caseId,
        string eventType)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS \"Value\" FROM \"CaseHistory\" WHERE \"CaseId\" = {caseId} AND \"EventType\" = {eventType}")
            .SingleAsync();
    }

    private sealed class RecordingExternalWorkQueue : IExternalWorkEnqueuer
    {
        public List<Guid> WorkItemIds { get; } = [];

        public Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            WorkItemIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan interval) => currentUtcNow += interval;
    }

    private static async Task RetryFailedCustodyAsync(
        IServiceProvider services,
        AcceptedSource accepted,
        string operationKey)
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var workflow = Assert.IsType<CaseWorkflowRecord>(await services
            .GetRequiredService<ICaseWorkflowQueries>().GetAsync(accepted.CaseId, default));
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(accepted.CaseId, workflow.Version, actor, $"{operationKey}:lease"), default);
        var result = await services.GetRequiredService<IRetryCaseCustody>().ExecuteAsync(
            new(
                accepted.CaseId,
                lease.Version,
                actor,
                operationKey,
                "Staff reviewed the uncertain custody effect and approved reconciliation.",
                lease.Token,
                CustodyTargetKind.CaseSource),
            default);
        Assert.Equal(RetryCaseCustodyOutcome.Pending, result.Outcome);
    }

    private static async Task<(string State, string? LeaseToken)> ReadWorkLeaseAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid workId)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.ExternalWorkItems.AsNoTracking()
            .Where(item => item.Id == workId)
            .Select(item => new ValueTuple<string, string?>(item.State, item.LeaseToken))
            .SingleAsync();
    }

    private static async Task ExpireLeaseAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid workId,
        DateTimeOffset expiresAtUtc)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.ExternalWorkItems.Where(item => item.Id == workId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LeaseExpiresAtUtc, expiresAtUtc));
    }

    private sealed class OptionsDbContextFactory(DbContextOptions<PegasusDbContext> options)
        : IDbContextFactory<PegasusDbContext>
    {
        public PegasusDbContext CreateDbContext() => new(options);
    }

    private sealed class FailNextCustodyCompletionInterceptor : SaveChangesInterceptor
    {
        private int failNext;

        public void FailNextCompletion() => Interlocked.Exchange(ref failNext, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref failNext) == 1
                && eventData.Context is not null
                && eventData.Context.ChangeTracker.Entries<ExternalWorkItemEntity>()
                    .Any(entry => entry.State == EntityState.Modified
                        && string.Equals(entry.Entity.State, "completed", StringComparison.Ordinal))
                && Interlocked.Exchange(ref failNext, 0) == 1)
            {
                throw new DbUpdateException("Injected post-adapter custody completion failure.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class StealLeaseOnCheckStore(
        IExternalWorkStore inner,
        IDbContextFactory<PegasusDbContext> factory,
        string newerLeaseToken,
        DateTimeOffset newerLeaseExpiry) : IExternalWorkStore
    {
        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            inner.ClaimDispatchAsync(nowUtc, leaseDuration, cancellationToken);
        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(Guid workItemId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            inner.ClaimDispatchAsync(workItemId, nowUtc, leaseDuration, cancellationToken);

        public Task MarkDispatchedAsync(Guid workItemId, string leaseToken, DateTimeOffset dispatchedAtUtc, CancellationToken cancellationToken) =>
            inner.MarkDispatchedAsync(workItemId, leaseToken, dispatchedAtUtc, cancellationToken);

        public Task ReleaseDispatchAsync(Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) =>
            inner.ReleaseDispatchAsync(workItemId, leaseToken, dueAtUtc, cancellationToken);

        public Task MarkPoisonedAsync(Guid workItemId, DateTimeOffset failedAtUtc, CancellationToken cancellationToken) =>
            inner.MarkPoisonedAsync(workItemId, failedAtUtc, cancellationToken);

        public async Task<bool> HoldsProcessingLeaseAsync(Guid workItemId, string leaseToken, CancellationToken cancellationToken)
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await context.ExternalWorkItems.Where(item => item.Id == workItemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "processing")
                    .SetProperty(item => item.LeaseToken, newerLeaseToken)
                    .SetProperty(item => item.LeaseExpiresAtUtc, newerLeaseExpiry),
                    cancellationToken);
            return false;
        }

        public Task FailProcessingAsync(Guid workItemId, string leaseToken, DateTimeOffset failedAtUtc, string failureCode, string failureReason, CancellationToken cancellationToken) =>
            inner.FailProcessingAsync(workItemId, leaseToken, failedAtUtc, failureCode, failureReason, cancellationToken);
    }

    private sealed class ExpireLeaseOnCheckStore(
        IExternalWorkStore inner,
        IDbContextFactory<PegasusDbContext> factory,
        DateTimeOffset expiredAtUtc) : IExternalWorkStore
    {
        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            inner.ClaimDispatchAsync(nowUtc, leaseDuration, cancellationToken);
        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(Guid workItemId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            inner.ClaimDispatchAsync(workItemId, nowUtc, leaseDuration, cancellationToken);
        public Task MarkDispatchedAsync(Guid workItemId, string leaseToken, DateTimeOffset dispatchedAtUtc, CancellationToken cancellationToken) =>
            inner.MarkDispatchedAsync(workItemId, leaseToken, dispatchedAtUtc, cancellationToken);
        public Task ReleaseDispatchAsync(Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) =>
            inner.ReleaseDispatchAsync(workItemId, leaseToken, dueAtUtc, cancellationToken);
        public Task MarkPoisonedAsync(Guid workItemId, DateTimeOffset failedAtUtc, CancellationToken cancellationToken) =>
            inner.MarkPoisonedAsync(workItemId, failedAtUtc, cancellationToken);
        public async Task<bool> HoldsProcessingLeaseAsync(Guid workItemId, string leaseToken, CancellationToken cancellationToken)
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await context.ExternalWorkItems.Where(item => item.Id == workItemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAtUtc, expiredAtUtc),
                    cancellationToken);
            return await inner.HoldsProcessingLeaseAsync(workItemId, leaseToken, cancellationToken);
        }
        public Task FailProcessingAsync(Guid workItemId, string leaseToken, DateTimeOffset failedAtUtc, string failureCode, string failureReason, CancellationToken cancellationToken) =>
            inner.FailProcessingAsync(workItemId, leaseToken, failedAtUtc, failureCode, failureReason, cancellationToken);
    }

    private class CountingCustody(ICaseCustody inner) : ICaseCustody
    {
        public int EffectCalls { get; protected set; }

        public virtual async Task<CaseCustodyRoot> CreateCaseRootAsync(
            Guid caseId, string caseReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken)
        {
            EffectCalls++;
            return await inner.CreateCaseRootAsync(
                caseId, caseReference, creationOwnerToken, operationKey, cancellationToken);
        }

        public virtual async Task<CaseCustodyRoot> GetExistingCaseRootAsync(
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken)
        {
            EffectCalls++;
            return await inner.GetExistingCaseRootAsync(caseId, caseReference, cancellationToken);
        }

        public virtual async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root, IntakeSourceCustodyReference source, string operationKey,
            CancellationToken cancellationToken)
        {
            EffectCalls++;
            return await inner.RetainAcceptedIntakeSourceAsync(root, source, operationKey, cancellationToken);
        }

        public virtual async Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentAsync(
            CaseCustodyRoot root,
            IntakeSourceCustodyReference attachment,
            int ordinal,
            string operationKey,
            CancellationToken cancellationToken)
        {
            EffectCalls++;
            return await inner.RetainAcceptedIntakeAttachmentAsync(
                root, attachment, ordinal, operationKey, cancellationToken);
        }

        public virtual async Task<string> CreateAuditReferenceFolderAsync(
            CaseCustodyRoot root, string auditReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken)
        {
            EffectCalls++;
            return await inner.CreateAuditReferenceFolderAsync(
                root, auditReference, creationOwnerToken, operationKey, cancellationToken);
        }
    }

    private sealed class StealLeaseAfterEffectCustody(
        ICaseCustody inner,
        IDbContextFactory<PegasusDbContext> factory,
        Guid workId,
        string newerLeaseToken,
        DateTimeOffset newerLeaseExpiry) : CountingCustody(inner)
    {
        public override async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root,
            IntakeSourceCustodyReference source,
            string operationKey,
            CancellationToken cancellationToken)
        {
            var result = await base.RetainAcceptedIntakeSourceAsync(
                root, source, operationKey, cancellationToken);
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await context.ExternalWorkItems.Where(item => item.Id == workId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "processing")
                    .SetProperty(item => item.LeaseToken, newerLeaseToken)
                    .SetProperty(item => item.LeaseExpiresAtUtc, newerLeaseExpiry),
                    cancellationToken);
            return result;
        }
    }

    private sealed class ExpireLeaseAfterEffectCustody(
        ICaseCustody inner,
        IDbContextFactory<PegasusDbContext> factory,
        Guid workId,
        DateTimeOffset expiredAtUtc) : CountingCustody(inner)
    {
        public override async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root,
            IntakeSourceCustodyReference source,
            string operationKey,
            CancellationToken cancellationToken)
        {
            var result = await base.RetainAcceptedIntakeSourceAsync(
                root, source, operationKey, cancellationToken);
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await context.ExternalWorkItems.Where(item => item.Id == workId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAtUtc, expiredAtUtc),
                    cancellationToken);
            return result;
        }
    }

    private sealed class AlwaysFailingCustody : ICaseCustody
    {
        private static HttpRequestException Failure() => new("Fixture adapter failure.");

        public Task<CaseCustodyRoot> CreateCaseRootAsync(
            Guid caseId, string caseReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken) => throw Failure();

        public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken) => throw Failure();

        public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root, IntakeSourceCustodyReference source, string operationKey,
            CancellationToken cancellationToken) => throw Failure();

        public Task<string> CreateAuditReferenceFolderAsync(
            CaseCustodyRoot root, string auditReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken) => throw Failure();
    }

    private sealed record SourceFixture(IntakeSource Source, byte[] Content);

    private sealed record AcceptedSource(
        Guid CaseId,
        Guid CustodyWorkId,
        Guid ReceiptId,
        byte[] Content);
}

internal sealed class QdosMappingCustodyFactAttribute : FactAttribute
{
    public QdosMappingCustodyFactAttribute()
    {
        if (!Directory.Exists(Path.Combine(QdosCorpus.Root, "qdosmapping")))
        {
            Skip = "This machine's ignored local corpus has no qdosmapping folder; corpora differ per system.";
        }
    }
}
