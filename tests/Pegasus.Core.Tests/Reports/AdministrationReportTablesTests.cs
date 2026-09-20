using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class AdministrationReportTablesTests
{
    private static readonly DateTimeOffset From = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildsOneSheetPerReportWithTypedColumnsAndTotalsWhereTheyMakeSense()
    {
        var engineer = new EngineerActivityReport(From, To,
        [
            new(Guid.NewGuid(), "alex", 4, 3, Disputes: 1, AmendmentRequests: 1, AuditReportsSent: 2, AverageReceivedToSent: TimeSpan.FromHours(30))
        ]);
        var principal = new PrincipalReportActivityReport(From, To,
        [
            new(Guid.NewGuid(), "QDOS", 2, 3, 2, 1, 0, 0, 0, 0,
                TimeSpan.FromHours(20), TimeSpan.FromHours(21), TimeSpan.FromHours(22), TimeSpan.FromHours(40),
                0, null, 1, From, 0,
                [new("AssessmentReport", 2, 0), new("FeeNote", 1, 0)],
                AgreedFeeTotal: 250m)
        ]);
        var monthly = new List<MonthlyReportActivity> { new(Guid.NewGuid(), "QDOS", 2026, 8, 2, 1, 2, 250m) };

        var sheets = AdministrationReportTables.Build(engineer, principal, monthly);

        Assert.Equal(["Engineer activity", "Reports by Principal", "Turnaround", "By month"], sheets.Select(sheet => sheet.Name));
        var engineerSheet = sheets[0];
        Assert.Equal(["Person", "Queries received", "Disputes", "Amendment requests", "Reports sent", "Audit reports sent", "Received to sent"],
            engineerSheet.Columns.Select(column => column.Title));
        Assert.Equal(WorkbookColumnKind.Duration, engineerSheet.Columns[^1].Kind);
        Assert.True(engineerSheet.Totals);
        Assert.Equal(["alex", 3, 1, 1, 4, 2, TimeSpan.FromHours(30)], engineerSheet.Rows.Single());

        var byPrincipal = sheets[1];
        Assert.Equal(WorkbookColumnKind.Money, byPrincipal.Columns[3].Kind);
        Assert.Equal(["QDOS", 3, 2, 250m, "AssessmentReport 2; FeeNote 1"], byPrincipal.Rows.Single());
        Assert.False(sheets[2].Totals); // Averages and dates do not sum.
        Assert.Equal(["Aug 2026", "QDOS", 2, 1, 2, 250m], sheets[3].Rows.Single());
        Assert.All(sheets, sheet => Assert.All(sheet.Rows, row => Assert.Equal(sheet.Columns.Count, row.Count)));
    }

    [Fact]
    public void AnUnavailablePrincipalReportLeavesOnlyTheSheetsThatHaveData()
    {
        var sheets = AdministrationReportTables.Build(new EngineerActivityReport(From, To, []), null, []);

        Assert.Equal(["Engineer activity", "By month"], sheets.Select(sheet => sheet.Name));
    }

    [Fact]
    public void TheCsvCarriesTheNewColumnsInOrder()
    {
        var csv = EngineerActivityReportCsv.ToCsv(
        [
            new(Guid.NewGuid(), "alex", 4, 3, 1, 1, 2, TimeSpan.FromHours(1))
        ]);

        Assert.StartsWith(
            "Recorded send actor,Queries received for assigned Engineer,Disputes,Amendment requests,Reports sent by recorded actor,Audit reports sent,Received to sent\r\nalex,3,1,1,4,2,01:00:00\r\n",
            csv,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMonthlyReportIsForAdministratorsOverAPeriodOfAYearAtMost()
    {
        var queries = new FakeMonthly();
        var report = new GetMonthlyReportActivity(queries);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            report.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), From, To, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            report.ExecuteAsync(administrator, From, From.AddDays(400), default));

        var rows = await report.ExecuteAsync(administrator, From, To, default);
        Assert.Equal(["Sep 2026 QDOS", "Aug 2026 EVA", "Aug 2026 QDOS"], rows.Select(row => $"{row.MonthLabel} {row.PrincipalCode}"));
    }

    private sealed class FakeMonthly : IMonthlyReportActivityQueries
    {
        public Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MonthlyReportActivity>>(
            [
                new(Guid.NewGuid(), "QDOS", 2026, 8, 1, 0, 1, 0),
                new(Guid.NewGuid(), "EVA", 2026, 8, 1, 0, 0, 0),
                new(Guid.NewGuid(), "QDOS", 2026, 9, 1, 0, 0, 0)
            ]);
    }
}
