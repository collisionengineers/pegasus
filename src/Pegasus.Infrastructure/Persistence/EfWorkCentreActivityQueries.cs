using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's activity figures (FRD-15, operator 5 October 2026), read
/// straight from the records that hold each fact. Each figure is one
/// conditional aggregate in SQL on its own context, and the four run
/// concurrently.
/// </summary>
internal sealed class EfWorkCentreActivityQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IWorkCentreActivityQueries
{
    /// <summary>The workflow event that moves a Case into With Engineer.</summary>
    private const string FirstSentToEngineerEvent = "state_ReportPreparation";

    public async Task<WorkCentreActivityCounts> GetAsync(
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken)
    {
        // The four figures are independent reads, each on its own context.
        var newCases = CountAsync(NewCases, dayStartUtc, weekStartUtc, cancellationToken);
        var sentToEngineer = CountAsync(SentToEngineer, dayStartUtc, weekStartUtc, cancellationToken);
        var reportsSent = CountAsync(ReportsSent, dayStartUtc, weekStartUtc, cancellationToken);
        var emailsReceived = CountAsync(EmailsReceived, dayStartUtc, weekStartUtc, cancellationToken);

        await Task.WhenAll(newCases, sentToEngineer, reportsSent, emailsReceived);
        return new WorkCentreActivityCounts(
            await newCases, await sentToEngineer, await reportsSent, await emailsReceived);
    }

    // New cases counts what the New cases list counts: every Case except a
    // Triage Case (item G).
    private static IQueryable<DateTimeOffset> NewCases(PegasusDbContext context, DateTimeOffset weekStartUtc) =>
        context.Cases
            .Where(item => item.CreatedAtUtc >= weekStartUtc && item.Type != CaseTypeCodes.Triage)
            .Select(item => item.CreatedAtUtc);

    // First sent to Engineer is a Case's first entry into With Engineer,
    // counted once per Case (item E): a later return to the Engineer is not a
    // second send. Only this week's sends are read; a Case with an earlier
    // send was first sent before the week began.
    private static IQueryable<DateTimeOffset> SentToEngineer(PegasusDbContext context, DateTimeOffset weekStartUtc)
    {
        var sends = context.CaseWorkflowEvents.Where(item => item.EventType == FirstSentToEngineerEvent);
        return sends
            .Where(item => item.OccurredAtUtc >= weekStartUtc
                && !sends.Any(earlier => earlier.CaseId == item.CaseId && earlier.OccurredAtUtc < weekStartUtc))
            .GroupBy(item => item.CaseId)
            .Select(group => group.Min(item => item.OccurredAtUtc));
    }

    // Reports sent counts reports, each once, on its first send (R673): an
    // Inspection + Audit Case's two reports count two, even when one e-mail
    // carried both, and a re-send is not another report. Only this week's
    // sends are read; a report with an earlier send was first sent before the
    // week began.
    private static IQueryable<DateTimeOffset> ReportsSent(PegasusDbContext context, DateTimeOffset weekStartUtc)
    {
        var sends = ReportSends.All(context);
        return sends
            .Where(item => item.SentAtUtc >= weekStartUtc
                && !sends.Any(earlier => earlier.WorkId == item.WorkId && earlier.SentAtUtc < weekStartUtc))
            .GroupBy(item => item.WorkId)
            .Select(group => group.Min(item => item.SentAtUtc));
    }

    // E-mails received counts mail arrivals only; a manual upload is also a
    // receipt and must not inflate the figure (PLAT-012).
    private static IQueryable<DateTimeOffset> EmailsReceived(PegasusDbContext context, DateTimeOffset weekStartUtc)
    {
        var mailbox = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        return context.IntakeReceipts
            .Where(item => item.ReceivedAtUtc >= weekStartUtc && item.SourceChannel == mailbox)
            .Select(item => item.ReceivedAtUtc);
    }

    /// <summary>
    /// Counts one figure in one command, on its own context: the week's
    /// instants, and those since the day began. One statement reads one
    /// snapshot, so the day never exceeds the week.
    /// </summary>
    private async Task<WorkCentreActivityFigure> CountAsync(
        Func<PegasusDbContext, DateTimeOffset, IQueryable<DateTimeOffset>> weekInstants,
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var counts = await weekInstants(context, weekStartUtc)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Week = group.Count(),
                Today = group.Count(instant => instant >= dayStartUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);
        return counts is null
            ? new WorkCentreActivityFigure(0, 0)
            : new WorkCentreActivityFigure(counts.Today, counts.Week);
    }
}
