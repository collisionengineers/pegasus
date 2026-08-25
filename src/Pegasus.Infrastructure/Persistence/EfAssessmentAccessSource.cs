using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfAssessmentAccessSource(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAssessmentAccessSource
{
    public async Task<AssessmentAccessState?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.CaseWorkflows.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new
            {
                item.State,
                LatestReviewVersion = context.CaseWorkflowEvents
                    .Where(history => history.CaseId == item.CaseId
                        && (history.EventType == "state_Review"
                            || history.EventType == "case_returned_to_review"
                            || history.EventType == "case_reopened_Review"))
                    .Select(history => (long?)history.AfterVersion)
                    .Max() ?? 0,
                LatestExportVersion = context.EvaFirstHandoffProxies
                    .Where(export => export.CaseId == item.CaseId)
                    .Select(export => export.LatestExportedWorkflowVersion)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new(
                Enum.Parse<CaseLifecycleState>(row.State),
                row.LatestReviewVersion,
                row.LatestExportVersion);
    }
}
