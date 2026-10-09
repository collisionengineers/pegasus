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
        var queries = new Counts([
            new(goneId, 4, 1),
            new(knownId, 2, 7, 4, 2, TimeSpan.FromHours(6))]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(knownId, "engineer.one"));

        var report = await useCase.ExecuteAsync(Administrator(), From, To, CancellationToken.None);

        Assert.Equal((From, To), (report.FromUtc, report.ToUtc));
        Assert.Equal((From, To), queries.Request);
        Assert.Collection(
            report.Rows,
            row => Assert.Equal(
                new EngineerActivityRow(knownId, "engineer.one", 2, 7, 4, 2, TimeSpan.FromHours(6)),
                row),
            row => Assert.Equal(new EngineerActivityRow(goneId, ActorDisplayNames.FormerStaff, 4, 1), row));
    }

    /// <summary>
    /// Item J: Automation's sends are a row of their own, named as every
    /// surface names Automation, so the report's Reports sent is every send.
    /// Person narrows to one staff member and the Automation row leaves.
    /// </summary>
    [Fact]
    public async Task AutomationIsItsOwnRowAndPersonNarrowsToOneStaffMember()
    {
        var engineerId = Guid.NewGuid();
        var useCase = new GetEngineerActivityReport(
            new Counts([
                new(engineerId, 5, 2),
                new(Guid.Empty, 3, 0, AuditReportsSent: 1) { SenderKind = ActorKind.Automation }]),
            new Accounts(engineerId, "engineer.one"));

        var report = await useCase.ExecuteAsync(Administrator(), From, To, CancellationToken.None);

        Assert.Equal(8, report.Rows.Sum(row => row.ReportsSent));
        var automation = Assert.Single(report.Rows, row => row.SenderKind == ActorKind.Automation);
        Assert.Equal(ActorDisplayNames.Automation, automation.DisplayName);
        Assert.Equal([engineerId], report.People.Select(row => row.EngineerId));
        Assert.Equal([engineerId], report.For(engineerId).Rows.Select(row => row.EngineerId));
        Assert.Same(report, report.For(null));
        Assert.Empty(report.For(Guid.NewGuid()).Rows);
    }

    [Fact]
    public async Task ReportIsAdministratorOnly()
    {
        var queries = new Counts([]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(Guid.NewGuid(), "x"));
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(engineer, From, To, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(ActionActor.Automation("connector"), From, To, CancellationToken.None));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task ReportRejectsAnEmptyOrOverlongPeriod()
    {
        var queries = new Counts([]);
        var useCase = new GetEngineerActivityReport(queries, new Accounts(Guid.NewGuid(), "x"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(Administrator(), To, From, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(Administrator(), From, From.AddDays(367), CancellationToken.None));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task ReportRefusesDuplicateNegativeOrContradictoryRowsFromTheAdapter()
    {
        var id = Guid.NewGuid();
        IReadOnlyList<IReadOnlyList<EngineerActivityCounts>> invalid =
        [
            [new(id, 1, 1), new(id, 2, 2)],
            [new(id, -1, 0)],
            [new(id, 1, 1, AmendmentRequests: -1)],
            [new(id, 1, 1, AmendmentRequests: 2)],
            [new(id, 1, 1, AuditReportsSent: 2)],
            [new(id, 1, 1, AverageReceivedToSent: TimeSpan.FromHours(-1))],
            [new(Guid.Empty, 1, 0)],
            [new(id, 1, 0) { SenderKind = ActorKind.Automation }],
            [new(Guid.Empty, 1, 1) { SenderKind = ActorKind.Automation }],
            [new(Guid.Empty, 1, 0) { SenderKind = ActorKind.Automation }, new(Guid.Empty, 2, 0) { SenderKind = ActorKind.Automation }]
        ];

        foreach (var rows in invalid)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new GetEngineerActivityReport(new Counts(rows), new Accounts(id, "engineer.one"))
                    .ExecuteAsync(Administrator(), From, To, CancellationToken.None));
        }
    }

    private static ActionActor Administrator() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Counts(IReadOnlyList<EngineerActivityCounts> rows) : IEngineerActivityQueries
    {
        public (DateTimeOffset FromUtc, DateTimeOffset ToUtc)? Request { get; private set; }

        public Task<IReadOnlyList<EngineerActivityCounts>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Request = (fromUtc, toUtc);
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
                ? new StaffAccountSummary(staffId, userName, true, false, StaffRole.Engineer)
                : null);

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
            IReadOnlyCollection<Guid> staffIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccountSummary>>(
                staffIds.Contains(knownId)
                    ? [new(knownId, userName, true, false, StaffRole.Engineer)]
                    : []);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the report.");
    }
}
