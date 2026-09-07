using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Email;

public sealed class UnavailableStaffMailSend : IStaffMailSend
{
    private const string UnavailableMessage =
        "Staff mail delivery is unavailable in the DevelopmentOffline runtime profile.";

    public Task<StaffMailOperation> SendAsync(
        StaffMailSendCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<StaffMailOperation>(Unavailable());
    }

    public Task<StaffMailOperation?> GetAsync(
        ActionActor actor,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<StaffMailOperation?>(Unavailable());
    }

    public Task<StaffMailOperation?> GetLatestForOriginalAsync(
        ActionActor actor,
        Guid retainedMessageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<StaffMailOperation?>(null);
    }

    public Task<StaffMailOperation> ReconcileAsync(
        ActionActor actor,
        Guid operationId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<StaffMailOperation>(Unavailable());
    }

    public Task<StaffMailOperation> CancelAsync(
        ActionActor actor,
        Guid operationId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<StaffMailOperation>(Unavailable());
    }

    private static InvalidOperationException Unavailable() => new(UnavailableMessage);
}
