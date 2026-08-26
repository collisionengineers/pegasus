using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests.Support;

internal sealed class CommittedWorkPublisherDouble :
    ICommittedIntakeWorkPublisher,
    ICommittedExternalWorkPublisher
{
    public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
