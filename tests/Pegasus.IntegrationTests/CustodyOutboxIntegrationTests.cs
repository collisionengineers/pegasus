using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CustodyOutboxIntegrationTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

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

    [Fact]
    public async Task BoxCreateAndRevokeRecordExactAttributableActionHistory()
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
        var createLease = await leases.ClaimAsync(
            new(
                caseId,
                workflow.Version,
                actor,
                $"box-create-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var createOperationKey = $"box-create:{Guid.NewGuid():N}";
        var createCommand = new CreateBoxFileRequestCommand(
            caseId,
            actor,
            createOperationKey,
            null,
            createLease.Version,
            createLease.Token);
        var create = scope.ServiceProvider.GetRequiredService<ICreateBoxFileRequest>();
        var created = await create.ExecuteAsync(createCommand, CancellationToken.None);
        Assert.False(created.IsReplay);
        Assert.True((await create.ExecuteAsync(
            createCommand,
            CancellationToken.None)).IsReplay);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            create.ExecuteAsync(
                createCommand with
                {
                    Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator])
                },
                CancellationToken.None));

        workflow = Assert.IsType<CaseWorkflowRecord>(
            await queries.GetAsync(caseId, CancellationToken.None));
        var revokeLease = await leases.ClaimAsync(
            new(
                caseId,
                workflow.Version,
                actor,
                $"box-revoke-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var revokeOperationKey = $"box-revoke:{Guid.NewGuid():N}";
        var revokeCommand = new RevokeBoxFileRequestCommand(
            caseId,
            created.FileRequest.Id,
            actor,
            "The requested files are no longer required.",
            revokeOperationKey,
            created.FileRequest.Version,
            revokeLease.Version,
            revokeLease.Token);
        var revoke = scope.ServiceProvider.GetRequiredService<IRevokeBoxFileRequest>();
        var revoked = await revoke.ExecuteAsync(revokeCommand, CancellationToken.None);
        Assert.Equal(BoxFileRequestStatus.Deactivated, revoked.Status);
        Assert.Equal(
            revoked,
            await revoke.ExecuteAsync(revokeCommand, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            revoke.ExecuteAsync(
                revokeCommand with { Reason = "Different reason." },
                CancellationToken.None));

        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var history = await context.ActionHistory
            .Where(value => value.AggregateType == "box_file_request"
                && (value.CorrelationId == createOperationKey
                    || value.CorrelationId == revokeOperationKey))
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.All(history, entry =>
        {
            Assert.Equal(ActorKind.Staff.ToString(), entry.ActorKind);
            Assert.Equal(actor.SubjectId, entry.ActorSubjectId);
            Assert.Equal("[\"Engineer\"]", entry.ActorRolesJson);
            Assert.Equal("Succeeded", entry.Outcome);
            Assert.False(string.IsNullOrWhiteSpace(entry.AfterJson));
        });
    }

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
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
        var boxLease = await leases.ClaimAsync(
            new(
                caseId,
                workflow.Version,
                actor,
                $"terminal-box-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var createBoxRequest = new CreateBoxFileRequestCommand(
            caseId,
            actor,
            $"terminal-box-create:{Guid.NewGuid():N}",
            null,
            boxLease.Version,
            boxLease.Token);
        var createBox =
            scope.ServiceProvider.GetRequiredService<ICreateBoxFileRequest>();
        var boxRequest = await createBox.ExecuteAsync(
            createBoxRequest,
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
        Assert.True((await createBox.ExecuteAsync(
            createBoxRequest,
            CancellationToken.None)).IsReplay);
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
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            createBox.ExecuteAsync(
                createBoxRequest with
                {
                    OperationKey = $"terminal-box-new:{Guid.NewGuid():N}",
                    ExpectedCaseVersion = terminalLease.Version,
                    EditLeaseToken = terminalLease.Token
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            scope.ServiceProvider.GetRequiredService<IRevokeBoxFileRequest>()
                .ExecuteAsync(
                    new(
                        caseId,
                        boxRequest.FileRequest.Id,
                        actor,
                        "Terminal cases are read-only.",
                        $"terminal-box-revoke:{Guid.NewGuid():N}",
                        boxRequest.FileRequest.Version,
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

    private static async Task<AcceptedSource> AcceptDirectSourceAsync(IServiceProvider services)
    {
        var source = CreateSource();
        var receipt = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(source.Source, CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
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
                services.GetRequiredService<TimeProvider>())
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);
        var receipt = Assert.IsType<IntakeReceipt>(
            await services.GetRequiredService<IIntakeReceiptStore>()
                .FindBySourceIdentityAsync(source.Source.SourceIdentity, CancellationToken.None));
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);

        // The queued path allocated the case itself, so this fixture reads what
        // processing produced rather than accepting a second time. Accepting
        // again is exactly the conflict the store is meant to raise.
        var (caseId, custodyWorkId) = await ReadAllocatedCaseAsync(services, receipt.Id);
        return new(caseId, custodyWorkId, receipt.Id, source.Content);
    }

    private static async Task<(Guid CaseId, Guid CustodyWorkId)> ReadAllocatedCaseAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        var contextFactory = services
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<
                Pegasus.Infrastructure.Persistence.PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var connection = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions
            .GetDbConnection(context.Database);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CaseId, CustodyWorkId FROM CaseIntakeLinks WHERE IntakeReceiptId = @receiptId";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@receiptId";
        parameter.Value = receiptId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Processing did not allocate a case for the receipt.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<CaseAcceptanceOutcome> AcceptAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        const string principalCode = QdosPrincipal.Code;
        await SeedPrincipalAsync(services, principalCode);
        return await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receiptId,
                    0,
                    ActionActor.SystemWorker("custody-outbox-integration"),
                    $"case-accept:{Guid.NewGuid():N}",
                    "Integration fixture confirmed complete intake evidence.",
                    CaseType.Inspection,
                    principalCode,
                    new(true, true, true, true)),
                CancellationToken.None);
    }

    private static SourceFixture CreateSource()
    {
        var email = IntakeTestEvidence.CreateEmail(
            $"custody-{Guid.NewGuid():N}.eml",
            "QDOS instruction\r\nClaimant Name: Custody Test\r\nClaim Number: CUS-001\r\nVehicle Registration: AB12 CDE");
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

    private sealed record SourceFixture(IntakeSource Source, byte[] Content);

    private sealed record AcceptedSource(
        Guid CaseId,
        Guid CustodyWorkId,
        Guid ReceiptId,
        byte[] Content);
}
