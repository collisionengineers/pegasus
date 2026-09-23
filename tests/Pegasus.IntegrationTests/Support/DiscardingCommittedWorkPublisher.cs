using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// Discards what it is given. These tests assert persisted state, not that work
/// was published, so nothing here is recorded.
///
/// Named for what it does because the Core suite has a recording double for the
/// same two ports, and the two used to share the name
/// DiscardingCommittedWorkPublisher while behaving oppositely - one kept every id,
/// this one kept none.
///
/// One method satisfies both ports: they declare the same signature.
/// </summary>
internal sealed class DiscardingCommittedWorkPublisher :
    ICommittedIntakeWorkPublisher,
    ICommittedExternalWorkPublisher
{
    public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
