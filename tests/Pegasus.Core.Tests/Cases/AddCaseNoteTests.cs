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

    /// <summary>
    /// The rule this asserts is unchanged: the Automation Actor holds casework
    /// rights and records what it does under its own events, so it may not also
    /// author an operator note. TICK-058 widened the guard by exactly one kind
    /// (below) and left this one denied.
    /// </summary>
    [Fact]
    public async Task AnAutomationActorCannotWriteAnOperatorNote() =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            Command(new RecordingStore()).ExecuteAsync(
                new(Guid.NewGuid(), ActionActor.Automation("automation"), "note-4", "A note."),
                CancellationToken.None));

    /// <summary>
    /// The one kind admitted beside Staff (operator decision, 2026-08-28): the
    /// instructing Principal's own words about this job, written onto the case
    /// its instruction created.
    /// </summary>
    [Fact]
    public async Task AProviderMayWriteTheNoteItSubmittedWithItsInstruction()
    {
        var store = new RecordingStore();

        await Command(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.Provider(Guid.NewGuid()), "note-5", "Vehicle is at the repairer."),
            CancellationToken.None);

        Assert.Equal(ActorKind.Provider, store.Last?.Actor.Kind);
    }

    /// <summary>
    /// A right is still required: the widening admits Provider on its own
    /// permission, it does not stop asking for one.
    /// </summary>
    [Fact]
    public async Task AnActorWithNoCaseworkRightStillCannotWriteANote() =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            Command(new RecordingStore()).ExecuteAsync(
                new(Guid.NewGuid(), ActionActor.RequestLink(Guid.NewGuid()), "note-6", "A note."),
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
