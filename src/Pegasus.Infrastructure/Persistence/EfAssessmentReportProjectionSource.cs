using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Loads an <see cref="AssessmentReportProjectionInput"/> for a case by
/// composing the same accepted sources the rest of the app already reads
/// from: <see cref="IGetCase"/> for case identity, addressee and
/// authorisation (its own <c>StaffAuthorization</c> check, unchanged), <see
/// cref="IGetCaseAssessment"/> for the assessment record, and a direct
/// custody-document query — mirroring <c>EvaHandoffStore</c>'s own image
/// query — for confirmed source and photograph evidence. <see
/// cref="IGetCase"/>'s own <c>Documents</c> projection does not carry a real
/// occurrence ordinal (<c>EfCaseQueryStore.ReadDocumentsAsync</c> never sets
/// it), and <c>BoxDocumentContentStore</c> rejects an ordinal of zero, so
/// photograph content is read from a query that keeps the real value instead.
/// </summary>
internal sealed class EfAssessmentReportProjectionSource(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IGetCase getCase,
    IGetCaseAssessment getCaseAssessment,
    IDocumentContentStore contentStore,
    TimeProvider timeProvider) : IAssessmentReportProjectionSource
{
    private static readonly HashSet<string> PhotoMediaTypes =
        new(StringComparer.Ordinal) { "image/jpeg", "image/png", "image/webp" };

    public async Task<AssessmentReportProjectionInput?> GetAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken = default)
    {
        var details = await getCase.ExecuteAsync(new GetCaseQuery(caseId, actor), cancellationToken);
        if (details is null)
        {
            return null;
        }

        var assessment = await getCaseAssessment.ExecuteAsync(caseId, cancellationToken);
        if (assessment is null)
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

        var photos = new List<ReportImageEvidence>();
        foreach (var row in confirmed)
        {
            if (row.SemanticRole != DocumentSemanticRole.Image
                || !PhotoMediaTypes.Contains(row.MediaType)
                || row.ContentLength is < 0 or > int.MaxValue)
            {
                continue;
            }

            await using var content = await contentStore.OpenReadVersionAsync(
                new ManagedDocumentContentAddress(
                    caseId,
                    details.Summary.Reference,
                    row.OccurrenceId,
                    row.Ordinal,
                    row.DocumentId,
                    row.VersionId,
                    row.Version,
                    row.SemanticRole,
                    row.FileName,
                    row.MediaType),
                row.Sha256,
                row.ContentLength,
                cancellationToken);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)row.ContentLength));
            await content.ReadExactlyAsync(bytes, cancellationToken);
            photos.Add(new ReportImageEvidence(row.FileName, row.MediaType, bytes, row.Sha256));
        }

        // Repair-cost figures have no accepted formula anywhere in the
        // domain yet (see the remarks on AssessmentReportProjectionInput);
        // this production source never fabricates one.
        return new AssessmentReportProjectionInput(
            assessment,
            details.Summary.Claimant,
            details.Summary.Reference,
            details.Summary.ClaimNumber,
            [details.Summary.Principal],
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
            photos,
            sources,
            Costs: null);
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
