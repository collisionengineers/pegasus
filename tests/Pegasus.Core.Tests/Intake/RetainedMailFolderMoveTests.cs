using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class RetainedMailFolderMoveTests
{
    [Fact]
    public async Task StaffMoveNormalizesReasonAndOperationKeyWithoutAcceptingTransportIdentity()
    {
        var store = new RecordingStore();
        var useCase = new MoveRetainedMailFolder(store);
        var operationKey = Guid.NewGuid();

        var result = await useCase.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            new(Guid.NewGuid(), 2, "mail-logical-folder", 1, 3, operationKey.ToString("B"), "  confirmed  "));

        Assert.NotNull(result);
        Assert.Equal(operationKey.ToString("D"), store.Request!.OperationKey);
        Assert.Equal("confirmed", store.Request.Reason);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public async Task MoveRefusesMissingFreshnessBeforeCallingTheStore(
        int classificationVersion,
        int recommendationVersion,
        int mailboxVersion)
    {
        var store = new RecordingStore();
        var useCase = new MoveRetainedMailFolder(store);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            new(Guid.NewGuid(), classificationVersion, "policy", recommendationVersion,
                mailboxVersion, Guid.NewGuid().ToString("D"), "reason")));

        Assert.Null(store.Request);
    }

    [Fact]
    public async Task MoveRefusesInvalidOperationKeyAndReasonBeforeCallingTheStore()
    {
        var store = new RecordingStore();
        var useCase = new MoveRetainedMailFolder(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            actor, new(Guid.NewGuid(), 1, "policy", 1, 1, "not-a-guid", "reason")));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            actor, new(Guid.NewGuid(), 1, "policy", 1, 1, Guid.NewGuid().ToString("D"), " ")));

        Assert.Null(store.Request);
    }

    [Fact]
    public async Task AutomationCannotMoveMail()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new MoveRetainedMailFolder(store).ExecuteAsync(
                ActionActor.Automation("mail-agent"),
                new(Guid.NewGuid(), 1, "policy", 1, 1, Guid.NewGuid().ToString("D"), "reason")));

        Assert.Null(store.Request);
    }

    private sealed class RecordingStore : IRetainedMailFolderMoveStore
    {
        public MoveRetainedMailFolderRequest? Request { get; private set; }

        public Task<RetainedMailFolderMoveResult?> MoveAsync(
            ActionActor actor,
            MoveRetainedMailFolderRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult<RetainedMailFolderMoveResult?>(new(
                RetainedMailFolderMoveOutcome.Succeeded,
                MailLogicalFolderType.Instructions,
                request.Reason,
                DateTimeOffset.UtcNow));
        }

        public Task<RetainedMailFolderMoveResult?> GetLatestAsync(Guid messageId, CancellationToken cancellationToken) =>
            Task.FromResult<RetainedMailFolderMoveResult?>(null);
    }
}
