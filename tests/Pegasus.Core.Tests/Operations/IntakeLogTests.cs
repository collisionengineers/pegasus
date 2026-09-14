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
    }
}
