using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Case's recognised filed estimates as attachments (FRD-11): a document
/// staff or intake filed (not one Pegasus generated), whose current version is
/// confirmed in custody, not removed, and read by the Worker as an estimate,
/// with the format it was read in.
/// </summary>
internal sealed class EfFiledEstimateAttachmentQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IFiledEstimateAttachmentQueries
{
    public async Task<IReadOnlyList<FiledEstimateAttachment>> ListAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(context, caseId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<FiledEstimateAttachment>> ReadAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken)
    {
        var rows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                    && occurrence.Source != DocumentSource.Generated
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                    && version.IsRecognisedEstimate == true
                orderby occurrence.Ordinal, version.CreatedAtUtc
                select new
                {
                    version.DocumentId,
                    VersionId = version.Id,
                    version.Sha256,
                    version.ContentLength,
                    version.FileName,
                    version.MediaType,
                    version.RecognisedEstimateProvider
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. rows
                .Select(row => new FiledEstimateAttachment(
                    new StaffMailAttachment(
                        row.DocumentId, row.VersionId, row.Sha256, row.ContentLength, row.FileName, row.MediaType),
                    row.RecognisedEstimateProvider))
                .Where(estimate => IsSendable(estimate.Attachment))
                .DistinctBy(estimate => estimate.Attachment.VersionId)
        ];
    }

    private static bool IsSendable(StaffMailAttachment attachment) =>
        attachment.ContentLength > 0
        && attachment.Sha256.Length == 64
        && !string.IsNullOrWhiteSpace(attachment.FileName)
        && !string.IsNullOrWhiteSpace(attachment.MediaType);
}
