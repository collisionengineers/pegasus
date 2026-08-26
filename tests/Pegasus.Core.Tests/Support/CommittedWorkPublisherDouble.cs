using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Support;

internal sealed class CommittedWorkPublisherDouble :
    ICommittedIntakeWorkPublisher,
    ICommittedExternalWorkPublisher
{
    public List<Guid> IntakeWorkIds { get; } = [];

    public List<Guid> ExternalWorkIds { get; } = [];

    Task ICommittedIntakeWorkPublisher.PublishAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken)
    {
        IntakeWorkIds.Add(stagedReceiptId);
        return Task.CompletedTask;
    }

    Task ICommittedExternalWorkPublisher.PublishAsync(
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        ExternalWorkIds.Add(workItemId);
        return Task.CompletedTask;
    }
}
