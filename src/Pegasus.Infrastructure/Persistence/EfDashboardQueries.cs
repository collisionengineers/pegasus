using Microsoft.EntityFrameworkCore;
using Pegasus.Core.ImageIntake;
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
        var reportPreparation = CaseLifecycleState.ReportPreparation.ToString();
        var postReport = CaseLifecycleState.PostReport.ToString();
        var complete = CaseLifecycleState.PostReportComplete.ToString();

        var counts = await context.CaseWorkflows
            .AsNoTracking()
            .Where(workflow =>
                workflow.State == notReady
                || workflow.State == review
                || workflow.State == held
                || workflow.State == reportPreparation
                || workflow.State == postReport
                || workflow.State == complete)
            .GroupBy(workflow => workflow.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        int For(string state) =>
            counts.SingleOrDefault(item => item.State == state)?.Count ?? 0;

        // Awaiting instruction has no CaseWorkflows row until it merges. Its
        // count mirrors Cases/Index.cshtml.cs LoadAwaitingAsync: the lifecycle
        // is AwaitingInstruction and the origin receipt has no current case
        // association. A reversed manual association overrides an older
        // accepted link in the same way as EfImageIntakeStore.ProjectAsync.
        var awaitingInstruction = EfImageIntakeStore.ToCode(ImageInitiatedCaseState.AwaitingInstruction);
        var awaitingInstructionCount = await context.ImageIntakes
            .AsNoTracking()
            .CountAsync(
                item => item.LifecycleState == awaitingInstruction
                    && !context.IntakeManualAssociations.Any(association =>
                        association.IntakeReceiptId == item.OriginReceiptId && association.IsActive)
                    && (context.IntakeManualAssociations.Any(association =>
                            association.IntakeReceiptId == item.OriginReceiptId)
                        || !context.CaseIntakeLinks.Any(link =>
                            link.IntakeReceiptId == item.OriginReceiptId)),
                cancellationToken);

        return new(
            For(notReady),
            For(review),
            For(held),
            For(reportPreparation) + For(postReport),
            awaitingInstructionCount,
            For(complete));
    }

}
