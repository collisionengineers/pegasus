using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class CaseArtifactCustodyRecoveryTests
{
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
                .SingleAsync();
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
        var artifacts = new CountingArtifactStore();
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
            var content = "generated report bytes"u8.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var store = new FailFirstContentStore();
            var custody = new EfCaseArtifactCustody(
                factory, store, intake, TimeProvider.System);
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
            Guid pendingVersionId;
            await using (var db = await database.CreateContextAsync())
            {
                var pending = await db.Set<DocumentVersionEntity>().SingleAsync();
                pendingVersionId = pending.Id;
                Assert.Equal(DocumentCustodyStatus.Pending, pending.CustodyStatus);
                Assert.Single(await db.Set<DocumentOccurrenceEntity>().ToArrayAsync());
            }

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
                var confirmed = await db.Set<DocumentVersionEntity>().SingleAsync();
                confirmed.IsLogicallyRemoved = true;
                await db.SaveChangesAsync();
                await Assert.ThrowsAsync<FileNotFoundException>(() => custody.GetAsync(
                    actor, caseId, confirmed.DocumentId, confirmed.Id, default));
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

    [Fact]
    public async Task StatusPreservesFailedCustodyDisposition()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
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
            default);

        Assert.Equal(CaseArtifactCustodyDisposition.Failed, result.Disposition);
        Assert.Equal("case_custody_failed", result.FailureCode);
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
                Type = "Inspection",
                InitialState = "NotReady",
                CustodyState = "confirmed",
                OriginIntakeReceiptId = receiptId,
                CustodyRootRemoteId = "case-root",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ConcurrencyToken = Guid.NewGuid()
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

        public Task StoreAsync(Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content, string expectedSha256, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(Guid caseId, string caseReference, Guid versionId, string expectedSha256, long expectedLength, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DeleteAsync(Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A failed save must not delete remote content.");
    }

    private sealed class CountingArtifactStore : IIntakeArtifactStore
    {
        public int ReadCount { get; private set; }
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<ReadOnlyMemory<byte>?>(null);
        }
    }
}
