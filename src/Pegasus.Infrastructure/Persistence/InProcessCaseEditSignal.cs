using System.Collections.Concurrent;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The in-process <see cref="ICaseEditSignal"/>: one pending wake-up per Case, swapped out and
/// completed by each <see cref="Raise"/>. Exact on the one Web instance; a second instance would
/// still be correct, a hold late, because every waiter reads the Case before and after it waits.
/// A Case nobody raises again keeps one small entry until the process ends.
/// </summary>
public sealed class InProcessCaseEditSignal : ICaseEditSignal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _pending = new();

    public Task NextAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var source = _pending.GetOrAdd(
            caseId,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        return cancellationToken.CanBeCanceled
            ? source.Task.WaitAsync(cancellationToken)
            : source.Task;
    }

    public void Raise(Guid caseId)
    {
        if (_pending.TryRemove(caseId, out var source))
        {
            source.TrySetResult();
        }
    }
}
