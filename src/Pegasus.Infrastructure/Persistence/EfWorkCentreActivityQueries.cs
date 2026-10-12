using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's activity figures (FRD-15, operator 5 October 2026), read
/// straight from the records that hold each fact. Each figure is counted in
/// SQL on its own context, and the five run concurrently.
/// </summary>
internal sealed class EfWorkCentreActivityQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IWorkCentreActivityQueries
{
    /// <summary>The workflow event that moves a Case into With Engineer.</summary>
    private const string FirstSentToEngineerEvent = "state_ReportPreparation";

    /// <summary>The workflow events that link a Case's report-Sent evidence, by staff or by the Worker.</summary>
    private static readonly string[] ReportSentEvents =
    [
        "report_evidence_linked",
        "report_evidence_auto_linked"
    ];

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

    // Reports sent counts Cases whose first report was sent, each once
    // (operator, 11 October 2026): the first link of report-Sent evidence; a
    // later re-send is not a second report. Only this week's links are read; a
    // Case with an earlier link was first reported before the week began.
    private static IQueryable<DateTimeOffset> ReportsSent(PegasusDbContext context, DateTimeOffset weekStartUtc)
    {
        var links = context.CaseWorkflowEvents.Where(item => ReportSentEvents.Contains(item.EventType));
        return links
            .Where(item => item.OccurredAtUtc >= weekStartUtc
                && !links.Any(earlier => earlier.CaseId == item.CaseId && earlier.OccurredAtUtc < weekStartUtc))
            .GroupBy(item => item.CaseId)
            .Select(group => group.Min(item => item.OccurredAtUtc));
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
    /// Counts one figure in SQL, on its own context: the week's instants, and
    /// those since the day began.
    /// </summary>
    private async Task<WorkCentreActivityFigure> CountAsync(
        Func<PegasusDbContext, DateTimeOffset, IQueryable<DateTimeOffset>> weekInstants,
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var instants = weekInstants(context, weekStartUtc);
        var week = await instants.CountAsync(cancellationToken);
        var today = await instants.CountAsync(instant => instant >= dayStartUtc, cancellationToken);
        // The two counts are separate commands; a row landing between them
        // must not make today exceed the week.
        return new WorkCentreActivityFigure(Math.Min(today, week), week);
    }
}
