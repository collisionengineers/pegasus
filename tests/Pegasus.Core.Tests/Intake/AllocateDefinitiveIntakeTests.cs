using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class AllocateDefinitiveIntakeTests
{
    [Fact]
    public async Task AutomaticAllocationUsesPersistedTypedCaseType()
    {
        var receipt = Receipt(CaseType.InspectionAndAudit, "QDOS");
        var store = new RecordingAllocationStore();
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(new ReceiptQueries(receipt), store, accept, TimeProvider.System);

        var result = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
        Assert.Equal(CaseType.InspectionAndAudit, Assert.Single(accept.Requests).CaseType);
        Assert.Equal(IntakeAllocationAttemptKind.Automatic, store.Current?.Kind);
    }

    [Fact]
    public async Task FailedAutomaticAttemptIsDurableAndIsNotRetriedInBackground()
    {
        var receipt = Receipt(CaseType.Inspection, "MISSING");
        var store = new RecordingAllocationStore();
        var accept = new RecordingAcceptance(new PrincipalUnavailableException("MISSING"));
        var sut = new AllocateIntake(new ReceiptQueries(receipt), store, accept, TimeProvider.System);

        var first = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        var replay = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, first?.State.Status);
        Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, first?.State.FailureKind);
        Assert.True(replay?.IsSuppressed);
        Assert.Single(accept.Requests);
    }

    [Fact]
    public async Task AutomationActorCannotInvokeStaffRetry()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            new RecordingAcceptance(),
            TimeProvider.System);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.RetryAsync(new(
            receipt.Id,
            receipt.Version,
            Guid.NewGuid(),
            ActionActor.Automation("test-automation"),
            "retry:test",
            "Controlled retry.")));
    }

    [Fact]
    public async Task SequenceExhaustionIsBlockedAndUnexpectedFailureIsSafe()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var sequenceSut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            new RecordingAcceptance(new CaseIdentitySequenceExhaustedException("QDOS", 2031)),
            TimeProvider.System);
        var unexpectedSut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            new RecordingAcceptance(new InvalidDataException("private failure detail")),
            TimeProvider.System);

        var sequence = await sequenceSut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        var unexpected = await unexpectedSut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationFailureKind.SequenceExhausted, sequence?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.Blocked, sequence?.State.RecoveryDisposition);
        Assert.Equal(IntakeAllocationFailureKind.Unexpected, unexpected?.State.FailureKind);
        Assert.Equal("The case could not be created. No reference was allocated.", unexpected?.State.SafeReason);
        Assert.DoesNotContain("private", unexpected?.State.SafeReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationIsRethrownAndDoesNotLeaveAnAttempt()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var store = new RecordingAllocationStore();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            store,
            new RecordingAcceptance(new OperationCanceledException()),
            TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid()));

        Assert.Null(store.Current);
    }

    private static IntakeReceipt Receipt(CaseType caseType, string principalCode) => new(
        Guid.NewGuid(),
        "retained-instruction.pdf",
        "application/pdf",
        100,
        new string('a', 64),
        new(IntakeSourceChannel.Mailbox, "source-token"),
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        IntakeDecision.CaseCreated,
        "Eligible for case allocation.",
        [],
        [],
        new(principalCode, null, null, "AB12CDE", null, null, null, null, null, null, null),
        [],
        null,
        null,
        false,
        "test-reader",
        "1",
        "test-policy",
        1,
        MailClassificationDecision: MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
            [],
            "Definitive instruction.",
            "test-classifier",
            1,
            caseType));

    private sealed class ReceiptQueries(IntakeReceipt receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(id == receipt.Id ? receipt : null);

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) => Task.FromResult<IntakeAssetRecord?>(null);
    }

    private sealed class RecordingAcceptance(Exception? failure = null) : IAcceptIntake
    {
        public List<AcceptIntakeRequest> Requests { get; } = [];

        public Task<CaseAcceptanceOutcome> ExecuteAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (failure is not null)
            {
                return Task.FromException<CaseAcceptanceOutcome>(failure);
            }

            return Task.FromResult(new CaseAcceptanceOutcome(
                new(Guid.NewGuid(), request.PrincipalCode, 2031, 1, "QDOS/2031/0001"),
                CaseInitialState.Review,
                CaseCustodyState.Pending,
                Guid.NewGuid(),
                false));
        }
    }

    private sealed class RecordingAllocationStore : IIntakeAllocationStore
    {
        public IntakeAllocationAttempt? Current { get; private set; }

        public Task<IntakeAllocationAttempt?> GetCurrentAsync(
            Guid receiptId,
            CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task<BeginIntakeAllocationResult> BeginAsync(
            BeginIntakeAllocationAttempt request,
            CancellationToken cancellationToken)
        {
            if (Current is not null && request.Kind == IntakeAllocationAttemptKind.Automatic)
            {
                return Task.FromResult(new BeginIntakeAllocationResult(Current, true, true));
            }

            Current = new(
                Guid.NewGuid(),
                request.Command.ReceiptId,
                request.Kind,
                IntakeAllocationAttemptStatus.Pending,
                request.Command,
                request.Actor,
                request.OperationKey,
                request.CommandHash,
                request.Reason,
                request.StartedAtUtc,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            return Task.FromResult(new BeginIntakeAllocationResult(Current, false, false));
        }

        public Task<IntakeAllocationAttempt> CompleteFailureAsync(
            Guid attemptId,
            IntakeAllocationFailureKind failureKind,
            IntakeAllocationRecoveryDisposition recoveryDisposition,
            string safeReason,
            DateTimeOffset completedAtUtc,
            Exception exception,
            CancellationToken cancellationToken)
        {
            Current = Current! with
            {
                Status = IntakeAllocationAttemptStatus.Failed,
                CompletedAtUtc = completedAtUtc,
                FailureKind = failureKind,
                RecoveryDisposition = recoveryDisposition,
                SafeReason = safeReason
            };
            return Task.FromResult(Current);
        }

        public Task CancelPendingAsync(Guid attemptId, CancellationToken cancellationToken)
        {
            Current = null;
            return Task.CompletedTask;
        }
    }
}
