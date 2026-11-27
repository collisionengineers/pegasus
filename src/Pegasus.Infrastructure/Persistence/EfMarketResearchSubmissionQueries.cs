using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads a Case's market research submissions for the Valuation section's
/// space (issue 2166; R1313): two statements whatever their number, and one
/// for a Case with none. A file is listed while the Case document it was
/// filed as can still be opened: the report is one of the Case's own
/// documents, so staff may have removed it since (R763), and then the
/// submission stands without it.
/// </summary>
internal sealed class EfMarketResearchSubmissionQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IMarketResearchSubmissionQueries
{
    public async Task<IReadOnlyList<StoredMarketResearchSubmission>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var submissions = await context.MarketResearchSubmissions.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.SubmittedBy,
                item.SubmittedAtUtc,
                item.ProposedValue,
                item.RecordJson
            })
            .ToListAsync(cancellationToken);
        if (submissions.Count == 0)
        {
            return [];
        }

        var submissionIds = submissions.Select(item => item.Id).ToArray();
        var files = await (
                from file in context.MarketResearchSubmissionFiles.AsNoTracking()
                join occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                    on file.OccurrenceId equals occurrence.Id
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on file.VersionId equals version.Id
                where submissionIds.Contains(file.SubmissionId)
                    && occurrence.CaseId == caseId
                    && occurrence.VersionId == version.Id
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                    && !version.IsLogicallyRemoved
                orderby file.SubmissionId, file.Position
                select new
                {
                    file.SubmissionId,
                    file.Kind,
                    file.AdvertNumber,
                    file.OccurrenceId,
                    file.VersionId,
                    version.FileName,
                    version.MediaType
                })
            .ToListAsync(cancellationToken);
        var filesBySubmission = files.ToLookup(
            file => file.SubmissionId,
            file => new MarketResearchSpaceFile(
                Enum.Parse<MarketResearchFileKind>(file.Kind),
                file.AdvertNumber,
                file.OccurrenceId,
                file.VersionId,
                file.FileName,
                file.MediaType));
        return
        [
            .. submissions.Select(item => new StoredMarketResearchSubmission(
                item.Id,
                item.SubmittedBy,
                item.SubmittedAtUtc,
                item.ProposedValue,
                item.RecordJson,
                [.. filesBySubmission[item.Id]]))
        ];
    }
}
