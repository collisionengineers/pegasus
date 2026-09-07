using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

public interface IDocumentContentCacheCleanup
{
    Task<DocumentContentCacheCleanupResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken);
}

public sealed record DocumentContentCacheCleanupResult(
    int Candidates,
    int Deleted,
    int Retained,
    int Failures);

public sealed class NoDocumentContentCacheCleanup : IDocumentContentCacheCleanup
{
    public Task<DocumentContentCacheCleanupResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        return Task.FromResult(new DocumentContentCacheCleanupResult(0, 0, 0, 0));
    }
}

public sealed class DocumentContentCacheMetrics : IDocumentContentCacheMetrics
{
    private long hits;
    private long misses;

    public DocumentContentCacheMetricSnapshot Snapshot() =>
        new(Interlocked.Read(ref hits), Interlocked.Read(ref misses));

    public void RecordHit() => Interlocked.Increment(ref hits);

    public void RecordMiss() => Interlocked.Increment(ref misses);
}

internal sealed class CachedDocumentContentStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    BlobContainerClient container,
    BoxContentClient box,
    TimeProvider timeProvider,
    IDocumentContentCacheMetrics? metrics = null)
    : IReadLogicalDocumentVersion, IDocumentContentCacheCleanup
{
    private static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ReadLeaseLifetime = TimeSpan.FromMinutes(2);
    private const string CachePrefix = "cache/";
    private const string HashMetadata = "sha256";

    public async Task<LogicalDocumentContent> OpenAsync(
        ReadLogicalDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        StaffAuthorization.Require(
            request.Actor,
            request.Actor.Kind == ActorKind.SystemWorker
                ? StaffAccessRight.ExecuteSystemWork
                : StaffAccessRight.PerformCasework);
        await RequireCurrentActorAsync(request.Actor, cancellationToken);
        var source = await ResolveAuthorizedSourceAsync(request, cancellationToken);
        if (source.Length != request.ExpectedContentLength
            || !FixedHashEquals(source.Sha256, request.ExpectedSha256))
        {
            throw new InvalidDataException("The requested logical content identity does not match durable metadata.");
        }
        var now = timeProvider.GetUtcNow();

        var cached = await TryOpenCachedAsync(source, now, cancellationToken);
        if (cached is not null)
        {
            metrics?.RecordHit();
            return Result(request, source, cached);
        }
        metrics?.RecordMiss();

        await using var remote = await box.OpenOwnedVersionReadAsync(
            source.BoxFileId,
            source.BoxVersionId,
            source.ExpectedParentId,
            source.Length,
            cancellationToken);
        var downloaded = await ReadVerifiedToTemporaryAsync(
            remote, source.Length, source.Sha256, cancellationToken);
        try
        {
            await PublishAsync(source, downloaded, cancellationToken);
            downloaded.Position = 0;
            return Result(request, source, downloaded);
        }
        catch
        {
            await downloaded.DisposeAsync();
            throw;
        }
    }

    public async Task<DocumentContentCacheCleanupResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await db.Set<DocumentContentCacheEntryEntity>()
            .Where(value => value.ExpiresAtUtc <= now
                && (value.ReadLeaseExpiresAtUtc == null || value.ReadLeaseExpiresAtUtc <= now))
            .OrderBy(value => value.ExpiresAtUtc)
            .ThenBy(value => value.Id)
            .Take(maximumItems)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        var deleted = 0;
        var retained = 0;
        var failures = 0;
        foreach (var candidate in candidates)
        {
            var cleanupToken = Guid.NewGuid();
            try
            {
                await using var itemDb = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var claimed = await itemDb.Set<DocumentContentCacheEntryEntity>()
                    .Where(value => value.Id == candidate.Id
                        && value.ConcurrencyToken == candidate.ConcurrencyToken
                        && value.ExpiresAtUtc <= now
                        && (value.ReadLeaseExpiresAtUtc == null || value.ReadLeaseExpiresAtUtc <= now))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(value => value.ReadLeaseExpiresAtUtc, now.Add(ReadLeaseLifetime))
                        .SetProperty(value => value.ConcurrencyToken, cleanupToken),
                        cancellationToken);
                if (claimed == 0)
                {
                    retained++;
                    continue;
                }
                var response = await container.GetBlobClient(candidate.BlobIdentity)
                    .DeleteIfExistsAsync(
                        DeleteSnapshotsOption.IncludeSnapshots,
                        conditions: candidate.ETag is { Length: > 0 }
                            ? new BlobRequestConditions { IfMatch = new ETag(candidate.ETag) }
                            : null,
                        cancellationToken);
                var removed = await itemDb.Set<DocumentContentCacheEntryEntity>()
                    .Where(value => value.Id == candidate.Id
                        && value.ConcurrencyToken == cleanupToken)
                    .ExecuteDeleteAsync(cancellationToken);
                if (removed == 1)
                {
                    deleted++;
                }
                else
                {
                    retained++;
                }
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                await using var missingDb = await dbContextFactory.CreateDbContextAsync(
                    CancellationToken.None);
                var removed = await missingDb.Set<DocumentContentCacheEntryEntity>()
                    .Where(value => value.Id == candidate.Id
                        && value.ConcurrencyToken == cleanupToken)
                    .ExecuteDeleteAsync(CancellationToken.None);
                if (removed == 1) deleted++; else retained++;
            }
            catch (RequestFailedException exception) when (exception.Status == 412)
            {
                string? currentETag = null;
                try
                {
                    var properties = (await container.GetBlobClient(candidate.BlobIdentity)
                        .GetPropertiesAsync(cancellationToken: CancellationToken.None))
                        .Value;
                    if (properties.ContentLength == candidate.VerifiedSize
                        && properties.Metadata.TryGetValue(HashMetadata, out var currentHash)
                        && FixedHashEquals(currentHash, candidate.VerifiedSha256))
                    {
                        currentETag = properties.ETag.ToString();
                    }
                }
                catch (RequestFailedException propertiesFailure) when (propertiesFailure.Status == 404)
                {
                    // The next cleanup pass can remove the now-missing object and row.
                }
                await using var conflictDb = await dbContextFactory.CreateDbContextAsync(
                    CancellationToken.None);
                var released = await conflictDb.Set<DocumentContentCacheEntryEntity>()
                    .Where(value => value.Id == candidate.Id
                        && value.ConcurrencyToken == cleanupToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(value => value.LastCleanupOutcome, "RequestFailedException:412")
                        .SetProperty(value => value.ETag, currentETag ?? candidate.ETag)
                        .SetProperty(value => value.ReadLeaseExpiresAtUtc, (DateTimeOffset?)null)
                        .SetProperty(value => value.ConcurrencyToken, Guid.NewGuid()),
                        CancellationToken.None);
                if (released == 0)
                {
                    retained++;
                }
                else
                {
                    failures++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await using var failureDb = await dbContextFactory.CreateDbContextAsync(
                    CancellationToken.None);
                var outcome = exception is RequestFailedException requestFailure
                    ? $"{exception.GetType().Name}:{requestFailure.Status}"
                    : exception.GetType().Name;
                var recorded = await failureDb.Set<DocumentContentCacheEntryEntity>()
                    .Where(value => value.Id == candidate.Id
                        && value.ConcurrencyToken == cleanupToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(value => value.LastCleanupOutcome, outcome)
                        .SetProperty(value => value.ReadLeaseExpiresAtUtc, (DateTimeOffset?)null)
                        .SetProperty(value => value.ConcurrencyToken, Guid.NewGuid()),
                        CancellationToken.None);
                if (recorded == 0)
                {
                    throw new IOException(
                        "The cache cleanup failure could not be recorded because its lease was lost.",
                        exception);
                }
                failures++;
            }
        }
        return new(candidates.Length, deleted, retained, failures);
    }

    private async Task RequireCurrentActorAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Kind != ActorKind.Staff)
        {
            return;
        }
        if (!Guid.TryParse(actor.SubjectId, out var staffId))
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var enabled = await db.Users.AsNoTracking()
            .Where(value => value.Id == staffId)
            .Select(value => (bool?)value.IsEnabled)
            .SingleOrDefaultAsync(cancellationToken);
        if (enabled is not true)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        var currentRoleNames = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == staffId
            select role.Name).ToArrayAsync(cancellationToken);
        var roles = currentRoleNames
            .Select(value => value switch
            {
                StaffRoleNames.Administrator => (StaffRole?)StaffRole.Administrator,
                StaffRoleNames.Engineer => StaffRole.Engineer,
                StaffRoleNames.User => StaffRole.User,
                _ => null
            })
            .OfType<StaffRole>()
            .ToArray();
        if (roles.Length == 0)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        StaffAuthorization.Require(
            ActionActor.Staff(staffId, roles),
            StaffAccessRight.PerformCasework);
    }

    private async Task<ResolvedSource> ResolveAuthorizedSourceAsync(
        ReadLogicalDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (request.IntakeAssetId is { } assetId)
        {
            var asset = await db.Set<IntakeAssetEntity>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == assetId, cancellationToken)
                ?? throw new FileNotFoundException("The retained intake source is unavailable.");
            if (asset.IntakeReceiptId != request.IntakeReceiptId)
            {
                throw new UnauthorizedAccessException("The intake source does not belong to the authorized receipt.");
            }
            string expectedParentId = box.HoldingFolderId;
            if (request.CaseId is { } caseId)
            {
                var associatedCase = await db.Cases.AsNoTracking()
                    .Where(
                    value => value.Id == caseId
                        && (value.OriginIntakeReceiptId == asset.IntakeReceiptId
                            || value.IntakeLinks.Any(link => link.IntakeReceiptId == asset.IntakeReceiptId)))
                    .Select(value => value.CustodyRootRemoteId)
                    .SingleOrDefaultAsync(cancellationToken);
                var manuallyAssociated = await db.Set<IntakeManualAssociationEntity>().AnyAsync(
                    value => value.CaseId == caseId
                        && value.IntakeReceiptId == asset.IntakeReceiptId
                        && value.IsActive,
                    cancellationToken);
                if (associatedCase is null && !manuallyAssociated)
                {
                    throw new UnauthorizedAccessException("The intake source is not associated with the authorized Case.");
                }
                expectedParentId = associatedCase
                    ?? await db.Cases.Where(value => value.Id == caseId)
                        .Select(value => value.CustodyRootRemoteId)
                        .SingleAsync(cancellationToken)
                    ?? throw new FileNotFoundException("The authorized Case custody root is unavailable.");
            }
            return ResolvedSource.Create(
                documentVersionId: null,
                asset.Id,
                asset.BoxFileId,
                asset.BoxVersionId,
                asset.ContentHash,
                asset.ContentLength,
                asset.FileName,
                asset.MediaType,
                expectedParentId);
        }

        var version = await (
            from documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
            join document in db.Set<CaseDocumentEntity>().AsNoTracking()
                on documentVersion.DocumentId equals document.Id
            join caseEntity in db.Cases.AsNoTracking()
                on document.CaseId equals caseEntity.Id
            where documentVersion.Id == request.VersionId
                && documentVersion.DocumentId == request.DocumentId
                && document.CaseId == request.CaseId
                && documentVersion.CustodyStatus == DocumentCustodyStatus.Confirmed
                && !documentVersion.IsLogicallyRemoved
            select new
            {
                Version = documentVersion,
                CaseRootRemoteId = caseEntity.CustodyRootRemoteId
            }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new FileNotFoundException("The authorized document version is unavailable.");
        return ResolvedSource.Create(
            version.Version.Id,
            intakeAssetId: null,
            version.Version.BoxFileId,
            version.Version.BoxVersionId,
            version.Version.Sha256,
            version.Version.ContentLength,
            version.Version.FileName,
            version.Version.MediaType,
            version.CaseRootRemoteId);
    }

    private async Task<Stream?> TryOpenCachedAsync(
        ResolvedSource source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await CacheQuery(db, source).SingleOrDefaultAsync(cancellationToken);
        if (entry is null || entry.ExpiresAtUtc <= now)
        {
            return null;
        }
        if (entry.ReadLeaseExpiresAtUtc > now)
        {
            return await OpenUnderSharedReadLeaseAsync(entry, source, cancellationToken);
        }
        var leaseExpiry = now.Add(ReadLeaseLifetime);
        var leaseToken = Guid.NewGuid();
        var touched = await db.Set<DocumentContentCacheEntryEntity>()
            .Where(value => value.Id == entry.Id
                && value.ConcurrencyToken == entry.ConcurrencyToken
                && value.ExpiresAtUtc > now
                && (value.ReadLeaseExpiresAtUtc == null
                    || value.ReadLeaseExpiresAtUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.ReadLeaseExpiresAtUtc, leaseExpiry)
                .SetProperty(value => value.ConcurrencyToken, leaseToken),
                cancellationToken);
        if (touched == 0)
        {
            return null;
        }
        try
        {
            var blob = container.GetBlobClient(entry.BlobIdentity);
            var response = await blob.DownloadStreamingAsync(
                new BlobDownloadOptions
                {
                    Conditions = entry.ETag is { Length: > 0 }
                        ? new BlobRequestConditions { IfMatch = new ETag(entry.ETag) }
                        : null
                },
                cancellationToken);
            await using var content = response.Value.Content;
            var verified = await ReadVerifiedToTemporaryAsync(
                content, source.Length, source.Sha256, cancellationToken);
            var completedAtUtc = timeProvider.GetUtcNow();
            var completed = await db.Set<DocumentContentCacheEntryEntity>()
                .Where(value => value.Id == entry.Id
                    && value.ConcurrencyToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.ExpiresAtUtc, completedAtUtc.Add(IdleLifetime))
                    .SetProperty(value => value.LastCleanupOutcome, (string?)null)
                    .SetProperty(value => value.ReadLeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(value => value.ConcurrencyToken, Guid.NewGuid()),
                    cancellationToken);
            if (completed == 0)
            {
                await verified.DisposeAsync();
                throw new IOException("The cache read lease was lost before access could be recorded.");
            }
            return verified;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return null;
        }
        finally
        {
            await db.Set<DocumentContentCacheEntryEntity>()
                .Where(value => value.Id == entry.Id
                    && value.ConcurrencyToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.ReadLeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(value => value.ConcurrencyToken, Guid.NewGuid()),
                    CancellationToken.None);
        }
    }

    private async Task<Stream?> OpenUnderSharedReadLeaseAsync(
        DocumentContentCacheEntryEntity entry,
        ResolvedSource source,
        CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(entry.BlobIdentity);
        Azure.Response<BlobDownloadStreamingResult> response;
        try
        {
            response = await blob.DownloadStreamingAsync(
                new BlobDownloadOptions
                {
                    Conditions = entry.ETag is { Length: > 0 }
                        ? new BlobRequestConditions { IfMatch = new ETag(entry.ETag) }
                        : null
                },
                cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return null;
        }
        await using var content = response.Value.Content;
        var verified = await ReadVerifiedToTemporaryAsync(
            content, source.Length, source.Sha256, cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var completedAt = timeProvider.GetUtcNow();
        var touched = await db.Set<DocumentContentCacheEntryEntity>()
            .Where(value => value.Id == entry.Id
                && value.ETag == entry.ETag
                && value.ReadLeaseExpiresAtUtc > completedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.ExpiresAtUtc, completedAt.Add(IdleLifetime))
                .SetProperty(value => value.LastCleanupOutcome, (string?)null),
                cancellationToken);
        if (touched == 0)
        {
            await verified.DisposeAsync();
            throw new IOException("The shared cache read lease was lost before access could be recorded.");
        }
        return verified;
    }

    private async Task PublishAsync(
        ResolvedSource source,
        Stream content,
        CancellationToken cancellationToken)
    {
        var identity = source.DocumentVersionId is { } versionId
            ? $"{CachePrefix}document-versions/{versionId:D}"
            : $"{CachePrefix}intake-assets/{source.IntakeAssetId!.Value:D}";
        var blob = container.GetBlobClient(identity);
        try
        {
            content.Position = 0;
            await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                    Metadata = new Dictionary<string, string> { [HashMetadata] = source.Sha256 }
                },
                cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 412)
        {
            // A concurrent miss published the same logical identity. Verify it below.
        }
        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        if (properties.Value.ContentLength != source.Length
            || !properties.Value.Metadata.TryGetValue(HashMetadata, out var hash)
            || !FixedHashEquals(hash, source.Sha256))
        {
            throw new InvalidDataException("The published cache object failed integrity verification.");
        }
        var completedAt = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await CacheQuery(db, source).SingleOrDefaultAsync(cancellationToken);
        if (entry is null)
        {
            db.Add(new DocumentContentCacheEntryEntity
            {
                Id = Guid.NewGuid(),
                DocumentVersionId = source.DocumentVersionId,
                IntakeAssetId = source.IntakeAssetId,
                BlobIdentity = identity,
                ETag = properties.Value.ETag.ToString(),
                VerifiedSha256 = source.Sha256,
                VerifiedSize = source.Length,
                ExpiresAtUtc = completedAt.Add(IdleLifetime),
                ConcurrencyToken = Guid.NewGuid()
            });
        }
        else if (entry.ReadLeaseExpiresAtUtc is null
            || entry.ReadLeaseExpiresAtUtc <= completedAt)
        {
            entry.BlobIdentity = identity;
            entry.ETag = properties.Value.ETag.ToString();
            entry.VerifiedSha256 = source.Sha256;
            entry.VerifiedSize = source.Length;
            entry.ExpiresAtUtc = completedAt.Add(IdleLifetime);
            entry.LastCleanupOutcome = null;
            entry.ReadLeaseExpiresAtUtc = null;
            entry.ConcurrencyToken = Guid.NewGuid();
        }
        else
        {
            if (entry.ExpiresAtUtc <= completedAt)
            {
                throw new IOException(
                    "The cache cleanup lease must finish before publication can be recorded.");
            }
            var touched = await db.Set<DocumentContentCacheEntryEntity>()
                .Where(value => value.Id == entry.Id
                    && value.ETag == entry.ETag
                    && value.ReadLeaseExpiresAtUtc > completedAt
                    && value.ExpiresAtUtc > completedAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.ExpiresAtUtc, completedAt.Add(IdleLifetime))
                    .SetProperty(value => value.LastCleanupOutcome, (string?)null),
                    cancellationToken);
            if (touched == 0)
            {
                throw new IOException(
                    "The cache read lease changed before publication could record access.");
            }
            return;
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await CacheQuery(db, source)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (concurrent is null
                || concurrent.VerifiedSize != source.Length
                || !FixedHashEquals(concurrent.VerifiedSha256, source.Sha256)
                || !string.Equals(concurrent.BlobIdentity, identity, StringComparison.Ordinal))
            {
                throw;
            }
            var recordedAt = timeProvider.GetUtcNow();
            if (concurrent.ReadLeaseExpiresAtUtc > recordedAt
                && concurrent.ExpiresAtUtc <= recordedAt)
            {
                throw new IOException(
                    "The cache cleanup lease must finish before concurrent publication can record access.");
            }
            var recorded = await db.Set<DocumentContentCacheEntryEntity>()
                .Where(value => value.Id == concurrent.Id
                    && value.ConcurrencyToken == concurrent.ConcurrencyToken
                    && value.ETag == concurrent.ETag
                    && value.VerifiedSize == source.Length
                    && value.VerifiedSha256 == concurrent.VerifiedSha256
                    && value.BlobIdentity == identity
                    && (value.ReadLeaseExpiresAtUtc == null
                        || value.ReadLeaseExpiresAtUtc <= recordedAt
                        || value.ExpiresAtUtc > recordedAt))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.ExpiresAtUtc, recordedAt.Add(IdleLifetime))
                    .SetProperty(value => value.LastCleanupOutcome, (string?)null),
                    cancellationToken);
            if (recorded == 0)
            {
                throw new IOException(
                    "The concurrent cache publication changed before access could be recorded.");
            }
        }
    }

    private static IQueryable<DocumentContentCacheEntryEntity> CacheQuery(
        PegasusDbContext db,
        ResolvedSource source) =>
        db.Set<DocumentContentCacheEntryEntity>().Where(value =>
            source.DocumentVersionId != null
                ? value.DocumentVersionId == source.DocumentVersionId
                : value.IntakeAssetId == source.IntakeAssetId);

    private static LogicalDocumentContent Result(
        ReadLogicalDocumentVersionRequest request,
        ResolvedSource source,
        Stream content) =>
        new(
            content,
            request.DocumentId,
            request.VersionId,
            request.IntakeAssetId,
            source.Sha256,
            source.Length,
            source.FileName,
            source.MediaType);

    private static async Task<Stream> ReadVerifiedToTemporaryAsync(
        Stream content,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var path = Path.Combine(Path.GetTempPath(), $"pegasus-cache-{Guid.NewGuid():N}.tmp");
        var retained = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var buffer = new byte[81920];
        long length = 0;
        try
        {
            while (true)
            {
                var read = await content.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                length = checked(length + read);
                if (length > expectedLength)
                {
                    throw new InvalidDataException("Logical document length verification failed.");
                }
                hash.AppendData(buffer, 0, read);
                await retained.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (length != expectedLength || !FixedHashEquals(actual, expectedSha256))
            {
                throw new InvalidDataException("Logical document content verification failed.");
            }
            retained.Position = 0;
            return retained;
        }
        catch
        {
            await retained.DisposeAsync();
            throw;
        }
    }

    internal static void ValidateRequest(ReadLogicalDocumentVersionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hasDocumentId = request.DocumentId is { } documentId && documentId != Guid.Empty;
        var hasVersionId = request.VersionId is { } versionId && versionId != Guid.Empty;
        var hasDocument = hasDocumentId && hasVersionId;
        var hasAsset = request.IntakeAssetId is { } assetId && assetId != Guid.Empty;
        var validDocumentContext = request.CaseId is { } caseId && caseId != Guid.Empty;
        var validAssetContext =
            request.IntakeReceiptId is { } receiptId && receiptId != Guid.Empty;
        if (hasDocumentId != hasVersionId
            || hasDocument == hasAsset
            || hasDocument && !validDocumentContext
            || hasAsset && !validAssetContext
            || request.ExpectedContentLength < 0)
        {
            throw new ArgumentException("Exactly one complete logical content identity and its authorization context are required.", nameof(request));
        }
        _ = NormalizeHash(request.ExpectedSha256);
    }

    internal static bool FixedHashEquals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(NormalizeHash(left)),
                Convert.FromHexString(NormalizeHash(right)));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string NormalizeHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }
        return value.ToLowerInvariant();
    }

    private sealed record ResolvedSource(
        Guid? DocumentVersionId,
        Guid? IntakeAssetId,
        string BoxFileId,
        string BoxVersionId,
        string Sha256,
        long Length,
        string FileName,
        string MediaType,
        string ExpectedParentId)
    {
        public static ResolvedSource Create(
            Guid? documentVersionId,
            Guid? intakeAssetId,
            string? boxFileId,
            string? boxVersionId,
            string sha256,
            long length,
            string fileName,
            string mediaType,
            string? expectedParentId)
        {
            if (string.IsNullOrWhiteSpace(boxFileId) || string.IsNullOrWhiteSpace(boxVersionId)
                || string.IsNullOrWhiteSpace(expectedParentId))
            {
                throw new FileNotFoundException("Durable Box custody has not been confirmed.");
            }
            return new(
                documentVersionId,
                intakeAssetId,
                boxFileId,
                boxVersionId,
                NormalizeHash(sha256),
                length,
                fileName,
                mediaType,
                expectedParentId);
        }
    }
}
