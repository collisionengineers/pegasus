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
        var state = await context.CaseWorkflows.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.State)
            .SingleOrDefaultAsync(cancellationToken);

        return state is null
            ? null
            : new(Enum.Parse<CaseLifecycleState>(state));
    }
}
