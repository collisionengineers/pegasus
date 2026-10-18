using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Support;

internal sealed class CommittedWorkPublisherDouble :
    ICommittedIntakeWorkPublisher,
    ICommittedExternalWorkPublisher
{
    private readonly Lock sync = new();

    public List<Guid> IntakeWorkIds { get; } = [];

    public List<Guid> ExternalWorkIds { get; } = [];

    Task ICommittedIntakeWorkPublisher.PublishAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken)
    {
        lock (sync)
        {
            IntakeWorkIds.Add(stagedReceiptId);
        }

        return Task.CompletedTask;
    }

    Task ICommittedExternalWorkPublisher.PublishAsync(
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        lock (sync)
        {
            ExternalWorkIds.Add(workItemId);
        }

        return Task.CompletedTask;
    }
}
