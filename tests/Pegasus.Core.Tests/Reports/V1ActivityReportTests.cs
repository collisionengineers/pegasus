using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class V1ActivityReportTests
{
    private static readonly DateTimeOffset From = new(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2031, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReportPassesTheBoundedPeriodAndOrdersPrincipalRows()
    {
        var qdos = Guid.NewGuid();
        var beta = Guid.NewGuid();
        var queries = new Queries(
        [
            new(qdos, "QDOS", 1, 2, 1, 1, 0, 0, 0, 0, TimeSpan.FromDays(2), TimeSpan.FromDays(2), TimeSpan.FromDays(2), TimeSpan.FromDays(3), 0, null, 0, null, 0, []),
            new(beta, "BETA", 1, 1, 0, 1, 1, 1, 0, 0, null, null, null, null, 0, null, 0, null, 0, [])
        ]);

        var result = await new GetV1ActivityReport(queries)
            .ExecuteAsync(Administrator(), From, To, CancellationToken.None);

        Assert.Equal((From, To), (result.FromUtc, result.ToUtc));
        Assert.Equal((From, To), queries.Request);
        Assert.Equal(["BETA", "QDOS"], result.Rows.Select(x => x.PrincipalCode));
        Assert.Equal(1, result.Rows[0].MissingOriginForGeneratedTurnaround);
        Assert.Equal(1, result.Rows[0].Ready);
        Assert.Equal(1, result.Rows[0].MissingOriginForReadyTurnaround);
        Assert.Null(result.Rows[0].AverageReceivedToReady);
    }

    [Fact]
    public async Task ReportRequiresAnAdministratorAndAValidPeriod()
    {
        var queries = new Queries([]);
        var report = new GetV1ActivityReport(queries);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => report.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            From,
            To,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => report.ExecuteAsync(
            Administrator(),
            To,
            From,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => report.ExecuteAsync(
            Administrator(),
            From,
            From.AddDays(367),
            CancellationToken.None));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task ReportRejectsInvalidOrDuplicateAdapterRows()
    {
        var id = Guid.NewGuid();
        var invalid = new GetV1ActivityReport(new Queries(
            [new(id, "QDOS", 0, 0, 1, 0, 0, 0, 0, 2, null, null, null, null, 0, null, 0, null, 0, [])]));
        var duplicate = new GetV1ActivityReport(new Queries(
        [
            new(id, "QDOS", 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, []),
            new(id, "QDOS", 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, [])
        ]));

        await Assert.ThrowsAsync<InvalidDataException>(() => invalid.ExecuteAsync(
            Administrator(), From, To, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => duplicate.ExecuteAsync(
            Administrator(), From, To, CancellationToken.None));
    }

    [Fact]
    public void CsvUsesTheSharedFormulaSafeEscaping()
    {
        var csv = PrincipalReportActivityCsv.ToCsv(
            [new(Guid.NewGuid(), "=QDOS", 1, 1, 1, 1, 0, 0, 0, 0, TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(1), null, 0, null, 0, null, 0, [])]);

        Assert.StartsWith(PrincipalReportActivityCsv.Header + "\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("'=QDOS,1,1,0,1,1,0,0,0,0,01:00:00,02:00:00,01:00:00,,0,,0,,0,\r\n", csv, StringComparison.Ordinal);
    }

    private static ActionActor Administrator() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Queries(IReadOnlyList<PrincipalReportActivity> rows) : IV1ActivityReportQueries
    {
        public (DateTimeOffset From, DateTimeOffset To)? Request { get; private set; }

        public Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Request = (fromUtc, toUtc);
            return Task.FromResult(rows);
        }
    }
}
