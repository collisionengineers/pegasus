using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Every eligible retained photograph of a case, with its bytes.
///
/// This was private to <see cref="EvaHandoffStore"/> until EXT-04 gave the
/// case a second way to reach EVA. Both routes must send exactly the same
/// photographs in exactly the same order — an operator comparing the drag-and-
/// drop bundle against what EVA received should find no difference — and the
/// only way to guarantee that is one query, not two that agree today.
///
/// Eligibility itself stays in Core with
/// <see cref="EvaHandoffPolicy.SelectEligibleImages"/>; this reads what that
/// policy chose.
/// </summary>
public sealed class EvaCaseImageReader(IDocumentContentStore contentStore)
{
    public async Task<List<EvaBundleImage>> LoadEligibleImagesAsync(
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
                where occurrence.CaseId == caseId
                      && version.DocumentId == occurrence.DocumentId
                orderby occurrence.Ordinal
                select new SelectedDocument(
                    occurrence.Id,
                    occurrence.Ordinal,
                    occurrence.CaseId,
                    occurrence.DocumentId,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity,
                    occurrence.SemanticRole,
                    version.Id,
                    version.DocumentId,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    version.CustodyStatus,
                    version.IsCurrent,
                    version.IsLogicallyRemoved,
                    caseEntity.CustodyRootRemoteId))
            .ToArrayAsync(cancellationToken);
        var eligibleVersionIds = EvaHandoffPolicy.SelectEligibleImages(candidateRows.Select(
                selected => new EvaHandoffImageCandidate(
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
        var caseRootRemoteId = selectedImages.Length == 0
            ? null
            : selectedImages[0].CaseRootRemoteId;
        var reads = selectedImages.Select(selected => new ManagedDocumentContentRead(
                new ManagedDocumentContentAddress(
                    caseId,
                    caseReference,
                    caseRootRemoteId,
                    selected.OccurrenceId,
                    selected.Ordinal,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.SemanticRole,
                    selected.FileName,
                    selected.MediaType),
                selected.Sha256,
                selected.ContentLength))
            .ToArray();
        var contents = await contentStore.ReadVersionsAsync(reads, cancellationToken);

        var images = new List<EvaBundleImage>(selectedImages.Length);
        for (var index = 0; index < selectedImages.Length; index++)
        {
            var selected = selectedImages[index];
            images.Add(new(
                selected.OccurrenceId,
                selected.DocumentId,
                selected.VersionId,
                selected.Version,
                selected.FileName,
                selected.MediaType,
                selected.SemanticRole,
                selected.Source,
                selected.SourceOccurrenceIdentity,
                contents[index].ToArray(),
                selected.Sha256,
                CustodyConfirmed: true,
                IsCurrent: true,
                selected.Ordinal));
        }

        return images;
    }

    private sealed record SelectedDocument(
        Guid OccurrenceId,
        int Ordinal,
        Guid CaseId,
        Guid DocumentId,
        DocumentSource Source,
        string SourceOccurrenceIdentity,
        DocumentSemanticRole SemanticRole,
        Guid VersionId,
        Guid VersionDocumentId,
        int Version,
        string FileName,
        string MediaType,
        long ContentLength,
        string Sha256,
        DocumentCustodyStatus CustodyStatus,
        bool IsCurrent,
        bool IsLogicallyRemoved,
        string? CaseRootRemoteId);
}
