using Pegasus.Core.Tasks;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Stopping a case's chase schedule, written once. It was written out four
/// separate times — in the workflow store, the case-data store, the linked-case
/// replacement store and the intake mutation store — each an identical five-line
/// reset. Every route that makes a case terminal has to stop the chase, so the
/// copies would have gone on multiplying, and a chase left running on a closed
/// case is exactly the sort of thing one stale copy produces (INTK-029).
/// </summary>
internal static class CaseChaseState
{
    internal static void Stop(CaseWorkflowEntity workflow) => Stop(workflow.DueWork);

    internal static void Stop(CaseDueWorkEntity? dueWork)
    {
        if (dueWork is null)
        {
            return;
        }

        dueWork.State = nameof(CaseDueWorkState.Stopped);
        dueWork.NextChaseAtUtc = null;
        dueWork.HeldAtUtc = null;
        dueWork.RemainingChaseIntervalTicks = null;
        dueWork.Version++;
    }
}
