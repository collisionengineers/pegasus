using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Case's recognised filed estimates as attachments (FRD-11): a document
/// staff or intake filed (not one Pegasus generated), whose current version is
/// confirmed in custody, not removed, and read by the Worker as an estimate,
/// with the format it was read in. Also the Case's External estimates and
/// Repair invoices a Principal's Offer ticks may offer (R1151).
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

    public async Task<IReadOnlyList<OfferedCaseFile>> ListOfferableAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadOfferableAsync(context, caseId, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// The Case's External estimates and Repair invoices as the garage
    /// facts read them (<see cref="CaseGarage.Confirmed(PegasusDbContext)"/>):
    /// not generated, current, not removed and confirmed in custody, in the
    /// Case's file order (R1151).
    /// </summary>
    internal static async Task<IReadOnlyList<OfferedCaseFile>> ReadOfferableAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken)
    {
        var rows = await (
                from occurrence in CaseGarage.Confirmed(context)
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                    && (occurrence.SemanticRole == DocumentSemanticRole.ExternalEstimate
                        || occurrence.SemanticRole == DocumentSemanticRole.RepairInvoice)
                orderby occurrence.Ordinal, version.CreatedAtUtc
                select new
                {
                    OccurrenceId = occurrence.Id,
                    occurrence.SemanticRole,
                    version.DocumentId,
                    VersionId = version.Id,
                    version.Sha256,
                    version.ContentLength,
                    version.FileName,
                    version.MediaType
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. rows
                .Select(row => new OfferedCaseFile(
                    row.OccurrenceId,
                    row.SemanticRole,
                    new StaffMailAttachment(
                        row.DocumentId, row.VersionId, row.Sha256, row.ContentLength, row.FileName, row.MediaType)))
                .Where(file => IsSendable(file.Attachment))
                .DistinctBy(file => file.Attachment.VersionId)
        ];
    }

    private static bool IsSendable(StaffMailAttachment attachment) =>
        attachment.ContentLength > 0
        && attachment.Sha256.Length == 64
        && !string.IsNullOrWhiteSpace(attachment.FileName)
        && !string.IsNullOrWhiteSpace(attachment.MediaType);
}
