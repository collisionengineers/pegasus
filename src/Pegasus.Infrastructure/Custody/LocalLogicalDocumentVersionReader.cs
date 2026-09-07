using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

internal sealed class LocalLogicalDocumentVersionReader(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IDocumentContentStore documentContentStore,
    IIntakeArtifactStore intakeArtifactStore) : IReadLogicalDocumentVersion
{
    public async Task<LogicalDocumentContent> OpenAsync(
        ReadLogicalDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        CachedDocumentContentStore.ValidateRequest(request);
        StaffAuthorization.Require(
            request.Actor,
            request.Actor.Kind == ActorKind.SystemWorker
                ? StaffAccessRight.ExecuteSystemWork
                : StaffAccessRight.PerformCasework);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (request.IntakeAssetId is { } assetId)
        {
            var asset = await db.Set<IntakeAssetEntity>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == assetId, cancellationToken)
                ?? throw new FileNotFoundException("The retained intake source is unavailable.");
            if (asset.IntakeReceiptId != request.IntakeReceiptId
                || request.CaseId is { } caseId
                    && !await IsAssociatedAsync(db, asset.IntakeReceiptId, caseId, cancellationToken))
            {
                throw new UnauthorizedAccessException("The retained intake source is outside the authorized context.");
            }
            RequireExpected(request, asset.ContentHash, asset.ContentLength);
            var bytes = await intakeArtifactStore.ReadAsync(asset.StorageKey, cancellationToken)
                ?? throw new FileNotFoundException("The retained intake source content is unavailable.");
            Verify(bytes.Span, asset.ContentHash, asset.ContentLength);
            return new(
                new MemoryStream(bytes.ToArray(), writable: false),
                null,
                null,
                asset.Id,
                asset.ContentHash,
                asset.ContentLength,
                asset.FileName,
                asset.MediaType);
        }

        var resolved = await (
            from version in db.Set<DocumentVersionEntity>().AsNoTracking()
            join document in db.Set<CaseDocumentEntity>().AsNoTracking()
                on version.DocumentId equals document.Id
            join occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                on version.Id equals occurrence.VersionId
            join caseEntity in db.Cases.AsNoTracking()
                on document.CaseId equals caseEntity.Id
            where version.Id == request.VersionId
                && version.DocumentId == request.DocumentId
                && document.CaseId == request.CaseId
            select new { Version = version, Document = document, Occurrence = occurrence, Case = caseEntity })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new FileNotFoundException("The authorized document version is unavailable.");
        RequireExpected(request, resolved.Version.Sha256, resolved.Version.ContentLength);
        var address = new ManagedDocumentContentAddress(
            resolved.Case.Id,
            resolved.Case.Reference,
            resolved.Case.CustodyRootRemoteId,
            resolved.Occurrence.Id,
            resolved.Occurrence.Ordinal,
            resolved.Document.Id,
            resolved.Version.Id,
            resolved.Version.Version,
            resolved.Occurrence.SemanticRole,
            resolved.Version.FileName,
            resolved.Version.MediaType);
        var content = await documentContentStore.OpenReadVersionAsync(
            address,
            resolved.Version.Sha256,
            resolved.Version.ContentLength,
            cancellationToken);
        return new(
            content,
            resolved.Document.Id,
            resolved.Version.Id,
            null,
            resolved.Version.Sha256,
            resolved.Version.ContentLength,
            resolved.Version.FileName,
            resolved.Version.MediaType);
    }

    private static async Task<bool> IsAssociatedAsync(
        PegasusDbContext db,
        Guid receiptId,
        Guid caseId,
        CancellationToken cancellationToken) =>
        await db.Cases.AnyAsync(
            value => value.Id == caseId
                && (value.OriginIntakeReceiptId == receiptId
                    || value.IntakeLinks.Any(link => link.IntakeReceiptId == receiptId)),
            cancellationToken)
        || await db.Set<IntakeManualAssociationEntity>().AnyAsync(
            value => value.CaseId == caseId
                && value.IntakeReceiptId == receiptId
                && value.IsActive,
            cancellationToken);

    private static void RequireExpected(
        ReadLogicalDocumentVersionRequest request,
        string hash,
        long length)
    {
        if (length != request.ExpectedContentLength
            || !CachedDocumentContentStore.FixedHashEquals(hash, request.ExpectedSha256))
        {
            throw new InvalidDataException("The requested logical content identity does not match durable metadata.");
        }
    }

    private static void Verify(ReadOnlySpan<byte> content, string hash, long length)
    {
        var actual = Convert.ToHexString(SHA256.HashData(content));
        if (content.Length != length
            || !CachedDocumentContentStore.FixedHashEquals(actual, hash))
        {
            throw new InvalidDataException("Logical document content verification failed.");
        }
    }
}
