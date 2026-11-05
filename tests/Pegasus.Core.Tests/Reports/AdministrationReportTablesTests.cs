using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class AdministrationReportTablesTests
{
    private static readonly DateTimeOffset From = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildsOneSheetPerReportWithThePageHeadingsAndEveryWorkSplit()
    {
        var engineer = new EngineerActivityReport(From, To,
        [
            new(Guid.NewGuid(), "alex", 5, 4, AuditReportsSent: 2, AverageReceivedToSent: TimeSpan.FromHours(30), Resends: 1)
        ]);
        var principal = new PrincipalReportActivityReport(From, To,
        [
            new(Guid.NewGuid(), "QDOS", 4, 3, TimeSpan.FromHours(40), 2, From.AddDays(-3), 1, From, 0,
                AgreedFeeTotal: 250m,
                AuditSent: 1,
                AuditResends: 1,
                AuditAgreedFeeTotal: 100m),
            // Held now, nothing in the period: Queues only.
            new(Guid.NewGuid(), "PCH", 0, 0, null, 0, null, 1, From, 0)
        ]);
        var monthly = new List<MonthlyReportActivity>
        {
            new(Guid.NewGuid(), "QDOS", 2026, 8, 4, 3, 250m, AuditSent: 1, AuditResends: 1, AuditAgreedFeeTotal: 100m)
        };
        var outcomes = new ReportOutcomesReport(From, To, [new(Guid.NewGuid(), "QDOS", 1, 1, 0, 0, 1, 0)]);

        var sheets = AdministrationReportTables.Build(engineer, principal, monthly, outcomes, new Words());

        Assert.Equal(["Engineer activity", "Reports by Principal", "By month", "Outcomes", "Turnaround", "Queues"], sheets.Select(sheet => sheet.Name));
        var engineerSheet = sheets[0];
        Assert.Equal(
            ["Person", "Queries received", "Reports sent", "Reports sent · Inspection", "Reports sent · Audit", "Re-sends", "Received to sent"],
            engineerSheet.Columns.Select(column => column.Title));
        Assert.True(engineerSheet.Totals);
        // Item K: a turnaround reads as the page writes it.
        Assert.Equal(["alex", 4, 5, 3, 2, 1, "30h"], engineerSheet.Rows.Single());

        // MI-02: each total sits beside its Inspection and Audit split; a
        // re-send is counted on its own, never as another report sent (R673).
        var byPrincipal = sheets[1];
        Assert.Equal(
            [
                "Principal",
                "Reports sent", "Reports sent · Inspection", "Reports sent · Audit",
                "Re-sends", "Re-sends · Inspection", "Re-sends · Audit",
                "Agreed fees", "Agreed fees · Inspection", "Agreed fees · Audit"
            ],
            byPrincipal.Columns.Select(column => column.Title));
        Assert.All(byPrincipal.Columns.Skip(1).Take(6), column => Assert.Equal(WorkbookColumnKind.Count, column.Kind));
        Assert.All(byPrincipal.Columns.Skip(7).Take(3), column => Assert.Equal(WorkbookColumnKind.Money, column.Kind));
        Assert.Equal(["QDOS", 4, 3, 1, 3, 2, 1, 250m, 150m, 100m], byPrincipal.Rows.Single());

        // By month splits each figure by work too, so the page's Work choice has every figure.
        Assert.Equal(
            [
                "Month", "Principal",
                "Reports sent", "Reports sent · Inspection", "Reports sent · Audit",
                "Re-sends", "Re-sends · Inspection", "Re-sends · Audit",
                "Agreed fees", "Agreed fees · Inspection", "Agreed fees · Audit"
            ],
            sheets[2].Columns.Select(column => column.Title));
        Assert.Equal(["Aug 2026", "QDOS", 4, 3, 1, 3, 2, 1, 250m, 150m, 100m], sheets[2].Rows.Single());

        Assert.Equal(["Principal", "Repairable", "Total loss", "Cash in lieu", "Contract repair", "Agrees", "Differs"],
            sheets[3].Columns.Select(column => column.Title));
        Assert.Equal(["QDOS", 1, 1, 0, 0, 1, 0], sheets[3].Rows.Single());

        // Turnaround keeps the period's time to send; the held figures are Queues', now.
        Assert.Equal(["Principal", "Time to send"], sheets[4].Columns.Select(column => column.Title));
        Assert.Equal(["QDOS", "40h"], sheets[4].Rows.Single());
        Assert.False(sheets[4].Totals);
        Assert.Equal(["Principal", "Currently held", "Oldest held since", "Triages", "Oldest Triage since"], sheets[5].Columns.Select(column => column.Title));
        Assert.Equal(["QDOS", "PCH"], sheets[5].Rows.Select(row => row[0]));
        Assert.Equal(["QDOS", 1, From, 2, From.AddDays(-3)], sheets[5].Rows[0]);
        Assert.All(sheets, sheet => Assert.All(sheet.Rows, row => Assert.Equal(sheet.Columns.Count, row.Count)));
    }

    [Fact]
    public void ReportsByPrincipalListsAPrincipalWithAReportSentOrAReSend()
    {
        var report = new PrincipalReportActivityReport(From, To,
        [
            new(Guid.NewGuid(), "QDOS", 1, 0, TimeSpan.FromHours(5), 0, null, 0, null, 0, AgreedFeeTotal: 90m),
            // Only a re-send in the period: listed, with no time to send.
            new(Guid.NewGuid(), "LV", 0, 1, null, 0, null, 0, null, 0, AuditResends: 1),
            // Held now, nothing sent: Queues only.
            new(Guid.NewGuid(), "PCH", 0, 0, null, 0, null, 1, From, 0)
        ]);

        var byPrincipal = AdministrationReportTables.ReportsByPrincipal(report);

        Assert.Equal(["QDOS", "LV"], AdministrationReportTables.PrincipalRows(report).Select(row => row.PrincipalCode));
        Assert.Equal(["QDOS", "LV"], byPrincipal.Rows.Select(row => row[0]));
        Assert.Equal(["LV", 0, 0, 0, 1, 0, 1, 0m, 0m, 0m], byPrincipal.Rows[1]);
        Assert.Equal(["QDOS"], AdministrationReportTables.TurnaroundRows(report).Select(row => row.PrincipalCode));
        Assert.Equal(["PCH"], AdministrationReportTables.QueueRows(report).Select(row => row.PrincipalCode));
    }

    [Fact]
    public void AnUnavailablePrincipalReportCannotBuildAWorkbook()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AdministrationReportTables.Build(new EngineerActivityReport(From, To, []), null!, [], new ReportOutcomesReport(From, To, []), new Words()));
    }

    [Fact]
    public void AReportsCsvIsItsSheetUnderThePageHeadings()
    {
        var csv = WorkbookSheetCsv.Write(AdministrationReportTables.EngineerActivity(
            new EngineerActivityReport(From, To,
            [
                new(Guid.NewGuid(), "alex", 4, 3, 1, TimeSpan.FromHours(1), 2),
                new(Guid.NewGuid(), "Smith, \"J\"", 0, 1),
                new(Guid.NewGuid(), "=SUM(A1:A2)", 0, 0)
            ]),
            new Words()));

        Assert.Equal(
            "Person,Queries received,Reports sent,Reports sent · Inspection,Reports sent · Audit,Re-sends,Received to sent\r\n"
            + "alex,3,4,3,1,2,1h\r\n"
            + "\"Smith, \"\"J\"\"\",1,0,0,0,0,\r\n"
            + "'=SUM(A1:A2),0,0,0,0,0,\r\n",
            csv);
    }

    private sealed class Words : IAdministrationReportLabels
    {
        public string Turnaround(TimeSpan value) => $"{value.TotalHours:0}h";
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

    [Fact]
    public async Task TheMonthlyReportRejectsInvalidAdapterRows()
    {
        var id = Guid.NewGuid();
        IReadOnlyList<IReadOnlyList<MonthlyReportActivity>> invalidRows =
        [
            [new(id, "QDOS", 2026, 13, 0, 0, 0m)],
            [new(id, " ", 2026, 8, 0, 0, 0m)],
            [new(Guid.Empty, "QDOS", 2026, 8, 0, 0, 0m)],
            [new(id, "QDOS", 2026, 8, -1, 0, 0m)],
            [new(id, "QDOS", 2026, 8, 0, -1, 0m)],
            [new(id, "QDOS", 2026, 8, 0, 0, -1m)],
            [new(id, "QDOS", 2026, 8, 0, 1, 0m), new(id, "QDOS", 2026, 8, 1, 0, 0m)],
            // MI-02's Audit share is part of each total.
            [new(id, "QDOS", 2026, 8, 1, 1, 10m, AuditSent: 2)],
            [new(id, "QDOS", 2026, 8, 1, 1, 10m, AuditSent: -1)],
            [new(id, "QDOS", 2026, 8, 1, 1, 10m, AuditResends: 2)],
            [new(id, "QDOS", 2026, 8, 1, 1, 10m, AuditResends: -1)],
            [new(id, "QDOS", 2026, 8, 1, 1, 10m, AuditAgreedFeeTotal: 11m)]
        ];
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        foreach (var rows in invalidRows)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new GetMonthlyReportActivity(new FakeMonthly(rows))
                    .ExecuteAsync(administrator, From, To, default));
        }
    }

    private sealed class FakeMonthly(IReadOnlyList<MonthlyReportActivity>? rows = null) : IMonthlyReportActivityQueries
    {
        public Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult(rows ??
            [
                new(Guid.NewGuid(), "QDOS", 2026, 8, 1, 0, 0m),
                new(Guid.NewGuid(), "EVA", 2026, 8, 0, 1, 0m),
                new(Guid.NewGuid(), "QDOS", 2026, 9, 1, 0, 0m)
            ]);
    }
}
