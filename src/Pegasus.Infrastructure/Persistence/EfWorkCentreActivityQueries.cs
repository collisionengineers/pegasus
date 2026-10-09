using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's activity figures (FRD-15, operator 5 October 2026), read
/// straight from the records that hold each fact. Each figure is one command
/// that reads the week's instants, one column, and counts today and the week
/// from them.
/// </summary>
internal sealed class EfWorkCentreActivityQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IWorkCentreActivityQueries
{
    /// <summary>The workflow event that moves a Case into With Engineer.</summary>
    private const string FirstSentToEngineerEvent = "state_ReportPreparation";

    /// <summary>
    /// The workflow events that put a Case into Complete: Complete itself, a
    /// reply to a post-report query, and a query withdrawn by correction or
    /// unlink. A Case reopened later still counts for the week it completed,
    /// and a Case that enters Complete more than once counts once.
    /// </summary>
    private static readonly string[] CompletionEvents =
    [
        "case_completed",
        PostReportQueryTransitions.QueryRepliedEvent,
        PostReportQueryTransitions.QueryWithdrawnEvent
    ];

    public async Task<WorkCentreActivityCounts> GetAsync(
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // New cases counts what the New cases list counts: every Case except a
        // Triage Case (item G).
        var newCases = await CountAsync(
            context.Cases
                .AsNoTracking()
                .Where(item => item.CreatedAtUtc >= weekStartUtc && item.Type != CaseTypeCodes.Triage)
                .Select(item => item.CreatedAtUtc),
            dayStartUtc,
            cancellationToken);

        // First sent to Engineer is a Case's first entry into With Engineer,
        // counted once per Case (item E): a later return to the Engineer is
        // not a second send. Only this week's sends are read; a Case with an
        // earlier send was first sent before the week began.
        var sends = context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.EventType == FirstSentToEngineerEvent);
        var sentToEngineer = await CountAsync(
            sends
                .Where(item => item.OccurredAtUtc >= weekStartUtc
                    && !sends.Any(earlier => earlier.CaseId == item.CaseId && earlier.OccurredAtUtc < weekStartUtc))
                .GroupBy(item => item.CaseId)
                .Select(group => group.Min(item => item.OccurredAtUtc)),
            dayStartUtc,
            cancellationToken);

        // Reports sent counts sent report e-mails as the Engineer activity
        // report (MI-01) counts them, so the two agree for the same week (item F).
        var reportsSent = await CountAsync(
            context.Set<StaffMailSendOperationEntity>()
                .AsNoTracking()
                .Where(item => item.Purpose == StaffMailPurpose.CaseReport
                    && item.State == StaffMailState.Sent
                    && item.ObservedSentAtUtc >= weekStartUtc)
                .Select(item => item.ObservedSentAtUtc!.Value),
            dayStartUtc,
            cancellationToken);

        // Completed counts Cases, each once (item H): a Case's latest entry
        // this week is today exactly when any of its entries is.
        var completed = await CountAsync(
            context.CaseWorkflowEvents
                .AsNoTracking()
                .Where(item => item.OccurredAtUtc >= weekStartUtc && CompletionEvents.Contains(item.EventType))
                .GroupBy(item => item.CaseId)
                .Select(group => group.Max(item => item.OccurredAtUtc)),
            dayStartUtc,
            cancellationToken);

        // E-mails received counts mail arrivals only; a manual upload is also a
        // receipt and must not inflate the figure (PLAT-012).
        var mailbox = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var emailsReceived = await CountAsync(
            context.IntakeReceipts
                .AsNoTracking()
                .Where(item => item.ReceivedAtUtc >= weekStartUtc && item.SourceChannel == mailbox)
                .Select(item => item.ReceivedAtUtc),
            dayStartUtc,
            cancellationToken);

        return new WorkCentreActivityCounts(newCases, sentToEngineer, reportsSent, completed, emailsReceived);
    }

    /// <summary>Counts the week's instants, and those since the day began.</summary>
    private static async Task<WorkCentreActivityFigure> CountAsync(
        IQueryable<DateTimeOffset> weekInstants,
        DateTimeOffset dayStartUtc,
        CancellationToken cancellationToken)
    {
        var instants = await weekInstants.ToArrayAsync(cancellationToken);
        return new WorkCentreActivityFigure(instants.Count(instant => instant >= dayStartUtc), instants.Length);
    }
}
