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
using Pegasus.Core.Eva;
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

    [Fact]
    public async Task ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = CreateSource();
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
    }

    [Fact]
    public async Task ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = CreateSource();
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
        var failedWork = Assert.IsType<IntakeWorkItem>(await workStore.FindWorkItemAsync(
            received.StagedReceiptId, CancellationToken.None));
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
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<IAddCaseDocument>()
                .ExecuteAsync(
                    new(
                        accepted.CaseId,
                        "stale-editor.txt",
                        "text/plain",
                        "stale editor content"u8.ToArray(),
                        DocumentSemanticRole.Other,
                        DocumentSource.StaffUpload,
                        $"stale-editor:{Guid.NewGuid():N}",
                        editor,
                        $"stale-editor-add:{Guid.NewGuid():N}",
                        editorLease.Version,
                        editorLease.Token),
                    CancellationToken.None));

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

    [Fact]
    public async Task LogicallyRemovedVersionCannotBeDownloadedOrExported()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var content = "retained document"u8.ToArray();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var state = Assert.IsType<CaseDocumentState>(
            await scope.ServiceProvider.GetRequiredService<ICaseDocumentStateQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var addLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                state.CaseVersion,
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
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
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
        var exportOperationKey = $"document-export:{Guid.NewGuid():N}";

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


        var exportLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                checked(addLease.Version + 1),
                actor,
                $"document-export-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        await using (var export = await scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                         .ExecuteAsync(
                             new(
                                 accepted.CaseId,
                                 [new(added.Occurrence.Id, added.Version.Id)],
                                actor,
                                 exportOperationKey,
                                 1024 * 1024,
                                 exportLease.Version,
                                 exportLease.Token),
                             CancellationToken.None))
        {
            Assert.Equal(added.Version.Id, Assert.Single(export.Manifest).VersionId);
        }
        await using (var replay = await scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                         .ExecuteAsync(
                             new(
                                 accepted.CaseId,
                                 [new(added.Occurrence.Id, added.Version.Id)],
                                 actor,
                                 exportOperationKey,
                                 1024 * 1024,
                                 exportLease.Version,
                                 exportLease.Token),
                             CancellationToken.None))
        {
            Assert.Equal(added.Version.Id, Assert.Single(replay.Manifest).VersionId);
        }

        var removeLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                checked(exportLease.Version + 1),
                actor,
                $"document-remove-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var auditContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var auditContext = await auditContextFactory.CreateDbContextAsync())
        {
            var auditEntries = await auditContext.ActionHistory
                .Where(value => value.AggregateType == "case_document"
                    && (value.CorrelationId == downloadOperationKey
                        || value.CorrelationId == exportOperationKey))
                .ToArrayAsync();
            Assert.Equal(2, auditEntries.Length);
            Assert.All(auditEntries, entry =>
            {
                Assert.Equal(actor.SubjectId, entry.ActorSubjectId);
                Assert.False(string.IsNullOrWhiteSpace(entry.AfterJson));
            });
        }


        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                .ExecuteAsync(
                    new(
                        accepted.CaseId,
                        [new(added.Occurrence.Id, added.Version.Id)],
                        actor,
                        $"document-export-over-limit:{Guid.NewGuid():N}",
                        content.LongLength,
                        removeLease.Version,
                        removeLease.Token),
                    CancellationToken.None));

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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                .ExecuteAsync(
                    new(
                        accepted.CaseId,
                        [new(added.Occurrence.Id, added.Version.Id)],
                        actor,
                        $"document-export-removed:{Guid.NewGuid():N}",
                        1024 * 1024,
                        checked(removeLease.Version + 1),
                        removeLease.Token),
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

        timeProvider.Advance(TimeSpan.FromMinutes(6));

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            add.ExecuteAsync(Command(actor, lease.Token), CancellationToken.None));
        var unchanged = Assert.IsType<CaseWorkflowRecord>(
            await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        Assert.Equal(workflow.Version, unchanged.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestUploadPortsRemainRegisteredAndFailClosedWithoutAcceptedLimits(
        bool localCustodyConfigured)
    {
        var services = new ServiceCollection();
        services.AddPegasusInfrastructure(
            (_, options) => options.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=unused;Integrated Security=true"),
            localCustodyConfigured ? _ => Path.GetTempPath() : null);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            scope.ServiceProvider.GetRequiredService<ICreateRequestUploadLink>()
                .ExecuteAsync(
                    new(
                        Guid.NewGuid(),
                        ActionActor.SystemWorker("unauthorized-request-create"),
                        $"unauthorized-create:{Guid.NewGuid():N}",
                        0,
                        "lease"),
                    CancellationToken.None));

        await Assert.ThrowsAsync<DocumentRequestUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<ICreateRequestUploadLink>()
                .ExecuteAsync(
                    new(
                        Guid.NewGuid(),
                        actor,
                        $"unavailable-create:{Guid.NewGuid():N}",
                        0,
                        "lease"),
                    CancellationToken.None));
        await Assert.ThrowsAsync<DocumentRequestUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<IRevokeRequestUploadLink>()
                .ExecuteAsync(
                    new(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        actor,
                        "Unavailable.",
                        $"unavailable-revoke:{Guid.NewGuid():N}",
                        0,
                        0,
                        "lease"),
                    CancellationToken.None));
        Assert.Equal(
            RequestUploadDecision.Unavailable,
            (await scope.ServiceProvider.GetRequiredService<IUploadToRequest>()
                .ExecuteAsync(
                    new(
                        "invalid",
                        new(
                            "evidence.txt",
                            "text/plain",
                            "evidence"u8.ToArray(),
                            $"unavailable-upload:{Guid.NewGuid():N}"),
                        0),
                    CancellationToken.None)).Decision);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IGetRequestUpload>()
            .ExecuteAsync("invalid", CancellationToken.None));
    }

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    public async Task EveryTerminalCaseStateRejectsNewCustodyMutationsButPreservesExactReplay(
        CaseLifecycleState terminalState)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        // The accepted request upload below is a real submission, so this host
        // needs the custody adapter Stream A will register in production.
        using var factory = PublicUploadRetentionWebTests.WithRetention(baseFactory);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var caseId = accepted.CaseId;
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var queries = scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>();
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(caseId, CancellationToken.None));

        var requestLease = await leases.ClaimAsync(
            new(
                caseId,
                workflow.Version,
                actor,
                $"terminal-request-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var createRequest = new CreateRequestUploadLinkCommand(
            caseId,
            actor,
            $"terminal-request-create:{Guid.NewGuid():N}",
            requestLease.Version,
            requestLease.Token);
        var createUploadLink =
            scope.ServiceProvider.GetRequiredService<ICreateRequestUploadLink>();
        var requestLink = await createUploadLink.ExecuteAsync(
            createRequest,
            CancellationToken.None);

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

        var uploadCommand = new UploadToRequestCommand(
            requestLink.Secret!.Token,
            new(
                "request-evidence.txt",
                "text/plain",
                "request evidence"u8.ToArray(),
                $"terminal-request-file:{Guid.NewGuid():N}"),
            AttemptsInCurrentRateWindow: 0);
        var upload = scope.ServiceProvider.GetRequiredService<IUploadToRequest>();
        Assert.Equal(
            RequestUploadDecision.Accepted,
            (await upload.ExecuteAsync(uploadCommand, CancellationToken.None)).Decision);

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

        var requestReplay = await createUploadLink.ExecuteAsync(
            createRequest,
            CancellationToken.None);
        Assert.True(requestReplay.IsReplay);
        Assert.Null(requestReplay.Secret);
        Assert.Equal(requestLink.Link, requestReplay.Link);
        Assert.True((await addDocument.ExecuteAsync(
            addCommand,
            CancellationToken.None)).IsReplay);
        Assert.Equal(
            RequestUploadDecision.Replay,
            (await upload.ExecuteAsync(uploadCommand, CancellationToken.None)).Decision);

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
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            createUploadLink.ExecuteAsync(
                createRequest with
                {
                    OperationKey = $"terminal-request-new:{Guid.NewGuid():N}",
                    ExpectedCaseVersion = terminalLease.Version,
                    EditLeaseToken = terminalLease.Token
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            scope.ServiceProvider.GetRequiredService<IRevokeRequestUploadLink>()
                .ExecuteAsync(
                    new(
                        caseId,
                        requestLink.Link.Id,
                        actor,
                        "Terminal cases are read-only.",
                        $"terminal-request-revoke:{Guid.NewGuid():N}",
                        requestLink.Link.Version,
                        terminalLease.Version,
                        terminalLease.Token),
                    CancellationToken.None));
        Assert.Equal(
            RequestUploadDecision.Unavailable,
            (await upload.ExecuteAsync(
                uploadCommand with
                {
                    File = uploadCommand.File with
                    {
                        OperationKey = $"terminal-request-file-new:{Guid.NewGuid():N}"
                    }
                },
                CancellationToken.None)).Decision);
    }

    [Fact]
    public async Task RequestCreateAndRevokeRecordExactIdempotentStaffActionHistory()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [StaffRole.Engineer]);
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(accepted.CaseId, CancellationToken.None));
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var createLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                workflow.Version,
                actor,
                $"request-create-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var createOperationKey = $"request-create:{Guid.NewGuid():N}";
        var createRequest = new CreateRequestUploadLinkCommand(
            accepted.CaseId,
            actor,
            createOperationKey,
            createLease.Version,
            createLease.Token);
        var create = scope.ServiceProvider.GetRequiredService<ICreateRequestUploadLink>();
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            create.ExecuteAsync(
                createRequest with
                {
                    Actor = ActionActor.SystemWorker("custody-test"),
                    OperationKey = $"request-create-unauthorized:{Guid.NewGuid():N}"
                },
                CancellationToken.None));

        var created = await create.ExecuteAsync(createRequest, CancellationToken.None);
        var createReplay = await create.ExecuteAsync(createRequest, CancellationToken.None);

        Assert.False(created.IsReplay);
        Assert.NotNull(created.Secret);
        Assert.True(createReplay.IsReplay);
        Assert.Null(createReplay.Secret);
        Assert.Equal(created.Link, createReplay.Link);
        var changedActor = ActionActor.Staff(staffId, [StaffRole.Administrator]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            create.ExecuteAsync(
                createRequest with { Actor = changedActor },
                CancellationToken.None));

        var revokeLease = await leases.ClaimAsync(
            new(
                accepted.CaseId,
                checked(createLease.Version + 1),
                actor,
                $"request-revoke-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var revokeOperationKey = $"request-revoke:{Guid.NewGuid():N}";
        var revokeRequest = new RevokeRequestUploadLinkCommand(
            accepted.CaseId,
            created.Link.Id,
            actor,
            "The intended recipient no longer requires access.",
            revokeOperationKey,
            created.Link.Version,
            revokeLease.Version,
            revokeLease.Token);
        var revoke = scope.ServiceProvider.GetRequiredService<IRevokeRequestUploadLink>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            revoke.ExecuteAsync(
                revokeRequest with { CaseId = Guid.NewGuid() },
                CancellationToken.None));

        await revoke.ExecuteAsync(revokeRequest, CancellationToken.None);
        await revoke.ExecuteAsync(revokeRequest, CancellationToken.None);

        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var history = await context.ActionHistory
            .Where(value => value.AggregateType == "request_upload_link"
                && (value.CorrelationId == createOperationKey
                    || value.CorrelationId == revokeOperationKey))
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.All(history, entry =>
        {
            Assert.Equal(actor.Kind.ToString(), entry.ActorKind);
            Assert.Equal(actor.SubjectId, entry.ActorSubjectId);
            Assert.Equal("[\"Engineer\"]", entry.ActorRolesJson);
            Assert.Equal("Succeeded", entry.Outcome);
            Assert.False(string.IsNullOrWhiteSpace(entry.AfterJson));
        });
        var createHistory = Assert.Single(
            history,
            entry => entry.CorrelationId == createOperationKey);
        Assert.Equal("request_upload_created", createHistory.EventKind);
        Assert.Null(createHistory.BeforeJson);
        var revokeHistory = Assert.Single(
            history,
            entry => entry.CorrelationId == revokeOperationKey);
        Assert.Equal("request_upload_revoked", revokeHistory.EventKind);
        Assert.NotNull(revokeHistory.BeforeJson);
        Assert.Equal(revokeRequest.Reason, revokeHistory.Reason);
    }

    [Fact]
    public async Task ExportIsRefusedForACaseThatIsNotInReview()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();

        // The queued path allocates the case with nothing confirmed, so it
        // enters Not ready — which is exactly the stage the rule excludes.
        var accepted = await AcceptQueuedSourceAsync(scope.ServiceProvider);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await scope.ServiceProvider
            .GetRequiredService<IAcquireCaseEditLease>()
            .ExecuteAsync(
                new(accepted.CaseId, 0, actor, $"export-gate-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);

        // A greyed button is presentation. The rule is a precondition, so it
        // holds for every caller and not just the one that renders the button.
        await Assert.ThrowsAsync<CaseNotInReviewException>(() =>
            scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                .ExecuteAsync(
                    new(
                        accepted.CaseId,
                        [new(Guid.NewGuid(), Guid.NewGuid())],
                        actor,
                        $"export-gate:{Guid.NewGuid():N}",
                        1024 * 1024,
                        lease.Version,
                        lease.Token),
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
    /// DOCS-005: an accepted instruction's attachments land beside the retained
    /// source as their own custody files, and no binding JSON accompanies them.
    /// </summary>
    [Fact]
    public async Task AcceptedCaseRetainsInstructionAttachmentsBesideTheSource()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var attachmentBytes = "%PDF-1.4 synthetic estimate body"u8.ToArray();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: Attachment Test {fixtureId}\r\nClaim Number: ATT-{fixtureId}",
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
    /// DOCS-008: the production shape is more than one attachment. QDOS26009
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
        var first = "%PDF-1.4 synthetic instruction letter"u8.ToArray();
        var second = "%PDF-1.4 synthetic bodyshop report"u8.ToArray();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: Two Attachments {fixtureId}\r\nClaim Number: ATT2-{fixtureId}",
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
    /// CASE-019: the operator's own export of a case, end to end — a real
    /// instruction accepted through the pipeline, its custody completed, then
    /// the archive built and opened. The Core tests cover the field mapping;
    /// this is the only thing that proves an archive comes out at all, which
    /// is what the operator asked for and what never worked.
    /// </summary>
    [Fact]
    public async Task EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange()
    {
        using var factory = new IntakeWebApplicationFactory();
        var host = factory;
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: Export Case {fixtureId}\r\nClaim Number: EXP-{fixtureId}",
        };
        builder.Attachments.Add(
            "53364_1_LtrtoEngineerIn.pdf",
            "%PDF-1.4 synthetic instruction letter"u8.ToArray(),
            MimeKit.ContentType.Parse("application/pdf"));
        var first = SyntheticJpeg();
        var second = SyntheticJpeg(shade: 90);
        builder.Attachments.Add(
            "1_CLVoffside-V1.jpg", first, MimeKit.ContentType.Parse("image/jpeg"));
        builder.Attachments.Add(
            "2_CLVnearside-V1.jpg", second, MimeKit.ContentType.Parse("image/jpeg"));
        message.Body = builder.ToMessageBody();
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
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var outcome = await AcceptAsync(services, receipt.Id);
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
        // export end to end off Box. The gap itself is [[PLAT-038]].
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
        await using (var principalSettings = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var principal = await principalSettings.Principals.SingleAsync(
                item => item.Code == QdosPrincipal.Code);
            principal.EvaManualSubmission = true;
            await principalSettings.SaveChangesAsync();
        }
        var evaTransport = new RecordingEvaTransport();
        var submitter = new EvaSubmissionStore(
            services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            services.GetRequiredService<ICaseDataQueries>(),
            services.GetRequiredService<IVehicleEvidenceQueries>(),
            services.GetRequiredService<IEvaSubmissionModeStore>(),
            services.GetRequiredService<EvaCaseImageReader>(),
            evaTransport,
            new EvaInstructionSettings("CASE040", "Desktop", "eva@example.test"),
            services.GetRequiredService<TimeProvider>());
        var beforeSignOffRefusal = await services
            .GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None);
        await Assert.ThrowsAsync<EvaSignOffEngineerRequiredException>(() => exporter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "66666666666666666666666666666666"),
            CancellationToken.None));
        var afterSignOffRefusal = await services
            .GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None);
        Assert.Equal(beforeSignOffRefusal, afterSignOffRefusal);
        await Assert.ThrowsAsync<EvaSignOffEngineerRequiredException>(() => submitter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "77777777777777777777777777777777",
                EvaSubmissionTrigger.Manual),
            CancellationToken.None));
        Assert.Equal(0, evaTransport.CallCount);
        Assert.Equal(
            beforeSignOffRefusal,
            await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(outcome.Identity.CaseId, CancellationToken.None));
        await using (var refusalCheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            Assert.Empty(await refusalCheck.EvaSubmissions
                .Where(item => item.CaseId == outcome.Identity.CaseId)
                .ToListAsync());
        }
        var signOffEngineerId = await ConfigureDefaultSignOffEngineerAsync(services);

        var assignedEngineerId = DevelopmentOfflineIdentity.AdministratorId;
        await using (var missingEngineer = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await missingEngineer.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.AssignedEngineerId = null;
            await missingEngineer.SaveChangesAsync();
        }
        var missingExport = await Assert.ThrowsAsync<InvalidOperationException>(() => exporter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
            CancellationToken.None));
        Assert.Contains("after an Engineer is assigned", missingExport.Message, StringComparison.Ordinal);
        var missingSubmission = await Assert.ThrowsAsync<InvalidOperationException>(() => submitter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                EvaSubmissionTrigger.Manual),
            CancellationToken.None));
        Assert.Contains("after an Engineer is assigned", missingSubmission.Message, StringComparison.Ordinal);

        var disabledEngineerId = Guid.NewGuid();
        await using (var disabledEngineer = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await disabledEngineer.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            var engineerRoleId = await disabledEngineer.Roles
                .Where(role => role.NormalizedName == "ENGINEER")
                .Select(role => role.Id)
                .SingleAsync();
            disabledEngineer.Users.Add(new PegasusIdentityUser
            {
                Id = disabledEngineerId,
                UserName = $"disabled-engineer-{disabledEngineerId:N}",
                NormalizedUserName = $"DISABLED-ENGINEER-{disabledEngineerId:N}",
                IsEnabled = false,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            disabledEngineer.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = disabledEngineerId,
                RoleId = engineerRoleId
            });
            workflow.AssignedEngineerId = disabledEngineerId;
            await disabledEngineer.SaveChangesAsync();
        }
        var disabledExport = await Assert.ThrowsAsync<InvalidOperationException>(() => exporter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "cccccccccccccccccccccccccccccccc"),
            CancellationToken.None));
        Assert.Contains("Engineer account is disabled", disabledExport.Message, StringComparison.Ordinal);
        var disabledSubmission = await Assert.ThrowsAsync<InvalidOperationException>(() => submitter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "dddddddddddddddddddddddddddddddd",
                EvaSubmissionTrigger.Manual),
            CancellationToken.None));
        Assert.Contains("Engineer account is disabled", disabledSubmission.Message, StringComparison.Ordinal);
        Assert.Equal(0, evaTransport.CallCount);
        await using (var assignEnabledEngineer = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await assignEnabledEngineer.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.AssignedEngineerId = assignedEngineerId;
            await assignEnabledEngineer.SaveChangesAsync();
        }

        const string demotionRaceKey = "55555555555555555555555555555555";
        await using (var lockContext = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            await using var transaction = await lockContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
            var lockedWorkflow = await lockContext.CaseWorkflows
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [CaseWorkflows] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [CaseId] = {outcome.Identity.CaseId}
                    """)
                .SingleAsync();
            var racedExport = Task.Run(() => exporter.ExecuteAsync(
                new(
                    outcome.Identity.CaseId,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    demotionRaceKey),
                CancellationToken.None));
            await Task.Delay(250);
            Assert.False(racedExport.IsCompleted);
            lockedWorkflow.State = CaseLifecycleState.NotReady.ToString();
            await lockContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await Assert.ThrowsAsync<EvaHandoffStateException>(() => racedExport);
        }
        await using (var rejectedCheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            Assert.Empty(await rejectedCheck.EvaFirstHandoffProxies
                .Where(item => item.CaseId == outcome.Identity.CaseId)
                .ToListAsync());
            Assert.Empty(await rejectedCheck.ActionHistory
                .Where(item => item.AggregateType == "Case"
                    && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                    && item.EventKind == "eva_bundle_exported"
                    && item.CorrelationId == demotionRaceKey)
                .ToListAsync());
            await rejectedCheck.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.Review)} WHERE CaseId = {outcome.Identity.CaseId}");
        }

        var firstActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        const string firstOperationKey = "11111111111111111111111111111111";
        var firstSendVersion = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!.Version;
        var export = await exporter.ExecuteAsync(
            new(outcome.Identity.CaseId, firstActor, firstOperationKey),
            CancellationToken.None);

        Assert.NotNull(export);
        var afterFirstSend = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;
        Assert.Equal(CaseLifecycleState.ReportPreparation, afterFirstSend.State);
        Assert.Equal(firstSendVersion + 1, afterFirstSend.Version);
        Assert.Empty(export!.BlockingReasons);
        var bundle = Assert.IsType<EvaBundle>(export.Bundle);
        var reference = outcome.Identity.Reference;
        Assert.Equal($"EVA-{reference}.zip", bundle.FileName);

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        // The shape the operator asked for: a zip of the images and a JSON,
        // and since ENG-014 nothing else -- no manifest.sha256, no
        // provenance.json, neither of which was ever an operator requirement.
        Assert.Contains($"EVA-{reference}.json", entries);
        Assert.Equal(2, entries.Count(name => name.StartsWith("Images/", StringComparison.Ordinal)));
        Assert.Equal(3, entries.Length);
        Assert.Contains(entries, name => name.EndsWith("1_CLVoffside-V1.jpg", StringComparison.Ordinal));
        // The instruction PDF and the .eml are not photographs and stay out.
        Assert.DoesNotContain(entries, name => name.EndsWith(".pdf", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, name => name.EndsWith(".eml", StringComparison.Ordinal));

        using var eva = JsonDocument.Parse(bundle.JsonContent);
        var fields = eva.RootElement.EnumerateObject().ToArray();
        Assert.Equal(
            [
                "Work Provider", "VRM", "Vehicle Model", "Claimant Name", "Reference",
                "Incident Date", "Instruction Date", "Inspection Date", "Inspection Address",
                "Accident Circumstances", "VAT Status", "Mileage", "Mileage Unit"
            ],
            fields.Select(field => field.Name));
        // Every key is a string, present whether or not the case knows it.
        Assert.All(fields, field => Assert.Equal(JsonValueKind.String, field.Value.ValueKind));
        // ENG-015: Reference is the work provider's own reference -- the claim
        // number the letter carried -- not the Pegasus case reference. The
        // archive is still named by the case, asserted above.
        Assert.Equal($"EXP-{fixtureId}", eva.RootElement.GetProperty("Reference").GetString());
        Assert.Equal(QdosPrincipal.Code, eva.RootElement.GetProperty("Work Provider").GetString());
        // Operator direction (2026-08-22): an absent inspection date is today's.
        Assert.False(
            string.IsNullOrWhiteSpace(eva.RootElement.GetProperty("Inspection Date").GetString()),
            "An inspection date must always be present, defaulting to the export date.");

        // The JSON is indented, which is the layout every known-good EVA
        // sample uses and the one EVA will import.
        Assert.StartsWith(
            "{\n  \"Work Provider\": ",
            Encoding.UTF8.GetString(bundle.JsonContent),
            StringComparison.Ordinal);

        // ENG-016: an export is the act that records the once-per-case
        // First sent to Engineer proxy. It used to record nothing -- the
        // gated hand-off did -- and this assertion is the inverse of the one
        // it replaces.
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var proxy = Assert.Single(await context.EvaFirstHandoffProxies
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .ToListAsync());
        Assert.False(proxy.ClaimsExternalDelivery);
        Assert.False(proxy.ClaimsEngineerAssignment);
        Assert.Equal(
            await context.CaseWorkflows
                .Where(item => item.CaseId == outcome.Identity.CaseId)
                .Select(item => item.Version)
                .SingleAsync(),
            proxy.LatestExportedWorkflowVersion);
        Assert.Single(await context.ActionHistory
            .Where(item => item.AggregateType == "Case"
                && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                && item.EventKind == "eva_bundle_exported")
            .ToListAsync());

        // A re-send records a distinct handoff without changing case state.
        var again = await services.GetRequiredService<IExportCaseBundle>().ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "22222222222222222222222222222222"),
            CancellationToken.None);
        Assert.NotNull(again);
        var afterResend = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;
        Assert.Equal(CaseLifecycleState.ReportPreparation, afterResend.State);
        Assert.Equal(afterFirstSend.Version, afterResend.Version);
        Assert.Equal(bundle.Sha256, again!.Bundle!.Sha256);
        await using var recheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var single = Assert.Single(await recheck.EvaFirstHandoffProxies
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .ToListAsync());
        Assert.Equal(proxy.RecordedAtUtc, single.RecordedAtUtc);
        Assert.Equal(2, await recheck.ActionHistory.CountAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == outcome.Identity.CaseId.ToString("D")
            && item.EventKind == "eva_bundle_exported"));
        var secondExportHistory = await recheck.ActionHistory.SingleAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == outcome.Identity.CaseId.ToString("D")
            && item.EventKind == "eva_bundle_exported"
            && item.CorrelationId == "22222222222222222222222222222222");
        using (var secondExportPayload = JsonDocument.Parse(
                   Assert.IsType<string>(secondExportHistory.AfterJson)))
        {
            Assert.Equal(
                assignedEngineerId,
                secondExportPayload.RootElement.GetProperty("assignedEngineerId").GetGuid());
            Assert.Equal(
                signOffEngineerId,
                secondExportPayload.RootElement.GetProperty("signOffEngineerId").GetGuid());
        }

        var replay = await services.GetRequiredService<IExportCaseBundle>().ExecuteAsync(
            new(outcome.Identity.CaseId, firstActor, firstOperationKey),
            CancellationToken.None);
        Assert.Equal(bundle.Sha256, replay!.Bundle!.Sha256);
        await using var replayCheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(2, await replayCheck.ActionHistory.CountAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == outcome.Identity.CaseId.ToString("D")
            && item.EventKind == "eva_bundle_exported"));

        await using (var returnToReview = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await returnToReview.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.State = CaseLifecycleState.Review.ToString();
            await returnToReview.SaveChangesAsync();
        }
        var apiFirstVersion = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!.Version;
        var firstApi = await submitter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                firstActor,
                "88888888888888888888888888888888",
                EvaSubmissionTrigger.Manual),
            CancellationToken.None);
        Assert.True(firstApi?.IsSubmitted);
        var afterFirstApi = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;
        Assert.Equal(CaseLifecycleState.ReportPreparation, afterFirstApi.State);
        Assert.Equal(apiFirstVersion + 1, afterFirstApi.Version);

        var apiResend = await submitter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                firstActor,
                "99999999999999999999999999999999",
                EvaSubmissionTrigger.Manual),
            CancellationToken.None);
        Assert.True(apiResend?.IsSubmitted);
        var afterApiResend = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;
        Assert.Equal(CaseLifecycleState.ReportPreparation, afterApiResend.State);
        Assert.Equal(afterFirstApi.Version, afterApiResend.Version);
        Assert.Equal(2, evaTransport.CallCount);
        Assert.Equal(2, await recheck.EvaSubmissions.CountAsync(
            item => item.CaseId == outcome.Identity.CaseId));
        Assert.Equal(2, await recheck.ActionHistory.CountAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == outcome.Identity.CaseId.ToString("D")
            && item.EventKind == "eva_api_submitted"));
        var secondApiHistory = await recheck.ActionHistory.SingleAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == outcome.Identity.CaseId.ToString("D")
            && item.EventKind == "eva_api_submitted"
            && item.CorrelationId == "99999999999999999999999999999999");
        using (var secondApiPayload = JsonDocument.Parse(
                   Assert.IsType<string>(secondApiHistory.AfterJson)))
        {
            Assert.Equal(
                assignedEngineerId,
                secondApiPayload.RootElement.GetProperty("assignedEngineerId").GetGuid());
            Assert.Equal(
                signOffEngineerId,
                secondApiPayload.RootElement.GetProperty("signOffEngineerId").GetGuid());
        }

        await using (var returnApiToReview = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await returnApiToReview.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.State = CaseLifecycleState.Review.ToString();
            await returnApiToReview.SaveChangesAsync();
        }
        const string versionRaceKey = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var versionRaceTransport = new RecordingEvaTransport(async () =>
        {
            await using var race = await services
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await race.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET Version = Version + 1 WHERE CaseId = {outcome.Identity.CaseId}");
        });
        var racingSubmitter = new EvaSubmissionStore(
            services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            services.GetRequiredService<ICaseDataQueries>(),
            services.GetRequiredService<IVehicleEvidenceQueries>(),
            services.GetRequiredService<IEvaSubmissionModeStore>(),
            services.GetRequiredService<EvaCaseImageReader>(),
            versionRaceTransport,
            new EvaInstructionSettings("CASE040", "Desktop", "eva@example.test"),
            services.GetRequiredService<TimeProvider>());
        var versionRaceRequest = new SubmitCaseToEvaRequest(
            outcome.Identity.CaseId,
            firstActor,
            versionRaceKey,
            EvaSubmissionTrigger.Manual);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => racingSubmitter.ExecuteAsync(
            versionRaceRequest,
            CancellationToken.None));

        Assert.Equal(1, versionRaceTransport.CallCount);
        await using (var versionRaceCheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var recordedSubmission = await versionRaceCheck.EvaSubmissions.SingleAsync(item =>
                item.CaseId == outcome.Identity.CaseId
                && item.OperationKey == versionRaceKey);
            Assert.Equal("eva-1", recordedSubmission.EvaId);
            Assert.Equal("file-1", recordedSubmission.FileReference);
            Assert.Single(await versionRaceCheck.ActionHistory
                .Where(item => item.AggregateType == "Case"
                    && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                    && item.EventKind == "eva_api_submitted"
                    && item.CorrelationId == versionRaceKey)
                .ToListAsync());
        }
        var versionRaceReplay = await racingSubmitter.ExecuteAsync(
            versionRaceRequest,
            CancellationToken.None);
        Assert.True(versionRaceReplay?.IsSubmitted);
        Assert.Equal("eva-1", versionRaceReplay!.Submission!.EvaId);
        Assert.Equal(1, versionRaceTransport.CallCount);

        // CASE-040 review, blocker 1: a Rejected or Unknown manual send never
        // reached EVA, so it is not a handoff. The case must stay in Review,
        // at its current version, with an in-progress edit lease untouched --
        // while the attempt and its outcome are still durably recorded.
        const string undeliveredLeaseToken = "undelivered-send-lease-token";
        await using (var seedLease = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await seedLease.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            Assert.Equal(nameof(CaseLifecycleState.Review), workflow.State);
            workflow.EditLeaseToken = undeliveredLeaseToken;
            workflow.EditLeaseHolder = "undelivered-send-test";
            workflow.EditLeaseHolderKind = "Staff";
            workflow.EditLeaseExpiresAtUtc = FixedUtcNow.AddMinutes(10);
            await seedLease.SaveChangesAsync();
        }
        var beforeUndeliveredSends = (await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;
        Assert.Equal(CaseLifecycleState.Review, beforeUndeliveredSends.State);
        foreach (var (undeliveredOutcome, undeliveredKey) in new[]
        {
            (EvaSubmissionOutcome.Rejected, "ffffffffffffffffffffffffffffff01"),
            (EvaSubmissionOutcome.Unknown, "ffffffffffffffffffffffffffffff02")
        })
        {
            var undeliveredTransport = new FixedOutcomeEvaTransport(undeliveredOutcome);
            var undeliveredSubmitter = new EvaSubmissionStore(
                services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                services.GetRequiredService<ICaseDataQueries>(),
                services.GetRequiredService<IVehicleEvidenceQueries>(),
                services.GetRequiredService<IEvaSubmissionModeStore>(),
                services.GetRequiredService<EvaCaseImageReader>(),
                undeliveredTransport,
                new EvaInstructionSettings("CASE040", "Desktop", "eva@example.test"),
                services.GetRequiredService<TimeProvider>());
            var undeliveredResult = await undeliveredSubmitter.ExecuteAsync(
                new(outcome.Identity.CaseId, firstActor, undeliveredKey, EvaSubmissionTrigger.Manual),
                CancellationToken.None);
            Assert.True(undeliveredResult?.IsSubmitted);
            Assert.Equal(undeliveredOutcome, undeliveredResult!.Submission!.Outcome);
            Assert.False(undeliveredResult.Submission!.IsDelivered);
            Assert.Equal(1, undeliveredTransport.CallCount);

            var afterUndeliveredSend = (await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(outcome.Identity.CaseId, CancellationToken.None))!;
            Assert.Equal(CaseLifecycleState.Review, afterUndeliveredSend.State);
            Assert.Equal(beforeUndeliveredSends.Version, afterUndeliveredSend.Version);

            await using var undeliveredCheck = await services
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var undeliveredWorkflow = await undeliveredCheck.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            Assert.Equal(nameof(CaseLifecycleState.Review), undeliveredWorkflow.State);
            Assert.Equal(undeliveredLeaseToken, undeliveredWorkflow.EditLeaseToken?.TrimEnd());
            var undeliveredRow = await undeliveredCheck.EvaSubmissions.SingleAsync(item =>
                item.CaseId == outcome.Identity.CaseId && item.OperationKey == undeliveredKey);
            Assert.False(undeliveredRow.IsDelivered);
            Assert.Equal(undeliveredOutcome.ToString(), undeliveredRow.Outcome);
            Assert.Single(await undeliveredCheck.ActionHistory
                .Where(item => item.AggregateType == "Case"
                    && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                    && item.EventKind == "eva_api_submitted"
                    && item.CorrelationId == undeliveredKey)
                .ToListAsync());
        }
        await using (var clearLease = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await clearLease.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.EditLeaseToken = null;
            workflow.EditLeaseHolder = null;
            workflow.EditLeaseHolderKind = null;
            workflow.EditLeaseExpiresAtUtc = null;
            await clearLease.SaveChangesAsync();
        }

        await using (var restoreReportPreparation = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var workflow = await restoreReportPreparation.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.State = CaseLifecycleState.ReportPreparation.ToString();
            await restoreReportPreparation.SaveChangesAsync();
        }

        const string concurrentOperationKey = "44444444444444444444444444444444";
        var concurrentActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var concurrent = await Task.WhenAll(
            exporter.ExecuteAsync(
                new(outcome.Identity.CaseId, concurrentActor, concurrentOperationKey),
                CancellationToken.None),
            exporter.ExecuteAsync(
                new(outcome.Identity.CaseId, concurrentActor, concurrentOperationKey),
                CancellationToken.None));
        Assert.All(concurrent, result => Assert.Equal(bundle.Sha256, result!.Bundle!.Sha256));
        await using var concurrentCheck = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Single(await concurrentCheck.ActionHistory
            .Where(item => item.AggregateType == "Case"
                && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                && item.EventKind == "eva_bundle_exported"
                && item.CorrelationId == concurrentOperationKey)
            .ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => exporter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                concurrentOperationKey),
            CancellationToken.None));

        await replayCheck.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.NotReady)} WHERE CaseId = {outcome.Identity.CaseId}");
        await Assert.ThrowsAsync<EvaHandoffStateException>(() =>
            services.GetRequiredService<IExportCaseBundle>().ExecuteAsync(
                new(
                    outcome.Identity.CaseId,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    "33333333333333333333333333333333"),
                CancellationToken.None));
    }

    [Fact]
    public async Task AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS automatic EVA test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: Automatic EVA {fixtureId}\r\nClaim Number: AUTO-{fixtureId}"
        };
        builder.Attachments.Add(
            "53364_1_LtrtoEngineerIn.pdf",
            "%PDF-1.4 synthetic instruction letter"u8.ToArray(),
            MimeKit.ContentType.Parse("application/pdf"));
        var imageBytes = SyntheticJpeg();
        builder.Attachments.Add(
            "1_CLVoffside-V1.jpg",
            imageBytes,
            MimeKit.ContentType.Parse("image/jpeg"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);

        var receipt = await services.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                $"automatic-eva-{fixtureId}.eml",
                "message/rfc822",
                output.ToArray(),
                FixedUtcNow,
                "custody-test",
                new IntakeSourceIdentity(
                    IntakeSourceChannel.ManualUpload,
                    $"automatic-eva:{Guid.NewGuid():N}")),
            CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var outcome = await AcceptAsync(services, receipt.Id);
        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        await using (var seed = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var image = await (
                    from occurrence in seed.Set<DocumentOccurrenceEntity>().AsNoTracking()
                    join version in seed.Set<DocumentVersionEntity>().AsNoTracking()
                        on occurrence.VersionId equals version.Id
                    where occurrence.CaseId == outcome.Identity.CaseId
                          && occurrence.SemanticRole == DocumentSemanticRole.Image
                    select new { version.Id, version.Sha256 })
                .SingleAsync();
            await services.GetRequiredService<IDocumentContentStore>().StoreAsync(
                outcome.Identity.CaseId,
                outcome.Identity.Reference,
                image.Id,
                imageBytes,
                image.Sha256,
                CancellationToken.None);

            var principal = await seed.Principals.SingleAsync(
                item => item.Code == QdosPrincipal.Code);
            principal.EvaAutomaticSubmission = true;
            await seed.SaveChangesAsync();
        }
        await ConfigureDefaultSignOffEngineerAsync(services);

        var reconciler = new ReconcileAutomaticEvaSubmissions(
            services.GetRequiredService<IAutomaticEvaSubmissionStore>());
        Assert.Equal(1, await reconciler.ExecuteAsync(10, CancellationToken.None));

        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        Guid workItemId;
        string operationKey;
        await using (var queued = await contextFactory.CreateDbContextAsync())
        {
            var workItem = await queued.ExternalWorkItems.SingleAsync(item =>
                item.CaseId == outcome.Identity.CaseId
                && item.Kind == ExternalWorkKinds.SubmitCaseToEva);
            workItemId = workItem.Id;
            operationKey = EvaSubmissionPolicy.AttemptOperationKey(
                workItem.OperationKey,
                attemptCount: 1);
        }

        var transport = new RecordingEvaTransport(async () =>
        {
            await using var race = await contextFactory.CreateDbContextAsync();
            await race.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET Version = Version + 1 WHERE CaseId = {outcome.Identity.CaseId}");
        });
        var submitter = new EvaSubmissionStore(
            contextFactory,
            services.GetRequiredService<ICaseDataQueries>(),
            services.GetRequiredService<IVehicleEvidenceQueries>(),
            services.GetRequiredService<IEvaSubmissionModeStore>(),
            services.GetRequiredService<EvaCaseImageReader>(),
            transport,
            new EvaInstructionSettings("CASE040", "Desktop", "eva@example.test"),
            services.GetRequiredService<TimeProvider>());
        var workStore = new EfEvaSubmissionWorkStore(contextFactory);
        var claimedWork = Assert.IsType<EvaSubmissionWorkItem>(
            await workStore.ClaimProcessingAsync(
                workItemId,
                FixedUtcNow,
                TimeSpan.FromMinutes(5),
                CancellationToken.None));
        await Assert.ThrowsAsync<CaseVersionConflictException>(() => submitter.ExecuteAsync(
            new(
                claimedWork.CaseId,
                ActionActor.SystemWorker("pegasus-worker"),
                EvaSubmissionPolicy.AttemptOperationKey(
                    claimedWork.OperationKey,
                    claimedWork.AttemptCount),
                EvaSubmissionTrigger.Automatic),
            CancellationToken.None));

        Assert.Equal(1, transport.CallCount);
        await using (var expired = await contextFactory.CreateDbContextAsync())
        {
            var processingWork = await expired.ExternalWorkItems.SingleAsync(
                item => item.Id == workItemId);
            Assert.Equal("processing", processingWork.State);
            Assert.Equal(
                nameof(CaseLifecycleState.Review),
                (await expired.CaseWorkflows.SingleAsync(
                    item => item.CaseId == outcome.Identity.CaseId)).State);
            processingWork.LeaseExpiresAtUtc = FixedUtcNow.AddMinutes(-1);
            await expired.SaveChangesAsync();
        }

        var processor = new ProcessQueuedEvaSubmission(
            workStore,
            submitter,
            new MutableTimeProvider(FixedUtcNow));

        await processor.ExecuteAsync(workItemId, CancellationToken.None);

        Assert.Equal(1, transport.CallCount);
        await using var verification = await contextFactory.CreateDbContextAsync();
        var submission = await verification.EvaSubmissions.SingleAsync(item =>
            item.CaseId == outcome.Identity.CaseId);
        Assert.Equal(operationKey, submission.OperationKey);
        Assert.Equal("eva-1", submission.EvaId);
        Assert.Equal("file-1", submission.FileReference);
        Assert.Single(await verification.ActionHistory
            .Where(item => item.AggregateType == "Case"
                && item.AggregateId == outcome.Identity.CaseId.ToString("D")
                && item.EventKind == "eva_api_submitted")
            .ToListAsync());
        var completedWork = await verification.ExternalWorkItems.SingleAsync(
            item => item.Id == workItemId);
        Assert.Equal("completed", completedWork.State);
        Assert.Equal(2, completedWork.AttemptCount);
        Assert.Equal("eva_submission_no_longer_applicable", completedWork.FailureCode);
        Assert.Null(completedWork.LeaseToken);

        // SHOULD-FIX 3 (CASE-040 review round 4): the assertions above prove
        // the outcome only through ProcessQueuedEvaSubmission's exception
        // mapping onto the retried work item. Assert directly against the
        // store that a wholly independent automatic submission call over the
        // now-delivered case is refused before EVA is ever called, so the
        // once-only guard is proved at this level, not only in
        // EvaSubmissionPolicyTests.
        var deliveredCallCount = transport.CallCount;
        await Assert.ThrowsAsync<EvaAutomaticSubmissionAlreadyDeliveredException>(() => submitter.ExecuteAsync(
            new(
                outcome.Identity.CaseId,
                ActionActor.SystemWorker("pegasus-worker"),
                Guid.NewGuid().ToString("N"),
                EvaSubmissionTrigger.Automatic),
            CancellationToken.None));
        Assert.Equal(deliveredCallCount, transport.CallCount);
    }

    private static async Task<Guid> ConfigureDefaultSignOffEngineerAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = await userManager.FindByIdAsync(
            DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
        Assert.NotNull(user);
        if (!await userManager.IsInRoleAsync(user, StaffRoleNames.Engineer))
        {
            Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.Engineer)).Succeeded);
        }

        var signature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        user.IsSignOffEngineer = true;
        user.SignOffPrintedName = "Integration Engineer";
        user.SignOffQualifications = null;
        user.SignOffSignature = signature;
        user.SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(signature));
        user.IsDefaultSignOffEngineer = true;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        return user.Id;
    }

    private sealed class RecordingEvaTransport(Func<Task>? afterSubmit = null) : IEvaApiTransport
    {
        public int CallCount { get; private set; }

        public async Task<EvaSubmissionResult> SubmitInstructionAsync(
            EvaInstructionPayload payload,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (afterSubmit is not null)
            {
                await afterSubmit();
            }
            return new EvaSubmissionResult(
                EvaSubmissionOutcome.Succeeded,
                $"eva-{CallCount}",
                $"file-{CallCount}",
                null,
                null,
                payload.Files.Count);
        }
    }

    /// <summary>
    /// CASE-040 review, blocker 1: every other store-level test drives
    /// <see cref="RecordingEvaTransport"/>, which always returns Succeeded --
    /// this is the one fake that lets a test prove what happens when EVA
    /// does not deliver the instruction.
    /// </summary>
    private sealed class FixedOutcomeEvaTransport(EvaSubmissionOutcome outcome) : IEvaApiTransport
    {
        public int CallCount { get; private set; }

        public Task<EvaSubmissionResult> SubmitInstructionAsync(
            EvaInstructionPayload payload,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new EvaSubmissionResult(
                outcome,
                null,
                null,
                "eva-refused",
                "synthetic refusal for CASE-040 review coverage",
                0));
        }
    }

    /// <summary>
    /// DOCS-009: the production shape is a PDF instruction plus photographs.
    /// Every attachment used to be filed as an instruction document whatever
    /// its media type, so a case's own damage photographs were invisible to
    /// both the evidence gallery's image test and EVA image selection — an
    /// export of QDOS26011 would have contained no photographs at all.
    /// </summary>
    [Fact]
    public async Task AnAcceptedInstructionFilesItsPhotographsAsImagesAndItsLetterAsAnInstruction()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var fixtureId = Guid.NewGuid().ToString("N");
        var letter = "%PDF-1.4 synthetic instruction letter"u8.ToArray();
        var photograph = SyntheticJpeg();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS test instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: Photograph Roles {fixtureId}\r\nClaim Number: IMG-{fixtureId}",
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
        var roles = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == outcome.Identity.CaseId
                select new { version.FileName, occurrence.SemanticRole })
            .ToDictionaryAsync(item => item.FileName, item => item.SemanticRole);

        Assert.Equal(
            DocumentSemanticRole.Image,
            roles["1_CLVoffside-V1.jpg"]);
        Assert.Equal(
            DocumentSemanticRole.Instruction,
            roles["53364_1_LtrtoEngineerIn.pdf"]);

        // DOCS-010: the gallery's own id is what the case-document download
        // route resolves. It was the document id, not the occurrence id, so
        // every photograph on the Evidence tab 404d before Box was reached —
        // built positionally into two adjacent Guid slots, and nothing
        // asserted which one it was.
        var occurrenceIds = await context.Set<DocumentOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .Select(item => item.Id)
            .ToListAsync();
        var gallery = await services
            .GetRequiredService<ICaseEvidenceImageQueries>()
            .ListForCaseAsync(outcome.Identity.CaseId, CancellationToken.None);

        Assert.NotEmpty(gallery);
        Assert.All(gallery, image =>
        {
            Assert.True(image.IsCaseDocument);
            Assert.Contains(image.OccurrenceId!.Value, occurrenceIds);
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
    /// DOCS-008: no custody test has ever run an audit case — every other
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
        var report = "%PDF-1.4 synthetic bodyshop report"u8.ToArray();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS audit instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: Audit Custody {fixtureId}\r\nClaim Number: AUD-{fixtureId}",
        };
        builder.Attachments.Add(
            "Bodyshopreport236502-V1.pdf", report, MimeKit.ContentType.Parse("application/pdf"));
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

        var evidenceId = await AllocationTestData.SeedAutomaticAuditEvidenceAsync(services, receipt.Id);
        var outcome = await AcceptAsync(
            services,
            receipt.Id,
            caseType: CaseType.Audit,
            standaloneAuditEvidenceId: evidenceId);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));
    }

    /// <summary>
    /// The three things an operator reported about QDOS26009 that only appear
    /// once custody has actually completed, asserted on one case at the
    /// production shape: it reaches Review rather than sitting at Not ready
    /// (CASE-013), it carries one prefixed reference and no second audit
    /// identity (CASE-014), and its retained files are registered as case
    /// documents (DOCS-007).
    ///
    /// Each was verifiable only by a live case until this existed, because the
    /// promotion, the identity and the document rows are all written inside
    /// CompleteCaseCustodyAsync's single transaction. Custody failing in
    /// production (DOCS-008) meant none of them ever ran.
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
        var report = "%PDF-1.4 synthetic bodyshop report"u8.ToArray();
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS audit instruction";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = $"QDOS instruction\r\nClaimant Name: End To End {fixtureId}\r\nClaim Number: E2E-{fixtureId}",
        };
        builder.Attachments.Add(
            "Bodyshopreport236503-V1.pdf", report, MimeKit.ContentType.Parse("application/pdf"));
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

        var evidenceId = await AllocationTestData.SeedAutomaticAuditEvidenceAsync(services, receipt.Id);
        var outcome = await AcceptAsync(
            services,
            receipt.Id,
            completeness: new(true, true, false, false),
            caseType: CaseType.Audit,
            standaloneAuditEvidenceId: evidenceId);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(services, outcome.CustodyWorkId));

        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

        // CASE-013 — the case moves off Not ready without staff confirmation
        // the automatic route was never going to receive.
        var state = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .Select(item => item.State)
            .SingleAsync();
        Assert.Equal(nameof(CaseLifecycleState.Review), state);

        // CASE-014 — one identity. The reference itself carries the audit
        // prefix and nothing allocates a second one beside it.
        var identity = await context.Set<CaseEntity>()
            .AsNoTracking()
            .Where(item => item.Id == outcome.Identity.CaseId)
            .Select(item => new { item.Reference, item.AuditReference })
            .SingleAsync();
        Assert.StartsWith("a.", identity.Reference, StringComparison.Ordinal);
        Assert.Null(identity.AuditReference);

        // DOCS-007 — the retained files are case documents, not just bytes in
        // custody storage, so the Evidence tab can serve them.
        var documents = await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .CountAsync();
        Assert.True(
            documents > 0,
            $"Custody completed but registered {documents} case documents.");
    }

    /// <summary>
    /// DOCS-006: an instruction's evidence photographs — embedded in its PDF
    /// documents — land beside the source as their own custody files after
    /// the attachments, while letterhead art stays out. Runs against the
    /// operator-supplied mapping corpus (local, git-ignored).
    /// </summary>
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
            $"decision={receipt.Decision}; reason={receipt.DecisionReason}; route={receipt.MailRouteDecision?.Disposition}/{receipt.MailRouteDecision?.SelectedRoute?.WorkProviderCode}; sender={receipt.MailRouteDecision?.EffectiveSender?.Address}");
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
                services.GetRequiredService<IAutomaticCaseAssociationStore>(),
                services.GetRequiredService<IAllocateIntake>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IReadLogicalDocumentVersion>())
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);
        var receipt = Assert.IsType<IntakeReceipt>(
            await services.GetRequiredService<IIntakeReceiptStore>()
                .FindBySourceIdentityAsync(source.Source.SourceIdentity, CancellationToken.None));
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);

        // Manual uploads have no persisted mailbox classification. Processing
        // therefore records a truthful case-type-unavailable allocation
        // failure; the custody scenario supplies the explicit staff acceptance
        // that turns this reviewable receipt into a case.
        var failed = Assert.IsType<IntakeAllocationState>(
            (await services.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receipt.Id, CancellationToken.None))!.AllocationState);
        Assert.Equal(IntakeAllocationFailureKind.CaseTypeUnavailable, failed.FailureKind);
        var accepted = await AcceptAsync(
            services,
            receipt.Id,
            new CaseCompleteness(false, false, false, false));
        return new(accepted.Identity.CaseId, accepted.CustodyWorkId, receipt.Id, source.Content);
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
        return await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receiptId,
                    expectedVersion,
                    ActionActor.SystemWorker("custody-outbox-integration"),
                    $"case-accept:{Guid.NewGuid():N}",
                    "Integration fixture confirmed complete intake evidence.",
                    caseType,
                    principalCode,
                    completeness ?? new(true, true, true, true),
                    standaloneAuditEvidenceId),
                CancellationToken.None);
    }

    private static SourceFixture CreateSource()
    {
        var fixtureId = Guid.NewGuid().ToString("N");
        var email = IntakeTestEvidence.CreateEmail(
            $"custody-{fixtureId}.eml",
            $"QDOS instruction\r\nClaimant Name: Custody Test {fixtureId}\r\nClaim Number: CUS-{fixtureId}");
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


    private static async Task SeedPrincipalAsync(
        IServiceProvider services,
        string principalCode)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        const string organizationName = "Custody test organization";
        const string organizationRole = "work_provider";
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
            Guid caseId, string caseReference, CancellationToken cancellationToken)
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
            Guid caseId, string caseReference, CancellationToken cancellationToken) => throw Failure();

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
