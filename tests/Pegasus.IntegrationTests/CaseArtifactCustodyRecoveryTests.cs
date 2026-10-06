using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.IntegrationTests.Support;

namespace Pegasus.IntegrationTests;

public sealed class CaseArtifactCustodyRecoveryTests
{
    [Fact]
    public async Task HoldingCustodyResolvesTheIntakeAssetByItsNFormatGuidAndConfirmsOneBoxFile()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var receiptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var bytes = "holding photo bytes"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        await using (var db = await database.CreateContextAsync())
        {
            db.Add(new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "images.pdf",
                MediaType = "application/pdf",
                SourceLength = bytes.Length,
                SourceHash = hash,
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"holding:{Guid.NewGuid():N}",
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                ProcessedAtUtc = DateTimeOffset.UtcNow,
                SourceReaderKey = "test",
                SourceReaderVersion = "1",
                Decision = "needs_sorting",
                DecisionReason = "Test.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            });
            db.Add(new IntakeAssetEntity
            {
                Id = assetId,
                IntakeReceiptId = receiptId,
                SourceLabel = "uploaded images.pdf, page 1, image 1",
                FileName = "page-1-image-1.jpg",
                MediaType = "image/jpeg",
                Kind = "embedded_image",
                Disposition = "embedded",
                ContentLength = bytes.Length,
                ContentHash = hash,
                StorageKey = "test/holding-photo"
            });
            await db.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var box = new HoldingBox();
        var client = new BoxContentClient(
            new(
                new Uri("https://api.box.com/2.0/"),
                new Uri("https://upload.box.com/api/2.0/"),
                HoldingBox.RootId,
                "test", "test", "test", "test", "test", "test", HoldingBox.HoldingFolderId),
            new HttpClient(new HoldingBoxHandler(box)),
            new StaticBoxAuthorizationHeaderProvider(),
            TimeProvider.System);
        var publisher = new RecordingCachePublisher();
        var custody = new EfCaseArtifactCustody(
            factory,
            new BoxDocumentContentStore(client),
            new MemoryArtifactStore(),
            TimeProvider.System,
            client,
            HoldingBox.HoldingFolderId,
            publisher);
        var request = new CaseArtifactCustodyRequest(
            ActionActor.SystemWorker("intake-processing"),
            null,
            receiptId,
            assetId.ToString("N"),
            $"intake:{receiptId:N}:{assetId:N}",
            "page-1-image-1.jpg",
            "image/jpeg",
            bytes.Length,
            hash,
            new MemoryStream(bytes, writable: false));

        var confirmed = await custody.RetainAsync(request, CancellationToken.None);
        var replay = await custody.RetainAsync(
            request with { Content = new MemoryStream(bytes, writable: false) },
            CancellationToken.None);

        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, confirmed.Disposition);
        Assert.Equal(("holding-file", "holding-version"),
            (confirmed.BoxFileId, confirmed.BoxVersionId));
        Assert.Equal(1, box.UploadCount);
        Assert.Equal(confirmed.BoxFileId, replay.BoxFileId);

        // The staged bytes were still in hand when Box confirmed the file, so the
        // asset's read-cache copy was published then, once, under the asset. The
        // replay uploaded nothing and published nothing.
        var published = Assert.Single(publisher.Published);
        Assert.Equal(DocumentContentCacheKey.ForIntakeAsset(assetId), published.Key);
        Assert.Equal(bytes, published.Bytes);
        Assert.Equal(hash.ToLowerInvariant(), published.Sha256);
        Assert.Equal(bytes.LongLength, published.ContentLength);
        await using var verify = await database.CreateContextAsync();
        var stored = await verify.Set<IntakeAssetEntity>().SingleAsync(item => item.Id == assetId);
        Assert.Equal("confirmed", stored.CustodyStatus);
        Assert.Equal(confirmed.BoxFileId, stored.BoxFileId);
        Assert.Equal(confirmed.BoxVersionId, stored.BoxVersionId);
        Assert.Equal(HoldingBox.HoldingFolderId, stored.BoxParentFolderId);

        var wrongReceipt = request with
        {
            IntakeReceiptId = Guid.NewGuid(),
            Content = new MemoryStream(bytes, writable: false)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => custody.RetainAsync(wrongReceipt, CancellationToken.None));
        var changedMetadata = request with
        {
            FileName = "different.jpg",
            Content = new MemoryStream(bytes, writable: false)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => custody.RetainAsync(changedMetadata, CancellationToken.None));
    }

    [Fact]
    public async Task IntakeAssetRetentionClaimsOnceAndKeepsConfirmedContentIdentity()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var receiptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var failedAssetId = Guid.NewGuid();
        var bytes = "intake image bytes"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var operationKey = IncomingArtifactOperationKey.ForIntake(receiptId, assetId);
        await using (var db = await database.CreateContextAsync())
        {
            db.Add(new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "images.pdf",
                MediaType = "application/pdf",
                SourceLength = bytes.Length,
                SourceHash = hash,
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"holding:{Guid.NewGuid():N}",
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                ProcessedAtUtc = DateTimeOffset.UtcNow,
                SourceReaderKey = "test",
                SourceReaderVersion = "1",
                Decision = "needs_sorting",
                DecisionReason = "Test.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            });
            db.Add(new IntakeAssetEntity
            {
                Id = assetId,
                IntakeReceiptId = receiptId,
                SourceLabel = "uploaded images.pdf, page 1, image 1",
                FileName = "page-1-image-1.jpg",
                MediaType = "image/jpeg",
                Kind = "embedded_image",
                Disposition = "embedded",
                ContentLength = bytes.Length,
                ContentHash = hash,
                StorageKey = "test/holding-photo"
            });
            db.Add(new IntakeAssetEntity
            {
                Id = failedAssetId,
                IntakeReceiptId = receiptId,
                SourceLabel = "uploaded images.pdf, page 1, image 2",
                FileName = "page-1-image-2.jpg",
                MediaType = "image/jpeg",
                Kind = "embedded_image",
                Disposition = "embedded",
                ContentLength = bytes.Length,
                ContentHash = hash,
                StorageKey = "test/holding-photo-2"
            });
            await db.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var winner = new EfIncomingArtifactRetentionStore(contextFactory);
        var contender = new EfIncomingArtifactRetentionStore(contextFactory);
        var wrongReceiptKey = IncomingArtifactOperationKey.ForIntake(Guid.NewGuid(), assetId);

        Assert.Null(await winner.FindAsync(wrongReceiptKey, CancellationToken.None));
        Assert.False(await winner.TryClaimHandOverAsync(wrongReceiptKey, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => winner.FindAsync($"public-upload:{receiptId:N}:{assetId:N}", CancellationToken.None));

        var claims = await Task.WhenAll(
            winner.TryClaimHandOverAsync(operationKey, CancellationToken.None),
            contender.TryClaimHandOverAsync(operationKey, CancellationToken.None));
        Assert.Equal(1, claims.Count(claim => claim));
        Assert.Equal(1, claims.Count(claim => !claim));

        var claimed = await winner.FindAsync(operationKey, CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.Equal(IncomingArtifactCustodyState.Unknown, claimed.State);
        Assert.Equal(hash, claimed.Sha256);
        Assert.Equal(bytes.Length, claimed.ContentLength);

        await winner.RecordAsync(new(
            assetId,
            operationKey,
            IncomingArtifactCustodyState.Pending,
            Sha256: hash,
            ContentLength: bytes.Length), CancellationToken.None);
        var pending = await winner.FindAsync(operationKey, CancellationToken.None);
        Assert.NotNull(pending);
        Assert.Equal(IncomingArtifactCustodyState.Pending, pending.State);

        await winner.RecordAsync(new(
            assetId,
            operationKey,
            IncomingArtifactCustodyState.Confirmed,
            BoxFileId: "intake-file",
            BoxVersionId: "intake-version",
            Sha256: hash,
            ContentLength: bytes.Length), CancellationToken.None);
        await contender.RecordAsync(new(
            assetId,
            operationKey,
            IncomingArtifactCustodyState.Pending,
            BoxFileId: "late-file",
            BoxVersionId: "late-version",
            Sha256: new string('f', 64),
            ContentLength: bytes.Length + 1), CancellationToken.None);

        var confirmed = await winner.FindAsync(operationKey, CancellationToken.None);
        Assert.NotNull(confirmed);
        Assert.True(confirmed.IsConfirmed);
        Assert.Equal("intake-file", confirmed.BoxFileId);
        Assert.Equal("intake-version", confirmed.BoxVersionId);
        Assert.Equal(hash, confirmed.Sha256);
        Assert.Equal(bytes.Length, confirmed.ContentLength);

        var failedKey = IncomingArtifactOperationKey.ForIntake(receiptId, failedAssetId);
        await winner.RecordAsync(new(
            failedAssetId,
            failedKey,
            IncomingArtifactCustodyState.Failed,
            Sha256: hash,
            ContentLength: bytes.Length), CancellationToken.None);
        var failed = await winner.FindAsync(failedKey, CancellationToken.None);
        Assert.NotNull(failed);
        Assert.Equal(IncomingArtifactCustodyState.Failed, failed.State);
        Assert.Null(failed.BoxFileId);
        Assert.Null(failed.BoxVersionId);
    }

    [Fact]
    public async Task LocalLogicalReaderRejectsWrongReceiptCaseHashAndLengthBeforeReadingContent()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        Guid receiptId;
        var assetId = Guid.NewGuid();
        await using (var db = await database.CreateContextAsync())
        {
            receiptId = await db.Cases.Where(value => value.Id == caseId)
                .Select(value => value.OriginIntakeReceiptId)
                .SingleAsync()
                ?? throw new InvalidOperationException("The seeded Case has no origin receipt.");
            db.Add(new IntakeAssetEntity
            {
                Id = assetId,
                IntakeReceiptId = receiptId,
                SourceLabel = "source",
                FileName = "source.eml",
                MediaType = "message/rfc822",
                Kind = "source",
                Disposition = "source",
                ContentLength = 3,
                ContentHash = Convert.ToHexString(SHA256.HashData("abc"u8)).ToLowerInvariant(),
                StorageKey = "not-read"
            });
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var artifacts = new CountingArtifactStore("abc"u8.ToArray());
        var reader = new LocalLogicalDocumentVersionReader(
            factory,
            new FailFirstContentStore(),
            artifacts);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var hash = Convert.ToHexString(SHA256.HashData("abc"u8)).ToLowerInvariant();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.OpenAsync(
            new(actor, null, null, assetId, Guid.NewGuid(), receiptId, hash, 3),
            CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.OpenAsync(
            new(actor, null, null, assetId, caseId, Guid.NewGuid(), hash, 3),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.OpenAsync(
            new(actor, null, null, assetId, caseId, receiptId, new string('a', 64), 3),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.OpenAsync(
            new(actor, null, null, assetId, caseId, receiptId, hash, 4),
            CancellationToken.None));

        Assert.Equal(0, artifacts.ReadCount);

        var worker = ActionActor.SystemWorker("intake-processing");
        await using (var content = await reader.OpenAsync(
            new(worker, null, null, assetId, caseId, receiptId, hash, 3),
            CancellationToken.None))
        {
            var bytes = new byte[3];
            await content.Content.ReadExactlyAsync(bytes);
            Assert.Equal("abc"u8.ToArray(), bytes);
        }
        Assert.Equal(1, artifacts.ReadCount);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.OpenAsync(
            new(worker, null, null, assetId, Guid.NewGuid(), receiptId, hash, 3),
            CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => reader.OpenAsync(
            new(ActionActor.Principal(Guid.NewGuid()), null, null, assetId, caseId, receiptId, hash, 3),
            CancellationToken.None));
        Assert.Equal(1, artifacts.ReadCount);
    }

    [Fact]
    public async Task FailedWriteLeavesOnePendingIntentAndReplayUsesTheSameVersionIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.ArtifactRecovery", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            await using var scope = database.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            var intake = scope.ServiceProvider.GetRequiredService<IIntakeArtifactStore>();
            var quarantine = scope.ServiceProvider.GetRequiredService<IIntakeQuarantineArtifactStore>();
            var content = "generated report bytes"u8.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var store = new FailFirstContentStore();
            var custody = new EfCaseArtifactCustody(
                factory, store, quarantine, TimeProvider.System);
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var request = new CaseArtifactCustodyRequest(
                actor,
                caseId,
                null,
                "report-generation:17",
                "artifact-operation:17",
                "report.pdf",
                "application/pdf",
                content.LongLength,
                hash,
                new MemoryStream(content, writable: false));

            await Assert.ThrowsAsync<IOException>(
                () => custody.RetainAsync(request, CancellationToken.None));
            Guid pendingDocumentId;
            Guid pendingVersionId;
            Guid pendingOccurrenceId;
            await using (var db = await database.CreateContextAsync())
            {
                var pending = await db.Set<DocumentVersionEntity>().SingleAsync();
                pendingDocumentId = pending.DocumentId;
                pendingVersionId = pending.Id;
                Assert.Equal(DocumentCustodyStatus.Pending, pending.CustodyStatus);
                pendingOccurrenceId = (await db.Set<DocumentOccurrenceEntity>().SingleAsync()).Id;
                Assert.Equal(0, (await db.CaseWorkflows.SingleAsync()).Version);
            }

            // The provider result was lost, but the accepted Pending intent is
            // discoverable by the caller's original key without re-offering bytes.
            var recoveredPending = await custody.FindByOperationKeyAsync(
                actor, caseId, request.OperationKey, CancellationToken.None);
            Assert.NotNull(recoveredPending);
            Assert.Equal(CaseArtifactCustodyDisposition.Pending, recoveredPending!.Disposition);
            Assert.Equal(pendingVersionId, recoveredPending.VersionId);
            Assert.Equal(pendingDocumentId, recoveredPending.DocumentId);
            Assert.Equal(pendingOccurrenceId, recoveredPending.OccurrenceId);
            Assert.Equal(hash, recoveredPending.Sha256, ignoreCase: true);
            Assert.Equal(content.LongLength, recoveredPending.ContentLength);

            var restarted = new ReconcilePendingArtifactCustody(factory, store, intake);
            var replay = await restarted.ExecuteAsync(10, CancellationToken.None);

            Assert.Equal(1, replay.Confirmed);
            Assert.Equal(2, store.Addresses.Count);
            Assert.All(store.Addresses, address => Assert.Equal(pendingVersionId, address.VersionId));
            await using (var db = await database.CreateContextAsync())
            {
                Assert.Single(await db.Set<DocumentVersionEntity>().ToArrayAsync());
                Assert.Single(await db.Set<DocumentOccurrenceEntity>().ToArrayAsync());
                Assert.Equal(
                    DocumentCustodyStatus.Confirmed,
                    (await db.Set<DocumentVersionEntity>().SingleAsync()).CustodyStatus);
                Assert.Null((await db.Set<DocumentVersionEntity>().SingleAsync()).PendingContentStorageKey);
                // Custody confirms source evidence without consuming a staff edit.
                Assert.Equal(0, (await db.CaseWorkflows.SingleAsync()).Version);
                var confirmed = await db.Set<DocumentVersionEntity>().SingleAsync();
                confirmed.IsLogicallyRemoved = true;
                await db.SaveChangesAsync();
                await Assert.ThrowsAsync<FileNotFoundException>(() => custody.GetAsync(
                    actor, caseId, confirmed.DocumentId, confirmed.Id, pendingOccurrenceId, default));
            }
            await Assert.ThrowsAsync<FileNotFoundException>(() => custody.RetainAsync(
                request with { Content = new MemoryStream(content, writable: false) }, default));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticCustodyPreservesLiveCaseAuthority(bool recover)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var lease = await new AcquireCaseEditLease(new EfCaseWorkflowStore(factory, TimeProvider.System))
            .ExecuteAsync(new(caseId, 0, actor, "lease-custody"), default);
        IDocumentContentStore content = recover ? new FailFirstContentStore() : new SuccessfulContentStore();
        var artifacts = new MemoryArtifactStore();
        var custody = new EfCaseArtifactCustody(factory, content, artifacts, TimeProvider.System);
        var bytes = "request evidence"u8.ToArray();
        var request = ArtifactRequest(actor, caseId, bytes);
        if (recover)
        {
            await Assert.ThrowsAsync<IOException>(() => custody.RetainAsync(request, default));
            var result = await new ReconcilePendingArtifactCustody(factory, content, artifacts)
                .ExecuteAsync(10, default);
            Assert.Equal(1, result.Confirmed);
        }
        else
        {
            Assert.Equal(CaseArtifactCustodyDisposition.Confirmed,
                (await custody.RetainAsync(request, default)).Disposition);
        }
        var retained = await custody.FindByOperationKeyAsync(actor, caseId, request.OperationKey, default);
        var replay = await custody.RetainAsync(request with
        {
            Content = new MemoryStream(bytes, writable: false)
        }, default);
        Assert.NotNull(retained);
        Assert.Equal(retained.DocumentId, replay.DocumentId);
        Assert.Equal(retained.VersionId, replay.VersionId);
        Assert.Equal(retained.OccurrenceId, replay.OccurrenceId);
        await using var context = await factory.CreateDbContextAsync();
        Assert.Single(await context.Set<DocumentVersionEntity>().ToArrayAsync());
        Assert.Single(await context.Set<DocumentOccurrenceEntity>().ToArrayAsync());
        var workflow = await context.CaseWorkflows.SingleAsync();
        Assert.Equal(0, workflow.Version);
        Assert.Equal(lease.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        CaseMutationGuard.Require(workflow, actor, 0, lease.Token, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// A guide valuation's report is filed while the Engineer who pressed Get
    /// valuation still holds the Case open with the figures unsaved (ADR-0060):
    /// it must leave their version and lease standing so their Save still
    /// lands, and a second filing of the same report files nothing new.
    /// </summary>
    [Fact]
    public async Task AGuideValuationReportIsFiledWithoutTouchingTheOpenEditSession()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var lease = await new AcquireCaseEditLease(new EfCaseWorkflowStore(factory, TimeProvider.System))
            .ExecuteAsync(new(caseId, 0, actor, "lease-valuation"), default);
        var filing = new FileGuideValuationReport(
            new EfCaseArtifactCustody(factory, new SuccessfulContentStore(), new MemoryArtifactStore(), TimeProvider.System),
            new PdfPigPageTextExtractor());
        var request = new FileGuideValuationReportRequest(
            actor, caseId, ValuationSource.Glasses, "AB12CDE", new DateOnly(2031, 4, 1), new PdfReport());

        var filed = await filing.ExecuteAsync(request, default);
        var again = await filing.ExecuteAsync(request, default);

        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, filed.Disposition);
        Assert.Equal(filed.VersionId, again.VersionId);
        await using var context = await factory.CreateDbContextAsync();
        var version = Assert.Single(await context.Set<DocumentVersionEntity>().ToArrayAsync());
        Assert.Equal("Glass's valuation AB12CDE 2031-04.pdf", version.FileName);
        Assert.Equal("application/pdf", version.MediaType);
        var occurrence = Assert.Single(await context.Set<DocumentOccurrenceEntity>().ToArrayAsync());
        Assert.Equal(DocumentSemanticRole.Other, occurrence.SemanticRole);
        Assert.Equal("guide-valuation-report:Glasses:glass-stock:33636950", occurrence.OperationKey);
        var workflow = await context.CaseWorkflows.SingleAsync();
        Assert.Equal(0, workflow.Version);
        Assert.Equal(lease.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        CaseMutationGuard.Require(workflow, actor, 0, lease.Token, DateTimeOffset.UtcNow);
    }

    private sealed class PdfReport : IGuideValuationReport
    {
        // Built once: a second fetch of the same report must answer the same
        // bytes, as the provider's file does, or the replay is a different file.
        private readonly byte[] _content = IntakeTestEvidence.CreatePdf("Vehicle Valuation Report - AB12CDE");

        public string Identity => "glass-stock:33636950";

        public Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken) => Task.FromResult(_content);
    }

    /// <summary>
    /// The Worker's sweep can file a version while the request that recorded
    /// it is still writing the same bytes. The request's own confirmation then
    /// changes nothing, and it answers with what the stored row says.
    /// </summary>
    [Fact]
    public async Task AVersionFiledByReconciliationDuringTheWriteIsReturnedAsConfirmed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var artifacts = new MemoryArtifactStore();
        var content = new ReconcilingContentStore(factory, artifacts);
        var custody = new EfCaseArtifactCustody(factory, content, artifacts, TimeProvider.System);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var request = ArtifactRequest(actor, caseId, "generated report bytes"u8.ToArray());

        var result = await custody.RetainAsync(request, default);

        Assert.NotNull(content.Reconciled);
        Assert.Equal(1, content.Reconciled!.Confirmed);
        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, result.Disposition);
        // The identities are the ones reconciliation stored, not the request's.
        Assert.Equal(ReconcilingContentStore.ReconciledFileId, result.BoxFileId);
        Assert.Equal(ReconcilingContentStore.ReconciledVersionId, result.BoxVersionId);
        Assert.Null(result.FailureCode);
        Assert.Null(result.PendingContentStorageKey);
        await using var db = await database.CreateContextAsync();
        var version = await db.Set<DocumentVersionEntity>().SingleAsync();
        Assert.Equal(version.Id, result.VersionId);
        Assert.Equal(version.DocumentId, result.DocumentId);
        Assert.Equal(DocumentCustodyStatus.Confirmed, version.CustodyStatus);
        Assert.Equal(ReconcilingContentStore.ReconciledFileId, version.BoxFileId);
        Assert.Null(version.PendingContentStorageKey);
        Assert.Equal(
            result.OccurrenceId,
            (await db.Set<DocumentOccurrenceEntity>().SingleAsync()).Id);
    }

    [Fact]
    public async Task StatusPreservesDispositionAndSelectsExactOccurrenceWhenVersionIsShared()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var firstOccurrenceId = Guid.NewGuid();
        var secondOccurrenceId = Guid.NewGuid();
        await using (var db = await database.CreateContextAsync())
        {
            db.Add(new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                Ordinal = 1,
                SourceOccurrenceIdentity = "failed-custody"
            });
            db.Add(new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = "failed.pdf",
                MediaType = "application/pdf",
                ContentLength = 0,
                Sha256 = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant(),
                CustodyStatus = DocumentCustodyStatus.Failed,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "test",
                IsCurrent = true
            });
            db.AddRange(new DocumentOccurrenceEntity
            {
                Id = firstOccurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                Ordinal = 1,
                SemanticRole = DocumentSemanticRole.OriginalSource,
                Source = DocumentSource.Generated,
                SourceOccurrenceIdentity = "failed-custody",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = "failed-custody"
            }, new DocumentOccurrenceEntity
            {
                Id = secondOccurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                Ordinal = 2,
                SemanticRole = DocumentSemanticRole.OriginalSource,
                Source = DocumentSource.Generated,
                SourceOccurrenceIdentity = "failed-custody-repeated",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = "failed-custody-repeated"
            });
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var custody = new EfCaseArtifactCustody(
            factory,
            new FailFirstContentStore(),
            new CountingArtifactStore(),
            TimeProvider.System);

        var result = await custody.GetAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            caseId,
            documentId,
            versionId,
            firstOccurrenceId,
            default);

        Assert.Equal(CaseArtifactCustodyDisposition.Failed, result.Disposition);
        Assert.Equal(firstOccurrenceId, result.OccurrenceId);
        Assert.Equal("case_custody_failed", result.FailureCode);
        var repeated = await custody.GetAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            caseId,
            documentId,
            versionId,
            secondOccurrenceId,
            default);
        Assert.Equal(secondOccurrenceId, repeated.OccurrenceId);
        await Assert.ThrowsAsync<FileNotFoundException>(() => custody.GetAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            caseId,
            documentId,
            versionId,
            Guid.NewGuid(),
            default));
    }

    [Fact]
    public async Task ReconciliationRotatesRetainedRowsSoLaterPendingWorkIsSelected()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var db = await database.CreateContextAsync())
        {
            var caseEntity = await db.Cases.SingleAsync(value => value.Id == caseId);
            caseEntity.CustodyRootRemoteId = null;
            for (var ordinal = 1; ordinal <= 2; ordinal++)
            {
                AddPendingVersion(db, caseId, ordinal);
            }
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var reconciler = new ReconcilePendingArtifactCustody(
            factory,
            new FailFirstContentStore(),
            new CountingArtifactStore());

        Assert.Equal(1, (await reconciler.ExecuteAsync(1, default)).Retained);
        Assert.Equal(1, (await reconciler.ExecuteAsync(1, default)).Retained);

        await using var verify = await database.CreateContextAsync();
        var attempts = await verify.ActionHistory
            .Where(value => value.EventKind == "ArtifactCustodyReconciliationAttempt")
            .Select(value => value.AggregateId)
            .Distinct()
            .ToArrayAsync();
        Assert.Equal(2, attempts.Length);
    }

    /// <summary>
    /// A version whose filing failed waits 1, 2, 4 and 8 minutes after its first
    /// four consecutive failures, then 10 minutes, and is not offered to the sweep
    /// until the wait has passed.
    /// </summary>
    [Theory]
    [InlineData(1, 30, false)]
    [InlineData(1, 120, true)]
    [InlineData(2, 90, false)]
    [InlineData(2, 150, true)]
    [InlineData(3, 200, false)]
    [InlineData(3, 250, true)]
    [InlineData(4, 420, false)]
    [InlineData(4, 540, true)]
    [InlineData(5, 540, false)]
    [InlineData(5, 660, true)]
    [InlineData(30, 540, false)]
    [InlineData(30, 660, true)]
    public async Task AFailedCandidateIsOfferedAgainOnlyOnceItsWaitHasPassed(
        int consecutiveFailures, int lastFailedSecondsAgo, bool triedAgain)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var clock = new FixedClock(new DateTimeOffset(2031, 5, 1, 12, 0, 0, TimeSpan.Zero));
        await using (var db = await database.CreateContextAsync())
        {
            var versionId = AddPendingVersion(db, caseId, 1);
            for (var failure = 0; failure < consecutiveFailures; failure++)
            {
                AddAttempt(db, versionId, "Failed",
                    clock.GetUtcNow().AddSeconds(-lastFailedSecondsAgo).AddMinutes(-15 * failure));
            }
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var artifacts = new CountingArtifactStore();

        var result = await new ReconcilePendingArtifactCustody(
            factory, new SuccessfulContentStore(), artifacts, clock).ExecuteAsync(50, default);

        Assert.Equal(triedAgain ? 1 : 0, result.Candidates);
        Assert.Equal(triedAgain ? 1 : 0, artifacts.ReadCount);
    }

    /// <summary>
    /// A retained outcome is not a failure. It never delays the version, whether
    /// it is the only attempt or newer than the failures before it.
    /// </summary>
    [Fact]
    public async Task ARetainedCandidateIsNeverSkipped()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var clock = new FixedClock(new DateTimeOffset(2031, 5, 1, 12, 0, 0, TimeSpan.Zero));
        await using (var db = await database.CreateContextAsync())
        {
            var retainedOnly = AddPendingVersion(db, caseId, 1);
            AddAttempt(db, retainedOnly, "Retained", clock.GetUtcNow().AddSeconds(-1));
            var retainedAfterFailures = AddPendingVersion(db, caseId, 2);
            AddAttempt(db, retainedAfterFailures, "Failed", clock.GetUtcNow().AddSeconds(-40));
            AddAttempt(db, retainedAfterFailures, "Failed", clock.GetUtcNow().AddSeconds(-30));
            AddAttempt(db, retainedAfterFailures, "Retained", clock.GetUtcNow().AddSeconds(-5));
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var artifacts = new CountingArtifactStore();

        var result = await new ReconcilePendingArtifactCustody(
            factory, new SuccessfulContentStore(), artifacts, clock).ExecuteAsync(50, default);

        Assert.Equal(2, result.Candidates);
        Assert.Equal(2, artifacts.ReadCount);
    }

    /// <summary>
    /// The wait counts the failures after the last retained outcome. Two failures
    /// in all would wait 2 minutes; the one after the retained outcome waits 1.
    /// </summary>
    [Fact]
    public async Task OnlyFailuresAfterTheLastRetainedOutcomeSetTheWait()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var clock = new FixedClock(new DateTimeOffset(2031, 5, 1, 12, 0, 0, TimeSpan.Zero));
        await using (var db = await database.CreateContextAsync())
        {
            var versionId = AddPendingVersion(db, caseId, 1);
            AddAttempt(db, versionId, "Failed", clock.GetUtcNow().AddMinutes(-9));
            AddAttempt(db, versionId, "Retained", clock.GetUtcNow().AddMinutes(-8));
            AddAttempt(db, versionId, "Failed", clock.GetUtcNow().AddSeconds(-90));
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var artifacts = new CountingArtifactStore();

        var result = await new ReconcilePendingArtifactCustody(
            factory, new SuccessfulContentStore(), artifacts, clock).ExecuteAsync(50, default);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, artifacts.ReadCount);
    }

    /// <summary>
    /// The version that is still waiting sorts ahead of the fresh one, so it would
    /// take the only slot if the wait were decided after Take.
    /// </summary>
    [Fact]
    public async Task AWaitingCandidateDoesNotUseUpASlot()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var clock = new FixedClock(new DateTimeOffset(2031, 5, 1, 12, 0, 0, TimeSpan.Zero));
        Guid waitingId;
        Guid freshId;
        await using (var db = await database.CreateContextAsync())
        {
            waitingId = AddPendingVersion(db, caseId, 1);
            for (var failure = 0; failure < 5; failure++)
            {
                AddAttempt(db, waitingId, "Failed",
                    clock.GetUtcNow().AddMinutes(-9).AddMinutes(-15 * failure));
            }
            freshId = AddPendingVersion(db, caseId, 2);
            AddAttempt(db, freshId, "Retained", clock.GetUtcNow().AddMinutes(-1));
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var artifacts = new CountingArtifactStore();

        var result = await new ReconcilePendingArtifactCustody(
            factory, new SuccessfulContentStore(), artifacts, clock).ExecuteAsync(1, default);

        Assert.Equal(1, result.Candidates);
        await using var verify = await database.CreateContextAsync();
        var attempts = await verify.ActionHistory
            .Where(value => value.EventKind == "ArtifactCustodyReconciliationAttempt")
            .ToArrayAsync();
        Assert.Equal(5, attempts.Count(value => value.AggregateId == waitingId.ToString()));
        Assert.Equal(2, attempts.Count(value => value.AggregateId == freshId.ToString()));
    }

    /// <summary>
    /// The sweep swallows a failed candidate, so its log line is the only place
    /// that says why: the exception type and message, once, at Warning, and the
    /// type is returned for the Worker's count line.
    /// </summary>
    [Fact]
    public async Task AFailedAttemptLogsItsCauseOnceAtWarning()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        Guid versionId;
        await using (var db = await database.CreateContextAsync())
        {
            versionId = AddPendingVersion(db, caseId, 1);
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var logger = new RecordingLogger();

        var result = await new ReconcilePendingArtifactCustody(
            factory, new SuccessfulContentStore(), new CountingArtifactStore(), TimeProvider.System, logger: logger)
            .ExecuteAsync(50, default);

        Assert.Equal(1, result.Failures);
        Assert.Equal(nameof(FileNotFoundException), result.FirstFailure);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(FileNotFoundException), entry.Message);
        Assert.Contains("Pending artifact content is unavailable.", entry.Message);
        Assert.Contains(versionId.ToString(), entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// The attempt row is bookkeeping. When it cannot be written the item still
    /// counts as failed, the failure is named in the log, and the next candidate
    /// is still tried.
    /// </summary>
    [Fact]
    public async Task AnAttemptThatCannotBeRecordedFailsOneItemAndTheSweepContinues()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var db = await database.CreateContextAsync())
        {
            AddPendingVersion(db, caseId, 1);
            AddPendingVersion(db, caseId, 2);
            await db.SaveChangesAsync();
        }
        await using var scope = database.CreateAsyncScope();
        var inner = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        // Call 1 reads the candidates; call 2 is the first candidate's attempt record.
        var factory = new ThrowingContextFactory(inner, throwOnCall: 2);
        var artifacts = new CountingArtifactStore();
        var logger = new RecordingLogger();

        var result = await new ReconcilePendingArtifactCustody(
            factory, new SuccessfulContentStore(), artifacts, TimeProvider.System, logger: logger)
            .ExecuteAsync(50, default);

        Assert.Equal(2, result.Candidates);
        Assert.Equal(2, result.Failures);
        Assert.Equal(2, artifacts.ReadCount);
        await using var verify = await database.CreateContextAsync();
        Assert.Equal(1, await verify.ActionHistory.CountAsync(
            value => value.EventKind == "ArtifactCustodyReconciliationAttempt"));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains("could not be recorded", StringComparison.Ordinal));
    }

    private static Guid AddPendingVersion(PegasusDbContext db, Guid caseId, int ordinal)
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        db.Add(new CaseDocumentEntity
        {
            Id = documentId,
            CaseId = caseId,
            Ordinal = ordinal,
            SourceOccurrenceIdentity = $"pending-{ordinal}"
        });
        db.Add(new DocumentVersionEntity
        {
            Id = versionId,
            DocumentId = documentId,
            Version = 1,
            FileName = $"pending-{ordinal}.pdf",
            MediaType = "application/pdf",
            ContentLength = 0,
            Sha256 = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant(),
            PendingContentStorageKey = $"pending-{ordinal}",
            CustodyStatus = DocumentCustodyStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(ordinal),
            CreatedBy = "test",
            IsCurrent = true
        });
        db.Add(new DocumentOccurrenceEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            DocumentId = documentId,
            VersionId = versionId,
            Ordinal = ordinal,
            SemanticRole = DocumentSemanticRole.OriginalSource,
            Source = DocumentSource.Generated,
            SourceOccurrenceIdentity = $"pending-{ordinal}",
            RecordedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(ordinal),
            OperationKey = $"pending-{ordinal}"
        });
        return versionId;
    }

    private static void AddAttempt(
        PegasusDbContext db, Guid versionId, string outcome, DateTimeOffset occurredAtUtc) =>
        db.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = nameof(DocumentVersionEntity),
            AggregateId = versionId.ToString(),
            EventKind = "ArtifactCustodyReconciliationAttempt",
            ActorKind = nameof(ActorKind.SystemWorker),
            ActorSubjectId = "artifact-custody-reconciliation",
            ActorRolesJson = "[]",
            OccurredAtUtc = occurredAtUtc,
            Outcome = outcome,
            CorrelationId = versionId.ToString(),
            Reason = outcome == "Failed" ? "IOException" : "CaseRootUnavailable"
        });

    [Fact]
    public async Task SystemWorkerMayRetainCaseArtifactThroughExecuteSystemWorkRight()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var custody = new EfCaseArtifactCustody(
            factory, new SuccessfulContentStore(), new MemoryArtifactStore(), TimeProvider.System);
        var bytes = "worker evidence"u8.ToArray();

        var result = await custody.RetainAsync(
            ArtifactRequest(ActionActor.SystemWorker("custody-reconciler"), caseId, bytes), default);

        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, result.Disposition);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => custody.RetainAsync(
            ArtifactRequest(ActionActor.Principal(Guid.NewGuid()), caseId, bytes), default));
    }

    /// <summary>
    /// A file custody confirms on a Case is published to the read cache while
    /// its staged bytes are still in hand, under its version, and only once the
    /// version is confirmed and committed: the cache row refers to the version
    /// row, so an earlier publish could not even be recorded. A replay files
    /// nothing and publishes nothing.
    /// </summary>
    [Fact]
    public async Task AConfirmedCaseArtifactIsPublishedToTheReadCacheOnceItsVersionHasCommitted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var statusWhenPublished = new List<DocumentCustodyStatus>();
        var publisher = new RecordingCachePublisher(async key =>
        {
            await using var db = await factory.CreateDbContextAsync();
            statusWhenPublished.Add((await db.Set<DocumentVersionEntity>().AsNoTracking()
                .SingleAsync(version => version.Id == key.DocumentVersionId)).CustodyStatus);
        });
        var custody = new EfCaseArtifactCustody(
            factory,
            new SuccessfulContentStore(),
            new MemoryArtifactStore(),
            TimeProvider.System,
            cachePublisher: publisher);
        var bytes = "a report the caller filed and still holds"u8.ToArray();
        var request = ArtifactRequest(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), caseId, bytes);

        var result = await custody.RetainAsync(request, default);
        var replay = await custody.RetainAsync(
            request with { Content = new MemoryStream(bytes, writable: false) }, default);

        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, result.Disposition);
        Assert.Equal(result.VersionId, replay.VersionId);
        var published = Assert.Single(publisher.Published);
        Assert.Equal(DocumentContentCacheKey.ForVersion(result.VersionId!.Value), published.Key);
        Assert.Equal(bytes, published.Bytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), published.Sha256);
        Assert.Equal(bytes.LongLength, published.ContentLength);
        Assert.Equal([DocumentCustodyStatus.Confirmed], statusWhenPublished);
    }

    /// <summary>
    /// The read cache is an optimisation, never the record: when the blob store
    /// cannot take the copy the filing is still confirmed, and no cache entry
    /// exists for the first read to trip over.
    /// </summary>
    [Fact]
    public async Task ACachePublishThatFailsLeavesTheFilingConfirmed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var box = new BoxContentClient(
            new(
                new Uri("https://api.box.com/2.0/"),
                new Uri("https://upload.box.com/api/2.0/"),
                HoldingBox.RootId,
                "test", "test", "test", "test", "test", "test", HoldingBox.HoldingFolderId),
            new HttpClient(new HoldingBoxHandler(new HoldingBox())),
            new StaticBoxAuthorizationHeaderProvider(),
            TimeProvider.System);
        var cache = new CachedDocumentContentStore(
            factory, new UnavailableCacheContainer(), box, TimeProvider.System);
        var custody = new EfCaseArtifactCustody(
            factory,
            new SuccessfulContentStore(),
            new MemoryArtifactStore(),
            TimeProvider.System,
            cachePublisher: cache);

        var result = await custody.RetainAsync(
            ArtifactRequest(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                caseId,
                "filed while the cache is down"u8.ToArray()),
            default);

        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, result.Disposition);
        await using var db = await database.CreateContextAsync();
        Assert.Equal(
            DocumentCustodyStatus.Confirmed,
            (await db.Set<DocumentVersionEntity>().SingleAsync()).CustodyStatus);
        Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
    }

    /// <summary>
    /// A version the Worker's reconciliation files later is published the same
    /// way, from the bytes it read from the pending copy.
    /// </summary>
    [Fact]
    public async Task AVersionFiledByReconciliationIsPublishedToTheReadCache()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var content = new FailFirstContentStore();
        var artifacts = new MemoryArtifactStore();
        var custody = new EfCaseArtifactCustody(factory, content, artifacts, TimeProvider.System);
        var bytes = "filed later by reconciliation"u8.ToArray();
        await Assert.ThrowsAsync<IOException>(() => custody.RetainAsync(
            ArtifactRequest(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), caseId, bytes), default));
        var publisher = new RecordingCachePublisher();

        var reconciled = await new ReconcilePendingArtifactCustody(
                factory, content, artifacts, TimeProvider.System, publisher)
            .ExecuteAsync(10, default);

        Assert.Equal(1, reconciled.Confirmed);
        await using var db = await database.CreateContextAsync();
        var version = await db.Set<DocumentVersionEntity>().SingleAsync();
        var published = Assert.Single(publisher.Published);
        Assert.Equal(DocumentContentCacheKey.ForVersion(version.Id), published.Key);
        Assert.Equal(bytes, published.Bytes);
        Assert.Equal(version.Sha256, published.Sha256);
        Assert.Equal(version.ContentLength, published.ContentLength);
    }

    private static CaseArtifactCustodyRequest ArtifactRequest(
        ActionActor actor, Guid caseId, byte[] content) => new(
        actor, caseId, null, $"test:{Guid.NewGuid():N}", $"test:{Guid.NewGuid():N}",
        "evidence.txt", "text/plain", content.LongLength,
        Convert.ToHexString(SHA256.HashData(content)),
        new MemoryStream(content, writable: false));

    private static async Task<Guid> SeedCaseAsync(LocalDbTestDatabase database)
    {
        await using var db = await database.CreateContextAsync();
        var seeded = await SeededPrincipals.QdosAsync(db);
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        db.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "source.eml",
                MediaType = "message/rfc822",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"recovery:{Guid.NewGuid():N}",
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                ProcessedAtUtc = DateTimeOffset.UtcNow,
                SourceReaderKey = "test",
                SourceReaderVersion = "1",
                Decision = "case_created",
                DecisionReason = "Test.",
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
                Type = "inspection",
                InitialState = "NotReady",
                CustodyState = "confirmed",
                OriginIntakeReceiptId = receiptId,
                CustodyRootRemoteId = "case-root",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 0,
                ConcurrencyToken = Guid.NewGuid(),
            });
        await db.SaveChangesAsync();
        return caseId;
    }

    private sealed class FailFirstContentStore : IDocumentContentStore
    {
        private int calls;
        public List<ManagedDocumentContentAddress> Addresses { get; } = [];

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new IOException("Injected pre-write dependency failure.");
            }
            return Task.FromResult(new DocumentContentWriteResult(
                DocumentContentWriteDisposition.Created, null, null));
        }

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new IOException("Injected pre-write dependency failure.");
            }
            return Task.FromResult(new DocumentContentWriteResult(
                DocumentContentWriteDisposition.Created, null, null));
        }

        public Task StoreAsync(Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content, string expectedSha256, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(Guid caseId, string caseReference, Guid versionId, string expectedSha256, long expectedLength, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DeleteAsync(Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A failed save must not delete remote content.");
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    /// <summary>Fails the context creation with the given call number, and only that one.</summary>
    private sealed class ThrowingContextFactory(
        IDbContextFactory<PegasusDbContext> inner, int throwOnCall) : IDbContextFactory<PegasusDbContext>
    {
        private int calls;

        public PegasusDbContext CreateDbContext() =>
            Interlocked.Increment(ref calls) == throwOnCall
                ? throw new InvalidOperationException("Injected context creation failure.")
                : inner.CreateDbContext();

        public Task<PegasusDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref calls) == throwOnCall
                ? throw new InvalidOperationException("Injected context creation failure.")
                : inner.CreateDbContextAsync(cancellationToken);
    }

    private sealed class RecordingLogger : ILogger<ReconcilePendingArtifactCustody>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class CountingArtifactStore(ReadOnlyMemory<byte>? content = null)
        : IIntakeArtifactStore, IIntakeQuarantineArtifactStore
    {
        public int ReadCount { get; private set; }
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<StagedArtifactInventoryItem> StageAsync(Guid stagedReceiptId, string contentHash,
            Stream content, long contentLength, DateTimeOffset firstSeenAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(content);
        }
        public Task<IntakeQuarantineArtifact> StoreStreamAsync(
            Stream content, long contentLength, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task VerifyAsync(IntakeQuarantineArtifact artifact, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>A cache container whose blobs refuse every upload.</summary>
    private sealed class UnavailableCacheContainer : BlobContainerClient
    {
        public override BlobClient GetBlobClient(string blobName) => new UnavailableCacheBlob();
    }

    private sealed class UnavailableCacheBlob : BlobClient
    {
        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromException<Response<BlobContentInfo>>(
                new RequestFailedException(503, "The blob store is unavailable."));
    }

    private sealed class SuccessfulContentStore : IDocumentContentStore
    {
        public int WriteCount { get; private set; }
        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address, ReadOnlyMemory<byte> content,
            string expectedSha256, CancellationToken cancellationToken)
        {
            WriteCount++;
            return Task.FromResult(new DocumentContentWriteResult(
                DocumentContentWriteDisposition.Created, null, null));
        }
        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address, Stream content, long contentLength,
            string expectedSha256, CancellationToken cancellationToken)
        {
            WriteCount++;
            return Task.FromResult(new DocumentContentWriteResult(
                DocumentContentWriteDisposition.Created, null, null));
        }
        public Task StoreAsync(Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content, string expectedSha256, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(Guid caseId, string caseReference, Guid versionId, string expectedSha256, long expectedLength, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// A content store whose write for the request lets the Worker's
    /// reconciliation run to its end first, as the ten-second sweep did in
    /// production. Each writer is answered with its own stored identities.
    /// </summary>
    private sealed class ReconcilingContentStore(
        IDbContextFactory<PegasusDbContext> factory,
        IIntakeArtifactStore artifacts) : IDocumentContentStore
    {
        internal const string ReconciledFileId = "reconciled-file";
        internal const string ReconciledVersionId = "reconciled-version";

        public PendingArtifactCustodyReconciliationResult? Reconciled { get; private set; }

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address, ReadOnlyMemory<byte> content,
            string expectedSha256, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentContentWriteResult(
                DocumentContentWriteDisposition.Created, ReconciledFileId, ReconciledVersionId));

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address, Stream content, long contentLength,
            string expectedSha256, CancellationToken cancellationToken)
        {
            Reconciled = await new ReconcilePendingArtifactCustody(factory, this, artifacts)
                .ExecuteAsync(10, cancellationToken);
            return new(DocumentContentWriteDisposition.Replay, "request-file", "request-version");
        }

        public Task StoreAsync(Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content, string expectedSha256, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(Guid caseId, string caseReference, Guid versionId, string expectedSha256, long expectedLength, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryArtifactStore : IIntakeArtifactStore, IIntakeQuarantineArtifactStore
    {
        private readonly Dictionary<string, ReadOnlyMemory<byte>> values = [];
        public int StoreCount { get; private set; }
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            StoreCount++;
            var key = $"test/{contentHash}";
            values[key] = content;
            return Task.FromResult(key);
        }
        public async Task<StagedArtifactInventoryItem> StageAsync(Guid stagedReceiptId, string contentHash,
            Stream content, long contentLength, DateTimeOffset firstSeenAtUtc,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var storageKey = await StoreAsync(contentHash, buffer.ToArray(), cancellationToken);
            return new(storageKey, contentHash, contentLength, firstSeenAtUtc,
                StagedArtifactDisposition.Pending, string.Empty);
        }
        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult(values.TryGetValue(storageKey, out var content)
                ? (ReadOnlyMemory<byte>?)content
                : null);

        public async Task<IntakeQuarantineArtifact> StoreStreamAsync(
            Stream content, long contentLength, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length != contentLength)
            {
                throw new InvalidDataException("Test artifact length mismatch.");
            }
            var value = buffer.ToArray();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(value));
            var key = await StoreAsync(hash, value, cancellationToken);
            return new IntakeQuarantineArtifact(key, hash, contentLength);
        }

        public Task VerifyAsync(IntakeQuarantineArtifact artifact, CancellationToken cancellationToken)
        {
            if (!values.TryGetValue(artifact.StorageKey, out var value)
                || value.Length != artifact.ContentLength
                || !string.Equals(
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(value.Span)),
                    artifact.ContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Test artifact verification failed.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingReadStream(byte[] content) : Stream
    {
        private int position;
        private bool blocked;
        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => content.LongLength;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!blocked)
            {
                blocked = true;
                ReadEntered.SetResult();
                await ReleaseRead.Task.WaitAsync(cancellationToken);
            }
            if (position >= content.Length)
                return 0;
            var count = Math.Min(buffer.Length, content.Length - position);
            content.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CallbackArtifactStore(Func<Task> onStore)
        : IIntakeArtifactStore, IIntakeQuarantineArtifactStore
    {
        public int StoreCount { get; private set; }
        public async Task<string> StoreAsync(
            string contentHash, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            StoreCount++;
            await onStore();
            return $"test/{contentHash}";
        }
        public async Task<StagedArtifactInventoryItem> StageAsync(Guid stagedReceiptId,
            string contentHash, Stream content, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var storageKey = await StoreAsync(contentHash, buffer.ToArray(), cancellationToken);
            return new(storageKey, contentHash, contentLength, firstSeenAtUtc,
                StagedArtifactDisposition.Pending, string.Empty);
        }
        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(null);

        public async Task<IntakeQuarantineArtifact> StoreStreamAsync(
            Stream content, long contentLength, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length != contentLength)
            {
                throw new InvalidDataException("Test artifact length mismatch.");
            }
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buffer.ToArray()));
            await onStore();
            StoreCount++;
            return new IntakeQuarantineArtifact($"test/{hash}", hash, contentLength);
        }

        public Task VerifyAsync(IntakeQuarantineArtifact artifact, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StaticBoxAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider
    {
        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Bearer test-token");

        public Task<bool> RenewIfDueAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>
    /// The minimum Box surface holding custody uses: ancestry, an exact-name
    /// child lookup, upload and a version read-back. Keeping it local to this
    /// regression makes the test exercise the real Box adapter rather than a
    /// replacement custody implementation.
    /// </summary>
    private sealed class HoldingBox
    {
        public const string RootId = "405543781910";
        public const string HoldingFolderId = "holding-folder";
        public int UploadCount { get; private set; }
        private byte[]? uploaded;

        public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == $"/2.0/folders/{HoldingFolderId}")
            {
                return Json(new
                {
                    id = HoldingFolderId,
                    type = "folder",
                    parent = new { id = RootId },
                    trashed_at = (string?)null
                });
            }

            if (request.Method == HttpMethod.Get && path == $"/2.0/folders/{HoldingFolderId}/items")
            {
                var entries = uploaded is null
                    ? Array.Empty<object>()
                    : [new
                    {
                        id = "holding-file",
                        name = "retained-file",
                        type = "file",
                        file_version = new { id = "holding-version" },
                        size = uploaded.LongLength,
                        content_type = "image/jpeg",
                        parent = new { id = HoldingFolderId }
                    }];
                return Json(new { entries });
            }

            if (request.Method == HttpMethod.Post && path == "/api/2.0/files/content")
            {
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                var file = multipart.First(part =>
                    string.Equals(part.Headers.ContentDisposition?.Name?.Trim('\"'), "file", StringComparison.Ordinal));
                uploaded = await file.ReadAsByteArrayAsync();
                UploadCount++;
                return Json(new
                {
                    entries = new[]
                    {
                        new
                        {
                            id = "holding-file",
                            type = "file",
                            file_version = new { id = "holding-version" },
                            parent = new { id = HoldingFolderId }
                        }
                    }
                });
            }

            if (request.Method == HttpMethod.Get && path == "/2.0/files/holding-file")
            {
                return Json(new
                {
                    id = "holding-file",
                    type = "file",
                    parent = new { id = HoldingFolderId },
                    trashed_at = (string?)null
                });
            }

            if (request.Method == HttpMethod.Get && path == "/2.0/files/holding-file/content")
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(uploaded ?? throw new InvalidOperationException("No Box content uploaded."))
                };
            }

            throw new InvalidOperationException($"Unexpected Box request: {request.Method} {request.RequestUri}");
        }

        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }

    private sealed class HoldingBoxHandler(HoldingBox box) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => box.HandleAsync(request);
    }
}
