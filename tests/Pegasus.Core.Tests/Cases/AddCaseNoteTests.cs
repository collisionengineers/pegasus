using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

public sealed class AddCaseNoteTests
{
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public async Task AStaffNoteIsRecordedAgainstTheCase()
    {
        var store = new RecordingStore();
        await Command(store).ExecuteAsync(
            new(Guid.NewGuid(), Staff, "note-1", "  Called the bodyshop; awaiting the estimate.  "),
            CancellationToken.None);

        Assert.Equal("Called the bodyshop; awaiting the estimate.", store.Last!.Note);
    }

    [Fact]
    public async Task AnEmptyNoteIsRefused() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Command(new RecordingStore()).ExecuteAsync(
                new(Guid.NewGuid(), Staff, "note-2", "   "),
                CancellationToken.None));

    [Fact]
    public async Task AnOverlongNoteIsRefused() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Command(new RecordingStore()).ExecuteAsync(
                new(Guid.NewGuid(), Staff, "note-3", new string('n', AddCaseNote.MaximumLength + 1)),
                CancellationToken.None));

    [Fact]
    public async Task AnAutomationActorCannotWriteAnOperatorNote() =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            Command(new RecordingStore()).ExecuteAsync(
                new(Guid.NewGuid(), ActionActor.Automation("automation"), "note-4", "A note."),
                CancellationToken.None));

    private static AddCaseNote Command(ICaseNoteStore store) => new(store, TimeProvider.System);

    private sealed class RecordingStore : ICaseNoteStore
    {
        internal AddCaseNoteRequest? Last { get; private set; }

        public Task AddAsync(
            AddCaseNoteRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            Last = request;
            return Task.CompletedTask;
        }
    }
}
