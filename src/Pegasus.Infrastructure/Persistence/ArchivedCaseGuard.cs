using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal static class ArchivedCaseGuard
{
    public static void RequireNotArchived(CaseWorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflow.ArchivedAtUtc is not null)
        {
            throw new CaseArchivedException(workflow.CaseId);
        }
    }

    public static void RequireMutable(CaseWorkflowEntity workflow)
    {
        RequireNotArchived(workflow);
        RequireOpenState(workflow.CaseId, workflow.State);
    }

    /// <summary>
    /// Loads the Case's workflow tracked and requires it mutable, so a caller
    /// that goes on to change it reads it once.
    /// </summary>
    public static async Task<CaseWorkflowEntity> RequireMutableAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        RequireMutable(workflow);
        return workflow;
    }

    private static void RequireOpenState(Guid caseId, string state)
    {
        if (!Enum.TryParse<CaseLifecycleState>(state, ignoreCase: false, out var lifecycleState))
        {
            throw new InvalidDataException(
                $"Case '{caseId}' has an unrecognized lifecycle state.");
        }

        if (CaseLifecycleRules.IsTerminal(lifecycleState))
        {
            throw new CaseTerminalMutationException(caseId);
        }
    }
}

internal static class CaseTerminalReadinessGuard
{
    public static async Task RequireNoOpenTasksAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (await context.CaseTasks.AnyAsync(
                item => item.CaseId == caseId
                    && item.State == nameof(CaseTaskState.Open),
                cancellationToken))
        {
            throw new InvalidOperationException(CaseTaskRules.OpenTasksBlockTerminal);
        }
    }
}
