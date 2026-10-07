using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class RetainedMailDismissalTests
{
    private static readonly Guid MessageId = Guid.NewGuid();
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task AStaffMemberDismissesAndRestoresAMessage()
    {
        var store = new RecordingStore();

        var dismissed = await new DismissRetainedMail(store).ExecuteAsync(new(MessageId, Staff, " dismiss-1 "), default);
        var restored = await new RestoreRetainedMail(store).ExecuteAsync(new(MessageId, Staff, "restore-1"), default);

        Assert.True(dismissed!.IsDismissed);
        Assert.False(restored!.IsDismissed);
        Assert.Equal(["dismiss-1", "restore-1"], store.OperationKeys);
    }

    /// <summary>
    /// Dismiss and Restore are casework, so the Automation Actor performs them
    /// as staff do (ADR-0064); a system worker or Principal does not.
    /// </summary>
    [Fact]
    public async Task TheAutomationActorDismissesAndRestoresAMessage()
    {
        var store = new RecordingStore();
        var automation = ActionActor.Automation("client");

        var dismissed = await new DismissRetainedMail(store).ExecuteAsync(new(MessageId, automation, "mcp:dismiss-1"), default);
        var restored = await new RestoreRetainedMail(store).ExecuteAsync(new(MessageId, automation, "mcp:restore-1"), default);

        Assert.True(dismissed!.IsDismissed);
        Assert.Equal("client", dismissed.DismissedBy);
        Assert.False(restored!.IsDismissed);
        Assert.Equal(["mcp:dismiss-1", "mcp:restore-1"], store.OperationKeys);
    }

    [Fact]
    public async Task OnlyCaseworkActorsMayDismissAndTheEnvelopeIsChecked()
    {
        var store = new RecordingStore();
        var dismiss = new DismissRetainedMail(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => dismiss.ExecuteAsync(new(MessageId, ActionActor.SystemWorker("worker"), "dismiss-2"), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => dismiss.ExecuteAsync(new(MessageId, ActionActor.Principal(Guid.NewGuid()), "dismiss-2"), default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => dismiss.ExecuteAsync(new(Guid.Empty, Staff, "dismiss-3"), default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => dismiss.ExecuteAsync(new(MessageId, Staff, " "), default));
        Assert.Empty(store.OperationKeys);
    }

    [Fact]
    public void TheDismissedFolderIsInTheVocabulary()
    {
        // MailLogicalFolderPolicyTests covers that no settled classification
        // recommends it: Dismissed is only ever reached by the Dismiss act.
        Assert.Equal("Dismissed", MailLogicalFolders.Definition(MailLogicalFolderType.Dismissed).Label);
        Assert.Equal("dismissed", MailLogicalFolders.Definition(MailLogicalFolderType.Dismissed).Key);
    }

    private sealed class RecordingStore : IRetainedMailDismissalStore
    {
        private DateTimeOffset? _dismissedAt;

        public List<string> OperationKeys { get; } = [];

        public Task<RetainedMailDismissal?> DismissAsync(DismissRetainedMailRequest request, CancellationToken cancellationToken)
        {
            OperationKeys.Add(request.OperationKey);
            _dismissedAt = DateTimeOffset.UnixEpoch;
            return Task.FromResult<RetainedMailDismissal?>(new(request.MessageId, true, _dismissedAt, request.Actor.SubjectId, false));
        }

        public Task<RetainedMailDismissal?> RestoreAsync(RestoreRetainedMailRequest request, CancellationToken cancellationToken)
        {
            OperationKeys.Add(request.OperationKey);
            _dismissedAt = null;
            return Task.FromResult<RetainedMailDismissal?>(new(request.MessageId, false, null, null, false));
        }
    }
}
