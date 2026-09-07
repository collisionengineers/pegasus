using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

internal sealed class EfCaseArtifactCustody(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IDocumentContentStore documentContentStore,
    IIntakeArtifactStore intakeArtifactStore,
    TimeProvider timeProvider,
    BoxContentClient? box = null,
    string? holdingFolderId = null) : ICaseArtifactCustody, ICaseArtifactCustodyStatus
{
    internal const long MaximumArtifactContentLength = 128L * 1024 * 1024;
    public async Task<CaseArtifactCustodyResult> RetainAsync(
        CaseArtifactCustodyRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        await RequireRetainAuthorizationAsync(request, cancellationToken);
        var bytes = await ReadVerifiedAsync(request, cancellationToken);
        await RequireRetainAuthorizationAsync(request, cancellationToken);
        var pendingKey = request.CaseId is not null
            ? await intakeArtifactStore.StoreAsync(request.Sha256, bytes, cancellationToken)
            : null;
        return request.CaseId is { } caseId
            ? await RetainCaseAsync(caseId, request, bytes, pendingKey!, cancellationToken)
            : await RetainHoldingAsync(request, bytes, cancellationToken);
    }

    private async Task RequireRetainAuthorizationAsync(
        CaseArtifactCustodyRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.Actor.Kind)
        {
            case ActorKind.Staff:
            case ActorKind.Automation:
                StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
                return;
            case ActorKind.SystemWorker:
                StaffAuthorization.Require(request.Actor, StaffAccessRight.ExecuteSystemWork);
                return;
            case ActorKind.RequestLink:
                StaffAuthorization.Require(request.Actor, StaffAccessRight.SubmitRequestUpload);
                if (request.CaseId is not { } caseId
                    || !Guid.TryParse(request.Actor.SubjectId, out var requestLinkId)
                    || requestLinkId == Guid.Empty)
                {
                    throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
                }
                await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
                {
                    await RequireRequestLinkAuthorityAsync(
                        db, requestLinkId, caseId, cancellationToken);
                }
                return;
            default:
                throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
    }

    private async Task RequireRequestLinkAuthorityAsync(
        PegasusDbContext db,
        Guid requestLinkId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var authorized = await db.Set<RequestUploadLinkEntity>().AsNoTracking()
            .AnyAsync(value => value.Id == requestLinkId
                && value.CaseId == caseId
                && value.Status == RequestUploadStatus.Active
                && value.RevokedAtUtc == null
                && value.ExpiresAtUtc > nowUtc,
                cancellationToken);
        if (!authorized)
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
    }

    public async Task<CaseArtifactCustodyResult> GetAsync(
        ActionActor actor,
        Guid caseId,
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty || documentId == Guid.Empty || versionId == Guid.Empty)
        {
            throw new ArgumentException("Complete Case artifact identities are required.");
        }
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var requestLinkId = RequireStatusActor(actor);
        var requestLinkCreator = requestLinkId is { } linkId
            ? $"{ActorKind.RequestLink}:{linkId:D}"
            : null;
        var nowUtc = timeProvider.GetUtcNow();
        var version = await (
            from document in db.Set<CaseDocumentEntity>().AsNoTracking()
            join item in db.Set<DocumentVersionEntity>().AsNoTracking()
                on document.Id equals item.DocumentId
            join occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                on item.Id equals occurrence.VersionId
            where document.CaseId == caseId
                && document.Id == documentId
                && item.Id == versionId
                && occurrence.CaseId == caseId
                && occurrence.DocumentId == documentId
                && !item.IsLogicallyRemoved
                && (requestLinkCreator == null
                    || item.CreatedBy == requestLinkCreator
                    && db.Set<RequestUploadLinkEntity>().Any(link =>
                        link.Id == requestLinkId
                        && link.CaseId == caseId
                        && link.Status == RequestUploadStatus.Active
                        && link.RevokedAtUtc == null
                        && link.ExpiresAtUtc > nowUtc))
            select new { Version = item, OccurrenceId = occurrence.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            if (requestLinkId is { } missingRequestLinkId)
            {
                // This query classifies a miss only. Successful disclosure above
                // is authorized atomically in the statement that returns the row.
                await RequireRequestLinkAuthorityAsync(
                    db, missingRequestLinkId, caseId, cancellationToken);
            }
            throw new FileNotFoundException("The authorized Case artifact version is unavailable.");
        }
        return Status(version.Version, version.OccurrenceId);
    }

    public async Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
        ActionActor actor,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty || string.IsNullOrWhiteSpace(operationKey))
        {
            throw new ArgumentException("A Case and custody operation key are required.");
        }
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var requestLinkId = RequireStatusActor(actor);
        var requestLinkCreator = requestLinkId is { } linkId
            ? $"{ActorKind.RequestLink}:{linkId:D}"
            : null;
        var nowUtc = timeProvider.GetUtcNow();
        var version = await (
            from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join document in db.Set<CaseDocumentEntity>().AsNoTracking()
                on occurrence.DocumentId equals document.Id
            join item in db.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.VersionId equals item.Id
            where occurrence.OperationKey == operationKey
                && occurrence.CaseId == caseId
                && document.CaseId == caseId
                && item.DocumentId == document.Id
                && !item.IsLogicallyRemoved
                && (requestLinkCreator == null
                    || item.CreatedBy == requestLinkCreator
                    && db.Set<RequestUploadLinkEntity>().Any(link =>
                        link.Id == requestLinkId
                        && link.CaseId == caseId
                        && link.Status == RequestUploadStatus.Active
                        && link.RevokedAtUtc == null
                        && link.ExpiresAtUtc > nowUtc))
            select new { Version = item, OccurrenceId = occurrence.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (version is not null)
        {
            return Status(version.Version, version.OccurrenceId);
        }
        if (requestLinkId is { } missingRequestLinkId)
        {
            await RequireRequestLinkAuthorityAsync(
                db, missingRequestLinkId, caseId, cancellationToken);
        }
        return null;
    }

    private static Guid? RequireStatusActor(ActionActor actor)
    {
        if (actor.Kind != ActorKind.RequestLink)
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            return null;
        }
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitRequestUpload);
        if (!Guid.TryParse(actor.SubjectId, out var requestLinkId) || requestLinkId == Guid.Empty)
        {
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        }
        return requestLinkId;
    }

    private static CaseArtifactCustodyResult Status(DocumentVersionEntity version, Guid occurrenceId) =>
        version.CustodyStatus switch
        {
            DocumentCustodyStatus.Confirmed => Confirmed(version, occurrenceId),
            DocumentCustodyStatus.Pending => Pending(version, occurrenceId, "case_custody_pending"),
            DocumentCustodyStatus.Failed => Failed(version, occurrenceId, "case_custody_failed"),
            _ => throw new InvalidDataException("The artifact custody status is invalid.")
        };

    private async Task<CaseArtifactCustodyResult> RetainCaseAsync(
        Guid caseId,
        CaseArtifactCustodyRequest request,
        ReadOnlyMemory<byte> bytes,
        string pendingContentStorageKey,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var requestLinkTransaction = request.Actor.Kind == ActorKind.RequestLink
            ? await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        if (request.Actor.Kind == ActorKind.RequestLink)
        {
            await RequireRequestLinkAuthorityAsync(
                db, Guid.Parse(request.Actor.SubjectId), caseId, cancellationToken);
        }
        var existing = await (
            from persistedOccurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join persistedVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                on persistedOccurrence.VersionId equals persistedVersion.Id
            where persistedOccurrence.CaseId == caseId
                && persistedOccurrence.OperationKey == request.OperationKey
            select new { Occurrence = persistedOccurrence, Version = persistedVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.Version.IsLogicallyRemoved)
            {
                throw new FileNotFoundException("The Case artifact version was removed.");
            }
            RequireReplay(existing.Occurrence, existing.Version, request);
            if (existing.Version.CustodyStatus == DocumentCustodyStatus.Confirmed)
            {
                RequireConfirmed(existing.Version, documentContentStore is BoxDocumentContentStore);
                if (requestLinkTransaction is not null)
                    await requestLinkTransaction.CommitAsync(cancellationToken);
                return Confirmed(existing.Version, existing.Occurrence.Id);
            }
            if (existing.Version.CustodyStatus is DocumentCustodyStatus.Pending
                or DocumentCustodyStatus.Failed)
            {
                if (requestLinkTransaction is not null)
                    await requestLinkTransaction.CommitAsync(cancellationToken);
                return Status(existing.Version, existing.Occurrence.Id);
            }
        }

        var caseEntity = await db.Cases.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == caseId, cancellationToken)
            ?? throw new InvalidOperationException("The artifact Case is unavailable.");
        var lastOrdinal = await db.Set<CaseDocumentEntity>()
            .Where(value => value.CaseId == caseId)
            .Select(value => (int?)value.Ordinal)
            .MaxAsync(cancellationToken) ?? 0;
        var document = existing is null ? new CaseDocumentEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Ordinal = checked(lastOrdinal + 1),
            SourceOccurrenceIdentity = request.OccurrenceIdentity
        } : await db.Set<CaseDocumentEntity>().SingleAsync(
            value => value.Id == existing.Version.DocumentId, cancellationToken);
        var version = existing?.Version ?? new DocumentVersionEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Version = 1,
            FileName = SafeName(request.FileName),
            MediaType = request.MediaType.Trim(),
            ContentLength = request.ContentLength,
            Sha256 = NormalizeHash(request.Sha256),
            PendingContentStorageKey = pendingContentStorageKey,
            CustodyStatus = DocumentCustodyStatus.Pending,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            CreatedBy = $"{request.Actor.Kind}:{request.Actor.SubjectId}",
            IsCurrent = true
        };
        var occurrence = existing?.Occurrence ?? new DocumentOccurrenceEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            DocumentId = document.Id,
            VersionId = version.Id,
            Ordinal = document.Ordinal,
            SemanticRole = DocumentSemanticRole.OriginalSource,
            Source = DocumentSource.Generated,
            SourceOccurrenceIdentity = request.OccurrenceIdentity,
            RecordedAtUtc = version.CreatedAtUtc,
            OperationKey = request.OperationKey
        };
        if (existing is null)
        {
            db.AddRange(document, version, occurrence);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            db.Attach(version);
        }
        if (string.IsNullOrWhiteSpace(version.PendingContentStorageKey))
        {
            version.PendingContentStorageKey = pendingContentStorageKey;
            await db.SaveChangesAsync(cancellationToken);
        }
        if (requestLinkTransaction is not null)
            await requestLinkTransaction.CommitAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(caseEntity.CustodyRootRemoteId))
        {
            return Pending(version, occurrence.Id, "case_custody_pending");
        }

        var address = new ManagedDocumentContentAddress(
            caseId,
            caseEntity.Reference,
            caseEntity.CustodyRootRemoteId,
            occurrence.Id,
            occurrence.Ordinal,
            document.Id,
            version.Id,
            version.Version,
            occurrence.SemanticRole,
            version.FileName,
            version.MediaType,
            version.BoxFileId,
            version.BoxVersionId);
        var write = await documentContentStore.StoreVersionAsync(
            address, bytes, version.Sha256, cancellationToken);
        if (documentContentStore is BoxDocumentContentStore
            && (string.IsNullOrWhiteSpace(write.RemoteId)
                || string.IsNullOrWhiteSpace(write.BoxVersionId)))
        {
            throw new InvalidDataException(
                "Confirmed artifact custody requires exact Box file and version identities.");
        }
        var capturedRoot = address.CaseRootRemoteId!;
        var changed = await db.Set<DocumentVersionEntity>()
            .Where(value => value.Id == version.Id
                && value.CustodyStatus == DocumentCustodyStatus.Pending
                && value.PendingContentStorageKey == pendingContentStorageKey
                && db.Cases.Any(caseValue => caseValue.Id == caseId
                    && caseValue.CustodyRootRemoteId == capturedRoot))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.BoxFileId, write.RemoteId)
                .SetProperty(value => value.BoxVersionId, write.BoxVersionId)
                .SetProperty(value => value.CustodyStatus, DocumentCustodyStatus.Confirmed)
                .SetProperty(value => value.PendingContentStorageKey, (string?)null),
                cancellationToken);
        if (changed == 0)
        {
            return Pending(version, occurrence.Id, "case_custody_pending");
        }
        version.BoxFileId = write.RemoteId;
        version.BoxVersionId = write.BoxVersionId;
        version.CustodyStatus = DocumentCustodyStatus.Confirmed;
        version.PendingContentStorageKey = null;
        return Confirmed(version, occurrence.Id);
    }

    private async Task<CaseArtifactCustodyResult> RetainHoldingAsync(
        CaseArtifactCustodyRequest request,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        var receiptId = request.IntakeReceiptId!.Value;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedOccurrence = request.OccurrenceIdentity.Trim();
        var asset = await db.Set<IntakeAssetEntity>()
            .SingleOrDefaultAsync(value => value.IntakeReceiptId == receiptId
                && (value.Id.ToString() == normalizedOccurrence
                    || value.SourceLabel == normalizedOccurrence),
                cancellationToken)
            ?? throw new InvalidOperationException("The holding artifact has no retained intake identity.");
        if (!string.Equals(asset.FileName, request.FileName, StringComparison.Ordinal)
            || asset.ContentLength != request.ContentLength
            || !string.Equals(asset.ContentHash, request.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The holding occurrence identity was reused with different content or metadata.");
        }
        if (documentContentStore is not BoxDocumentContentStore)
        {
            var retained = await intakeArtifactStore.ReadAsync(asset.StorageKey, cancellationToken)
                ?? throw new FileNotFoundException("The local holding artifact content is unavailable.");
            _ = await ReadVerifiedAsync(
                request with { Content = new MemoryStream(retained.ToArray(), writable: false) },
                cancellationToken);
            asset.CustodyStatus = "confirmed";
            await db.SaveChangesAsync(cancellationToken);
            return new(
                CaseArtifactCustodyDisposition.Confirmed,
                null, null, null, null, null,
                asset.ContentHash,
                asset.ContentLength,
                asset.MediaType,
                null,
                null);
        }
        if (box is null || string.IsNullOrWhiteSpace(holdingFolderId))
        {
            throw new InvalidOperationException("Box holding custody is not configured.");
        }
        await box.EnsureDescendantAsync(holdingFolderId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(asset.BoxFileId)
            && !string.IsNullOrWhiteSpace(asset.BoxVersionId))
        {
            return new(
                CaseArtifactCustodyDisposition.Confirmed,
                null, null, null,
                asset.BoxFileId,
                asset.BoxVersionId,
                asset.ContentHash,
                asset.ContentLength,
                asset.MediaType,
                null,
                null);
        }
        var fileName = $"{receiptId:N}-{asset.Id:N}-{SafeName(request.FileName)}";
        var existing = await box.FindChildAsync(
            holdingFolderId, fileName, "file", cancellationToken);
        var file = existing ?? await box.UploadAsync(
            holdingFolderId, fileName, bytes, request.MediaType, cancellationToken);
        await using (var retained = await box.OpenVersionReadAsync(
                         file.Id,
                         file.VersionId ?? throw new InvalidDataException(
                             "Box omitted the holding file version identity."),
                         request.ContentLength,
                         cancellationToken))
        {
            _ = await ReadVerifiedAsync(request with { Content = retained }, cancellationToken);
        }
        asset.BoxFileId = file.Id;
        asset.BoxVersionId = file.VersionId;
        asset.CustodyStatus = "confirmed";
        await db.SaveChangesAsync(cancellationToken);
        return new(
            CaseArtifactCustodyDisposition.Confirmed,
            null, null, null,
            file.Id,
            file.VersionId,
            NormalizeHash(request.Sha256),
            request.ContentLength,
            request.MediaType,
            null,
            null);
    }

    private static CaseArtifactCustodyResult Confirmed(DocumentVersionEntity version, Guid occurrenceId) => new(
        CaseArtifactCustodyDisposition.Confirmed,
        version.DocumentId,
        version.Id,
        occurrenceId,
        version.BoxFileId,
        version.BoxVersionId,
        version.Sha256,
        version.ContentLength,
        version.MediaType,
        null,
        null);

    private static void RequireConfirmed(DocumentVersionEntity version, bool requiresBoxIdentity)
    {
        if (version.CustodyStatus != DocumentCustodyStatus.Confirmed
            || requiresBoxIdentity
                && (string.IsNullOrWhiteSpace(version.BoxFileId)
                    || string.IsNullOrWhiteSpace(version.BoxVersionId)))
        {
            throw new InvalidDataException(
                "Confirmed artifact custody is missing its exact Box identity.");
        }
    }

    private static CaseArtifactCustodyResult Pending(DocumentVersionEntity version, Guid occurrenceId, string code) => new(
        CaseArtifactCustodyDisposition.Pending,
        version.DocumentId, version.Id, occurrenceId, version.BoxFileId, version.BoxVersionId,
        version.Sha256, version.ContentLength, version.MediaType, code,
        version.PendingContentStorageKey!);

    private static CaseArtifactCustodyResult Failed(DocumentVersionEntity version, Guid occurrenceId, string code) => new(
        CaseArtifactCustodyDisposition.Failed,
        version.DocumentId, version.Id, occurrenceId, version.BoxFileId, version.BoxVersionId,
        version.Sha256, version.ContentLength, version.MediaType, code,
        version.PendingContentStorageKey);

    private static void RequireReplay(
        DocumentOccurrenceEntity occurrence,
        DocumentVersionEntity version,
        CaseArtifactCustodyRequest request)
    {
        if (!string.Equals(occurrence.SourceOccurrenceIdentity, request.OccurrenceIdentity, StringComparison.Ordinal)
            || !string.Equals(version.FileName, SafeName(request.FileName), StringComparison.Ordinal)
            || !string.Equals(version.MediaType, request.MediaType.Trim(), StringComparison.OrdinalIgnoreCase)
            || version.ContentLength != request.ContentLength
            || !string.Equals(version.Sha256, NormalizeHash(request.Sha256), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The artifact operation key was reused with different content or metadata.");
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(
        CaseArtifactCustodyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumArtifactContentLength)
        {
            throw new InvalidDataException("The artifact exceeds the supported bounded custody size.");
        }
        using var retained = new MemoryStream((int)request.ContentLength);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = await request.Content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            copied = checked(copied + read);
            if (copied > request.ContentLength)
            {
                throw new InvalidDataException("Artifact length verification failed.");
            }
            hash.AppendData(buffer, 0, read);
            await retained.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (copied != request.ContentLength
            || !string.Equals(actual, NormalizeHash(request.Sha256), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Artifact content verification failed.");
        }
        return retained.ToArray();
    }

    private static void Validate(CaseArtifactCustodyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((request.CaseId is null) == (request.IntakeReceiptId is null)
            || request.CaseId == Guid.Empty
            || request.IntakeReceiptId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.OccurrenceIdentity)
            || string.IsNullOrWhiteSpace(request.OperationKey)
            || string.IsNullOrWhiteSpace(request.FileName)
            || string.IsNullOrWhiteSpace(request.MediaType)
            || request.ContentLength < 0
            || !request.Content.CanRead)
        {
            throw new ArgumentException("A complete Case or holding artifact request is required.", nameof(request));
        }
        _ = NormalizeHash(request.Sha256);
    }

    private static string SafeName(string value) => CustodyNames.SafeName(Path.GetFileName(value));

    private static string NormalizeHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }
        return value.ToLowerInvariant();
    }
}

public sealed record PendingArtifactCustodyReconciliationResult(
    int Candidates,
    int Confirmed,
    int Retained,
    int Failures);

public sealed class ReconcilePendingArtifactCustody
{
    private readonly IDbContextFactory<PegasusDbContext> dbContextFactory;
    private readonly IDocumentContentStore contentStore;
    private readonly IIntakeArtifactStore artifactStore;
    private readonly TimeProvider timeProvider;
    private const string AttemptEvent = "ArtifactCustodyReconciliationAttempt";

    public ReconcilePendingArtifactCustody(
        IDbContextFactory<PegasusDbContext> dbContextFactory,
        IDocumentContentStore contentStore,
        IIntakeArtifactStore artifactStore,
        TimeProvider? timeProvider = null)
    {
        this.dbContextFactory = dbContextFactory;
        this.contentStore = contentStore;
        this.artifactStore = artifactStore;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }
    public async Task<PendingArtifactCustodyReconciliationResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await (
            from version in db.Set<DocumentVersionEntity>().AsNoTracking()
            join document in db.Set<CaseDocumentEntity>().AsNoTracking()
                on version.DocumentId equals document.Id
            join occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                on version.Id equals occurrence.VersionId
            join caseEntity in db.Cases.AsNoTracking()
                on document.CaseId equals caseEntity.Id
            where version.CustodyStatus == DocumentCustodyStatus.Pending
                && version.PendingContentStorageKey != null
            let lastAttempt = db.Set<ActionHistoryEntity>()
                .Where(history => history.AggregateType == nameof(DocumentVersionEntity)
                    && history.AggregateId == version.Id.ToString()
                    && history.EventKind == AttemptEvent)
                .Max(history => (DateTimeOffset?)history.OccurredAtUtc)
            orderby lastAttempt == null descending, lastAttempt, version.CreatedAtUtc, version.Id
            select new { Version = version, Document = document, Occurrence = occurrence, Case = caseEntity })
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken);
        var confirmed = 0; var retained = 0; var failures = 0;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Case.CustodyRootRemoteId))
            {
                await RecordAttemptAsync(candidate.Version.Id, "Retained", "CaseRootUnavailable");
                retained++;
                continue;
            }
            try
            {
                var bytes = await artifactStore.ReadAsync(
                    candidate.Version.PendingContentStorageKey!,
                    cancellationToken)
                    ?? throw new FileNotFoundException("Pending artifact content is unavailable.");
                Verify(bytes.Span, candidate.Version.Sha256, candidate.Version.ContentLength);
                var address = new ManagedDocumentContentAddress(
                    candidate.Case.Id,
                    candidate.Case.Reference,
                    candidate.Case.CustodyRootRemoteId,
                    candidate.Occurrence.Id,
                    candidate.Occurrence.Ordinal,
                    candidate.Document.Id,
                    candidate.Version.Id,
                    candidate.Version.Version,
                    candidate.Occurrence.SemanticRole,
                    candidate.Version.FileName,
                    candidate.Version.MediaType,
                    candidate.Version.BoxFileId,
                    candidate.Version.BoxVersionId);
                var write = await contentStore.StoreVersionAsync(
                    address, bytes, candidate.Version.Sha256, cancellationToken);
                if (contentStore is BoxDocumentContentStore
                    && (string.IsNullOrWhiteSpace(write.RemoteId)
                        || string.IsNullOrWhiteSpace(write.BoxVersionId)))
                {
                    throw new InvalidDataException("Recovered custody omitted the exact Box identity.");
                }
                await using var update = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var changed = await update.Set<DocumentVersionEntity>()
                    .Where(value => value.Id == candidate.Version.Id
                        && value.CustodyStatus == DocumentCustodyStatus.Pending
                        && value.PendingContentStorageKey == candidate.Version.PendingContentStorageKey
                        && update.Cases.Any(caseValue => caseValue.Id == candidate.Case.Id
                            && caseValue.CustodyRootRemoteId == candidate.Case.CustodyRootRemoteId))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(value => value.BoxFileId, write.RemoteId)
                        .SetProperty(value => value.BoxVersionId, write.BoxVersionId)
                        .SetProperty(value => value.CustodyStatus, DocumentCustodyStatus.Confirmed)
                        .SetProperty(value => value.PendingContentStorageKey, (string?)null),
                        cancellationToken);
                if (changed == 1)
                {
                    confirmed++;
                }
                else
                {
                    await RecordAttemptAsync(candidate.Version.Id, "Retained", "CustodyStateChanged");
                    retained++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                await RecordAttemptAsync(
                    candidate.Version.Id,
                    "Failed",
                    exception.GetType().Name);
                failures++;
            }
        }
        return new(candidates.Length, confirmed, retained, failures);
    }

    private async Task RecordAttemptAsync(Guid versionId, string outcome, string reason)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        db.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = nameof(DocumentVersionEntity),
            AggregateId = versionId.ToString(),
            EventKind = AttemptEvent,
            ActorKind = nameof(ActorKind.SystemWorker),
            ActorSubjectId = "artifact-custody-reconciliation",
            ActorRolesJson = "[]",
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Outcome = outcome,
            CorrelationId = versionId.ToString(),
            Reason = reason
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static void Verify(ReadOnlySpan<byte> bytes, string expectedHash, long expectedLength)
    {
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (bytes.Length != expectedLength
            || !string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Pending artifact content verification failed.");
        }
    }
}
