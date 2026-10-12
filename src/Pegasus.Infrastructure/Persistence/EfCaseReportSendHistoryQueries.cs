using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;
using Pegasus.Core.Cases;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// What a Case's report has already been sent (v28 P23): the sends the
/// provider accepted, counted from the staff-send operations whose report
/// context is one of this Case's generations, with the report date of the one
/// sent last. A send counts once Graph accepts it (Submitted) and stays
/// counted when its Sent item is observed; the Next action follows the same
/// record rather than waiting for the Sent poll (operator, 12 October 2026).
/// A prepared, failed or cancelled delivery is not a send.
/// </summary>
internal sealed class EfCaseReportSendHistoryQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseReportSendHistoryQueries
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseReportSendHistory> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Only sends of the named work's reports count: an Audit report's
        // re-send suffix never counts the Inspection's sends, nor the other
        // way round (decision X).
        var workId = await CaseWorkScope.ResolveIdAsync(context, caseId, work, cancellationToken)
            .ConfigureAwait(false);
        var sent =
            from send in context.Set<StaffMailSendOperationEntity>().AsNoTracking()
            join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                on send.ContextId equals generation.Id
            where generation.WorkId == workId
                && send.Purpose == StaffMailPurpose.CaseReport
                && (send.State == StaffMailState.Submitted || send.State == StaffMailState.Sent)
            select new { send.CreatedAtUtc, generation.SnapshotJson };
        var sentCount = await sent.CountAsync(cancellationToken).ConfigureAwait(false);
        if (sentCount == 0)
        {
            return CaseReportSendHistory.None;
        }

        // Only the newest send's snapshot is read: the count and the last
        // report date are all the history carries.
        var latestSnapshotJson = await sent
            .OrderByDescending(row => row.CreatedAtUtc)
            .Select(row => row.SnapshotJson)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        var latest = JsonSerializer.Deserialize<CaseReportGenerationSnapshot>(
            latestSnapshotJson, SnapshotJsonOptions);
        return new(sentCount, latest?.ReportDate);
    }
}
