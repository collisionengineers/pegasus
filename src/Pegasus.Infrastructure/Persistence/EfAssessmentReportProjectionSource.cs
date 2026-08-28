using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Loads an <see cref="AssessmentReportProjectionInput"/> for a case by
/// reusing the same bounded Assessment workspace query as the screen, then
/// loading confirmed document metadata once. Photograph bytes use PLAT-041's
/// ordered batch route; opening the Assessment screen never reaches this
/// source or the content store.
/// </summary>
internal sealed class EfAssessmentReportProjectionSource(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    IDocumentContentStore contentStore,
    TimeProvider timeProvider) : IAssessmentReportProjectionSource
{
    private static readonly HashSet<string> PhotoMediaTypes =
        new(StringComparer.Ordinal) { "image/jpeg", "image/png", "image/webp" };

    public async Task<AssessmentReportProjectionInput?> GetAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken = default)
    {
        var workspace = await getAssessmentWorkspace.ExecuteAsync(
            new(caseId, actor),
            cancellationToken);
        if (workspace is null)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var confirmed = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                      && version.DocumentId == occurrence.DocumentId
                      && version.IsCurrent
                      && !version.IsLogicallyRemoved
                      && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                orderby occurrence.Ordinal
                select new ConfirmedDocumentRow(
                    occurrence.Id,
                    occurrence.Ordinal,
                    occurrence.DocumentId,
                    occurrence.SemanticRole,
                    version.Id,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256))
            .ToArrayAsync(cancellationToken);

        var sources = confirmed
            .Select(row => new AcceptedReportSource(
                row.FileName,
                row.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.Sha256))
            .ToArray();

        var photoRows = confirmed
            .Where(row => row.SemanticRole == DocumentSemanticRole.Image
                && PhotoMediaTypes.Contains(row.MediaType)
                && row.ContentLength is >= 0 and <= int.MaxValue)
            .ToArray();
        var reads = photoRows
            .Select(row => new ManagedDocumentContentRead(
                new ManagedDocumentContentAddress(
                    caseId,
                    workspace.Header.Reference,
                    workspace.Header.CaseRootRemoteId,
                    row.OccurrenceId,
                    row.Ordinal,
                    row.DocumentId,
                    row.VersionId,
                    row.Version,
                    row.SemanticRole,
                    row.FileName,
                    row.MediaType),
                row.Sha256,
                row.ContentLength))
            .ToArray();
        var contents = await contentStore.ReadVersionsAsync(reads, cancellationToken);
        var photos = photoRows
            .Select((row, index) => new ReportImageEvidence(
                row.FileName,
                row.MediaType,
                contents[index].ToArray(),
                row.Sha256))
            .ToArray();

        // Repair costs are never typed here: the projection derives them
        // from the Current estimate (the workspace's accepted specification)
        // through EstimateTotals, and fails closed when there is none.
        return new AssessmentReportProjectionInput(
            workspace.Assessment,
            workspace.Data.Claimant.Name.Current?.Value,
            workspace.Header.Reference,
            workspace.Data.Claim.Number.Current?.Value,
            [workspace.Header.Principal],
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
            photos,
            sources,
            Costs: null,
            CurrentEstimate: workspace.AcceptedSpecification);
    }

    private sealed record ConfirmedDocumentRow(
        Guid OccurrenceId,
        int Ordinal,
        Guid DocumentId,
        DocumentSemanticRole SemanticRole,
        Guid VersionId,
        int Version,
        string FileName,
        string MediaType,
        long ContentLength,
        string Sha256);
}
