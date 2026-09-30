using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using SkiaSharp;

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

/// <summary>
/// The publisher for a profile with no content cache: there is nothing to
/// fill, so nothing is written.
/// </summary>
public sealed class NoDocumentContentCachePublisher : IDocumentContentCachePublisher
{
    public Task PublishAsync(
        DocumentContentCacheKey key,
        Stream content,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishRetainedIntakeCopyAsync(
        DocumentContentCacheKey key,
        string storageKey,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken) => Task.CompletedTask;
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

internal sealed partial class CachedDocumentContentStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    BlobContainerClient container,
    BoxContentClient box,
    TimeProvider timeProvider,
    IDocumentContentCacheMetrics? metrics = null,
    ILogger<CachedDocumentContentStore>? logger = null,
    IIntakeArtifactStore? intakeArtifacts = null)
    : IReadLogicalDocumentVersion,
        IReadCachedDocumentVersions,
        IDocumentContentCacheCleanup,
        IDocumentContentCachePublisher
{
    private static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// How long cleanup holds the entry it has claimed. Only cleanup takes
    /// this lease, and only on an expired entry.
    /// </summary>
    private static readonly TimeSpan ReadLeaseLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long after its last extension an entry is extended again. A hit
    /// inside this interval writes nothing.
    /// </summary>
    internal static readonly TimeSpan TouchInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// How many versions of one batch are read at once. A cold read also
    /// takes the process-wide Box gate, so this bounds the cached reads.
    /// </summary>
    private const int MaximumConcurrentBatchReads = 4;
    private const string CachePrefix = "cache/";
    private const string HashMetadata = "sha256";

    /// <summary>
    /// The variant an entry of the durable content itself carries: none.
    /// A derived rendering is a different kind of entry for the same version
    /// (see <see cref="DocumentThumbnailCache"/>), so every query for the
    /// content entry says which kind it means.
    /// </summary>
    internal const string OriginalVariant = "";

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

        Stream? cached;
        using (DocumentReadTelemetry.Start("document.original.cache.read"))
        {
            cached = await TryOpenCachedAsync(source, now, cancellationToken);
        }
        if (cached is not null)
        {
            metrics?.RecordHit();
            return Result(request, source, cached);
        }
        metrics?.RecordMiss();

        // A cache miss is a Box read, so it takes the same gate and
        // the same 429/5xx retry as every other managed read rather than
        // failing the request the first time Box says "later". The client
        // hashes the download as it writes it, so the temporary file it
        // returns is the verified copy and is not copied again.
        var downloaded = await BoxDocumentContentStore.ReadGatedWithRetryAsync(
            async token =>
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var remote = await box.OpenOwnedVersionReadAsync(
                    source.BoxFileId,
                    source.BoxVersionId,
                    source.ExpectedParentId,
                    source.Length,
                    hash,
                    token);
                try
                {
                    // The hashing ran while the download was written, inside
                    // document.provider.read. This phase is the comparison alone.
                    using (DocumentReadTelemetry.Start("document.content.verify"))
                    {
                        RequireVerified(remote.Length, hash, source.Length, source.Sha256);
                    }
                    return remote;
                }
                catch
                {
                    await remote.DisposeAsync();
                    throw;
                }
            },
            cancellationToken);
        try
        {
            using (DocumentReadTelemetry.Start("document.original.cache.write"))
            {
                await PublishAsync(source, downloaded, cancellationToken);
            }
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

    private Task RequireCurrentActorAsync(
        ActionActor actor,
        CancellationToken cancellationToken) =>
        RequireCurrentActorAsync(dbContextFactory, actor, cancellationToken);

    /// <summary>
    /// The staff account behind a logical read is still enabled and still
    /// holds a casework role, read from the database rather than from the
    /// cookie the request arrived with.
    /// </summary>
    /// <remarks>
    /// The derived-thumbnail read serves from its own cache entry
    /// without opening the durable content, so it applies this same check
    /// itself instead of inheriting it from a read it no longer makes.
    /// </remarks>
    internal static async Task RequireCurrentActorAsync(
        IDbContextFactory<PegasusDbContext> dbContextFactory,
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
        // One round trip: the account and its current role names together.
        var account = await db.Users.AsNoTracking()
            .Where(value => value.Id == staffId)
            .Select(value => new
            {
                value.IsEnabled,
                RoleNames = (
                    from userRole in db.UserRoles
                    join role in db.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == value.Id
                    select role.Name).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is not { IsEnabled: true })
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        var roles = account.RoleNames
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
            // One round trip: the asset, and whether its receipt belongs to the
            // requested Case by origin, by link or by an active manual
            // association.
            var requestedCaseId = request.CaseId;
            var row = await db.Set<IntakeAssetEntity>().AsNoTracking()
                .Where(value => value.Id == assetId)
                .Select(value => new
                {
                    Asset = value,
                    AssociatedWithCase = requestedCaseId == null
                        || db.Cases.Any(caseEntity => caseEntity.Id == requestedCaseId
                            && (caseEntity.OriginIntakeReceiptId == value.IntakeReceiptId
                                || caseEntity.IntakeLinks.Any(link => link.IntakeReceiptId == value.IntakeReceiptId)))
                        || db.Set<IntakeManualAssociationEntity>().Any(association =>
                            association.CaseId == requestedCaseId
                            && association.IntakeReceiptId == value.IntakeReceiptId
                            && association.IsActive)
                })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new FileNotFoundException("The retained intake source is unavailable.");
            var asset = row.Asset;
            if (asset.IntakeReceiptId != request.IntakeReceiptId)
            {
                throw new UnauthorizedAccessException("The intake source does not belong to the authorized receipt.");
            }
            if (!row.AssociatedWithCase)
            {
                throw new UnauthorizedAccessException("The intake source is not associated with the authorized Case.");
            }
            // The confirmed copy is wherever custody filed it: the holding
            // folder, the Case folder or the Vehicle images folder. Box then
            // checks the file really sits there.
            var expectedParentId = asset.BoxParentFolderId;
            if (!string.Equals(asset.CustodyStatus, "confirmed", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(asset.BoxFileId)
                || string.IsNullOrWhiteSpace(asset.BoxVersionId)
                || string.IsNullOrWhiteSpace(expectedParentId))
            {
                throw new IntakeCustodyUnavailableException(
                    "Durable custody has not confirmed the retained intake file.");
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
                // The document's own folder: the a. folder for an Audit report.
                CaseRootRemoteId = document.CustodyFolder == CaseCustodyFolders.Audit
                    ? caseEntity.AuditCustodyRemoteId
                    : caseEntity.CustodyRootRemoteId
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

    /// <summary>
    /// The cached content, or <c>null</c> when there is no live entry to
    /// serve.
    /// </summary>
    /// <remarks>
    /// A hit is the entry read and, at most once an hour, one conditional
    /// update that pushes its idle expiry out. The update happens before the
    /// object is read, and cleanup claims only expired entries, so an entry
    /// being served always has most of a day left: cleanup cannot remove the
    /// object under the read. An entry that expired, or that cleanup has
    /// claimed, is a miss.
    /// </remarks>
    private async Task<Stream?> TryOpenCachedAsync(
        ResolvedSource source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await CacheQuery(db, source).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (entry is null || !await TryTouchAsync(db, entry, IdleLifetime, now, cancellationToken))
        {
            return null;
        }
        var response = await TryDownloadAsync(entry, cancellationToken);
        if (response is null)
        {
            return null;
        }
        await using var content = response.Value.Content;
        return await ReadVerifiedToTemporaryAsync(
            content, source.Length, source.Sha256, cancellationToken);
    }

    /// <summary>
    /// Every managed version of one Case in <paramref name="reads"/>, cache
    /// first: a warm version is read from its cached object, a cold one from
    /// Box through the same gate, retry and fence as a single read, and then
    /// cached for the next time. Each version is verified against its custody
    /// hash and length either way.
    /// </summary>
    /// <remarks>
    /// The caller has already authorised the Case, as it has for
    /// <see cref="IDocumentContentStore"/>. Every version is held in memory
    /// before any is returned.
    /// </remarks>
    public async Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadVersionsAsync(
        IReadOnlyList<ManagedDocumentContentRead> reads,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reads);
        if (reads.Count == 0)
        {
            return [];
        }
        // Every argument is checked before any I/O starts.
        var first = reads[0].Address;
        var sources = new ResolvedSource[reads.Count];
        for (var index = 0; index < reads.Count; index++)
        {
            var read = reads[index];
            var address = read.Address;
            if (address.CaseId != first.CaseId
                || !string.Equals(address.CaseReference, first.CaseReference, StringComparison.Ordinal)
                || !string.Equals(address.CaseRootRemoteId, first.CaseRootRemoteId, StringComparison.Ordinal)
                || address.VersionId == Guid.Empty)
            {
                throw new ArgumentException("A managed content batch reads one Case only.", nameof(reads));
            }
            sources[index] = ResolvedSource.Create(
                address.VersionId,
                intakeAssetId: null,
                address.BoxFileId,
                address.BoxVersionId,
                read.ExpectedSha256,
                read.ExpectedLength,
                address.FileName,
                address.MediaType,
                address.CaseRootRemoteId);
        }

        Dictionary<Guid, DocumentContentCacheEntryEntity> entries;
        await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var versionIds = sources
                .Select(source => source.DocumentVersionId!.Value)
                .Distinct()
                .ToArray();
            entries = await db.Set<DocumentContentCacheEntryEntity>().AsNoTracking()
                .Where(value => value.Variant == OriginalVariant
                    && value.DocumentVersionId != null
                    && versionIds.Contains(value.DocumentVersionId.Value))
                .ToDictionaryAsync(value => value.DocumentVersionId!.Value, cancellationToken);
        }

        var contents = new ReadOnlyMemory<byte>[reads.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, reads.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentBatchReads,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                var source = sources[index];
                if (entries.TryGetValue(source.DocumentVersionId!.Value, out var entry)
                    && await TryReadCachedBytesAsync(entry, source, token) is { } cached)
                {
                    metrics?.RecordHit();
                    contents[index] = cached;
                    return;
                }
                metrics?.RecordMiss();
                byte[] downloaded;
                try
                {
                    downloaded = await BoxDocumentContentStore.ReadGatedWithRetryAsync(
                        async attemptToken =>
                        {
                            await using var remote = await box.OpenOwnedVersionReadAsync(
                                source.BoxFileId,
                                source.BoxVersionId,
                                source.ExpectedParentId,
                                source.Length,
                                attemptToken);
                            return await ReadVerifiedBytesAsync(
                                remote, source.Length, source.Sha256, attemptToken);
                        },
                        token);
                }
                catch (HttpRequestException exception)
                    when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // The same answer the uncached batch gives for a missing version.
                    throw new FileNotFoundException("The exact managed Box version is unavailable.", exception);
                }
                contents[index] = downloaded;
                try
                {
                    using var published = new MemoryStream(downloaded, writable: false);
                    await PublishAsync(source, published, token);
                }
                catch (Exception exception) when (exception is IOException
                    or InvalidDataException
                    or DbUpdateException
                    or RequestFailedException)
                {
                    // The verified bytes are the answer either way; a later
                    // read caches them.
                }
            });
        return contents;
    }

    /// <summary>
    /// One batch version from its cached object, or <c>null</c> for a miss.
    /// </summary>
    /// <remarks>
    /// The time is taken when this version is read, not when the batch
    /// started. The entry was read with the batch, but its expiry only ever
    /// moves later and the touch is decided by the row itself, so an entry
    /// that has since expired or been claimed by cleanup is a miss. Any
    /// failure of the cached copy is a miss too: a lost or changed object, a
    /// broken stream, or bytes that fail verification. That version is then
    /// read from Box.
    /// </remarks>
    private async Task<byte[]?> TryReadCachedBytesAsync(
        DocumentContentCacheEntryEntity entry,
        ResolvedSource source,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (entry.ExpiresAtUtc <= now || entry.ReadLeaseExpiresAtUtc > now)
        {
            return null;
        }
        try
        {
            if (NeedsTouch(entry, IdleLifetime, now))
            {
                await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                if (!await TryTouchAsync(db, entry, IdleLifetime, now, cancellationToken))
                {
                    return null;
                }
            }
            var response = await TryDownloadAsync(entry, cancellationToken);
            if (response is null)
            {
                return null;
            }
            await using var content = response.Value.Content;
            return await ReadVerifiedBytesAsync(content, source.Length, source.Sha256, cancellationToken);
        }
        catch (Exception exception) when (exception is RequestFailedException
            or IOException
            or InvalidDataException)
        {
            return null;
        }
    }

    private async Task<Response<BlobDownloadStreamingResult>?> TryDownloadAsync(
        DocumentContentCacheEntryEntity entry,
        CancellationToken cancellationToken)
    {
        try
        {
            return await container.GetBlobClient(entry.BlobIdentity).DownloadStreamingAsync(
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
    }

    /// <summary>
    /// Whether a live entry's idle expiry was last pushed out more than
    /// <see cref="TouchInterval"/> ago.
    /// </summary>
    internal static bool NeedsTouch(
        DocumentContentCacheEntryEntity entry,
        TimeSpan idleLifetime,
        DateTimeOffset now) =>
        entry.ExpiresAtUtc - idleLifetime <= now - TouchInterval;

    /// <summary>
    /// Whether an entry may be served: it has not expired, cleanup has not
    /// claimed it, and when its expiry is due to be pushed out, one
    /// conditional update did so. The update matches only an unexpired,
    /// unclaimed row, so a claim that landed after the entry was read makes
    /// the read a miss.
    /// </summary>
    internal static async Task<bool> TryTouchAsync(
        PegasusDbContext db,
        DocumentContentCacheEntryEntity entry,
        TimeSpan idleLifetime,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (entry.ExpiresAtUtc <= now || entry.ReadLeaseExpiresAtUtc > now)
        {
            return false;
        }
        if (!NeedsTouch(entry, idleLifetime, now))
        {
            return true;
        }
        var touched = await db.Set<DocumentContentCacheEntryEntity>()
            .Where(value => value.Id == entry.Id
                && value.ExpiresAtUtc > now
                && (value.ReadLeaseExpiresAtUtc == null || value.ReadLeaseExpiresAtUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.ExpiresAtUtc, now.Add(idleLifetime))
                .SetProperty(value => value.LastCleanupOutcome, (string?)null),
                cancellationToken);
        return touched == 1;
    }

    /// <summary>
    /// The longest a publish at filing takes before it is given up. The filing
    /// it follows is already confirmed, so a slow store must not hold its caller.
    /// </summary>
    private static readonly TimeSpan FilingPublishTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Publishes content a caller has just filed and holds. The bytes are
    /// checked against <paramref name="sha256"/> and
    /// <paramref name="contentLength"/> before anything is written, then go
    /// through the same write, integrity check and entry as a copy made by a
    /// read miss. Nothing escapes: a failure is logged and the first read
    /// publishes instead.
    /// </summary>
    public Task PublishAsync(
        DocumentContentCacheKey key,
        Stream content,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken) =>
        PublishAtFilingAsync(
            key,
            sha256,
            contentLength,
            (filed, token) => PublishVerifiedAsync(filed, content, token),
            cancellationToken);

    /// <summary>
    /// The same for a file whose bytes the caller did not hold: they are read
    /// from the copy intake retained, which is Azure storage and not Box.
    /// </summary>
    public Task PublishRetainedIntakeCopyAsync(
        DocumentContentCacheKey key,
        string storageKey,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken) =>
        PublishAtFilingAsync(
            key,
            sha256,
            contentLength,
            async (filed, token) =>
            {
                var reader = intakeArtifacts ?? throw new InvalidOperationException(
                    "No reader of retained intake copies is composed.");
                if (await reader.ReadAsync(storageKey, token) is not { } bytes)
                {
                    throw new FileNotFoundException("The retained intake copy is unavailable.");
                }
                await using var retained = DocumentContentCachePublisherExtensions.StreamOf(bytes);
                await PublishVerifiedAsync(filed, retained, token);
            },
            cancellationToken);

    /// <summary>
    /// Runs one publish at filing so that nothing it does reaches the caller:
    /// it is bounded in time, and a failure of any kind is logged at Warning.
    /// A caller that is stopping ends it quietly.
    /// </summary>
    private async Task PublishAtFilingAsync(
        DocumentContentCacheKey key,
        string sha256,
        long contentLength,
        Func<FiledContent, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        try
        {
            var filed = FiledContent.Create(key, sha256, contentLength);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(FilingPublishTimeout);
            await publish(filed, bounded.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller is stopping; the first read publishes instead.
        }
        catch (Exception exception)
        {
            LogFilingPublishFailed(
                logger ?? NullLogger<CachedDocumentContentStore>.Instance,
                key?.DocumentVersionId is null ? "intake asset" : "document version",
                key?.DocumentVersionId ?? key?.IntakeAssetId,
                exception);
        }
    }

    /// <summary>
    /// Writes the copy only when the stream really holds the bytes it is said
    /// to, so a caller's mistake never becomes a copy that a read would refuse.
    /// </summary>
    private async Task PublishVerifiedAsync(
        FiledContent filed,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        content.Position = 0;
        var actual = Convert.ToHexString(
            await SHA256.HashDataAsync(content, cancellationToken)).ToLowerInvariant();
        if (content.Length != filed.Length || !FixedHashEquals(actual, filed.Sha256))
        {
            throw new InvalidDataException(
                "The filed content does not match the length and hash it was published under.");
        }
        await PublishAsync(filed, content, cancellationToken);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The read-cache copy of a filed {Kind} {ContentId} could not be published, so its first read fetches it from Box.")]
    private static partial void LogFilingPublishFailed(
        ILogger logger, string kind, Guid? contentId, Exception exception);

    private async Task PublishAsync(
        ICachedContentIdentity source,
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
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            // A concurrent miss published the same logical identity (Azure answers an
            // If-None-Match: * loser with 409 BlobAlreadyExists, a fake with 412). Verify it below.
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
                Variant = OriginalVariant,
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
        ICachedContentIdentity source) =>
        db.Set<DocumentContentCacheEntryEntity>().Where(value =>
            value.Variant == OriginalVariant
            && (source.DocumentVersionId != null
                ? value.DocumentVersionId == source.DocumentVersionId
                : value.IntakeAssetId == source.IntakeAssetId));

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
        using var verification = DocumentReadTelemetry.Start("document.content.verify");
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
            RequireVerified(length, hash, expectedLength, expectedSha256);
            retained.Position = 0;
            return retained;
        }
        catch
        {
            await retained.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Refuses content whose length or SHA-256, taken as it was written,
    /// differs from what custody recorded. It finishes <paramref name="hash"/>.
    /// </summary>
    private static void RequireVerified(
        long length,
        IncrementalHash hash,
        long expectedLength,
        string expectedSha256)
    {
        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (length != expectedLength || !FixedHashEquals(actual, expectedSha256))
        {
            throw new InvalidDataException("Logical document content verification failed.");
        }
    }

    private static async Task<byte[]> ReadVerifiedBytesAsync(
        Stream content,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        using var verification = DocumentReadTelemetry.Start("document.content.verify");
        if (expectedLength is < 0 or > int.MaxValue)
        {
            throw new InvalidDataException("Logical document length verification failed.");
        }
        var bytes = GC.AllocateUninitializedArray<byte>((int)expectedLength);
        try
        {
            await content.ReadExactlyAsync(bytes, cancellationToken);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Logical document length verification failed.", exception);
        }
        if (await content.ReadAsync(new byte[1], cancellationToken) != 0)
        {
            throw new InvalidDataException("Logical document length verification failed.");
        }
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!FixedHashEquals(actual, expectedSha256))
        {
            throw new InvalidDataException("Logical document content verification failed.");
        }
        return bytes;
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

    /// <summary>
    /// What a cache copy is keyed by and proved against: the version or intake
    /// asset it belongs to, and the SHA-256 and length its bytes must have. A
    /// copy made by a read miss has it from the resolved source; a copy made at
    /// filing has it from the caller. One write serves both.
    /// </summary>
    private interface ICachedContentIdentity
    {
        Guid? DocumentVersionId { get; }

        Guid? IntakeAssetId { get; }

        string Sha256 { get; }

        long Length { get; }
    }

    /// <summary>The identity of content a caller has just filed.</summary>
    private sealed record FiledContent(
        Guid? DocumentVersionId,
        Guid? IntakeAssetId,
        string Sha256,
        long Length) : ICachedContentIdentity
    {
        public static FiledContent Create(DocumentContentCacheKey key, string sha256, long length)
        {
            if ((key.DocumentVersionId is null) == (key.IntakeAssetId is null)
                || key.DocumentVersionId == Guid.Empty
                || key.IntakeAssetId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Exactly one document version or intake asset identity is required.", nameof(key));
            }
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            return new(key.DocumentVersionId, key.IntakeAssetId, NormalizeHash(sha256), length);
        }
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
        string ExpectedParentId) : ICachedContentIdentity
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

/// <summary>
/// The derived-thumbnail entry kind of the document content cache: one object
/// and one row per document version, alongside — never in place of — that
/// version's own content entry.
/// </summary>
/// <remarks>
/// The content cache is one entry per <c>DocumentVersionId</c>, so a
/// derived rendering needs a variant of its own rather than a second row that
/// the unique index would refuse and that
/// <see cref="CachedDocumentContentStore"/> would then read as the content.
/// <see cref="DocumentContentCacheEntryEntity.Variant"/> is what tells the two
/// apart; the content entry keeps its own integrity check untouched.
///
/// A thumbnail is derived data. Its recorded hash is the hash of the rendering
/// itself, which is what a read verifies the cached object against; the version
/// it was derived from is immutable, so that version identity is the only key
/// it needs. Where anything about the cached object fails to verify, the answer
/// is simply "no cached thumbnail" and the caller derives it again.
/// </remarks>
internal sealed class DocumentThumbnailCache(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    BlobContainerClient container,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The variant key of the plain gallery rendering. A prepared region
    /// (rotation, crop) is cached under its own variant from
    /// <see cref="CaseDocumentThumbnails.VariantToken"/>, so an occurrence's
    /// edited crop is never answered with an earlier rendering.
    /// </summary>
    internal static readonly string Variant =
        CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, null);

    /// <summary>
    /// How long an unused plain rendering is kept. The Worker makes these
    /// ahead of the first view, so they outlive the content cache's day.
    /// </summary>
    internal static readonly TimeSpan PlainIdleLifetime = TimeSpan.FromDays(30);

    /// <summary>How long an unused prepared-region rendering is kept.</summary>
    private static readonly TimeSpan PreparedIdleLifetime = TimeSpan.FromHours(24);

    private const string CachePrefix = "cache/";
    private const string HashMetadata = "sha256";

    private static TimeSpan IdleLifetimeOf(string variant) =>
        string.Equals(variant, Variant, StringComparison.Ordinal) ? PlainIdleLifetime : PreparedIdleLifetime;

    /// <summary>
    /// The cached rendering, or <c>null</c> when there is none to serve.
    /// </summary>
    /// <remarks>
    /// The staff authorization is applied here rather than inherited from the
    /// durable read, because a cache hit makes no durable read. A hit extends
    /// the entry's expiry before the object is read, at most once an hour, by
    /// the same conditional update as the content cache.
    /// </remarks>
    public Task<byte[]?> TryReadAsync(
        ActionActor actor,
        Guid versionId,
        CancellationToken cancellationToken) =>
        TryReadAsync(actor, versionId, Variant, cancellationToken);

    public async Task<byte[]?> TryReadAsync(
        ActionActor actor,
        Guid versionId,
        string variant,
        CancellationToken cancellationToken)
    {
        using var cacheRead = DocumentReadTelemetry.Start("document.thumbnail.cache.read");
        StaffAuthorization.Require(
            actor,
            actor.Kind == ActorKind.SystemWorker
                ? StaffAccessRight.ExecuteSystemWork
                : StaffAccessRight.PerformCasework);
        await CachedDocumentContentStore.RequireCurrentActorAsync(
            dbContextFactory, actor, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await Query(db, versionId, variant).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (entry is null
            || !await CachedDocumentContentStore.TryTouchAsync(
                db, entry, IdleLifetimeOf(variant), now, cancellationToken))
        {
            return null;
        }
        byte[] content;
        try
        {
            var response = await container.GetBlobClient(entry.BlobIdentity)
                .DownloadStreamingAsync(
                    new BlobDownloadOptions
                    {
                        Conditions = entry.ETag is { Length: > 0 }
                            ? new BlobRequestConditions { IfMatch = new ETag(entry.ETag) }
                            : null
                    },
                    cancellationToken);
            await using var stream = response.Value.Content;
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return null;
        }
        if (content.LongLength != entry.VerifiedSize
            || !CachedDocumentContentStore.FixedHashEquals(
                Sha256Hex(content), entry.VerifiedSha256))
        {
            return null;
        }
        return content;
    }

    /// <summary>
    /// Records one rendering. Best effort: the caller already holds the bytes,
    /// so a lost race writes nothing rather than failing the preview.
    /// </summary>
    public Task WriteAsync(
        Guid versionId,
        byte[] content,
        CancellationToken cancellationToken) =>
        WriteAsync(versionId, Variant, content, cancellationToken);

    public async Task WriteAsync(
        Guid versionId,
        string variant,
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var cacheWrite = DocumentReadTelemetry.Start("document.thumbnail.cache.write");
        var now = timeProvider.GetUtcNow();
        var identity = $"{CachePrefix}document-versions/{versionId:D}/{variant}";
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await Query(db, versionId, variant).SingleOrDefaultAsync(cancellationToken);
        if (entry is not null && entry.ReadLeaseExpiresAtUtc > now)
        {
            // A cleanup pass holds this entry and is about to remove the object
            // its row names, so publishing now would leave an orphan behind it.
            return;
        }
        var hash = Sha256Hex(content);
        try
        {
            using var source = new MemoryStream(content, writable: false);
            var uploaded = await container.GetBlobClient(identity).UploadAsync(
                source,
                new BlobUploadOptions
                {
                    Metadata = new Dictionary<string, string> { [HashMetadata] = hash }
                },
                cancellationToken);
            var etag = uploaded.Value.ETag.ToString();
            if (entry is null)
            {
                db.Add(new DocumentContentCacheEntryEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentVersionId = versionId,
                    Variant = variant,
                    BlobIdentity = identity,
                    ETag = etag,
                    VerifiedSha256 = hash,
                    VerifiedSize = content.LongLength,
                    ExpiresAtUtc = now.Add(IdleLifetimeOf(variant)),
                    ConcurrencyToken = Guid.NewGuid()
                });
            }
            else
            {
                entry.BlobIdentity = identity;
                entry.ETag = etag;
                entry.VerifiedSha256 = hash;
                entry.VerifiedSize = content.LongLength;
                entry.ExpiresAtUtc = now.Add(IdleLifetimeOf(variant));
                entry.LastCleanupOutcome = null;
                entry.ReadLeaseExpiresAtUtc = null;
                entry.ConcurrencyToken = Guid.NewGuid();
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent derivation recorded the same variant first. It named
            // the same object and the same bytes, so there is nothing to fix.
            db.ChangeTracker.Clear();
        }
        catch (RequestFailedException)
        {
            // The rendering is served from memory either way, and the next read
            // derives it again.
        }
    }

    private static IQueryable<DocumentContentCacheEntryEntity> Query(
        PegasusDbContext db,
        Guid versionId,
        string variant) =>
        db.Set<DocumentContentCacheEntryEntity>().Where(value =>
            value.DocumentVersionId == versionId && value.Variant == variant);

    private static string Sha256Hex(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}

/// <summary>
/// The gallery-sized rendering of one image version: the cached variant where
/// there is one, otherwise derived from the verified full bytes and recorded.
/// </summary>
/// <remarks>
/// The derivation reads through
/// <see cref="IReadLogicalDocumentVersion"/>, so it inherits that read's
/// authorization, its custody-hash verification and its confirmed-versions-only
/// rule rather than restating any of them. A profile with no content cache
/// composed simply derives on every read.
/// </remarks>
internal sealed class CaseDocumentThumbnailReader(
    IReadLogicalDocumentVersion source,
    DocumentThumbnailCache? cache = null) : IReadCaseDocumentThumbnail
{
    private static readonly ConcurrentDictionary<(Guid VersionId, string Variant), RenderGate> RenderGates = [];

    public async Task<CaseDocumentThumbnail?> OpenAsync(
        CaseDocumentThumbnailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CaseDocumentThumbnails.IsThumbnailable(request.MediaType))
        {
            return null;
        }
        // The prepared region is its own variant (v26 crop and tag): the tile
        // shows the crop of the rotated source the operator saved, while
        // Download and the viewer keep the original bytes.
        var variant = CaseDocumentThumbnails.VariantToken(request.Rotation, request.Crop);
        if (cache is null)
        {
            return await RenderAndCacheAsync(request, variant, cancellationToken);
        }

        var cached = await cache.TryReadAsync(
            request.Actor, request.VersionId, variant, cancellationToken);
        if (cached is not null)
        {
            return Rendering(cached, request.Sha256);
        }

        // Cache.WriteAsync resolves its database race only after every
        // contender has fetched and rendered the source. Recheck under a
        // bounded, process-local gate so a same-representation burst does
        // that expensive work once while retaining cancellation for each
        // waiter. Different versions and prepared regions remain parallel.
        using var renderGate = await AcquireRenderGateAsync(
            (request.VersionId, variant), cancellationToken);
        cached = await cache.TryReadAsync(
            request.Actor, request.VersionId, variant, cancellationToken);
        if (cached is not null)
        {
            return Rendering(cached, request.Sha256);
        }

        return await RenderAndCacheAsync(request, variant, cancellationToken);
    }

    private async Task<CaseDocumentThumbnail?> RenderAndCacheAsync(
        CaseDocumentThumbnailRequest request,
        string variant,
        CancellationToken cancellationToken)
    {
        await using var full = await source.OpenAsync(
            new(
                Actor: request.Actor,
                DocumentId: request.DocumentId,
                VersionId: request.VersionId,
                IntakeAssetId: null,
                CaseId: request.CaseId,
                IntakeReceiptId: null,
                ExpectedSha256: request.Sha256,
                ExpectedContentLength: request.ContentLength),
            cancellationToken);
        var rendered = await ImageThumbnailRendering.TryRenderAsync(
            full.Content,
            request.ContentLength,
            request.Rotation,
            request.Crop ?? CaseAssetCrop.Full,
            cancellationToken);
        if (rendered is null)
        {
            return null;
        }
        if (cache is not null)
        {
            await cache.WriteAsync(request.VersionId, variant, rendered, cancellationToken);
        }
        return Rendering(rendered, request.Sha256);
    }

    private static async Task<RenderGateLease> AcquireRenderGateAsync(
        (Guid VersionId, string Variant) key,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var gate = RenderGates.GetOrAdd(key, static _ => new RenderGate());
            if (!gate.TryAddReference())
            {
                continue;
            }

            try
            {
                var wait = gate.Semaphore.WaitAsync(cancellationToken);
                if (wait.IsCompleted)
                {
                    await wait;
                }
                else
                {
                    using (DocumentReadTelemetry.Start("document.thumbnail.render.gate"))
                    {
                        await wait;
                    }
                }
                return new RenderGateLease(key, gate);
            }
            catch
            {
                ReleaseRenderGate(key, gate);
                throw;
            }
        }
    }

    private static void ReleaseRenderGate(
        (Guid VersionId, string Variant) key,
        RenderGate gate)
    {
        if (gate.ReleaseReference())
        {
            RenderGates.TryRemove(key, out _);
            gate.Dispose();
        }
    }

    private sealed class RenderGate : IDisposable
    {
        private readonly object sync = new();
        private int references;
        private bool retired;

        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public bool TryAddReference()
        {
            lock (sync)
            {
                if (retired)
                {
                    return false;
                }

                references++;
                return true;
            }
        }

        public bool ReleaseReference()
        {
            lock (sync)
            {
                references--;
                if (references != 0)
                {
                    return false;
                }

                retired = true;
                return true;
            }
        }

        public void Dispose() => Semaphore.Dispose();
    }

    private readonly struct RenderGateLease(
        (Guid VersionId, string Variant) key,
        RenderGate gate) : IDisposable
    {
        public void Dispose()
        {
            gate.Semaphore.Release();
            ReleaseRenderGate(key, gate);
        }
    }

    private static CaseDocumentThumbnail Rendering(byte[] content, string sourceSha256) =>
        new(
            new MemoryStream(content, writable: false),
            CaseDocumentThumbnails.MediaType,
            content.LongLength,
            sourceSha256);
}

/// <summary>
/// The Case gallery's rendering offered for bytes that are not a Case document
/// version (a pre-Case intake image's prepared tile): the same
/// <see cref="ImageThumbnailRendering"/>, uncached, because the intake read has
/// no content cache and a pre-Case record holds a handful of images.
/// </summary>
internal sealed class ImageThumbnailRenderer : IRenderImageThumbnail
{
    public async Task<byte[]?> RenderAsync(
        ReadOnlyMemory<byte> content,
        CaseAssetRotation rotation,
        CaseAssetCrop crop,
        CancellationToken cancellationToken)
    {
        using var stream = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(content.ToArray(), writable: false);
        return await ImageThumbnailRendering.TryRenderAsync(stream, content.Length, rotation, crop, cancellationToken);
    }
}

/// <summary>
/// Renders one image's gallery thumbnail with SkiaSharp: longest displayed edge
/// <see cref="CaseDocumentThumbnails.LongestEdge"/>, JPEG, EXIF orientation
/// applied.
/// </summary>
/// <remarks>
/// Nothing here throws at the caller: bytes that are not a decodable
/// image, or an image past the decode bound, mean "no thumbnail", and the full
/// image is served instead.
///
/// The decode gate is separate from the Box read gate, because decoding is the
/// expensive half of a derivation: sixty first-visit tiles would otherwise hold
/// sixty full-resolution bitmaps at once.
/// </remarks>
internal static class ImageThumbnailRendering
{
    private const int JpegQuality = 78;

    /// <summary>
    /// The decoded-pixel bound, matching the recognition engine's: past it the
    /// decode is refused rather than attempted.
    /// </summary>
    private const long MaximumDecodedPixels = 40_000_000;

    /// <summary>
    /// The largest source a thumbnail is derived from. A larger file is served
    /// whole rather than buffered again for a tile.
    /// </summary>
    internal const long MaximumSourceBytes = 32L * 1024 * 1024;

    private static readonly SemaphoreSlim DecodeGate = new(2, 2);

    internal static Task<byte[]?> TryRenderAsync(
        Stream content,
        long contentLength,
        CancellationToken cancellationToken) =>
        TryRenderAsync(content, contentLength, CaseAssetRotation.None, CaseAssetCrop.Full, cancellationToken);

    internal static async Task<byte[]?> TryRenderAsync(
        Stream content,
        long contentLength,
        CaseAssetRotation rotation,
        CaseAssetCrop crop,
        CancellationToken cancellationToken)
    {
        using var rendering = DocumentReadTelemetry.Start("document.thumbnail.render");
        ArgumentNullException.ThrowIfNull(crop);
        ArgumentNullException.ThrowIfNull(content);
        if (contentLength <= 0 || contentLength > MaximumSourceBytes)
        {
            return null;
        }
        using (DocumentReadTelemetry.Start("document.thumbnail.decode.gate"))
        {
            await DecodeGate.WaitAsync(cancellationToken);
        }
        try
        {
            // Keep the verified source stream while queued. Its copy begins
            // only after a decode slot is available, preserving cancellation
            // during an actual source read without retaining a second managed
            // buffer for every queued thumbnail.
            using var buffer = new MemoryStream(checked((int)contentLength));
            await content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            using var source = SKData.Create(buffer, contentLength);
            if (source is null)
            {
                return null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var rendered = rotation == CaseAssetRotation.None && crop.IsFull
                ? Render(source)
                : RenderPrepared(source, rotation, crop);
            cancellationToken.ThrowIfCancellationRequested();
            return rendered;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
        finally
        {
            DecodeGate.Release();
        }
    }

    private static byte[]? Render(SKData source)
    {
        using var codec = SKCodec.Create(source);
        if (codec is null
            || (long)codec.Info.Width * codec.Info.Height is <= 0 or > MaximumDecodedPixels)
        {
            return null;
        }
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        // What the operator sees, which is what the longest edge bounds.
        var displayedWidth = transposed ? codec.Info.Height : codec.Info.Width;
        var displayedHeight = transposed ? codec.Info.Width : codec.Info.Height;
        var scale = Math.Min(
            1d,
            (double)CaseDocumentThumbnails.LongestEdge / Math.Max(displayedWidth, displayedHeight));
        var targetWidth = Math.Max(1, (int)Math.Round(displayedWidth * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(displayedHeight * scale));
        var decodedSize = codec.GetScaledDimensions((float)scale);
        using var decoded = SKBitmap.Decode(
            codec,
            new SKImageInfo(
                decodedSize.Width,
                decodedSize.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul));
        if (decoded is null)
        {
            return null;
        }
        using var scaled = decoded.Resize(
            new SKImageInfo(
                transposed ? targetHeight : targetWidth,
                transposed ? targetWidth : targetHeight,
                SKColorType.Rgba8888,
                SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear));
        if (scaled is null)
        {
            return null;
        }
        // One opaque surface: JPEG carries no alpha, so a transparent source is
        // composed onto white rather than onto black.
        using var target = new SKBitmap(
            new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.White);
            EncodedImageOrientation.Apply(canvas, origin, targetWidth, targetHeight);
            canvas.DrawBitmap(scaled, 0, 0);
        }
        using var image = SKImage.FromBitmap(target);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded?.ToArray();
    }

    /// <summary>
    /// The prepared region: EXIF orientation first, then the operator's
    /// whole-turn rotation, then the crop taken as fractions of that rotated
    /// source - the same geometry the report renderer prints and the crop
    /// editor draws - scaled so the crop's longest edge is the thumbnail edge.
    /// </summary>
    private static byte[]? RenderPrepared(SKData source, CaseAssetRotation rotation, CaseAssetCrop crop)
    {
        using var codec = SKCodec.Create(source);
        if (codec is null
            || (long)codec.Info.Width * codec.Info.Height is <= 0 or > MaximumDecodedPixels)
        {
            return null;
        }
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        var quarterTurn = rotation is CaseAssetRotation.Clockwise90 or CaseAssetRotation.Clockwise270;
        var displayedWidth = transposed ? codec.Info.Height : codec.Info.Width;
        var displayedHeight = transposed ? codec.Info.Width : codec.Info.Height;
        var rotatedWidth = quarterTurn ? displayedHeight : displayedWidth;
        var rotatedHeight = quarterTurn ? displayedWidth : displayedHeight;
        var cropLongEdge = Math.Max(
            1d,
            Math.Max((double)crop.Width * rotatedWidth, (double)crop.Height * rotatedHeight));
        var desiredScale = (float)Math.Min(1d, CaseDocumentThumbnails.LongestEdge / cropLongEdge);
        var decodedSize = codec.GetScaledDimensions(desiredScale);
        using var decoded = SKBitmap.Decode(
            codec,
            new SKImageInfo(decodedSize.Width, decodedSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (decoded is null)
        {
            return null;
        }

        // The rotated source, exactly as the crop editor drew it.
        var shownWidth = transposed ? decoded.Height : decoded.Width;
        var shownHeight = transposed ? decoded.Width : decoded.Height;
        var sourceWidth = quarterTurn ? shownHeight : shownWidth;
        var sourceHeight = quarterTurn ? shownWidth : shownHeight;
        using var rotated = new SKBitmap(
            new SKImageInfo(sourceWidth, sourceHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(sourceWidth / 2f, sourceHeight / 2f);
            canvas.RotateDegrees((int)rotation);
            canvas.Translate(-shownWidth / 2f, -shownHeight / 2f);
            EncodedImageOrientation.Apply(canvas, origin, shownWidth, shownHeight);
            canvas.DrawBitmap(decoded, 0, 0);
        }

        var region = new SKRect(
            (float)crop.Left * sourceWidth,
            (float)crop.Top * sourceHeight,
            (float)(crop.Left + crop.Width) * sourceWidth,
            (float)(crop.Top + crop.Height) * sourceHeight);
        var regionWidth = Math.Max(1f, region.Width);
        var regionHeight = Math.Max(1f, region.Height);
        var scale = Math.Min(1d, CaseDocumentThumbnails.LongestEdge / (double)Math.Max(regionWidth, regionHeight));
        var targetWidth = Math.Max(1, (int)Math.Round(regionWidth * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(regionHeight * scale));
        using var target = new SKBitmap(
            new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(target))
        using (var image = SKImage.FromBitmap(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(
                image,
                region,
                new SKRect(0, 0, targetWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        using var output = SKImage.FromBitmap(target);
        using var encoded = output.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded?.ToArray();
    }
}
