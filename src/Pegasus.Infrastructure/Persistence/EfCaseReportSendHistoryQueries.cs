using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;
using Pegasus.Core.Cases;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// What a Case's report has already been sent (v28 P23): the sends that
/// actually left the approved mailbox, counted from the staff-send operations
/// that carried one of this work's generations (either report of a send that
/// carried both), with the report date of the one sent last. A
/// prepared-but-unsent delivery is not a send. The accepted count also counts
/// the sends Graph accepted whose Sent item is not yet observed: the Next
/// action follows it rather than waiting for the Sent poll (operator, 12
/// October 2026), while the re-send suffix keeps to observed sends, so a
/// replayed send keeps its name.
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
        var accepted =
            from send in context.Set<StaffMailSendOperationEntity>().AsNoTracking()
            from generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
            where (generation.Id == send.ContextId || generation.Id == send.SecondContextId)
                && generation.WorkId == workId
                && send.Purpose == StaffMailPurpose.CaseReport
                && (send.State == StaffMailState.Submitted || send.State == StaffMailState.Sent)
            select new { send.State, send.ObservedSentAtUtc, generation.SnapshotJson };
        var acceptedCount = await accepted.CountAsync(cancellationToken).ConfigureAwait(false);
        if (acceptedCount == 0)
        {
            return CaseReportSendHistory.None;
        }

        var sent = accepted.Where(row => row.State == StaffMailState.Sent);
        var sentCount = await sent.CountAsync(cancellationToken).ConfigureAwait(false);
        if (sentCount == 0)
        {
            return new(0, null, acceptedCount);
        }

        // Only the newest send's snapshot is read: the counts and the last
        // report date are all the history carries.
        var latestSnapshotJson = await sent
            .OrderByDescending(row => row.ObservedSentAtUtc)
            .Select(row => row.SnapshotJson)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        var latest = JsonSerializer.Deserialize<CaseReportGenerationSnapshot>(
            latestSnapshotJson, SnapshotJsonOptions);
        return new(sentCount, latest?.ReportDate, acceptedCount);
    }
}
