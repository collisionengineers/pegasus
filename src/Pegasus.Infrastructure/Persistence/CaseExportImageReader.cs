using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.CaseExport;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// A case's eligible photographs as metadata, in ordinal order, and the read
/// that returns one photograph's bytes by itself.
/// </summary>
public sealed record CaseExportImageSet(
    IReadOnlyList<CaseExportImage> Images,
    CaseExportImageContentReader ReadContentAsync);

/// <summary>
/// Every eligible retained photograph of a case, for
/// <see cref="EfCaseExportStore"/>: the metadata of the set, and a read that
/// returns one photograph's bytes.
///
/// Eligibility itself stays in Core with
/// <see cref="CaseExportPolicy.SelectEligibleImages"/>; this reads what that
/// policy chose. No byte is read here: the archive asks for the photographs
/// one at a time as it writes them, so the set is never held in memory. Where
/// the document content cache is composed each photograph is read through it
/// first, so a repeat export does not ask Box again.
/// </summary>
public sealed class CaseExportImageReader(
    IDocumentContentStore contentStore,
    IReadCachedDocumentVersions? cachedVersions = null)
{
    public async Task<CaseExportImageSet> LoadEligibleImagesAsync(
        PegasusDbContext context,
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken)
    {
        // The Third party tag is the one tag the export reads: an image
        // wearing it stays out of the bundle (the rule itself is Core's).
        var tagIdsByOccurrence = (await context.Set<DocumentOccurrenceTagEntity>()
                .AsNoTracking()
                .Where(assignment => context.Set<DocumentOccurrenceEntity>()
                    .Any(occurrence => occurrence.Id == assignment.OccurrenceId
                        && occurrence.CaseId == caseId))
                .Select(assignment => new { assignment.OccurrenceId, assignment.TagId })
                .ToArrayAsync(cancellationToken))
            .GroupBy(row => row.OccurrenceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)[.. group.Select(row => row.TagId)]);
        var candidateRows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                join caseEntity in context.Cases.AsNoTracking()
                    on occurrence.CaseId equals caseEntity.Id
                join document in context.Set<CaseDocumentEntity>().AsNoTracking()
                    on occurrence.DocumentId equals document.Id
                where occurrence.CaseId == caseId
                      && version.DocumentId == occurrence.DocumentId
                orderby occurrence.Ordinal
                select new SelectedDocument(
                    occurrence.Id,
                    occurrence.Ordinal,
                    occurrence.DocumentId,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity,
                    occurrence.SemanticRole,
                    version.Id,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    version.CustodyStatus,
                    version.IsCurrent,
                    version.IsLogicallyRemoved,
                    document.CustodyFolder == CaseCustodyFolders.Audit
                        ? caseEntity.AuditCustodyRemoteId
                        : caseEntity.CustodyRootRemoteId,
                    version.BoxFileId,
                    version.BoxVersionId))
            .ToArrayAsync(cancellationToken);
        var eligibleVersionIds = CaseExportPolicy.SelectEligibleImages(candidateRows.Select(
                selected => new CaseExportImageCandidate(
                    selected.OccurrenceId,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.FileName,
                    selected.MediaType,
                    selected.ContentLength,
                    selected.Sha256,
                    selected.SemanticRole,
                    selected.Source,
                    selected.SourceOccurrenceIdentity,
                    selected.CustodyStatus == DocumentCustodyStatus.Confirmed,
                    selected.IsCurrent,
                    selected.IsLogicallyRemoved,
                    tagIdsByOccurrence.GetValueOrDefault(selected.OccurrenceId, []),
                    selected.Ordinal)))
            .Select(candidate => candidate.VersionId)
            .ToHashSet();

        var selectedImages = candidateRows.Where(
            selected => eligibleVersionIds.Contains(selected.VersionId)
                        && selected.ContentLength <= int.MaxValue)
            .ToArray();
        var reads = selectedImages.ToDictionary(
            selected => selected.OccurrenceId,
            selected => new ManagedDocumentContentRead(
                new ManagedDocumentContentAddress(
                    caseId,
                    caseReference,
                    selected.CaseRootRemoteId,
                    selected.OccurrenceId,
                    selected.Ordinal,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.SemanticRole,
                    selected.FileName,
                    selected.MediaType,
                    selected.BoxFileId,
                    selected.BoxVersionId),
                selected.Sha256,
                selected.ContentLength));
        var images = selectedImages.Select(selected => new CaseExportImage(
                selected.OccurrenceId,
                selected.DocumentId,
                selected.VersionId,
                selected.Version,
                selected.FileName,
                selected.MediaType,
                selected.SemanticRole,
                selected.Source,
                selected.SourceOccurrenceIdentity,
                selected.ContentLength,
                selected.Sha256,
                CustodyConfirmed: true,
                IsCurrent: true,
                selected.Ordinal))
            .ToArray();

        // One single-version read per photograph, issued when the archive
        // reaches it. Hash and length are verified by the store that reads it.
        return new(
            images,
            (image, token) => ManagedVersionBytes.ReadAsync(
                contentStore, cachedVersions, reads[image.OccurrenceId], token));
    }

    private sealed record SelectedDocument(
        Guid OccurrenceId,
        int Ordinal,
        Guid DocumentId,
        DocumentSource Source,
        string SourceOccurrenceIdentity,
        DocumentSemanticRole SemanticRole,
        Guid VersionId,
        int Version,
        string FileName,
        string MediaType,
        long ContentLength,
        string Sha256,
        DocumentCustodyStatus CustodyStatus,
        bool IsCurrent,
        bool IsLogicallyRemoved,
        string? CaseRootRemoteId,
        string? BoxFileId,
        string? BoxVersionId);
}
