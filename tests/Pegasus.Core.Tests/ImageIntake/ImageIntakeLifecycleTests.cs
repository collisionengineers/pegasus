using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class ImageIntakeLifecycleTests
{
    private static readonly Guid ReceiptId = Guid.NewGuid();
    private static readonly Guid EvaluationRevisionId = Guid.NewGuid();
    private static readonly string SourceHash = new('a', 64);

    private static ImageIntakeOrigin Origin() => new(
        ReceiptId,
        new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "receipt-token"),
        SourceHash,
        EvaluationRevisionId);

    private static ActionActor StaffActor() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    private static RegisterImageIntakeRequest Request(
        ImageIntakeOrigin? origin = null,
        string registration = "AB12CDE",
        ActionActor? actor = null,
        string operationKey = "op-key",
        string reason = "A usable registration was confirmed.") => new(
        origin ?? Origin(),
        registration,
        actor ?? StaffActor(),
        operationKey,
        reason);

    [Fact]
    public void ValidRegisterRequestPasses() =>
        ImageIntakeLifecycleRules.ValidateRegister(Request());

    [Fact]
    public void SystemWorkerActorMayRegister() =>
        ImageIntakeLifecycleRules.ValidateRegister(
            Request(actor: ActionActor.SystemWorker("image-intake-automation")));

    [Fact]
    public void RequestLinkActorCannotRegister() =>
        Assert.Throws<StaffAuthorizationException>(
            () => ImageIntakeLifecycleRules.ValidateRegister(
                Request(actor: ActionActor.RequestLink(Guid.NewGuid()))));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ab12cde")]
    [InlineData("AB12 CDE")]
    [InlineData("AB12-CDE")]
    [InlineData("AB12CDE!")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void InvalidRegistrationsAreRejected(string registration) =>
        Assert.ThrowsAny<ArgumentException>(
            () => ImageIntakeLifecycleRules.ValidateRegister(Request(registration: registration)));

    [Fact]
    public void ShortAndInteriorPadHashesAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(
                Request(origin: Origin() with { SourceHash = "abc" })));
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(
                Request(origin: Origin() with { SourceHash = new string('z', 64) })));
    }

    [Fact]
    public void EmptyOriginIdentifiersAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(
                Request(origin: Origin() with { ReceiptId = Guid.Empty })));
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(
                Request(origin: Origin() with { EvaluationRevisionId = Guid.Empty })));
    }

    [Fact]
    public void OperationKeyAndReasonBoundsAreEnforced()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(Request(operationKey: " ")));
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(
                Request(operationKey: new string('k', 101))));
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(Request(reason: " ")));
        Assert.ThrowsAny<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateRegister(Request(reason: new string('r', 501))));
    }

    [Fact]
    public void MergeRequiresFormalCaseAndPermitsSystemWorker()
    {
        var request = new MergeImageInitiatedCaseRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ActionActor.SystemWorker("image-intake-automation"),
            "merge-op",
            "The exact VRM match is unambiguous.",
            0);

        ImageIntakeLifecycleRules.ValidateMerge(request);
    }

    [Fact]
    public void StaffCloseRequiresCaseworkReason()
    {
        var request = new CloseImageInitiatedCaseRequest(
            Guid.NewGuid(),
            StaffActor(),
            "close-op",
            "Instructions will not arrive.",
            0);

        ImageIntakeLifecycleRules.ValidateClose(request);
        Assert.Throws<StaffAuthorizationException>(() => ImageIntakeLifecycleRules.ValidateClose(
            request with { Actor = ActionActor.RequestLink(Guid.NewGuid()) }));
    }

    [Fact]
    public void ImageIntakeRecordCarriesAnOptionalPrincipal()
    {
        var absent = new ImageIntakeRecord(Guid.NewGuid(), Origin(), "AB12CDE", "AB12CDE-01");
        var principalId = Guid.NewGuid();
        var recorded = absent with { PrincipalId = principalId };

        // No registration path supplies a principal: recording one is a later,
        // separate staff decision.
        Assert.Null(absent.PrincipalId);
        Assert.Equal(principalId, recorded.PrincipalId);
    }

    [Fact]
    public void PrincipalAssignmentRequiresStaffAndValidIdentifiers()
    {
        var request = new SetImageIntakePrincipalRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StaffActor(),
            0);

        ImageIntakeLifecycleRules.ValidateSetPrincipal(request);
        // Clearing is a legitimate state, not an error: `Not known` is a value
        // staff may return to.
        ImageIntakeLifecycleRules.ValidateSetPrincipal(request with { PrincipalId = null });

        Assert.Throws<StaffAuthorizationException>(() =>
            ImageIntakeLifecycleRules.ValidateSetPrincipal(
                request with { Actor = ActionActor.RequestLink(Guid.NewGuid()) }));
        Assert.Throws<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateSetPrincipal(
                request with { ImageIntakeId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() =>
            ImageIntakeLifecycleRules.ValidateSetPrincipal(
                request with { PrincipalId = Guid.Empty }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ImageIntakeLifecycleRules.ValidateSetPrincipal(
                request with { ExpectedVersion = -1 }));
    }

    public static TheoryData<CaseLifecycleState, bool, bool> EligibilityCases()
    {
        var data = new TheoryData<CaseLifecycleState, bool, bool>();
        foreach (var state in Enum.GetValues<CaseLifecycleState>())
        {
            var eligibleState = state is CaseLifecycleState.NotReady
                or CaseLifecycleState.Held
                or CaseLifecycleState.Review
                or CaseLifecycleState.ReportPreparation;
            data.Add(state, false, eligibleState);
            data.Add(state, true, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EligibilityCases))]
    public void EligibilityCoversEveryStateAndReportEvidence(
        CaseLifecycleState state,
        bool hasReportSentEvidence,
        bool expected) =>
        Assert.Equal(
            expected,
            ImageIntakeLifecycleRules.IsCaseEligibleForAssociation(state, hasReportSentEvidence));

    [Fact]
    public async Task RegisterReplaysCommittedResultWithoutRegistering()
    {
        var committed = new ImageIntakeRecord(Guid.NewGuid(), Origin(), "AB12CDE", "AB12CDE-01");
        var store = new FakeStore { Replay = new(committed) };

        var actual = await new RegisterImageIntake(store, new CommittedWorkPublisherDouble()).ExecuteAsync(
            Request(),
            CancellationToken.None);

        Assert.Equal(committed, actual);
        Assert.Equal(1, store.ProbeCount);
        Assert.Equal(0, store.RegisterCount);
    }

    [Fact]
    public async Task RegisterConflictSurfacesBeforeRegistering()
    {
        var store = new FakeStore
        {
            ProbeFailure = new ImageIntakeOperationConflictException(ReceiptId, "op-key")
        };

        await Assert.ThrowsAsync<ImageIntakeOperationConflictException>(
            () => new RegisterImageIntake(store, new CommittedWorkPublisherDouble()).ExecuteAsync(Request(), CancellationToken.None));
        Assert.Equal(0, store.RegisterCount);
    }

    [Fact]
    public async Task UnseenOperationRegisters()
    {
        var workItemId = Guid.NewGuid();
        var store = new FakeStore { PendingExternalWorkId = workItemId };
        var publisher = new CommittedWorkPublisherDouble();

        var actual = await new RegisterImageIntake(store, publisher).ExecuteAsync(
            Request(),
            CancellationToken.None);

        Assert.Equal(1, store.ProbeCount);
        Assert.Equal(1, store.RegisterCount);
        Assert.Equal("AB12CDE-01", actual.ImageIntakeReference);
        Assert.Equal([workItemId], publisher.ExternalWorkIds);
    }

    private sealed class FakeStore : IImageIntakeStore
    {
        public ImageIntakeOperationReplay? Replay { get; init; }

        public Exception? ProbeFailure { get; init; }

        public int ProbeCount { get; private set; }

        public int RegisterCount { get; private set; }

        public Guid? PendingExternalWorkId { get; init; }

        public Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken)
        {
            ProbeCount++;
            return ProbeFailure is null
                ? Task.FromResult(Replay)
                : Task.FromException<ImageIntakeOperationReplay?>(ProbeFailure);
        }

        public Task<ImageIntakeRecord> RegisterAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken)
        {
            RegisterCount++;
            return Task.FromResult(new ImageIntakeRecord(
                Guid.NewGuid(),
                request.Origin,
                request.NormalizedVehicleRegistration,
                ImageIntakeReferenceFormat.Create(request.NormalizedVehicleRegistration, 1),
                PendingExternalWorkId: PendingExternalWorkId));
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task EnsureRegisteredReceiptDecisionAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
