using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class EngineerActivityReportTests
{
    private static readonly DateTimeOffset From = new(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2031, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReportResolvesNamesAndOrdersRowsByName()
    {
        var knownId = Guid.NewGuid();
        var goneId = Guid.NewGuid();
        var queries = new Counts([new(goneId, 4, 1), new(knownId, 2, 7)]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(knownId, "engineer.one"));

        var report = await useCase.ExecuteAsync(Administrator(), From, To, null, CancellationToken.None);

        Assert.Equal((From, To), (report.FromUtc, report.ToUtc));
        Assert.Equal((From, To, (Guid?)null), queries.Request);
        Assert.Collection(
            report.Rows,
            row => Assert.Equal(new EngineerActivityRow(knownId, "engineer.one", 2, 7), row),
            row => Assert.Equal(new EngineerActivityRow(goneId, ActorDisplayNames.UnknownStaff, 4, 1), row));
    }

    [Fact]
    public async Task ReportPassesTheEngineerFilterThrough()
    {
        var engineerId = Guid.NewGuid();
        var queries = new Counts([]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(engineerId, "engineer.one"));

        var report = await useCase.ExecuteAsync(Administrator(), From, To, engineerId, CancellationToken.None);

        Assert.Empty(report.Rows);
        Assert.Equal(engineerId, queries.Request!.Value.EngineerId);
    }

    [Fact]
    public async Task ReportIsAdministratorOnly()
    {
        var queries = new Counts([]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(Guid.NewGuid(), "x"));
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(engineer, From, To, null, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(ActionActor.Automation("connector"), From, To, null, CancellationToken.None));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task ReportRejectsAnEmptyOrOverlongPeriod()
    {
        var queries = new Counts([]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(Guid.NewGuid(), "x"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(Administrator(), To, From, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(Administrator(), From, From.AddDays(367), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(Administrator(), From, To, Guid.Empty, CancellationToken.None));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task ReportRefusesADuplicateOrNegativeRowFromTheAdapter()
    {
        var id = Guid.NewGuid();
        var duplicate = new GetEngineerActivityReport(
            new Counts([new(id, 1, 1), new(id, 2, 2)]),
            new Accounts(id, "engineer.one"));
        var negative = new GetEngineerActivityReport(
            new Counts([new(id, -1, 0)]),
            new Accounts(id, "engineer.one"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            duplicate.ExecuteAsync(Administrator(), From, To, null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            negative.ExecuteAsync(Administrator(), From, To, null, CancellationToken.None));
    }

    [Fact]
    public void CsvHasTheTableColumnsAndQuotesOnlyWhatNeedsIt()
    {
        var csv = EngineerActivityReportCsv.ToCsv(
        [
            new(Guid.NewGuid(), "engineer.one", 3, 5),
            new(Guid.NewGuid(), "Smith, \"J\"", 0, 1)
        ]);

        Assert.Equal(
            "Recorded send actor,Queries received for assigned Engineer,Reports sent by recorded actor\r\n"
            + "engineer.one,5,3\r\n"
            + "\"Smith, \"\"J\"\"\",1,0\r\n",
            csv);
        Assert.Equal("Recorded send actor,Queries received for assigned Engineer,Reports sent by recorded actor\r\n", EngineerActivityReportCsv.ToCsv([]));
    }

    [Fact]
    public void CsvMakesFormulaLookingNamesLiteral()
    {
        var csv = EngineerActivityReportCsv.ToCsv([new(Guid.NewGuid(), "=SUM(A1:A2)", 0, 0)]);
        Assert.Contains("'=SUM(A1:A2),0,0\r\n", csv, StringComparison.Ordinal);
    }

    private static ActionActor Administrator() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Counts(IReadOnlyList<EngineerActivityCounts> rows) : IEngineerActivityQueries
    {
        public (DateTimeOffset FromUtc, DateTimeOffset ToUtc, Guid? EngineerId)? Request { get; private set; }

        public Task<IReadOnlyList<EngineerActivityCounts>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? engineerId,
            CancellationToken cancellationToken)
        {
            Request = (fromUtc, toUtc, engineerId);
            return Task.FromResult(rows);
        }
    }

    private sealed class Accounts(Guid knownId, string userName) : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the report.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            Task.FromResult(staffId == knownId
                ? new StaffAccountSummary(staffId, userName, true, false, [StaffRole.Engineer])
                : null);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the report.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the report.");
    }
}
