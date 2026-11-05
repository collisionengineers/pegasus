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
            new(qdos, "QDOS", 2, 0, TimeSpan.FromDays(3), 0, null, 0, null, 0),
            // Only re-sends in the period: no report first sent, so no time to send.
            new(beta, "BETA", 0, 2, null, 0, null, 0, null, 0)
        ]);

        var result = await new GetV1ActivityReport(queries)
            .ExecuteAsync(Administrator(), From, To, CancellationToken.None);

        Assert.Equal((From, To), (result.FromUtc, result.ToUtc));
        Assert.Equal((From, To), queries.Request);
        Assert.Equal(["BETA", "QDOS"], result.Rows.Select(x => x.PrincipalCode));
        Assert.Equal((0, 2), (result.Rows[0].Sent, result.Rows[0].Resends));
        Assert.Null(result.Rows[0].AverageReceivedToSent);
        Assert.Equal((2, 0), (result.Rows[1].Sent, result.Rows[1].Resends));
        Assert.Equal(TimeSpan.FromDays(3), result.Rows[1].AverageReceivedToSent);
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
        var valid = new PrincipalReportActivity(id, "QDOS", 2, 1, TimeSpan.FromDays(1), 1, From, 2, From, 1, 10m);
        IReadOnlyList<IReadOnlyList<PrincipalReportActivity>> invalidRows =
        [
            [valid with { PrincipalId = Guid.Empty }],
            [valid with { PrincipalCode = " " }],
            [valid with { Sent = -1 }],
            [valid with { Resends = -1 }],
            [valid with { AverageReceivedToSent = TimeSpan.FromHours(-1) }],
            [valid with { CurrentTriage = -1 }],
            [valid with { CurrentHeldCases = -1 }],
            [valid with { HeldWithoutRecordedHoldEvent = -1 }],
            [valid with { HeldWithoutRecordedHoldEvent = 3 }],
            [valid with { AgreedFeeTotal = -1m }],
            [valid, valid]
        ];

        foreach (var rows in invalidRows)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GetV1ActivityReport(new Queries(rows))
                .ExecuteAsync(Administrator(), From, To, CancellationToken.None));
        }

        var accepted = await new GetV1ActivityReport(new Queries([valid]))
            .ExecuteAsync(Administrator(), From, To, CancellationToken.None);
        Assert.Equal(valid, Assert.Single(accepted.Rows));
    }

    [Fact]
    public async Task ReportRejectsAnAuditShareOutsideItsTotal()
    {
        var id = Guid.NewGuid();
        const int oneReport = 1;
        const int oneResend = 1;
        IReadOnlyList<PrincipalReportActivity> invalidRows =
        [
            new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, AuditSent: 2),
            new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, AuditSent: -1),
            new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, AuditResends: 2),
            new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, AuditResends: -1),
            new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, AuditAgreedFeeTotal: 11m),
            new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, AuditAgreedFeeTotal: -1m)
        ];

        foreach (var row in invalidRows)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GetV1ActivityReport(new Queries([row]))
                .ExecuteAsync(Administrator(), From, To, CancellationToken.None));
        }

        var valid = await new GetV1ActivityReport(new Queries(
            [new(id, "QDOS", oneReport, oneResend, null, 0, null, 0, null, 0, 10m, 1, 1, 10m)]))
            .ExecuteAsync(Administrator(), From, To, CancellationToken.None);
        var only = Assert.Single(valid.Rows);
        Assert.Equal((0, 0, 0m), (only.InspectionSent, only.InspectionResends, only.InspectionAgreedFeeTotal));
    }

    /// <summary>MI-02: each Inspection figure is its total less the Audit share.</summary>
    [Fact]
    public void EachInspectionFigureIsTheTotalLessTheAuditShare()
    {
        var row = new PrincipalReportActivity(
            Guid.NewGuid(), "QDOS", 5, 3, null, 0, null, 0, null, 0, 400m,
            AuditSent: 2, AuditResends: 1, AuditAgreedFeeTotal: 150m);

        Assert.Equal((3, 2, 250m), (row.InspectionSent, row.InspectionResends, row.InspectionAgreedFeeTotal));
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
