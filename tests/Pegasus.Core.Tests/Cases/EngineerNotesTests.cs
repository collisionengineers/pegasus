using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

public sealed class EngineerNotesTests
{
    private static readonly Guid StaffId = Guid.Parse("72d56ae8-84d3-4a74-b93b-5900a27a71f7");
    private static readonly ActionActor Staff = ActionActor.Staff(StaffId, [StaffRole.User]);
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StaffNoteIsTrimmedAndItsMutationEnvelopeIsForwarded()
    {
        var store = new RecordingStore();
        var command = new AddEngineerNote(store, new FixedTimeProvider(RecordedAtUtc));
        var caseId = Guid.NewGuid();

        await command.ExecuteAsync(
            new(caseId, Staff, 17, "  operation-one  ", "  Check the sill.  ", "  lease-one  "),
            CancellationToken.None);

        Assert.Equal(caseId, store.Request!.CaseId);
        Assert.Same(Staff, store.Request.Actor);
        Assert.Equal(17, store.Request.ExpectedVersion);
        Assert.Equal("operation-one", store.Request.OperationKey);
        Assert.Equal("Check the sill.", store.Request.Note);
        Assert.Equal("lease-one", store.Request.EditLeaseToken);
        Assert.Equal(RecordedAtUtc, store.RecordedAtUtc);
    }

    [Theory]
    [MemberData(nameof(NonStaffActors))]
    public async Task NonStaffActorsCannotAddEngineerNotes(ActionActor actor) =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            Command().ExecuteAsync(
                new(Guid.NewGuid(), actor, 1, "operation", "Note", "lease"),
                CancellationToken.None));

    public static TheoryData<ActionActor> NonStaffActors => new()
    {
        ActionActor.Provider(Guid.NewGuid()),
        ActionActor.Automation("automation"),
        ActionActor.SystemWorker("worker"),
        ActionActor.RequestLink(Guid.NewGuid())
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankNotesAreRefused(string? note) =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Command().ExecuteAsync(
                new(Guid.NewGuid(), Staff, 1, "operation", note!, "lease"),
                CancellationToken.None));

    [Fact]
    public async Task OverlongNotesAreRefused() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Command().ExecuteAsync(
                new(
                    Guid.NewGuid(),
                    Staff,
                    1,
                    "operation",
                    new string('n', AddEngineerNote.MaximumLength + 1),
                    "lease"),
                CancellationToken.None));

    private static AddEngineerNote Command() =>
        new(new RecordingStore(), new FixedTimeProvider(RecordedAtUtc));

    private sealed class RecordingStore : IEngineerNoteStore
    {
        public AddEngineerNoteRequest? Request { get; private set; }
        public DateTimeOffset RecordedAtUtc { get; private set; }

        public Task AddAsync(
            AddEngineerNoteRequest request,
            DateTimeOffset recordedAtUtc,
            CancellationToken cancellationToken)
        {
            Request = request;
            RecordedAtUtc = recordedAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
