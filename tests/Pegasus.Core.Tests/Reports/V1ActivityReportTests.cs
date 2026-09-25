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
    public async Task ReportRejectsAnAuditShareOutsideItsTotal()
    {
        var id = Guid.NewGuid();
        IReadOnlyList<PrincipalReportArtifactTypeActivity> oneReport = [new(nameof(CaseReportArtifactKind.AssessmentReport), 1, 0)];
        IReadOnlyList<PrincipalReportActivity> invalidRows =
        [
            new(id, "QDOS", 1, 1, 1, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, oneReport, 10m, AuditReportsProduced: 2),
            new(id, "QDOS", 1, 1, 1, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, oneReport, 10m, AuditSent: 2),
            new(id, "QDOS", 1, 1, 1, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, oneReport, 10m, AuditAgreedFeeTotal: 11m),
            new(id, "QDOS", 1, 1, 1, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, oneReport, 10m, AuditSent: -1)
        ];

        foreach (var row in invalidRows)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GetV1ActivityReport(new Queries([row]))
                .ExecuteAsync(Administrator(), From, To, CancellationToken.None));
        }

        var valid = await new GetV1ActivityReport(new Queries(
            [new(id, "QDOS", 1, 1, 1, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 0, null, 0, oneReport, 10m, 1, 1, 10m)]))
            .ExecuteAsync(Administrator(), From, To, CancellationToken.None);
        var only = Assert.Single(valid.Rows);
        Assert.Equal((0, 0, 0m), (only.InspectionReportsProduced, only.InspectionSent, only.InspectionAgreedFeeTotal));
    }

    [Fact]
    public void FirstReportFeeBelongsToOnlyOneHalfOfASplitMonth()
    {
        var first = new DateTimeOffset(2031, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var split = new DateTimeOffset(2031, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = new DateTimeOffset(2031, 10, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.True(FirstReportFeeAttribution.InPeriod(first, first, split));
        Assert.False(FirstReportFeeAttribution.InPeriod(first, split, monthEnd));
        Assert.False(FirstReportFeeAttribution.InPeriod(split, first, split));
        Assert.True(FirstReportFeeAttribution.InPeriod(split, split, monthEnd));
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
