using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// What a Case's report has already been sent (v28 P23): the sends that
/// actually left the approved mailbox, counted from the staff-send operations
/// whose report context is one of this Case's generations, with the report
/// date of the one sent last. A prepared-but-unsent delivery is not a send.
/// </summary>
internal sealed class EfCaseReportSendHistoryQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseReportSendHistoryQueries
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseReportSendHistory> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Only sends of the current work's reports count: an Audit report's
        // re-send suffix never counts the Inspection's sends (decision X).
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken)
            .ConfigureAwait(false);
        var sends = await (
                from send in context.Set<StaffMailSendOperationEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on send.ContextId equals generation.Id
                where generation.WorkId == workId
                    && send.Purpose == StaffMailPurpose.CaseReport
                    && send.State == StaffMailState.Sent
                orderby send.ObservedSentAtUtc descending
                select new { generation.SnapshotJson })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (sends.Count == 0)
        {
            return CaseReportSendHistory.None;
        }

        var latest = JsonSerializer.Deserialize<CaseReportGenerationSnapshot>(
            sends[0].SnapshotJson, SnapshotJsonOptions);
        return new(sends.Count, latest?.ReportDate);
    }
}
