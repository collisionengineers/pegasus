using Pegasus.Core.Custody;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class DocumentCustodyDurabilityTests
{
    [Fact]
    public async Task RemovingAFileWritesOneNoteTheOperatorCanActuallySee()
    {
        // The point of this test is the ROUND TRIP, not the row. A note written
        // to CaseHistory persists happily, reports success, and never appears on
        // the Notes tab — which is how the Release 22 note defect reached
        // production. So it is asserted through CaseDetails.History, the same
        // read the page makes (DOCS-012).
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"removal-note-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new LogicallyRemoveDocumentCommand(
                caseId,
                occurrenceId,
                actor,
                "Wrong vehicle — the photograph belongs to another claim.",
                $"removal-note:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);

            var remover = scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>();
            await remover.ExecuteAsync(command, CancellationToken.None);
            // Replay must not add a second note.
            await remover.ExecuteAsync(command, CancellationToken.None);

            // Read through ICaseQueryStore — the component that builds the very
            // History collection the Notes tab renders. Asserting the row in
            // CaseWorkflowEvents directly would pass just as happily for a row
            // written to CaseHistory, which is the defect this guards against.
            var details = await scope.ServiceProvider.GetRequiredService<ICaseQueryStore>()
                .GetAsync(new(caseId, actor), CancellationToken.None);
            Assert.NotNull(details);
            var note = Assert.Single(
                details!.History,
                entry => entry.EventType == "case_document_removed");
            Assert.Equal(command.Reason, note.Reason);
            Assert.Equal(ActorKind.Staff.ToString(), note.ActorKind);
            Assert.Equal(actor.SubjectId.ToString(), note.Actor);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StaffConfirmationOfThirdPartyVehicleEvidenceIsDurableAndExactlyReplayable()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"third-party-image-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new ConfirmThirdPartyVehicleEvidenceCommand(
                caseId,
                occurrenceId,
                actor,
                "The retained image depicts the other vehicle.",
                $"third-party-image-confirmation:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var confirmer = scope.ServiceProvider.GetRequiredService<IConfirmThirdPartyVehicleEvidence>();

            await confirmer.ExecuteAsync(command, CancellationToken.None);
            await confirmer.ExecuteAsync(command, CancellationToken.None);

            await using var verification = await database.CreateContextAsync();
            var occurrence = await verification.Set<DocumentOccurrenceEntity>()
                .SingleAsync(item => item.Id == occurrenceId);
            Assert.NotNull(occurrence.ThirdPartyVehicleConfirmedAtUtc);
            Assert.Equal(command.Reason, occurrence.ThirdPartyVehicleConfirmationReason);
            Assert.Equal(command.OperationKey, occurrence.ThirdPartyVehicleConfirmationOperationKey);
            var history = await verification.ActionHistory.SingleAsync(item =>
                item.CorrelationId == command.OperationKey);
            Assert.Equal("third_party_vehicle_evidence_confirmed", history.EventKind);
            Assert.Equal(command.Reason, history.Reason);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CancelledContentWriteLeavesNoImmutableDestinationAndRetrySucceeds()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalDocumentContentStore(root);
            var caseId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var content = "complete managed document content"u8.ToArray();
            var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.StoreAsync(
                    caseId,
                    "QDOS001",
                    versionId,
                    content,
                    sha256,
                    cancellationSource.Token));

            var directory = Path.Combine(
                root,
                "cases",
                "QDOS001",
                "managed",
                versionId.ToString("N"));
            Assert.False(File.Exists(Path.Combine(directory, "content")));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));

            await store.StoreAsync(
                caseId,
                "QDOS001",
                versionId,
                content,
                sha256,
                CancellationToken.None);

            await using var retained = await store.OpenReadAsync(
                caseId,
                "QDOS001",
                versionId,
                sha256,
                content.LongLength,
                CancellationToken.None);
            using var copy = new MemoryStream();
            await retained.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FailedDatabaseSaveRollsBackCaseAndRemovesUnreferencedContent()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        var interceptor = new FailNextDocumentSaveInterceptor();
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                configureDatabase: options => options.AddInterceptors(interceptor),
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(
                        caseId,
                        ExpectedVersion: 0,
                        actor,
                        $"document-add-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new AddCaseDocumentCommand(
                caseId,
                "evidence.txt",
                "text/plain",
                "retained evidence"u8.ToArray(),
                DocumentSemanticRole.Other,
                DocumentSource.StaffUpload,
                $"durability:{Guid.NewGuid():N}",
                actor,
                $"document-add:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var addDocument = scope.ServiceProvider.GetRequiredService<IAddCaseDocument>();
            interceptor.FailNextDocumentSave();

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                addDocument.ExecuteAsync(command, CancellationToken.None));

            var managedDirectory = Path.Combine(
                root,
                "custody",
                "cases",
                "QDOS001",
                "managed");
            Assert.Empty(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Empty(await context.Set<DocumentVersionEntity>().ToArrayAsync());
                Assert.Equal(
                    3,
                    await context.Set<CaseEntity>()
                        .Where(value => value.Id == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
                Assert.Equal(
                    0,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }

            var added = await addDocument.ExecuteAsync(command, CancellationToken.None);

            Assert.False(added.IsReplay);
            Assert.Single(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Equal(
                    1,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FailedRequestUploadReceiptSaveRetriesThroughTheSameDurableCustodyIntent()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        var interceptor = new FailNextDocumentSaveInterceptor();
        var contentStore = new ManagedOnlyDocumentContentStore(
            new LocalDocumentContentStore(Path.Combine(root, "custody")));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                configureDatabase: options => options.AddInterceptors(interceptor),
                localArtifactRootFactory: _ => root,
                configureServices: services =>
                    services.AddSingleton<IDocumentContentStore>(contentStore));
            var caseId = await SeedCaseAsync(database);
            var token = RequestUploadToken.Create();
            var requestId = Guid.NewGuid();
            var limits = new RequestUploadLimits(
                "durability-v1",
                TimeSpan.FromHours(1),
                1,
                1024,
                2048,
                ["text/plain"],
                10,
                TimeSpan.FromMinutes(1));
            var createdAtUtc = DateTimeOffset.UtcNow;
            await using (var context = await database.CreateContextAsync())
            {
                context.Add(new RequestUploadLinkEntity
                {
                    Id = requestId,
                    CaseId = caseId,
                    TokenDigest = token.TokenDigest,
                    Status = RequestUploadStatus.Active,
                    CreatedAtUtc = createdAtUtc,
                    ExpiresAtUtc = createdAtUtc.Add(limits.Lifetime),
                    LimitsVersion = limits.Version,
                    Version = 1,
                    CreateOperationKey = $"request-create:{Guid.NewGuid():N}"
                });
                await context.SaveChangesAsync();
            }

            var command = new UploadToRequestCommand(
                token.Secret.Token,
                new(
                    "evidence.txt",
                    "text/plain",
                    "request upload evidence"u8.ToArray(),
                    $"request-file:{Guid.NewGuid():N}"),
                AttemptsInCurrentRateWindow: 1);
            interceptor.FailNextRequestUploadSave();

            await using (var firstScope = database.CreateAsyncScope())
            {
                var upload = CreateUpload(firstScope.ServiceProvider);
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    upload.ExecuteAsync(command, CancellationToken.None));
            }

            var managedDirectory = Path.Combine(
                root,
                "custody",
                "cases",
                "QDOS001",
                "managed");
            Assert.Single(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            Assert.Single(contentStore.Addresses);
            Guid retainedDocumentId;
            Guid retainedVersionId;
            Guid retainedOccurrenceId;
            int retainedOrdinal;
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Empty(await context.Set<RequestUploadReceiptEntity>().ToArrayAsync());
                var retained = await context.Set<DocumentVersionEntity>().SingleAsync();
                var retainedDocument = await context.Set<CaseDocumentEntity>().SingleAsync();
                retainedDocumentId = retained.DocumentId;
                retainedVersionId = retained.Id;
                retainedOrdinal = retainedDocument.Ordinal;
                Assert.Equal(retainedDocumentId, retainedDocument.Id);
                Assert.Equal(DocumentCustodyStatus.Confirmed, retained.CustodyStatus);
                var retainedOccurrence = await context.Set<DocumentOccurrenceEntity>().SingleAsync();
                retainedOccurrenceId = retainedOccurrence.Id;
                Assert.Equal(DocumentSemanticRole.OriginalSource, retainedOccurrence.SemanticRole);
                Assert.Equal(
                    EfPublicUploadRetentionStore.ScopeOperationKey(
                        requestId,
                        command.File.OperationKey),
                    (await context.Set<PublicUploadOccurrenceEntity>().SingleAsync()).OperationKey);
                Assert.Equal(
                    2,
                    await context.Set<RequestUploadLinkEntity>()
                        .Where(value => value.Id == requestId)
                        .Select(value => value.Version)
                        .SingleAsync());
                Assert.Equal(
                    RequestUploadStatus.Active,
                    await context.Set<RequestUploadLinkEntity>()
                        .Where(value => value.Id == requestId)
                        .Select(value => value.Status)
                        .SingleAsync());
                var reservedTotals = await context.Set<RequestUploadLinkEntity>()
                    .Where(value => value.Id == requestId)
                    .Select(value => new { value.AcceptedFileCount, value.AcceptedByteCount })
                    .SingleAsync();
                Assert.Equal(1, reservedTotals.AcceptedFileCount);
                Assert.Equal((long)command.File.Content.Length, reservedTotals.AcceptedByteCount);
                Assert.Equal(
                    1,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }

            UploadToRequestResult result;
            await using (var restartedScope = database.CreateAsyncScope())
            {
                result = await CreateUpload(restartedScope.ServiceProvider)
                    .ExecuteAsync(command, CancellationToken.None);
            }

            Assert.Equal(RequestUploadDecision.Accepted, result.Decision);
            Assert.False(result.IsReplay);
            Assert.Single(contentStore.Addresses);
            Assert.All(contentStore.Addresses, address =>
            {
                Assert.Equal(caseId, address.CaseId);
                Assert.Equal("QDOS001", address.CaseReference);
                Assert.Equal("case-root-id", address.CaseRootRemoteId);
                Assert.Equal(retainedOccurrenceId, address.OccurrenceId);
                Assert.Equal(retainedOrdinal, address.OccurrenceOrdinal);
                Assert.Equal(retainedDocumentId, address.DocumentId);
                Assert.Equal(retainedVersionId, address.VersionId);
                Assert.Equal(1, address.Version);
                Assert.Equal(DocumentSemanticRole.OriginalSource, address.SemanticRole);
                Assert.Equal("evidence.txt", address.FileName);
                Assert.Equal("text/plain", address.MediaType);
            });
            var retainedFile = Assert.Single(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            Assert.Equal(command.File.Content.ToArray(), await File.ReadAllBytesAsync(retainedFile));
            await using (var context = await database.CreateContextAsync())
            {
                var document = await context.Set<CaseDocumentEntity>().SingleAsync();
                var version = await context.Set<DocumentVersionEntity>().SingleAsync();
                var occurrence = await context.Set<DocumentOccurrenceEntity>().SingleAsync();
                Assert.Single(await context.Set<PublicUploadOccurrenceEntity>().ToArrayAsync());
                var receipt = await context.Set<RequestUploadReceiptEntity>().SingleAsync();
                Assert.Equal(result.ReceiptId, receipt.Id);
                Assert.Equal(retainedDocumentId, document.Id);
                Assert.Equal(retainedVersionId, version.Id);
                Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
                Assert.Equal(command.File.FileName, version.FileName);
                Assert.Equal(command.File.MediaType, version.MediaType);
                Assert.Equal(command.File.Content.Length, version.ContentLength);
                Assert.Equal(retainedVersionId, occurrence.VersionId);
                Assert.Equal(retainedOccurrenceId, occurrence.Id);
                Assert.Equal(DocumentSemanticRole.OriginalSource, occurrence.SemanticRole);
                Assert.Equal(retainedVersionId, receipt.VersionId);
                Assert.Equal(command.File.OperationKey, receipt.OperationKey);
                Assert.Equal(retainedOrdinal, document.Ordinal);
                Assert.Equal(document.Ordinal, occurrence.Ordinal);
                Assert.Equal(
                    3,
                    await context.Set<RequestUploadLinkEntity>()
                        .Where(value => value.Id == requestId)
                        .Select(value => value.Version)
                        .SingleAsync());
                Assert.Equal(
                    RequestUploadStatus.Exhausted,
                    await context.Set<RequestUploadLinkEntity>()
                        .Where(value => value.Id == requestId)
                        .Select(value => value.Status)
                        .SingleAsync());
                Assert.Equal(
                    2,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }

            UploadToRequestResult replay;
            await using (var replayScope = database.CreateAsyncScope())
            {
                replay = await CreateUpload(replayScope.ServiceProvider)
                    .ExecuteAsync(command, CancellationToken.None);
            }

            Assert.Equal(RequestUploadDecision.Replay, replay.Decision);
            Assert.True(replay.IsReplay);
            Assert.Equal(result.ReceiptId, replay.ReceiptId);
            Assert.Single(contentStore.Addresses);
            await using (var replayContext = await database.CreateContextAsync())
            {
                Assert.Single(await replayContext.Set<CaseDocumentEntity>().ToArrayAsync());
                Assert.Single(await replayContext.Set<DocumentVersionEntity>().ToArrayAsync());
                Assert.Single(await replayContext.Set<DocumentOccurrenceEntity>().ToArrayAsync());
                Assert.Single(await replayContext.Set<PublicUploadOccurrenceEntity>().ToArrayAsync());
                Assert.Single(await replayContext.Set<RequestUploadReceiptEntity>().ToArrayAsync());
            }

            IUploadToRequest CreateUpload(IServiceProvider services)
            {
                var contextFactory = services
                    .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
                var timeProvider = services.GetRequiredService<TimeProvider>();
                var custody = services.GetRequiredService<ICaseArtifactCustody>();
                var custodyStatus = services.GetRequiredService<ICaseArtifactCustodyStatus>();
                Assert.Same(custody, custodyStatus);
                return new EfDocumentRequestStore(
                    contextFactory,
                    new RequestUploadPolicy(limits, timeProvider),
                    limits,
                    timeProvider,
                    new RetainIncomingArtifact(
                        custody,
                        new EfPublicUploadRetentionStore(contextFactory),
                        custodyStatus));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<Guid> SeedCaseAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var seeded = await SeededPrincipals.QdosAsync(context);
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var occurredAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        context.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "durability.eml",
                MediaType = "message/rfc822",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"durability:{Guid.NewGuid():N}",
                ReceivedAtUtc = occurredAtUtc,
                ProcessedAtUtc = occurredAtUtc,
                SourceReaderKey = "durability-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Durability test fixture.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = seeded.Id,
                SequenceLineageId = seeded.SequenceLineageId,
                Year = 2031,
                Sequence = 1,
                Reference = "QDOS001",
                Type = "Inspection",
                InitialState = "NotReady",
                // Lowercase, as ToCode writes it in production. The seed said
                // "Confirmed" and nothing noticed, because no test in this file
                // had ever read the case back through GetCase (DOCS-012).
                CustodyState = "confirmed",
                CustodyRootRemoteId = "case-root-id",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = occurredAtUtc,
                Version = 3,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 0,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }

    private static async Task<Guid> SeedCurrentImageAsync(LocalDbTestDatabase database, Guid caseId)
    {
        await using var context = await database.CreateContextAsync();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        context.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}"
            },
            new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = "third-party.jpg",
                MediaType = "image/jpeg",
                ContentLength = 1,
                Sha256 = new string('a', 64),
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "Staff:test",
                IsCurrent = true
            },
            new DocumentOccurrenceEntity
            {
                Id = occurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                SemanticRole = DocumentSemanticRole.Image,
                Source = DocumentSource.StaffUpload,
                SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = $"seed-image:{occurrenceId:N}"
            });
        await context.SaveChangesAsync();
        return occurrenceId;
    }

    private sealed class FailNextDocumentSaveInterceptor : SaveChangesInterceptor
    {
        private int failNextDocumentSave;
        private int failNextRequestUploadSave;

        public void FailNextDocumentSave() =>
            Interlocked.Exchange(ref failNextDocumentSave, 1);

        public void FailNextRequestUploadSave() =>
            Interlocked.Exchange(ref failNextRequestUploadSave, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref failNextDocumentSave) == 1
                && eventData.Context is not null
                && eventData.Context.ChangeTracker.Entries<DocumentVersionEntity>()
                    .Any(entry => entry.State == EntityState.Added)
                && Interlocked.Exchange(ref failNextDocumentSave, 0) == 1)
            {
                throw new DbUpdateException("Injected document database failure.");
            }

            if (Volatile.Read(ref failNextRequestUploadSave) == 1
                && eventData.Context is not null
                && eventData.Context.ChangeTracker.Entries<RequestUploadReceiptEntity>()
                    .Any(entry => entry.State == EntityState.Added)
                && Interlocked.Exchange(ref failNextRequestUploadSave, 0) == 1)
            {
                throw new DbUpdateException("Injected request-upload database failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class ManagedOnlyDocumentContentStore(IDocumentContentStore inner)
        : IDocumentContentStore
    {
        public List<ManagedDocumentContentAddress> Addresses { get; } = [];

        public Task StoreAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Managed custody addressing is required.");

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            return await inner.StoreVersionAsync(
                address,
                content,
                expectedSha256,
                cancellationToken);
        }

        public Task<Stream> OpenReadAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken) =>
            inner.OpenReadAsync(
                caseId,
                caseReference,
                versionId,
                expectedSha256,
                expectedLength,
                cancellationToken);

        public Task DeleteAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            CancellationToken cancellationToken) =>
            inner.DeleteAsync(caseId, caseReference, versionId, cancellationToken);
    }
}
