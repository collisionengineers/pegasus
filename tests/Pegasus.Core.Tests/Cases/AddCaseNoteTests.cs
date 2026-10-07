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
    /// The Automation Actor holds casework rights like staff, and does
    /// anything a staff member can (operator, 7 October 2026): it notes the
    /// Case as staff do.
    /// </summary>
    [Fact]
    public async Task AnAutomationActorMayWriteANote()
    {
        var store = new RecordingStore();

        await Command(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.Automation("automation"), "note-4", "A note."),
            CancellationToken.None);

        Assert.Equal(ActorKind.Automation, store.Last?.Actor.Kind);
        Assert.Equal("A note.", store.Last?.Note);
    }

    /// <summary>
    /// The one kind admitted beside Staff (operator decision, 2026-08-28): the
    /// instructing Principal's own words about this job, written onto the case
    /// its instruction created.
    /// </summary>
    [Fact]
    public async Task APrincipalMayWriteTheNoteItSubmittedWithItsInstruction()
    {
        var store = new RecordingStore();

        await Command(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.Principal(Guid.NewGuid()), "note-5", "Vehicle is at the repairer."),
            CancellationToken.None);

        Assert.Equal(ActorKind.Principal, store.Last?.Actor.Kind);
    }

    /// <summary>
    /// A right is still required: the widening admits Provider on its own
    /// permission, it does not stop asking for one.
    /// </summary>
    [Fact]
    public async Task AnActorWithNoCaseworkRightStillCannotWriteANote() =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            Command(new RecordingStore()).ExecuteAsync(
                new(Guid.NewGuid(), ActionActor.SystemWorker("note-6"), "note-6", "A note."),
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
