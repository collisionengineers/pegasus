using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's activity figures (FRD-15, operator 5 October 2026), read
/// straight from the records that hold each fact. Five aggregate commands;
/// nothing projects rows into memory except the two instants a day and a week
/// are both counted from.
/// </summary>
internal sealed class EfWorkCentreActivityQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IWorkCentreActivityQueries
{
    /// <summary>The workflow event that moves a Case into With Engineer.</summary>
    private const string FirstSentToEngineerEvent = "state_ReportPreparation";

    /// <summary>
    /// The workflow events that put a Case into Complete: Complete itself, a
    /// reply to a post-report query, and a query withdrawn by correction or
    /// unlink. A Case reopened later still counts for the week it completed.
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

        // New cases today counts what the New cases list counts: every Case
        // except a Triage Case (item G).
        var newCasesToday = await context.Cases
            .AsNoTracking()
            .CountAsync(
                item => item.CreatedAtUtc >= dayStartUtc && item.Type != CaseTypeCodes.Triage,
                cancellationToken);

        // First sent to Engineer is a Case's first entry into With Engineer,
        // counted once per Case (item E): a later return to the Engineer is
        // not a second send.
        var sentToEngineer = await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.EventType == FirstSentToEngineerEvent)
            .GroupBy(item => item.CaseId)
            .Select(group => group.Min(item => item.OccurredAtUtc))
            .Where(instant => instant >= weekStartUtc)
            .ToArrayAsync(cancellationToken);

        // Reports sent counts sent report e-mails as the Engineer activity
        // report (MI-01) counts them, so the two agree for the same week (item F).
        var reportsSent = await context.Set<StaffMailSendOperationEntity>()
            .AsNoTracking()
            .Where(item => item.Purpose == StaffMailPurpose.CaseReport
                && item.State == StaffMailState.Sent
                && item.ObservedSentAtUtc >= weekStartUtc)
            .Select(item => item.ObservedSentAtUtc!.Value)
            .ToArrayAsync(cancellationToken);

        var completedThisWeek = await context.CaseWorkflowEvents
            .AsNoTracking()
            .CountAsync(
                item => item.OccurredAtUtc >= weekStartUtc && CompletionEvents.Contains(item.EventType),
                cancellationToken);

        // E-mails received counts mail arrivals only; a manual upload is also a
        // receipt and must not inflate the figure (PLAT-012).
        var mailbox = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var emailsReceivedToday = await context.IntakeReceipts
            .AsNoTracking()
            .CountAsync(
                item => item.ReceivedAtUtc >= dayStartUtc && item.SourceChannel == mailbox,
                cancellationToken);

        return new WorkCentreActivityCounts(
            newCasesToday,
            sentToEngineer.Count(instant => instant >= dayStartUtc),
            sentToEngineer.Length,
            reportsSent.Count(instant => instant >= dayStartUtc),
            reportsSent.Length,
            completedThisWeek,
            emailsReceivedToday);
    }
}
