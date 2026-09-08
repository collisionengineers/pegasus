using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

public sealed class DocumentContentCacheTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Corpus")]
    public async Task GenuineImageColdWarmExpiryAndExactVersionRehydrationAreMeasured()
    {
        var sample = GenuineMultiFormatCorpus.ReadSelected(".jpg", expectedHash: null);
        var estate = await Estate.CreateAsync(sample.Bytes);
        await using (estate)
        {
            const int iterations = 5;
            var cold = new Measurement[iterations];
            var warm = new Measurement[iterations];
            for (var index = 0; index < iterations; index++)
            {
                var downloadsBeforeCold = estate.Box.Downloads;
                cold[index] = await MeasureReadAsync(estate.Reader, estate.Request, sample.Bytes);
                Assert.Equal(downloadsBeforeCold + 1, estate.Box.Downloads);
                Assert.Equal("box-version-1", estate.Box.RequestedVersion);

                var downloadsBeforeWarm = estate.Box.Downloads;
                warm[index] = await MeasureReadAsync(estate.Reader, estate.Request, sample.Bytes);
                Assert.Equal(downloadsBeforeWarm, estate.Box.Downloads);

                await using var db = await estate.Database.CreateContextAsync();
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddTicks(-1);
                await db.SaveChangesAsync();
                var cleanup = await estate.Reader.ExecuteAsync(1, default);
                Assert.Equal(1, cleanup.Deleted);
            }

            var downloadsBeforeRehydrate = estate.Box.Downloads;
            await using var rehydrated = await estate.Reader.OpenAsync(estate.Request, default);
            Assert.Equal(sample.Bytes, await ReadAsync(rehydrated.Content));
            Assert.Equal(downloadsBeforeRehydrate + 1, estate.Box.Downloads);
            Assert.Equal("box-version-1", estate.Box.RequestedVersion);

            WriteMeasurements("cold", cold, sample.Bytes.LongLength);
            WriteMeasurements("warm", warm, sample.Bytes.LongLength);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdExactVersionBecomesWarmAndSuccessfulAccessExtendsIdleExpiry(bool systemWorker)
    {
        var bytes = "cached exact version"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            var request = systemWorker
                ? estate.Request with { Actor = ActionActor.SystemWorker("intake-processing") }
                : estate.Request;
            await using var cold = await estate.Reader.OpenAsync(request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(cold.Content));
            Assert.Equal(1, estate.Box.Downloads);
            Assert.Equal("box-version-1", estate.Box.RequestedVersion);

            estate.Clock.Advance(TimeSpan.FromHours(5));
            estate.Box.Unavailable = true;
            await using var warm = await estate.Reader.OpenAsync(request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(warm.Content));
            Assert.Equal(1, estate.Box.Downloads);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Equal(
                estate.Clock.GetUtcNow().AddHours(24),
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc);
        }
    }

    [Fact]
    public async Task AdministrationHealthReadsRecordedCacheFactsWithoutTouchingContent()
    {
        var bytes = "health cache bytes"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).LastCleanupOutcome =
                    "delete_failed";
                await db.SaveChangesAsync();
            }

            await using var scope = estate.Database.CreateAsyncScope();
            var health = scope.ServiceProvider
                .GetRequiredService<IAdministrationHealthMetricsQueries>();
            var metrics = await health.GetAsync(estate.Clock.GetUtcNow(), CancellationToken.None);

            Assert.Equal(1, metrics.CacheEntries);
            Assert.Equal(bytes.Length, metrics.CacheBytes);
            Assert.Equal(1, metrics.CacheCleanupFailures);
            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task ActiveReadLeasePreventsCleanupAndCorruptWarmBytesAreRefused()
    {
        var bytes = "race cache bytes"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(-1);
                entry.ReadLeaseExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            var cleanup = await estate.Reader.ExecuteAsync(10, CancellationToken.None);
            Assert.Equal(0, cleanup.Candidates);
            Assert.Equal(0, estate.Blob.DeleteCount);
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;
            await using (var concurrentWarm = await estate.Reader.OpenAsync(
                             estate.Request,
                             CancellationToken.None))
            {
                Assert.Equal(bytes, await ReadAsync(concurrentWarm.Content));
            }
            Assert.Equal(1, estate.Box.Downloads);

            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddHours(1);
                entry.ReadLeaseExpiresAtUtc = null;
                await db.SaveChangesAsync();
            }
            estate.Blob.Content = "corrupt"u8.ToArray();
            await Assert.ThrowsAsync<InvalidDataException>(
                () => estate.Reader.OpenAsync(estate.Request, CancellationToken.None));
        }
    }

    [Fact]
    public async Task CleanupRemovesSqlEntryWhenBlobIsAlreadyMissing()
    {
        var estate = await Estate.CreateAsync("missing cache"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(-1);
                await db.SaveChangesAsync();
            }
            estate.Blob.DeleteResult = false;

            var result = await estate.Reader.ExecuteAsync(10, CancellationToken.None);

            Assert.Equal(1, result.Deleted);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Empty(await verify.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    [Fact]
    public async Task MissingBlobRepublishedDuringActiveReadLeaseRecordsCompletedAccess()
    {
        var bytes = "republished under read lease"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddHours(1);
                entry.ReadLeaseExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            estate.Blob.Content = null;
            estate.Blob.MissingContentIsNotFound = true;

            await using var republished = await estate.Reader.OpenAsync(estate.Request, default);
            Assert.Equal(bytes, await ReadAsync(republished.Content));
            Assert.Equal(2, estate.Box.Downloads);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Equal(
                estate.Clock.GetUtcNow().AddHours(24),
                (await verify.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc);
        }
    }

    [Fact]
    public async Task DisabledStaffCannotReadPreviouslyWarmContent()
    {
        var estate = await Estate.CreateAsync("revoked cache read"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Users.SingleAsync(value => value.Id == estate.StaffId)).IsEnabled = false;
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await Assert.ThrowsAsync<StaffAuthorizationException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task StaffWithoutCurrentRoleCannotReadPreviouslyWarmContent()
    {
        var estate = await Estate.CreateAsync("removed role cache read"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                db.UserRoles.RemoveRange(db.UserRoles.Where(value => value.UserId == estate.StaffId));
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await Assert.ThrowsAsync<StaffAuthorizationException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupPreconditionConflictReleasesClaimAndRecordsBoundedFailure(bool corruptReplacement)
    {
        var estate = await Estate.CreateAsync("cleanup conflict"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc =
                    estate.Clock.GetUtcNow().AddMinutes(-1);
                await db.SaveChangesAsync();
            }
            estate.Blob.DeletePreconditionFails = true;
            estate.Blob.ReplaceContent(corruptReplacement ? "changed replacement"u8.ToArray() : estate.Blob.Content!);

            var result = await estate.Reader.ExecuteAsync(10, default);

            Assert.Equal(1, result.Failures);
            Assert.Equal(0, result.Deleted);
            await using var verify = await estate.Database.CreateContextAsync();
            var retained = await verify.Set<DocumentContentCacheEntryEntity>().SingleAsync();
            Assert.Null(retained.ReadLeaseExpiresAtUtc);
            Assert.Equal("RequestFailedException:412", retained.LastCleanupOutcome);
            Assert.Equal(corruptReplacement ? "\"1\"" : "\"2\"", retained.ETag);
            estate.Blob.DeletePreconditionFails = false;
            var retry = await estate.Reader.ExecuteAsync(10, default);
            Assert.Equal(corruptReplacement ? 0 : 1, retry.Deleted);
            Assert.Equal(corruptReplacement ? 1 : 0, retry.Failures);
        }
    }

    [Fact]
    public async Task ConcurrentColdMissesConvergeOnOneVerifiedCacheEntry()
    {
        var bytes = "concurrent cache miss"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            var firstTask = estate.Reader.OpenAsync(estate.Request, default);
            var secondTask = estate.Reader.OpenAsync(estate.Request, default);
            var opened = await Task.WhenAll(firstTask, secondTask);
            await using var first = opened[0];
            await using var second = opened[1];

            Assert.Equal(bytes, await ReadAsync(first.Content));
            Assert.Equal(bytes, await ReadAsync(second.Content));
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Single(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
            Assert.Equal(1, estate.Blob.UploadCount);
        }
    }

    [Fact]
    public async Task VerifiedBlobFromInterruptedPublishIsAdoptedOnRetry()
    {
        var bytes = "interrupted cache publish"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            estate.Blob.Content = bytes;

            await using var opened = await estate.Reader.OpenAsync(estate.Request, default);

            Assert.Equal(bytes, await ReadAsync(opened.Content));
            Assert.Equal(0, estate.Blob.UploadCount);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Single(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    [Fact]
    public async Task ColdProviderFailureDoesNotPublishCacheState()
    {
        var estate = await Estate.CreateAsync("cold provider failure"u8.ToArray());
        await using (estate)
        {
            estate.Box.Unavailable = true;

            await Assert.ThrowsAnyAsync<Exception>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Null(estate.Blob.Content);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    [Fact]
    public async Task LogicallyRemovedVersionCannotUsePreviouslyWarmContent()
    {
        var bytes = "removed document version"u8.ToArray();
        var estate = await Estate.CreateDocumentAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentVersionEntity>().SingleAsync()).IsLogicallyRemoved = true;
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task FailedVersionWithRemoteIdsIsRejectedBeforeContentRead()
    {
        var estate = await Estate.CreateDocumentAsync("failed document version"u8.ToArray());
        await using (estate)
        {
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentVersionEntity>().SingleAsync()).CustodyStatus =
                    DocumentCustodyStatus.Failed;
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(0, estate.Box.Downloads);
        }
    }

    private static async Task<byte[]> ReadAsync(Stream stream)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private static async Task<Measurement> MeasureReadAsync(
        CachedDocumentContentStore reader,
        ReadLogicalDocumentVersionRequest request,
        byte[] expected)
    {
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        var started = Stopwatch.GetTimestamp();
        await using var content = await reader.OpenAsync(request, default);
        Assert.Equal(expected, await ReadAsync(content.Content));
        return new(
            Stopwatch.GetElapsedTime(started),
            Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore));
    }

    private void WriteMeasurements(string phase, Measurement[] values, long contentLength)
    {
        var elapsed = values.Select(value => value.Elapsed.TotalMilliseconds).Order().ToArray();
        var allocated = values.Select(value => value.AllocatedBytes).Order().ToArray();
        output.WriteLine(
            "cache-{0}: sample-count={1}; content-bytes={2}; elapsed-ms-p50={3:F3}; elapsed-ms-p95={4:F3}; process-allocated-bytes-p50={5}; process-allocated-bytes-p95={6}",
            phase,
            values.Length,
            contentLength,
            Percentile(elapsed, 0.50),
            Percentile(elapsed, 0.95),
            Percentile(allocated, 0.50),
            Percentile(allocated, 0.95));
    }

    private static T Percentile<T>(T[] sorted, double percentile) =>
        sorted[(int)Math.Ceiling(percentile * sorted.Length) - 1];

    private sealed record Measurement(TimeSpan Elapsed, long AllocatedBytes);

    private sealed class Estate : IAsyncDisposable
    {
        private Estate(LocalDbTestDatabase database, CachedDocumentContentStore reader,
            CacheBlob blob, BoxHandler box, MutableTimeProvider clock,
            ReadLogicalDocumentVersionRequest request,
            IAsyncDisposable? readerScope = null)
        {
            Database = database; Reader = reader; Blob = blob; Box = box; Clock = clock;
            Request = request; this.readerScope = readerScope;
        }
        private readonly IAsyncDisposable? readerScope;
        public LocalDbTestDatabase Database { get; }
        public CachedDocumentContentStore Reader { get; }
        public CacheBlob Blob { get; }
        public BoxHandler Box { get; }
        public MutableTimeProvider Clock { get; }
        public ReadLogicalDocumentVersionRequest Request { get; }
        public Guid StaffId { get; private init; }

        public static async Task<Estate> CreateAsync(byte[] bytes)
        {
            var blob=new CacheBlob();
            var container=new CacheContainer(blob);
            var box=new BoxHandler(bytes);
            var options=BoxCustodyOptions.Create("https://api.box.com/2.0/","https://upload.box.com/api/2.0/",
                "405543781910","""{"boxAppSettings":{"clientID":"x","appAuth":{"publicKeyID":"x","privateKey":"x","passphrase":"x"}},"enterpriseID":"x"}""","x","holding");
            var clock=new MutableTimeProvider(new DateTimeOffset(2031,1,1,0,0,0,TimeSpan.Zero));
            var database = await LocalDbTestDatabase.CreateAsync(configureServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddProductionDocumentStorage(_ => container, _ => false, _ => options);
                services.RemoveAll<IBoxAuthorizationHeaderProvider>();
                services.AddSingleton<IBoxAuthorizationHeaderProvider>(new Header());
                services.AddHttpClient(nameof(BoxContentClient))
                    .ConfigurePrimaryHttpMessageHandler(() => box);
            });
            var receiptId = Guid.NewGuid(); var assetId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await using (var db = await database.CreateContextAsync())
            {
                db.Add(new IntakeReceiptEntity
                {
                    Id=receiptId, SourceFileName="source.bin", MediaType="application/octet-stream",
                    SourceLength=bytes.Length, SourceHash=hash, SourceChannel="manual_upload",
                    ExternalReceiptToken=$"cache:{receiptId:N}", ReceivedAtUtc=DateTimeOffset.UtcNow,
                    ProcessedAtUtc=DateTimeOffset.UtcNow, SourceReaderKey="test", SourceReaderVersion="1",
                    Decision="unidentified", DecisionReason="Test.", EvidenceJson="[]", FieldsJson="[]",
                    OcrCandidatesJson="[]"
                });
                db.Add(new PegasusIdentityUser
                {
                    Id = staffId,
                    UserName = $"cache-{staffId:N}@example.invalid",
                    NormalizedUserName = $"CACHE-{staffId:N}@EXAMPLE.INVALID",
                    Email = $"cache-{staffId:N}@example.invalid",
                    NormalizedEmail = $"CACHE-{staffId:N}@EXAMPLE.INVALID",
                    IsEnabled = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                var roleId = await db.Roles.Where(role => role.Name == StaffRoleNames.Engineer).Select(role => role.Id).SingleAsync();
                db.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = roleId });
                db.Add(new IntakeAssetEntity
                {
                    Id=assetId, IntakeReceiptId=receiptId, SourceLabel="source", FileName="source.bin",
                    MediaType="application/octet-stream", Kind="source", Disposition="source",
                    ContentLength=bytes.Length, ContentHash=hash, StorageKey="staging",
                    BoxFileId="box-file-1", BoxVersionId="box-version-1", CustodyStatus="confirmed"
                });
                await db.SaveChangesAsync();
            }
            var scope = database.CreateAsyncScope();
            var logicalReader=scope.ServiceProvider.GetRequiredService<IReadLogicalDocumentVersion>();
            var reader=Assert.IsType<CachedDocumentContentStore>(logicalReader);
            Assert.Same(reader, scope.ServiceProvider.GetRequiredService<IDocumentContentCacheCleanup>());
            return new Estate(database,reader,blob,box,clock,
                new(ActionActor.Staff(staffId,[StaffRole.Engineer]),null,null,assetId,null,receiptId,hash,bytes.Length),
                scope)
                { StaffId = staffId };
        }

        public static async Task<Estate> CreateDocumentAsync(byte[] bytes)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            var staffId = Guid.NewGuid();

            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await using (var db = await database.CreateContextAsync())
            {
                var principal = await SeededPrincipals.QdosAsync(db);
                db.Add(new PegasusIdentityUser
                {
                    Id = staffId,
                    UserName = $"cache-{staffId:N}@example.invalid",
                    NormalizedUserName = $"CACHE-{staffId:N}@EXAMPLE.INVALID",
                    IsEnabled = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                var roleId = await db.Roles.Where(role => role.Name == StaffRoleNames.Engineer).Select(role => role.Id).SingleAsync();
                db.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = roleId });
                db.Add(new IntakeReceiptEntity
                {
                    Id = receiptId, SourceFileName = "source.bin", MediaType = "application/octet-stream",
                    SourceLength = bytes.Length, SourceHash = hash, SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"cache-document:{receiptId:N}", ReceivedAtUtc = DateTimeOffset.UtcNow,
                    ProcessedAtUtc = DateTimeOffset.UtcNow, SourceReaderKey = "test", SourceReaderVersion = "1",
                    Decision = "case_created", DecisionReason = "Test.", EvidenceJson = "[]", FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                });
                db.Add(new CaseEntity
                {
                    Id = caseId, PrincipalId = principal.Id, SequenceLineageId = principal.SequenceLineageId,
                    Year = 2031, Sequence = 91, Reference = "QDOS091", Type = "Inspection",
                    InitialState = "NotReady", CustodyState = "confirmed", OriginIntakeReceiptId = receiptId,
                    CustodyRootRemoteId = "holding", CreatedAtUtc = DateTimeOffset.UtcNow,
                    ConcurrencyToken = Guid.NewGuid()
                });
                db.Add(new CaseDocumentEntity
                {
                    Id = documentId, CaseId = caseId, Ordinal = 1, SourceOccurrenceIdentity = "document-cache"
                });
                db.Add(new DocumentVersionEntity
                {
                    Id = versionId, DocumentId = documentId, Version = 1, FileName = "source.bin",
                    MediaType = "application/octet-stream", ContentLength = bytes.Length, Sha256 = hash,
                    BoxFileId = "box-file-1", BoxVersionId = "box-version-1",
                    CustodyStatus = DocumentCustodyStatus.Confirmed, CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = "test", IsCurrent = true
                });
                await db.SaveChangesAsync();
            }
            await using var scope = database.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            var blob = new CacheBlob();
            var box = new BoxHandler(bytes);
            var options = BoxCustodyOptions.Create(
                "https://api.box.com/2.0/", "https://upload.box.com/api/2.0/", "405543781910",
                """{"boxAppSettings":{"clientID":"x","appAuth":{"publicKeyID":"x","privateKey":"x","passphrase":"x"}},"enterpriseID":"x"}""",
                "x", "holding");
            var clock = new MutableTimeProvider(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var reader = new CachedDocumentContentStore(
                factory, new CacheContainer(blob), new BoxContentClient(options, new HttpClient(box), new Header()), clock);
            return new Estate(database, reader, blob, box, clock,
                new(ActionActor.Staff(staffId, [StaffRole.Engineer]), documentId, versionId, null,
                    caseId, null, hash, bytes.Length))
                { StaffId = staffId };
        }
        public async ValueTask DisposeAsync()
        {
            if (readerScope is not null) await readerScope.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private sealed class CacheContainer(CacheBlob blob) : BlobContainerClient
    {
        public override BlobClient GetBlobClient(string blobName) { blob.BlobName=blobName; return blob; }
    }
    private sealed class CacheBlob : BlobClient
    {
        private ETag etag = new("\"1\"");
        public string BlobName { get; set; }="";
        public byte[]? Content { get; set; }
        public int DeleteCount { get; private set; }
        public int UploadCount { get; private set; }
        public bool DeleteResult { get; set; } = true;
        public bool MissingContentIsNotFound { get; set; }
        public bool DeletePreconditionFails { get; set; }
        public void ReplaceContent(byte[] content) { Content = content; etag = new ETag("\"2\""); }
        public override string Name=>BlobName;
        public override Task<Response<BlobContentInfo>> UploadAsync(Stream content,BlobUploadOptions options,CancellationToken token=default)
        {
            lock (this)
            {
                if (Content is not null)
                {
                    return Task.FromException<Response<BlobContentInfo>>(
                        new RequestFailedException(412, "condition"));
                }
                using var m=new MemoryStream();
                content.CopyTo(m);
                Content=m.ToArray();
                UploadCount++;
                return Task.FromResult(Response.FromValue(
                    BlobsModelFactory.BlobContentInfo(etag,DateTimeOffset.UtcNow,null,null,0),
                    new StubResponse()));
            }
        }
        public override Task<Response<BlobProperties>> GetPropertiesAsync(BlobRequestConditions? conditions=null,CancellationToken token=default)=>
            Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(contentLength:Content?.LongLength??0,eTag:etag,metadata:new Dictionary<string,string>{{"sha256",Convert.ToHexString(SHA256.HashData(Content??[])).ToLowerInvariant()}}),new StubResponse()));
        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(BlobDownloadOptions? options=null,CancellationToken token=default)
        {
            if (Content is null && MissingContentIsNotFound)
            {
                return Task.FromException<Response<BlobDownloadStreamingResult>>(
                    new RequestFailedException(404, "missing"));
            }
            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobDownloadStreamingResult(new MemoryStream(Content ?? [])),
                new StubResponse()));
        }
        public override Task<Response<bool>> DeleteIfExistsAsync(DeleteSnapshotsOption option=default,BlobRequestConditions? conditions=null,CancellationToken token=default)
        {
            DeleteCount++;
            if (DeletePreconditionFails || (conditions?.IfMatch is { } expected && expected != etag))
            {
                return Task.FromException<Response<bool>>(new RequestFailedException(412, "condition"));
            }
            Content=null;
            return Task.FromResult(Response.FromValue(DeleteResult,new StubResponse()));
        }
    }
    private sealed class BoxHandler(byte[] bytes) : HttpMessageHandler
    {
        public int Downloads { get; private set; } public string? RequestedVersion { get; private set; } public bool Unavailable { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/content",StringComparison.Ordinal)){ Downloads++; RequestedVersion=System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["version"]; if(Unavailable)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}); }
            var body=path.Contains("/files/",StringComparison.Ordinal)
                ? """{"id":"box-file-1","type":"file","parent":{"id":"holding"}}"""
                : path.Contains("/folders/holding",StringComparison.Ordinal)
                    ? """{"id":"holding","type":"folder","parent":{"id":"405543781910"}}"""
                    : """{"id":"405543781910","type":"folder"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")});
        }
    }
    private sealed class Header : IBoxAuthorizationHeaderProvider { public Task<string> GetAuthorizationHeaderAsync(CancellationToken token)=>Task.FromResult("Bearer x"); }
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow()=>now; public void Advance(TimeSpan value)=>now=now.Add(value); }
    private sealed class StubResponse : Response
    {
        public override int Status=>200; public override string ReasonPhrase=>"OK"; public override Stream? ContentStream{get;set;} public override string ClientRequestId{get;set;}="";
        public override void Dispose(){} protected override bool ContainsHeader(string name)=>false; protected override IEnumerable<HttpHeader> EnumerateHeaders()=>[]; protected override bool TryGetHeader(string name,out string value){value="";return false;} protected override bool TryGetHeaderValues(string name,out IEnumerable<string> values){values=[];return false;}
    }
}
