using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class IntakeLogTests
{
    [Theory]
    [InlineData(IntakeDecision.CaseCreated, false, false, false, IntakeLogOutcome.CaseCreated)]
    [InlineData(IntakeDecision.ImageIntakeRegistered, false, false, false, IntakeLogOutcome.VehicleImages)]
    [InlineData(IntakeDecision.NeedsSorting, false, false, false, IntakeLogOutcome.Unidentified)]
    [InlineData(IntakeDecision.NeedsSorting, true, false, false, IntakeLogOutcome.Triage)]
    [InlineData(IntakeDecision.NeedsSorting, false, true, false, IntakeLogOutcome.Closed)]
    [InlineData(IntakeDecision.Unsupported, false, false, false, IntakeLogOutcome.CouldNotBeRead)]
    [InlineData(IntakeDecision.OcrRequired, false, false, false, IntakeLogOutcome.CouldNotBeRead)]
    [InlineData(IntakeDecision.TechnicalFailure, false, false, false, IntakeLogOutcome.CouldNotBeRead)]
    [InlineData(IntakeDecision.TechnicalFailure, false, false, true, IntakeLogOutcome.ProcessingFailed)]
    [InlineData(IntakeDecision.Unsupported, false, true, false, IntakeLogOutcome.Closed)]
    [InlineData(IntakeDecision.BlockedIntake, false, false, false, IntakeLogOutcome.Closed)]
    public void TheOutcomeReadsInOperatorWords(
        IntakeDecision decision, bool triageOpened, bool unidentifiedClosed, bool processingFailed, IntakeLogOutcome expected) =>
        Assert.Equal(expected, IntakeLogPolicy.Outcome(decision, triageOpened, unidentifiedClosed, processingFailed));

    [Theory]
    [InlineData(IntakeDecision.CaseCreated, false, true, false, IntakeLogOutcome.AllocationFailed)]
    [InlineData(IntakeDecision.NeedsSorting, false, false, true, IntakeLogOutcome.OcrFailed)]
    [InlineData(IntakeDecision.OcrRequired, false, true, true, IntakeLogOutcome.AllocationFailed)]
    [InlineData(IntakeDecision.TechnicalFailure, true, true, true, IntakeLogOutcome.ProcessingFailed)]
    [InlineData(IntakeDecision.TechnicalFailure, false, false, true, IntakeLogOutcome.OcrFailed)]
    public void FailedAllocationAndFailedOcrAreDistinctOutcomesAfterProcessingFailure(
        IntakeDecision decision, bool processingFailed, bool allocationFailed, bool ocrFailed, IntakeLogOutcome expected) =>
        Assert.Equal(
            expected,
            IntakeLogPolicy.Outcome(decision, triageOpened: false, unidentifiedClosed: false, processingFailed, allocationFailed, ocrFailed));

    [Theory]
    [InlineData(IntakeLogOutcome.AllocationFailed, true)]
    [InlineData(IntakeLogOutcome.OcrFailed, true)]
    [InlineData(IntakeLogOutcome.ProcessingFailed, true)]
    [InlineData(IntakeLogOutcome.CouldNotBeRead, false)]
    [InlineData(IntakeLogOutcome.Unidentified, false)]
    [InlineData(IntakeLogOutcome.Closed, false)]
    [InlineData(IntakeLogOutcome.CaseCreated, false)]
    [InlineData(IntakeLogOutcome.Triage, false)]
    [InlineData(IntakeLogOutcome.VehicleImages, false)]
    public void FailedIntakeCountsEveryRetryableFailureOperationsLists(IntakeLogOutcome outcome, bool counted)
    {
        Assert.Equal(counted, IntakeLogPolicy.IsRetryableFailure(outcome));
        Assert.Equal(
            [IntakeLogOutcome.AllocationFailed, IntakeLogOutcome.OcrFailed, IntakeLogOutcome.ProcessingFailed],
            IntakeLogPolicy.RetryableFailures);
    }

    [Theory]
    [InlineData(IntakeOcrState.Failed, true)]
    [InlineData(IntakeOcrState.Pending, false)]
    [InlineData(IntakeOcrState.Processing, false)]
    [InlineData(IntakeOcrState.RetryScheduled, false)]
    [InlineData(IntakeOcrState.Unknown, false)]
    [InlineData(IntakeOcrState.Completed, false)]
    public void OcrCanBeRetriedOnlyWhenTheLastAttemptFailed(IntakeOcrState lastState, bool expected)
    {
        Assert.Equal(expected, IntakeOcrRetryPolicy.CanRetry(lastState));
        Assert.False(IntakeOcrRetryPolicy.CanRetry(null));
    }

    [Fact]
    public async Task RetryOcrRequiresStaffCaseworkAReasonAndAnOperationKey()
    {
        var store = new RecordingMutationStore();
        var sut = new RetryIntakeOcr(store, TimeProvider.System);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var receiptId = Guid.NewGuid();

        await sut.ExecuteAsync(new RetryIntakeOcrRequest(receiptId, 4, staff, "retry-ocr-1", "The provider was down."));
        var recorded = Assert.Single(store.OcrRetries);
        Assert.Equal(receiptId, recorded.ReceiptId);
        Assert.Equal(4, recorded.ExpectedVersion);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(new RetryIntakeOcrRequest(receiptId, 4, staff, "retry-ocr-2", " ")));
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(new RetryIntakeOcrRequest(Guid.Empty, 4, staff, "retry-ocr-3", "Reason")));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(new RetryIntakeOcrRequest(receiptId, 4, ActionActor.SystemWorker("intake-processing"), "retry-ocr-4", "Reason")));
        Assert.Single(store.OcrRetries);
    }

    private sealed class RecordingMutationStore : IIntakeMutationStore
    {
        public List<RetryIntakeOcrRequest> OcrRetries { get; } = [];

        public Task<IntakeReceipt> ScheduleOcrRetryAsync(RetryIntakeOcrRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
        {
            OcrRetries.Add(request);
            return Task.FromResult<IntakeReceipt>(null!);
        }

        public Task<IntakeReceipt> ResolveAsync(ResolveIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleReevaluationAsync(ReevaluateIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AttachSuppliedOriginalReportResult> AttachSuppliedOriginalReportAsync(AttachSuppliedOriginalReportRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AttachSuppliedOriginalReportResult?> ProbeSuppliedOriginalReportReplayAsync(ProbeSuppliedOriginalReportReplayRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LinkAsync(LinkIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReverseLinkAsync(ReverseIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AutoLinkAsync(AutomaticIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task TheIntakeLogIsForAdministratorsOnly()
    {
        var queries = new RecordingQueries();
        var sut = new ListIntakeLog(queries);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        await sut.ExecuteAsync(administrator, new IntakeLogFilter(Text: "  AB12 CDE ", PrincipalCode: " qdos "), 2, default);
        await sut.CountsAsync(administrator, default);

        var (filter, page) = Assert.Single(queries.Lists);
        Assert.Equal("AB12 CDE", filter.Text);
        Assert.Equal("QDOS", filter.PrincipalCode);
        Assert.Equal(2, page);
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), new IntakeLogFilter(), 1, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.GetAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), Guid.NewGuid(), default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(administrator, new IntakeLogFilter(FromUtc: DateTimeOffset.UnixEpoch.AddDays(1), ToUtc: DateTimeOffset.UnixEpoch), 1, default));
    }

    private sealed class RecordingQueries : IIntakeLogQueries
    {
        public List<(IntakeLogFilter Filter, int Page)> Lists { get; } = [];

        public Task<IntakeLogPage> ListAsync(IntakeLogFilter filter, int page, int pageSize, CancellationToken cancellationToken)
        {
            Lists.Add((filter, page));
            return Task.FromResult(new IntakeLogPage([], page, pageSize, 0));
        }

        public Task<IntakeLogCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeLogCounts(0, null));

        public Task<IntakeLogDetail?> GetAsync(Guid receiptId, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeLogDetail?>(null);

        public Task<IReadOnlyList<IntakeLogActionableFailure>> ListRetryableFailuresAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeLogActionableFailure>>([]);
    }
}
