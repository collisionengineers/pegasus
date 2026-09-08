using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Triage;

public sealed class TriageReplayTests
{
    [Theory]
    [InlineData(CaseLifecycleState.PostReport, false, false, true)]
    [InlineData(CaseLifecycleState.Review, true, false, false)]
    [InlineData(CaseLifecycleState.Review, false, true, false)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false, false, false)]
    public void AutomaticTriageLinkKeepsTheExistingTargetBoundary(
        CaseLifecycleState state, bool archived, bool leased, bool expected)
    {
        Assert.Equal(expected, TriageCasePairing.CanLinkTarget(state, archived,
            leased ? ActorKind.Staff : null, leased ? DateTimeOffset.UnixEpoch.AddMinutes(1) : null,
            DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task CreationReplayAttemptsPairingAndStillReturnsItsHistoricalCreationResult()
    {
        var created = CreateRecord(TriageState.Open, 0);
        var candidate = new TriageCaseLinkCandidate(created.Id, 0, Guid.NewGuid(), 0, "fixture-match", 1);
        var store = new ReplayStore { CreationResult = created, PairingCandidates = [candidate], FailFirstPairingWrite = true };
        var command = new CreateTriageFromIntake(store, new TriageCasePairing(store));
        var evidence = new IntakeEvidence(IntakeEvidenceSource.SystemDefault, IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch, created.NormalizedVehicleRegistration,
            "Accepted creation replay fixture.", "fixture-match", 1);
        var request = new CreateTriageFromIntakeRequest(created.Origin, created.NormalizedVehicleRegistration,
            evidence, ActionActor.SystemWorker("creation-replay"), "creation-replay");
        Assert.Equal(created, await command.ExecuteAsync(request, CancellationToken.None));
        Assert.Single(store.PairingCandidates);
        Assert.Equal(created, await command.ExecuteAsync(request, CancellationToken.None));
        Assert.Empty(store.PairingCandidates);
        Assert.Equal(2, store.PairingActors.Count);
    }

    [Fact]
    public async Task AutomaticPairingSurfacesAFailedWriteAndRetriesWithoutSkippingOtherWork()
    {
        var first = new TriageCaseLinkCandidate(Guid.NewGuid(), 0, Guid.NewGuid(), 0, "fixture-match", 1);
        var second = new TriageCaseLinkCandidate(Guid.NewGuid(), 0, Guid.NewGuid(), 0, "fixture-match", 1);
        var store = new ReplayStore { PairingCandidates = [first, second], FailFirstPairingWrite = true };
        var pairing = new TriageCasePairing(store);
        Assert.Equal(new TriageCasePairingResult(2, 1, 1, nameof(InvalidOperationException)),
            await pairing.ReconcileAsync(2, CancellationToken.None));
        Assert.Equal(new TriageCasePairingResult(1, 1, 0),
            await pairing.PairTriageAsync(first.TriageId, CancellationToken.None));
        Assert.Equal(new TriageCasePairingResult(0, 0, 0),
            await pairing.PairAcceptedCaseAsync(first.CaseId, CancellationToken.None));
        Assert.All(store.PairingActors, actor =>
        {
            Assert.Equal(ActorKind.SystemWorker, actor.Kind);
            Assert.Equal(TriageCasePairing.ActorId, actor.SubjectId);
        });
    }

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
        public TriageRecord? CreationResult { get; init; }
        public List<TriageCaseLinkCandidate> PairingCandidates { get; init; } = [];
        public List<ActionActor> PairingActors { get; } = [];
        public bool FailFirstPairingWrite { get; set; }
        public Task<IReadOnlyList<TriageCaseLinkCandidate>> ListAutomaticLinkCandidatesAsync(
            Guid? triageId, Guid? caseId, int maximumItems, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TriageCaseLinkCandidate>>(PairingCandidates
                .Where(item => (triageId is null || item.TriageId == triageId)
                    && (caseId is null || item.CaseId == caseId)).Take(maximumItems).ToArray());
        public Task<bool> LinkAutomaticallyAsync(
            TriageCaseLinkCandidate candidate, ActionActor actor, CancellationToken cancellationToken)
        {
            PairingActors.Add(actor);
            if (FailFirstPairingWrite)
            {
                FailFirstPairingWrite = false;
                throw new InvalidOperationException("Injected link transaction failure.");
            }
            return Task.FromResult(PairingCandidates.Remove(candidate));
        }

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
            CancellationToken cancellationToken) => CreationResult is null
                ? UnexpectedMutation<TriageRecord>() : Task.FromResult(CreationResult);

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
