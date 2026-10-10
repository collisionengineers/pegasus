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
            new(Guid.NewGuid(), "alex", 4, 3, AmendmentRequests: 1, AuditReportsSent: 2, AverageReceivedToSent: TimeSpan.FromHours(30))
        ]);
        var principal = new PrincipalReportActivityReport(From, To,
        [
            new(Guid.NewGuid(), "QDOS", 2, 3, 2, 1, 0, 0, 0, 0,
                TimeSpan.FromHours(20), TimeSpan.FromHours(21), TimeSpan.FromHours(22), TimeSpan.FromHours(40),
                2, From.AddDays(-3), 1, From, 0,
                ReportsProduced: 2,
                AgreedFeeTotal: 250m,
                AuditReportsProduced: 1,
                AuditSent: 1,
                AuditAgreedFeeTotal: 100m),
            // Held now, nothing in the period: Queues only.
            new(Guid.NewGuid(), "PCH", 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, 0, null, 1, From, 0, ReportsProduced: 0)
        ]);
        var monthly = new List<MonthlyReportActivity>
        {
            new(Guid.NewGuid(), "QDOS", 2026, 8, 2, 2, 2, 250m, AuditReportsGenerated: 1, AuditSent: 1, AuditAgreedFeeTotal: 100m, AuditFeeNotesGenerated: 1)
        };
        var outcomes = new ReportOutcomesReport(From, To, [new(Guid.NewGuid(), "QDOS", 1, 1, 0, 0, 1, 0)]);

        var sheets = AdministrationReportTables.Build(engineer, principal, monthly, outcomes, new Words());

        Assert.Equal(["Engineer activity", "Reports by Principal", "By month", "Outcomes", "Turnaround", "Queues"], sheets.Select(sheet => sheet.Name));
        var engineerSheet = sheets[0];
        Assert.Equal(["Person", "Queries received", "Amendment requests", "Reports sent", "Audit reports sent", "Received to sent"],
            engineerSheet.Columns.Select(column => column.Title));
        Assert.True(engineerSheet.Totals);
        // Item K: a turnaround reads as the page writes it.
        Assert.Equal(["alex", 3, 1, 4, 2, "30h"], engineerSheet.Rows.Single());

        // MI-02: each total sits beside its Inspection and Audit split.
        var byPrincipal = sheets[1];
        Assert.Equal(
            [
                "Principal",
                "Reports produced", "Reports produced · Inspection", "Reports produced · Audit",
                "Reports sent", "Reports sent · Inspection", "Reports sent · Audit",
                "Agreed fees", "Agreed fees · Inspection", "Agreed fees · Audit"
            ],
            byPrincipal.Columns.Select(column => column.Title));
        Assert.All(byPrincipal.Columns.Skip(7).Take(3), column => Assert.Equal(WorkbookColumnKind.Money, column.Kind));
        Assert.Equal(["QDOS", 2, 1, 1, 2, 1, 1, 250m, 150m, 100m], byPrincipal.Rows.Single());

        // Item D: fee notes split by work too, so the page's Work choice has every figure.
        Assert.Equal(
            [
                "Month", "Principal",
                "Reports produced", "Reports produced · Inspection", "Reports produced · Audit",
                "Fee notes produced", "Fee notes produced · Inspection", "Fee notes produced · Audit",
                "Reports sent", "Reports sent · Inspection", "Reports sent · Audit",
                "Agreed fees", "Agreed fees · Inspection", "Agreed fees · Audit"
            ],
            sheets[2].Columns.Select(column => column.Title));
        Assert.Equal(["Aug 2026", "QDOS", 2, 1, 1, 2, 1, 1, 2, 1, 1, 250m, 150m, 100m], sheets[2].Rows.Single());

        Assert.Equal(["Principal", "Repairable", "Total loss", "Cash in lieu", "Contract repair", "Agrees", "Differs"],
            sheets[3].Columns.Select(column => column.Title));
        Assert.Equal(["QDOS", 1, 1, 0, 0, 1, 0], sheets[3].Rows.Single());

        // Turnaround keeps the period's times; the held figures are Queues', now.
        Assert.Equal(["Principal", "Time to produce", "Time to ready", "Time to send"], sheets[4].Columns.Select(column => column.Title));
        Assert.Equal(["QDOS", "20h", "22h", "40h"], sheets[4].Rows.Single());
        Assert.False(sheets[4].Totals);
        Assert.Equal(["Principal", "Currently held", "Oldest held since", "Triages", "Oldest Triage since"], sheets[5].Columns.Select(column => column.Title));
        Assert.Equal(["QDOS", "PCH"], sheets[5].Rows.Select(row => row[0]));
        Assert.Equal(["QDOS", 1, From, 2, From.AddDays(-3)], sheets[5].Rows[0]);
        Assert.All(sheets, sheet => Assert.All(sheet.Rows, row => Assert.Equal(sheet.Columns.Count, row.Count)));
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
                new(Guid.NewGuid(), "alex", 4, 3, 1, 2, TimeSpan.FromHours(1)),
                new(Guid.NewGuid(), "Smith, \"J\"", 0, 1),
                new(Guid.NewGuid(), "=SUM(A1:A2)", 0, 0)
            ]),
            new Words()));

        Assert.Equal(
            "Person,Queries received,Amendment requests,Reports sent,Audit reports sent,Received to sent\r\n"
            + "alex,3,1,4,2,1h\r\n"
            + "\"Smith, \"\"J\"\"\",1,0,0,0,\r\n"
            + "'=SUM(A1:A2),0,0,0,0,\r\n",
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
            [new(id, "QDOS", 2026, 13, 0, 0, 0, 0)],
            [new(id, " ", 2026, 8, 0, 0, 0, 0)],
            [new(Guid.Empty, "QDOS", 2026, 8, 0, 0, 0, 0)],
            [new(id, "QDOS", 2026, 8, -1, 0, 0, 0)],
            [new(id, "QDOS", 2026, 8, 0, 0, 0, -1)],
            [new(id, "QDOS", 2026, 8, 0, 0, 0, 0), new(id, "QDOS", 2026, 8, 1, 0, 0, 0)],
            // MI-02's Audit share is part of each total.
            [new(id, "QDOS", 2026, 8, 1, 0, 1, 10m, AuditReportsGenerated: 2)],
            [new(id, "QDOS", 2026, 8, 1, 0, 1, 10m, AuditSent: 2)],
            [new(id, "QDOS", 2026, 8, 1, 0, 1, 10m, AuditAgreedFeeTotal: 11m)],
            [new(id, "QDOS", 2026, 8, 1, 0, 1, 10m, AuditReportsGenerated: -1)],
            [new(id, "QDOS", 2026, 8, 1, 1, 1, 10m, AuditFeeNotesGenerated: 2)]
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
                new(Guid.NewGuid(), "QDOS", 2026, 8, 1, 0, 1, 0),
                new(Guid.NewGuid(), "EVA", 2026, 8, 1, 0, 0, 0),
                new(Guid.NewGuid(), "QDOS", 2026, 9, 1, 0, 0, 0)
            ]);
    }
}
