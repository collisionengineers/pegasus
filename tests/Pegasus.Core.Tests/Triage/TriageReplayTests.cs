using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

public sealed class TriageReplayTests
{
    private static readonly Guid TriageId = Guid.NewGuid();
    private static readonly Guid SupersededFindingId = Guid.NewGuid();
    private static readonly ActionActor Actor =
        ActionActor.Automation("triage-replay-test");

    [Theory]
    [InlineData(ReplayCommand.RecordFinding)]
    [InlineData(ReplayCommand.SupersedeFinding)]
    [InlineData(ReplayCommand.AwaitInformation)]
    [InlineData(ReplayCommand.Complete)]
    [InlineData(ReplayCommand.Cancel)]
    [InlineData(ReplayCommand.Reopen)]
    public async Task ExactReplayReturnsCommittedSnapshotBeforeCurrentStateRules(
        ReplayCommand command)
    {
        var expected = CreateRecord(ResultState(command), version: 4);
        var store = new ReplayStore
        {
            Replay = new(expected)
        };

        var actual = await ExecuteAsync(command, store);

        Assert.Equal(expected, actual);
        Assert.Equal(command, store.ProbedCommand);
        Assert.Equal(1, store.ProbeCount);
        Assert.Equal(0, store.QueryCount);
        Assert.Equal(0, store.MutationCount);
    }

    [Theory]
    [InlineData(ReplayCommand.RecordFinding)]
    [InlineData(ReplayCommand.SupersedeFinding)]
    [InlineData(ReplayCommand.AwaitInformation)]
    [InlineData(ReplayCommand.Complete)]
    [InlineData(ReplayCommand.Cancel)]
    [InlineData(ReplayCommand.Reopen)]
    public async Task AlteredReplayConflictIsReturnedBeforeCurrentStateRules(
        ReplayCommand command)
    {
        var conflict = new TriageOperationConflictException(TriageId, OperationKey(command));
        var store = new ReplayStore
        {
            ProbeFailure = conflict
        };

        var actual = await Assert.ThrowsAsync<TriageOperationConflictException>(
            () => ExecuteAsync(command, store));

        Assert.Same(conflict, actual);
        Assert.Equal(command, store.ProbedCommand);
        Assert.Equal(1, store.ProbeCount);
        Assert.Equal(0, store.QueryCount);
        Assert.Equal(0, store.MutationCount);
    }

    [Theory]
    [InlineData(ReplayCommand.RecordFinding)]
    [InlineData(ReplayCommand.SupersedeFinding)]
    [InlineData(ReplayCommand.AwaitInformation)]
    [InlineData(ReplayCommand.Complete)]
    [InlineData(ReplayCommand.Cancel)]
    [InlineData(ReplayCommand.Reopen)]
    public async Task NewOperationStillFailsClosedAgainstCurrentState(
        ReplayCommand command)
    {
        var store = new ReplayStore
        {
            Current = CreateDetail(InvalidCurrentState(command))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(command, store));

        Assert.Equal(command, store.ProbedCommand);
        Assert.Equal(1, store.ProbeCount);
        Assert.Equal(1, store.QueryCount);
        Assert.Equal(0, store.MutationCount);
    }

    private static Task<TriageRecord> ExecuteAsync(
        ReplayCommand command,
        ITriageStore store) => command switch
    {
        ReplayCommand.RecordFinding => new RecordTriageFinding(store).ExecuteAsync(
            FindingRequest(command, superseding: false),
            CancellationToken.None),
        ReplayCommand.SupersedeFinding => new SupersedeTriageFinding(store).ExecuteAsync(
            FindingRequest(command, superseding: true),
            CancellationToken.None),
        ReplayCommand.AwaitInformation => new AwaitTriageInformation(store).ExecuteAsync(
            MutationRequest(command),
            CancellationToken.None),
        ReplayCommand.Complete => new CompleteTriage(store).ExecuteAsync(
            MutationRequest(command),
            CancellationToken.None),
        ReplayCommand.Cancel => new CancelTriage(store).ExecuteAsync(
            MutationRequest(command),
            CancellationToken.None),
        ReplayCommand.Reopen => new ReopenTriage(store).ExecuteAsync(
            MutationRequest(command),
            CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private static RecordTriageFindingRequest FindingRequest(
        ReplayCommand command,
        bool superseding) => new(
        TriageId,
        3,
        Actor,
        OperationKey(command),
        "Retained triage evidence",
        RoadworthinessFinding.Unroadworthy,
        AssessmentFinding.TotalLoss,
        superseding ? SupersededFindingId : null);

    private static TriageMutationRequest MutationRequest(ReplayCommand command) => new(
        TriageId,
        3,
        Actor,
        OperationKey(command),
        "Required lifecycle transition");

    private static string OperationKey(ReplayCommand command) =>
        $"triage-replay-{command}";

    private static TriageState ResultState(ReplayCommand command) => command switch
    {
        ReplayCommand.RecordFinding or ReplayCommand.SupersedeFinding =>
            TriageState.FindingRecorded,
        ReplayCommand.AwaitInformation => TriageState.AwaitingInformation,
        ReplayCommand.Complete => TriageState.Completed,
        ReplayCommand.Cancel => TriageState.Cancelled,
        ReplayCommand.Reopen => TriageState.Open,
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private static TriageState InvalidCurrentState(ReplayCommand command) => command switch
    {
        ReplayCommand.RecordFinding => TriageState.Completed,
        ReplayCommand.SupersedeFinding => TriageState.Cancelled,
        ReplayCommand.AwaitInformation => TriageState.AwaitingInformation,
        ReplayCommand.Complete => TriageState.Open,
        ReplayCommand.Cancel => TriageState.Cancelled,
        ReplayCommand.Reopen => TriageState.Open,
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private static TriageDetail CreateDetail(TriageState state) => new(
        CreateRecord(state, version: 3),
        DateTimeOffset.UnixEpoch,
        [],
        [],
        [],
        []);

    private static TriageRecord CreateRecord(TriageState state, long version) => new(
        TriageId,
        new(
            Guid.NewGuid(),
            new(IntakeSourceChannel.ManualUpload, "triage-replay-receipt"),
            new string('b', 64),
            Guid.NewGuid()),
        "AB12CDE",
        state,
        null,
        null,
        version);

    public enum ReplayCommand
    {
        RecordFinding,
        SupersedeFinding,
        AwaitInformation,
        Complete,
        Cancel,
        Reopen
    }

    private sealed class ReplayStore : ITriageStore
    {
        public TriageOperationReplay? Replay { get; init; }

        public Exception? ProbeFailure { get; init; }

        public TriageDetail? Current { get; init; }

        public ReplayCommand? ProbedCommand { get; private set; }

        public int ProbeCount { get; private set; }

        public int QueryCount { get; private set; }

        public int MutationCount { get; private set; }

        public Task<TriageOperationReplay?> ProbeRecordFindingReplayAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) =>
            ProbeAsync(ReplayCommand.RecordFinding, cancellationToken);

        public Task<TriageOperationReplay?> ProbeSupersedeFindingReplayAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) =>
            ProbeAsync(ReplayCommand.SupersedeFinding, cancellationToken);

        public Task<TriageOperationReplay?> ProbeStateChangeReplayAsync(
            TriageMutationRequest request,
            TriageState targetState,
            CancellationToken cancellationToken) =>
            ProbeAsync(
                targetState switch
                {
                    TriageState.AwaitingInformation => ReplayCommand.AwaitInformation,
                    TriageState.Completed => ReplayCommand.Complete,
                    TriageState.Cancelled => ReplayCommand.Cancel,
                    TriageState.Open => ReplayCommand.Reopen,
                    _ => throw new ArgumentOutOfRangeException(nameof(targetState))
                },
                cancellationToken);

        public Task<TriageOperationReplay?> ProbeLinkResponseEvidenceReplayAsync(
            TriageResponseEvidenceLinkRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<TriageOperationReplay?>(null);

        public Task<TriageOperationReplay?> ProbeUnlinkResponseEvidenceReplayAsync(
            TriageResponseEvidenceUnlinkRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<TriageOperationReplay?>(null);

        public Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryCount++;
            return Task.FromResult(Current);
        }

        public Task<TriageSummary?> GetByOriginReceiptAsync(
            Guid originReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<TriageSummary>> ListAsync(
            TriageState? state,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TriageSummary>>([]);

        public Task<IReadOnlyList<TriageSentEvidenceReference>> ListSentEvidenceReferencesAsync(
            Guid triageId,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TriageSentEvidenceReference>>([]);

        public Task<TriageRecord> CreateAsync(
            CreateTriageFromIntakeRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation<TriageRecord>();

        public Task<TriageRecord> AssignAsync(
            AssignTriageRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation<TriageRecord>();

        public Task<TriageRecord> UnassignAsync(
            TriageMutationRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation<TriageRecord>();

        public Task<TriageRecord> RecordFindingAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation<TriageRecord>();

        public Task<TriageRecord> SupersedeFindingAsync(
            RecordTriageFindingRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation<TriageRecord>();

        public Task LinkResponseEvidenceAsync(
            TriageResponseEvidenceLinkRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation();

        public Task UnlinkResponseEvidenceAsync(
            TriageResponseEvidenceUnlinkRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation();

        public Task<TriageRecord> ChangeStateAsync(
            TriageMutationRequest request,
            TriageState targetState,
            CancellationToken cancellationToken) => UnexpectedMutation<TriageRecord>();

        public Task LinkCaseAsync(
            TriageCaseLinkRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation();

        public Task UnlinkCaseAsync(
            TriageCaseLinkRequest request,
            CancellationToken cancellationToken) => UnexpectedMutation();


        private Task<TriageOperationReplay?> ProbeAsync(
            ReplayCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProbeCount++;
            ProbedCommand = command;
            if (ProbeFailure is not null)
            {
                throw ProbeFailure;
            }

            return Task.FromResult(Replay);
        }

        private Task UnexpectedMutation()
        {
            MutationCount++;
            return Task.FromException(new NotSupportedException("Unexpected mutation."));
        }

        private Task<T> UnexpectedMutation<T>()
        {
            MutationCount++;
            return Task.FromException<T>(new NotSupportedException("Unexpected mutation."));
        }
    }
}
