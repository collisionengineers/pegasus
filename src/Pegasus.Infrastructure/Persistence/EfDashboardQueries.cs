using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The dashboard's counts, read straight from the records that hold the fact.
/// </summary>
/// <remarks>
/// Each of these is a single aggregate query. The dashboard is the most
/// frequently loaded screen in the product, so none of them projects rows into
/// memory to count them.
/// </remarks>
internal sealed class EfDashboardQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IDashboardQueries
{
    private readonly IDbContextFactory<PegasusDbContext> contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var notReady = CaseLifecycleState.NotReady.ToString();
        var review = CaseLifecycleState.Review.ToString();
        var held = CaseLifecycleState.Held.ToString();

        var counts = await context.CaseWorkflows
            .AsNoTracking()
            .Where(workflow =>
                workflow.State == notReady
                || workflow.State == review
                || workflow.State == held)
            .GroupBy(workflow => workflow.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        int For(string state) =>
            counts.SingleOrDefault(item => item.State == state)?.Count ?? 0;

        return new(For(notReady), For(review), For(held));
    }

    public async Task<CaseActivityCounts> GetCaseActivityCountsAsync(
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var newCasesToday = await context.Cases
            .AsNoTracking()
            .CountAsync(item => item.CreatedAtUtc >= dayStartUtc, cancellationToken);

        // The first-handoff proxy is the recorded fact that a case reached an
        // Engineer. A workflow transition would only say the case became
        // eligible to be sent, which is a different claim.
        var sentToEngineer = await context.EvaFirstHandoffProxies
            .AsNoTracking()
            .Where(item => item.RecordedAtUtc >= weekStartUtc)
            .Select(item => item.RecordedAtUtc)
            .ToArrayAsync(cancellationToken);

        // Case-linked only: a sent message that was never attributed to a case
        // is not evidence that a report was delivered for one.
        var reportsSent = await context.CaseReportSentEvidence
            .AsNoTracking()
            .Where(item => item.CaseId != null && item.SentAtUtc >= weekStartUtc)
            .Select(item => item.SentAtUtc)
            .ToArrayAsync(cancellationToken);

        return new(
            newCasesToday,
            sentToEngineer.Count(recordedAtUtc => recordedAtUtc >= dayStartUtc),
            sentToEngineer.Length,
            reportsSent.Count(sentAtUtc => sentAtUtc >= dayStartUtc),
            reportsSent.Length);
    }

    public async Task<MailActivityCounts> GetMailActivityCountsAsync(
        DateTimeOffset dayStartUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var receivedToday = await context.IntakeReceipts
            .AsNoTracking()
            .CountAsync(item => item.ReceivedAtUtc >= dayStartUtc, cancellationToken);

        // The persisted decision is the snake_case code, not the enum name.
        // Comparing against `IntakeDecision.NeedsSorting.ToString()` matched
        // nothing, so this tile read 0 for as long as it existed — including
        // with a Needs sorting receipt sitting one click away in the Inbox.
        // The store owns the code, so the count asks it rather than spelling
        // the string a second time and inviting the same drift back.
        var needsSorting = EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting);
        var needsSortingCount = await context.IntakeReceipts
            .AsNoTracking()
            .CountAsync(item => item.Decision == needsSorting, cancellationToken);

        var unidentifiedCount = await context.Set<UnidentifiedItemEntity>()
            .AsNoTracking()
            .CountAsync(item => item.State == "Open", cancellationToken);

        return new(receivedToday, needsSortingCount) { Unidentified = unidentifiedCount };
    }
}
