using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

public sealed class AddTriageNoteTests
{
    private static readonly Guid TriageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ActionActor StaffActor() => ActionActor.Staff(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        [StaffRole.Engineer]);

    private static AddTriageNoteRequest Request(string note = "The repairer confirmed the vehicle is on site.") =>
        new(TriageId, 3, StaffActor(), "note-op-1", note);

    [Fact]
    public async Task ANoteIsAppendedThroughTheOneReplayProbedHistory()
    {
        var store = new NoteStore(Record(3));
        var record = await new AddTriageNote(store).ExecuteAsync(Request());

        Assert.Equal(1, store.Probes);
        Assert.Equal(1, store.Writes);
        Assert.Equal(TriageId, record.Id);
    }

    [Fact]
    public async Task ARetriedNoteReturnsTheCommittedEntryRatherThanWritingItTwice()
    {
        var store = new NoteStore(Record(3))
        {
            Replay = new(Record(4))
        };
        var record = await new AddTriageNote(store).ExecuteAsync(Request());

        Assert.Equal(4, record.Version);
        Assert.Equal(1, store.Probes);
        // The probe answered, so nothing was written and the current state was
        // never even read.
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, store.Reads);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankNoteIsRefusedBeforeAnythingIsRead(string note)
    {
        var store = new NoteStore(Record(3));

        await Assert.ThrowsAsync<ArgumentException>(
            () => new AddTriageNote(store).ExecuteAsync(Request(note)));
        Assert.Equal(0, store.Probes);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task ANoteOverTheBoundIsRefusedAndASettledTriageTakesNoMoreNotes()
    {
        var store = new NoteStore(Record(3));
        // Triage's own convention (TriageLifecycleRules.RequireText): missing
        // text is an ArgumentException, text past its bound is an
        // ArgumentOutOfRangeException. A note is bounded like every other
        // Triage text.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new AddTriageNote(store).ExecuteAsync(
                Request(new string('n', TriageNotes.MaximumLength + 1))));

        var settled = new NoteStore(Record(3, TriageState.Completed));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AddTriageNote(settled).ExecuteAsync(Request()));
        Assert.Equal(0, settled.Writes);
    }

    private static TriageRecord Record(long version, TriageState state = TriageState.Open) => new(
        TriageId,
        new(
            Guid.NewGuid(),
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "token"),
            new string('a', 64),
            Guid.NewGuid()),
        "AB12CDE",
        state,
        AssigneeId: null,
        LinkedCaseId: null,
        version,
        "T-00001");

    private sealed class NoteStore(TriageRecord current) : ITriageStore
    {
        public TriageOperationReplay? Replay { get; init; }

        public int Probes { get; private set; }

        public int Writes { get; private set; }

        public int Reads { get; private set; }

        public Task<TriageOperationReplay?> ProbeAddNoteReplayAsync(
            AddTriageNoteRequest request,
            CancellationToken cancellationToken)
        {
            Probes++;
            return Task.FromResult(Replay);
        }

        public Task<TriageRecord> AddNoteAsync(
            AddTriageNoteRequest request,
            CancellationToken cancellationToken)
        {
            Writes++;
            return Task.FromResult(current with { Version = current.Version + 1 });
        }

        public Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<TriageDetail?>(new(current, DateTimeOffset.UnixEpoch, [], [], [], []));
        }

        public Task<IReadOnlyList<TriageSummary>> ListAsync(
            TriageState? state,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageSummary?> GetByOriginReceiptAsync(
            Guid originReceiptId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<TriageSentEvidenceReference>> ListSentEvidenceReferencesAsync(
            Guid triageId,
            int maximumResults,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageOperationReplay?> ProbeCreateReplayAsync(
            CreateTriageFromIntakeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageOperationReplay?> ProbeRecordFindingReplayAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageOperationReplay?> ProbeSupersedeFindingReplayAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageOperationReplay?> ProbeStateChangeReplayAsync(
            TriageMutationRequest request,
            TriageState targetState,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageOperationReplay?> ProbeLinkResponseEvidenceReplayAsync(
            TriageResponseEvidenceLinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageOperationReplay?> ProbeUnlinkResponseEvidenceReplayAsync(
            TriageResponseEvidenceUnlinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageRecord> CreateAsync(
            CreateTriageFromIntakeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageRecord> AssignAsync(
            AssignTriageRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageRecord> UnassignAsync(
            TriageMutationRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageRecord> RecordFindingAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageRecord> SupersedeFindingAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LinkResponseEvidenceAsync(
            TriageResponseEvidenceLinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UnlinkResponseEvidenceAsync(
            TriageResponseEvidenceUnlinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TriageRecord> ChangeStateAsync(
            TriageMutationRequest request,
            TriageState targetState,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LinkCaseAsync(
            TriageCaseLinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UnlinkCaseAsync(
            TriageCaseLinkRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
