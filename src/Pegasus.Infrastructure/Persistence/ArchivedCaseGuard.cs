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

    public static async Task RequireMutableAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        var mutationState = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new { item.ArchivedAtUtc, item.State })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        if (mutationState.ArchivedAtUtc is not null)
        {
            throw new CaseArchivedException(caseId);
        }

        RequireOpenState(caseId, mutationState.State);
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
            throw new InvalidOperationException(
                "Complete or cancel every open case task before terminalizing or archiving the case.");
        }
    }
}

internal sealed class CaseTerminalMutationException(Guid caseId)
    : InvalidOperationException(
        $"Closed case '{caseId}' is application read-only until an authorized reopen.")
{
    public Guid CaseId { get; } = caseId;
}
