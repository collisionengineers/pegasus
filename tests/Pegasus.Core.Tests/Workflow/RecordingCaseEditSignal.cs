using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

/// <summary>
/// An <see cref="ICaseEditSignal"/> that records every raise and lets a test complete the next
/// wake-up by hand.
/// </summary>
internal sealed class RecordingCaseEditSignal : ICaseEditSignal
{
    private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<Guid> Raised { get; } = [];

    public int Waits { get; private set; }

    public Task NextAsync(Guid caseId, CancellationToken cancellationToken)
    {
        Waits += 1;
        return _next.Task.WaitAsync(cancellationToken);
    }

    public void Raise(Guid caseId)
    {
        Raised.Add(caseId);
        var current = _next;
        _next = new(TaskCreationOptions.RunContinuationsAsynchronously);
        current.TrySetResult();
    }
}
