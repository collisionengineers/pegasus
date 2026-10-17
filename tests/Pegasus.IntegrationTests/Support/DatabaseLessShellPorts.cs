using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// The shell's bell on a database-less test host, as the migrated database
/// answers it: no migration seeds a notification, so nobody has one.
/// </summary>
internal sealed class EmptyStaffNotifications : IMyStaffNotifications
{
    public Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffNotification>>([]);

    public Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken) =>
        Task.FromResult<StaffNotification?>(null);

    public Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}

/// <summary>
/// The shell's What's new on a database-less test host, as the migrated
/// database answers it: no migration seeds a release note, so none is
/// published and there is nothing to acknowledge.
/// </summary>
internal sealed class NoReleaseNotes : IMyReleaseNotes
{
    public Task<ReleaseNote?> GetUnacknowledgedAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Task.FromResult<ReleaseNote?>(null);

    public Task AcknowledgeAsync(ActionActor actor, Guid noteId, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException(
            "A database-less test host has no release note to acknowledge."));

    public Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReleaseNote>>([]);
}
