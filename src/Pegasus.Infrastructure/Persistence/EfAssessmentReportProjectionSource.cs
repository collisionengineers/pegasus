using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Loads an <see cref="AssessmentReportProjectionInput"/> for a case by
/// reusing the same bounded Assessment workspace query as the screen, then
/// loading confirmed document metadata once. Photograph bytes use PLAT-041's
/// ordered batch route; opening the Assessment screen never reaches this
/// source or the content store.
/// </summary>
/// <remarks>
/// The report date is deliberately not set here: a report date is frozen when
/// a generation happens, or stated by a labelled preview. Images are the
/// operator's prepared set (<see cref="ICaseAssetPreparationQueries"/>), never
/// every confirmed image, and each carries its role, order, rotation and crop.
/// </remarks>
internal sealed class EfAssessmentReportProjectionSource(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    IDocumentContentStore contentStore,
    IStaffAccountQueries staffAccountQueries,
    ICaseAssetPreparationQueries assetPreparationQueries,
    IListAppliedValuations listAppliedValuations)
    : IAssessmentReportProjectionSource, ICaseReportSnapshotSource
{
    /// <summary>The preview path: the same facts, with image bytes read.</summary>
    public async Task<AssessmentReportProjectionInput?> GetAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken = default) =>
        (await LoadAsync(caseId, actor, withImageContent: true, cancellationToken))?.Projection;

    /// <summary>
    /// The freeze path: identical facts with image bytes omitted, plus the
    /// readiness inputs, so a generation neither reads image bytes twice nor
    /// decides readiness from anything but persisted state.
    /// </summary>
    async Task<CaseReportFreezeInputs?> ICaseReportSnapshotSource.GetAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken) =>
        await LoadAsync(caseId, actor, withImageContent: false, cancellationToken);

    private async Task<CaseReportFreezeInputs?> LoadAsync(
        Guid caseId, ActionActor actor, bool withImageContent, CancellationToken cancellationToken)
    {
        var workspace = await getAssessmentWorkspace.ExecuteAsync(
            new(caseId, actor),
            cancellationToken);
        if (workspace is null)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new { item.AssignedEngineerId, item.SignOffEngineerId, item.Version })
            .SingleAsync(cancellationToken);
        var profiles = await staffAccountQueries.ListSignOffEngineersAsync(cancellationToken);
        var signOffEngineer = CaseSignOffEngineerResolver.Resolve(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
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
                    version.Sha256,
                    version.BoxFileId,
                    version.BoxVersionId))
            .ToArrayAsync(cancellationToken);

        var sources = confirmed
            .Select(row => new AcceptedReportSource(
                row.FileName,
                row.Version.ToString(CultureInfo.InvariantCulture),
                row.Sha256,
                row.DocumentId,
                row.VersionId,
                row.BoxFileId,
                row.BoxVersionId))
            .ToArray();

        // Only the operator's prepared images, in the report's own order.
        var preparations = await assetPreparationQueries.ListForCaseAsync(caseId, cancellationToken);
        var prepared = CaseAssetPreparationPolicy.ForReport(preparations);
        var photoRows = (
                from image in prepared
                join row in confirmed on image.OccurrenceId equals row.OccurrenceId
                where row.ContentLength is >= 0 and <= int.MaxValue
                select (Image: image, Row: row))
            .ToArray();
        var reads = photoRows
            .Select(pair => new ManagedDocumentContentRead(
                new ManagedDocumentContentAddress(
                    caseId,
                    workspace.Header.Reference,
                    workspace.Header.CaseRootRemoteId,
                    pair.Row.OccurrenceId,
                    pair.Row.Ordinal,
                    pair.Row.DocumentId,
                    pair.Row.VersionId,
                    pair.Row.Version,
                    pair.Row.SemanticRole,
                    pair.Row.FileName,
                    pair.Row.MediaType),
                pair.Row.Sha256,
                pair.Row.ContentLength))
            .ToArray();
        var contents = withImageContent
            ? await contentStore.ReadVersionsAsync(reads, cancellationToken)
            : [];
        var photos = photoRows
            .Select((pair, index) => new ReportImageEvidence(
                pair.Row.FileName,
                pair.Row.MediaType,
                withImageContent ? contents[index].ToArray() : [],
                pair.Row.Sha256,
                pair.Image.Role,
                pair.Image.Order,
                pair.Image.Rotation,
                pair.Image.Crop,
                pair.Row.OccurrenceId,
                pair.Row.VersionId,
                pair.Row.BoxFileId,
                pair.Row.BoxVersionId))
            .ToArray();

        var applied = await listAppliedValuations.ExecuteAsync(caseId, cancellationToken);
        var latestApplied = applied
            .OrderByDescending(valuation => valuation.AcceptedAtUtc)
            .FirstOrDefault();
        var guides = await GuidesOfAsync(context, caseId, cancellationToken);

        // Repair costs are never typed here: the projection derives them from
        // the Current estimate (the workspace's accepted specification) through
        // EstimateTotals, and fails closed when there is none.
        var projection = new AssessmentReportProjectionInput(
            workspace.Assessment,
            workspace.Data.Claimant.Name.Current?.Value,
            workspace.Header.Reference,
            workspace.Data.Claim.Number.Current?.Value,
            [workspace.Header.Principal],
            ReportDate: null,
            photos,
            sources,
            CurrentEstimate: workspace.AcceptedSpecification,
            Signatory: signOffEngineer is null
                ? null
                : new ReportSignatory(
                    signOffEngineer.PrintedName,
                    signOffEngineer.Qualifications,
                    signOffEngineer.Signature,
                    signOffEngineer.SignatureContentType),
            Guides: guides,
            ValuationCommentary: latestApplied?.Reason);

        var readiness = new CaseReportReadinessInput(
            workspace.Assessment,
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles,
            workspace.AcceptedSpecification,
            latestApplied,
            preparations,
            confirmed.ToDictionary(
                row => row.OccurrenceId,
                row => new DocumentVersion(
                    row.VersionId, row.DocumentId, row.Version, row.FileName, row.MediaType,
                    row.ContentLength, row.Sha256, DocumentCustodyStatus.Confirmed,
                    default, string.Empty, true, false, null)));

        return new CaseReportFreezeInputs(
            projection, readiness, workspace.Header.Reference, workflow.Version);
    }

    /// <summary>
    /// The valuation guides the Case's accepted values were taken from. Only
    /// these decide the report's source-aware guide wording.
    /// </summary>
    private static async Task<ReportGuideSources> GuidesOfAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken)
    {
        var names = await context.CaseValuations
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.Source)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var guides = new List<ValuationSource>(names.Length);
        foreach (var name in names)
        {
            if (Enum.TryParse<ValuationSource>(name, out var source))
            {
                guides.Add(source);
            }
        }

        return new ReportGuideSources(guides);
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
        string Sha256,
        string? BoxFileId,
        string? BoxVersionId);
}
